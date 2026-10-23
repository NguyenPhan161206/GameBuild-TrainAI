#!/usr/bin/env python3
"""Create/verify portable AeroLab bundles. Checkpoints must be trusted project artifacts."""
import argparse,json,pathlib,zipfile,stat,os,shutil
from delivery_runs import digest,write_json,verify_snapshot,environment_hash
root=pathlib.Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser();sub=p.add_subparsers(dest='command',required=True)
pack=sub.add_parser('pack');pack.add_argument('--kind',choices=['training','source','linux','evidence','checkpoint'],required=True);pack.add_argument('--run');pack.add_argument('--output',type=pathlib.Path,required=True)
unpack=sub.add_parser('unpack');unpack.add_argument('archive',type=pathlib.Path);unpack.add_argument('--destination',type=pathlib.Path,required=True);unpack.add_argument('--restore-run',action='store_true',help='Merge a trusted checkpoint bundle into an existing identical run under destination')
a=p.parse_args()
if a.command=='unpack':
 destination=a.destination.resolve();destination.mkdir(parents=True,exist_ok=True)
 with zipfile.ZipFile(a.archive) as z:
  names=z.namelist()
  if len(set(names))!=len(names):raise ValueError('Duplicate archive names')
  for item in z.infolist():
   rel=pathlib.PurePosixPath(item.filename)
   if rel.is_absolute() or '..' in rel.parts or '\\' in item.filename or stat.S_ISLNK(item.external_attr>>16):raise ValueError('Unsafe archive path')
  manifest=json.loads(z.read('bundle.json'))
  if set(names)!={'bundle.json',*manifest['files']}:raise ValueError('Archive contents differ from manifest')
  if manifest.get('run') and not manifest['run'].replace('-','').replace('_','').isalnum():raise ValueError('Invalid manifest run ID')
  if a.restore_run:
   if manifest['kind']!='checkpoint' or not manifest.get('run'):raise ValueError('Restore requires checkpoint bundle')
   if any(not name.startswith('results/'+manifest['run']+'/') for name in manifest['files']):raise ValueError('Unexpected checkpoint payload')
   import tempfile,fcntl
   with tempfile.TemporaryDirectory() as temporary:
    stage=pathlib.Path(temporary)
    for name,meta in manifest['files'].items():
     import hashlib
     content=z.read(name)
     if hashlib.sha256(content).hexdigest()!=meta['sha256']:raise ValueError('Corrupt bundle')
     target=stage/name;target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(content)
    incoming=stage/'results'/manifest['run'];target=destination/'results'/manifest['run']
    remote=json.loads((incoming/'run.json').read_text())
    target.mkdir(parents=True,exist_ok=True)
    lock=(target/'.session.lock').open('a+');fcntl.flock(lock,fcntl.LOCK_EX|fcntl.LOCK_NB)
    if (target/'run.json').exists():
     local=json.loads((target/'run.json').read_text())
     if local['identity']!=remote['identity']:raise ValueError('Run identity differs')
    from delivery_runs import restore
    valid=[f for f in (incoming/'snapshots').iterdir() if not f.name.startswith('.')]
    if not valid:raise ValueError('No snapshots')
    for folder in valid:
     verify_snapshot(folder)
     copy=target/'snapshots'/folder.name
     if copy.exists():
      if (copy/'snapshot.json').read_bytes()!=(folder/'snapshot.json').read_bytes():raise ValueError('Snapshot collision')
     else:shutil.copytree(folder,copy)
    if (target/'run.json').exists():
     sessions={v['id']:v for v in local.get('sessions',[])}
     sessions.update({v['id']:v for v in remote.get('sessions',[])})
     remote['sessions']=sorted(sessions.values(),key=lambda v:v['started'])
    remote['status']='saved_for_resume'
    write_json(target/'run.json',remote)
    shutil.copy2(incoming/'configuration.yaml',target/'configuration.yaml')
    step,failures=restore(target,remote['identity']['behavior'])
    print(json.dumps({'restored_run':manifest['run'],'step':step,'skipped_corrupt':failures}))
   raise SystemExit(0)
  import hashlib
  for name,meta in manifest['files'].items():
   if hashlib.sha256(z.read(name)).hexdigest()!=meta['sha256']:raise ValueError('Corrupt bundle: '+name)
   target=destination/name
   if not target.resolve().is_relative_to(destination):raise ValueError('Destination symlink escapes root')
   if target.exists():raise ValueError('Refusing to overwrite '+name)
  for name,meta in manifest['files'].items():
   path=destination/name;path.parent.mkdir(parents=True,exist_ok=True)
   path.write_bytes(z.read(name));path.chmod(meta['mode'] & 0o777)
 write_json(destination/('bundle-'+manifest['kind']+'.json'),manifest)
 print(destination);raise SystemExit(0)
if a.output.exists():raise ValueError('Output already exists')
files=set();virtual={}
def tree(folder):
 for path in folder.rglob('*'):
  if not path.is_file() or path.is_symlink():continue
  if any(v in path.relative_to(folder).parts for v in ['bin','obj','__pycache__','.git']):continue
  files.add(path)
if a.kind in ['source','training']:
 for folder in ['Assets','Packages','ProjectSettings','tools','training','docs','notebooks']:tree(root/folder)
 for name in ['README.md','.gitignore']:files.add(root/name)
 if a.kind=='source':files={f for f in files if 'Models' not in f.parts and not f.name.startswith('DeliveryBuildInfo')}
if a.kind in ['linux','training']:
 current=root/'Builds/Delivery/current.json';version=json.loads(current.read_text())['version']
 if a.kind=='training' and a.run:
  meta=json.loads((root/'results'/a.run/'run.json').read_text())
  if environment_hash(root)!=meta['identity']['environment_sha256']:raise ValueError('Current source differs from checkpoint source; use its archived training bundle')
  for name,checksum in meta['identity'].get('tooling',{}).items():
   if digest(root/'tools'/name)!=checksum:raise ValueError('Training tools differ from checkpoint source')
  version=meta['identity'].get('build_version',version)
 if len(version)!=32 or any(c not in '0123456789abcdef' for c in version):raise ValueError('Invalid version')
 tree(root/'Builds/DeliveryVersions'/version)
 virtual['Builds/Delivery/current.json']=(json.dumps({'version':version}).encode(),0o644)
 virtual['Builds/Delivery/AeroLabDelivery.x86_64']=(('#!/bin/sh\nexec "$(dirname "$0")/../DeliveryVersions/'+version+'/AeroLabDelivery.x86_64" "$@"\n').encode(),0o755)
 for path in (root/'artifacts/packages/com.unity.ml-agents').glob('*LICENSE*'):
  if path.is_file() and not path.name.endswith('.meta'):files.add(path)
if a.kind=='evidence':
 tree(root/'artifacts/evaluations');tree(root/'artifacts/studies');tree(root/'Assets/AeroLab/Resources/Models')
 for path in (root/'results').rglob('*.json'):
  if path.name in ['run.json','dagger.json','pretraining.json','calibration.json']:files.add(path)
 for path in (root/'artifacts').glob('*invocation.json'):files.add(path)
 if (root/'artifacts/datasets').is_dir():tree(root/'artifacts/datasets')
 if (root/'artifacts/references').is_dir():tree(root/'artifacts/references')
 if (root/'artifacts/portability/portability-final-v2').exists():
  files.add(root/'artifacts/portability/portability-final-v2/acceptance.json')
 for folder in ['docs','training']:tree(root/folder)
 for name in ['core-checks.log','delivery-smoke.log','flight-regression-final.log']:
  if (root/'artifacts'/name).exists():files.add(root/'artifacts'/name)
 study_config=root/'training/studies/continuity-v2.json'
 if study_config.exists():
  audit=json.loads(study_config.read_text()).get('inherited_validation')
  if audit:
   archives=[f for f in (root/'artifacts/packages').glob('*.zip') if digest(f)==audit['archive_sha256']]
   if not archives:raise ValueError('The reviewed parent source archive is required in the evidence bundle')
   files.add(archives[0])
if a.run:
 if not a.run.replace('-','').replace('_','').isalnum():raise ValueError('Invalid run')
 run=root/'results'/a.run
 if (run/'.session.lock').exists():
  import fcntl
  lock=(run/'.session.lock').open('r');fcntl.flock(lock,fcntl.LOCK_EX|fcntl.LOCK_NB)
 valid=[]
 for folder in sorted((run/'snapshots').glob('*'),reverse=True):
  try:verify_snapshot(folder);valid.append(folder)
  except Exception:continue
 if not valid:raise ValueError('No verified snapshot; legacy runs need a new training session')
 for folder in valid[:3]:tree(folder)
 files.add(run/'run.json');files.add(run/'configuration.yaml')
if a.kind=='checkpoint' and not a.run:p.error('--run required')
manifest={'schema':1,'kind':a.kind,'run':a.run,'trusted_pytorch_only':True,'files':{}}
a.output.parent.mkdir(parents=True,exist_ok=True);temporary=a.output.with_suffix(a.output.suffix+'.tmp')
with zipfile.ZipFile(temporary,'w',zipfile.ZIP_DEFLATED) as z:
 for path in sorted(files):
  name=path.relative_to(root).as_posix();manifest['files'][name]={'sha256':digest(path),'mode':stat.S_IMODE(path.stat().st_mode)};z.write(path,name)
 for name,(content,mode) in virtual.items():
  import hashlib
  manifest['files'][name]={'sha256':hashlib.sha256(content).hexdigest(),'mode':mode};z.writestr(name,content)
 z.writestr('bundle.json',json.dumps(manifest,indent=2))
os.replace(temporary,a.output);write_json(pathlib.Path(str(a.output)+'.sha256.json'),{'sha256':digest(a.output)})
print(a.output)
