# DeliveryLab continuation evidence ledger

Generated UTC: 2026-10-23T11:04:57.319482+00:00

This ledger uses saved reports. A checkpoint, successful import or teacher-mixed training trajectory is not a qualification result.

## Verified implementation

| Item | Evidence/status |
| --- | --- |
| Shared C# core | 32 passed / 0 failed in `artifacts/recovery-core-checks.log` |
| Checkpoint integrity and evaluation resume | See `artifacts/workflow-tests.log` |
| Interrupted training resume | 2371 → 4765; `artifacts/recovery-training-v4*.log` |
| Portable checkpoint resume | 599 → 1053 after bundle extraction to another project directory; `artifacts/recovery-final-relocated.log` |
| Colab | Notebook prepared; actual Colab runtime/account not accessed |

## What varied during training

The ground is a flat 80 × 80 m plane. Six static buildings vary between seeded episodes; they do not move during a flight. The depot and four delivery pads remain fixed.

| Component | Variation |
| --- | --- |
| Building center | Independent X/Z jitter of ±3 m around six fixed grid anchors |
| Building width/depth | Independently generated, approximately 4–8 m |
| Building height | From 5 m up to, but below, 24 m |
| Ground elevation / slopes | Not varied; flat ground only |
| Depot / delivery-pad locations | Fixed |
| Moving obstacles / taller structures blocking the cruise route | Not part of this experiment |

The scripted clearance teacher uses a roughly 26 m cruise-height prior above the building bound. The reported success is not a demonstration of general terrain adaptation or active range-based obstacle avoidance. Different building layouts are a bounded generalization test, not hills, valleys or arbitrary cities.

| Model seed | Final DAgger collection: completed episodes / unique layouts | Final PPO continuation: completed episodes / unique layouts |
| --- | --- | --- |
| 11 | 30 / 30 | 3 / 3 |
| 29 | 30 / 30 | 3 / 3 |
| 47 | 30 / 30 | 3 / 3 |

These counts cover completed episodes in the final continuation stage, not all earlier training or partial episodes. DAgger uses teacher mixing during collection; its counts are not autonomous mission qualification. The allowed training seed pool 0–7999 does not mean all 8000 seeds were used. Held-out validation/test use separate seeds, within the same bounded building generator.

## Current flight validation

| Training seed | Single ideal | Single noisy | FIFO ideal | FIFO noisy | EDD ideal | EDD noisy |
| --- | --- | --- | --- | --- | --- | --- |
| 11 | 100/100; {'Completed': 100} | 100/100; {'Completed': 100} | 100/100; {'Horizon': 100} | 100/100; {'Horizon': 100} | 100/100; {'Horizon': 100} | 100/100; {'Horizon': 100} |
| 29 | 100/100; {'Completed': 100} | 100/100; {'Completed': 100} | 100/100; {'Horizon': 100} | 100/100; {'Horizon': 100} | 100/100; {'Horizon': 100} | 100/100; {'Horizon': 100} |
| 47 | 100/100; {'Completed': 100} | 100/100; {'Completed': 100} | 100/100; {'Horizon': 100} | 100/100; {'Horizon': 100} | 100/100; {'Horizon': 100} | 100/100; {'Horizon': 100} |

Multi-order success means Completed/Horizon with at least one delivered parcel and safe return; an in-progress final sortie remains separate.

Flight continuity gate: True.

## Historical dispatch pilot with original fine-v1 flight

| Seed | Sensors | Delivered | On time | Outcomes |
| --- | --- | ---: | ---: | --- |
| 11 | ideal | 41/240 | 40/240 | {'FlightTimeout': 20} |
| 11 | noisy | 46/240 | 43/240 | {'FlightTimeout': 20} |
| 29 | ideal | 56/240 | 55/240 | {'FlightTimeout': 20} |
| 29 | noisy | 58/240 | 54/240 | {'FlightTimeout': 20} |
| 47 | ideal | 85/240 | 53/240 | {'FlightTimeout': 20} |
| 47 | noisy | 85/240 | 53/240 | {'FlightTimeout': 19, 'Horizon': 1} |

These are 20-scenario pilot comparisons on the old flight model, not qualification of the new continuity flight.

## New dispatch and final test

Selected dispatch milestone: 32768
Dispatch validation conditions: 6/6
Final test conditions: 24/24
Study evidence complete: True
Logistics qualified: True

Training: synthetic state coverage + DAgger + PPO fine-tuning. All inference qualification uses the frozen ONNX in Unity, without teacher mixing.
Buildings remain below 24 m with fixed clear approaches. This does not demonstrate universal obstacle avoidance, real-drone calibration or legal permission.

## Frozen dispatch validation

| Seed | Sensors | Safe episodes | On time / generated | Outcomes |
| --- | --- | --- | --- | --- |
| 11 | ideal | 100/100 | 456/1200 | {'Horizon': 100} |
| 11 | noisy | 100/100 | 453/1200 | {'Horizon': 100} |
| 29 | ideal | 100/100 | 481/1200 | {'Horizon': 100} |
| 29 | noisy | 100/100 | 482/1200 | {'Horizon': 100} |
| 47 | ideal | 100/100 | 473/1200 | {'Horizon': 100} |
| 47 | noisy | 100/100 | 465/1200 | {'Horizon': 100} |

## Held-out final comparison

On-time percentages are mean and sample standard deviation across three independently trained models. Each logistics row contains 300 episodes and 3600 generated orders.

| Sensors | Dispatcher | On time (%) | Delivered / generated | Safe episodes | Incidents |
| --- | --- | --- | --- | --- | --- |
| ideal | FIFO | 17.28 ± 1.26 | 2402/3600 | 300/300 | 0 |
| ideal | Earliest deadline | 16.72 ± 0.96 | 2400/3600 | 300/300 | 0 |
| ideal | Learned dispatch | 39.00 ± 1.39 | 2158/3600 | 300/300 | 0 |
| noisy | FIFO | 17.00 ± 1.12 | 2397/3600 | 300/300 | 0 |
| noisy | Earliest deadline | 16.50 ± 0.93 | 2395/3600 | 300/300 | 0 |
| noisy | Learned dispatch | 38.72 ± 1.31 | 2145/3600 | 300/300 | 0 |

The learned dispatcher increases the fraction of all generated orders delivered on time, while completing fewer orders overall than FIFO/EDD. No claim of uniform superiority is made.

## Time, distance and energy

| Sensors | Dispatcher | Delivery latency (s) | Energy / episode | Distance / episode (m) | Energy / delivered |
| --- | --- | --- | --- | --- | --- |
| ideal | FIFO | 286.48 ± 11.58 | 126.22 ± 0.45 | 1728.64 ± 16.50 | 15.76 ± 0.11 |
| ideal | Earliest deadline | 285.26 ± 10.83 | 126.23 ± 0.44 | 1728.36 ± 16.99 | 15.78 ± 0.13 |
| ideal | Learned dispatch | 198.95 ± 69.71 | 114.80 ± 17.32 | 1553.89 ± 222.69 | 15.94 ± 0.18 |
| noisy | FIFO | 288.42 ± 11.42 | 126.28 ± 0.53 | 1727.22 ± 16.18 | 15.80 ± 0.13 |
| noisy | Earliest deadline | 287.79 ± 11.08 | 126.28 ± 0.48 | 1726.97 ± 17.07 | 15.82 ± 0.14 |
| noisy | Learned dispatch | 199.15 ± 68.37 | 114.86 ± 16.98 | 1552.50 ± 217.18 | 16.05 ± 0.21 |

All multi-order episodes reach the 900-second horizon. Latency is computed only for delivered orders; unfinished/rejected orders remain in the on-time denominator above. Energy uses uncalibrated research units. Values are mean ± sample standard deviation across seeds.


Safety and completion are separate from deadline performance. A successful horizon can contain unfinished orders or an in-progress sortie. These results support only the bounded synthetic campus.

## Dispatch training milestones

| Budget | On time (%) | Incidents | Mean energy |
| --- | --- | --- | --- |
| 2048 | 32.08 | 0 | 125.34 |
| 8192 | 37.01 | 0 | 126.95 |
| 32768 | 39.51 | 0 | 113.61 |

Milestone scores use 20 validation scenarios per condition; they are selection evidence, not held-out final scores.

## Reproduce / continue

See [delivery-continuation.md](delivery-continuation.md), the study JSON and immutable evaluation manifests. Existing historical reports are retained. Local milestone commits were explicitly requested; no push, cloud resource purchase or real-drone control was performed.
