# DeliveryLab: resumable continuity research

## Run safely in sessions

`train-delivery.py` now runs at most 120 minutes per session. It requests a graceful stop five minutes before that limit (short verification sessions use a proportionate margin). Ctrl+C and SIGTERM request the same checkpoint/export path. Sudden power loss cannot execute a final save: resume uses the latest complete verified snapshot, and any newer unsaved work is lost. An in-flight episode and unsaved rollout buffers restart; optimizer/model/global-step state is restored, not an exact bit-for-bit simulation continuation.

```bash
.venv/bin/python tools/train-delivery.py dispatch --seed 11 --stage 3 --noisy --steps 32768 --batch-size 32 --buffer-size 128 --checkpoint-interval 128 --summary-freq 128 --flight-model Models/continuity-ppo-v2-flight-s11 --tag continuity-dispatch-v2
# Later, the same arguments, plus --resume:
.venv/bin/python tools/train-delivery.py dispatch --seed 11 --stage 3 --noisy --steps 32768 --batch-size 32 --buffer-size 128 --checkpoint-interval 128 --summary-freq 128 --flight-model Models/continuity-ppo-v2-flight-s11 --tag continuity-dispatch-v2 --resume
```

The full PPO schedule stays fixed while `--stop-at-step 2048` or `8192` stops at a milestone. It is not a new max-steps schedule. Default checkpoints are every 256 flight or 128 dispatch decisions; wall-time saving is attempted every 300 seconds at trainer boundaries. `--backup-dir /durable/path` copies verified generations there. Three valid generations are retained. Saving on the SDK/trainer thread avoids copying partially written checkpoints. The optional wrapper extends the SDK in memory and does not edit installed SDK files.

Run metadata is `results/<run>/run.json`. Each session has a unique episode-report directory, preventing resumed reports from replacing earlier episodes. `running` after a power loss means the previous process did not finalize; the OS releases its lock. Resume checks code, configuration, build assemblies and model dependencies and restores the latest valid snapshot. Corrupt generations are skipped and recorded. Legacy runs without this manifest must initialize a new tag rather than claiming a full resume. `--initialize-from` creates a new experiment; it is not `--resume`.

Only load PyTorch checkpoints produced by a trusted project run. This tool does not make arbitrary downloaded pickle files safe.

## Transfer between machines

A training bundle contains source, pinned setup, model resources and exactly one immutable Linux player. It excludes the Editor, caches, virtual environments and unrelated build versions.

```bash
.venv/bin/python tools/delivery-transfer.py pack --kind training --output artifacts/packages/AeroLab-training.zip
# With a current portable run:
.venv/bin/python tools/delivery-transfer.py pack --kind checkpoint --run continuity-dispatch-v2-dispatch-s11-stage3-noisy --output artifacts/packages/session-checkpoint.zip
# Restore to another checkout containing the identical source/build/resources:
.venv/bin/python tools/delivery-transfer.py unpack artifacts/packages/session-checkpoint.zip --destination . --restore-run
```

Stop the run on the old machine before switching. A local lock prevents concurrent local writers; it is not a distributed Google Drive lock. Checkpoint imports merge generations and session history only when identities match; they do not overwrite unrelated experiments. Ordinary bundle extraction refuses overwrite, path traversal, symlinks and corrupt hashes, and restores executable permissions. Keep the launcher and its selected version directory together.

`notebooks/AeroLab-Colab.ipynb` mounts Drive only when the user runs its mount cell. It installs an isolated Python 3.10.12 environment, verifies the bundle, runs a reference and communicator smoke check, resumes in sessions and exports snapshots/ONNX. Colab compatibility and speed must be established on the actual runtime. CPU is the default; no paid service or GPU is assumed. Runtime files are temporary. A disconnect may lose work newer than the last verified Drive snapshot. No Colab runtime/account has been accessed by this implementation.

A checkpoint zip resumes training; an ONNX supplies Unity inference. To import an ONNX:

```bash
.venv/bin/python tools/freeze-delivery-model.py /path/to/DeliveryDispatch.onnx --layer dispatch --name my-dispatch-candidate
bash tools/check-delivery-unity.sh build
```

A new model is a candidate, not a qualified model, until native Unity evaluation has passed.

## Diagnose repeated flights

Environment version `delivery-v2` preserves the 26/60 observation dimensions and action sizes. Reports add `ReportVersion` and `Legs` (trip/order, direction, endpoints, times, battery, distance, final position/velocity and outcome). `Horizon` closes an in-progress leg without inventing a landing. Reserve redirection closes the outbound leg separately. Reset clears leg history.

Use evaluation `--trace` to record action/observation frames at the five-tick decision cadence and leg/handover/end events. Trace uses cached observations and does not generate random sensor samples. Raw pose/goal fields are diagnostic-only; they are not appended to agent input.

```bash
.venv/bin/python tools/evaluate-delivery.py --mode flight-eval --flight-model Models/fine-v1-flight-s11 --split diagnostic --seed-start 10000 --stage 3 --episodes 1 --trace --name diagnosis-unique
```

`diagnose-delivery.py` executes an actual checkpoint against the shared C# core over JSON lines; `--pattern repeat`/`cycle`, `--teacher` and `--neutral-nuisance` are explicit diagnostic interventions. They are never native Unity qualification results. Normal inference cannot silently invoke those interventions.

## Gated study and final evidence

`training/studies/continuity-v2.json` defines the next study. Training seeds remain 0–7999. Validation is now 11000–11099. Final seeds 30000–30099 remain unopened until candidates are fixed. Older test seeds 20000–20099 are historical/diagnostic after being inspected during development.

```bash
.venv/bin/python tools/run-delivery-study.py --action status
.venv/bin/python tools/run-delivery-study.py --action flight-validation
.venv/bin/python tools/run-delivery-study.py --action dispatch-train
.venv/bin/python tools/run-delivery-study.py --action dispatch-validation
.venv/bin/python tools/run-delivery-study.py --action final
.venv/bin/python tools/run-delivery-study.py --action package
```

Freeze/import the named flight candidates and rebuild before the first validation action. The dispatch action uses a fixed 32768-step schedule, stops/evaluates at 2048/8192/32768, compares 20 validation scenarios per condition, and preserves the selected immutable milestone. Continue only if the mean on-time rate increases by at least one percentage point or total incident count falls, with no collision/bounds/energy event. A two-hour session that ends earlier leaves a resumable study, not a completed result.

Flight continuity requires >=90/100 complete single-order ideal missions, and >=95/100 operationally safe multi-order episodes for FIFO and EDD under each sensor mode, separately per seed. For the multi-order metric, Completed/Horizon must include at least one delivery and safe return. Waiting all episode does not pass. Any collision, bounds violation or depleted battery fails the gate. Delivery counts and incidents are reported separately; Horizon is not an accident and an ongoing final sortie is not a safe return.

The research dispatch action refuses to start before flight continuity passes. Existing pilot dispatch models may be evaluated separately with their original flight models and are explicitly historical diagnostics. No superiority claim is assumed. Final comparisons use the same learned flight weights and scenario streams for FIFO, EDD and AI dispatch. The map-aware route controller is a separate reference.

The final matrix is 600 one-order episodes plus 1800 multi-order episodes. Evaluations use one immutable build, atomic per-seed reports and a settings/model/build manifest. Append `--resume` with identical arguments to run only missing seeds. The study automatically does this. Changing a build or model needs a new evaluation name; it cannot merge incompatible reports. Final aggregates include all generated orders, all failures and variability over three seeds.

## Bounded improvement and limits

The first continuation candidate uses three DAgger rounds of ten noisy multi-order training episodes, 60 fit epochs/round, initialized from each fine-v1 flight checkpoint. It then receives at most 30000 PPO decisions per seed with physical observation scaling, learning rate 0.00003 and beta 0. A second feature-selection candidate is allowed only if the first fails. Do not extend the 120-second leg timeout or alter deadlines to hide failure.

The clearance teacher assumes buildings below 24 m and clear fixed-pad approaches. Neither new training nor a successful test establishes general sensor-driven obstacle avoidance, calibrated hardware physics/energy, legal permission or real-world drone readiness.

## Reviewed reporting-only revision

The complete 18-condition flight validation belongs to immutable build `20ca169e32df4b3da5ea22ac65577553` and study `continuity-v2-r1`. Play Mode then exposed a default report-file collision. The fix assigns a unique default output session and increments completion counters only after the atomic report write; the smoke probe also fails on AeroLab runtime errors. Physics, observation/action contracts and flight/dispatch agents were unchanged.

The follow-up study `continuity-v2-r2` records that validation as inherited evidence, not as a rerun on the new executable. `fork-delivery-study.py` verifies the original source/configuration archive against the parent hashes, permits only the two exact reviewed file revisions, and records every changed hash. It never resumes training across source changes. The evidence bundle includes that original source archive. All subsequent dispatch validation and final tests use the new immutable builds.

## Final handoff

The frozen continuity study completed all 2400 held-out episodes. Consult the generated evidence ledger for the exact means and variability. The learned dispatcher improves on-time delivery while completing fewer orders overall; all multi-order runs reach the horizon, so unfinished orders remain explicit.

The release aliases are `Models/campus-flight-s11` and `Models/campus-dispatch-s11`, with corresponding seed-29/47 resources. They contain the same ONNX bytes as the selected research candidates. Flight provenance's original `qualified` boolean encodes the single-order flight gate only; complete logistics qualification is recorded in the study's ideal/noisy validation and final test results.

`--action package` creates `AeroLab-source.zip`, `AeroLab-linux.zip`, `AeroLab-models-evidence.zip`, and `AeroLab-training.zip` beside the study JSON. Each has a SHA-256 sidecar and per-file manifest. The evidence archive retains the exact research executable; a later demo build may add resource aliases without changing controller weights. The training archive pins the original training player and includes the seed-11 dispatch checkpoint for resume/initialize-from. A completed training budget cannot be extended by silently changing `--steps`: start a new tag with `--initialize-from` when deliberately planning a further experiment.

In the Colab notebook, start a new unique tag with `RESUME = False`. In a later session use the same bundle, tag and arguments with `RESUME = True`. If the VM vanished before exporting a zip, the notebook restores the verified Drive generations using:

```bash
.venv/bin/python tools/restore-delivery-backup.py --backup-dir /durable/snapshots --run YOUR_RUN_ID
```

This recovery path was also checked locally with an incomplete backup generation and a real optimizer checkpoint. Live Colab compatibility still requires running the notebook on the user's selected runtime.

## Terrain coverage

Here, layout variation means six static buildings with independently varied centers (±3 m around fixed anchors), full widths/depths approximately 4–8 m and heights in [5, 24) m. The ground remains flat; the depot and four pads are fixed. Each selected model's final continuation stage recorded 30 distinct DAgger layouts and three completed PPO episodes on three layouts, in addition to earlier training. The allowed seed pool is not a claim that every seed was trained.

The roughly 26 m clearance-teacher prior overflies the generated buildings. These results do not establish hill/slope adaptation, flight through structures blocking that height, or reactive obstacle avoidance driven by range sensors. Those require a separately designed experiment and acceptance criteria.
