#!/usr/bin/env python3
"""DAgger experiment with the shared C# simulator over JSON-lines (no Python physics).
Any bridge evaluation is diagnostic, not the Unity qualification gate.
"""
import argparse,json,pathlib,subprocess,os,hashlib,shutil
import numpy as np
import torch,yaml
from mlagents.plugins.trainer_type import register_trainer_plugins
from mlagents.trainers.settings import RunOptions
from mlagents.trainers.demo_loader import demo_to_buffer
from mlagents.trainers.policy.torch_policy import TorchPolicy
from mlagents.trainers.torch_entities.networks import SimpleActor
from mlagents.trainers.model_saver.torch_model_saver import TorchModelSaver
root=pathlib.Path(__file__).resolve().parents[1];p=argparse.ArgumentParser();p.add_argument('--source-run',required=True);p.add_argument('--output-run',required=True);p.add_argument('--demo',type=pathlib.Path,required=True);p.add_argument('--seed',type=int,choices=[11,29,47],required=True);p.add_argument('--iterations',type=int,default=3);p.add_argument('--episodes',type=int,default=30);p.add_argument('--epochs',type=int,default=30);p.add_argument('--control-features-only',action='store_true');p.add_argument('--stage',type=int,choices=[1,3],default=1);p.add_argument('--noisy',action='store_true');p.add_argument('--teacher-probability',type=float,default=.5);p.add_argument('--scenario-start',type=int,default=500);p.add_argument('--eval-only',action='store_true');a=p.parse_args()
for v in [a.source_run,a.output_run]:
 if not v.replace('-','').replace('_','').isalnum():p.error('Invalid run name')
if not 0<=a.teacher_probability<=1:p.error('Teacher probability must be 0..1')
if not 0<=a.scenario_start or a.scenario_start+a.iterations*100+a.seed+a.episodes>=8000:p.error('Bridge scenarios must remain in the training split')
source=root/'results'/a.source_run;out=root/'results'/a.output_run;out.mkdir(exist_ok=False)
torch.set_num_threads(2);torch.manual_seed(a.seed);rng=np.random.default_rng(a.seed)
register_trainer_plugins();cfg=yaml.safe_load((source/'configuration.yaml').read_text());settings=RunOptions.from_dict(cfg);spec,_=demo_to_buffer(str(a.demo.resolve()),1)
policy=TorchPolicy(a.seed,spec,settings.behaviors['DeliveryFlight'].network_settings,SimpleActor,{'conditional_sigma':False,'tanh_squash':False});actor=policy.actor;actor.load_state_dict(torch.load(source/'DeliveryFlight/checkpoint.pt',map_location='cpu')['Policy'])
if a.control_features_only:
 from flight_feature_policy import mask_actor
 mask_actor(actor)
distribution=actor.action_model._continuous_distribution;distribution.log_sigma.requires_grad_(False)
def predict(x):
 encoding,_=actor.network_body([x]);return distribution.mu(encoding)/3
optimizer=torch.optim.Adam([v for v in actor.parameters() if v.requires_grad],lr=.0002)
records=[];data=[]
for iteration in range(a.iterations):
 actor.eval();beta=0 if a.eval_only else a.teacher_probability/(iteration+1)
 process=subprocess.Popen([str(root/'artifacts/tooling/dotnet/dotnet'),str(root/'tools/DeliveryRunner/bin/Release/net8.0/DeliveryRunner.dll'),'--policy-bridge',str(a.episodes),str(a.scenario_start+iteration*100+a.seed), str(a.stage),str(a.noisy).lower()],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True,bufsize=1)
 report=[];batch=[]
 for line in process.stdout:
  msg=json.loads(line)
  if 'report' in msg:report.append(msg['report']);continue
  obs=np.array(msg['observation'],dtype=np.float32);teacher=np.array(msg['teacher'],dtype=np.float32)
  with torch.no_grad():action=predict(torch.tensor(obs).unsqueeze(0))[0].clamp(-1,1).numpy()
  if rng.random()<beta:action=teacher
  process.stdin.write(json.dumps(action.tolist())+'\n');process.stdin.flush()
  if msg['phase'] in ('Outbound','Returning'):batch.append(np.concatenate([obs,teacher]))
 if process.wait()!=0:raise RuntimeError('C# bridge failed')
 np.save(out/f'iteration-{iteration}.npy',np.array(batch));data.extend(batch)
 record={'iteration':iteration,'teacher_probability':beta,'episodes':len(report),'operational_safe_episodes':sum(r['Result'] in ['Completed','Horizon'] and r['Delivered']>0 and r['SafeReturns']>0 for r in report),'safe_complete_roundtrips':sum(r['Result']=='Completed' and r['Delivered']>0 and r['SafeReturns']==r['Trips'] for r in report),'reports':report,'samples':len(batch)}
 print({k:v for k,v in record.items() if k!='reports'},flush=True)
 if not a.eval_only:
  actor.train();train=torch.tensor(np.array(data));losses=[]
  for epoch in range(a.epochs):
   total=0
   for idx in torch.randperm(len(train)).split(512):
    b=train[idx];loss=(predict(b[:,:26])-b[:,26:]).square().mean();optimizer.zero_grad();loss.backward();optimizer.step();total+=loss.item()*len(idx)
   losses.append(total/len(train))
  record['fit_mse']=losses
 records.append(record)
actor.eval();saver=TorchModelSaver(settings.behaviors['DeliveryFlight'],str(out/'DeliveryFlight'));saver.register(policy);path,_=saver.save_checkpoint('DeliveryFlight',0);shutil.copy2(path,out/'DeliveryFlight.onnx');shutil.copy2(source/'configuration.yaml',out/'configuration.yaml')
(out/'dagger.json').write_text(json.dumps({'method':'DAgger, scripted clearance teacher, C# shared core transport','source_run':a.source_run,'source_checkpoint_sha256':hashlib.sha256((source/'DeliveryFlight/checkpoint.pt').read_bytes()).hexdigest(),'seed':a.seed,'eval_only':a.eval_only,'control_features_only':a.control_features_only,'stage':a.stage,'noisy':a.noisy,'records':records,'qualification':'not_evaluated'},indent=2))
