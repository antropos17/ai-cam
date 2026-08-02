# BugCam — Execution Plan (2 days + buffer)

Goal: on a pure-physics Unity scene — find the smallest perturbation that changes the outcome → ghost-trajectory fan → retro replay from 4 angles → export MP4 + evidence card. Plus hero video and landing page.

Everything else in SPEC.md (CI, attribution, capsule viewer, subscription) is backlog. Do not touch it these two days.

---

## DAY 1 — Core & proof

### Block 1.1 — Scene & harness
- Unity 6.3 LTS URP project `BugCam`, editor `6000.3.20f1` or newer within 6.3 — never 6.5 (rationale in `STATUS.md` Decisions log). Demo scene = test scene = `Assets/BugCam/Tests/TowerScene.unity`: tower of 40–60 Rigidbody cubes + one projectile/impulse, generated procedurally by an Editor menu item (no hand-authored `.unity`). Tune for sensitivity (tall, narrow base, low friction).
- `SimulationHarness`: Simulation Mode = Script; local `PhysicsScene`; run = N × `Simulate(0.02f)`; clone scene into isolated physics world from saved initial state. PhysicsScene is recreated per run by default.
- Physics simulation threading: Unity 6.3 exposes a multi-threaded / single-threaded simulation switch. Its exact API and settings name is read off the installed `6000.3.20f1` editor (Project Settings → Physics, cross-checked against that editor's Scripting API reference) — never guessed from memory. Its value joins the physics settings snapshot, and `DeterminismProbe` prints the mode it ran under. No mode is preferred in advance: the Block 1.1 measurement decides, not this plan.
- VERIFY: two identical runs match within 1e-6 per component (the gate). Report numbers, not pass/fail: `bitwiseEqual`, `maxComponentDelta`, `firstDivergingStep`, `firstDivergingBody`, `managedBytesAllocatedInLoop`, `simulationThreadingMode`. Run order A, B, A′ (B = perturbed) so cross-run state leakage is visible. Repeat once after an Editor restart to separate in-session from cross-session determinism. **Run the whole probe in BOTH threading modes and record `maxComponentDelta` for each** — single-threaded removes solver thread ordering as a variable, and the delta between the two modes is a measured finding for `STATUS.md` and for the README caveat "solver effects exist". If the gate fails: fix instantiation order, then record it as a finding — do not hide it.

### Block 1.2 — State recording
- `StateRecorder`: per physics step per body — position, rotation, linear/angular velocity, sleeping. Flat preallocated arrays `[runs × steps × bodies × 14]`, field order `pos.xyz, rot.xyzw, vel.xyz, angVel.xyz, sleeping(0f|1f)`.
- `RunResult`: frames + metadata — `epsilon` (metres), `perturbation` (axis / magnitude / bodyId), `stepCount`, `simulatedTime = steps × FixedStep`, `seed = 0` (reserved; non-zero only for a randomized perturbation mode). Wall-clock, if recorded at all, lives in a separate `wallClockMs` field excluded from every comparison and hash.
- VERIFY: kinematic replay reproduces the recorded transforms frame for frame — `maxComponentDelta == 0`, asserted in the harness. Visual inspection is an optional extra, never the verification.

### Block 1.3 — Divergence Engine
- Per-body per-frame: posError/objectScale, rotation angle error, velocity error, sleep mismatch. Scene Divergence Score = weighted sum.
- Significant = score > threshold, sustained ≥ 5 steps, AND ≥ 1 tracked body exceeds `DivergenceSettings.PerBodyPositionThreshold`.
- Output: firstDivergenceFrame, maxSpread (m), affectedBodies, amplification = maxSpread(m) / epsilon(m) — both in metres; mm is display formatting only.

**`DivergenceSettings` field contract.** `ScriptableObject` with `[CreateAssetMenu]`; defaults live in code, no hand-written `.asset` YAML.

| Field | Unit | Role |
|---|---|---|
| `PerBodyPositionThreshold` | metres | the "meaningfully affected" condition |
| `PerBodyRotationThreshold` | degrees | per-body rotation gate |
| `PerBodyVelocityThreshold` | m/s | per-body velocity gate |
| `SceneScoreThreshold` | — | weighted-sum gate |
| `SustainedSteps` = 5 | steps | consecutive steps required |
| `WeightPosition` / `WeightRotation` / `WeightVelocity` / `WeightSleep` | — | weights of the Scene Divergence Score |
| `MinEvidenceCoverageScore` | — | Block 2.1 honest-verdict gate |

Epsilon search (Block 1.4) — same asset, per `CLAUDE.md` "all thresholds live in one `DivergenceSettings`":

| Field | Unit | Role |
|---|---|---|
| `EpsilonStart` = 1e-5 | metres | first magnitude of the exponential search (0.01 mm) |
| `EpsilonGrowthFactor` = 2 | — | multiplier per exponential step |
| `EpsilonCeiling` = 1e-2 | metres | upper bound of the tested range (10 mm); above it the verdict is `STABLE WITHIN TESTED RANGE` |
| `BisectionIterations` | steps | binary-search depth; the single default is fixed in Block 1.4 (range 6–8 was never narrowed) |
| `LadderPointCount` = 12 | points | log-uniform monotonicity ladder run before bisection is trusted |
| `FanMultipliers` = {0.8, 0.9, 1.0, 1.1, 1.2} | × threshold | fan spread; 5 multipliers × 3 axes = 15 runs + baseline |

Evidence (Blocks 1.5, 2.1) — same asset:

| Field | Unit | Role |
|---|---|---|
| `GhostBodyLimit` = 10 | bodies | top-N diverging bodies drawn as ghosts, plus baseline |
| `EvidenceCandidateCount` | candidates | Fibonacci-sphere N; recorded in `camera-plan.json` |
| `EvidenceOcclusionRays` = 9 | rays | AABB centre + 8 corners; fractional occlusion = hits / rays |
| `EvidenceTopScoreFraction` = 0.25 | — | survivor filter applied before optimizing cameras 2–4 |
| `WeightEvidenceCentrality` | — | frame-edge penalty in candidate scoring |

Rules:
- Every default value carries a one-line comment stating **why that number**, not merely what it is.
- No threshold may be referenced anywhere in `Core/` or `Evidence/` unless it exists in this contract.
- If a later block needs a new threshold, it is added to this contract first, in the same commit.

- VERIFY: unit tests on synthetic trajectories (known divergence frame must be found exactly; pure noise must yield none).

### Block 1.4 — Adaptive Epsilon Search
- Search range is `EpsilonStart`…`EpsilonCeiling` (default 0.01 mm…10 mm). Characterization (fan) may reach `1.2 × EpsilonCeiling`; do not silently clamp fan magnitudes — mark every fan sample above the search ceiling `OutsideSearchRange=true`. Console/result data report search range separately from characterization range.
- Step-driven state machine (`TryGetNextProbe` / `SubmitProbeResult`): baseline → 12-point log-uniform ladder → (if monotonic) exponential by strategy → bisection → fan. Unity runner executes probes sequentially and waits for prior local PhysicsScene cleanup.
- Ladder first: non-monotonic → verdict `NON-MONOTONIC WITHIN TESTED RANGE`, skip bisection, preserve the ladder, fan around the smallest observed divergent ladder sample labeled as **reference epsilon** (not an exact threshold). No divergence through the ceiling → `STABLE WITHIN TESTED RANGE` (no invented threshold, no fan). Monotonic bracket → report largest tested stable epsilon, smallest tested divergent epsilon, threshold **estimate** = smallest tested divergent, final bracket width — never claim an exact mathematical threshold.
- Fan remains exactly `{0.8, 0.9, 1.0, 1.1, 1.2} × reference epsilon × X/Y/Z` (15 runs) plus retained baseline. Full `RunResult` frames only for baseline + those 15 fan runs; ladder/exponential/bisection keep compact summaries.
- VERIFY, two separate checks: (a) a repeat with identical configuration must be bit-identical — this is the determinism regression test, not a convergence test; (b) **algorithm convergence** compares different starting strategies (`AscendFromStart` 0.01 mm ×2; `AscendFromCustomStart` 0.02 mm ×2; `DescendFromCeiling`) on the **same axis, same scene, same target body, and same configuration** — they must land within ±1 bisection/growth step of each other. X/Y/Z searches are directional characterization; their physical thresholds are reported but are **not** required to match.

### Block 1.5 — Ghost visualization
- Trajectories of all runs: LineRenderer or DrawMeshInstanced ghosts. Baseline white, runs colored by divergence magnitude. Red sphere at first divergence. Show only top-10 diverging bodies + baseline.
- Scene View gizmos are sufficient today.

### DAY 1 CHECKPOINT (hard gate)
Console output with threshold / first divergence frame / spread / amplification / affected count AND a visible fan in Scene View. Do not start Day 2 without it.

---

## DAY 2 — Evidence & packaging

### Block 2.1 — Retro replay
- `RetroPlayer`: kinematic frame-by-frame playback in main scene, time scrub, slow-mo 0.1–0.25×.
- 4 evidence cameras around the first-divergence point, chosen by a **deterministic scored post-process** — not by hand-placed roles and not by a binary visibility test. 2×2 split via viewport rects or RenderTextures.

**Timing.** Selection runs AFTER both the baseline and the perturbed replay are complete, as a post-process over recorded trajectories. Never per-frame during simulation.

**Candidates.**
- Deterministic Fibonacci sphere around the divergence event; candidate count N is recorded in the manifest. No `Random` anywhere in the path.
- Sphere radius derives from the divergence-event bounds only — not the union of all affected bodies.
- Candidates below the ground plane are discarded before scoring.

**Scoring** — per candidate, summed over affected bodies:
- in-frustum: `GeometryUtility.CalculateFrustumPlanes` + `GeometryUtility.TestPlanesAABB`
- occlusion: fractional, 9 raycasts per body (AABB centre + 8 corners), score = hits / 9. Never binary.
- screen-space separation between baseline and perturbed positions via `Camera.WorldToViewportPoint`, measured in pixels
- centrality penalty for bodies near the frame edges
- ties broken strictly by candidate index, never by float comparison

**Winners.**
- Camera 1 = highest score.
- Cameras 2–4: filter to the top 25% by score FIRST, then optimize within the survivors for (a) orthogonality to camera 1, (b) contact proximity, (c) trajectory alignment. Constraint-then-optimize, in that order.

**Honest verdict (required).** If the best candidate score falls below `DivergenceSettings.MinEvidenceCoverageScore`, output `EVIDENCE COVERAGE: LOW` with the best score, the count of affected bodies visible, and the reason. This is a valid result in the same sense as `STABLE WITHIN TESTED RANGE` — do not emit four poor cameras instead.

**Manifest** (`camera-plan.json`, written when the capsule lands on Day 3): algorithm version, candidate count N, and the score of EVERY candidate including the rejected ones — provenance requires the losers. Per chosen camera: bodies in frame, distances, fractional occlusion values, final score.

**Constraints.** No Cinemachine, no MCP, no `com.unity.perception` (abandoned since Nov 2024). Plain `UnityEngine.Camera` plus the three APIs named above. Nothing lands in `Core/` — this lives in `Evidence/EvidenceCameras.cs`.

- VERIFY: selection is reproducible bit-for-bit by a third party from the same recorded runs — re-running selection over identical recorded trajectories yields identical candidate scores, identical winner indices, and an identical `camera-plan.json`.

### Block 2.2 — Evidence overlay + export
- UI Canvas overlay: test numbers, frame counter, timeline, logo.
- Unity Recorder: MP4 1080×1920 and 1920×1080. Evidence card: PNG 1200×630 via RenderTexture → EncodeToPNG.

### Block 2.3 — EditorWindow
- Single `BugCam` window: target root, duration, epsilon range, `Run Butterfly Test` / `Export` buttons, progress bar, results text, `Focus divergence` button. Minimal styling.

### Block 2.4 — Hero video shoot (human task, agent prepares assets)
- Fixed 22s script (see SPEC.md §17). Agent outputs: shot list, exact overlay text files, camera-plan, raw footage export presets. Human edits in CapCut. Zero occurrences of "AI".

### Block 2.5 — Landing + waitlist
- One page: video, three numbers, `Get early access`, "Send me your fragile scene" form. Headline: `BugCam finds how fragile your scene is — and proves it with a replay.` Deploy: GitHub Pages / Vercel.

### DAY 2 CHECKPOINT (hard gate)
Full cycle "press button → get MP4" with zero manual editing + published video + live landing.

---

## DAY 3 — Buffer (strict priority order)
1. Stabilization: run untouched core on a second scene (dominoes / Rube Goldberg).
2. `.bugcam` export v0: one JSON + trajectory binary + preview PNG. No viewer.
3. UPM packaging: package.json, README with honest caveats (sensitivity ≠ bug; environment-scoped repeatability; solver effects exist).
4. Second short video (Butterfly Test #002).

---

## Known risks → mitigations
| Risk | Mitigation |
|---|---|
| Runs don't match within 1e-6 | PhysicsScene is already recreated per run; fix instantiation order; if it persists, record as a finding (solver/order sensitivity) |
| Scene too stable, no fan | Taller tower, narrower base, less friction; perturb projectile instead of brick |
| 16 runs × 250 steps slow | Simulate without rendering (milliseconds); render only replay |
| Recorder won't record in Edit Mode | Replay in Play Mode. Production `SimulationHarness` also requires Play Mode — local `PhysicsScene` via `SceneManager.CreateScene(..., LocalPhysicsMode.Physics3D)` is a runtime path (Edit Mode contract tests assert the deterministic failure; simulation correctness lives in `BugCam.Tests.PlayMode`) |
| 800-line ghost spaghetti | Top-10 bodies only + baseline |
