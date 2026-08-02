# BugCam — STATUS

> Updated by the agent after every completed block. This file is the memory between sessions.

## Current position
- Active block: 1.4 (Block 1.3 DivergenceEngine VERIFY — see evidence below)
- Day 1 checkpoint: NOT PASSED (needs Blocks 1.4–1.5 threshold/fan/ghost console + Scene View fan)
- Day 2 checkpoint: NOT PASSED

## Completed blocks
| Block | Result | Verification | Commit |
|---|---|---|---|
| docs | Spec contradictions resolved (11 items), git repo initialised | Review approved by human 2026-07-29 | `chore: docs + claude setup`, `docs: fix spec contradictions` |
| 1.1 | TowerScene A/B/A′ determinism probe in both threading modes + Editor restart | See Evidence log 2026-08-02 | squash `91ae29d` (#2) |
| 1.2 | StateRecorder + RunResult + kinematic transform replay VERIFY | See Evidence log 2026-08-02 (Block 1.2) | squash `a90765a` (#3) |
| 1.3 | DivergenceSettings + DivergenceEngine synthetic + RunResult integration | See Evidence log 2026-08-02 (Block 1.3) | `feat/block-1.3-divergence-engine` |

## Open findings / blockers
- OPEN (needs a human call): the fan `1.2 × threshold` can exceed the `EpsilonCeiling` of 10 mm when the threshold lands above 8.33 mm. Reporting a fan run from outside the tested range while the verdict says "within tested range" is exactly the kind of dishonest output `CLAUDE.md` forbids. Two candidate resolutions, neither chosen: clamp fan magnitudes to the ceiling and record the clamp in `RunResult`, or extend the tested range to `1.2 × ceiling` and say so in the verdict line.
- OPEN (needs a human call): `.gitignore` line 21 excludes `*.csproj`, and the headless test csproj deferred to Block 1.3 is a hand-authored file that would therefore be silently untracked. Either force-add it (`!BugCam.Tests.csproj`) or generate it from a committed script.
- OPEN (wording): `CLAUDE.md` line 33 says bitwise equality is "never used as the gate", while `PLAN.md` Block 1.4 VERIFY (a) makes bit-identical a hard pass condition. The two were reconciled in the 2026-07-29 decision below (1.4a is a determinism regression test, not the feature gate) but `CLAUDE.md` still reads as an absolute. One clarifying clause fixes it.
- RESOLVED (code path): local `PhysicsScene` via `SceneManager.CreateScene(..., LocalPhysicsMode.Physics3D)` requires Play Mode. Production harness now fails deterministically outside Play Mode; simulation tests live in `BugCam.Tests.PlayMode`. See Evidence log 2026-08-01.
- RESOLVED (Block 1.1 measurement): TowerScene dual-threading A/B/A′ numbers exist under `Library/BugCamEvidence/Block1.1/` (gitignored). `m_ThreadingMode` restored to `0` after the controlled experiment.

## Evidence log

### 2026-08-02 — Block 1.3 Divergence Engine (`feat/block-1.3-divergence-engine`)

**VERIFIED FACT — formulas (metres / degrees; mm only at display):**
- Object scale = characteristic body size in metres (box: largest local-scale axis). Null ⇒ 1 m for every body. Non-positive/non-finite scale ⇒ fall back to 1 m (never NaN/Infinity in posNorm).
- Per body, per frame: `posNorm = |Δpos| / objectScale`, `rotNorm = angleDegrees(q,q') / PerBodyRotationThreshold` (q and −q identical), `velNorm = |Δvel| / PerBodyVelocityThreshold`, `sleep ∈ {0,1}`.
- Scene Divergence Score (step) = `Σ_i (Wp·posNorm + Wr·rotNorm + Wv·velNorm + Ws·sleep)` (weighted sum, not a mean; default gate 1.0).
- Significant iff score > `SceneScoreThreshold` AND ≥1 body has `|Δpos| > PerBodyPositionThreshold` AND both hold for `SustainedSteps` consecutive frames. `firstDivergenceFrame` = first frame of that window.
- `amplification = maxSpreadMetres / epsilonMetres` when epsilon > 0; when epsilon == 0 → `AmplificationDefined=false`, `Amplification=0` (never Infinity).

**ASSUMPTION:** default thresholds in `DivergenceSettings` (e.g. position 1 mm, scene score 0.05, weights) are provisional product defaults with why-comments; Block 1.4/2.1 may ratify provisional evidence/search fields.

**LIMITATION:** engine is a post-process over recorded frames — it does not search epsilon, draw ghosts, or claim cross-machine repeatability.

**OPEN QUESTION:** whether provisional `MinEvidenceCoverageScore` / `EvidenceCandidateCount` / `BisectionIterations=7` survive Block 1.4/2.1 unchanged.

**Block 1.2 quaternion caveat (preserved):** Transform replay is component-exact for the verified identity-rotation fixture. Arbitrary normalized quaternions may differ by approximately one ULP after Readback while remaining rotationally equivalent.

**VERIFIED FACT — batchmode Unity `6000.3.21f1`:**

| Suite | total | passed | failed | result | XML |
|---|---|---|---|---|---|
| EditMode | 34 | 34 | 0 | Passed | `Library/BugCamEvidence/Block1.3/EditMode.xml` |
| PlayMode | 11 | 11 | 0 | Passed | `Library/BugCamEvidence/Block1.3/PlayMode.xml` |

EditMode +21 (`DivergenceEngineTests` — 20 required synthetic cases + settings defaults). PlayMode +1 (`AnalyzeAcceptsRealRunResultsFromHarness`). Blocks 1.1/1.2 regressions included.

**Block 1.3 VERIFY conclusion:** PASSED. Day 1 hard gate remains NOT PASSED until Blocks 1.4 and 1.5 pass.

**Residual tails:** checkpoint asserts `enhancedDeterminism == false`; EditMode contract asserts `StateStride == 14` and `RepeatabilityGate == 1e-6`.

**VERIFIED FACT — checkpoint runner tracked:** `Tools/BugCam/run-checkpoint.ps1` is the repository-portable development automation entry point (resolves project root from `$PSScriptRoot`, optional `-UnityExe`, discovers Unity `6000.3.21f1`, writes evidence under gitignored `Library/BugCamEvidence/`). Development automation only — not a BugCam Core dependency and not product runtime.

### 2026-08-02 — Block 1.2 StateRecorder + kinematic replay (`feat/block-1.2-state-recorder`)

**VERIFIED FACT — batchmode Unity `6000.3.21f1`, project junction `X:\bugcam` → this worktree:**

| Suite | total | passed | failed | skipped | result | XML |
|---|---|---|---|---|---|---|
| EditMode | 13 | 13 | 0 | 0 | Passed | `Library/BugCamEvidence/Block1.2/EditMode.xml` |
| PlayMode | 10 | 10 | 0 | 0 | Passed | `Library/BugCamEvidence/Block1.2/PlayMode.xml` |

EditMode +5 contract tests (`StateRecorderContractTests`). PlayMode +2 (`StateRecorderPlayModeTests`), including `KinematicReplayReproducesRecordedTransformsWithZeroDelta`.

**VERIFIED FACT — Block 1.2 VERIFY:** kinematic transform replay reports `maxComponentDelta == 0` for a recorded falling-body run with identity rotation (`PlayMode.xml` / `summary.txt`).

**VERIFIED FACT — implementation surface in `BugCam.Core`:**
- `StateRecorder` — preallocated `[runs × steps × bodies × 14]`; `WriteBody` / `WriteRigidbodies`; rotations stored normalized
- `RunResult` — frames + `epsilonMetres`, `perturbation`, `stepCount`, `simulatedTime`, `seed`, `wallClockMs` (excluded from comparisons)
- `KinematicReplayer` — Play Mode temporary scene; `SetLocalPositionAndRotation` round-trip; harness uses `StateRecorder` for per-step capture

**FINDING:** Unity Transform readback of arbitrary (non-identity) Euler quaternions can introduce ~1 ULP after renormalization (`1.19e-7` observed). VERIFY used identity rotation so `maxComponentDelta == 0` is honest for the transform components under test. Not a physics determinism failure.

**Block 1.2 VERIFY conclusion:** PASSED. Day 1 hard gate remains NOT PASSED until Blocks 1.3–1.5.

### 2026-08-02 — Block 1.1 TowerScene determinism checkpoint (`feat/tower-probe-checkpoint`)

**VERIFIED FACT — batchmode Unity `6000.3.21f1`, project junction `X:\bugcam` → this worktree:**

| Suite | total | passed | failed | skipped | result | XML |
|---|---|---|---|---|---|---|
| EditMode | 8 | 8 | 0 | 0 | Passed | `Library/BugCamEvidence/Block1.1/pre-restart/EditMode.xml` |
| PlayMode | 8 | 8 | 0 | 0 | Passed | `Library/BugCamEvidence/Block1.1/pre-restart/PlayMode.xml` |

PlayMode gained one test: `TowerSceneCheckpoint_RecordsAbaMetricsForCurrentThreadingMode` (7 → 8).

**VERIFIED FACT — environment snapshot** (`pre-restart/environment.txt` / `post-restart/environment.txt`):
- Unity `6000.3.21f1`, platform `WindowsEditor`, OS `Windows 11 (10.0.26200)`
- Scripting backend `Mono2x` (PlayerSettings); probe runs report `EditorPlayMode`
- `simulationMode=Script`, gravity `(0, -9.81, 0)`, solver 6 / velocity 1
- `autoSyncTransforms=False`, `enhancedDeterminism=False`
- `fixedDeltaTime≈0.02` (`0.0199999921` float), harness step constant `0.02`
- Serialized `m_ThreadingMode`: `0` = MultiThreaded, `1` = SingleThreaded (confirmed by apply/read-back + behavior)
- Baseline ProjectSettings restored to `m_ThreadingMode: 0` after experiments
- Evidence git commit stamp during runs: `3c7dfc3022944b55b21b8ffb1c584e5e9fde39f2` (main merge base before this block commit)

**VERIFIED FACT — TowerScene A/B/A′ metrics** (49 bodies, 250 steps, stride 14, gate 1e-6; evidence under `Library/BugCamEvidence/Block1.1/`):

| Phase | Mode | A vs A′ bitwiseEqual | A vs A′ maxComponentDelta | withinGate | A vs B maxComponentDelta | firstDivergingStep / Body | managedBytesAllocatedInLoop | sceneCleanup |
|---|---|---|---|---|---|---|---|---|
| pre-restart | MultiThreaded (0) | True | 0 | True | 7.313021 | 0 / 49 | 0 | True |
| pre-restart | SingleThreaded (1) | True | 0 | True | 7.313021 | 0 / 49 | 0 | True |
| post-restart | MultiThreaded (0) | True | 0 | True | 7.313021 | 0 / 49 | 0 | True |
| post-restart | SingleThreaded (1) | True | 0 | True | 7.313021 | 0 / 49 | 0 | True |

**VERIFIED FACT — restart comparison:** for each threading mode, post-restart metrics files are byte-identical to pre-restart except the `phase=` line. Full Editor process exit + new process (not domain reload).

**VERIFIED FACT — cleanup / physics validity:** `physicsSceneValidity=True`, `sceneCleanupResult=True`, temporary local Physics3D scenes unload to the initial scene count.

**INFERENCE:** On this machine/editor, MultiThreaded and SingleThreaded produced the same A/A′ and A/B summary metrics for this TowerScene probe (including identical A/B `maxComponentDelta`). That does not prove solver-thread ordering is never a factor on other scenes/hardware.

**OPEN QUESTION:** Whether A/B `maxComponentDelta` remains numerically stable across non-identical CPU/OS configs (environment-scoped repeatability caveat still stands).

**Block 1.1 VERIFY conclusion:** PASSED. Day 1 hard gate (threshold/fan/ghost console) remains NOT PASSED until later blocks.

### 2026-08-01 — PhysicsScene test lifecycle (PR #1 / `fix/physics-scene-test`)

**Old claim:** `PLAN.md` risk table said “Replay in Play Mode; harness in Edit Mode.” STATUS also listed local PhysicsScene outside Play Mode as OPEN/unverified.

**New code evidence:**
- `SimulationHarness.Run` returns `SimulationRunResult.Failure("SimulationHarness requires Play Mode because it creates an isolated local Physics3D scene.")` when `!Application.isPlaying`, before `SceneManager.CreateScene`.
- Misleading `EditorSceneManager.NewScene` comment removed; comment now states the Play Mode isolation model.
- Runtime simulation assertions remain in `BugCam.Tests.PlayMode` (not EditMode; no EnterPlayMode/ExitPlayMode).
- EditMode adds `RunFailsDeterministicallyOutsidePlayModeWithoutCreatingLocalPhysicsScene`.
- `BugCamTestAutomation`: `Library/BugCamTestResults.xml` renamed in docs/code to **LatestResultPath** semantics (most recent suite only); suite files remain `*.EditMode.xml` / `*.PlayMode.xml`; stale pending marker >30 min cleared on load; recovery request + Execute try/catch keep the Editor from sticking.

**ProjectSettings snapshot (VERIFIED FACT from files on disk):**
- Unity: `6000.3.21f1` (`ProjectSettings/ProjectVersion.txt`)
- `m_SimulationMode: 2` (Script)
- `m_ThreadingMode: 0` (MultiThreaded)
- Solver iterations: 6 / velocity 1; gravity `(0, -9.81, 0)`; Auto Sync Transforms off
- Fixed timestep from TimeManager: `2822399 / 141120000` ≈ `0.02` s

**Prior local XML (REPORTED FACT — pre-fix timestamps, superseded):**
- `Library/BugCamTestResults.EditMode.xml` — 7/7 Passed @ 2026-08-01 23:04:18Z
- `Library/BugCamTestResults.PlayMode.xml` — 7/7 Passed @ 2026-08-01 23:07:08Z
- `Library/BugCamTestResults.xml` SHA matched PlayMode only (CONTRADICTION with any “combined” naming)

**Fresh test evidence (VERIFIED FACT — batchmode Unity `6000.3.21f1`, two Editor process restarts):**

| Run | Suite | total | passed | failed | skipped | result | XML | log |
|---|---|---|---|---|---|---|---|---|
| 1 | EditMode | 8 | 8 | 0 | 0 | Passed | `Library/BugCamEvidence/EditMode.run1.xml` | `Library/BugCamEvidence/EditMode.run1.log` |
| 1 | PlayMode | 7 | 7 | 0 | 0 | Passed | `Library/BugCamEvidence/PlayMode.run1.xml` | `Library/BugCamEvidence/PlayMode.run1.log` |
| 2 (after full Editor exit) | EditMode | 8 | 8 | 0 | 0 | Passed | `Library/BugCamEvidence/EditMode.run2.xml` | `Library/BugCamEvidence/EditMode.run2.log` |
| 2 (after full Editor exit) | PlayMode | 7 | 7 | 0 | 0 | Passed | `Library/BugCamEvidence/PlayMode.run2.xml` | `Library/BugCamEvidence/PlayMode.run2.log` |

Canonical copies after run 2: `Library/BugCamTestResults.EditMode.xml`, `Library/BugCamTestResults.PlayMode.xml`, `Library/BugCamTestResults.xml` (= latest suite = PlayMode). Counts/outcomes identical across restart. Day 1 checkpoint remains NOT PASSED — unit tests ≠ Block 1.1 tower measurement.

**Updated conclusion:** The EditMode/PlayMode lifecycle mismatch is the root cause of the PhysicsScene test failure class. Architecture is Play Mode for simulation; Edit Mode for contracts. PR #1 on `fix/physics-scene-test` carries the fix. Do not merge products with SceneSight.

**Remaining open questions from that entry:** superseded for Block 1.1 measurement — see 2026-08-02 evidence. Allocation-zero held on this Editor/Mono probe path; broader GC/runtime variants remain untested.

## Decisions log
- 2026-08-02 — Block 1.2 kinematic VERIFY uses Transform-only replay (`SetLocalPositionAndRotation`), not Rigidbody writeback (PhysX set/get added ~1 ULP). `StateRecorder` stores normalized quaternions. Branch `feat/block-1.2-state-recorder` stacks on `feat/tower-probe-checkpoint` (PR #2); do not auto-merge.
- 2026-08-02 — Block 1.1 dual-mode TowerScene probe uses separate batchmode Editor processes that patch `m_ThreadingMode` via `PhysicsSettingsProbe.SetThreadingMode`, then run the filtered PlayMode checkpoint test. Never leave Enhanced Determinism on; restore `m_ThreadingMode` to MultiThreaded (`0`) after the experiment. Evidence stays under gitignored `Library/BugCamEvidence/Block1.1/`.
- 2026-08-01 — Production `SimulationHarness` is Play Mode-only for local Physics3D scene creation. Edit Mode retains contract/reflection tests only. Obsolete “harness in Edit Mode” plan wording corrected. `BugCamTestResults.xml` documented as latest-suite copy, not a merged report.
- 2026-07-30 — Document audit, bookkeeping only, no scope changes. Three fixes applied: (1) the `DivergenceSettings` contract in `PLAN.md` gained the epsilon-search and evidence numbers that Blocks 1.4/1.5/2.1 already specify — the contract's own rule ("no threshold may be referenced in `Core/` or `Evidence/` unless it exists in this contract") was violated by the plan itself in eleven places, which would have blocked the first commit of Block 1.4; values are transcribed from the plan text, none invented, and `BisectionIterations` is flagged as still carrying a 6–8 range instead of a single default. (2) The risk table said "20 runs × 250 steps", stale since the fan was fixed at 15 + baseline = 16. (3) `SPEC.md` §5 listed "active state" and "state mismatch" without the backlog asterisk although the ratified 14-float stride excludes them.
- 2026-07-29 — Build target pinned to **Unity 6.3 LTS, editor `6000.3.20f1` or newer within 6.3**. Not 6.5. Reasons: (1) 6.3 LTS is supported until Dec 2027, while 6.5 is a rolling Supported release that stops receiving fixes once 6.6 ships — `6000.6.0b5` is already in beta, and 6.4 reached EOL in roughly six months; (2) the Unity version is part of the evidence, not an implementation detail — it goes into `environment.json` and into every environment-scoped PASS, so pinning a repeatability tool to a short-lived release is self-contradictory; (3) Asset Store buyers are predominantly on LTS.
- 2026-07-29 — Unity 6.5 will be installed later as a **second** editor for cross-version ghosting experiments (Day 3+), never as the v0.1 build target.
- 2026-07-29 — Unity 6.3 exposes a multi-threaded / single-threaded physics simulation switch. Block 1.1 VERIFY now runs the whole determinism probe in **both** modes and records `maxComponentDelta` for each; the value joins the environment snapshot and the probe prints the mode it ran under. This attacks Risk 1 directly — single-threaded removes solver thread ordering as a variable, and the delta between modes is a measured finding for the README caveat "solver effects exist". No mode is preferred in advance; the measurement decides in Block 1.1, not the plan. The switch's exact name stays unwritten until it is read off the installed editor.
- 2026-07-29 — State stride 13 → **14** floats (`pos.xyz, rot.xyzw, vel.xyz, angVel.xyz, sleeping`). Reason: 13 fields cannot hold `sleeping`, which `PLAN.md` Block 1.2 requires; a parallel `bool[]` was rejected because it creates two structures with different indexing. Contacts / collision-pair IDs (`SPEC.md §5`, marked `*`) stay out of the stride — backlog.
- 2026-07-29 — `docs/SPEC.md` added; precedence recorded in `CLAUDE.md`: SPEC describes the full product, PLAN wins for v0.1, SPEC-beyond-PLAN is backlog. `SPEC.md` was delivered at the repository parent directory and moved to `bugcam/docs/SPEC.md`.
- 2026-07-29 — Determinism gate is **1e-6 per component**; bitwise equality is measured and logged but is not the gate. `PLAN.md` previously demanded bitwise first.
- 2026-07-29 — Per-run PhysicsScene recreation is the **default**, not a fallback (was contradictory between `core-physics.md` and `PLAN.md`).
- 2026-07-29 — Core physics rules **inlined into `CLAUDE.md`** as an authoritative section; `.claude/rules/core-physics.md` kept as a copy. Which mechanism the runtime honours: hierarchical `CLAUDE.md` loading is the only one confirmed from inside the session — the session-start context listed the global `~/.claude/CLAUDE.md` and no rules file, and while the session ran from the parent directory the project `CLAUDE.md` was not loaded either. Auto-loading of `.claude/rules/*.md` could not be confirmed, and the frontmatter key differed across projects (`paths:` here, `alwaysApply:`/`globs:` in another). Inlining removes the dependency on that question.
- 2026-07-29 — Git root = Unity project root = `bugcam/`. All future sessions start with cwd = `X:\AI CAM\nimbalyst-local\bugcam`. Before this, project `.claude/settings.json` was not in effect because the session ran one directory above.
- 2026-07-29 — Fan size fixed at exactly 15 runs (5 multipliers × 3 axes) + baseline = 16, replacing "10–20 runs".
- 2026-07-29 — Block 1.4 VERIFY split: identical-config repeat must be bit-identical (a determinism regression test), and three searches from different start brackets must agree within ±1 bisection step. As originally written the check was satisfied trivially by determinism and tested nothing about convergence. A 12-point log-uniform epsilon ladder runs first to expose non-monotonicity as data.
- 2026-07-29 — Architecture tree gained `Tests/`, and a fourth test-only asmdef `BugCam.Tests` is allowed alongside the three production asmdefs.
- 2026-07-29 — `PLAN.md` Block 2.1 amended: evidence cameras are a deterministic scored post-process (Fibonacci-sphere candidates, fractional 9-ray occlusion, index tie-break, top-25% filter then optimize), replacing "raycast visibility check". Carries a requirement into Block 1.3: `DivergenceSettings` needs `MinEvidenceCoverageScore` alongside `PerBodyPositionThreshold`, so the `EVIDENCE COVERAGE: LOW` verdict has a number behind it.
- 2026-07-29 — Deferred deliberately: headless test csproj → Block 1.3; `BugCam.Evidence` asmdef → Block 1.5/2.1 (an empty assembly only produces warnings today); `DivergenceSettings` `.asset` authoring → Block 1.3 via `[CreateAssetMenu]` + code defaults; an Editor-side PhysicsScene provider abstraction → contingency, only if the Edit Mode path fails.
