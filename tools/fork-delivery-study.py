#!/usr/bin/env python3
"""Explicit audited carry-forward of validation across the reviewed reporting-only fix.
Never resumes training across code changes. Final tests still run on the new build.
"""
import argparse,json,pathlib,zipfile,hashlib
from delivery_runs import environment_hash,digest,write_json
root=pathlib.Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser();p.add_argument('--parent',required=True);p.add_argument('--archive',type=pathlib.Path,required=True);p.add_argument('--tag',required=True);a=p.parse_args()
for name in [a.parent,a.tag]:
 if not name.replace('-','').replace('_','').isalnum():p.error('Invalid tag')
parent_path=root/'artifacts/studies'/a.parent/'study.json';parent=json.loads(parent_path.read_text())
if parent.get('flight_gate_met') is not True:raise ValueError('Parent flight gate is not complete/passed')
config_path=root/'training/studies/continuity-v2.json';config=json.loads(config_path.read_text())
if config['tag']!=a.parent:raise ValueError('Current configuration must still identify the parent')
files=[]
for folder in ['Assets/AeroLab/Core','Assets/AeroLab/Runtime','Assets/AeroLab/Editor']:files.extend((root/folder).rglob('*.cs'))
files += [root/p for p in ['Packages/manifest.json','Packages/packages-lock.json','tools/learn-delivery.py','tools/delivery_checkpoint_hooks.py','tools/flight_feature_policy.py','tools/prepare-mlagents.py']]
changes={};h=hashlib.sha256()
with zipfile.ZipFile(a.archive) as z:
 archived_config=z.read('training/studies/continuity-v2.json')
 if hashlib.sha256(archived_config).hexdigest()!=parent['identity']['config_sha256']:raise ValueError('Archived parent configuration mismatch')
 for path in sorted(files):
  name=path.relative_to(root).as_posix();old=z.read(name);h.update(name.encode()+b'\0'+old+b'\0')
  if old!=path.read_bytes():changes[name]={'before':hashlib.sha256(old).hexdigest(),'after':digest(path)}
if h.hexdigest()!=parent['identity']['environment_sha256']:raise ValueError('Archive is not the original parent source')
expected={'Assets/AeroLab/Runtime/DeliveryLab.cs','Assets/AeroLab/Runtime/RuntimeSmokeProbe.cs'}
if set(changes)!=expected:raise ValueError('Only the reviewed report-session naming/atomic-counter/smoke-error fix may be carried forward')
approved_after={'Assets/AeroLab/Runtime/DeliveryLab.cs': '23f2c5fa9e9c65ad141b19b7d81c75d9522fb836da7641aa55f5a27bf418f634', 'Assets/AeroLab/Runtime/RuntimeSmokeProbe.cs': '1486478298380fc4fb61e00ea74e3f5b051389b0aac6209ccb160f744fb2b7c4'}
if any(changes[name]['after']!=checksum for name,checksum in approved_after.items()):raise ValueError('Reporting fix differs from the reviewed source; revalidation required')
# This explicit command records the reviewed source diff. It is not a general compatibility bypass.
audit={'parent':a.parent,'parent_state_sha256':digest(parent_path),'archive_sha256':digest(a.archive),
 'changes':changes,'reviewed_reason':'Unique default report session directories; counters advance only after durable report write; smoke fails on AeroLab runtime errors. Physics, observations, actions and agent adapters unchanged.',
 'qualification_build':'Original immutable parent build; final evaluation uses the new build.'}
config['tag']=a.tag;config['inherited_validation']=audit
out=root/'artifacts/studies'/a.tag;out.mkdir(parents=True,exist_ok=False)
# Keep the original configuration next to its archived study.
write_json(root/'artifacts/studies'/a.parent/'configuration.json',json.loads(archived_config))
write_json(config_path,config)
write_json(out/'study.json',{'identity':{'config_sha256':digest(config_path),'environment_sha256':environment_hash(root)},
 'tag':a.tag,'flight_validation':parent['flight_validation'],'flight_gate_met':True,'inherited_validation':audit,
 'dispatch_validation':{},'test':{},'complete':False})
print(out/'study.json')
