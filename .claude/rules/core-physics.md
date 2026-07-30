---
paths:
  - "Assets/BugCam/Core/**"
---

# Core physics rules (loaded only for Core/ edits)

- Physics.simulationMode must be Script before any `Simulate()` call; assert it in harness init.
- Fixed step is a single constant `BugCamConstants.FixedStep = 0.02f`. Never pass Time.deltaTime.
- One run = fresh local PhysicsScene created via `SceneManager.CreateScene` with `LocalPhysicsMode.Physics3D` — default, not a fallback; instantiate bodies in deterministic sorted order (by stable ID, not hierarchy order).
- No `Update`/`FixedUpdate` reliance inside harness runs — the loop drives everything explicitly.
- No allocations inside the step loop: no LINQ, no closures, no string concat, no new arrays. Preallocate in Init.
- All thresholds/weights live in one serializable `DivergenceSettings` asset — no magic numbers in engine code.
- Perturbations are recorded exactly as applied (axis, magnitude, target body ID) in RunResult metadata before the run starts.
- Every public Core method that can fail returns a `readonly struct` result carrying an error reason; no silent catches, no Debug.Log-and-continue in Core. Scope: Init/Run-level entry points — per-step methods inside the loop must not allocate, so they report through preallocated state.

> This file is a copy. The authoritative version of these rules is the "Core physics rules" section in `CLAUDE.md` — edit both together.
