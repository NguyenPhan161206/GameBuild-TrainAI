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
 '## Reproduce / continue','', 'See [delivery-continuation.md](delivery-continuation.md), the study JSON and immutable evaluation manifests. Existing historical reports are retained. No commit, push, cloud resource purchase or real-drone control is part of this implementation.','']
(root/'docs/delivery-progress.md').write_text('\n'.join(lines))
print(root/'docs/delivery-progress.md')
