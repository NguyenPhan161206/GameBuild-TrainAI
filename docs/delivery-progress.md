# DeliveryLab continuation evidence ledger

Generated UTC: 2026-10-23T04:39:15.971320+00:00

This ledger uses saved reports. A checkpoint, successful import or teacher-mixed training trajectory is not a qualification result.

## Verified implementation

| Item | Evidence/status |
| --- | --- |
| Shared C# core | 32 passed / 0 failed in `artifacts/recovery-core-checks.log` |
| Checkpoint integrity and evaluation resume | See `artifacts/workflow-tests.log` |
| Interrupted training resume | 2371 → 4765; `artifacts/recovery-training-v4*.log` |
| Portable checkpoint resume | 599 → 1053 after bundle extraction to another project directory; `artifacts/recovery-final-relocated.log` |
| Colab | Notebook prepared; actual Colab runtime/account not accessed |

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

Selected dispatch milestone: pending
Dispatch validation conditions: 0/6
Final test conditions: 0/24
Study evidence complete: False
Logistics qualified: pending

Training: synthetic state coverage + DAgger + PPO fine-tuning. All inference qualification uses the frozen ONNX in Unity, without teacher mixing.
Buildings remain below 24 m with fixed clear approaches. This does not demonstrate universal obstacle avoidance, real-drone calibration or legal permission.

## Reproduce / continue

See [delivery-continuation.md](delivery-continuation.md), the study JSON and immutable evaluation manifests. Existing historical reports are retained. No commit, push, cloud resource purchase or real-drone control is part of this implementation.
