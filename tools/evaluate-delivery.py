#!/usr/bin/env python3
"""Resumable frozen-player evaluation; one immutable build and one report per seed."""
import argparse,pathlib,subprocess,json,os,fcntl,time
from delivery_runs import digest,write_json
from delivery_metrics import summarize
root=pathlib.Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser()
p.add_argument('--env',type=pathlib.Path,default=root/'Builds/Delivery/AeroLabDelivery.x86_64')
p.add_argument('--mode',choices=['reference','flight-eval','dispatch-eval'],default='reference')
p.add_argument('--flight-model');p.add_argument('--dispatch-model')
p.add_argument('--split',choices=['validation','test','diagnostic'],default='test')
p.add_argument('--seed-start',type=int)
p.add_argument('--time-scale',type=float,default=200)
p.add_argument('--episodes',type=int,default=100);p.add_argument('--stage',type=int,default=3,choices=range(4))
p.add_argument('--rule',choices=['fifo','edd'],default='edd');p.add_argument('--noisy',action='store_true')
p.add_argument('--name',required=True);p.add_argument('--resume',action='store_true');p.add_argument('--trace',action='store_true')
p.add_argument('--chunk-size',type=int,default=100)
a=p.parse_args()
if a.mode!='reference' and not a.flight_model:p.error('--flight-model required')
if a.mode=='dispatch-eval' and not a.dispatch_model:p.error('--dispatch-model required')
if not 1<=a.episodes<=100 or not 1<=a.time_scale<=1000 or not 1<=a.chunk_size<=100:p.error('Invalid count/speed')
if not a.name.replace('-','').replace('_','').isalnum():p.error('Use a simple unique name')
if a.split=='diagnostic' and a.seed_start is None:p.error('Diagnostic requires --seed-start')
start=a.seed_start if a.seed_start is not None else (11000 if a.split=='validation' else 30000)
if a.split=='validation' and not 11000<=start<=11100-a.episodes:p.error('Validation range is 11000..11099')
if a.split=='test' and not 30000<=start<=30100-a.episodes:p.error('Final test range is 30000..30099')
player=a.env.resolve()
if (player.parent/'current.json').exists():
 version=json.loads((player.parent/'current.json').read_text())['version']
 if len(version)!=32 or any(c not in '0123456789abcdef' for c in version):p.error('Invalid build ID')
 player=player.parent.parent/'DeliveryVersions'/version/'AeroLabDelivery.x86_64'
settings={'schema':3,'build_version':player.parent.name,
 'player_assets_sha256':{str(f.relative_to(player.parent)):digest(f) for f in sorted((player.parent/'AeroLabDelivery_Data').glob('*.assets'))},'mode':a.mode,'split':a.split,'stage':a.stage,'noisy':a.noisy,'rule':a.rule,
 'model':a.flight_model,'dispatch_model':a.dispatch_model,'seed_start':start,'requested_episodes':a.episodes,
 'requested_time_scale':a.time_scale,'trace':a.trace,
 'build_sha256':{f.name:digest(f) for f in [player,player.parent/'AeroLabDelivery_Data/Managed/AeroLab.Runtime.dll',player.parent/'AeroLabDelivery_Data/Managed/AeroLab.Core.dll']},
 'requested_flight_sha256':digest(root/'Assets/AeroLab/Resources'/(a.flight_model+'.onnx')) if a.flight_model else '',
 'requested_dispatch_sha256':digest(root/'Assets/AeroLab/Resources'/(a.dispatch_model+'.onnx')) if a.dispatch_model else ''}
out=root/'artifacts/evaluations'/a.name;out.mkdir(parents=True,exist_ok=a.resume)
lock=(out/'.evaluation.lock').open('a+');fcntl.flock(lock,fcntl.LOCK_EX|fcntl.LOCK_NB)
manifest=out/'evaluation.json'
if a.resume:
 if not manifest.exists() or json.loads(manifest.read_text())!=settings:raise ValueError('Evaluation identity differs; use a new name')
else:write_json(manifest,settings)
def read_reports():
 rows={}
 for file in out.glob('episode-*.json'):
  row=json.loads(file.read_text());seed=row['Seed']
  if seed in rows or not start<=seed<start+a.episodes:raise ValueError('Duplicate/unexpected episode seed')
  for field,key in [('FlightModelSha256','requested_flight_sha256'),('DispatchModelSha256','requested_dispatch_sha256')]:
   if settings[key] and row.get(field)!=settings[key]:raise ValueError('Player model checksum differs from requested resource')
  if row['Result']=='None':raise ValueError('Incomplete episode report')
  rows[seed]=row
 return rows
rows=read_reports();env=os.environ.copy();env['XDG_CONFIG_HOME']=str(root/'artifacts/cache/player-config')
while len(rows)<a.episodes:
 missing=next(s for s in range(start,start+a.episodes) if s not in rows);count=1
 while count<a.chunk_size and missing+count<start+a.episodes and missing+count not in rows:count+=1
 log=out/f'player-{missing}-{time.time_ns()}.log'
 args=[str(player),'-batchmode','-nographics','-job-worker-count','2','-logFile',str(log),
 '--delivery-mode',a.mode,'--stage',str(a.stage),'--scenario-seed',str(missing),'--episodes',str(count),
 '--reports',str(out),'--rule',a.rule,'--evaluation-time-scale',str(a.time_scale),'--noisy',str(a.noisy).lower()]
 if a.flight_model:args+=['--flight-model',a.flight_model]
 if a.dispatch_model:args+=['--dispatch-model',a.dispatch_model]
 if a.trace:args+=['--trace-directory',str(out/'traces')]
 subprocess.run(args,cwd=root,env=env,check=True,timeout=count*180+120)
 fresh=read_reports()
 if len(fresh)<=len(rows):raise RuntimeError('Player returned without new reports')
 rows=fresh;write_json(out/'progress.json',{'completed':len(rows),'expected':a.episodes})
summary=summarize([rows[s] for s in sorted(rows)],settings)
write_json(out/'summary.json',summary);print(json.dumps(summary,indent=2))
