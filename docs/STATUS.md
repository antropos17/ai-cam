# BugCam — STATUS

> Updated by the agent after every completed block. This file is the memory between sessions.

## Current position
- Active block: 2.1 (evidence camera selection on `feat/block-2.1-evidence-cameras`) — deterministic core landed; RetroPlayer + 2×2 compositing deferred to a follow-up (see Evidence log 2026-08-03 below).
- Day 1 checkpoint: PASSED (Scene View fan + success-path honest reports + evidence bundle)
- Day 2 checkpoint: NOT PASSED — `EvidenceCameras.cs` + `camera-plan.json` exist and are batchmode-verified; RetroPlayer, MP4, cockpit, SceneSight remain not started; 2×2 viewport/RenderTexture compositing remains not started.
- PR #6 (Block 1.5, Ghost Visualization + evidence bundles): reviewed and squash-merged. Squash SHA `558d4f1a763808e4eb3e4cbbe675b3d698cb1cf5`. Merge base `1bd10113eeeb8376ae31379b391e8c408d2884a8`. Independent review of the final commit range `3f2c93d6e6997f91ad0f431af6a5fc756ff0eb38..35bdb6f8d0290b2c7a2023061ccbeeaa1cb8647d` found no blocker (interrupt cleanup destroys only the Host-owned TEMP runner, Busy/Pending always clear, cleanup is idempotent, no false `SearchCompleted` success on interrupt, regression test is behavioral). `local main` fast-forwarded to the squash SHA; no post-merge bookkeeping commit was made on `main` (this STATUS update lands on the Block 2.1 branch instead, per the no-`main`-commit rule).
- This session had **zero live Unity verification**: no Unity MCP server was connected and no Unity Editor process was running throughout. All facts below come from Git/GitHub state and batchmode `Tools/BugCam/run-checkpoint.ps1` runs only.

## Completed blocks
| Block | Result | Verification | Commit |
|---|---|---|---|
| docs | Spec contradictions resolved (11 items), git repo initialised | Review approved by human 2026-07-29 | `chore: docs + claude setup`, `docs: fix spec contradictions` |
| 1.1 | TowerScene A/B/A′ determinism probe in both threading modes + Editor restart | See Evidence log 2026-08-02 | squash `91ae29d` (#2) |
| 1.2 | StateRecorder + RunResult + kinematic transform replay VERIFY | See Evidence log 2026-08-02 (Block 1.2) | squash `a90765a` (#3) |
| 1.3 | DivergenceSettings + DivergenceEngine synthetic + RunResult integration + review-fix | See Evidence log 2026-08-02 (Block 1.3 review-fix) | squash `6d676ad` (#4) |
| 1.4 | Adaptive epsilon search (step-driven) + partial PlayMode VERIFY (fail-closed bracket/fan + ±1 growth-step Ratio≤2; VERIFY (a) OPEN) | See Evidence log 2026-08-02 (Block 1.4 verify-fix) | merge SHA `1bd10113eeeb8376ae31379b391e8c408d2884a8` (#5) |
| 1.5 | Ghost visualization + evidence bundle (`BugCam.Evidence` + Scene View drawer + Ghost Visualization window + interrupt-safe Host cleanup) | See Evidence log 2026-08-03 (pr6-interrupt-fix) + 2026-08-03 merge review below | squash `558d4f1a763808e4eb3e4cbbe675b3d698cb1cf5` (#6, merged) |

## Open findings / blockers
- OPEN (Block 2.1 scope): `RetroPlayer` (kinematic scrub/slow-mo playback) and the actual 2×2 viewport/RenderTexture compositing are **not started**. `EvidenceCameras.cs` computes and ranks the 4 camera positions but nothing renders them yet. Needs a live GPU Editor session to verify visually, same standard Block 1.5's screenshot capture was held to after the `#1F1F24` blank-PNG correction — do not claim this done from batchmode alone.
- OPEN (Block 2.1 approximation): "contact proximity" (PLAN criterion b for cameras 2-4) is approximated as inverse distance to the divergence-event bounds center, because collision-pair/contact-point data is explicitly backlog per `CLAUDE.md` (not part of the 14-float stride) and so is unavailable to score against. Revisit if contact tracking is ever added.
- OPEN (Block 2.1 approximation): cameras 2-4 ranking implements PLAN's "(a) orthogonality, (b) contact proximity, (c) trajectory alignment ... in that order" as a single weighted sum (100:10:1) rather than strict lexicographic sorting, to avoid float-equality-driven sort instability. Ties still break by ascending candidate index, never by float equality.
- OPEN (Block 2.1 design): bodies are scored as world-axis-aligned AABBs (recorded position ± half of `SimulationBodyDefinition.Size`) at each queried frame — rotation is not applied to the bounding box. A documented simplification, not a silent one; revisit if a future scene relies on rotated bodies for occlusion accuracy.
- RESOLVED (Block 1.4 design): fan samples may exceed `EpsilonCeiling` up to `1.2 × EpsilonCeiling`. Magnitudes are **not** silently clamped; every fan sample above the search ceiling is marked `OutsideSearchRange=true`. Search range and characterization range are reported separately.
- OPEN (needs a human call): `.gitignore` line 21 excludes `*.csproj`, and the headless test csproj deferred to Block 1.3 is a hand-authored file that would therefore be silently untracked. Either force-add it (`!BugCam.Tests.csproj`) or generate it from a committed script.
- OPEN (wording): `CLAUDE.md` line 33 says bitwise equality is "never used as the gate", while `PLAN.md` Block 1.4 VERIFY (a) makes bit-identical a hard pass condition. The two were reconciled in the 2026-07-29 decision below (1.4a is a determinism regression test, not the feature gate) but `CLAUDE.md` still reads as an absolute. One clarifying clause fixes it.
- RESOLVED (code path): local `PhysicsScene` via `SceneManager.CreateScene(..., LocalPhysicsMode.Physics3D)` requires Play Mode. Production harness now fails deterministically outside Play Mode; simulation tests live in `BugCam.Tests.PlayMode`. See Evidence log 2026-08-01.
- RESOLVED (Block 1.1 measurement): TowerScene dual-threading A/B/A′ numbers exist under `Library/BugCamEvidence/Block1.1/` (gitignored). `m_ThreadingMode` restored to `0` after the controlled experiment.

## Evidence log

### 2026-08-03 — PR #6 (Block 1.5) reviewed and squash-merged; Block 2.1 evidence-camera core landed (`feat/block-2.1-evidence-cameras`)

**PR #6 close-out.** Verified exact state before acting: local HEAD, `origin/feat/block-1.5-ghost-visualization` HEAD, and PR #6 `headRefOid` all matched the expected `35bdb6f8d0290b2c7a2023061ccbeeaa1cb8647d`; `mergeStateStatus=CLEAN`, `mergeable=MERGEABLE`, merge base `1bd10113eeeb8376ae31379b391e8c408d2884a8`; tracked worktree clean; the four protected files remained untracked. Independent read-only review of `3f2c93d..35bdb6f` (the interrupt-cleanup commit) confirmed: `CleanupHostOwnedSearch` destroys only the `BugCamGhostEvidenceRunner_TEMP` GameObject by name (no other object touched); `FinishBusy()` clears both Busy and Pending together; `shouldNotify` is captured from `IsSearchBusy` *before* `FinishBusy()` runs, so a second cleanup call is a no-op notify (idempotent); the success path's `Cleanup()` calls `notifyInterrupted:false` so a later `ExitingPlayMode` after a successful run finds `IsSearchBusy` already false and never emits a false "Interrupted" completion; the regression test `PlayModeInterruptCleanupDestroysTempRunnerAndAllowsRestart` is behavioral (creates a real `GameObject`, invokes the real `CleanupInterruptedSearchForTests` seam via reflection, asserts real destruction + real single non-success event + idempotent repeat + accepted restart), not a source-string assertion. Squash-merged as `558d4f1a763808e4eb3e4cbbe675b3d698cb1cf5` with the exact head-SHA merge guard; local `main` fast-forwarded to match `origin/main`.

**Block 2.1 landing scope (this branch).** Per docs/PLAN.md's amended Block 2.1 section: lands `Assets/BugCam/Evidence/EvidenceCameras.cs` (+ `EvidenceCameraMath.cs`, `EvidenceCameraPlanSchema.cs`, `EvidenceCameraPlanWriter.cs`) — deterministic candidate generation, scoring, honest verdict, and `camera-plan.json` — plus EditMode VERIFY. `RetroPlayer` and 2×2 compositing are deferred (see Open findings above). This split exists because Unity MCP was unavailable this session (no live Editor to verify a rendering path against), while the selection algorithm is fully checkable in batchmode.

**Why a pure post-process, no live scene.** PLAN's VERIFY demands selection "reproducible bit-for-bit by a third party from the same recorded runs" — a third party has only the run JSON, not the Unity scene, so occlusion is ray-vs-AABB math over recorded positions (never `Physics.Raycast`), and each candidate's frustum uses a `Matrix4x4` view-projection built directly from the candidate position (`Matrix4x4.LookAt(...).inverse` flipped to Unity's camera-space convention, verified against Unity 6000.3 scripting docs since no live Editor was available to test it empirically) plus `Matrix4x4.Perspective(...)`, never a scene `Camera` GameObject.

**Ratified this block (`DivergenceSettings`):** `MinEvidenceCoverageScore=0.5`, `EvidenceCandidateCount=128`, `WeightEvidenceCentrality=0.25` — all previously marked PROVISIONAL, now carry ratified why-comments. New fields added and recorded in `docs/PLAN.md`'s contract table (per its own "add to the contract in the same commit" rule): `EvidenceCameraVerticalFovDegrees=50`, `EvidenceCameraNearClip=0.05`, `EvidenceCameraFarClip=500`, `EvidenceRenderWidth/Height=1920x1080`, `EvidenceEventBoundsRadiusMultiplier=2.5`, `WeightCameraOrthogonality/ContactProximity/TrajectoryAlignment=100/10/1`, `ScreenSpaceSeparationNormalizer=500`.

**EditMode VERIFY added (`EvidenceCameraTests.cs`, zero-ref reflection pattern matching `GhostEvidenceTests.cs`):** repeated `Plan` over identical recorded runs yields byte-identical `camera-plan.json` and identical winner indices; every candidate (including ground-plane-rejected ones) is present in the output for provenance; camera 1 is the max-raw-score non-rejected candidate with index tie-break; cameras 2-4 are distinct from camera 1 and each other; visibility score is fractional (not binary — some, not all 9, of the per-body sample rays blocked) when an adjacent body partially occludes; ground-plane-rejected candidates carry all-zero scores; the honest verdict flips from `EVIDENCE COVERAGE: OK` to `EVIDENCE COVERAGE: LOW` when the coverage gate is raised, without changing the underlying candidate scores; `Plan` fails honestly (no fabricated event bounds/candidates/winners — `null`/`[]` in JSON) when there is no significant divergence to frame.

**VERIFIED FACT — batchmode Unity `6000.3.21f1`** (`run-checkpoint.ps1 -Suite All -EvidenceDir Library\BugCamEvidence\Block2.1-final`, exit 0):

| Suite | total | passed | failed | result | XML |
|---|---|---|---|---|---|
| EditMode | 102 | 102 | 0 | Passed | `Library/BugCamEvidence/Block2.1-final/EditMode.xml` |
| PlayMode | 21 | 21 | 0 | Passed | `Library/BugCamEvidence/Block2.1-final/PlayMode.xml` |

EditMode +9 vs the merged Block 1.5 tip (`EvidenceCameraTests`). PlayMode unchanged at 21 (this block adds no PlayMode surface). Two earlier local iterations of this same checkpoint (`Block2.1`, `Block2.1-retry`) failed on one EditMode test each — both were fixture-geometry bugs in the new test file itself (an occluder body positioned too far from, then too tightly overlapping, the diverging body), not defects in `EvidenceCameras.cs`; both were fixed by adjusting the test fixture and rerunning, not by loosening the property being asserted (the "never binary" occlusion property itself). `Assets/BugCam/Tests/TowerScene.unity` was touched by every PlayMode run (procedural regeneration, differing fileIDs) and reverted before commit each time — pre-existing behavior, unrelated to this block.

**Independent read-only review** (general-purpose reviewer, full diff + CLAUDE.md/AGENTS.md/PLAN.md/GhostEvidenceTests.cs/DivergenceEngine.cs/GhostEvidenceWriter.cs as background): zero BLOCKERs. Five NOTEs; three addressed in this same commit — (1) `EvidenceCameras.Validate` now fails closed if `settings.EvidenceOcclusionRays != 9` instead of silently ignoring that settings field (the 9-point AABB-centre-plus-8-corners sample set stays a fixed structural constant, per PLAN, but the settings field is no longer decorative); (2) fixed a wrong code comment misattributing Unity's w-divide behavior to `Matrix4x4.MultiplyPoint4x4` (it is `MultiplyPoint` that divides by w; the code itself was already correct); (3) `WorldToViewport`'s `inFrontOfCamera` output is now actually checked — a body behind the near plane contributes 0 to separation/centrality instead of garbage NDC coordinates. Two NOTEs left as-is by design: float-equality tie detection in the two sort routines is the standard FP-sort limitation (determinism across repeated runs is unaffected — same inputs always give the same outputs and the same winners, which is what PLAN's VERIFY requires); the four protected files remain untracked in the working tree, which is the correct, expected state, not a defect — confirmed not part of `git diff`/`git add` scope before commit. Full re-run after the three fixes: EditMode 102/102, PlayMode 21/21 (this table).

**Unity MCP:** unavailable this whole session (`instance_count` not queryable — no server connected, no Unity Editor process running, confirmed via `tasklist`). No live physics-settings snapshot (threading mode, solver iterations, contact offset, sleep/bounce thresholds) was captured this session; the multithreading toggle and per-body physics-settings snapshot from prior sessions live under `Library/BugCamEvidence/Block1.1/` (Block 1.1 measured both threading modes explicitly) and are not repeated here.

### 2026-08-02 — Block 1.5 ghost visualization (`feat/block-1.5-ghost-visualization`)

**Base:** Block 1.4 merge SHA `1bd10113eeeb8376ae31379b391e8c408d2884a8`.

**Architecture:**
- `Assets/BugCam/Evidence/` + `BugCam.Evidence.asmdef` (refs `BugCam.Core` only; no UnityEditor).
- `BugCam.Editor` references `BugCam.Evidence`.
- Core: zero edits — consumes `EpsilonSearchResult` / `RunResult` / `DivergenceEngine` / `DivergenceSettings.GhostBodyLimit`.
- Builder re-Analyzes baseline vs each retained fan; STABLE ⇒ no fabricated fans; fan order multiplier-major × X/Y/Z; `OutsideSearchRange` preserved; search identity (targetBodyId, axis, strategy) captured at build time.
- Ranking comparator: `PerBodyMaxPositionErrorMetres` desc, `bodyId` asc; limit 10; omit zero-error.
- Scene View: `SceneView.duringSceneGui` + `Handles.DrawAAPolyLine`; menu `BugCam/Ghost Visualization`.
- Evidence paths: `Library/BugCamEvidence/Runs/<run-id>/` + `Library/BugCamEvidence/Block1.5` checkpoint. Schema `BugCam.GhostEvidence` v1 (`metrics.json` for AI consume).
- Honesty: no fake threshold when `HasThresholdEstimate=false`; `referenceIsExactThreshold` always false; amplification only when `AmplificationDefined`.
- Day 2 not started.

**How to run viz:** Unity menu `BugCam/Ghost Visualization` → Run / Load Ghost Search (Play Mode TowerScene search at step count 32 by default) → Scene View shows baseline/fans/markers → evidence written automatically. Alternate: `BugCam/Run Ghost Evidence (Tower / step 32)`.

**AI consume:** `Library/BugCamEvidence/Runs/<run-id>/metrics.json` (`schemaVersion=1`, `kind=BugCam.GhostEvidence`).

**VERIFIED FACT — batchmode Unity `6000.3.21f1`** (`run-checkpoint.ps1 -Suite All -EvidenceDir Library\BugCamEvidence\Block1.5`, exit 0):

| Suite | total | passed | failed | result | XML |
|---|---|---|---|---|---|
| EditMode | 81 | 81 | 0 | Passed | `Library/BugCamEvidence/Block1.5/EditMode.xml` |
| PlayMode | 20 | 20 | 0 | Passed | `Library/BugCamEvidence/Block1.5/PlayMode.xml` |

EditMode +14 (`GhostEvidenceTests`). PlayMode +1 smoke (`GhostEvidencePlayModeTests`, FastStepCount=40 — separate from VERIFY step 32).

**VERIFIED FACT — live Unity MCP** (project `X:\AI CAM\nimbalyst-local\bugcam`, editor `6000.3.21f1`):
- Simulation Mode = Script; Enhanced Determinism = False.
- Real Tower search (step 32, AscendFromStart, body 49, axis X) → verdict `THRESHOLD BRACKET FOUND`; success-path console `BUGCAM_BLOCK_1_4_EPSILON_SEARCH` + `BUGCAM_BLOCK_1_5_GHOST_EVIDENCE`.
- Scene View session: 15 fans, 10 ranked bodies, 160 polylines, first-divergence + max-spread markers; `activeSceneDirty=false`; no leaked `BugCamGhost*` / `*_TEMP` GameObjects; sceneCount returned to 1; Play Mode exited cleanly.
- Generated run evidence: `Library/BugCamEvidence/Runs/ghost-20260803T024343884-body49-X-AscendFromStart/` (+ checkpoint pointer under `Library/BugCamEvidence/Block1.5/`).
- **CORRECTION — named PNGs in that run are NOT visual success:** `overview` / `first-divergence` / `max-spread` / `final` were byte-identical solid clear-color blanks (`#1F1F24`, same SHA256). Prior GL path omitted `Material.SetPass`, so lines never composited. Do not cite those PNGs as proof of distinct framing.

### 2026-08-02 — Block 1.5 gate fix (first-div marker + screenshot compositing)

**Historical note:** an earlier tip asserted first-div marker via `MaxSpreadBodyId` proxy. That was incorrect and is superseded by the 2026-08-03 pr6-fix entry (`FirstDivergenceBodyId`).

**Screenshot capture:** `GhostScreenshotCapture` uses `Hidden/Internal-Colored` + `Material.SetPass(0)` before GL lines/markers; refuses solid clear-color frames (no blank PNG kept). EditMode contract `ScreenshotCaptureFailsClosedOnBlankOrWritesDistinctPngs` accepts honest omit under `-nographics`, or distinct hashes when GPU compositing succeeds. Named PNGs from GPU editor sessions are the only valid visual proof — not batchmode blanks.

**Honesty:** failure-console assert requires both `thresholdEstimateMetres=null` and `succeeded=False`. Day 2 not started.

**VERIFIED FACT — batchmode Unity `6000.3.21f1`** (`run-checkpoint.ps1 -Suite All -EvidenceDir Library\BugCamEvidence\Block1.5-review-fix-2`, exit 0):

| Suite | total | passed | failed | result | XML |
|---|---|---|---|---|---|
| EditMode | 85 | 85 | 0 | Passed | `Library/BugCamEvidence/Block1.5-review-fix-2/EditMode.xml` |
| PlayMode | 20 | 20 | 0 | Passed | `Library/BugCamEvidence/Block1.5-review-fix-2/PlayMode.xml` |

EditMode +1 vs prior tip (`ScreenshotCaptureFailsClosedOnBlankOrWritesDistinctPngs`). Named GPU PNGs not regenerated this pass (no Unity MCP / interactive GPU editor); capture now omits under Null graphics instead of keeping `#1F1F24` blanks.

**Day 2:** not started.

### 2026-08-03 — Block 1.5 PR #6 merge-blocker fix (`feat/block-1.5-ghost-visualization`)

**Base tip before fix:** `459e7ee586281cda62ae9c43eeba489ebdec6946`. **Fix tip:** `df38791d8baab95fc9d0850bae8fcf478318ae77`. **Base main:** `1bd10113eeeb8376ae31379b391e8c408d2884a8`.

**Blockers fixed:**
1. **Nested Editor coroutine:** removed broken `EditorCoroutineUtility`; Window routes through `GhostEvidencePlayModeHost` MonoBehaviour nested coroutines; shared `BugCam.GhostSearch.Busy` lock.
2. **Evidence bundle:** writes `runs/baseline.json` + `runs/fan-XX.json` from canonical `BaselineRun`/`Fans[i].Run`; visual filenames `first-sustained-divergence.png` / `maximum-spread.png` / `final-state.png` / `overview.png`; console at `report/console-report.txt`; manifest lists relative paths + availability/status.
3. **Fabricated primary metrics:** JSON null + `has*` when `!Success` / no significant divergence; panel shows `unavailable`; `FormatHonestSearchReport` gated on `document.Success`.
4. **First-div marker:** Core `FirstDivergenceBodyId` (argmax |Δpos| @ first-div frame); GhostRenderer uses it; fixture proves first-div body ≠ MaxSpreadBodyId ≠ AffectedBodyIds[0].

**Hardening:** both axes validated; fan ε tolerance fail-closed; OutsideSearchRange consistency fail-closed; session Clear unregisters Scene View callback; screenshot material destroyed on reload/quit.

**VERIFIED FACT — batchmode Unity `6000.3.21f1`** (`run-checkpoint.ps1 -Suite All -EvidenceDir Library\BugCamEvidence\Block1.5-pr6-fix`, exit 0):

| Suite | total | passed | failed | result | XML |
|---|---|---|---|---|---|
| EditMode | 90 | 90 | 0 | Passed | `Library/BugCamEvidence/Block1.5-pr6-fix/EditMode.xml` |
| PlayMode | 21 | 21 | 0 | Passed | `Library/BugCamEvidence/Block1.5-pr6-fix/PlayMode.xml` |

**Unity MCP live:** unavailable this pass (`instance_count=0`). GPU PNG regenerate / Window-button Scene View proof not re-run live; batchmode honesty + nested-coroutine / Host path contracts are green. Day 2 not started.

### 2026-08-03 — Block 1.5 PR #6 leftover merge-blocker fix2 (`feat/block-1.5-ghost-visualization`)

**Prior code-fix SHA:** `df38791d8baab95fc9d0850bae8fcf478318ae77`. **Prior docs SHA:** `d0ff85418cee9c9cd05660ca69dead1e3a632648`. **This leftover-fix commit / HEAD:** same as the commit that lands this entry (full SHA in PR #6 body after push). **Base main:** `1bd10113eeeb8376ae31379b391e8c408d2884a8`.

**Blockers fixed:**
1. **Busy lock:** Menu + all public starts route through `TryStartTowerSearch`; `StartTowerSearch` private; `ExitingPlayMode` calls `FinishBusy` when Busy (interrupted mid-run too); EditMode concurrent reject while busy.
2. **BuildFailed honesty:** `GhostEvidenceReport.hasThresholdEstimate = search.HasThresholdEstimate && document.Success` (matches metrics); BuildFailed-over-bracket regression.
3. **STATUS tip labeling:** distinguish prior code-fix SHA vs docs SHA; HEAD = this leftover-fix commit (no tip-chase of ancestors).
4. **Host nested-coroutine soft-green:** Window→`TryStartTowerSearch` + Host `StartCoroutine` / `yield return runner.Run` source contracts (in-memory enumerator demo is footnote only).
5. **Floor-divergent JSON honesty:** success path `DIVERGENT AT SEARCH FLOOR` → no threshold, fans retained (matches PlayMode FastStepCount=40).

**Hardening:** Session `Register` gated on has-document; Window unsubscribes `SearchCompleted` in `OnDisable` (idempotent OnEnable).

**VERIFIED FACT — batchmode Unity `6000.3.21f1`** (`run-checkpoint.ps1 -Suite All -EvidenceDir Library\BugCamEvidence\Block1.5-pr6-fix2`, exit 0):

| Suite | total | passed | failed | result | XML |
|---|---|---|---|---|---|
| EditMode | 92 | 92 | 0 | Passed | `Library/BugCamEvidence/Block1.5-pr6-fix2/EditMode.xml` |
| PlayMode | 21 | 21 | 0 | Passed | `Library/BugCamEvidence/Block1.5-pr6-fix2/PlayMode.xml` |

EditMode +2 vs pr6-fix (`ConcurrentTryStartTowerSearchRejectsWhileBusy`, `BuildFailedOverBracketNullsThresholdInConsoleAndMetrics`, `FloorDivergentSuccessRetainsFansWithoutThresholdEstimate`; nested-coroutine contracts consolidated). PlayMode unchanged at 21 with FastStepCount=40 floor-divergent honesty asserts.

**Unity MCP live:** unavailable (`instance_count=0`). GPU PNG / Window-button Scene View not re-run live. Day 2 not started.

### 2026-08-03 — Block 1.5 PR #6 play-mode interrupt Host cleanup (`feat/block-1.5-ghost-visualization`)

**Prior HEAD:** `3f2c93d6e6997f91ad0f431af6a5fc756ff0eb38`. **This fix commit / HEAD:** same as the commit that lands this entry (full SHA in PR #6 after push). **Base main:** `1bd10113eeeb8376ae31379b391e8c408d2884a8`.

**Root cause:** `BugCamGhostEvidenceRunner_TEMP` uses `DontDestroyOnLoad` + `HideFlags.DontSave`, so exiting Play Mode mid-search left the TEMP runner alive and could leave Busy sticky / block a later Window search.

**Cleanup:** single Host path `CleanupHostOwnedSearch` clears Busy/Pending, destroys Host TEMP runner (stops its coroutine), and on interruption notifies `SearchCompleted` with `WriteSucceeded=false` (never false success). Used by normal completion, write/search failure, `ExitingPlayMode`, and runner shutdown. Deferred `Destroy` during Play Mode exit; hard-sweep leftovers on `EnteredEditMode`. Deterministic EditMode seam: `CleanupInterruptedSearchForTests` (+ `AllowPlayModeEntry` for restart without flipping play mode).

**Regression:** `PlayModeInterruptCleanupDestroysTempRunnerAndAllowsRestart` — TEMP present → interrupt cleanup → TEMP gone, Busy/Pending false, interruption notify once, idempotent second cleanup, subsequent `TryStartTowerSearch` accepted.

**VERIFIED FACT — batchmode Unity `6000.3.21f1`** (`run-checkpoint.ps1 -Suite All -EvidenceDir Library\BugCamEvidence\Block1.5-pr6-interrupt-fix`, exit 0):

| Suite | total | passed | failed | result | XML |
|---|---|---|---|---|---|
| EditMode | 93 | 93 | 0 | Passed | `Library/BugCamEvidence/Block1.5-pr6-interrupt-fix/EditMode.xml` |
| PlayMode | 21 | 21 | 0 | Passed | `Library/BugCamEvidence/Block1.5-pr6-interrupt-fix/PlayMode.xml` |

EditMode +1 vs leftover-fix2 (`PlayModeInterruptCleanupDestroysTempRunnerAndAllowsRestart`). PlayMode unchanged at 21.

**Unity MCP live interrupt (editor `6000.3.21f1`):** Physics Simulation Mode=Script; Enhanced Determinism=OFF. Ghost Visualization Window/`TryStartTowerSearch` started → interrupted at update frame 2 while Busy+TEMP → TEMP gone, Busy/Pending false, no Host TEMP / BugCamGhost / BugCam RT/material leaks, sceneCount=1. Immediate second search completed `WriteSucceeded=true`, verdict `THRESHOLD BRACKET FOUND`, evidence `Library/BugCamEvidence/Runs/ghost-20260803T051155871-body49-X-AscendFromStart` (four GPU PNGs written). Prior run `ghost-20260803T042655079-body49-X-AscendFromStart` four PNGs remain intact. Day 2 not started.

### 2026-08-02 — Block 1.4 adaptive epsilon search (`feat/block-1.4-epsilon-search`)

**Implementation:** step-driven `EpsilonSearch` (`TryGetNextProbe` / `SubmitProbeResult`) + `EpsilonSearchRunner` (sequential harness probes, scene-cleanup wait). Ladder → monotonicity gate → exponential (strategy) → bisection → fan. Retains full `RunResult` frames for baseline + exactly 15 fan runs; compact summaries elsewhere. Fan above search ceiling marked `OutsideSearchRange` (no silent clamp). Search range vs characterization range reported separately. Never claims an exact mathematical threshold.

**VERIFY correction (docs):** algorithm convergence compares starting strategies on the same axis / scene / target body / configuration. X/Y/Z are directional characterization only.

**VERIFIED FACT — batchmode Unity `6000.3.21f1`** (`run-checkpoint.ps1 -Suite All -EvidenceDir Library\BugCamEvidence\Block1.4`, exit 0):

| Suite | total | passed | failed | result | XML |
|---|---|---|---|---|---|
| EditMode | 58 | 58 | 0 | Passed | `Library/BugCamEvidence/Block1.4/EditMode.xml` |
| PlayMode | 16 | 16 | 0 | Passed | `Library/BugCamEvidence/Block1.4/PlayMode.xml` |

EditMode +13 (`EpsilonSearchTests`). PlayMode +5 (`EpsilonSearchPlayModeTests`). Blocks 1.1–1.3 regressions included.

**Review-fix (merge blockers):** unbracketed divergent samples report `DIVERGENT AT SEARCH FLOOR` without `HasThresholdEstimate`; ladder locked to exactly 12; fan locked to `{0.8,0.9,1.0,1.1,1.2}` (15 runs); cleanup timeout is a structured search failure that stops further probes.

**VERIFIED FACT — batchmode Unity `6000.3.21f1`** (`run-checkpoint.ps1 -Suite All -EvidenceDir Library\BugCamEvidence\Block1.4-review-fix`, exit 0):

| Suite | total | passed | failed | result | XML |
|---|---|---|---|---|---|
| EditMode | 67 | 67 | 0 | Passed | `Library/BugCamEvidence/Block1.4-review-fix/EditMode.xml` |
| PlayMode | 19 | 19 | 0 | Passed | `Library/BugCamEvidence/Block1.4-review-fix/PlayMode.xml` |

**Verify-fix (merge blockers):** Commit labels use feature `95ef465` / verify-fix code `8346e34` (not “tip/HEAD” for older SHAs); PLAN `BisectionIterations` default = 7 (prior 6–8 range narrowed); PlayMode `WaitCleanup` captures `initialSceneCount` before search; named VERIFY contracts fail closed on STABLE / non-bracket verdicts. `DefaultStepCount` 250 yields `DIVERGENT AT SEARCH FLOOR` on this tower — measured AscendFromStart X sweep: 31=STABLE, 32–34=`THRESHOLD BRACKET FOUND`, ≥35=floor divergent; VERIFY uses step count **32**. FanMultipliers SO getter / EditMode honesty assert left NON_BLOCKING.

**VERIFIED FACT — batchmode Unity `6000.3.21f1` after verify-fix** (`run-checkpoint.ps1 -Suite All -EvidenceDir Library\BugCamEvidence\Block1.4-verify-fix`, exit 0; verify-fix/evidence code SHA `8346e3425e1ab76a0beee9c0111b8ca08eb1dd41`):

| Suite | total | passed | failed | result | XML |
|---|---|---|---|---|---|
| EditMode | 67 | 67 | 0 | Passed | `Library/BugCamEvidence/Block1.4-verify-fix/EditMode.xml` |
| PlayMode | 19 | 19 | 0 | Passed | `Library/BugCamEvidence/Block1.4-verify-fix/PlayMode.xml` |

**PlayMode VERIFY scope (honest):**
- **Proven:** fail-closed `THRESHOLD BRACKET FOUND` + fan retention contracts; same-axis strategy convergence within **±1 growth step** (`EpsilonGrowthFactor=2` ⇒ Ratio≤2) at measured `VerifyStepCount=32`.
- **OPEN/partial — VERIFY (a):** literal PLAN bit-identical identical-config **search** repeat is **not** proven; current test is repeated **baseline-only** within gate ≤1e-6.
- **Not claimed:** ±1 **bisection-step** agreement (PLAN literal (b) as originally written) — proven scope is ±1 **growth** step only. Adjudicated DOCS_CLAIM_AMENDMENT; do not reopen Core for bit-identical full-search.

**Day 1 hard checkpoint:** NOT PASSED (Block 1.5 ghost visualization still required for Scene View fan; success-path threshold/fan console report not logged).

### 2026-08-02 — Block 1.3 review-fix (PR #4, `feat/block-1.3-divergence-engine`)

**Commit:** `block-1.3: address final divergence review` — SHA: `875afb4484905e968a840e6a29b5bacc644431ec`

**Review findings fixed (exact):**
1. Non-finite quaternion components (NaN / +Inf / −Inf) rejected before quaternion dot; Analyze returns structured `DivergenceResult` failure (no Inf→clamp→0° path).
2. Opposite-half AND-gate synthetic test: position > threshold for ≥ SustainedSteps with scene score ≤ SceneScoreThreshold ⇒ not significant; plus strict `>` boundaries at exact score/position thresholds. Existing high-score-without-position test retained.
3. Non-finite quaternion regression tests (NaN / +Inf / −Inf); q/−q retained.
4. Checkpoint runner suite pass requires `total>=1`, `failed==0`, `passed==total`, result starts with `Passed`, Unity exit code 0 (empty suite fails).
5. `Analyze(RunResult,RunResult)` coverage: failed baseline/perturbed, mismatched step/body/stable ids → structured failure (public factories only).
6. STATUS: AffectedBodyIds definition, SceneScoreThreshold default corrected to 1.0, Day 1 still NOT PASSED, Block 1.4 not started.

**AffectedBodyIds definition:** AffectedBodyIds contains bodies whose maximum position error anywhere in the analyzed run exceeds PerBodyPositionThreshold. It is not limited to the first sustained divergence window.

**VERIFIED FACT — batchmode Unity `6000.3.21f1` after review-fix** (`run-checkpoint.ps1 -Suite All -EvidenceDir Library\BugCamEvidence\PR4-review-fix`, exit 0):

| Suite | total | passed | failed | result | XML |
|---|---|---|---|---|---|
| EditMode | 45 | 45 | 0 | Passed | `Library/BugCamEvidence/PR4-review-fix/EditMode.xml` |
| PlayMode | 11 | 11 | 0 | Passed | `Library/BugCamEvidence/PR4-review-fix/PlayMode.xml` |

EditMode was 34 → 45 (+11: AND-gate opposite half, score/position boundary cases, 3 non-finite quat cases, 5 RunResult Analyze API cases). PlayMode unchanged at 11. Enhanced Determinism OFF; Physics SimulationMode Script; no ProjectSettings drift intended.

**Day 1 hard checkpoint:** NOT PASSED. **Block 1.4:** not started.

### 2026-08-02 — Block 1.3 Divergence Engine (`feat/block-1.3-divergence-engine`)

**VERIFIED FACT — formulas (metres / degrees; mm only at display):**
- Object scale = characteristic body size in metres (box: largest local-scale axis). Null ⇒ 1 m for every body. Non-positive/non-finite scale ⇒ fall back to 1 m (never NaN/Infinity in posNorm).
- Per body, per frame: `posNorm = |Δpos| / objectScale`, `rotNorm = angleDegrees(q,q') / PerBodyRotationThreshold` (q and −q identical), `velNorm = |Δvel| / PerBodyVelocityThreshold`, `sleep ∈ {0,1}`.
- Scene Divergence Score (step) = `Σ_i (Wp·posNorm + Wr·rotNorm + Wv·velNorm + Ws·sleep)` (weighted sum, not a mean; default gate 1.0).
- Significant iff score > `SceneScoreThreshold` AND ≥1 body has `|Δpos| > PerBodyPositionThreshold` AND both hold for `SustainedSteps` consecutive frames. `firstDivergenceFrame` = first frame of that window.
- `amplification = maxSpreadMetres / epsilonMetres` when epsilon > 0; when epsilon == 0 → `AmplificationDefined=false`, `Amplification=0` (never Infinity).
- Quaternion components must be finite before angle; non-finite ⇒ structured Analyze failure.

**ASSUMPTION:** default thresholds in `DivergenceSettings` (e.g. position 1 mm, scene score **1.0**, weights) are provisional product defaults with why-comments; Block 1.4/2.1 may ratify provisional evidence/search fields.

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
- 2026-08-03 — Block 2.1 split into a batchmode-verifiable core (this commit) plus a deferred follow-up (RetroPlayer + 2×2 compositing), because Unity MCP was unavailable this session and a rendering path cannot be honestly claimed done without a live GPU Editor session to look at it — the same standard applied to Block 1.5's screenshot capture after the `#1F1F24` blank-PNG correction.
- 2026-08-03 — Evidence-camera occlusion/frustum scoring is a pure post-process over recorded `RunResult`/`DivergenceResult` data: ray-vs-AABB math (never `Physics.Raycast`) and a `Matrix4x4`-built view-projection (never a scene `Camera`), because PLAN's VERIFY requires bit-for-bit reproducibility "by a third party from the same recorded runs" — a third party has the run JSON, not the Unity scene.
- 2026-08-03 — Bodies are scored as world-axis-aligned AABBs (position ± half of recorded `Size`), not rotation-aware OBBs. "Contact proximity" (PLAN camera 2-4 criterion b) approximates to distance-from-divergence-event-bounds-center, since collision-pair/contact data is backlog per `CLAUDE.md`. Cameras 2-4 ranking approximates PLAN's "(a), (b), (c) in that order" as a 100:10:1 weighted sum rather than strict lexicographic sort, to keep ties resolved by index rather than float equality.
- 2026-08-02 — Block 1.4 docs honesty amend: PlayMode proves fail-closed bracket/fan + same-axis ±1 **growth** step (Ratio≤2) at `VerifyStepCount=32`; VERIFY (a) bit-identical identical-config **search** repeat remains OPEN/partial (baseline-only ≤1e-6 today); ±1 bisection-step agreement not claimed. Success-path `EpsilonSearchReport` is not logged (cleanup-timeout errors only).
- 2026-08-02 — Block 1.4 VERIFY (b) corrected: algorithm convergence compares different starting strategies on the **same axis, same scene, same target body, same configuration**. X/Y/Z searches are directional characterization; physical thresholds are reported but not required to match. Fan above search ceiling is marked `OutsideSearchRange` (no silent clamp). Search range and characterization range are reported separately.
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
- 2026-07-29 — Block 1.4 VERIFY split: identical-config repeat must be bit-identical (a determinism regression test), and three searches from different start brackets must agree within ±1 step. As originally written the check was satisfied trivially by determinism and tested nothing about convergence. A 12-point log-uniform epsilon ladder runs first to expose non-monotonicity as data. **2026-08-02 amend:** PlayMode proves ±1 **growth** step (Ratio≤2); ±1 bisection-step remains future/OPEN; VERIFY (a) search-repeat bit-identical remains OPEN/partial (baseline-only ≤1e-6).
- 2026-07-29 — Architecture tree gained `Tests/`, and a fourth test-only asmdef `BugCam.Tests` is allowed alongside the three production asmdefs.
- 2026-07-29 — `PLAN.md` Block 2.1 amended: evidence cameras are a deterministic scored post-process (Fibonacci-sphere candidates, fractional 9-ray occlusion, index tie-break, top-25% filter then optimize), replacing "raycast visibility check". Carries a requirement into Block 1.3: `DivergenceSettings` needs `MinEvidenceCoverageScore` alongside `PerBodyPositionThreshold`, so the `EVIDENCE COVERAGE: LOW` verdict has a number behind it.
- 2026-07-29 — Deferred deliberately: headless test csproj → Block 1.3; `BugCam.Evidence` asmdef → Block 1.5/2.1 (an empty assembly only produces warnings today); `DivergenceSettings` `.asset` authoring → Block 1.3 via `[CreateAssetMenu]` + code defaults; an Editor-side PhysicsScene provider abstraction → contingency, only if the Edit Mode path fails.
