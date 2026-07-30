# BugCam — STATUS

> Updated by the agent after every completed block. This file is the memory between sessions.

## Current position
- Active block: 1.1 (waiting for Unity environment values from the human — manual steps 1–7)
- Day 1 checkpoint: NOT PASSED
- Day 2 checkpoint: NOT PASSED

## Completed blocks
| Block | Result | Verification | Commit |
|---|---|---|---|
| docs | Spec contradictions resolved (11 items), git repo initialised | Review approved by human 2026-07-29 | `chore: docs + claude setup`, `docs: fix spec contradictions` |

## Open findings / blockers
- Environment snapshot not yet recorded. Needed before `BugCamConstants` is written: exact Unity 6 version + Physics settings (Simulation Mode, Enhanced Determinism, Default Solver Iterations, Default Solver Velocity Iterations, Default Contact Offset, Sleep Threshold, Bounce Threshold, Default Max Angular Speed, Friction Type, Solver Type, Broadphase Type, Auto Sync Transforms, Reuse Collision Callbacks, Gravity) + Time.fixedTimestep. Repeatability claims are environment-scoped and meaningless without this snapshot.
- OPEN: whether a local `PhysicsScene` can be created and stepped outside Play Mode is unverified. It is answered by the first print of Block 1.1's probe (`physicsScene.IsValid()`), not from documentation. Play Mode is the sanctioned fallback (`PLAN.md` risk table).

## Decisions log
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
