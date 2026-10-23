#!/usr/bin/env python3
"""Render the current evidence ledger without inferring qualification from training."""
import json,pathlib,datetime
root=pathlib.Path(__file__).resolve().parents[1]
cfg=json.loads((root/'training/studies/continuity-v2.json').read_text());state_path=root/'artifacts/studies'/cfg['tag']/'study.json'
state=json.loads(state_path.read_text()) if state_path.exists() else {}
lines=['# DeliveryLab continuation evidence ledger','', 'Generated UTC: '+datetime.datetime.now(datetime.timezone.utc).isoformat(),'',
 'This ledger uses saved reports. A checkpoint, successful import or teacher-mixed training trajectory is not a qualification result.','',
 '## Verified implementation','', '| Item | Evidence/status |','| --- | --- |',
 '| Shared C# core | 32 passed / 0 failed in `artifacts/recovery-core-checks.log` |',
 '| Checkpoint integrity and evaluation resume | See `artifacts/workflow-tests.log` |',
 '| Interrupted training resume | 2371 → 4765; `artifacts/recovery-training-v4*.log` |',
 '| Portable checkpoint resume | 599 → 1053 after bundle extraction to another project directory; `artifacts/recovery-final-relocated.log` |',
 '| Colab | Notebook prepared; actual Colab runtime/account not accessed |','',
 '## Current flight validation','', '| Training seed | Single ideal | Single noisy | FIFO ideal | FIFO noisy | EDD ideal | EDD noisy |','| --- | --- | --- | --- | --- | --- | --- |']
for seed in cfg['seeds']:
 row=[str(seed)]
 for stage,kind,noisy in [(1,'edd',False),(1,'edd',True),(3,'fifo',False),(3,'fifo',True),(3,'edd',False),(3,'edd',True)]:
  r=state.get('flight_validation',{}).get(f'{seed}-{stage}-{kind}-{noisy}')
  if not r:row.append('Pending')
  else:
   count=r['safe_complete_roundtrips'] if stage==1 else r['operational_safe_episodes'];row.append(f"{count}/{r['episodes']}; {r['outcomes']}")
 lines.append('| '+' | '.join(row)+' |')
lines+=['', 'Multi-order success means Completed/Horizon with at least one delivered parcel and safe return; an in-progress final sortie remains separate.',
 '', 'Flight continuity gate: '+str(state.get('flight_gate_met','pending'))+'.', '',
 '## Historical dispatch pilot with original fine-v1 flight','', '| Seed | Sensors | Delivered | On time | Outcomes |','| --- | --- | ---: | ---: | --- |']
for seed in cfg['seeds']:
 for sensor in ['ideal','noisy']:
  p=root/f'artifacts/evaluations/continuity-pilot-s{seed}-{sensor}/summary.json'
  if p.exists():
   r=json.loads(p.read_text());lines.append(f"| {seed} | {sensor} | {r['delivered']}/{r['total_orders']} | {r['on_time']}/{r['total_orders']} | {r['outcomes']} |")
lines+=['', 'These are 20-scenario pilot comparisons on the old flight model, not qualification of the new continuity flight.','',
 '## New dispatch and final test','',
 f"Selected dispatch milestone: {state.get('selected_dispatch_milestone','pending')}",
 f"Dispatch validation conditions: {len(state.get('dispatch_validation',{}))}/6",
 f"Final test conditions: {len(state.get('test',{}))}/24",
 f"Study evidence complete: {state.get('complete',False)}",
 f"Logistics qualified: {state.get('qualified_logistics','pending')}", '',
 'Training: synthetic state coverage + DAgger + PPO fine-tuning. All inference qualification uses the frozen ONNX in Unity, without teacher mixing.',
 'Buildings remain below 24 m with fixed clear approaches. This does not demonstrate universal obstacle avoidance, real-drone calibration or legal permission.','',
 '## Reproduce / continue','', 'See [delivery-continuation.md](delivery-continuation.md), the study JSON and immutable evaluation manifests. Existing historical reports are retained. Local milestone commits were explicitly requested; no push, cloud resource purchase or real-drone control was performed.','']
extra=['## Frozen dispatch validation','', '| Seed | Sensors | Safe episodes | On time / generated | Outcomes |','| --- | --- | --- | --- | --- |']
for seed in cfg['seeds']:
 for noisy in [False,True]:
  row=state.get('dispatch_validation',{}).get(f'{seed}-{noisy}')
  if row:extra.append(f"| {seed} | {'noisy' if noisy else 'ideal'} | {row['operational_safe_episodes']}/{row['episodes']} | {row['on_time']}/{row['total_orders']} | {row['outcomes']} |")
extra+=['', '## Held-out final comparison','', 'On-time percentages are mean and sample standard deviation across three independently trained models. Each logistics row contains 300 episodes and 3600 generated orders.','',
 '| Sensors | Dispatcher | On time (%) | Delivered / generated | Safe episodes | Incidents |', '| --- | --- | --- | --- | --- | --- |']
import csv
export=[]
for noisy in [False,True]:
 for kind,label in [('fifo','FIFO'),('edd','Earliest deadline'),('ai','Learned dispatch')]:
  keys=[f'{seed}-3-{kind}-{noisy}' for seed in cfg['seeds']]
  if not all(key in state.get('test',{}) for key in keys):continue
  rows=[state['test'][key] for key in keys];metrics=state.get('aggregates',{}).get(f'3-{kind}-{noisy}')
  if not metrics:continue
  rate=metrics['on_time_rate'];generated=sum(row['total_orders'] for row in rows)
  delivered=sum(row['delivered'] for row in rows);safe=sum(row['operational_safe_episodes'] for row in rows)
  incidents=sum(sum(n for outcome,n in row['outcomes'].items() if outcome not in ['Completed','Horizon']) for row in rows)
  extra.append(f"| {'noisy' if noisy else 'ideal'} | {label} | {rate['mean']*100:.2f} ± {rate['sample_stdev']*100:.2f} | {delivered}/{generated} | {safe}/300 | {incidents} |")
  export.append({'sensor':'noisy' if noisy else 'ideal','dispatcher':kind,'on_time_percent_mean':rate['mean']*100,'on_time_percent_stdev':rate['sample_stdev']*100,'delivered':delivered,'generated':generated,'safe_episodes':safe,'incidents':incidents,
   'energy_per_episode_mean':metrics['mean_energy']['mean'],'energy_per_episode_stdev':metrics['mean_energy']['sample_stdev'],'distance_per_episode_mean':metrics['mean_distance']['mean'],'elapsed_per_episode_mean':metrics['mean_elapsed']['mean'],'delivery_latency_mean':metrics['mean_delivery_latency']['mean'],'delivery_latency_stdev':metrics['mean_delivery_latency']['sample_stdev'],'energy_per_delivered_mean':metrics['energy_per_delivered']['mean'],'energy_per_delivered_stdev':metrics['energy_per_delivered']['sample_stdev']})
if export:
 with (state_path.parent/'comparison.csv').open('w',newline='') as stream:
  writer=csv.DictWriter(stream,fieldnames=list(export[0]));writer.writeheader();writer.writerows(export)
extra+=['', 'The learned dispatcher increases the fraction of all generated orders delivered on time, while completing fewer orders overall than FIFO/EDD. No claim of uniform superiority is made.','',
 '## Time, distance and energy','', '| Sensors | Dispatcher | Delivery latency (s) | Energy / episode | Distance / episode (m) | Energy / delivered |', '| --- | --- | --- | --- | --- | --- |']
def formatted(metric):
 if metric['mean'] is None:return 'Undefined'
 return f"{metric['mean']:.2f} ± {metric['sample_stdev']:.2f}"
for noisy in [False,True]:
 for kind,label in [('fifo','FIFO'),('edd','Earliest deadline'),('ai','Learned dispatch')]:
  metric=state.get('aggregates',{}).get(f'3-{kind}-{noisy}')
  if metric and 'mean_delivery_latency' in metric:
   extra.append(f"| {'noisy' if noisy else 'ideal'} | {label} | {formatted(metric['mean_delivery_latency'])} | {formatted(metric['mean_energy'])} | {formatted(metric['mean_distance'])} | {formatted(metric['energy_per_delivered'])} |")
extra+=['', 'All multi-order episodes reach the 900-second horizon. Latency is computed only for delivered orders; unfinished/rejected orders remain in the on-time denominator above. Energy uses uncalibrated research units. Values are mean ± sample standard deviation across seeds.','']
extra+=['', 'Safety and completion are separate from deadline performance. A successful horizon can contain unfinished orders or an in-progress sortie. These results support only the bounded synthetic campus.','',
 '## Dispatch training milestones','', '| Budget | On time (%) | Incidents | Mean energy |','| --- | --- | --- | --- |']
for budget,item in state.get('dispatch_milestones',{}).items():
 score=item['score'];extra.append(f"| {budget} | {score['on_time_rate']*100:.2f} | {score['incidents']} | {score['mean_energy']:.2f} |")
extra+=['', 'Milestone scores use 20 validation scenarios per condition; they are selection evidence, not held-out final scores.','']
terrain=['## What varied during training','',
 'The ground is a flat 80 × 80 m plane. Six static buildings vary between seeded episodes; they do not move during a flight. The depot and four delivery pads remain fixed.','',
 '| Component | Variation |', '| --- | --- |',
 '| Building center | Independent X/Z jitter of ±3 m around six fixed grid anchors |',
 '| Building width/depth | Independently generated, approximately 4–8 m |',
 '| Building height | From 5 m up to, but below, 24 m |',
 '| Ground elevation / slopes | Not varied; flat ground only |',
 '| Depot / delivery-pad locations | Fixed |',
 '| Moving obstacles / taller structures blocking the cruise route | Not part of this experiment |','',
 'The scripted clearance teacher uses a roughly 26 m cruise-height prior above the building bound. The reported success is not a demonstration of general terrain adaptation or active range-based obstacle avoidance. Different building layouts are a bounded generalization test, not hills, valleys or arbitrary cities.','',
 '| Model seed | Final DAgger collection: completed episodes / unique layouts | Final PPO continuation: completed episodes / unique layouts |', '| --- | --- | --- |']
import hashlib
for seed in cfg['seeds']:
 dagger_path=root/f'results/continuity-v2-s{seed}/dagger.json'
 if not dagger_path.exists():continue
 dagger=json.loads(dagger_path.read_text());dagger_rows=[r for record in dagger['records'] for r in record['reports']]
 ppo_rows=[json.loads(p.read_text()) for p in (root/f'artifacts/training-episodes/continuity-ppo-v2-flight-s{seed}-stage3-noisy').rglob('episode-*.json')]
 def layouts(rows):return len({hashlib.sha256(json.dumps(r['Config']['Buildings'],sort_keys=True).encode()).hexdigest() for r in rows})
 terrain.append(f'| {seed} | {len(dagger_rows)} / {layouts(dagger_rows)} | {len(ppo_rows)} / {layouts(ppo_rows)} |')
terrain+=['', 'These counts cover completed episodes in the final continuation stage, not all earlier training or partial episodes. DAgger uses teacher mixing during collection; its counts are not autonomous mission qualification. The allowed training seed pool 0–7999 does not mean all 8000 seeds were used. Held-out validation/test use separate seeds, within the same bounded building generator.','']
index=lines.index('## Current flight validation');lines[index:index]=terrain

index=lines.index('## Reproduce / continue');lines[index:index]=extra
(root/'docs/delivery-progress.md').write_text('\n'.join(lines))
print(root/'docs/delivery-progress.md')
