#!/usr/bin/env python3
"""Record shared-C#-core trajectories for a trusted ML-Agents checkpoint, without fitting."""
import argparse,pathlib,json,subprocess,os
import torch,numpy as np,yaml
from mlagents.plugins.trainer_type import register_trainer_plugins
from mlagents.trainers.settings import RunOptions
from mlagents.trainers.demo_loader import demo_to_buffer
from mlagents.trainers.policy.torch_policy import TorchPolicy
from mlagents.trainers.torch_entities.networks import SimpleActor
from delivery_runs import write_json,digest
root=pathlib.Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser();p.add_argument('--run',required=True);p.add_argument('--seed',type=int,default=10000)
p.add_argument('--episodes',type=int,default=1);p.add_argument('--stage',type=int,default=3,choices=[1,3]);p.add_argument('--noisy',action='store_true')
p.add_argument('--pattern',choices=['generated','repeat','cycle'],default='generated');p.add_argument('--teacher',action='store_true')
p.add_argument('--neutral-nuisance',action='store_true',help='Diagnostic intervention only; never qualification')
p.add_argument('--output',type=pathlib.Path,required=True)
a=p.parse_args()
if not a.run.replace('-','').replace('_','').isalnum():p.error('Invalid run')
a.output.mkdir(parents=True,exist_ok=False);torch.set_num_threads(2)
source=root/'results'/a.run;register_trainer_plugins()
cfg=RunOptions.from_dict(yaml.safe_load((source/'configuration.yaml').read_text()))
spec,_=demo_to_buffer(str(root/'artifacts/datasets/expert-stage0-v1.demo'),1)
policy=TorchPolicy(11,spec,cfg.behaviors['DeliveryFlight'].network_settings,SimpleActor,{'conditional_sigma':False,'tanh_squash':False})
checkpoint=source/'DeliveryFlight/checkpoint.pt';policy.actor.load_state_dict(torch.load(checkpoint,map_location='cpu')['Policy']);policy.actor.eval()
process=subprocess.Popen([str(root/'artifacts/tooling/dotnet/dotnet'),str(root/'tools/DeliveryRunner/bin/Release/net8.0/DeliveryRunner.dll'),
 '--policy-bridge',str(a.episodes),str(a.seed),str(a.stage),str(a.noisy).lower(),a.pattern],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True,bufsize=1)
reports=[]
try:
 with (a.output/'trace.jsonl').open('w') as trace:
  for line in process.stdout:
   msg=json.loads(line)
   if 'report' in msg:reports.append(msg['report']);continue
   obs=np.asarray(msg['observation'],dtype=np.float32)
   if a.neutral_nuisance:
    obs=obs.copy();obs[20]=1;obs[24]=.7
   with torch.no_grad():
    x=torch.tensor(obs).unsqueeze(0)
    encoding,_=policy.actor.network_body([x])
    action=(policy.actor.action_model._continuous_distribution.mu(encoding)/3).clamp(-1,1)[0].numpy()
   if a.teacher:action=np.asarray(msg['teacher'])
   process.stdin.write(json.dumps(action.tolist())+'\n');process.stdin.flush()
   msg['command']=action.tolist();msg['actual_observation']=obs.tolist();trace.write(json.dumps(msg)+'\n')
 if process.wait()!=0:raise RuntimeError('C# bridge failed')
finally:
 if process.poll() is None:process.terminate();process.wait(timeout=10)
write_json(a.output/'diagnosis.json',{'run':a.run,'checkpoint_sha256':digest(checkpoint),'teacher':a.teacher,
 'neutral_nuisance':a.neutral_nuisance,'pattern':a.pattern,'native_unity_qualification':False,'reports':reports})
for row in reports:print({k:row[k] for k in ['Seed','Result','Delivered','SafeReturns','Elapsed','Legs']})
