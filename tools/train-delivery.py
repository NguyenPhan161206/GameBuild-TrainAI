#!/usr/bin/env python3
"""One explicit training stage. Resume/checkpoints and evaluation are separate commands."""
import argparse, os, subprocess, pathlib, yaml, json, hashlib, time, signal, fcntl, socket, uuid
from delivery_runs import digest, environment_hash, write_json, restore, checkpoint_step
root=pathlib.Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser()
p.add_argument('layer',choices=['flight','dispatch'])
p.add_argument('--env',type=pathlib.Path,default=root/'Builds/Delivery/AeroLabDelivery.x86_64')
p.add_argument('--seed',type=int,required=True,choices=[11,29,47])
p.add_argument('--stage',type=int,choices=range(4),default=0)
p.add_argument('--steps',type=int)
p.add_argument('--control-features-only',action='store_true',help='Explicitly keep flight goal X/Z, velocity and altitude columns only')
p.add_argument('--learning-rate',type=float,help='Optional PPO learning rate for explicit fine-tuning experiments')
p.add_argument('--entropy-coefficient',type=float,help='Optional PPO beta; recorded in the run configuration')
p.add_argument('--no-normalize',action='store_true',help='Keep physical observation scales, e.g. for supervised initialization')
p.add_argument('--demo',type=pathlib.Path,help='Optional verified vector demonstration file for PPO behavioral cloning')
p.add_argument('--time-scale',type=float,help='Physics stays at 50 Hz; changes wall-clock training speed only')
p.add_argument('--checkpoint-interval',type=int)
p.add_argument('--summary-freq',type=int)
p.add_argument('--batch-size',type=int,help='Explicit PPO minibatch size')
p.add_argument('--buffer-size',type=int,help='Explicit PPO rollout buffer size')
p.add_argument('--small-buffer',action='store_true',help='16-sample batches/64-sample buffer for wiring checks only')
p.add_argument('--initialize-from')
p.add_argument('--resume',action='store_true')
p.add_argument('--session-minutes',type=float,default=120)
p.add_argument('--stop-at-step',type=int,help='Session milestone; does not change the total PPO schedule')
p.add_argument('--checkpoint-seconds',type=float,default=300)
p.add_argument('--backup-dir',type=pathlib.Path,help='Durable snapshot directory, e.g. mounted Drive')
p.add_argument('--base-port',type=int)
p.add_argument('--flight-model',help='Frozen model resource name, required for dispatch')
p.add_argument('--noisy',action='store_true')
p.add_argument('--tag',default='',help='Optional unique run prefix; use smoke for wiring tests')
a=p.parse_args()
if not a.env.is_file():p.error('Build the Linux delivery player first: AeroLab > Build > Delivery Linux x64')
if a.layer=='dispatch' and not a.flight_model:p.error('--flight-model must name a frozen model in Assets/**/Resources')
config=yaml.safe_load((root/f'training/{a.layer}.yaml').read_text())
if a.demo:
 if not a.demo.is_file():p.error('--demo must be an existing demonstration file')
 behavior=config['behaviors']['DeliveryFlight' if a.layer=='flight' else 'DeliveryDispatch']
 behavior['network_settings']['normalize']=False
 behavior['behavioral_cloning']={'demo_path':str(a.demo.resolve()),'strength':1.0,'steps':0,'batch_size':256,'num_epoch':3,'samples_per_update':0}
if a.learning_rate is not None:
 if not 0<a.learning_rate<=1:p.error('--learning-rate must be 0..1')
 config['behaviors']['DeliveryFlight' if a.layer=='flight' else 'DeliveryDispatch']['hyperparameters']['learning_rate']=a.learning_rate
if a.entropy_coefficient is not None:
 if not 0<=a.entropy_coefficient<=1:p.error('--entropy-coefficient must be 0..1')
 config['behaviors']['DeliveryFlight' if a.layer=='flight' else 'DeliveryDispatch']['hyperparameters']['beta']=a.entropy_coefficient
if a.no_normalize:config['behaviors']['DeliveryFlight' if a.layer=='flight' else 'DeliveryDispatch']['network_settings']['normalize']=False
if a.time_scale is not None:
 if not 1<=a.time_scale<=1000:p.error('--time-scale must be 1..1000')
 config['engine_settings']['time_scale']=a.time_scale
if a.small_buffer:
 behavior=config['behaviors']['DeliveryFlight' if a.layer=='flight' else 'DeliveryDispatch']
 behavior['hyperparameters'].update(batch_size=16,buffer_size=64);behavior['summary_freq']=64;behavior['checkpoint_interval']=128
if a.batch_size is not None or a.buffer_size is not None:
 hyper=config['behaviors']['DeliveryFlight' if a.layer=='flight' else 'DeliveryDispatch']['hyperparameters']
 if a.batch_size is not None:hyper['batch_size']=a.batch_size
 if a.buffer_size is not None:hyper['buffer_size']=a.buffer_size
 if hyper['batch_size']<1 or hyper['buffer_size']<hyper['batch_size'] or hyper['buffer_size']%hyper['batch_size']:p.error('Buffer must be a positive multiple of batch size')
for option in ['checkpoint_interval','summary_freq']:
 value=getattr(a,option)
 if value is not None:
  if value<1:p.error(option+' must be positive')
  config['behaviors']['DeliveryFlight' if a.layer=='flight' else 'DeliveryDispatch'][option]=value
if a.checkpoint_interval is None:
 config['behaviors']['DeliveryFlight' if a.layer=='flight' else 'DeliveryDispatch']['checkpoint_interval']=256 if a.layer=='flight' else 128
if a.steps:config['behaviors']['DeliveryFlight' if a.layer=='flight' else 'DeliveryDispatch']['max_steps']=a.steps
if a.tag and not a.tag.replace('-','').replace('_','').isalnum():p.error('Invalid tag')
run=(a.tag+'-' if a.tag else '')+f'{a.layer}-s{a.seed}-stage{a.stage}'+('-noisy' if a.noisy else '')
if a.resume and a.initialize_from:p.error('--resume and --initialize-from are mutually exclusive')
if not 0<a.session_minutes<=120 or a.checkpoint_seconds<=0:p.error('Session must be 0..120 minutes; checkpoint seconds must be positive')
behavior='DeliveryFlight' if a.layer=='flight' else 'DeliveryDispatch'
folder=root/'results'/run
folder.mkdir(parents=True,exist_ok=True)
lock=(folder/'.session.lock').open('a+')
try:fcntl.flock(lock,fcntl.LOCK_EX|fcntl.LOCK_NB)
except BlockingIOError:p.error('This run already has an active writer')
# Resolve an immutable build once; a launcher switch must not change a running experiment.
player=a.env.resolve()
prior_manifest=folder/'run.json'
pinned=json.loads(prior_manifest.read_text())['identity'].get('build_version') if a.resume and prior_manifest.exists() else None
if pinned and a.env.resolve()==(root/'Builds/Delivery/AeroLabDelivery.x86_64').resolve():
 if len(pinned)!=32 or any(c not in '0123456789abcdef' for c in pinned):p.error('Invalid pinned build ID')
 player=root/'Builds/DeliveryVersions'/pinned/'AeroLabDelivery.x86_64'

if player.parent.name=='Delivery' and (player.parent/'current.json').is_file():
 version=json.loads((player.parent/'current.json').read_text())['version']
 if len(version)!=32 or any(c not in '0123456789abcdef' for c in version):p.error('Invalid build version')
 player=player.parent.parent/'DeliveryVersions'/version/'AeroLabDelivery.x86_64'
build_files=[player,player.parent/'UnityPlayer.so']+sorted((player.parent/'AeroLabDelivery_Data/Managed').glob('AeroLab.*.dll'))+sorted((player.parent/'AeroLabDelivery_Data').glob('*.assets'))
if not all(f.is_file() for f in build_files):p.error('Incomplete Linux build')
canonical=json.loads(json.dumps(config))
if a.demo:canonical['behaviors'][behavior]['behavioral_cloning']['demo_path']='dataset:'+digest(a.demo)
from importlib.metadata import version
import platform
versions={'python':platform.python_version(),'mlagents':version('mlagents'),'mlagents-envs':version('mlagents-envs'),'torch':version('torch')}
if versions!={'python':'3.10.12','mlagents':'1.1.0','mlagents-envs':'1.1.0','torch':'2.2.2+cpu'}:p.error('Use the pinned CPU environment: '+str(versions))
identity={'schema':1,'behavior':behavior,'seed':a.seed,'stage':a.stage,'noisy':a.noisy,
 'control_features_only':a.control_features_only,'environment_sha256':environment_hash(root),
 'config':canonical,'versions':versions,
 'tooling':{name:digest(root/'tools'/name) for name in ['train-delivery.py','learn-delivery.py','delivery_runs.py','delivery_checkpoint_hooks.py','flight_feature_policy.py']},
 'build_version':player.parent.name,'build':{str(f.relative_to(player.parent)):digest(f) for f in build_files},
 'flight_model_sha256':digest(root/'Assets/AeroLab/Resources'/(a.flight_model+'.onnx')) if a.flight_model else None,
 'demonstration_sha256':digest(a.demo) if a.demo else None}
manifest=folder/'run.json'
if a.resume:
 if not manifest.exists():p.error('Legacy run has no portable manifest; use a new tag and --initialize-from')
 state=json.loads(manifest.read_text())
 if state['identity']!=identity:p.error('Environment/configuration/dependencies differ; create a new experiment')
 before,recovery=restore(folder,behavior)
else:
 if manifest.exists() or (folder/behavior).exists():p.error('Run exists; use --resume or a new tag')
 state={'run_id':run,'identity':identity,'sessions':[],'status':'new'}
 before=0;recovery=[]
session_id=time.strftime('%Y%m%dT%H%M%S')+'-'+uuid.uuid4().hex[:8]
reports=root/'artifacts/training-episodes'/run/session_id
seed_start=a.seed*100
prior=list((root/'artifacts/training-episodes'/run).rglob('episode-*.json'))
if prior:
 latest=max(prior,key=lambda f:f.stat().st_mtime)
 seed_start=(json.loads(latest.read_text())['Seed']+1)%8000
config_dir=root/'artifacts/training-configs';config_dir.mkdir(parents=True,exist_ok=True)
config_path=config_dir/(run+'.yaml')
config_path.write_text(yaml.safe_dump(config,sort_keys=False))
# The SDK writes the same resolved configuration at startup; keep it available to snapshots.
(folder/'configuration.yaml').write_text(yaml.safe_dump(config,sort_keys=False))
args=[str(root/'.venv/bin/python'),str(root/'tools/learn-delivery.py'),str(config_path),
 '--env',str(player),'--run-id',run,'--seed',str(a.seed),'--results-dir',str(root/'results'),
 '--num-envs','1','--base-port',str(a.base_port if a.base_port is not None else 20000+int(hashlib.sha256(run.encode()).hexdigest()[:6],16)%20000),
 '--no-graphics','--timeout-wait','120']
if a.initialize_from:args+=['--initialize-from',a.initialize_from]
if a.resume:args+=['--resume']
else:args+=['--force'] # Only a newly-created, locked metadata directory; prior training was rejected above.
args+=['--env-args','--delivery-mode',a.layer,'--stage',str(a.stage),'--scenario-seed',str(seed_start),
 '--reports',str(reports),'--noisy',str(a.noisy).lower()]
if a.flight_model:args+=['--flight-model',a.flight_model]
env=os.environ.copy();env.update(OMP_NUM_THREADS='2',MKL_NUM_THREADS='2',OPENBLAS_NUM_THREADS='2',
 XDG_CONFIG_HOME=str(root/'artifacts/cache/player-config'),AEROLAB_CHECKPOINT_SECONDS=str(a.checkpoint_seconds))
env.pop('AEROLAB_FLIGHT_CONTROL_FEATURES',None)
if a.control_features_only:
 if a.layer!='flight':p.error('Control feature selection is flight-only')
 env['AEROLAB_FLIGHT_CONTROL_FEATURES']='1'
if a.backup_dir:env['AEROLAB_BACKUP_DIR']=str(a.backup_dir.resolve())
else:env.pop('AEROLAB_BACKUP_DIR',None)
env.pop('AEROLAB_STOP_AT_STEP',None)
if a.stop_at_step:
 if a.stop_at_step<1 or a.stop_at_step>config['behaviors'][behavior]['max_steps']:p.error('Milestone outside run budget')
 env['AEROLAB_STOP_AT_STEP']=str(a.stop_at_step)
session={'id':session_id,'host':socket.gethostname(),'started':time.time(),'before_step':before,
 'scenario_seed_start':seed_start,'recovery':recovery,'args':args}
state['sessions'].append(session);state['status']='running';write_json(manifest,state)
write_json(root/'artifacts'/(run+'-invocation.json'),{'args':args,'training_seed':a.seed,'identity':identity})
started=time.monotonic();requested=[False]
def stop(signum=None,frame=None):requested[0]=True
signal.signal(signal.SIGINT,stop);signal.signal(signal.SIGTERM,stop)
process=None
try:
 process=subprocess.Popen(args,cwd=root,env=env,start_new_session=True)
 deadline=started+max(0,a.session_minutes*60-min(300,a.session_minutes*6))
 while process.poll() is None:
  if requested[0] or time.monotonic()>=deadline:
   session['stop_requested']=time.time();os.killpg(process.pid,signal.SIGINT)
   try:process.wait(timeout=300)
   except subprocess.TimeoutExpired:
    os.killpg(process.pid,signal.SIGTERM)
    try:process.wait(timeout=10)
    except subprocess.TimeoutExpired:os.killpg(process.pid,signal.SIGKILL);process.wait()
   break
  time.sleep(.5)
 session['exit_code']=process.returncode
 try:after,recovery=restore(folder,behavior)
 except ValueError:
  after=before;recovery=[{'reason':'No verified training checkpoint written'}]
 session.update(after_step=after,recovery_after=recovery,ended=time.time())
 if process.returncode!=0:state['status']='error'
 elif after>=config['behaviors'][behavior]['max_steps']:state['status']='budget_complete'
 elif after>0:state['status']='saved_for_resume'
 else:state['status']='error'
finally:
 if process is not None and process.poll() is None:
  os.killpg(process.pid,signal.SIGTERM);process.wait(timeout=30)
 session.setdefault('ended',time.time());write_json(manifest,state)
print(json.dumps({'run':run,'status':state['status'],'after_step':session.get('after_step')},indent=2))
if state['status']=='error':raise SystemExit(1)
