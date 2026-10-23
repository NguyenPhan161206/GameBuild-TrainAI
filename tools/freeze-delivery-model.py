#!/usr/bin/env python3
"""Copy a trained ONNX into Resources only with its evaluation evidence and checksum."""
import argparse,pathlib,json,hashlib,shutil,onnx,uuid
root=pathlib.Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser();p.add_argument('model',type=pathlib.Path);p.add_argument('--layer',choices=['flight','dispatch'],required=True);p.add_argument('--name',required=True);p.add_argument('--evaluation',type=pathlib.Path)
a=p.parse_args()
if not a.name.replace('-','').replace('_','').isalnum():p.error('Use a simple resource name')
evidence=json.loads(a.evaluation.read_text()) if a.evaluation else {"status":"not_evaluated","target_met":False};model=onnx.load(a.model);onnx.checker.check_model(model)
expected=26 if a.layer=='flight' else 60
inputs=[i for i in model.graph.input if i.name.startswith('obs_')]
if len(inputs)!=1 or inputs[0].type.tensor_type.shape.dim[-1].dim_value!=expected:raise RuntimeError('Observation shape does not match layer')
dest=root/'Assets/AeroLab/Resources/Models';dest.mkdir(parents=True,exist_ok=True)
target=dest/(a.name+'.onnx')
if target.exists():raise RuntimeError('Frozen resource already exists; choose a new name')
digest=hashlib.sha256(a.model.read_bytes()).hexdigest()
if a.evaluation:
 expected_field='flight_model_sha256' if a.layer=='flight' else 'dispatch_model_sha256'
 if evidence.get(expected_field)!=[digest]:raise RuntimeError('Evaluation evidence does not identify this exact model checksum')
qualified=a.layer=='flight' and evidence.get('target_met') is True and evidence.get('mode')=='flight-eval' and evidence.get('stage')==1 and evidence.get('episodes')==100 and evidence.get('noisy') is False
source_run=a.model.parent
training={}
if (source_run/'run.json').exists():
 run_meta=json.loads((source_run/'run.json').read_text());training={'run':source_run.name,'training_seed':run_meta['identity']['seed'],'environment_sha256':run_meta['identity']['environment_sha256'],'method':'PPO; source initialization recorded in sessions'}
shutil.copyfile(a.model,target)
(dest/(a.name+'.provenance.json')).write_text(json.dumps({'resource':'Models/'+a.name,'sha256':digest,'layer':a.layer,'observation_size':expected,'evaluation':evidence,'qualified':qualified,'training':training,'qualification_scope':{'stage':evidence.get('stage'),'noisy':evidence.get('noisy'),'source_sha256':evidence.get('source_sha256',[])}},indent=2))
for f in [dest,target,dest/(a.name+'.provenance.json')]:
 meta=pathlib.Path(str(f)+'.meta')
 if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n')
print('Frozen resource: Models/'+a.name+'; rebuild player before using it')
