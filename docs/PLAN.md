# BugCam — Execution Plan (2 days + buffer)

Goal: on a pure-physics Unity scene — find the smallest perturbation that changes the outcome → ghost-trajectory fan → retro replay from 4 angles → export MP4 + evidence card. Plus hero video and landing page.

Everything else in SPEC.md (CI, attribution, capsule viewer, subscription) is backlog. Do not touch it these two days.

---

## DAY 1 — Core & proof

### Block 1.1 — Scene & harness
- Unity 6.3 LTS URP project `BugCam`, editor `6000.3.20f1` or newer within 6.3 — never 6.5 (rationale in `STATUS.md` Decisions log). Demo scene = test scene = `Assets/BugCam/Tests/TowerScene.unity`: tower of 40–60 Rigidbody cubes + one projectile/impulse, generated procedurally by an Editor menu item (no hand-authored `.unity`). Tune for sensitivity (tall, narrow base, low friction).
- `SimulationHarness`: Simulation Mode = Script; local `PhysicsScene`; run = N × `Simulate(0.02f)` — note the effective step is float(0.02) = `0.0199999921` s, bit-identical to the editor's serialized fixed timestep `2822399/141120000` (measured live 2026-08-03); "0.02 s" in prose always means this float value, not an exact decimal. Clone scene into isolated physics world from saved initial state. PhysicsScene is recreated per run by default.
- Physics simulation threading: Unity 6.3 exposes a multi-threaded / single-threaded simulation switch. Its exact API and settings name is read off the installed `6000.3.20f1` editor (Project Settings → Physics, cross-checked against that editor's Scripting API reference) — never guessed from memory. Its value joins the physics settings snapshot, and `DeterminismProbe` prints the mode it ran under. No mode is preferred in advance: the Block 1.1 measurement decides, not this plan.
- VERIFY: two identical runs match within 1e-6 per component (the gate). Report numbers, not pass/fail: `bitwiseEqual`, `maxComponentDelta`, `firstDivergingStep`, `firstDivergingBody`, `managedBytesAllocatedInLoop`, `simulationThreadingMode`. Run order A, B, A′ (B = perturbed) so cross-run state leakage is visible. Repeat once after an Editor restart to separate in-session from cross-session determinism. **Run the whole probe in BOTH threading modes and record `maxComponentDelta` for each** — single-threaded removes solver thread ordering as a variable, and the delta between the two modes is a measured finding for `STATUS.md` and for the README caveat "solver effects exist". If the gate fails: fix instantiation order, then record it as a finding — do not hide it.

### Block 1.2 — State recording
- `StateRecorder`: per physics step per body — position, rotation, linear/angular velocity, sleeping. Flat preallocated arrays `[runs × steps × bodies × 14]`, field order `pos.xyz, rot.xyzw, vel.xyz, angVel.xyz, sleeping(0f|1f)`.
- `RunResult`: frames + metadata — `epsilon` (metres), `perturbation` (axis / magnitude / bodyId), `stepCount`, `simulatedTime = steps × FixedStep`, `seed = 0` (reserved; non-zero only for a randomized perturbation mode). Wall-clock, if recorded at all, lives in a separate `wallClockMs` field excluded from every comparison and hash.
- VERIFY: kinematic replay reproduces the recorded transforms frame for frame — `maxComponentDelta == 0`, asserted in the harness. Visual inspection is an optional extra, never the verification.

### Block 1.3 — Divergence Engine
- Per-body per-frame: posError/objectScale, rotation angle error, velocity error, sleep mismatch. Scene Divergence Score = MAX per-body weighted norm (re-ratified 2026-08-03, Block 2.2.1 A3, from the original weighted sum — the sum was measured degenerate on the 49-body tower; `SceneScoreThreshold` re-ratified to 0.2 over measured distributions, see `docs/CONTRACT-2.2.1.md` and the A3 Evidence log).
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
| `MinEvidenceCoverageScore` = 0.25 | 0..1 | Block 2.1 honest-verdict gate over camera 1's **occlusion coverage** (visible sample-point fraction averaged over affected bodies) — distinct from the ranking score. **PROVISIONAL threshold** (2026-08-03): the calibration measured only a tight "good" cluster (0.354–0.376) and one zero (0.000); the intermediate 0.1–0.3 region ("barely visible") was never measured, so 0.25 separates the observed extremes but is not a calibrated boundary — refine on a partial-occlusion scene before treating LOW/OK near 0.25 as meaningful |

Epsilon search (Block 1.4) — same asset, per `CLAUDE.md` "all thresholds live in one `DivergenceSettings`":

| Field | Unit | Role |
|---|---|---|
| `EpsilonStart` = 1e-5 | metres | first magnitude of the exponential search (0.01 mm) |
| `EpsilonGrowthFactor` = 2 | — | multiplier per exponential step |
| `EpsilonCeiling` = 1e-2 | metres | upper bound of the tested range (10 mm); above it the verdict is `STABLE WITHIN TESTED RANGE` |
| `BisectionIterations` = 7 | steps | binary-search depth; Block 1.4 fixed the prior 6–8 range to this single default |
| `LadderPointCount` = 12 | points | log-uniform monotonicity ladder run before bisection is trusted |
| `FanMultipliers` = {0.8, 0.9, 1.0, 1.1, 1.2} | × threshold | fan spread; 5 multipliers × 3 axes = 15 runs + baseline |

Evidence (Blocks 1.5, 2.1) — same asset:

| Field | Unit | Role |
|---|---|---|
| `GhostBodyLimit` = 10 | bodies | top-N diverging bodies drawn as ghosts, plus baseline |
| `EvidenceCandidateCount` | candidates | Fibonacci-sphere N; recorded in `camera-plan.json` |
| `EvidenceOcclusionRays` = 9 | rays | AABB centre + 8 corners; fractional occlusion = hits / rays |
| `EvidenceTopScoreFraction` = 0.25 | — | survivor filter applied before optimizing cameras 2–4 |
| `WeightEvidenceCentrality` = 0.25 | — | frame-edge penalty in candidate scoring |
| `EvidenceCameraVerticalFovDegrees` = 50 | degrees | vertical FOV of each candidate's virtual camera |
| `EvidenceCameraNearClip` = 0.05 | metres | near clip plane for candidate frustum construction |
| `EvidenceCameraFarClip` = 500 | metres | far clip plane for candidate frustum construction |
| `EvidenceRenderWidth` / `EvidenceRenderHeight` = 1920×1080 | pixels | canonical resolution for pixel-space scoring only (matches Block 2.3 landscape export) |
| `EvidenceEventBoundsRadiusMultiplier` = 2.5 | × bounds extents | candidate sphere distance from the divergence-event bounds center |
| ~~`WeightCameraOrthogonality` / `WeightContactProximity` / `WeightTrajectoryAlignment`~~, ~~`ScreenSpaceSeparationNormalizer`~~ | — | REMOVED 2026-08-03 (live calibration): contact proximity was constant across the single-radius candidate sphere, trajectory alignment never influenced a winner, screen-space separation was 4–5 orders below the other terms at the first-divergence frame. Cameras 2–4 rank by orthogonality alone; a single ranking term carries no weight field. |

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
- VERIFY, two separate checks: (a) a repeat with identical configuration must be bit-identical — this is the determinism regression test, not a convergence test (**OPEN/partial in Block 1.4:** PlayMode currently asserts repeated **baseline-only** within ≤1e-6; bit-identical identical-config **search** repeat remains future work — do not reopen Core for it in this block); (b) **algorithm convergence** compares different starting strategies (`AscendFromStart` 0.01 mm ×2; `AscendFromCustomStart` 0.02 mm ×2; `DescendFromCeiling`) on the **same axis, same scene, same target body, and same configuration** — **proven in Block 1.4:** they land within ±1 **growth** step of each other (`EpsilonGrowthFactor=2` ⇒ Ratio≤2) at measured `VerifyStepCount=32`. Agreement within ±1 **bisection** step is **future/OPEN** (not claimed). X/Y/Z searches are directional characterization; their physical thresholds are reported but are **not** required to match.

### Block 1.5 — Ghost visualization
- Assembly `BugCam.Evidence` (refs `BugCam.Core` only; no UnityEditor). Editor refs Evidence.
- `GhostEvidenceBuilder` is the single source of truth: re-Analyze baseline vs each retained fan; STABLE fabricates no fans; primary fan = 1.0× search-axis (tie-break index asc). Fail-closed: both `FanSummary.Axis` and `Run.Perturbation.Axis` must match expected fan axis; fan ε ≈ `ReferenceEpsilon×multiplier` within `FanEpsilonRelativeTolerance`; `OutsideSearchRange` must agree with ε > search ceiling.
- Ranking: `PerBodyMaxPositionErrorMetres` desc, `bodyId` asc; `GhostBodyLimit=10`; omit zero-error bodies.
- Core: `DivergenceResult.FirstDivergenceBodyId` = argmax |Δpos| at `FirstDivergenceFrame` (bodyIndex asc tie-break). Independent of `MaxSpreadBodyId`.
- Pure-data `GhostDrawSet` / `GhostRenderer`; first-div marker samples `FirstDivergenceBodyId` @ first-div frame; max-spread samples `MaxSpreadBodyId` @ max-spread step. Scene View via `SceneView.duringSceneGui` + `Handles.DrawAAPolyLine`. No permanent GameObjects; no scene dirty.
- Single search pipeline: Window and menu both route through `GhostEvidencePlayModeHost` MonoBehaviour (Unity nested coroutines). Shared busy lock prevents concurrent Window+Host searches. Do not use outer-only `EditorApplication.update` wrappers for nested `IEnumerator` search.
- Evidence bundle under `Library/BugCamEvidence/Runs/<run-id>/`:
  `manifest.json`, `metrics.json`, `summary.md`,
  `report/console-report.txt` (not `report.txt` — keeps console text distinct),
  `runs/baseline.json` + `runs/fan-00.json`…`fan-14.json` (STABLE → baseline only; failure → no fabricated run JSON),
  `visuals/overview.png`, `first-sustained-divergence.png`, `maximum-spread.png`, `final-state.png`
  plus `Library/BugCamEvidence/Block1.5` checkpoint pointer. Schema `BugCam.GhostEvidence` v1.
- Honesty: when `!document.Success` / unavailable primary, JSON uses `null` + `has*` flags (not fabricated 0). Core in-memory ints may still use `-1` sentinels; machine-facing JSON prefers null. Panel shows `unavailable` for failure/STABLE primary metrics.
- Editor window menu `BugCam/Ghost Visualization`. Success path logs honest search report + `GhostEvidenceReport`.
- Day 2 (RetroPlayer / MP4 / evidence cameras / cockpit / SceneSight) not started.

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

**Scoring** — per candidate, summed over affected bodies (amended 2026-08-03 after live calibration):
- in-frustum: `GeometryUtility.CalculateFrustumPlanes` + `GeometryUtility.TestPlanesAABB`
- occlusion: fractional, 9 raycasts per body (AABB centre + 8 corners), score = hits / 9. Never binary.
- centrality penalty for bodies near the frame edges
- ties broken strictly by candidate index, never by float comparison
- ~~screen-space separation in pixels~~ — REMOVED: at the first-divergence frame the physical offset is ~1 mm, sub-pixel at 1920 from candidate distance; the measured term (0.00036–0.00074 total over 21 bodies) sat 4–5 orders below the other terms and never influenced selection. A separation term scored at the max-spread frame is a candidate for the Block 2.3 RetroPlayer work, not part of v0.1 scoring.

**Winners** (amended 2026-08-03 after live calibration).
- Camera 1 = highest score.
- Cameras 2–4: filter to the top 25% of surviving (above-ground) candidates by score FIRST, then pick the most orthogonal to camera 1 among the survivors (orthogonality descending, candidate index ascending on ties). The original criteria (b) contact proximity and (c) trajectory alignment were measured dead on live data — contact is constant by construction while all candidates share one sphere radius, and trajectory never outweighed observed orthogonality gaps (winners identical with its weight at 1 vs 0) — and were removed rather than left as decorative weights. Close-up/contact cameras require contact data (backlog per `CLAUDE.md`) plus multi-radius candidate generation — SPEC §7 backlog, not v0.1.

**Honest verdict (required).** If camera 1's **occlusion coverage** — the fraction of unoccluded sample points averaged over affected bodies (`OcclusionCoveragePerBody`, 0..1) — falls below `DivergenceSettings.MinEvidenceCoverageScore`, output `EVIDENCE COVERAGE: LOW` with the coverage value, the count of affected bodies visible, and the reason. The gate deliberately ignores the ranking score: the in-frustum term alone can clear any total-score threshold while every affected body is fully occluded (measured live 2026-08-03). This is a valid result in the same sense as `STABLE WITHIN TESTED RANGE` — do not emit four poor cameras instead.

**Manifest** (`camera-plan.json`, written when the capsule lands on Day 3): algorithm version, candidate count N, and the score of EVERY candidate including the rejected ones — provenance requires the losers. Per chosen camera: bodies in frame, distances, fractional occlusion values, final score.

**Constraints.** No Cinemachine, no MCP, no `com.unity.perception` (abandoned since Nov 2024). Plain `UnityEngine.Camera` plus the three APIs named above. Nothing lands in `Core/` — this lives in `Evidence/EvidenceCameras.cs`.

- VERIFY: selection is reproducible bit-for-bit by a third party from the same recorded runs — re-running selection over identical recorded trajectories yields identical candidate scores, identical winner indices, and an identical `camera-plan.json`.

**Reproducibility means no live scene.** "Reproducible... by a third party from the same recorded runs" means the algorithm is a pure post-process over `RunResult` + `DivergenceResult` + per-body extents — occlusion uses ray-vs-AABB math over recorded positions, never `Physics.Raycast` against live colliders, and the frustum test builds its view-projection matrix directly (`Matrix4x4`) rather than from a scene `Camera` GameObject. Bodies are treated as world-axis-aligned boxes at each queried frame (position ± half-extent from `SimulationBodyDefinition.Size`), not rotation-aware OBBs — a documented simplification. (Historical note: "contact proximity" was first approximated as distance-from-event-bounds-center; the 2026-08-03 calibration showed that distance is constant across the single-radius candidate sphere, so the criterion was removed rather than kept as a dead term — see **Winners** above.)

**Block 2.1 landing scope.** The first commit lands `EvidenceCameras.cs` (candidate generation, scoring, honest verdict) + `camera-plan.json` schema/writer + EditMode VERIFY — everything checkable in batchmode without a live Editor. `RetroPlayer` (scrub/slow-mo playback) and the actual 2×2 viewport/RenderTexture compositing are deferred to Block 2.3 — Evidence overlay + export (needs a live GPU Editor session to verify, the same standard Block 1.5's screenshot capture was held to after the `#1F1F24` blank-PNG correction). Do not claim the deferred pieces as done.

### Block 2.2 — Ghost Visualization window UX (user-inserted, merged as PR #8, main = 168c022)
- Inserted 2026-08-03, ratified via two-round ASCII mockup review; search functionality untouched. Window state machine IDLE → READY → SEARCHING → DONE(verdict verbatim | neutral INTERRUPTED) as the single UI source of truth; disabled controls always render explicit reasons; numbers availability-gated (auto m/mm/µm, 3 significant digits, integer amplification); live progress from real probe steps; host exits only the Play Mode session it itself started.
- DONE: final gate EditMode 107/107 + PlayMode 21/21 (`Library/BugCamEvidence/Block2.2-final-gate`); window-state screenshots `Library/BugCamEvidence/Block2.2-ui/`.

### Block 2.2.1 — Polish pass (user-inserted, branch `feat/block-2.2.1`)
- Inserted 2026-08-03; goal axes: any-scene operation, convenience, accuracy, self-verifiability. Composition and the A1/A2/A3 contract ratified by the human 2026-08-03 (audit + contract review in-session; evidence dirs `Library/BugCamEvidence/Block2.2.1-*`). **Full ratified contract text: `docs/CONTRACT-2.2.1.md`** (this section is the summary; contradiction between the two = STOP and adjudicate).
- Order: **A6+A7** docs pass (epsilon phase comments, bitwise clause, SPEC §5 share-of-scene annotation, csproj-ignore decision) → **A4** Scene View legend/label positioning → **A1** search-entry parameterization (`DivergenceSettings` asset input + editable ε range + target/axis/steps; fail-closed validation with explicit reasons, no silent clamp; source precedence window-override > asset > defaults, recorded in manifest) → **A3** sceneScore normalization: per-step score changes from sum over bodies to **max** per-body weighted norm; `SceneScoreThreshold` re-ratified from tower noise/divergence measurement; any drift in the pinned tower numbers = STOP and adjudicate; removing the score half entirely (if measurement shows it never decides) is NOT automatic — separate adjudication over the measured data → **A2 (sub-stage)** arbitrary-scene capture: Box+Sphere primitive colliders (Sphere `Size` = diameter), kinematic bodies frozen to static (Animator ⇒ warning propagated to window AND result verdict AND manifest/evidence — the evidence consumer must see the caveat without the window), contactless bodies excluded (physically safe), unsupported contact-capable shapes/joints/shear ⇒ fail-closed capture with per-object reasons, deterministic stable IDs (hierarchy path + sibling index; id↔name map and capture hash in manifest), capture report in window + `sceneCapture` manifest section → **A5** bit-identical identical-config search-repeat pin (closes Block 1.4 VERIFY (a)) → **A8** exit gate: untouched core + new entry on a second procedural scene (dominoes).
- Bucket B deferred with reasons (ratified 2026-08-03): `MinEvidenceCoverageScore` calibration, EvidenceCameras ground-plane parameter, window coverage row (all tied to Block 2.3 wiring), rotation-aware OBB, contact-set instrumentation.

### Block 2.2.2 — Capture universality (user-inserted, branch `feat/block-2.2.2`)
- Inserted 2026-08-03, contract ratified by the human 2026-08-04 (open questions №1–№10 adjudicated). Exactly **two capture extensions and nothing else**: (1) static MeshCollider (convex and non-convex alike) → CapturedStatic; (2) dynamic Rigidbody + convex MeshCollider → CapturedDynamic; everything else keeps its A2 fail-closed outcome. Meshes captured **by asset reference** (assetGuid + localFileId + geometry contentHash: SHA-256 over vertices + triangles of ALL submeshes, `"R"` invariant floats) through an injectable resolve provider — interface in Core, implementation in `BugCam.Editor`, no `#if UNITY_EDITOR` in Core, missing provider = fail-closed. New simulation-point error code `SCENE_MESH_RESOLVE_FAILED` (capture point keeps `SCENE_CAPTURE_FAILED`). Capture hash extended additively: Box/Sphere lines stay byte-identical, mesh shapes get a `|mesh:…` tail; manifest `objects[]` records gain `meshRef` (SchemaVersion stays 1). objectScale for mesh bodies = max component of the world AABB (`mesh.bounds × |lossyScale|`), with a mandatory Box/Sphere objectScale bit-identity pin. Negative scale, non-default cookingOptions, unreadable geometry (Read/Write off), unresolvable mesh refs = fail-closed with ratified verbatim reasons. Gameplay scripts do not execute in the local PhysicsScene (SPEC §13 boundary, declared in the contract). Exit gate: one real downloaded scene (human-approved choice) captured + searched end-to-end, tower and domino pins bit-identical (incl. domino capture hash `40e46640…b5b6531`), green batchmode with grown counters. **Full ratified contract text: `docs/CONTRACT-2.2.2.md`** (this section is the summary; contradiction between the two = STOP and adjudicate).

### Block 2.2.3 — Play Mode snapshot capture (user-inserted, contract not written)
- Decided 2026-08-04 in the planning session as an explicit insert between 2.2.2 and 2.3; written into PLAN 2026-08-04 after the PR #11 merge.
- IN: the source of the initial state becomes a snapshot of a live Play Mode session instead of an edit-mode scene capture. The user runs the game, reaches an interesting moment, presses capture, and BugCam takes the current physics state (position, rotation, linear velocity, angular velocity, sleeping — already covered by the existing stride-14 recorder) as the initial state for the divergence search in the fast PhysicsScene. Gameplay scripts are not replayed; their result becomes the starting state. Existing determinism guarantees, pins and fail-closed behaviour are unchanged.
- OUT: full Play Mode replay with scripts re-executed stays backlog, marked "after first sale". No new physics features, no DOTS, no 2D.
- OPEN QUESTION FOR THE CONTRACT (recorded here, not resolved): interaction with CONTRACT-2.2.2 invariant P.6 — the full mesh contentHash is computed only at the edit-mode capture point before Play Mode entry, so a Play Mode snapshot path must define where mesh resolution happens and what the simulation-time structural fingerprint is compared against. To be adjudicated in `docs/CONTRACT-2.2.3.md` before any code, per the 2.2.1 and 2.2.2 precedent.

### Block 2.3 — Evidence overlay + export
- UI Canvas overlay: test numbers, frame counter, timeline, logo.
- Unity Recorder: MP4 1080×1920 and 1920×1080. Evidence card: PNG 1200×630 via RenderTexture → EncodeToPNG.
- `RetroPlayer` (kinematic scrub/slow-mo playback) + the actual 2×2 viewport/RenderTexture compositing (deferred from Block 2.1, needs live GPU Editor session).

### Block 2.4 — EditorWindow
- Single `BugCam` window: target root, duration, epsilon range, `Run Butterfly Test` / `Export` buttons, progress bar, results text, `Focus divergence` button. Minimal styling.

### Block 2.5 — Hero video shoot (human task, agent prepares assets)
- Fixed 22s script (see SPEC.md §17). Agent outputs: shot list, exact overlay text files, camera-plan, raw footage export presets. Human edits in CapCut. Zero occurrences of "AI".

### Block 2.6 — Landing + waitlist
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
