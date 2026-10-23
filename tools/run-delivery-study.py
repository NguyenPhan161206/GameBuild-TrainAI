#!/usr/bin/env python3
"""Gated, resumable continuity study. Training stops cleanly at session boundaries."""
import argparse,json,pathlib,subprocess,sys,statistics
from delivery_runs import write_json,environment_hash,digest
root=pathlib.Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser();p.add_argument('--config',type=pathlib.Path,default=root/'training/studies/continuity-v2.json')
p.add_argument('--action',choices=['status','flight-validation','dispatch-train','dispatch-validation','final','package'],default='status')
p.add_argument('--check-project',type=pathlib.Path,default=root/'artifacts/unity-check')
p.add_argument('--session-minutes',type=float,default=120);p.add_argument('--backup-dir',type=pathlib.Path)
a=p.parse_args();cfg=json.loads(a.config.read_text());out=root/'artifacts/studies'/cfg['tag'];out.mkdir(parents=True,exist_ok=True)
state_path=out/'study.json';identity={'config_sha256':digest(a.config),'environment_sha256':environment_hash(root)}
state=json.loads(state_path.read_text()) if state_path.exists() else {'identity':identity,'tag':cfg['tag'],'flight_validation':{},'dispatch_validation':{},'test':{},'complete':False}
if state['identity']!=identity:raise ValueError('Study configuration/environment changed; use a new study tag')
py=str(root/'.venv/bin/python')
def save():write_json(state_path,state)
def run(script,*args):subprocess.run([py,str(root/'tools'/script),*map(str,args)],cwd=root,check=True)
def evaluate(seed,kind,noisy,split,stage,episodes=100,checkpoint=None):
 flight=cfg['flight_models'][str(seed)];dispatch=checkpoint if kind=='ai' else None
 name=f"{cfg['tag']}-s{seed}-{split}-stage{stage}-{kind}-"+('noisy' if noisy else 'ideal')+(('-'+checkpoint.split('/')[-1]) if checkpoint else '')
 folder=root/'artifacts/evaluations'/name
 args=['--mode','dispatch-eval' if dispatch else 'flight-eval','--flight-model',flight,'--split',split,'--stage',stage,'--episodes',episodes,
 '--rule','fifo' if kind=='fifo' else 'edd','--name',name,'--time-scale',cfg['time_scale']]
 if noisy:args+=['--noisy']
 if dispatch:args+=['--dispatch-model',dispatch]
 if folder.exists():
  recorded=json.loads((folder/'evaluation.json').read_text())
  version=recorded.get('build_version','')
  if len(version)==32 and all(c in '0123456789abcdef' for c in version):
   args+=['--env',root/'Builds/DeliveryVersions'/version/'AeroLabDelivery.x86_64']
  args+=['--resume']
 run('evaluate-delivery.py',*args)
 return json.loads((folder/'summary.json').read_text())
def flight_gate():
 rows=list(state['flight_validation'].values())
 return len(rows)==18 and all(r.get('requested_flight_sha256')==digest(root/'Assets/AeroLab/Resources'/(r['model']+'.onnx')) for r in rows) and all(r['target_met'] is True if r['stage']==1 and not r['noisy'] else r['safe_complete_roundtrips']>=90 if r['stage']==1 else r['logistics_target_met'] is True for r in rows)
def build():
 env=__import__('os').environ.copy();env['AEROLAB_CHECK_PROJECT']=str(a.check_project.resolve())
 subprocess.run(['bash','tools/check-delivery-unity.sh','build'],cwd=root,env=env,check=True)
 run('publish-delivery-build.py',a.check_project)
def freeze(model,name):
 target=root/'Assets/AeroLab/Resources'/(name+'.onnx')
 if target.exists():
  if digest(target)!=digest(model):raise ValueError('Frozen resource differs: '+name)
 else:run('freeze-delivery-model.py',model,'--layer','dispatch','--name',name.removeprefix('Models/'))
def aggregate(rows):
 keys=['on_time_rate','delivered','on_time','operational_safe_episodes','mean_energy','mean_distance']
 return {key:{'mean':statistics.mean(r[key] for r in rows),'sample_stdev':statistics.stdev(r[key] for r in rows)} for key in keys}
save()
if a.action=='flight-validation':
 for seed in cfg['seeds']:
  for noisy in [False,True]:
   for stage,kind in [(1,'edd'),(3,'fifo'),(3,'edd')]:
    key=f'{seed}-{stage}-{kind}-{noisy}'
    state['flight_validation'][key]=evaluate(seed,kind,noisy,'validation',stage);save()
 state['flight_gate_met']=flight_gate();save()
elif a.action=='dispatch-train':
 if not flight_gate():raise SystemExit('Flight continuity gate has not passed; dispatch research is gated.')
 # Fixed full schedule; stop-at-step creates resumable checkpoints without resetting it.
 milestones=cfg['dispatch_checkpoints'];previous=None;chosen=None;best=None
 for milestone in milestones:
  stage_rows=[]
  for seed in cfg['seeds']:
   run_id=f"{cfg['dispatch_tag']}-dispatch-s{seed}-stage3-noisy";folder=root/'results'/run_id
   current=json.loads((folder/'run.json').read_text()) if (folder/'run.json').exists() else None
   last=current['sessions'][-1].get('after_step',0) if current else 0
   if last<milestone:
    args=['dispatch','--seed',seed,'--stage',3,'--noisy','--steps',32768,'--batch-size',32,'--buffer-size',128,
     '--checkpoint-interval',128,'--summary-freq',128,'--time-scale',cfg['time_scale'],'--flight-model',cfg['flight_models'][str(seed)],
     '--tag',cfg['dispatch_tag'],'--session-minutes',a.session_minutes,'--stop-at-step',milestone]
    if current:args+=['--resume']
    if a.backup_dir:args+=['--backup-dir',a.backup_dir]
    run('train-delivery.py',*args)
    current=json.loads((folder/'run.json').read_text());state.setdefault('dispatch_runs',{})[str(seed)]=current;save()
    if current['sessions'][-1].get('after_step',0)<milestone:raise SystemExit('Session saved; resume dispatch-train next session.')
   if current and current['sessions'][-1].get('after_step',0)>milestone+256 and not (root/'Assets/AeroLab/Resources'/(cfg['dispatch_models'][str(seed)]+f'-step{milestone}.onnx')).exists():
    raise ValueError('Missing historical milestone export; refusing to label newer weights as an earlier checkpoint')
   name=cfg['dispatch_models'][str(seed)]+f'-step{milestone}'
   # Resuming later stages must reuse the original immutable milestone, not a newer final export.
   if not (root/'Assets/AeroLab/Resources'/(name+'.onnx')).exists():freeze(folder/'DeliveryDispatch.onnx',name)
  build()
  for seed in cfg['seeds']:
   for noisy in [False,True]:
    name=cfg['dispatch_models'][str(seed)]+f'-step{milestone}'
    stage_rows.append(evaluate(seed,'ai',noisy,'validation',3,20,name))
  score={'mean_energy':statistics.mean(r['mean_energy'] for r in stage_rows),'on_time_rate':statistics.mean(r['on_time_rate'] for r in stage_rows),
         'incidents':sum(sum(v for k,v in r['outcomes'].items() if k not in ['Completed','Horizon']) for r in stage_rows),
         'critical':sum(sum(r['outcomes'].get(k,0) for k in ['Collision','OutOfBounds','OutOfEnergy']) for r in stage_rows)}
  state.setdefault('dispatch_milestones',{})[str(milestone)]={'score':score,'rows':stage_rows};save()
  if score['critical']:break
  improved=previous is None or score['on_time_rate']>=previous['on_time_rate']+.01 or score['incidents']<previous['incidents']
  ranking=(score['incidents'],-score['on_time_rate'],score['mean_energy'])
  if best is None or ranking<best:chosen=milestone;best=ranking
  if not improved:break
  previous=score
 if chosen is None:raise SystemExit('No dispatch milestone passed safety; evidence retained.')
 for seed in cfg['seeds']:
  source=root/'Assets/AeroLab/Resources'/(cfg['dispatch_models'][str(seed)]+f'-step{chosen}.onnx')
  freeze(source,cfg['dispatch_models'][str(seed)])
 state['selected_dispatch_milestone']=chosen;save();build()
elif a.action=='dispatch-validation':
 if not flight_gate():raise SystemExit('Flight gate has not passed.')
 for seed in cfg['seeds']:
  for noisy in [False,True]:
   key=f'{seed}-{noisy}';state['dispatch_validation'][key]=evaluate(seed,'ai',noisy,'validation',3,checkpoint=cfg['dispatch_models'][str(seed)]);save()
elif a.action=='final':
 if not flight_gate() or len(state['dispatch_validation'])!=6:raise SystemExit('Freeze candidates and complete validation before opening final test seeds.')
 for seed in cfg['seeds']:
  for noisy in [False,True]:
   for stage,kind in [(1,'edd'),(3,'fifo'),(3,'edd'),(3,'ai')]:
    key=f'{seed}-{stage}-{kind}-{noisy}'
    state['test'][key]=evaluate(seed,kind,noisy,'test',stage,checkpoint=cfg['dispatch_models'][str(seed)] if kind=='ai' else None);save()
 state['aggregates']={}
 for noisy in [False,True]:
  for stage,kind in [(1,'edd'),(3,'fifo'),(3,'edd'),(3,'ai')]:
   rows=[state['test'][f'{seed}-{stage}-{kind}-{noisy}'] for seed in cfg['seeds']]
   state['aggregates'][f'{stage}-{kind}-{noisy}']=aggregate(rows)
 state['complete']=True;state['qualified_flight']=all(r['safe_complete_roundtrips']>=90 for r in state['test'].values() if r['stage']==1);state['qualified_logistics']=all(r['logistics_target_met'] for r in state['test'].values() if r['stage']==3);save()
elif a.action=='package':
 for kind in ['source','linux','evidence','training']:
  run('delivery-transfer.py','pack','--kind',kind,'--output',out/(cfg['tag']+'-'+kind+'.zip'))
else:print(json.dumps(state,indent=2))
print(state_path)
