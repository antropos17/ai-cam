# BugCam — CLAUDE.md

Chaos testing & repeatability diagnostics for Unity scenes.
One sentence: perturb initial state → rerun → measure divergence → prove it with a retroactive replay.

## Source of truth
- Product spec: `docs/SPEC.md` (full product). Where SPEC and PLAN differ, `PLAN.md` wins for v0.1; SPEC items beyond PLAN are backlog. Do not re-litigate scope in either direction.
- Execution plan: `docs/PLAN.md` (2-day plan, work strictly in order of its blocks)
- Current status: `docs/STATUS.md` (update after every completed block — this file, not chat, carries state between sessions)

## Non-negotiable scope (v0.1)
- Unity 6, built-in 3D physics only: Rigidbody + Collider, local `PhysicsScene`, fixed step `Simulate(0.02f)`, Simulation Mode = Script.
- NOT in v0.1: DOTS, 2D physics, Cloth, Cinemachine, MCP, AI features, CI integration, attribution, browser viewer, cloud. If a task seems to need one of these — stop and ask, do not add.
- Deterministic core: no LLM calls, no randomness without an explicit recorded seed, no wall-clock dependence.

## Definition of done (v0.1) — the only 8 criteria
baseline capture → measurable perturbation → run series → first sustained divergence → ghost trajectories → threshold + spread numbers → MP4 + evidence card export → repeatable on a fresh pure-physics scene with zero manual editing.

## Architecture (fixed — do not restructure)
```
Assets/BugCam/
├── Core/       SimulationHarness, StateRecorder, DivergenceEngine, EpsilonSearch
├── Evidence/   GhostRenderer, RetroPlayer, EvidenceCameras, Exporter
├── Editor/     BugCamWindow
└── Tests/      TowerScene.unity (demo = test scene), EditMode + PlayMode tests
```
- Core must have zero dependencies on Evidence/ and Editor/. Core contract/reflection tests are EditMode; local `PhysicsScene` simulation requires Play Mode.
- Production `SimulationHarness.Run` requires Play Mode because isolation uses `SceneManager.CreateScene` with `LocalPhysicsMode.Physics3D`. The old plan wording “harness in Edit Mode” is obsolete.
- State storage: flat preallocated float arrays `[runs × steps × bodies × 14]`. Field order per body: `pos.x pos.y pos.z rot.x rot.y rot.z rot.w vel.x vel.y vel.z angVel.x angVel.y angVel.z sleeping(0f|1f)`. No per-frame allocations inside the simulate loop.
- Contacts / collision-pair IDs / custom state (`SPEC.md §5`, marked `*`) are backlog — they are NOT part of the stride.
- All lengths inside Core are metres. Millimetres exist only at the display layer.

## Engineering rules
- Every physics claim gets a verification harness before UI work: two identical runs must match within 1e-6 per component before any feature building on top. The gate is 1e-6; bitwise equality is measured and logged separately, never used as the gate — the sole exception is `PLAN.md` Block 1.4 VERIFY (a), where a bit-identical identical-config repeat serves as a determinism regression test, not as a physics-claim gate (reconciled 2026-07-29).
- Divergence is "significant" only if score > threshold AND sustained ≥ 5 consecutive physics steps AND ≥ 1 tracked body exceeds `DivergenceSettings.PerBodyPositionThreshold` (a number, not a judgement). Never report one-frame noise.
- If identical runs do NOT match: recreate the entire PhysicsScene per run with identical instantiation order. If still divergent, record it as a finding (solver/order sensitivity), don't hide it.
- Honest output is a feature: "STABLE WITHIN TESTED RANGE" is a valid, complete result. Never fake a fan.
- Editor code guards: all Editor-only code under `#if UNITY_EDITOR` or in Editor/ asmdef.
- Three production asmdefs: `BugCam.Core`, `BugCam.Evidence`, `BugCam.Editor`, plus test-only `BugCam.Tests` (EditMode) and `BugCam.Tests.PlayMode` (local PhysicsScene simulation). Keep compile times low.

## Core physics rules (authoritative; also kept as a copy in `.claude/rules/core-physics.md`)

Applies to every edit under `Assets/BugCam/Core/**`.

- `Physics.simulationMode` must be Script before any `Simulate()` call; assert it in harness init.
- Fixed step is a single constant `BugCamConstants.FixedStep = 0.02f`. Never pass `Time.deltaTime`.
- One run = fresh local PhysicsScene created via `SceneManager.CreateScene` with `LocalPhysicsMode.Physics3D` in Play Mode. This is the default, not a fallback. Instantiate bodies in deterministic sorted order (by stable ID, not hierarchy order). Outside Play Mode the harness returns a deterministic failure result — it does not attempt CreateScene.
- No `Update`/`FixedUpdate` reliance inside harness runs — the loop drives everything explicitly.
- No allocations inside the step loop: no LINQ, no closures, no string concat, no new arrays. Preallocate in Init.
- All thresholds/weights live in one serializable `DivergenceSettings` asset — no magic numbers in engine code.
- Perturbations are recorded exactly as applied (axis, magnitude, target body ID) in RunResult metadata before the run starts.
- Every public Core method that can fail returns a `readonly struct` result carrying an error reason; no silent catches, no Debug.Log-and-continue in Core. This rule applies to Init/Run-level entry points — per-step methods inside the loop must not allocate, so they report through preallocated state.

## Language & claims policy (marketing artifacts, README, overlays)
- Never claim: "first ever", "proves the bug", "makes PhysX deterministic", "all writers".
- Always frame: sensitivity ≠ bug; repeatability is environment-scoped; category is "chaos testing for game scenes".
- Video overlays: numbers, not adjectives. Format: `INPUT 0.27 MM → SPREAD 1.74 M → 6,444×`.
- The word "AI" appears zero times in the hero video and product UI.

## Workflow
- Work block-by-block from `docs/PLAN.md`. One block = one commit. Commit message: `block-X.Y: <what>`.
- Checkpoint gates are hard: Day 1 checkpoint (console numbers + visible fan in Scene View) must pass before any Day 2 work.
- When stuck > 30 min on a Unity API issue: write the smallest repro script, log findings to `docs/STATUS.md`, move to the next independent task within the same block.
- Do not polish UI, naming, or visuals until DivergenceEngine passes its verification harness.
- Unity runs are executed by the human in the Editor unless batchmode CLI is set up; generate code + exact manual test steps, don't assume you can run Play Mode yourself.

## Testing
- Editor / reflection / contract tests: `BugCam.Tests` (EditMode). Includes the deterministic Play Mode requirement failure for `SimulationHarness`.
- Simulation and local `PhysicsScene` correctness: `BugCam.Tests.PlayMode` — fresh local Physics3D scenes, falling Rigidbody, 14-float state, stable IDs, perturbation, A/B/A-prime, cleanup. Do not move these back into EditMode; do not use EnterPlayMode/ExitPlayMode from the PlayMode assembly.
- Tower demo scene: `Assets/BugCam/Tests/TowerScene.unity`, asserted via harness, not eyeballed.
- Never mark a block done without its verification step from PLAN.md executed and result noted in STATUS.md.
- `*.csproj` stays gitignored deliberately: no csproj is tracked and none exists in the worktree — the Block 1.3 "headless test csproj" was never created (headless runs go through `Tools/BugCam/run-checkpoint.ps1`); if a hand-authored csproj ever becomes necessary, generate it from a committed script instead of force-adding it (decision 2026-08-03, Block 2.2.1).
- Day 1 checkpoint remains NOT PASSED until Block 1.1 tower determinism measurements exist — passing unit tests alone is not that checkpoint.
