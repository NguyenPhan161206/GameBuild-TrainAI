# DeliveryLab validation record

> Historical validation record through the initial PPO/imitation attempts. Later fine-v1 models reached 100/100 single-order roundtrips per seed in both sensor modes. Multi-order continuity and the new resumable workflow are tracked in [delivery-continuation.md](delivery-continuation.md) and [delivery-progress.md](delivery-progress.md). The old failure tables below refer only to their named historical models.

This record distinguishes implementation verification from research qualification. The application is a synthetic research simulator; neither the physics, sensor settings nor energy units are calibrated against a real drone. Generated land/building status never establishes flight permission.

## Verified environment

- Local Unity Editor **6000.6.7f1**, Linux x64; CPU training on AMD Ryzen 7 5700U. Cloud execution has not been verified and is not required.
- Unity ML-Agents **4.0.0**, prepared from the checksum-verified official archive with the two compatibility changes documented in `delivery-lab.md`.
- Python **3.10.12**, `mlagents` / `mlagents-envs` **1.1.0**, PyTorch **2.2.2+cpu**; the observed dependency lock is `training/requirements-observed.txt`.
- C# regression runner uses .NET SDK **8.0.426**. Unity and Python-connected training use the same C# simulation core.

The initial Unity manifest/project-version changes and other local settings were retained. No checkout reset, pull, commit or push was performed.

## Implementation verification

| Check | Observed result | Evidence under repository root |
| --- | --- | --- |
| Core behavior, configuration, state transitions and serialization | 30 passed, 0 failed | `artifacts/core-checks.log` |
| Original FlightLab actual Editor Play Mode | Passed original flight, reset and pause checks | `artifacts/flight-regression-final.log` |
| DeliveryLab actual Editor Play Mode | Passed delivery, safe return and reset | `artifacts/delivery-smoke.log` |
| Linux player / native Python communicator | Connected and trained both behaviors | `artifacts/studies/wiring-v4/study.json` |
| Export and reload | Three independent flight and three dispatch PPO models exported and ran in native Unity inference | same study and corresponding evaluation logs |
| Deterministic replay | Entire episode report for validation seed 10000 matched a repeated execution, including model/source hashes | `artifacts/evaluations/deterministic-replay-s11/` versus `qualification-s11-ideal/` |

The wiring study used only 4096 flight decisions and 128 dispatch decisions per trainer seed (11, 29, 47). It deliberately bypassed the research-quality gate to check the second adapter. These six models are **integration artifacts, not qualified delivery AI**.

## Scripted reference baseline

100 test scenarios per condition, seeds 20000–20099, 12 generated orders per scenario, 900-second horizon. Both dispatch rules used the same seeded order streams and the map-based route controller.

| Flight / dispatch | Delivered / 1200 | On time / 1200 | Safe returns | Incidents | Mean energy / episode |
| --- | ---: | ---: | ---: | ---: | ---: |
| Scripted route / FIFO | 638 | 140 | 598 | 0 | 123.69 |
| Scripted route / earliest deadline | 642 | 135 | 598 | 0 | 123.68 |

A horizon may finish while a safe flight is still in progress; delivered count, safe returns and complete roundtrips are deliberately separate. Energy can exceed battery capacity across an episode because the drone charges between sorties. Ideal and noisy reference outcomes were identical: this controller accesses the exact map/state and does not consume noisy observations. This is **not evidence of learned sensor robustness**. Raw reports: `artifacts/delivery-baselines.json`.

## PPO flight qualification attempt

The wiring models were evaluated on **100 varied stage-1 validation scenarios each**, with ideal and noisy sensors reported separately. No final AI test-set result is claimed.

| Trainer seed | Ideal safe roundtrips / 100 | Ideal failures | Noisy safe roundtrips / 100 | Noisy failures |
| --- | ---: | --- | ---: | --- |
| 11 | 0 | 9 collision, 91 bounds | 0 | 3 collision, 97 bounds |
| 29 | 0 | 57 collision, 43 bounds | 0 | 44 collision, 56 bounds |
| 47 | 0 | 20 collision, 80 bounds | 0 | 18 collision, 82 bounds |

Mean safe completion is **0%, sample standard deviation 0 percentage points**, separately for ideal and noisy observations. The required **90/100 ideal** gate is not met. These results establish an operational evaluation pipeline, not successful training.

Additional pure-PPO runs (`foundation-v1`, 100,000 stage-0 decisions per seed) also recorded no handovers. Twenty map-controller expert demonstrations (training scenario seeds 500–519) all completed safe roundtrips. PPO with SDK behavioral cloning (`imitation-v1`, 50,000 decisions per seed) and explicitly action-unit-calibrated candidates were evaluated separately on 20 validation scenarios at stages 0 and 1. All candidates still completed zero safe roundtrips. Calibration was an experimental candidate transformation, not an SDK patch or a qualification shortcut.

## Remaining research acceptance

A qualified three-seed flight result, noisy flight assessment, and meaningful learned-dispatch comparison remain open. Dispatch wiring models are available for adapter testing, but training a dispatch policy on a failing flight controller cannot support operational performance claims. The study runner stops at a failed flight gate in research mode.

The UI, fixed-step simulation, logs, model import/export, stage configurations, baselines and reproducible commands are implemented. They should be used to continue research; they are not a claim that the full AI acceptance plan has passed.
