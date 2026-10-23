#!/usr/bin/env python3
"""Aggregate independent training seeds; each input is one frozen evaluation summary."""
import argparse,json,pathlib,statistics
p=argparse.ArgumentParser();p.add_argument('summaries',nargs=3,type=pathlib.Path);p.add_argument('--output',type=pathlib.Path,required=True);a=p.parse_args()
rows=[json.loads(f.read_text()) for f in a.summaries]
for key in ['mode','split','stage','noisy','episodes','rule','seed_start','schema']:
 if len({r.get(key) for r in rows})!=1:raise ValueError('Evaluations must have identical '+key)
model_key='dispatch_model' if rows[0]['mode']=='dispatch-eval' else 'model'
if len({r[model_key] for r in rows})!=3:raise ValueError('Expected three independently named models')
for field in ['source_sha256','build_sha256']:
 if len({json.dumps(r.get(field),sort_keys=True) for r in rows})!=1:raise ValueError('Build/source versions differ')
for row in rows:
 row['incidents']=sum(count for outcome,count in row['outcomes'].items() if outcome not in ['Completed','Horizon'])
metrics={}
for key in ['delivered','on_time','on_time_rate','incidents','mean_elapsed','mean_energy','mean_distance','safe_complete_roundtrips','operational_safe_episodes','mean_delivery_latency','energy_per_delivered']:
 values=[r.get(key) for r in rows]
 metrics[key]={'mean':statistics.mean(values),'sample_stdev':statistics.stdev(values)} if all(v is not None for v in values) else {'mean':None,'sample_stdev':None,'reason':'Undefined in one or more seeds'}
a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps({'inputs':[str(f) for f in a.summaries],'metrics':metrics,'warning':'Independence must also be confirmed from training invocation metadata; names alone do not prove independent training.'},indent=2))
print(a.output)
