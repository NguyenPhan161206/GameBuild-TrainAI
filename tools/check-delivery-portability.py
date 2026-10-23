#!/usr/bin/env python3
"""Actual short PPO → bundle → relocated resume → restore-back acceptance check."""
import argparse,pathlib,subprocess,json,os
from delivery_runs import write_json,checkpoint_step
root=pathlib.Path(__file__).resolve().parents[1];p=argparse.ArgumentParser();p.add_argument('--tag',required=True);a=p.parse_args()
if not a.tag.replace('-','').replace('_','').isalnum():p.error('Invalid tag')
out=root/'artifacts/portability'/a.tag;out.mkdir(parents=True,exist_ok=False)
py=str(root/'.venv/bin/python')
def call(args,log,cwd=root):
 with (out/log).open('w') as stream:subprocess.run(args,cwd=cwd,stdout=stream,stderr=subprocess.STDOUT,check=True)
base=['flight','--seed','11','--stage','0','--steps','4096','--small-buffer','--checkpoint-interval','128','--tag',a.tag]
run_id=a.tag+'-flight-s11-stage0'
call([py,str(root/'tools/train-delivery.py'),*base,'--stop-at-step','512'],'first.log')
before=checkpoint_step(root/'results'/run_id/'DeliveryFlight/checkpoint.pt')
bundle=out/'training.zip'
call([py,str(root/'tools/delivery-transfer.py'),'pack','--kind','training','--run',run_id,'--output',str(bundle)],'pack.log')
relocated=out/'relocated'
call([py,str(root/'tools/delivery-transfer.py'),'unpack',str(bundle),'--destination',str(relocated)],'unpack.log')
# Reuse the exact installed interpreter; source/build/output paths are actually relocated.
(relocated/'.venv').symlink_to(root/'.venv',target_is_directory=True)
call([py,str(relocated/'tools/train-delivery.py'),*base,'--stop-at-step','1024','--resume'],'relocated.log',relocated)
after=checkpoint_step(relocated/'results'/run_id/'DeliveryFlight/checkpoint.pt')
if not after>before>=512:raise ValueError('Training progress did not resume')
if 'Failed to load for module' in (out/'relocated.log').read_text():raise ValueError('SDK silently initialized a training module')
return_bundle=out/'checkpoint-return.zip'
call([py,str(relocated/'tools/delivery-transfer.py'),'pack','--kind','checkpoint','--run',run_id,'--output',str(return_bundle)],'return-pack.log',relocated)
call([py,str(root/'tools/delivery-transfer.py'),'unpack',str(return_bundle),'--destination',str(root),'--restore-run'],'restore-back.log')
restored=checkpoint_step(root/'results'/run_id/'DeliveryFlight/checkpoint.pt')
if restored!=after:raise ValueError('Restore-back step differs')
write_json(out/'acceptance.json',{'passed':True,'before_step':before,'relocated_step':after,'restored_step':restored,
 'optimizer_modules_restored':True,'exact_inflight_episode_resume':False,'colab_executed':False})
print(out/'acceptance.json')
