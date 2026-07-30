# BugCam — CLAUDE.md

Chaos testing & repeatability diagnostics for Unity scenes.
One sentence: perturb initial state → rerun → measure divergence → prove it with a retroactive replay.

## Source of truth
- Product spec: `docs/SPEC.md` (final, do not re-litigate scope)
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
└── Editor/     BugCamWindow
```
- Core must have zero dependencies on Evidence/ and Editor/. Core is testable headless.
- State storage: flat preallocated float arrays `[runs × steps × bodies × 13]`. No per-frame allocations inside the simulate loop.

## Engineering rules
- Every physics claim gets a verification harness before UI work: two identical runs must match within 1e-6 per component before any feature building on top.
- Divergence is "significant" only if score > threshold AND sustained ≥ 5 consecutive physics steps AND affects ≥ 1 tracked body meaningfully. Never report one-frame noise.
- If identical runs do NOT match: recreate the entire PhysicsScene per run with identical instantiation order. If still divergent, record it as a finding (solver/order sensitivity), don't hide it.
- Honest output is a feature: "STABLE WITHIN TESTED RANGE" is a valid, complete result. Never fake a fan.
- Editor code guards: all Editor-only code under `#if UNITY_EDITOR` or in Editor/ asmdef.
- Three asmdefs: `BugCam.Core`, `BugCam.Evidence`, `BugCam.Editor`. Keep compile times low.

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
- Core logic (DivergenceEngine math, EpsilonSearch bisection): plain C# unit tests, EditMode, no scene needed.
- Simulation correctness: dedicated test scene `Assets/BugCam/Tests/TowerScene.unity`, asserted via harness, not eyeballed.
- Never mark a block done without its verification step from PLAN.md executed and result noted in STATUS.md.
