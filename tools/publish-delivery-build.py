#!/usr/bin/env python3
"""Copy a verified build from a verification project and switch only the launcher."""
import argparse,json,pathlib,shutil,os
root=pathlib.Path(__file__).resolve().parents[1];p=argparse.ArgumentParser();p.add_argument('project',type=pathlib.Path);a=p.parse_args()
v=json.loads((a.project/'Builds/Delivery/current.json').read_text())['version']
if len(v)!=32 or any(c not in '0123456789abcdef' for c in v):raise ValueError('Invalid build version')
source=a.project/'Builds/DeliveryVersions'/v;dest=root/'Builds/DeliveryVersions'/v
if not (source/'AeroLabDelivery_Data/Plugins/x86_64/libgrpc_csharp_ext.x64.so').is_file():raise ValueError('Missing native communicator')
if source.resolve()!=dest.resolve() and not dest.exists():shutil.copytree(source,dest)
(root/'Builds/Delivery').mkdir(exist_ok=True)
if a.project.resolve()!=root:
 for name in ['AeroLabDelivery.x86_64','current.json']:
  temp=root/'Builds/Delivery'/(name+'.new');shutil.copy2(a.project/'Builds/Delivery'/name,temp);os.replace(temp,root/'Builds/Delivery'/name)
print(v)
