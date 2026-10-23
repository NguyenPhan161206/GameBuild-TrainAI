# DeliveryLab: camera-free drone logistics research

> For the current session/resume/Colab workflow, continuation study commands, and revised validation/test seed ranges, use [delivery-continuation.md](delivery-continuation.md). The original study and budgets below describe the first implementation.

## What is implemented

DeliveryLab extends the existing Unity AeroLab project. FlightLab remains available from its original menu. Open **AeroLab → Open delivery campus**, press Play, then **Bắt đầu / Tiếp tục**. The default is explicitly labelled **Tham chiếu theo luật**. Seed, stage and sensor fields in the Inspector are respected unless command-line options override them. To view an imported model in the Editor, set Mode to `flight-eval` and Flight Model to its `Models/<name>` resource path before Play. For dispatch inference, set Mode to `dispatch-eval` and both model paths. This label never implies a trained model.

The campus contains one depot/charger, four delivery pads, six seeded static buildings, one drone and up to eight pending orders. Each sortie loads one parcel, flies to its destination, lands and holds for one second, hands over for one second, then returns to the depot. A return is counted only after a stable landing. Each leg must have reached at least 2 m hull clearance; a ground-only arrival fails with `NoTakeoff`. Late orders remain deliverable but are not counted as on time. Pending queue overflow, overweight orders and infeasible route/energy budgets are reported as rejections. Future and pending orders remaining at the horizon are unfinished; assigned but undelivered parcels are failed. A delivered parcel stays delivered if the return flight subsequently fails, while the incident and missing safe return are reported separately.

Physics is the existing assisted, fixed-attitude 50 Hz C# integration with swept hull AABB collision detection. Delivery legs have a 120-second limit; original FlightLab retains 60 seconds. No second Python or PhysX physics implementation is used. Dimensions remain the original research hull (2 × 1 × 2 m), not a calibrated commercial drone.

`ScenarioConfig` is a programmatic configuration API; do not mutate its fields during an episode. The generator changes building centers, widths and heights independently. The reference controller runs four-neighbour A* at 16 m with hull and tracking margin, checks vertical approach segments and smooths visible path segments. The reference has a known map and exact state; it is not a learned policy and is not a sensor-limited comparator.

## Energy and safety semantics

Battery uses arbitrary research units, initially 100; reserve 12; depot charge rate 1 unit/s. Payload limit is 2 kg. Per flight step:

```
energy = (0.12 + 0.04 × payload_kg) × dt + 0.008 × distance_m
```

Payload changes consumption, not the simplified acceleration limits. Depot idle/loading/handover energy is omitted in this version. Preflight energy is a conservative reference-route estimate:

```
required = (roundtrip_path_length / 2 + 70) × (0.12 + 0.04 × payload_kg)
           + 0.008 × roundtrip_path_length + reserve
```

All dispatchers use the same payload, route and energy gate. This is not a guarantee that an arbitrary learned trajectory will have enough energy. Crossing reserve outbound fails that delivery and initiates return; no return route is `UnsafeReturn`; depletion, collision, bounds and flight timeout have distinct outcomes. The finite episode horizon is not an accident and is bootstrapped as an interrupted ML episode.

Land and flight permission statuses default to `unverified`. Neither generated buildings nor land ownership implies permission to fly.

## Camera-free observation contract

The spectator camera is exclusively for rendering. No camera sensor, image tensor, object-list observation or Python scene reconstruction is used by the flight policy.

- `FlightObservation flight-delivery-1`: 26 floats. Original 20 flight observations (goal delta, velocity, altitude/contact, ten ranges, leg time remaining, landing hold progress), then battery fraction, payload fraction, return flag, emergency flag, deadline slack and sample-valid flag.
- `FlightAction`: three continuous assisted commands in [-1,1]. Decision every five physics ticks (0.1 s); action held over intervening ticks. Every tick contributes reward, and stepping stops at the terminal event.
- Missing range readings are **-1**, distinct from zero clearance. Whole initial delayed samples have validity 0. Noise defaults: bounded uniform ±0.15 m position/height, ±0.1 m range, 1% independent range dropout and five ticks (0.1 s) delay. These are hypothetical stress parameters, not measured specifications. Velocity and contact remain ideal in v1.
- Samples are generated once per physics tick and cached; rendering or repeated observation reads do not consume additional random draws.
- `DispatchObservation dispatch-delivery-1`: 60 floats: battery, elapsed fraction, reserve fraction, delivered fraction, plus eight stable pending slots of seven values (presence, deadline slack, age, mass, destination X/Z, required energy fraction). Slots are sorted by release time then order ID.
- `DispatchAction`: one discrete branch with nine options: wait/charge, then the eight slots. Invalid assignment options are masked. Dispatch is possible only at the depot, at one-second decision intervals. An assigned parcel cannot be replaced mid-sortie.

The flight reward combines scaled core progress/landing reward, a small time cost and 0.001 per consumed energy unit, handover reward (10 on-time, 2 late), a one-time 0.5 takeoff milestone per leg, and a terminal incident penalty of 10. Dispatch receives the accumulated same reward across its variable-length decisions. PPO discounting is per dispatch decision, not continuous simulated time; compare operational metrics directly rather than interpreting reward as a calibrated time-discounted utility. This is a scalar approximation to the stated priorities, not a mathematical guarantee of lexicographic safety/deadline optimality. Hard dispatch eligibility is outside either learned policy.

## Setup, checks and builds

From the repository root:

```bash
bash tools/setup-delivery-ai.sh
bash tools/check-core.sh
bash tools/check-delivery-unity.sh validate
bash tools/check-delivery-unity.sh smoke
bash tools/check-delivery-unity.sh build
```

Stop this project's Editor before batch commands. The GUI menu alternatives are **AeroLab → Validate delivery project**, **Run delivery Play Mode smoke**, and **Build → Delivery Linux x64**. Batch verification may be run in a source copy to avoid disturbing an open Editor; the checked copy must match current source and dependency lock. The default build contains DeliveryLab first and FlightLab second. Every build writes a new immutable `Builds/DeliveryVersions/<id>` directory, verifies the native trainer plugin, then atomically switches the launcher at `Builds/Delivery/AeroLabDelivery.x86_64`. Active players keep their existing binary/data files. Keep the launcher and version directories together when moving a build.

Tool versions: Unity 6000.6.7f1, Python 3.10.12, Python ML-Agents/mlagents-envs 1.1.0, Unity ML-Agents 4.0.0, PyTorch 2.2.2 CPU, setuptools 80.9.0. `training/requirements-observed.txt` records the full resolved Python set. The install script pins key compatibility dependencies; use this observed list for exact replay.

Unity 6000.6 treats `GetInstanceID()` in upstream ML-Agents' Match3 integration as a compilation error. `tools/prepare-mlagents.py` verifies the official 4.0.0 archive SHA-256, extracts to ignored `artifacts/packages`, and applies a conditional `GetEntityId().GetHashCode()` substitution for Unity 6000.6+. It preserves the original call on older Unity versions. It also explicitly sets the Linux native gRPC importer CPU to `x86_64`; Unity 6000.6 otherwise places the binary in `Plugins/AnyCPU`, outside the managed loader's search paths. No trainer, policy, protobuf or drone behavior is patched. Run setup before importing the project in a fresh checkout. The Unity dependency resolves to this prepared local package; do not commit the generated dependency tree. Unity 6000.6 may raise inference dependencies above their upstream minimums; the package lock records actual resolution.

The batch helper redirects writable caches but preserves the user's Unity licensing configuration. It never installs or activates a Unity license. A valid local license remains required.

For standalone reference evaluation (no Unity rendering/training needed), use .NET SDK 8:

```bash
dotnet run --project tools/DeliveryRunner --configuration Release -- 100 20000 artifacts/delivery-baselines.json
```

Arguments: episode count per condition, first seed, output. It evaluates both FIFO and earliest-deadline on the same ideal/noisy scenario streams. Noise does not affect the exact-state reference pilot, so identical reference ideal/noisy outcomes are expected.

## Reproducible research sequence

Training and assessment are separate operations. Frozen inference uses deterministic action selection; exploration stays enabled during PPO training. Do not tune on the final test seeds.

- Training scenarios: seeds 0–7999, cycling within that range. Independent trainer seeds: **11, 29, 47**.
- Validation: 10000–10099; choose checkpoint/configuration here.
- Test: 20000–20099, held out until model/configuration are frozen.
- Stages: 0 = single order, no buildings; 1 = single order, varied buildings; 2 = full order stream and return cycles; 3 = same full stream, with `--noisy` for sensor stress. The stage argument alone does not enable noise.

The full three-seed sequence can be run with `.venv/bin/python tools/run-delivery-study.py --tag research-v1`. It builds imported models, validates all three flight policies, stops at a failed flight gate, then trains dispatch and runs the held-out ideal/noisy comparisons. `--smoke --tag wiring-v1` instead uses 4096 flight decisions, 128 dispatch decisions with a small PPO buffer, and two validation episodes strictly to check both adapters/export/reload; it does not qualify models and deliberately bypasses the research gate. Each tag/output must be unique. `--resume` reuses completed training/checksums and resumes saved checkpoints; use it only with unchanged environment code/configuration. `--check-project` optionally builds a verification copy without touching an open Editor.

For each trainer seed, run these four flight stages in order (shown for seed 11):

```bash
.venv/bin/python tools/train-delivery.py flight --seed 11 --stage 0
.venv/bin/python tools/train-delivery.py flight --seed 11 --stage 1 --initialize-from flight-s11-stage0
.venv/bin/python tools/train-delivery.py flight --seed 11 --stage 2 --initialize-from flight-s11-stage1
.venv/bin/python tools/train-delivery.py flight --seed 11 --stage 3 --noisy --initialize-from flight-s11-stage2
```

Defaults are 500,000 PPO decisions per stage, 128-unit/two-layer networks and CPU, with checkpoints and TensorBoard summaries. `--steps 2048` is a connection/export smoke budget, never research completion. Use `--resume` only to resume the same run. Invocation files record trainer seed, scenario start seed and configuration hash. Optional expert demonstrations use player `--delivery-mode flight-demo`; they are not enabled by the default PPO configuration.

Import each candidate ONNX under a unique resource name, rebuild, then validate:

```bash
.venv/bin/python tools/freeze-delivery-model.py results/flight-s11-stage3-noisy/DeliveryFlight.onnx --layer flight --name flight-s11
bash tools/check-delivery-unity.sh build
.venv/bin/python tools/evaluate-delivery.py --mode flight-eval --flight-model Models/flight-s11 --split validation --stage 1 --name flight-s11-validation
```

“Frozen” means immutable weights/name/checksum; **not yet qualified**. The generated provenance defaults to `not_evaluated`. Store evaluation evidence separately; `--evaluation` can attach existing evidence to a new immutable resource copy. Do not claim qualification from export or successful inference alone.

The flight gate is at least 90/100 complete single-order roundtrips on ideal stage-1 validation scenarios, with no rejected/failed parcel in the successful episodes. If the gate fails, retain the failure report and improve using training/validation only; do not advance to claims of qualified logistics AI.

After the flight gate, freeze that flight resource and train dispatch independently for the same three trainer seeds:

```bash
.venv/bin/python tools/train-delivery.py dispatch --seed 11 --stage 3 --flight-model Models/flight-s11
```

Dispatch defaults to 100,000 decisions. Import `DeliveryDispatch.onnx` similarly with `--layer dispatch --name dispatch-s11`, rebuild, and evaluate using `--mode dispatch-eval --flight-model Models/flight-s11 --dispatch-model Models/dispatch-s11 --stage 3`. For fair FIFO/EDD comparisons with the **same learned flight model**, use `--mode flight-eval --stage 3 --rule fifo` and `--rule edd`; a fully scripted flight baseline is also reported separately.

Once settings are fixed, evaluate each frozen model on 100 test episodes. Run ideal and `--noisy` separately under unique names. Flight qualification uses stage 1; logistics comparisons use stage 3. Include all outcomes and all generated orders in the denominator, not just successful deliveries. Aggregate three independent model summaries:

```bash
.venv/bin/python tools/summarize-delivery.py artifacts/evaluations/seed11/summary.json artifacts/evaluations/seed29/summary.json artifacts/evaluations/seed47/summary.json --output artifacts/evaluations/aggregate.json
```

Review invocation metadata as well as model names to verify independence. Summary files report mean and sample standard deviation; no superiority claim is automatic. Episode reports include full generated configuration/buildings/orders, sensor setup, schema versions, model identity, every order outcome, energy, distance, elapsed time and safe returns. Model provenance adds the ONNX checksum. Keep reports, checkpoints, exported models and binaries outside Git; preserve the source/configuration and dependency locks.

## PPO with demonstration support

When pure PPO fails to discover delivery/handover, the helper also supports explicit behavior cloning alongside PPO. This is an additional experiment, not a replacement of the reported pure-PPO results. Collect vector demonstrations using `--delivery-mode flight-demo`; the teacher is the map-based reference controller, not a trained AI. Validate the teacher's completed roundtrips and keep demonstration seeds below 8000, separate from validation/test.

```bash
.venv/bin/python tools/train-delivery.py flight --seed 11 --stage 0 --steps 50000 --time-scale 100 --tag imitation-v1 --demo artifacts/datasets/expert-stage0-v1.demo
```

`--demo` enables the SDK's behavioral-cloning loss with strength 1, three epochs and 256-sample batches. Input normalization is disabled for this experiment because the published core observations already use physical scaling; demonstrations and live samples share those scales. The invocation records the demonstration checksum. The exported policy still receives only the 26 vector observations at inference; the teacher's map/object list is not an input. Use a new tag when changing algorithm, data, configuration or environment. `--time-scale` changes wall-clock execution speed while the physics tick stays 0.02 seconds.

## Supervised controller experiments

`ClearancePilot` is an additional **scripted teacher**, not an AI model. It uses goal-relative position, altitude and velocity and assumes this generator's buildings are below 24 m, with clear pad approaches; its cruise prior is 26 m. It does not establish range-based obstacle reasoning. Core regression checks cover its ideal roundtrips, separately from learned-policy qualification.

The optional state-coverage experiment emits observation/action pairs from the C# core. The dataset includes synthetic poses and deliberately randomized nuisance range values; these are not physically consistent flight trajectories or measured hardware data. `pretrain-flight.py` fits a 128 × 128 ML-Agents actor to these labels. The fit's validation MSE is **not** a mission completion metric. `fit-policy-bridge.py` optionally collects DAgger labels during shared-core trajectories through a JSON-lines subprocess: Python runs the neural network, C# alone advances physics. Teacher mixing is logged and is never permitted in the Unity qualification run.

```bash
dotnet run --project tools/DeliveryRunner --configuration Release -- --control-samples artifacts/datasets/clearance-v1.bin 100000
.venv/bin/python tools/pretrain-flight.py --dataset artifacts/datasets/clearance-v1.bin --spec-demo artifacts/datasets/expert-stage0-v1.demo --seed 11
.venv/bin/python tools/fit-policy-bridge.py --source-run supervised-v1-s11 --output-run dagger-v1-s11 --demo artifacts/datasets/expert-stage0-v1.demo --seed 11
```

The demonstration file supplies the actual ML-Agents behavior specification. It is not used as the labels in this state-coverage fit. Each command creates a new result directory and refuses to overwrite it. Bridge scenarios use training seeds only; qualified results must still come from the immutable ONNX in the native Unity player, with zero teacher mixing. These experiments extend the initial PPO research plan; report them separately from PPO and do not relabel the scripted teacher as learned AI.

For PPO fine-tuning of a supervised checkpoint, preserve `--no-normalize`. Optional `--learning-rate` and `--entropy-coefficient` are written into the run configuration. The actor-only checkpoint intentionally initializes a fresh PPO critic/optimizer; the SDK reports those absent modules instead of pretending to resume them. For example:

```bash
.venv/bin/python tools/train-delivery.py flight --seed 11 --stage 1 --steps 10000 --time-scale 200 --no-normalize --learning-rate 0.00003 --entropy-coefficient 0 --initialize-from dagger-v2-s11 --tag fine-v1
```

Native evaluation defaults to `--time-scale 200`; `--time-scale 30` restores the original evaluation wall-clock speed. Both hold the same 0.02-second C# timestep and five-tick policy decision period. Speed is recorded in the summary and is never a reason to change physical noise, deadlines or episode duration. The direct player flag is `--evaluation-time-scale` and does not override the Python trainer's engine side channel.

A further `--control-features-only` experiment selects observation columns **0, 2, 3, 4, 5, 6** (goal X/Z, velocity XYZ, altitude) for the clearance teacher. `flight_feature_policy.py` zeroes the other input-layer columns, masks their gradients and reapplies the mask on checkpoint load. The optional Python entrypoint wraps the SDK actor constructor; it does not edit SDK files or implement physics. The resulting ONNX is still an ordinary 26-input ML-Agents graph, with zero weights for unselected inputs. Record this choice in the invocation and DAgger metadata. It intentionally does **not** demonstrate range-based obstacle reasoning: it distils a height-prior strategy for this bounded generator.

```bash
.venv/bin/python tools/fit-policy-bridge.py --source-run dagger-v2-s11 --output-run masked-v1-s11 --demo artifacts/datasets/expert-stage0-v1.demo --seed 11 --iterations 3 --episodes 10 --epochs 60 --teacher-probability 0.5 --scenario-start 2200 --control-features-only --stage 3
```

Teacher probabilities apply only while collecting training trajectories. Native evaluation never invokes the teacher, feature-label generator or Python network. DAgger intermediate teacher-mixed completion counts are training diagnostics, not qualified AI results.
