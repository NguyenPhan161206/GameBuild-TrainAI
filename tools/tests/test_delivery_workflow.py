"""Behavioral checks: checkpoint corruption, portability, identity and operational denominators."""
import json,pathlib,sys,tempfile,unittest,subprocess,shutil
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[1]))
from delivery_runs import snapshot,restore,verify_snapshot,write_json,checkpoint_step
from delivery_metrics import summarize
import torch
ROOT=pathlib.Path(__file__).resolve().parents[2]
class Checkpoints(unittest.TestCase):
 def setUp(self):
  self.temp=tempfile.TemporaryDirectory();self.root=pathlib.Path(self.temp.name);self.run=self.root/'results'/'test-run';self.run.mkdir(parents=True)
  write_json(self.run/'run.json',{'identity':{'test':1},'sessions':[]});(self.run/'configuration.yaml').write_text('test: true\n')
  self.behavior='DeliveryFlight';(self.run/self.behavior).mkdir()
 def tearDown(self):self.temp.cleanup()
 def save(self,step):
  data={'Policy':{'weight':torch.tensor([step])},'global_step':{'step':step},'Optimizer:value_optimizer':{'state':{'momentum':torch.tensor([.2])}},'Optimizer:critic':{}}
  torch.save(data,self.run/self.behavior/'checkpoint.pt');(self.run/self.behavior/f'{self.behavior}-{step}.onnx').write_bytes(b'model')
  return snapshot(self.run,self.behavior,step,self.root/'durable')
 def test_fallback_after_corruption_keeps_optimizer(self):
  self.save(10);last=self.save(20);(last/'checkpoint.pt').write_bytes(b'crash during write')
  step,errors=restore(self.run,self.behavior);self.assertEqual(step,10);self.assertEqual(len(errors),1)
  data=torch.load(self.run/self.behavior/'checkpoint.pt');self.assertIn('Optimizer:value_optimizer',data)
 def test_partial_generation_ignored_and_retention(self):
  (self.run/'snapshots').mkdir();(self.run/'snapshots'/'.writing-crash').mkdir()
  for step in [10,20,30,40]:self.save(step)
  valid=[p for p in (self.run/'snapshots').iterdir() if not p.name.startswith('.')]
  self.assertEqual(len(valid),3);self.assertEqual(restore(self.run,self.behavior)[0],40)
 def test_relocate_without_absolute_paths(self):
  self.save(12);target=self.root/'another-machine'/'test-run';shutil.copytree(self.run,target)
  self.assertEqual(restore(target,self.behavior)[0],12)
  state=json.loads((target/'run.json').read_text());state['identity']={'test':2};write_json(target/'run.json',state)
  with self.assertRaises(ValueError):restore(target,self.behavior)
 def test_actor_only_cannot_claim_resume(self):
  p=self.root/'actor.pt';torch.save({'Policy':{}},p)
  with self.assertRaises(ValueError):checkpoint_step(p)
 def test_bundle_refuses_path_traversal(self):
  import zipfile
  archive=self.root/'evil.zip'
  with zipfile.ZipFile(archive,'w') as z:z.writestr('../escape','bad');z.writestr('bundle.json','{}')
  result=subprocess.run([sys.executable,str(ROOT/'tools/delivery-transfer.py'),'unpack',str(archive),'--destination',str(self.root/'extract')],capture_output=True)
  self.assertNotEqual(result.returncode,0);self.assertFalse((self.root/'escape').exists())
class Metrics(unittest.TestCase):
 def row(self,**changes):
  d={'Result':'Horizon','Delivered':2,'OnTime':1,'Orders':[{}, {}, {}],'SafeReturns':1,'Trips':2,'Failed':0,'Rejected':0,'Unfinished':1,'Elapsed':900,'Energy':20,'Distance':40,'MeanDeliveryLatency':30};d.update(changes);return d
 def test_horizon_not_accident_but_waiting_not_success(self):
  r=summarize([self.row(),self.row(Delivered=0,OnTime=0,SafeReturns=0)],{'stage':3,'noisy':False})
  self.assertEqual(r['operational_safe_episodes'],1);self.assertEqual(r['on_time_rate'],1/6)
  self.assertEqual(r['safe_complete_roundtrips'],0);self.assertIsNone(r['logistics_target_met'])
 def test_no_deliveries_is_undefined_energy_per_order(self):
  r=summarize([self.row(Delivered=0,OnTime=0)],{'stage':3,'noisy':False})
  self.assertIsNone(r['energy_per_delivered']);self.assertIsNone(r['mean_delivery_latency'])
class EvaluationResume(unittest.TestCase):
 def test_partial_player_failure_runs_only_missing_seeds(self):
  with tempfile.TemporaryDirectory() as temporary:
   root=pathlib.Path(temporary);(root/'tools').mkdir();player=root/'player';player.mkdir()
   for name in ['evaluate-delivery.py','delivery_runs.py','delivery_metrics.py']:
    shutil.copy2(ROOT/'tools'/name,root/'tools'/name)
   managed=player/'AeroLabDelivery_Data/Managed';managed.mkdir(parents=True)
   for name in ['AeroLab.Core.dll','AeroLab.Runtime.dll']:(managed/name).write_bytes(b'fake-test-only')
   executable=player/'fake-player'
   executable.write_text('#!'+sys.executable+'\n'+"""import sys,json,pathlib
args=sys.argv
def arg(key):return args[args.index(key)+1]
seed=int(arg('--scenario-seed'));out=pathlib.Path(arg('--reports'))
marker=out/'failed-once'
if seed==7001 and not marker.exists():
 marker.write_text('1');(out/'episode-writing.json.tmp').write_text('{');raise SystemExit(42)
row={'Seed':seed,'Result':'Horizon','Delivered':2,'OnTime':1,'Orders':[{}, {}, {}],'SafeReturns':1,'Trips':2,'Failed':0,'Rejected':0,'Unfinished':1,'Elapsed':900,'Energy':20,'Distance':40,'MeanDeliveryLatency':30}
(out/f'episode-{seed}-1.json').write_text(json.dumps(row))
""")
   executable.chmod(0o755)
   args=[sys.executable,str(root/'tools/evaluate-delivery.py'),'--env',str(executable),'--mode','reference','--split','diagnostic','--seed-start','7000','--episodes','3','--chunk-size','1','--name','resume-test']
   first=subprocess.run(args,capture_output=True);self.assertNotEqual(first.returncode,0)
   saved=root/'artifacts/evaluations/resume-test/episode-7000-1.json';before=saved.stat().st_mtime_ns
   second=subprocess.run(args+['--resume'],capture_output=True)
   self.assertEqual(second.returncode,0,second.stderr.decode());self.assertEqual(saved.stat().st_mtime_ns,before)
   summary=json.loads((saved.parent/'summary.json').read_text());self.assertEqual(summary['episodes'],3)
   (managed/'AeroLab.Core.dll').write_bytes(b'changed-build')
   third=subprocess.run(args+['--resume'],capture_output=True);self.assertNotEqual(third.returncode,0)
if __name__=='__main__':unittest.main()
