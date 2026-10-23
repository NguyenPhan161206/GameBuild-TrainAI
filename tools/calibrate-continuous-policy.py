#!/usr/bin/env python3
"""Explicit latent-action unit calibration for ML-Agents 1.1 continuous BC exports.
This creates a new candidate and never overwrites or qualifies its source model.
"""
import argparse,pathlib,json,hashlib,shutil,yaml,torch
from mlagents.plugins.trainer_type import register_trainer_plugins
from mlagents.trainers.settings import RunOptions
from mlagents.trainers.demo_loader import demo_to_buffer
from mlagents.trainers.policy.torch_policy import TorchPolicy
from mlagents.trainers.torch_entities.networks import SimpleActor
from mlagents.trainers.torch_entities.model_serialization import ModelSerializer
root=pathlib.Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser();p.add_argument('--source-run',required=True);p.add_argument('--output-run',required=True);p.add_argument('--demo',type=pathlib.Path,required=True);p.add_argument('--factor',type=float,default=3)
a=p.parse_args()
for name in [a.source_run,a.output_run]:
 if not name.replace('-','').replace('_','').isalnum():p.error('Invalid run name')
if not 0<a.factor<=10:p.error('Factor must be 0..10')
source=root/'results'/a.source_run;out=root/'results'/a.output_run;out.mkdir(exist_ok=False)
register_trainer_plugins();settings=RunOptions.from_dict(yaml.safe_load((source/'configuration.yaml').read_text()))
network=settings.behaviors['DeliveryFlight'].network_settings
spec,_=demo_to_buffer(str(a.demo.resolve()),1)
if spec.action_spec.continuous_size!=3 or spec.action_spec.discrete_size:p.error('Expected 3 continuous actions')
policy=TorchPolicy(0,spec,network,SimpleActor,{'conditional_sigma':False,'tanh_squash':False})
checkpoint=source/'DeliveryFlight/checkpoint.pt';state=torch.load(checkpoint,map_location='cpu')
for key in ['action_model._continuous_distribution.mu.weight','action_model._continuous_distribution.mu.bias']:
 state['Policy'][key]=state['Policy'][key]*a.factor
policy.actor.load_state_dict(state['Policy']);policy.actor.eval()
ModelSerializer(policy).export_policy_model(str(out/'DeliveryFlight'))
(out/'DeliveryFlight').mkdir();torch.save(state,out/'DeliveryFlight/checkpoint.pt');shutil.copy2(source/'configuration.yaml',out/'configuration.yaml')
(out/'calibration.json').write_text(json.dumps({'source_run':a.source_run,'source_checkpoint_sha256':hashlib.sha256(checkpoint.read_bytes()).hexdigest(),'factor':a.factor,'reason':'SDK BC compares raw Gaussian actions with Unity-space demonstrations; SDK continuous export clips [-3,3] then divides by 3. Mean-head scaling is an explicit action-unit calibration.','qualification':'not_evaluated'},indent=2))
print(out/'DeliveryFlight.onnx')
