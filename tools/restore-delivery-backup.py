#!/usr/bin/env python3
"""Recover verified Drive/local snapshot generations after a runtime disappears."""
import argparse,pathlib,json,shutil,fcntl
from delivery_runs import verify_snapshot,restore,write_json
root=pathlib.Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser();p.add_argument('--backup-dir',type=pathlib.Path,required=True);p.add_argument('--run',required=True)
a=p.parse_args()
if not a.run.replace('-','').replace('_','').isalnum():p.error('Invalid run ID')
source=a.backup_dir/a.run;valid=[];errors=[]
for folder in source.glob('*'):
 if not folder.is_dir() or folder.name.startswith('.'):continue
 try:
  meta=verify_snapshot(folder);saved=json.loads((folder/'run.json').read_text())
  if saved['run_id']!=a.run:raise ValueError('Snapshot run ID differs')
  valid.append((meta['step'],meta['created'],folder,saved))
 except Exception as error:errors.append({'snapshot':folder.name,'reason':str(error)})
if not valid:raise ValueError('No verified backup generations: '+str(errors))
valid.sort(reverse=True);state=valid[0][3]
target=root/'results'/a.run;target.mkdir(parents=True,exist_ok=True)
lock=(target/'.session.lock').open('a+');fcntl.flock(lock,fcntl.LOCK_EX|fcntl.LOCK_NB)
manifest=target/'run.json'
if manifest.exists():
 local=json.loads(manifest.read_text())
 if local['identity']!=state['identity']:raise ValueError('Existing run has different source/configuration/build')
 sessions={s['id']:s for s in state.get('sessions',[])}
 for item in local.get('sessions',[]):
  if item.get('ended') or item['id'] not in sessions:sessions[item['id']]=item
 state['sessions']=sorted(sessions.values(),key=lambda s:s['started'])
for step,created,folder,saved in valid[:3]:
 if saved['identity']!=state['identity']:raise ValueError('Backup contains mixed experiment identities')
 destination=target/'snapshots'/folder.name
 if destination.exists():
  verify_snapshot(destination)
  if (destination/'snapshot.json').read_bytes()!=(folder/'snapshot.json').read_bytes():raise ValueError('Snapshot name collision')
 else:shutil.copytree(folder,destination)
state['status']='saved_for_resume';state['backup_restore']={'source':str(source),'skipped':errors}
write_json(manifest,state);shutil.copy2(valid[0][2]/'configuration.yaml',target/'configuration.yaml')
step,recovery=restore(target,state['identity']['behavior'])
print(json.dumps({'run':a.run,'restored_step':step,'skipped_corrupt':errors+recovery},indent=2))
