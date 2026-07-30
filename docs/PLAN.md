# BugCam — Execution Plan (2 days + buffer)

Goal: on a pure-physics Unity scene — find the smallest perturbation that changes the outcome → ghost-trajectory fan → retro replay from 4 angles → export MP4 + evidence card. Plus hero video and landing page.

Everything else in SPEC.md (CI, attribution, capsule viewer, subscription) is backlog. Do not touch it these two days.

---

## DAY 1 — Core & proof

### Block 1.1 — Scene & harness
- Unity 6 URP project `BugCam`. Demo scene: tower of 40–60 Rigidbody cubes + one projectile/impulse. Tune for sensitivity (tall, narrow base, low friction).
- `SimulationHarness`: Simulation Mode = Script; local `PhysicsScene`; run = N × `Simulate(0.02f)`; clone scene into isolated physics world from saved initial state.
- VERIFY: two identical runs match bitwise (fallback tolerance 1e-6). If not: recreate PhysicsScene per run, fix instantiation order, retry.

### Block 1.2 — State recording
- `StateRecorder`: per physics step per body — position, rotation, linear/angular velocity, sleeping. Flat preallocated arrays `[runs × steps × bodies × 13]`.
- `RunResult`: frames + metadata (epsilon, seed, duration).
- VERIFY: replaying recorded frames kinematically reproduces the visual motion of the live run.

### Block 1.3 — Divergence Engine
- Per-body per-frame: posError/objectScale, rotation angle error, velocity error, sleep mismatch. Scene Divergence Score = weighted sum.
- Significant = score > threshold sustained ≥ 5 steps.
- Output: firstDivergenceFrame, maxSpread (m), affectedBodies, amplification = maxSpread / epsilon.
- VERIFY: unit tests on synthetic trajectories (known divergence frame must be found exactly; pure noise must yield none).

### Block 1.4 — Adaptive Epsilon Search
- Exponential from 0.01 mm ×2 up to 10 mm ceiling → binary search 6–8 iters → fan: 10–20 runs at {0.8, 0.9, 1.0, 1.1, 1.2} × threshold, perturb X/Y/Z.
- No divergence in range → verdict `STABLE WITHIN TESTED RANGE`.
- VERIFY: on demo scene, search returns a stable threshold across 3 repeated searches (±1 binary step).

### Block 1.5 — Ghost visualization
- Trajectories of all runs: LineRenderer or DrawMeshInstanced ghosts. Baseline white, runs colored by divergence magnitude. Red sphere at first divergence. Show only top-10 diverging bodies + baseline.
- Scene View gizmos are sufficient today.

### DAY 1 CHECKPOINT (hard gate)
Console output with threshold / first divergence frame / spread / amplification / affected count AND a visible fan in Scene View. Do not start Day 2 without it.

---

## DAY 2 — Evidence & packaging

### Block 2.1 — Retro replay
- `RetroPlayer`: kinematic frame-by-frame playback in main scene, time scrub, slow-mo 0.1–0.25×.
- 4 cameras around first-divergence point: overview, contact close-up, opposite angle, along-trajectory. Positions from affected-body bounds + raycast visibility check. 2×2 split via viewport rects or RenderTextures.

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
| Runs don't match bitwise | Recreate PhysicsScene per run, fixed instantiation order; compare at 1e-6 |
| Scene too stable, no fan | Taller tower, narrower base, less friction; perturb projectile instead of brick |
| 20 runs × 250 steps slow | Simulate without rendering (milliseconds); render only replay |
| Recorder won't record in Edit Mode | Replay in Play Mode; harness in Edit Mode |
| 800-line ghost spaghetti | Top-10 bodies only + baseline |
