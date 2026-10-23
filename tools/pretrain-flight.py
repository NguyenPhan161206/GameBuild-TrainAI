#!/usr/bin/env python3
"""Fit an ML-Agents actor to synthetic C# controller labels; never qualifies it.
The dataset contains observations, not a second physics implementation.
"""
import argparse,hashlib,json,pathlib,shutil,struct,time
import numpy as np
import torch,yaml
from mlagents.plugins.trainer_type import register_trainer_plugins
from mlagents.trainers.settings import RunOptions
from mlagents.trainers.demo_loader import demo_to_buffer
from mlagents.trainers.policy.torch_policy import TorchPolicy
from mlagents.trainers.torch_entities.networks import SimpleActor
from mlagents.trainers.model_saver.torch_model_saver import TorchModelSaver
root=pathlib.Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser();p.add_argument('--dataset',type=pathlib.Path,required=True);p.add_argument('--spec-demo',type=pathlib.Path,required=True);p.add_argument('--seed',type=int,choices=[11,29,47],required=True);p.add_argument('--epochs',type=int,default=120);p.add_argument('--tag',default='supervised-v1');a=p.parse_args()
if not a.tag.replace('-','').replace('_','').isalnum():p.error('Invalid tag')
run=f'{a.tag}-s{a.seed}';out=root/'results'/run;out.mkdir(exist_ok=False)
raw=a.dataset.read_bytes();magic,n,o,c=struct.unpack('<8siii',raw[:20]);assert magic==b'AEROLAB1' and o==26 and c==3 and len(raw)==20+n*29*4
samples=np.frombuffer(raw,dtype='<f4',offset=20).reshape(n,29).copy()
torch.set_num_threads(2);torch.manual_seed(a.seed);np.random.seed(a.seed)
register_trainer_plugins();cfg=yaml.safe_load((root/'training/flight.yaml').read_text());cfg['behaviors']['DeliveryFlight']['network_settings']['normalize']=False
settings=RunOptions.from_dict(cfg);spec,_=demo_to_buffer(str(a.spec_demo.resolve()),1);assert spec.observation_specs[0].shape==(26,) and spec.action_spec.continuous_size==3
policy=TorchPolicy(a.seed,spec,settings.behaviors['DeliveryFlight'].network_settings,SimpleActor,{'conditional_sigma':False,'tanh_squash':False});actor=policy.actor
# Labels use Unity action units; ML-Agents exports clip(raw_mean,-3,3)/3.
distribution=actor.action_model._continuous_distribution
distribution.log_sigma.data.fill_(-4);distribution.log_sigma.requires_grad_(False)
def prediction(x):
 encoding,_=actor.network_body([x]);return distribution.mu(encoding)/3
order=np.random.default_rng(811).permutation(n);cut=int(n*.8);train=torch.tensor(samples[order[:cut]]);valid=torch.tensor(samples[order[cut:]])
optimizer=torch.optim.Adam([v for v in actor.parameters() if v.requires_grad],lr=.001)
scheduler=torch.optim.lr_scheduler.CosineAnnealingLR(optimizer,a.epochs,eta_min=.00003)
history=[];start=time.time()
for epoch in range(a.epochs):
 actor.train();indices=torch.randperm(len(train));total=0
 for idx in indices.split(512):
  batch=train[idx];loss=(prediction(batch[:,:26])-batch[:,26:]).square().mean();optimizer.zero_grad();loss.backward();optimizer.step();total+=loss.item()*len(idx)
 scheduler.step();actor.eval()
 with torch.no_grad():
  losses=[(prediction(b[:,:26])-b[:,26:]).square().mean().item()*len(b) for b in valid.split(2048)]
 record={'epoch':epoch+1,'train_mse':total/len(train),'validation_mse':sum(losses)/len(valid)};history.append(record)
 if epoch%10==0 or epoch+1==a.epochs:print(record,flush=True)
actor.eval();saver=TorchModelSaver(settings.behaviors['DeliveryFlight'],str(out/'DeliveryFlight'));saver.register(policy);path,_=saver.save_checkpoint('DeliveryFlight',0);shutil.copy2(path,out/'DeliveryFlight.onnx')
(out/'configuration.yaml').write_text(yaml.safe_dump(cfg,sort_keys=False))
(out/'pretraining.json').write_text(json.dumps({'method':'supervised state coverage pretraining of ML-Agents actor','seed':a.seed,'dataset_sha256':hashlib.sha256(raw).hexdigest(),'dataset_metadata':json.loads(pathlib.Path(str(a.dataset)+'.json').read_text()),'physics_in_python':False,'actor_architecture':'128x2','normalization':False,'epochs':a.epochs,'fit_validation_is_not_mission_evaluation':True,'elapsed_seconds':time.time()-start,'history':history,'qualification':'not_evaluated'},indent=2))
print(out/'DeliveryFlight.onnx',flush=True)
