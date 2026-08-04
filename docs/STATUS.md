# BugCam — STATUS

> Updated by the agent after every completed block. This file is the memory between sessions.

## Current position
- Active block: **Block 2.2.2 — Capture universality** (user-inserted 2026-08-03 ahead of Block 2.3; branch `feat/block-2.2.2`). Contract written 2026-08-03 «contract first, no code», adversarially verified (26 checks, three fixes), open questions №1–№10 adjudicated by the human 2026-08-04, **ratified 2026-08-04**: full text `docs/CONTRACT-2.2.2.md` (PLAN Block 2.2.2 is the summary; contradiction = STOP). The contract's mandatory empirical precondition **executed 2026-08-04** (see Evidence log): edit mode reads Read/Write-disabled mesh geometry, Play Mode does NOT (editor `AssetDatabase` channel included), structural fingerprint (vertexCount / subMeshCount / bounds) and MeshCollider cooking DO work in Play Mode ⇒ **Amendment 2026-08-04 ratified by the human**: full contentHash moves to an edit-mode capture point before Play Mode entry; the simulation point verifies a structural fingerprint; `SCENE_MESH_RESOLVE_FAILED` fires on unresolved mesh or fingerprint mismatch. Amendment wordings approved 2026-08-04; **implementation LANDED** (`dc64ac2`/`d72f88b`/`f8bb4ca`/`7be8601`); **exit gate COMPLETE 2026-08-04**: assembled third-party mesh scene (KayKit Dungeon Remastered 1.0, CC0, https://github.com/KayKit-Game-Assets/KayKit-Dungeon-Remastered-1.0 — ruling 7 amendment) captured with zero fail-closed objects and searched end-to-end; verdict **DIVERGENT AT SEARCH FLOOR accepted by the human as the final gate result** (intentionally unstable scene, honest verdict, no tuning per the A8 precedent); tower and domino pins bit-identical after every change (see the exit-gate Evidence-log entry). **Block 2.2.2 work COMPLETE on the branch — merge to `main` is the human's call.** After 2.2.2 per PLAN order: **Block 2.3 — Evidence overlay + export (RetroPlayer + 2×2 compositing)**. Carried over: live window screenshots of scene-mode states (PENDING since 2.2.1, does not block).
- Block 2.2.1: **CLOSED 2026-08-03.**
- Block 2.2.1 (Polish pass, user-inserted): DONE and squash-merged as **PR #10**, squash SHA `e26952f041d6ff3561737d9137f92f7ca2a6ec1d` (merge with head-SHA match local == origin == PR headRefOid; local `main` fast-forwarded; post-merge main gate EditMode 134/134 + PlayMode 24/24, `Library/BugCamEvidence/Block2.2.1-main-closure`). A1 was merged earlier as PR #9, squash `7bbaa45c17de5e24be08227a34cc0af89cf1b29b`. Chain A6+A7 → A4 → A1 → A3 → A2 (+ sleeping-notice follow-up) → A5 → A8 complete; full ratified contract: `docs/CONTRACT-2.2.1.md`; per-stage evidence: the 2026-08-03 Evidence log entries. Closure rulings executed 2026-08-03: (1) **score-half of the AND-gate kept PERMANENTLY**, deletion question closed without right of reopening on this contract (A8 domino re-measurement: 175 opposite-direction cases, structural mechanism — see the A8 entry); (2) **default stepCount stays 32, question closed** — handoff to Block 2.3 window UX (editable stepCount or mode presets); (3) **CS0162 suppressed** with pragma disable/restore around the intentional StateStride dead-code guard, commit `c085ce0` — the guard itself must never be removed. Carried over to Block 2.3: live window screenshots of scene-mode states (PENDING, does not block).
- Block numbering sync (2026-08-03, after PR #8): «2.2-window-ux» in older entries below = **PLAN Block 2.2 — Ghost Visualization window UX**; historical suffixes in old log entries are intentionally left as written. The former PLAN 2.2 "Evidence overlay + export" is renumbered to 2.3 (EditorWindow → 2.4, hero video → 2.5, landing → 2.6).
- Block 2.2 (Ghost Visualization window UX): DONE, squash-merged as `168c022` (#8); final gate EditMode 107/107 + PlayMode 21/21 (`Library/BugCamEvidence/Block2.2-final-gate`). Choice of the next active block is a separate pending decision.
- Block 2.1 (evidence camera selection): deterministic core landed and merged as `fd54ab9` (#7); RetroPlayer + 2×2 compositing → Block 2.3 (see Evidence log 2026-08-03 below).
- Day 1 checkpoint: PASSED (Scene View fan + success-path honest reports + evidence bundle)
- Day 2 checkpoint: NOT PASSED — `EvidenceCameras.cs` + `camera-plan.json` exist and are batchmode-verified; RetroPlayer, MP4, cockpit, SceneSight remain not started; 2×2 viewport/RenderTexture compositing remains not started.
- PR #6 (Block 1.5, Ghost Visualization + evidence bundles): reviewed and squash-merged. Squash SHA `558d4f1a763808e4eb3e4cbbe675b3d698cb1cf5`. Merge base `1bd10113eeeb8376ae31379b391e8c408d2884a8`. Independent review of the final commit range `3f2c93d6e6997f91ad0f431af6a5fc756ff0eb38..35bdb6f8d0290b2c7a2023061ccbeeaa1cb8647d` found no blocker (interrupt cleanup destroys only the Host-owned TEMP runner, Busy/Pending always clear, cleanup is idempotent, no false `SearchCompleted` success on interrupt, regression test is behavioral). `local main` fast-forwarded to the squash SHA; no post-merge bookkeeping commit was made on `main` (this STATUS update lands on the Block 2.1 branch instead, per the no-`main`-commit rule).
- Unity MCP is **live again** as of 2026-08-03 (mcp-for-unity-server v3.4.5, HTTP 127.0.0.1:8080/mcp, user-scope config): see the "Live physics snapshot" Evidence log entry below. The earlier note that "this session had zero live Unity verification" described the previous session only.

## Completed blocks
| Block | Result | Verification | Commit |
|---|---|---|---|
| docs | Spec contradictions resolved (11 items), git repo initialised | Review approved by human 2026-07-29 | `chore: docs + claude setup`, `docs: fix spec contradictions` |
| 1.1 | TowerScene A/B/A′ determinism probe in both threading modes + Editor restart | See Evidence log 2026-08-02 | squash `91ae29d` (#2) |
| 1.2 | StateRecorder + RunResult + kinematic transform replay VERIFY | See Evidence log 2026-08-02 (Block 1.2) | squash `a90765a` (#3) |
| 1.3 | DivergenceSettings + DivergenceEngine synthetic + RunResult integration + review-fix | See Evidence log 2026-08-02 (Block 1.3 review-fix) | squash `6d676ad` (#4) |
| 1.4 | Adaptive epsilon search (step-driven) + partial PlayMode VERIFY (fail-closed bracket/fan + ±1 growth-step Ratio≤2; VERIFY (a) closed 2026-08-03 by Block 2.2.1 A5) | See Evidence log 2026-08-02 (Block 1.4 verify-fix) + 2026-08-03 A5 | merge SHA `1bd10113eeeb8376ae31379b391e8c408d2884a8` (#5) |
| 1.5 | Ghost visualization + evidence bundle (`BugCam.Evidence` + Scene View drawer + Ghost Visualization window + interrupt-safe Host cleanup) | See Evidence log 2026-08-03 (pr6-interrupt-fix) + 2026-08-03 merge review below | squash `558d4f1a763808e4eb3e4cbbe675b3d698cb1cf5` (#6, merged) |
| 2.2.1 | Polish pass: A1 search-entry parameterization; A3 max-norm score + threshold 0.2 (score-half adjudication closed permanently at A8); A2 arbitrary-scene capture + sleeping-body notice; A4 Scene View legend; A5 bit-identical repeat pin (closes PLAN 1.4 VERIFY (a)); A6+A7 docs; A8 domino exit gate through the capture path + agreement re-measurement | See Evidence log 2026-08-03 (A1…A8 entries); final gate EditMode 134/134 + PlayMode 24/24 (`Block2.2.1-a8`); post-merge main gate identical (`Block2.2.1-main-closure`) | A1 squash-merged as PR #9 `7bbaa45`; A2–A8 + closure rulings (CS0162 pragma `c085ce0`, stepCount=32 closed `c732790`) squash-merged as **PR #10**, squash SHA `e26952f041d6ff3561737d9137f92f7ca2a6ec1d` |

## Open findings / blockers
- OPEN (product, recorded 2026-08-03 during the human's first real A1 window session — record only, nothing decided): **with the default 32 steps the max spread is 3.79 mm — physically invisible in Scene View at any reasonable zoom**, so the ghost fan reads as "nothing happened" despite honest numbers. The hero demo and any visual evidence need a longer sim window. Reconsider the default step count (or make the window hint that visible fans need N steps). Related instrumentation datum: the 32-step window is also analytically tight — first divergence frame 27 + 5 sustained steps = exactly 32 (A3 step-1 Evidence entry, finding 3). Adjudication 2026-08-03: default step count NOT changed now; verified fact — every pinned regression test already passes its step count explicitly (`VerifyStepCount=32`, `FastStepCount=40`, checkpoint 250 — constants in test sources, none relies on a default), so the default can be reconsidered at block close without breaking pins. Third confirmation 2026-08-03 (Block 2.2.1 A8, adjudicated record): the domino chain physically cannot develop within 32 steps (0.64 s for a 5-domino run) — chain bodies stayed ≤ 0.34 mm apart between baseline and perturbed runs while the verdict bracket formed on the target body alone; scene deliberately NOT tuned (the gate proves the pipeline, spectacle belongs to the hero demo, Block 2.5); see the A8 Evidence entry. **Closure ruling 2 (2026-08-03, block 2.2.1 closure): the default stepCount STAYS 32 — the question is CLOSED.** Rationale as ratified: the control-gate procedure is defined as «search with defaults», so changing the default would invalidate every pinned number and the bit-identical comparison exactly when the block is stable; the triple-confirmed tight-window finding is a product finding about MODES (32 = engineering precision, 250 = visual demo material), not about a wrong default. **Handoff → Block 2.3:** the right place to resolve it is window UX (editable stepCount or mode presets), not a silent constant change.
- OPEN (product, addendum 2026-08-03 — human experiment at 250 steps with the A1-edited ε floor 0.001 mm; record only): verdict **DIVERGENT AT SEARCH FLOOR**, min diverging ε = 1 µm (= search floor), first frame 29 (body 6), max spread **4.54 m** (body 47), amplification **4535758×**, 49 of 49 bodies, fan fully visible in Scene View. Two regimes established: **32 steps = engineering precision (threshold bracket), 250 steps = visual demo material (visible fan, metre-scale spread)** — the hero demo will likely need both. The verdict system worked as designed: no fake threshold was invented at the floor.
- OPEN (product, backlog, recorded 2026-08-03 during Block 2.2.1 A8 — adjudicated record-only, no code changes in this block): **a THRESHOLD BRACKET FOUND verdict can describe the perturbation-detection threshold on the target itself rather than chain divergence.** Domino A8 run `ghost-20260803T174400539-body1-X-AscendFromStart`: amplification 1.00000107, affectedBodyIds=[1] (the perturbed target only), maxSpread = the applied ε itself — the bracket (2.28…2.30 mm) marks where the target's own offset + rotation transient starts qualifying, not propagated divergence. The verdict is formally honest, but the product sells amplification (SPEC §2, §10), and the semantically distinct case «divergence did not propagate beyond the perturbed target» is a candidate for a future verdict refinement (propagation-beyond-target as a separate flag).
- RESOLVED 2026-08-03 (Block 2.2.1, A1 — see Evidence log): editable ε search range from the window. Ratified during the PR #8 verification: the range is read-only by design (host builds settings via `DivergenceSettings.CreateDefault()` inside the runner; `TryStartTowerSearch` has no range parameter), which made an honest STABLE demo impossible on TowerScene — the default floor…ceiling always brackets at ~19.9 µm. A user with a slower scene will hit the same wall. Needs a range channel in `TryStartTowerSearch` or an override asset for `DivergenceSettings` — a search-entry signature change, deliberately out of Block 2.2's "do not touch search functionality" boundary.
- RESOLVED 2026-08-03 (Block 2.2.1, A4): Scene View legend re-anchored to the bottom-RIGHT of the viewport; marker labels moved to a GUI pass clamped into the viewport and skipped behind the camera. Verified with live before/after screenshots (`Library/BugCamEvidence/Block2.2.1-a4-ui/`) — see the Block 2.2.1 Evidence log entry. Cosmetic residual (pre-existing, recorded not fixed): the `lines=<count>` tail of the legend's markers row can truncate at the fixed label width.
- OPEN (backlog, Block 2.2-window-ux): Coverage row in the Ghost Visualization window — add only after camera-plan integration into `GhostEvidenceDocument`. `EvidenceCameras` is not wired into the ghost-evidence pipeline, so the window currently has no coverage verdict to show for either OK or LOW; per the ratified window spec the slot is NOT reserved and cameras are NOT wired in that block.
- OPEN (→ Block 2.3, deferred from Block 2.1): `RetroPlayer` (kinematic scrub/slow-mo playback) and the actual 2×2 viewport/RenderTexture compositing are **not started**. `EvidenceCameras.cs` computes and ranks the 4 camera positions but nothing renders them yet. Needs a live GPU Editor session to verify visually, same standard Block 1.5's screenshot capture was held to after the `#1F1F24` blank-PNG correction — do not claim this done from batchmode alone.
- SUPERSEDED 2026-08-03 (was: Block 2.1 approximations for contact proximity and the 100:10:1 weighted-sum ranking): both criteria were measured dead in the live calibration (contact constant across the single-radius candidate sphere; trajectory never influencing a winner) and were REMOVED from the algorithm rather than kept as decorative weights — see the "pre-PR7 blocker fixes" Evidence log entry. Cameras 2-4 now rank by orthogonality alone. Close-up/contact cameras (SPEC §7) remain backlog and additionally require multi-radius candidate generation.
- OPEN (Block 2.1 design): bodies are scored as world-axis-aligned AABBs (recorded position ± half of `SimulationBodyDefinition.Size`) at each queried frame — rotation is not applied to the bounding box. A documented simplification, not a silent one; revisit if a future scene relies on rotated bodies for occlusion accuracy.
- RESOLVED (Block 1.4 design): fan samples may exceed `EpsilonCeiling` up to `1.2 × EpsilonCeiling`. Magnitudes are **not** silently clamped; every fan sample above the search ceiling is marked `OutsideSearchRange=true`. Search range and characterization range are reported separately.
- RESOLVED 2026-08-03 (Block 2.2.1, A7 — human decision: keep the ignore, document explicitly): `.gitignore` keeps `*.csproj`; the premise of this item turned out stale — **no csproj exists anywhere in the worktree** (verified by full-tree search outside `Library/`), the Block 1.3 "headless test csproj" was never created, and headless runs go through `Tools/BugCam/run-checkpoint.ps1`. Documented in `CLAUDE.md` Testing; any future hand-authored csproj must be generated by a committed script, not force-added.
- RESOLVED 2026-08-03 (Block 2.2.1, A6): `CLAUDE.md` bitwise clause now names the sole exception explicitly — PLAN.md Block 1.4 VERIFY (a) bit-identical repeat is a determinism regression test, not a physics-claim gate (matching the 2026-07-29 reconciliation).
- RESOLVED (code path): local `PhysicsScene` via `SceneManager.CreateScene(..., LocalPhysicsMode.Physics3D)` requires Play Mode. Production harness now fails deterministically outside Play Mode; simulation tests live in `BugCam.Tests.PlayMode`. See Evidence log 2026-08-01.
- RESOLVED (Block 1.1 measurement): TowerScene dual-threading A/B/A′ numbers exist under `Library/BugCamEvidence/Block1.1/` (gitignored). `m_ThreadingMode` restored to `0` after the controlled experiment.

## Known issues (не блокируют PR #7)

Found 2026-08-03 during a static audit of every ratified `DivergenceSettings` field (commit `fa535e0`). Not fixed — recorded only, per explicit instruction not to touch code this pass.

1. **RESOLVED 2026-08-03 (Block 2.2.1, A3): the score is now the MAX per-body weighted norm with `SceneScoreThreshold=0.2`, re-ratified over measured tower distributions (see the A3 step-1/step-2 Evidence entries); the degeneracy below was confirmed live (sum 3.39 on zero-affected steps) before the change.** Original finding: **The weighted scene score is effectively degenerate on large scenes — the AND-gate's first half stops discriminating, not a false-positive source.** (Reformulated 2026-08-03 after review: the earlier claim of a sleep-flip / background-noise false positive was wrong — the AND-gate at `DivergenceEngine.cs:220-221` requires `stepAffectedBodies >= 1`, i.e. at least one body over `PerBodyPositionThreshold`, so neither a sleep mismatch nor sub-threshold noise alone can ever qualify a step.) The real problem: `DivergenceEngine.cs:188-192` accumulates `sceneScore` as a **sum over bodies**, never divided by `bodyCount`, so on a scene with many tracked bodies `sceneScore > SceneScoreThreshold=1` holds practically always (Block 1.1 measured ~1e-6 m/body noise; summed contributions over 49 bodies plus any real motion clear 1.0 trivially once anything diverges). The first half of the gate is therefore vacuous and the weighted score contributes nothing to the decision. Effective detector at defaults = "one body over 1 mm (`PerBodyPositionThreshold`), sustained 5 steps" — the position/rotation/velocity/sleep weight triad has no observable influence. Static read only; not yet demonstrated against live data.
2. **`SPEC.md`'s "sufficient share of the scene" gate has zero implementation — not narrowed-and-covered by PLAN, genuinely absent.** `SPEC.md:93`: "Divergence is significant only when it: exceeds threshold AND persists several physics steps AND affects an important body **or sufficient share of the scene**." `PLAN.md:24` and the implementation (`DivergenceEngine.cs:221`, `stepAffectedBodies >= 1`) both use a fixed count-based check with no weighting by importance and no fraction-of-`bodyCount` term. Per `CLAUDE.md`'s SPEC/PLAN precedence rule this is not a code bug (PLAN wins for v0.1), but the SPEC concept itself is unimplemented: 1 affected body out of 49 satisfies the gate identically to 20 or 49. Recommend an explicit backlog annotation in `SPEC.md` §5 (matching the existing `*` convention already used there) so this doesn't get rediscovered as a surprise. **Consequence (architectural debt, not cosmetics):** `SceneScoreThreshold=1` over an un-normalized per-body sum makes the threshold incomparable between scenes of different body counts — the same physical fragility scores ~10× higher on a 500-body scene than on a 50-body one. This blocks cross-scene fragility comparison (SPEC §12, historical dataset) until either the score is normalized by `bodyCount` or the threshold is made scene-size-aware; flagging it now so it is priced into any future scoring change rather than rediscovered when §12 work starts.
3. **RETRACTED as factually incorrect (2026-08-03): `EpsilonGrowthFactor` is NOT dead and there is no arithmetic mismatch.** The original suspicion (a `EpsilonGrowthFactor=2` vs `LadderPointCount=12` inconsistency) misread the search as one progression. It is two phases with two independent progressions: the ladder is built by log-uniform interpolation over `[EpsilonStart, EpsilonCeiling]` in `EpsilonSearch.cs:812 BuildLogUniformLadder` (step = `(ceiling/start)^(1/(count-1))`, never references `EpsilonGrowthFactor`), while `EpsilonGrowthFactor` is genuinely applied — iteratively, in the loop — in the Exponential-refinement phase only (`EpsilonSearch.cs:736-755 AdvanceExponentialCursor`: cursor `*=` / `/=` factor per probe, entered from `ClassifyLadderAndAdvance:580`). Both are internally consistent; the two progressions coincide only by accident when `ceiling/start = factor^(LadderPointCount-1)`. Residual (cosmetic only): `DivergenceSettings.cs`'s why-comments for the two fields both describe traversing the same span without naming their phases — a one-line clarification in each would prevent this misreading recurring. **Residual RESOLVED 2026-08-03 (Block 2.2.1, A6):** both why-comments now name their phases (`EpsilonGrowthFactor` = Exponential-refinement only; ladder = independent log-uniform interpolation) and state when the two progressions coincide.

## Evidence log

### 2026-08-04 — Block 2.2.2: exit gate — assembled third-party mesh scene end-to-end; FLOOR verdict accepted; block CLOSED on branch (`feat/block-2.2.2`)

**Ruling 7 amendment (ratified by the human 2026-08-04):** the «downloaded real scene» requirement is satisfied by a scene ASSEMBLED from downloaded third-party mesh assets — the research pass found no ready-made .unity scenes under clean licenses (sole verified candidate IronWarrior/ProjectileShooting fails on 2017-era serialized cookingOptions, an Animator, a PhysicMaterial and MonoBehaviours). **Source: KayKit Dungeon Remastered 1.0 — URL: https://github.com/KayKit-Game-Assets/KayKit-Dungeon-Remastered-1.0 — License: CC0** (license text committed as `Assets/BugCam/Tests/GateScene222Assets/KAYKIT-LICENSE.txt`). Asset placement adjudicated: the used subset (8 .obj + license + .meta) is COMMITTED — mesh resolve works by GUID from .meta, so a gitignored pack would make the gate scene an unresolvable evidence capsule on a fresh clone.

**Residual unknowns verified in the live editor BEFORE building** (`Library/BugCamEvidence/Block2.2.2-gate/asset-probe.txt`, all 8 meshes): prefab root/child scales all `(1,1,1)`, `globalScale=1`, `useFileScale=True` — lossyScale strictly positive, no shear; fresh `MeshCollider.cookingOptions` == the pinned default (`equalsPinnedDefault=True`); `sharedMaterial=null`. All eight meshes import with `isReadable=False` — the gate exercised exactly the Amendment 2026-08-04 path (edit-mode capture read the geometry of Read/Write-disabled meshes; contentHash computed for all).

**Gate scene (committed `6987b62`): `Assets/BugCam/Tests/GateScene222.unity`** — 4 non-convex mesh statics (floor_foundation_allsides pedestal with top at y=0 and NO implicit ground, wall, wall_arched, stairs) + 6 convex mesh dynamics (ColumnTrigger = stable ID 1, tilted 30° past its ~26.6° tipping angle — the domino trigger semantics; BarrelA/BarrelB stacked, Chest, two Coins on top). Intentionally unstable by explicit order. Capture pre-flight: `succeeded=True`, 6 bodies + 4 statics, ZERO fail-closed, ZERO excluded, hash `8b8e9a9430f244ded0f409f08cfa5a00bbb7a7442346fbed259a97790244b927`.

**VERIFIED FACT — gate run end-to-end** (host path, defaults, edit-mode capture → handover, run `ghost-20260804T074743614-body1-X-AscendFromStart`): `sceneCaptured=True`, `sceneCaptureHash` bit-identical to pre-flight, `sceneCaptureWarningCount=0`, manifest carries **meshRef on all 10 objects** (assetGuid/localFileId/meshName/contentHash/convex; both barrels and both coins share their per-asset contentHash pairwise — geometry-hash property visible in live data). Real divergence: first frame 17 (body 1), max spread `0.0156888068` m (body 6, coin, step 31), amplification `1568.88074`, 4/6 bodies, all 15 fans diverged. Verdict **DIVERGENT AT SEARCH FLOOR** (no bracket: the pile diverges at ε = search floor 10 µm). **Adjudication (human, 2026-08-04): FLOOR ACCEPTED as the final gate result** — the scene was ordered intentionally unstable, the verdict system reported honestly, and tuning the scene for a prettier verdict would contradict the A8 «deliberately NOT tuned» precedent and the product itself. The gate scene is frozen as committed.

**VERIFIED FACT — both pinned scenes bit-identical AFTER the asset/scene additions** (post-`6987b62` AssetDatabase): domino control `ghost-20260804T074908987…` — THRESHOLD BRACKET FOUND, threshold `0.0022972275`, capture hash `40e46640…b5b6531`, field diff vs the A8 pin EMPTY minus builtUtc/runId/runDirectory; tower control `ghost-20260804T074943556…` — THRESHOLD BRACKET FOUND, threshold `1.98919879E-05`, diff EMPTY minus builtUtc/runId/runDirectory + the documented `scenePath` environment-metadata class (pin recorded with an untitled scene; control ran with DominoScene open). No STOP fired.

**VERIFIED FACT — final batchmode gate on the code-final tip `6987b62`** (`run-checkpoint.ps1 -Suite All -EvidenceDir Library\BugCamEvidence\Block2.2.2-final`, exit 0, CHECKPOINT_PASSED; the docs commit that lands this entry is text-only and cannot affect the suites):

| Suite | total | passed | failed | result | XML |
|---|---|---|---|---|---|
| EditMode | 157 | 157 | 0 | Passed | `Library/BugCamEvidence/Block2.2.2-final/EditMode.xml` |
| PlayMode | 27 | 27 | 0 | Passed | `Library/BugCamEvidence/Block2.2.2-final/PlayMode.xml` |

Counters vs the 2.2.1 close: EditMode 134 → **157** (+23, all new `MeshCaptureTests`), PlayMode 24 → **27** (+3, all new `MeshSimulationPlayModeTests`); no existing test weakened or deleted. `TowerScene.unity` touched by the PlayMode run (pre-existing procedural regeneration) and reverted.

**Environment:** gate driven via mcp-for-unity menus from a TEMPORARY `Gate222Automation.cs` (never committed, deleted after; also probed assets and built/saved the scene deterministically). A stray empty `Assets/Scenes` folder from a failed `manage_scene create` call was removed before commit. Editor closed gracefully; no `ProjectSettings` drift; `TowerScene.unity`/`DominoScene.unity` untouched; the four protected files remain untracked.

### 2026-08-04 — Block 2.2.2: implementation landed; tower + domino gates bit-identical (`feat/block-2.2.2`)

**Implementation (four review-slice commits, verified as one tip):** `dc64ac2` core — `SimulationColliderShape.Mesh = 2`, `SimulationMeshReference` (asset ref + contentHash + structural fingerprint), additive 9-arg/6-arg definition ctors with `FullScale`, `Core/SceneMeshResolve.cs` (provider interface + static seam + `SCENE_MESH_RESOLVE_FAILED` code + provider-missing literal), `SceneCapture` MeshCollider branch (all ratified fail-closed rows verbatim; Size = world AABB per ruling 6; `DefaultMeshCookingOptions` const; contentHash over vertices + all-submesh triangles, `"R"` invariant; ratified `|mesh:<guid>:<fileId>:<hash>:<convex>` hash tail — Box/Sphere lines byte-identical), `SimulationHarness` Mesh branch (resolve via provider; bit-identical fingerprint check per Amendment P.2 — no tolerance; `MeshResolveException` → structured failure prefixed `SCENE_MESH_RESOLVE_FAILED`), `Editor/SceneMeshResolveProviderInstaller.cs` (AssetDatabase impl, `[InitializeOnLoadMethod]`). `d72f88b` evidence — `meshRef {assetGuid, localFileId, meshName, contentHash, convex}` on mesh-shaped `objects[]` records only; `GhostEvidenceErrorCodes.MeshResolveFailed`; `ResolveSearchErrorCode` mapping. `f8bb4ca` editor — Amendment P.6 run-path invariant: authoritative capture moved into `StartTowerSearch` (edit mode, before `isPlaying` flip), `Editor/SceneCaptureHandover.cs` (JsonUtility DTO handover across the domain reload; float bit-exactness pinned by test), runner consumes the handover and never re-captures in Play Mode, missing handover for a scene-kind run ⇒ `SCENE_MESH_RESOLVE_FAILED` failure document; window capture report gains mesh-reference rows. `7be8601` tests — 23 new EditMode (`MeshCaptureTests`: both extensions, every new fail-closed row verbatim, capsule/updated-tail literal, Box/Sphere + mesh hash byte-format pins, P.7 pair, ruling-10 submesh pin, ruling-6 objectScale pin, cooking-default pin, manifest meshRef presence/absence, error-code mapping, handover bit-identity; the unreadable-geometry row is pinned at source level because edit mode ALWAYS reads — measured, `UploadMeshData(true)` keeps `isReadable=true` in edit mode) + 3 new PlayMode (`MeshSimulationPlayModeTests` over built-in Cube.fbx through the real provider: convex body rests on non-convex static; tampered fingerprint ⇒ `SCENE_MESH_RESOLVE_FAILED` + verbatim mismatch reason; default mesh reference ⇒ invariant failure).

**VERIFIED FACT — batchmode Unity `6000.3.21f1`** (`run-checkpoint.ps1 -Suite All -EvidenceDir Library\BugCamEvidence\Block2.2.2-impl`, exit 0, CHECKPOINT_PASSED):

| Suite | total | passed | failed | result | XML |
|---|---|---|---|---|---|
| EditMode | 157 | 157 | 0 | Passed | `Library/BugCamEvidence/Block2.2.2-impl/EditMode.xml` |
| PlayMode | 27 | 27 | 0 | Passed | `Library/BugCamEvidence/Block2.2.2-impl/PlayMode.xml` |

Counters grew only by new tests (134→157, 24→27); no existing test weakened or deleted (`StaticMeshColliderFailsClosed` still passes unchanged — its fixture has no `sharedMesh`, which now hits the ratified «MeshCollider без sharedMesh» row whose reason still contains "MeshCollider").

**VERIFIED FACT — tower gate bit-identical (host path, defaults, live editor via mcp-for-unity):** run `ghost-20260804T073138215-body49-X-AscendFromStart`, verdict THRESHOLD BRACKET FOUND, `thresholdEstimateMetres=1.98919879E-05`; field-by-field diff vs the A3 pin `ghost-20260803T161330582…`: metrics.json differs ONLY in builtUtc/runId, manifest.json ONLY in runDirectory/runId — zero unexpected diffs. The run exercised the NEW handover-aware host path (Tower kind → handover cleared).

**VERIFIED FACT — domino gate bit-identical (captured-scene path через edit-mode-захват + handover):** run `ghost-20260804T073253121-body1-X-AscendFromStart` (DominoScene open, scene-kind entry, target body 1, defaults), verdict THRESHOLD BRACKET FOUND, `thresholdEstimateMetres=0.0022972275`, `sceneCaptured=True`, `sceneCaptureWarningCount=0`, **`sceneCaptureHash=40e466400a6fd4804a924e0376013782d40d02ffb19b14912d15d0d2bd5b6531` bit-identical to the pin**; field-by-field diff vs the A8 pin `ghost-20260803T174400539…`: metrics.json ONLY builtUtc/runId, manifest.json ONLY runDirectory/runId. The identical manifest also byte-confirms meshRef additivity on a real pinned no-mesh scene, and the bit-identical hash proves the capture moved to the edit-mode point (Amendment P.6) without shifting a single captured float.

**Environment:** live editor driven via mcp-for-unity (server package 10.1.0, port 8080; this toolset has NO `execute_code` — gate runs were driven through a TEMPORARY `Gate222Automation.cs` menu script, never committed, deleted after the gate; it also lifted unfocused-editor throttling via `InteractionMode=1`). One incident: the first editor instance exited cleanly mid-session before the tower run (no crash stack in Editor.log; cause not established — possibly closed manually); relaunched and completed both gates. Editor closed gracefully after the gate; worktree verified clean (no `ProjectSettings` drift, `TowerScene.unity`/`DominoScene.unity` untouched, four protected files untracked). **Block exit gate item 1 (downloaded scene) remains PENDING the human's scene choice (ruling №7)** — the block is NOT closed by this entry.

### 2026-08-04 — Block 2.2.2: empirical readability precondition (`feat/block-2.2.2`)

**Mandated by `docs/CONTRACT-2.2.2.md` («Эмпирическая предпосылка до кода») before any implementation code.** Method: three batchmode runs of Unity **`6000.3.21f1`** — the project's actual pinned editor (`ProjectSettings/ProjectVersion.txt`; PLAN pins «6000.3.20f1 or newer within 6.3», satisfied) — over a temporary two-material OBJ probe mesh (never committed, deleted after measurement; probe artifacts under gitignored `Library/BugCamEvidence/Block2.2.2-readability/`: `editmode-report.txt`, `playmode-report.txt`, `playmode-editor-channel-report.txt`, unity logs, test XMLs). Run 1: edit mode via `-executeMethod` (exit 0). Run 2: Play Mode via a temporary filtered PlayMode test, scene/Resources channel (1/1 Passed). Run 3: Play Mode, editor `AssetDatabase` channel reached by reflection, side-by-side with Resources channel in the same run (1/1 Passed).

**VERIFIED FACT — importer default:** a freshly imported OBJ reports `ModelImporter.isReadable=False` — Read/Write is DISABLED by default, so the «typical third-party scene» assumption of the contract is the Unity default, not an edge case.

**VERIFIED FACT — measurement (identical mesh asset, `isReadable=False`, vertexCount 8, subMeshCount 1):**

| Замер | Edit mode (захват) | Play Mode, Resources/scene channel | Play Mode, `AssetDatabase` channel |
|---|---|---|---|
| `mesh.isReadable` | False | False | False |
| `vertexCount` | 8 | 8 | 8 |
| `mesh.bounds` | — | Center (-0.5, 0.5, 0.5), Extents (0.5, 0.5, 0.5) | identical |
| `mesh.vertices` | **OK, length=8** | length=0 (silently empty) | length=0 |
| `GetTriangles(0)` | **OK, length=12** | length=0 | length=0 |
| `AcquireReadOnlyMeshData` | OK | InvalidOperationException «Not allowed to access vertex data … (isReadable is false…)» | not re-probed (same instance) |
| contentHash | **computed** `2bd713a3…510d0` | NOT computable | NOT computable |
| MeshCollider non-convex static | — | **OK**, bounds (1,1,1) | — |
| MeshCollider convex + Rigidbody | — | **OK**, bounds (1,1,1) | — |

Run 3 also pinned `sameInstanceAcrossChannels=True`: `AssetDatabase.LoadAllAssetsAtPath` returns the SAME `Mesh` instance as `Resources.LoadAll`, so readability is a property of the mesh object, not of the load channel — no editor-side read path exists in Play Mode without touching import settings.

**Consequence (Amendment 2026-08-04, ratified by the human, recorded in `docs/CONTRACT-2.2.2.md` «Поправка 2026-08-04»):** authoritative geometry capture + full contentHash move to an edit-mode point before Play Mode entry (result handed to the runner); the simulation point verifies a structural fingerprint (vertexCount, subMeshCount, `mesh.bounds` — all measured available in Play Mode) plus null/unresolved check; `SCENE_MESH_RESOLVE_FAILED` fires on unresolved mesh or fingerprint mismatch; the evidence capsule states honestly that the full hash was verified at capture and is not re-verifiable in Play Mode. Affected ratified verbatim literals / failure-table rows are not rewritten pending the human's approval of exact wordings (ruling №9).

**No pins touched:** no epsilon search was run in any of the three probes (filtered single-test runs + one `-executeMethod` run); tower/domino gate numbers were not exercised and cannot have drifted. `TowerScene.unity` untouched (verified via `git status` after each run); no `ProjectSettings` drift; the four protected files remain untracked.

### 2026-08-03 — Block 2.2.1: A8 — exit gate: domino scene through the captured-scene path (`feat/block-2.2.1`)

**Gate intent (human-adjudicated before execution):** the domino scene must exercise the A2 captured-scene path end to end (real scene objects → `SceneCapture` → search → verdict → evidence bundle with a `sceneCapture` section), NOT the procedural tower factory. This supersedes the literal «второй процедурной сцене» wording in `CONTRACT-2.2.1.md` A8 / `PLAN.md` Block 2.2.1 — recorded, not silent.

**Scene (new, committed): `Assets/BugCam/Tests/DominoScene.unity`.** Contract leaves composition open ⇒ minimal, rationale recorded: static ground box 2×0.2×1 m (top at y=0, no Rigidbody) + 5 dynamic dominoes 0.02×0.1×0.06 m (unit BoxCollider, size via localScale — the tower convention), mass 1, all other Rigidbody fields Unity defaults, pitch 0.06 m along X; «Domino 1» is the trigger, tilted 18° toward +X (past the 11.3° tipping angle, short of the 23.6° neighbour-contact angle), resting on its bottom edge; «Domino 2..5» upright. Rationale: creation order gives the trigger stableId 1 and the window's scene-mode default target is options[0] ⇒ the default search targets the trigger; the tilt gives the captured scene real dynamics under the documented A2 semantic (capture takes the scene as-is, velocities zero). Pre-flight capture (edit mode, before the search): `succeeded=True, bodies=5, statics=1, kinematicFreezeWarnings=0, sleepingBodyWarnings=0`, stable IDs 1–5 = Domino 1–5, hash `40e466400a6fd4804a924e0376013782d40d02ffb19b14912d15d0d2bd5b6531`.

**VERIFIED FACT — end-to-end captured-scene search (live editor via mcp-for-unity, host path `TryStartTowerSearch(entry, "window")`, defaults: 32 steps, axis X, AscendFromStart, target body 1, SceneKind=CapturedScene):** run `ghost-20260803T174400539-body1-X-AscendFromStart`, verdict **THRESHOLD BRACKET FOUND**; console report carries `sceneCaptured=True`, `sceneCaptureHash` bit-identical to the pre-flight hash, `sceneCaptureWarningCount=0`, `scenePath=Assets/BugCam/Tests/DominoScene.unity`; manifest carries the `sceneCapture` section (5 bodies, 1 static, bodyMap, warnings `[]`). Numbers: threshold estimate `0.0022972275` m (2.30 mm), bracket `0.00228482112…0.0022972275` m (width 12.4 µm), 43 physical probes, 15 fans; primary fan 6: first frame 3 (body 1), max spread `0.00229723` m (body 1, step 1), 1/5 bodies, amplification `1.00000107`. Host auto-exited Play Mode. Verdict-STOP conditions (STABLE / DIVERGENT AT SEARCH FLOOR) did not fire.

**VERIFIED FACT — binding A3 re-measurement executed on domino data** (instrumented pass over the in-memory retained runs; script + raw data in `Library/BugCamEvidence/Block2.2.1-a8-measure/`: instrumentation-script.cs, per-step.csv — 480 rows, summary.txt with ALL disagreement rows; body scales taken from `document.SceneCapture.Bodies` — exactly the array the host feeds `DivergenceEngine`, all 0.1 m):

| | tower (A3 step 1, 480 steps) | domino (A8, 480 steps) |
|---|---|---|
| agreement (maxNorm>0.2) vs (affected≥1) | **477/480 (99.4%)** | **202/480 (42.1%)** |
| score-fires / position-holds | 3 | 103 |
| **position-fires / score-holds (OPPOSITE)** | **0** | **175** |

Disagreement structure (fully enumerated in summary.txt): OPPOSITE = all five Z-axis fans (2/5/8/11/14) on all 32 steps each — 160 rows (mechanism: the target's ε offset along Z, 1.84–2.76 mm > the absolute 1 mm threshold, never decays — the domino topples in the X-Y plane identically in both runs, so affected=1 forever while maxNorm stays 0.018–0.119) — plus X-axis fans 0/3/6/9/12 on steps 0–2 — 15 rows (before the trigger's rotation transient develops). score-fires/pos-holds = X fans 0/3/6 steps ~5–31 (rotation-angle difference of the toppling trigger at sub-mm position difference: 27+26+24 rows), Y fans 1/4/7/10/13 steps 0–4 (landing transient of the ε-lifted trigger: 4–5 rows each), X fan 9 steps 8–9 (2 rows).

**Adjudication (human, 2026-08-03, three rulings over the measured data):**
1. **The score-half of the AND-gate is PERMANENTLY KEPT; the deletion question is CLOSED for good** (not deferred). The A3 closure condition demanded zero opposite-direction cases on both scenes; domino produced 175, and the mechanism is structural, not statistical: the positional half fires forever on the never-decaying echo of the target's own ε offset, and only the scale-relative score-half distinguishes «I moved an object» from «the scene diverged» — exactly the arbitrary-scale scenario for which the half was retained in the A3 adjudication. Closed without right of reopening on this contract.
2. **Product finding (backlog; no code changes in this block):** the found bracket describes the perturbation-detection threshold on the target itself, not chain divergence (amplification 1.00000107, affectedBodyIds=[1], spread = ε). «Divergence did not propagate beyond the perturbed target» is semantically distinct and a candidate for a future verdict refinement. Recorded in Open findings.
3. **Third confirmation of the tight-window finding:** the domino chain physically cannot develop within 32 steps (0.64 s for a 5-domino chain) — recorded in Open findings alongside the two tower entries. Scene deliberately NOT tuned: the gate's goal is the pipeline on a second scene through capture, which passed end-to-end; spectacle belongs to the hero demo (Block 2.5) with a long window.

**VERIFIED FACT — tower gate control after the A8 work (defaults, host path):** run `ghost-20260803T175705762-body49-X-AscendFromStart` — field-by-field diff vs the A3 pin `ghost-20260803T161330582…`: metrics.json differs ONLY in runId/builtUtc/scenePath (the pin was recorded with an untitled scene — documented environment-metadata exception), manifest.json differs ONLY in runId/runDirectory; zero unexpected diffs. All ten pinned numbers reproduced exactly: threshold `1.98919879E-05`, bracket `1.978456E-05…1.98919879E-05`, width `1.07427695E-07`, кадр 27 (body 49), spread `0.003794474` (body 5), 21/49, amplification `190.753891`. **No STOP fired.**

**VERIFIED FACT — batchmode Unity `6000.3.21f1`** (`run-checkpoint.ps1 -Suite All -EvidenceDir Library\BugCamEvidence\Block2.2.1-a8`, exit 0):

| Suite | total | passed | failed | result | XML |
|---|---|---|---|---|---|
| EditMode | 134 | 134 | 0 | Passed | `Library/BugCamEvidence/Block2.2.1-a8/EditMode.xml` |
| PlayMode | 24 | 24 | 0 | Passed | `Library/BugCamEvidence/Block2.2.1-a8/PlayMode.xml` |

Counts identical to the A5 baseline — A8 adds no test surface (gate + measurement + docs + the committed scene only; Core untouched).

**Environment:** editor driven via mcp-for-unity direct HTTP; `enterPlayModeOptions=None` throughout (no drift); editor exited gracefully before batchmode; `TowerScene.unity` touched twice (fileID regeneration during the live session; by the batchmode PlayMode run) and reverted both times; the four protected files remain untracked; `DominoScene.unity` + `.meta` become tracked in this commit. PENDING (unchanged, do not block): live window screenshots of scene-mode states.

### 2026-08-03 — Block 2.2.1: A5 — bit-identical identical-config search-repeat pin (`feat/block-2.2.1`)

**Closes `PLAN.md` Block 1.4 VERIFY (a)** (open since 2026-08-02; previously only a baseline-only ≤1e-6 repeat existed). Two layers of evidence:

**1. Test pin (PlayMode, permanent):** `SearchRepeatPlayModeTests.IdenticalConfigFullSearchRepeatsBitIdentically` — two COMPLETE default searches (default `EpsilonSearchSettings`, tower factory bodies, target 49, axis X, AscendFromStart, step 32) run back-to-back in one Play Mode session; asserts raw-bit equality (Int32 bit patterns, not float tolerance) of verdict-defining floats (threshold estimate, both bracket bounds, bracket width, reference ε) AND element-wise bit equality of the retained state frames for the baseline and every retained fan run (16 runs × 32 steps × 49 bodies × 14 floats each side). Per the CLAUDE.md bitwise clause this is a determinism regression test, not a physics-claim gate. **VERIFIED FACT — live PlayMode 24/24** (+1).

**2. Host-path repeat (live editor, the exact criterion from the adjudicated instruction):** two back-to-back window-host searches with defaults, `ghost-20260803T172236743…` and `ghost-20260803T172304173…` — **field-by-field diff of metrics.json AND manifest.json between the repeat runs is EMPTY except runId/builtUtc** (runDirectory contains the runId and was stripped with it; scenePath identical between the two). The pinned reference numbers reproduced bit-identically in both: `thresholdEstimateMetres:1.98919879E-05`, bracket `1.978456E-05 … 1.98919879E-05`, width `1.07427695E-07`, кадр 27 (body 49), spread `0.003794474` (3.79 mm, body 5), 21/49, amplification `190.753891` (191×). RUN2 vs the A3 pin also diffs empty minus runId/builtUtc/scenePath-metadata. **No STOP condition fired.**

**VERIFIED FACT — batchmode Unity `6000.3.21f1`** (`run-checkpoint.ps1 -Suite All -EvidenceDir Library\BugCamEvidence\Block2.2.1-a5`, exit 0):

| Suite | total | passed | failed | result | XML |
|---|---|---|---|---|---|
| EditMode | 134 | 134 | 0 | Passed | `Library/BugCamEvidence/Block2.2.1-a5/EditMode.xml` |
| PlayMode | 24 | 24 | 0 | Passed | `Library/BugCamEvidence/Block2.2.1-a5/PlayMode.xml` |

### 2026-08-03 — Block 2.2.1: A2 follow-up — sleeping-body capture notice (`feat/block-2.2.1`)

**Adjudication (human, A2 review):** sleeping bodies were the only unsupported-behavior case that changed semantics silently — a body asleep at capture time starts awake in the simulation, so the captured scene can behave differently from the live one, asymmetric with the kinematic-freeze precedent. Ratified handling: **notice only** — a warning per sleeping body reaches the **capture report and the manifest** (`sceneCapture.sleepingBodyWarnings`, same channel pattern as `kinematicFreezeWarnings`); the **verdict is untouched and the body stays dynamic** (unlike the kinematic freeze this is not a structural scene change). The A2 review also ratified all the fail-closed silent-contract decisions as recorded, and confirmed the truncation suspicion was unfounded — the STOP list had exactly the three conditions received.

**Implementation:** `SceneCapture` gains the `SleepingBodyWarnings` channel (populated from `Rigidbody.IsSleeping()` at capture time; the object record carries a «спит на момент захвата» note, which also folds the sleep state into the capture hash); manifest writer emits `sleepingBodyWarnings`; window capture report renders the notice rows; console report and summary verdict areas deliberately unchanged. **Empirical precondition verified in the live editor before relying on it:** in an edit-mode preview scene a fresh Rigidbody reports `IsSleeping()=False`, `Sleep()` sticks, `WakeUp()` clears (`default=False afterSleep=True afterWake=False`) — so no false-positive spam is possible.

**VERIFIED FACT — live EditMode 134/134** (+1: `SleepingBodyEmitsNoticeToCaptureChannelAndManifestButNotVerdict` — warning reaches the capture result and the written manifest, console report contains neither the channel nor the body name; awake-control assert added to the clean-scene test). **VERIFIED FACT — tower gate untouched:** fresh control search with defaults (`ghost-20260803T171935278…`) — metrics.json AND manifest.json field-by-field identical to the A3 pin (minus runId/builtUtc and the scenePath environment metadata), and the tower manifest contains **zero** `sceneCapture`/`sleeping` keys (grep count 0).

### 2026-08-03 — Block 2.2.1: A2 — arbitrary-scene capture (`feat/block-2.2.1`)

**Scope (per docs/CONTRACT-2.2.1.md A2, the authoritative text):** `Core/SceneCapture.cs` — capture of the open scene with the three contracted outcomes (captured / excluded-safely for contactless bodies / fail-closed with per-object reasons); Box + Sphere primitive colliders (**sphere `Size` = diameter**, PhysX max-|lossyScale|-component rule); kinematic bodies frozen to static, kinematic-with-Animator warning propagated to **window AND result verdict AND manifest/evidence**; deterministic stable IDs (**hierarchy path + sibling index** order key, ids 1…N by ordinal sort); id↔name map + SHA-256 capture hash in the manifest `sceneCapture` section; capture report in the window. Search over a captured scene runs through the SAME host/entry pipeline: `GhostSearchEntry` gains `SceneKind` (Tower | CapturedScene), the window gains a «Сцена» selector, the runner captures authoritatively at run start (fail-closed → `SCENE_CAPTURE_FAILED` failure bundle carrying the capture record), captured statics replace the legacy tower ground via `EpsilonSearchRunner.StaticColliders`.

**Implementation surface:** Core — `SceneCapture.cs` (new), `SimulationHarness.cs` (`SimulationColliderShape`, `SimulationStaticColliderDefinition`, 4-arg `SimulationRequest`, Sphere branch in `CreateBody`, statics creation; `StaticColliders == null` ⇒ legacy procedural ground, non-null ⇒ exactly the captured statics and no implicit ground — empty means none), `EpsilonSearchRunner.cs` (statics property — `Run` keeps its pinned single 5-parameter reflection signature). Evidence — document/builder/writer/report/schema: trailing `sceneCapture` parameter (default = not performed ⇒ tower manifests and reports byte-unchanged), `sceneCapture` manifest section (counts, hash, `bodyMap` with `orderKey` — display paths repeat for the 48 identically-named bricks, the order key is the unique identity, warnings, per-object records), console report `sceneCaptured=`/`sceneCaptureWarning[i]=` lines directly after `verdict=`, summary.md warning + capture rows next to the verdict. Editor — `GhostSearchEntry.cs` (kind, 10-arg ctor chaining, `SceneOptions` display-name provider, kind-aware fail-closed target validation that runs a capture in Resolve), host (SceneKind persistence, scene branch in the runner coroutine), window (scene selector, capture report block, warnings as HelpBox in the result verdict block, honest body count for scene runs, capture row in result, target re-defaulting on mode switch).

**Decisions where the contract is silent (all fail-closed, recorded not silent):** compound colliders (collider on a child of a Rigidbody, or >1 contact collider on one body) ⇒ fail; `PhysicMaterial` ⇒ fail (friction/bounce not reproducible); non-default damping / `useGravity=false` / constraints ≠ None / non-Discrete collision detection / manual COM or inertia tensor ⇒ fail; `ArticulationBody` ⇒ fail; `BoxCollider.center` offsets supported exactly (body recorded at the collider's world centre via `TransformPoint`); shear detected by `localToWorldMatrix` vs `TRS(pos, rot, lossyScale)` mismatch ⇒ fail; trigger-only / collider-less / inactive objects ⇒ excluded-safely; initial sleep state not reproduced (bodies recreated awake identically in baseline and perturbed runs). Reflection-pin-preserving API rules: existing ctor/method signatures kept verbatim, new overloads/trailing optionals only; `Build`/`CreateFailureDocument` stayed single methods (typeless `GetMethod` pins) — their three test-helper reflection call sites updated to pass the new argument (helpers only, zero assertion changes, matching the ratified A1 precedent). «Цель» tooltip updated to describe both modes.

**VERIFIED FACT — live editor (mcp-for-unity, direct HTTP):** EditMode **133/133** (116 existing + 17 new `SceneCaptureTests`: three outcomes, determinism/hash, freeze+Animator warning, fail-closed shapes/joints/compound/shear/damping/no-dynamics, sphere-diameter, center-offset, manifest section present for capture and absent for tower), PlayMode **23/23** (21 + 2 `SceneCapturePlayModeTests`: sphere rests on a captured static ground; empty statics array suppresses the legacy ground — free fall).

**VERIFIED FACT — A2 gate, control tower search with defaults TWICE (host path):** run `ghost-20260803T165642024…` — metrics.json AND manifest.json field-by-field identical to the pinned A3 control (`ghost-20260803T161330582…`) minus runId/builtUtc/runDirectory (diff EMPTY); run `ghost-20260803T170829166…` (after the EnterPlayModeOptions revert below, normal domain reload, TowerScene open in the editor) — identical except environment metadata `scenePath` (pin was recorded with an untitled scene); all pinned numbers bit-identical both times: `thresholdEstimateMetres:1.98919879E-05`, `largestStable:1.978456E-05`, `smallestDivergent:1.98919879E-05`, `bracketWidth:1.07427695E-07`, кадр 27 body 49, spread 0.003794474 body 5, 21/49, amplification 190.753891. Tower console report carries zero new lines. **No STOP condition fired.**

**VERIFIED FACT — scene-kind search end-to-end (live editor):** capture of the real `TowerScene.unity` → 49 bodies + 1 static, hash `ad33a6c816f11af3eeada33024cf88a8d4ce8d9247f67a059d7eaaa130dd5ff4`, projectile resolved by real hierarchy name to body 49; full search run `ghost-20260803T165827019…` produced an evidence bundle whose manifest carries the `sceneCapture` section (bodyMap 49 rows, warnings `[]`), console report shows `sceneCaptured=True` directly under the verdict, summary.md carries the capture row. Verdict honestly **STABLE WITHIN TESTED RANGE**: the captured projectile has zero initial velocity — the 12 m/s launch impulse exists only in the procedural factory, and a tower at rest is stable within 32 steps for ε ≤ 10 mm. Recorded as a documented capture semantic (capture takes the scene as-is), not a defect.

**Environment incident (recorded):** the worktree carried an uncommitted `ProjectSettings/EditorSettings.asset` drift `m_EnterPlayModeOptions: 0 → 1` (domain reload disabled; origin not established — discovered mid-A2 after MCP-driven test runs; it also explains ~10 s search times). Both searches executed under it matched the pins bit-identically anyway; the setting was restored to `None` from inside the editor, the control search repeated under normal domain reload (run `…170829166`, identical numbers), and the file left clean — nothing committed. `TowerScene.unity` was touched by the PlayMode run and again by the editor exit; reverted both times. The four protected files remain untracked.

**VERIFIED FACT — batchmode Unity `6000.3.21f1`** (`run-checkpoint.ps1 -Suite All -EvidenceDir Library\BugCamEvidence\Block2.2.1-a2`, exit 0):

| Suite | total | passed | failed | result | XML |
|---|---|---|---|---|---|
| EditMode | 133 | 133 | 0 | Passed | `Library/BugCamEvidence/Block2.2.1-a2/EditMode.xml` |
| PlayMode | 23 | 23 | 0 | Passed | `Library/BugCamEvidence/Block2.2.1-a2/PlayMode.xml` |

**PENDING (live UI, next focused-editor session):** window screenshots of the scene-mode states (capture report in «Настройка», fail-closed reasons under the disabled button, Animator warning HelpBox in the result) — same ReadScreenPixel flow as A1/A4; the window logic itself is pinned by the state-machine tests and the source-scan pins (`WindowNeverEntersPlayMode…` etc.), which all pass.

### 2026-08-03 — Block 2.2.1: A3 adjudication closed — score-half of the AND-gate KEPT (`feat/block-2.2.1`)

**Adjudication (human, over the step-1/step-2 measured data): the score-half of the AND-gate is RETAINED — do not delete.** Rationale as ratified:

1. It is the **only scale-relative criterion in the gate** — the positional half is an absolute 1 mm threshold (`PerBodyPositionThreshold`), while the max per-body weighted norm divides by object scale.
2. The sample is a **single scene**: 480 tower steps prove nothing about arbitrary scenes.
3. The direction that would actually flip verdicts (**affected ≥ 1 AND score ≤ 0.2**) has **zero observations** in the measured data.
4. The next task, A2, enters **arbitrary-scale scene territory**, exactly where the absolute 1 mm threshold is weakest.

**Measured agreement backing the decision (tower, 480 steps):** (maxNorm > 0.2) vs (affected ≥ 1) agree on **477/480** steps; all 3 disagreements are **score-fires / position-holds transients with affected = 0** (fan 0 steps 27/28: 0.4631/0.4722; fan 3 step 28: 0.5366); **zero cases in the opposite direction** (position-fires / score-holds).

**Re-measurement obligation (binding):** on **A8 (domino scene)** compute the same agreement numbers (agreement count, per-direction disagreement breakdown) and record them here. Deletion of the score-half may only be raised as a **separate adjudication at block closure**, and only if **both scenes show zero opposite-direction cases**.

Also this pass: the `DefaultSceneScoreThreshold` why-comment in `DivergenceSettings.cs` now cites its evidence source explicitly — measurement commit `e1c1cfc` (it already carried noise p99 0.0202, divergence floor 0.311, and the 10×-over-noise-p99 ratification). Comment-only change, no behavior change.

### 2026-08-03 — Block 2.2.1: A3 steps 2+3 — max formula applied, threshold 0.2, double control bit-identical (`feat/block-2.2.1`)

**Adjudication (human, over the step-1 data):** threshold **0.2** ratified (10× above noise p99 0.0202, below divergence floor 0.311; the lone 0.537 noise transient treated as adjacent-to-divergence). Pre-lock verification on the 480 measured steps: **zero noise steps pass the FULL gate** (score>0.2 AND per-body>1 mm AND sustained 5) — the 3 noise steps with maxNorm>0.2 (fan 0 steps 27/28: 0.4631/0.4722; fan 3 step 28: 0.5366) all have affectedBodies=0 and are contained by the AND. Under the new gate, sustained-5 windows exist exactly in fans 6/9/12 with firstFrame=27 — identical to the shipped verdicts. **Decision-coincidence for the score half: (maxNorm>0.2) vs (affected≥1) disagree on 3 of 480 steps (99.4% agreement)** — the same 3 transient rows above, where the score half fires and the position half does not. Score-half deletion NOT performed — separate adjudication per contract.

**Step 2 (code):** `DivergenceEngine` per-step score sum→**max** per-body weighted norm (loop change only, no allocations); `DefaultSceneScoreThreshold` 1→**0.2** with measurement-derived why-comment; header/tooltip comments updated; PLAN Block 1.3 line updated; SPEC §5 sum sentence annotated as superseded for v0.1. New pins: `SceneScoreIsMaxPerBodyNormNotSum` (2×0.5 m fixture: sum would be 1.0, max is 0.5) and `SceneScoreThreshold==0.2` added to the settings-defaults pin. Step-count audit (adjudication item 3): **verified — every pinned regression test already passes its step count explicitly** (VerifyStepCount=32 / FastStepCount=40 / checkpoint 250 constants); no test change needed.

**VERIFIED FACT — batchmode** (`Block2.2.1-a3-step2`, exit 0): EditMode **116/116**, PlayMode **21/21** — PlayMode search/determinism suites passed with the new formula without edits.

**VERIFIED FACT — step 3, control search with defaults TWICE** (`ghost-20260803T161254154…`, `ghost-20260803T161330582…`): window rows identical to the gate on both runs (19.9 µm / 19.8…19.9 µm / 0.107 µm / кадр 27 body 49 / 3.79 mm body 5 / 191× / 21 из 49); full-precision metrics **bit-identical between the two runs and to the pin** — `thresholdEstimateMetres:1.98919879E-05`, `largestStable:1.978456E-05`, `smallestDivergent:1.98919879E-05`, `bracketWidth:1.07427695E-07`; field-by-field diff of both metrics.json (minus runId/builtUtc) is empty. No drift ⇒ no STOP condition fired.

### 2026-08-03 — Block 2.2.1: A3 step 1 — instrumented max-per-body-norm measurement (measurement only, gate formula UNCHANGED, awaiting adjudication)

**Method:** editor-side instrumented pass (script copy: `Library/BugCamEvidence/Block2.2.1-a3-measure/instrumentation-script.cs`) over the in-memory retained runs of a fresh defaults control search `ghost-20260803T155941732-body49-X-AscendFromStart` (verdict/window numbers identical to the gate). Per step × per body it replicates `DivergenceEngine.cs:180-192` verbatim (posNorm=posErr/objectScale, rotNorm=rotDeg/1°, velNorm=velErr/0.05, sleep 0|1, weights 1/0.5/0.5/1; tower scales all 1 m) and records per-step **max_i bodyNorm_i** (proposed) vs **sum_i** (current) plus `affectedBodies` (per-body 1 mm gate). Classification: step with affectedBodies==0 → noise class, ≥1 → divergence class. Raw data: `per-step.csv` (480 steps), `summary.txt`.

**Measured distributions (defaults, 15 fans × 32 steps):**

| class | metric | n | min | p50 | p90 | p99 | max |
|---|---|---|---|---|---|---|---|
| noise (affected==0) | maxNorm | 458 | 1.57e-5 | 0.0198 | 0.0198 | 0.0202 | **0.5366** |
| noise | sumNorm | 458 | 1.57e-5 | 0.0594 | 0.1385 | 0.1781 | **3.3866** |
| divergence (affected≥1) | maxNorm | 22 | **0.3106** | 0.6452 | 3.9921 | 4.3078 | 4.3078 |
| divergence | sumNorm | 22 | 2.5722 | 4.0263 | 38.364 | 46.960 | 46.960 |

**Findings for adjudication (no code changed):**
1. **Sum degeneracy confirmed on live data** (matches the 2026-08-03 static audit): sum-norm reaches **3.39 on steps with zero affected bodies** — the current score half fires above `SceneScoreThreshold=1` on pure sub-threshold background, the AND-gate's second half is the only thing containing it.
2. **Max-norm classes OVERLAP around 1.0:** noise max 0.537 vs divergence min 0.311 — with `max` + threshold 1.0 roughly half of affected steps (p50 = 0.645) would FAIL the score half, i.e. the re-ratified threshold cannot stay at 1 without changing which steps qualify; candidate range for separation lies below ~0.3 (with the transient-burst caveat below) — a number to ratify over this data, not to pick silently.
3. **Sub-threshold transient bursts recorded:** X-axis fans at 0.8×/0.9× (eps 15.9/17.9 µm) transiently reach **22 affected bodies** (peakMaxNorm 2.78 / 1.35) yet stay non-significant (not sustained 5 steps within the 32-step window) — near-threshold tower dynamics are marginal, and the 32-step window cuts sustained checks close (first frame 27 + 5 = 32 exactly).
4. Y/Z-axis fans stay fully quiet (peakAffected=0) — the tower discriminates axes cleanly at these ε.

### 2026-08-03 — Block 2.2.1: A1 review fix — shortest round-trip mm display (`feat/block-2.2.1`)

**Review decision (human):** A1 accepted, deviations 1–3 approved; deviation 4 (full-"R" mm display of window overrides) REJECTED — the input field must show the shortest plain round-trip representation of the stored value ("0.0001", never "0.000100000005"); storage/manifest/evidence keep full precision. Display-only fix in `GhostSearchEntryResolver.MillimetresTextFromMetres` (G1…G17 ascending, plain notation only, first text that parses back bit-equal; "R" fallback); storage and resolver untouched. New test `DisplayShowsShortestRoundTripTextForNonExactFloats` (typed→display equality for 0.0001 / 0.002 / 0.1234 / 7.3 / 10 / 0.01 + display→stored bit-equality).

**Human manual check (2026-08-03, after the fix):** window is clear, settings edit worked (floor changed by hand), verdict and numbers readable. Fix `c437e8a` confirmed; A1 merged as PR #9.

**VERIFIED FACT — batchmode** (`Block2.2.1-a1-fix`, exit 0): EditMode **115/115**, PlayMode **21/21**. **VERIFIED FACT — control search with defaults re-run** (`ghost-20260803T153414301-body49-X-AscendFromStart`): window rows and `metrics.json` `thresholdEstimateMetres:1.98919879E-05` bit-identical to the gate. **VERIFIED FACT — override screenshot** (`Block2.2.1-a1-ui/state-override-shortest-display.png`, visually inspected): floor/ceiling committed as 0.002 / 7.3 мм display exactly `0.002` / `7.3` (stored 2E-06 / 0.0073 м), «Источник: дефолты + правка окна (обе границы)», сброс и запуск enabled.

### 2026-08-03 — Block 2.2.1: A1 search-entry parameterization (`feat/block-2.2.1`, awaiting review)

**Contract:** `docs/CONTRACT-2.2.1.md` (ratified full text + mockup amendment, commits `12ccfaf` + `5c7ab7d`). **Scope:** `GhostSearchEntry.cs` (new: entry struct, target catalog via display-name provider, resolver = the single settings path, verbatim table reasons, mm↔m invariant-culture conversion), host (`TryStartTowerSearch(GhostSearchEntry, source, out reject)`, full SessionState entry persistence, runner resolves fail-closed — `SETTINGS_RESOLVE_FAILED`, no CreateDefault call sites), window (asset ObjectField by GUID, target dropdown, editable ε range in mm with per-field verbatim reasons, no silent step reset, «Сбросить к источнику», «Источник» row, settings-source rows in the result), Evidence (`GhostSettingsProvenance` + manifest `settingsSource`), Core additive-only (3 validation-bound consts in `DivergenceSettings`).

**Deviations recorded:** tower bricks are stable IDs **1…48** (factory), not the 0…48 wording in the mockup approval — provider derives ids from `TowerProbeRequestFactory`; unparseable ε text shows that row's verbatim table reason (the table defines no separate not-a-number reason; fail-closed, raw text kept); committing a value equal to the current source clears rather than pins the override; an override's mm display shows full float "R" precision (0.0001 → `0.000100000005`), defaults display clean (`0.01`/`10`).

**VERIFIED FACT — batchmode Unity `6000.3.21f1`** (`run-checkpoint.ps1 -Suite All -EvidenceDir Library\BugCamEvidence\Block2.2.1-a1`, exit 0): EditMode **114/114** (107 existing + 7 new A1), PlayMode **21/21**; XML in `Library/BugCamEvidence/Block2.2.1-a1/`. First run failed 16+1 — all in pre-existing reflection helpers pinned to the old 6-arg `Build`/7-arg `CreateFailureDocument` signatures; helpers updated to pass `GhostSettingsProvenance`, no production change.

**VERIFIED FACT — control search on the tower with defaults (gate):** live editor, window's own `StartSearch` path, run `ghost-20260803T150822074-body49-X-AscendFromStart`. Window rows verbatim: 19.9 µm / вилка 19.8…19.9 µm / ширина 0.107 µm / первый кадр 27 (body 49) / разброс 3.79 mm (body 5) / 191× / 21 из 49; new rows «Источник настроек = дефолты», «Диапазон ε (эффективный) = 0.01 … 10 mm». `metrics.json`: `thresholdEstimateMetres:1.98919879E-05`, `searchFloorMetres:1E-05` — **bit-identical to the Block 2.2 pin**. `manifest.json`: `settingsSource{captured:true, sourceKind:"defaults", effectiveFloorMetres:1E-05, effectiveCeilingMetres:0.01}`. Host auto-exited Play Mode.

**VERIFIED FACT — window screenshots** (`Library/BugCamEvidence/Block2.2.1-a1-ui/`, full-screen ReadScreenPixel, both visually inspected): `state-valid-defaults-done.png` — DONE + result rows + expanded setup (ассет None, цель «снаряд — body 49», диапазон 0.01…10 мм, сброс disabled, «Источник: дефолты», кнопка enabled); `state-invalid-floor-below-gate.png` — floor 0.0001 мм через реальный `CommitRangeField`: ⚠ verbatim «ниже гейта воспроизводимости…», «Источник: дефолты + правка окна (нижняя граница)», кнопка disabled + «Причина: параметры невалидны — …». Focus caveat: the final valid shot was taken with the OS-foreground on the floating BugCam window while `isApplicationActive` returned a false negative — the gate was bypassed for that one capture and superseded by visual inspection of the PNG.

### 2026-08-03 — Block 2.2.1: A6+A7 docs pass + A4 Scene View legend/label positioning (`feat/block-2.2.1`)

**Base:** `5c0e6a9` (= main). Commit 1 `7c548be` (A6+A7, docs only); commit 2 = this entry (A4 + STATUS).

**A6 (docs):** `DivergenceSettings` epsilon why-comments now name their phases (growth factor = Exponential refinement only; ladder = independent log-uniform interpolation, coincidence condition stated); `CLAUDE.md` bitwise clause names its sole exception (PLAN 1.4 VERIFY (a) = determinism regression test); `SPEC.md` §5 share-of-scene gate annotated with the existing `*` backlog convention; `PLAN.md` gains the Block 2.2.1 section (composition, order, ratified decisions, deferred bucket B).

**A7 (human decision executed):** `*.csproj` ignore kept and documented in `CLAUDE.md` Testing. **FINDING:** the item's premise was stale — no csproj exists anywhere in the worktree (full-tree search outside `Library/`); the Block 1.3 "headless test csproj" was never created.

**A4 (code, Editor-only, search untouched):** `GhostSceneViewDrawer` — legend re-anchored to the bottom-RIGHT of the Scene View viewport. Deviation from the announced bottom-left, decided mid-fix on measured evidence: the first after-screenshot showed bottom-left occluded by the floating Ghost Visualization window in the real layout (it hangs over the scene view's whole left flank); top-left = toolbar/Tools overlay (the original defect), top-right = orientation gizmo; bottom-right is the only corner free in both default and observed layouts. Marker labels moved from world-space `Handles.Label` to a GUI pass: clamped into the viewport (readable near/beyond screen edges), skipped when the marker is behind the camera (previously drawn at a meaningless projected point). GUI styles cached; `Draw` signature gains the `SceneView` parameter (sole caller `GhostVisualizationSession` updated; no test pins the signature).

**VERIFIED FACT — live before/after screenshots** (full-screen `ReadScreenPixel` captures in a focused editor, `Library/BugCamEvidence/Block2.2.1-a4-ui/`): `legend-before.png` (run `ghost-20260803T142009152`) — legend at top-left with its left half hidden under the overlapping window, only line tails readable; `legend-after.png` (run `ghost-20260803T142301543`) — legend fully readable bottom-right, "First divergence" / "Max spread" centered above their markers. Each state exercised its own live tower search through the window's host path; all four searches this session produced bit-identical display numbers (threshold 19.9 µm, bracket 19.8…19.9 µm, width 0.107 µm, first frame 27 body 49, max spread 3.79 mm body 5, amplification 191×, 21/49 bodies) and the host auto-exited Play Mode each time.

**VERIFIED FACT — batchmode Unity `6000.3.21f1`** (`run-checkpoint.ps1 -Suite All -EvidenceDir Library\BugCamEvidence\Block2.2.1-a4`, exit 0):

| Suite | total | passed | failed | result | XML |
|---|---|---|---|---|---|
| EditMode | 107 | 107 | 0 | Passed | `Library/BugCamEvidence/Block2.2.1-a4/EditMode.xml` |
| PlayMode | 21 | 21 | 0 | Passed | `Library/BugCamEvidence/Block2.2.1-a4/PlayMode.xml` |

Counts identical to the Block 2.2 final gate — this pass adds no test surface. `TowerScene.unity` touched by the PlayMode run and reverted (pre-existing regeneration). The four protected files remain untracked.

### 2026-08-03 — Block 2.2-window-ux: manual-test blocker fixed, live MCP verification GREEN (`64f0698` + regression pins)

**Manual test of PR #8 (human, two runs)** confirmed the pipeline end-to-end and bit-identical repeatability (threshold 1.98919879E-05 m, first frame 27, 21/49 bodies on both runs) but found one BLOCKER and two must-fix UX defects; PR held open.

**BLOCKER root cause (double):** after a window-started search completed, (1) the host never exited Play Mode — Block 1.5 behavior, previously invisible because the old window gated nothing — and (2) the window drops its ownership flag on completion, so the leftover session was classified as foreign ("Play Mode запущен вручную, не BugCam") while the console honestly said `source=window`. **Fix (`64f0698`, host side):** `StartTowerSearch` arms a SessionState marker exactly where the host itself flips `isPlaying`; after the completion is delivered and Busy is cleared, `ExitHostEnteredPlayMode` exits exactly that session. A search launched inside a user-started Play Mode never arms the marker; `ExitingPlayMode` always disarms it, so a stale flag can never auto-exit a future manual session; ordering (after `Cleanup()`) guarantees no false "Interrupted" completion. Also in `64f0698`: display units per SPEC §17 (auto m/mm/µm with one shared unit per range, 3 significant digits, integer amplification — `19.9 µm`, `0.107 µm`, `191×`; full R precision stays in evidence files), and result block moved directly under the status line (tutorial below).

**VERIFIED FACT — live two-run series in the open editor (driven via mcp-for-unity, window's own `StartSearch` path):** run 1 and run 2 back-to-back with **no manual Stop between them**; after each completion the editor exited Play Mode by itself (`isPlaying=false`, window state `Done`, `IsForeignPlayMode()=false`); both runs wrote evidence bundles (`ghost-20260803T124204890…`, `ghost-20260803T124327732…`) with identical numbers: threshold estimate 19.9 µm, bracket 19.8…19.9 µm (width 0.107 µm), first frame 27 (body 49), max spread 3.79 mm (body 5), amplification 191×, 21/49 bodies — matching the human's manual runs bit-for-bit at display precision. Blocker closed on the exact scenario it lived in (the repeat run).

**MCP screenshot verification of every window state** (`Library/BugCamEvidence/Block2.2-ui/`, captured via `InternalEditorUtility.ReadScreenPixel` with editor-focus gating): `state-ready-first-run.png` (tutorial + expanded setup + ε range 0.01…10 mm read-only), `state-idle-foreign-playmode.png` (honest foreign-Play-Mode reason under the disabled button — captured against a genuinely user-equivalent manual Play Mode; prior-run row with evidence folder button), `state-searching.png` (live run: phase strip on [Fan], step 11/15, epsilon 21.9 µm, working Прервать, reason "идёт поиск"), `state-done-bracket-found.png` + `state-done-bracket-found-full.png` (verdict verbatim + all availability-gated rows + Scene View controls), `state-done-interrupted.png` (verbatim host status, neutral wording, evidence button disabled with reason, re-run enabled — a real 7-second interrupt), `state-done-stable-SYNTHETIC.png` (**synthetic UI-layout shot** — STABLE completion injected via reflection with the real search floor 10 µm, evidence intentionally NOT written, both buttons disabled with reasons; ratified fallback because the range is read-only and TowerScene cannot produce STABLE honestly).

**Regression pins added** (`GhostWindowUxTests.cs`, new EditMode file, existing tests untouched): host arms the exit marker exactly once and only where it flips `isPlaying`; the exit helper gates on the marker; both completion paths exit strictly after `Cleanup()`; `ExitingPlayMode` disarms the marker; the window never enters Play Mode and exits it in exactly one place (the interrupt button); Core progress accessors and the host `SearchProgress` event stay present and read-only. Live MCP EditMode run: **107/107 passed** (103 + 4).

**Batchmode checkpoint on `64f0698`** (editor closed at the time): EditMode 103/103, PlayMode 21/21, exit 0 (`Library/BugCamEvidence/Block2.2-fixes`). Final `-Suite All` on the branch tip (with the regression file) pending an editor-closed window before merge.

**Cosmetics adjudicated with the human:** window-title "duplication" retracted (second line is Unity's tab, not code); Scene View legend overlay positioning → Block 1.5 backlog (see Open findings); editable ε range → backlog (see Open findings).

### 2026-08-03 — Block 2.2-window-ux: Ghost Visualization window UX pass (`feat/block-2.2-window-ux`)

**Base:** `fd54ab9` (= `main`, Block 2.1 squash). UI-only block ratified by the human via an ASCII mockup review (two rounds); search functionality untouched.

**Baseline measurement BEFORE any code change (explicit instruction):** EditMode on untouched `fd54ab9` = **103/103 Passed** (`Library/BugCamEvidence/Block2.2-base/EditMode.xml`). This **corrects the stale 102/102** recorded in the Block 2.1 entry below: commit `05edadc` ("pre-PR7 blocker fixes") added exactly one EditMode regression (fully-occluded best candidate must yield LOW) after that table was written, and `git diff 05edadc fd54ab9` is empty, so `main` carries the 103rd test. Not a drift — a stale doc row.

**Commit 1 (Core, isolated):** `dd349f1` adds read-only progress accessors to `EpsilonSearch`: `CurrentPhaseStep`, `PhaseStepTotal` (-1 = honestly unknown, Exponential only), `HasOutstandingProbe`, `CurrentEpsilonMetres`. Pure getters over existing private fields; zero control-flow change, zero allocations, no test edits. EditMode after: 103/103 (`Block2.2-core-props`).

**Commit 2 (Editor):** window state machine + host progress events.
- `GhostEvidencePlayModeHost`: new `SearchProgress` event + `GhostSearchProgress` readonly struct; the runner MonoBehaviour polls the Core accessors in `Update` (field compares only, zero alloc on quiet frames) and raises the event on change. All Block 1.5 lifecycle contracts untouched (`TryStartTowerSearch` gate, `StartCoroutine(Run(`, `yield return runner.Run(`, interrupt cleanup).
- `GhostVisualizationWindow` rewritten around the ratified state machine IDLE → READY → SEARCHING → DONE. IDLE = transient blockers only, each rendered as its own reason row under the disabled button (compiling / play-mode transition / foreign Busy-Pending host lock / manually-started Play Mode); READY shows body count and the ε search range; SEARCHING shows phase strip + real step (`шаг 3 / —` when total unknown) + current ε and a working «Прервать» (Play Mode exit; the host's existing interrupt cleanup owns the completion — the window never synthesizes a verdict line); DONE renders the verdict verbatim and LARGE, one meaning line, and only the numbers defined for that verdict (availability-flag-gated; no "unavailable" placeholders anywhere). INTERRUPTED is neutral (verbatim host status, no numbers, folder button disabled with reason). Write-failure path: verdict + numbers + verbatim error banner, folder button disabled with reason. Domain reload: document dies → result block does not render (no stale numbers); only «Открыть папку улик» survives via the serialized path. First-run "3 шага" block + EditorPrefs; setup collapses after first successful run; every field carries a unit tooltip (step duration computed from `BugCamConstants.FixedStep` — the harness steps with the constant, NOT `Time.fixedDeltaTime`, ratified deviation). Removed per ratified mockup: label legend, Copy Summary / Copy Metrics Path, Regenerate Screenshots, summary TextArea. Perf: progress strings rebuilt only in event handlers (shared StringBuilder, cached GUIContent); `Repaint` only from search/compilation/play-mode events.
- One compile fix during checkpoint: the Editor facade `BugCam.Editor.TowerProbeRequestFactory` shadows the Core type inside the Editor namespace and has no `ExpectedBodyCount`; the window now fully qualifies `BugCam.Core.TowerProbeRequestFactory.ExpectedBodyCount`.

**VERIFIED FACT — batchmode Unity `6000.3.21f1` (`run-checkpoint.ps1 -Suite All`, exit 0):**

| Suite | total | passed | failed | result | XML |
|---|---|---|---|---|---|
| EditMode | 103 | 103 | 0 | Passed | `Library/BugCamEvidence/Block2.2-final/EditMode.xml` |
| PlayMode | 21 | 21 | 0 | Passed | `Library/BugCamEvidence/Block2.2-final/PlayMode.xml` |

No test files touched; counts identical to the pre-change base (103) and to Block 2.1 PlayMode (21).

**PENDING:** live Unity MCP screenshot verification of every window state (IDLE / READY / SEARCHING / DONE × BRACKET FOUND / STABLE / INTERRUPTED) — requires a live Editor session with the MCP server; batchmode cannot render EditorWindows. PR stays OPEN (explicit instruction: do not merge).

**Environment note:** first baseline attempt failed with `Win32 IO returned 112` (drive X: at 0 bytes free) — regenerable Unity caches (`Library/Bee`, `ShaderCache`) cleared and the X: recycle bin emptied with the human's explicit approval; ~13.5 GB free afterwards.

### 2026-08-03 — Pre-PR7 blocker fixes: occlusion-coverage verdict + degenerate ranking removed; physics documentation facts

**BLOCKER 1 fixed — the EVIDENCE COVERAGE verdict now gates on occlusion coverage, not the ranking score.** New metric `OcclusionCoveragePerBody` = camera 1's `VisibilityScore / AffectedBodyCount` — the fraction of unoccluded sample points averaged over affected bodies (0..1), named separately from and reported alongside the informational `BestScorePerBody`. Threshold changed **0.5 → 0.25** — **PROVISIONAL, not a calibrated boundary**: the four live measurements gave only a tight "good" cluster (**0.354 / 0.365 / 0.376**) and one zero (**0.000**); the intermediate 0.1–0.3 region ("barely visible") was never measured, so 0.25 is a value that separates the two observed extremes, chosen without data about where "poor but usable" actually ends. It needs refinement on a scene with genuine partial occlusion before a LOW/OK flip near 0.25 can be trusted; only the extremes (≥0.35 → OK, 0.0 → LOW) are evidence-backed today. The old total-score gate was degenerate by the numbers: the in-frustum term alone contributed ~0.83–0.98 per body, so the worst live candidate (visibility 0.00, per-body score 0.98) passed as OK. New EditMode regression `FullyOccludedBestCandidateMustYieldLowVerdictDespiteHighRankingScore`: affected body fully enclosed by an occluder ⇒ coverage exactly 0 ⇒ verdict LOW, while asserting the ranking score stays above the gate (pinning that the verdict no longer follows the score).

**BLOCKER 2 fixed — path (б): degenerate ranking terms removed, PLAN rewritten honestly.** Chosen over path (а) by the numbers: even re-anchoring contact proximity to the first-diverged body keeps every candidate on the same sphere (distance range ≈9–13 m ⇒ 10×contact spread ≈0.3 rank units) against observed adjacent orthogonality gaps ≥3 units — the criterion still could not flip a single choice; making it real requires multi-radius candidate generation + contact data = a new feature (SPEC §7), backlog, not a PR #7 fix. Removed outright (no dead weights left behind): `WeightCameraOrthogonality`, `WeightContactProximity`, `WeightTrajectoryAlignment`, `ScreenSpaceSeparationNormalizer` from `DivergenceSettings`; the separation term from candidate scoring (measured 0.00036–0.00074 total ≈ 0.29 px over 21 bodies at the first-divergence frame — 4–5 orders below the other terms); `ContactProximity`/`TrajectoryAlignment`/`RankScore` from `EvidenceCameraWinner`. Cameras 2–4 rank by orthogonality to camera 1 alone (descending, index-ascending ties) within the top-25% survivor pool; a single ranking term carries no weight field because a scale factor cannot change an ordering. `EvidenceCameras.AlgorithmVersion` **1 → 2**; `EvidenceCameraPlanSchema.SchemaVersion` **1 → 2** (adds `occlusionCoveragePerBody`, removes the dead fields — breaking for v1 consumers). `docs/PLAN.md` Block 2.1 Scoring/Winners/Honest-verdict sections and the `DivergenceSettings` contract table amended in the same change.

**Verified in the live editor (Unity MCP `run_tests`):** compile clean (0 errors), `BugCam.Tests.EvidenceCameraTests` **10/10 passed** (9 prior + the new LOW-verdict regression).

**VERIFIED FACT — batchmode Unity `6000.3.21f1`** (`run-checkpoint.ps1 -Suite All -EvidenceDir Library\BugCamEvidence\Block2.1-pre-pr7-fixes`, exit 0, full suites — not a subset):

| Suite | total | passed | failed | result | XML |
|---|---|---|---|---|---|
| EditMode | 103 | 103 | 0 | Passed | `Library/BugCamEvidence/Block2.1-pre-pr7-fixes/EditMode.xml` |
| PlayMode | 21 | 21 | 0 | Passed | `Library/BugCamEvidence/Block2.1-pre-pr7-fixes/PlayMode.xml` |

EditMode +1 vs the Block 2.1 baseline (102 → 103: `FullyOccludedBestCandidateMustYieldLowVerdictDespiteHighRankingScore`); every pre-existing test passed unchanged — no test was loosened to absorb the algorithm change. PlayMode unchanged at 21 (this change adds no PlayMode surface). `TowerScene.unity` was touched by the PlayMode run (pre-existing procedural regeneration) and reverted, matching prior sessions. The interactive editor was exited gracefully via MCP before the batchmode runs and the four protected untracked files remain untracked.

**DOCUMENTATION FACTS (no code touched):**

1. **`Physics.defaultContactOffset` = 0.01 m coincides exactly with `EpsilonCeiling` = 0.01 m.** The entire tested perturbation range (1e-5…1e-2 m) therefore lies **inside** the contact-generation shell: every probed epsilon changes positions by less than or equal to the distance at which PhysX begins generating contacts, so perturbations act by re-ordering/re-timing contact creation rather than by geometric separation — a plausible physical mechanism for the observed sensitivity (THRESHOLD BRACKET FOUND at sub-millimetre epsilons on the tower), and an independent justification for the ceiling: above contact-offset scale a perturbation is no longer "small" relative to the solver's own contact horizon. Recorded as a likely mechanism, not a proven one — proving it needs contact-set instrumentation, which is backlog (not in the 14-float stride).
2. **Threading mode: every A/A′ `delta=0` result in this repository was measured on MultiThreaded physics (`m_ThreadingMode=0`), the live editor runs MultiThreaded, and the 2026-08-03 capsule records `threadingModeName=MultiThreaded`.** Block 1.1 also measured SingleThreaded explicitly (same A/A′=0, same A/B numbers on this machine), but all routine evidence since then is MultiThreaded-only. Repeatability claims are therefore **environment-scoped by threading mode** like any other environment parameter: a PASS carries `threadingModeSerialized` in its capsule and does not transfer to the other mode without re-measurement.
3. **The fixed step is `0.0199999921` s, not decimal 0.02.** The serialized TimeManager value is `2822399/141120000` ≈ 0.0199999921, bit-identical to `float(0.02)` — so `BugCamConstants.FixedStep = 0.02f` and the editor agree exactly (same float bits), and no code change is needed; but prose claiming "0.02 s" means this float value. `docs/PLAN.md` Block 1.1 harness line now states the measured value; `simulatedTime`-style arithmetic in docs should use 0.0199999921 per step when compared against wall-clock-like quantities.

### 2026-08-03 — Block 2.1 evidence-camera live calibration (Unity MCP, same editor session)

**Setup.** Live Tower ghost search (step 32, body 49, axis X, AscendFromStart) started through `GhostEvidencePlayModeHost.TryStartTowerSearch` in the running editor → `BUGCAM_BLOCK_1_5_HOST_COMPLETE success=True`, verdict `THRESHOLD BRACKET FOUND`, 15 fans, run `Library/BugCamEvidence/Runs/ghost-20260803T082315020-body49-X-AscendFromStart`. That capsule's `manifest.json` carries the new `physicsSnapshot` with live values (`captured=true`, `enhancedDeterminism=false`, `threadingModeSerialized=0/MultiThreaded`) — end-to-end verification of the schema change below. `EvidenceCameras.Plan` was then re-run over the retained baseline + primary fan in the same session (recomputed twice, identical winners — determinism held). Screenshots: all 49 bodies reconstructed at `FirstDivergenceFrame=27` from the retained perturbed-run frames (recorded positions + rotations, real `Size`), layer-isolated camera (fov 50, 1920×1080, near 0.05, far 500, `LookAt` event center, up=Y — matching the plan's projection constants), affected bodies tinted red → `calibration/cand-XXX.png` under the run directory.

**Plan numbers:** verdict `EVIDENCE COVERAGE: OK`; `bestScorePerBody=1.3334`; affected bodies 21; event center `(-0.458, 3.975, 0.004)`; candidate sphere radius `10.92`; 128 candidates → 41 rejected below ground → **87 survivors** → top-25% pool = **22**.

**Calibration table (computed vs what the screenshot actually shows):**

| Candidate | Position | Computed | Screenshot |
|---|---|---|---|
| 84 = camera 1 | (8.44, 0.48, 5.28) | total 28.00 = frustum 21 + vis 7.89 + sep 0.0006 − 0.25×3.55; visibility = 37.6% of samples | Tower fully in frame from a low side angle; red affected faces clearly visible; partial mutual occlusion consistent with vis < 50% |
| 65 = camera 2 | (4.67, 3.72, −9.63) | total 27.52; orthogonality 0.96 | Near-orthogonal view confirmed; the displaced red body visibly sticks out of the stack |
| 32 = mid-pool | (1.15, 9.35, 9.37) | total 27.87; vis 7.67 | Elevated view, affected bodies on the shadow side; visually as usable as 84/65 — scores within 0.5% of each other and the pictures agree |
| 0 = deliberately bad | (0.90, 14.81, 0.004) | total 20.58; **visibility 0.00** | The entire frame is one white top face of the topmost cube; zero affected bodies visible — computed vis=0 matches the picture exactly |

**Findings (numbers only — nothing fixed silently, per instruction):**

1. **`MinEvidenceCoverageScore=0.5` does not measure visibility.** Per-body score = inFrustum(1) + visibility + separation − 0.25×centrality, so the inFrustum term alone yields ~0.83–0.98 per body after the centrality penalty — already above the 0.5 gate with **zero** visibility. Worst survivor (candidate 0): 20.58/21 = **0.98 per body at visibility 0.00** — it would pass the coverage gate as camera 1. The gate as ratified distinguishes "AABBs inside the frustum" from "AABBs outside it", not "bodies actually seen". The scores themselves are honest — vis=0 matched the screenshot pixel-for-pixel; the threshold's semantics are the issue.
2. **`ScreenSpaceSeparationNormalizer=500` at render width 1920 makes the separation term vestigial at the first-divergence frame.** Observed separation contribution across all 87 survivors: 0.00036–0.00074 (mean 0.00058) ≈ 0.29 px summed over 21 bodies — 4–5 orders of magnitude below the frustum/visibility terms. Physically inevitable: at the *first sustained* divergence the offset is ~1 mm, which is sub-pixel at 1920 from ~11 m away. The term could only matter if scored at a late/max-spread frame; as computed today it never influences selection.
3. **Weight triad 100/10/1: TrajectoryAlignment influenced no choice in this run.** Winners 2–4 and their order are identical with `WeightTrajectoryAlignment=1` vs `0` (rank re-computed both ways for the whole pool: 65→52→78 both times; adjacent rank gaps ≥ 3 units vs traj range ≤ 1). Additionally **ContactProximity is constant across all candidates by construction**: every candidate sits on the same sphere (radius 10.92), so contact = 1/(1+10.92) = 0.0839 for each — criterion (b) has zero discriminating power under the current single-radius candidate generation. Effective ranking today = orthogonality alone, with traj as a sub-1% tie-breaker that never fired.
4. **`EvidenceTopScoreFraction=0.25`: "128 → 32" holds only before the ground cull.** Actual chain: 128 generated → 41 culled below ground (this scene) → 87 survivors → pool = ceil(87×0.25) = 22 (21 available for slots 2–4 after removing camera 1). The fraction applies to survivors, so the real pool is ~⅔ of the naive 32; whether that is enough diversity for slots 2–4 is a design question to settle when RetroPlayer/compositing work starts, not a code bug.

### 2026-08-03 — Live physics snapshot via Unity MCP + `physicsSnapshot` in manifest.json (`feat/block-2.1-evidence-cameras`)

**VERIFIED FACT — live Unity MCP** (mcp-for-unity-server v3.4.5 over HTTP `127.0.0.1:8080/mcp`, `execute_code` inside the running editor `6000.3.21f1`, read from `Physics.*` / `Time.*` / SerializedObject over the PhysicsManager singleton — not from ProjectSettings files on disk):

| Setting | Live value |
|---|---|
| `Application.unityVersion` | `6000.3.21f1` |
| Fixed timestep (`Time.fixedDeltaTime`) | `0.0199999921` (float "R"; matches serialized `2822399/141120000 ≈ 0.02`) |
| Simulation mode (`Physics.simulationMode`) | `Script` (`m_SimulationMode=2`) |
| Enhanced determinism (`m_EnableEnhancedDeterminism`) | `False` |
| Physics threading mode (`m_ThreadingMode`) | `0` = MultiThreaded |
| Solver iterations (position / velocity) | `6` / `1` |
| `Physics.defaultContactOffset` | `0.01` |
| `Physics.defaultMaxDepenetrationVelocity` | `10` |
| `Physics.sleepThreshold` | `0.005` |
| `Physics.bounceThreshold` | `2` |
| `Physics.gravity` | `(0, -9.81, 0)` |

Probe note: the first read used the property name `m_EnhancedDeterminism` and honestly returned null — the actual serialized name on this editor is **`m_EnableEnhancedDeterminism`** (confirmed by a full serialized-property dump of the PhysicsManager singleton). Values match the Block 1.1 environment snapshot and the on-disk `DynamicsManager.asset` read from 2026-08-01 — no drift.

**Schema change — every evidence capsule now records live physics values.** `manifest.json` gains a `physicsSnapshot` object (top level, before `artifacts`): `captured`, `unityVersion`, `fixedDeltaTime`, `simulationMode`, `solverIterations`, `solverVelocityIterations`, `defaultContactOffset`, `defaultMaxDepenetrationVelocity`, `sleepThreshold`, `bounceThreshold`, `gravity{x,y,z}`, `enhancedDeterminism`, `threadingModeSerialized`, `threadingModeName`. Values are captured at evidence-build time from the running editor (`PhysicsRuntimeSnapshot.CaptureLive` in `BugCam.Evidence`), **never** from BugCam constants; `captured=false` or a missing editor-serialized value emits honest nulls. Editor-serialized values (enhanced determinism, threading mode) are read by the Editor-side host via the existing `PhysicsSettingsProbe` and degrade to nulls with a logged warning if the probe throws (`GhostEvidencePlayModeHost.CapturePhysicsSnapshot`). `SchemaVersion` stays `1` — the field is additive and no existing consumer key changed.

**Implementation surface:** `GhostEvidenceDocument.cs` (+`PhysicsRuntimeSnapshot`, `GhostRunEnvironment.Physics`, two-ctor split), `GhostEvidenceWriter.cs` (`AppendPhysicsSnapshot` in `BuildManifestJson`), `GhostEvidencePlayModeHost.cs` (capture at search start). Core untouched.

**VERIFIED FACT — live EditMode run via MCP `run_tests`:** first run failed 16/25 (`MissingMethodException: Constructor on type 'GhostRunEnvironment' not found`) because the zero-ref reflection tests construct the struct with exactly four argument types and an optional fifth parameter changes the reflected signature. Fixed by restoring the literal 4-parameter constructor (chaining to the new 5-parameter one) — tests untouched. Rerun: `BugCam.Tests.GhostEvidenceTests` **25/25 passed** in the live editor. Compile clean; the only warnings are two pre-existing CS0618 in `PhysicsSettingsProbe.cs` (obsolete `GetScriptingBackend(BuildTargetGroup)` / `Physics.autoSyncTransforms`), untouched by this change.

**Also this pass (docs only):** the three Known issues above were re-adjudicated after review — #1 reformulated (no false-positive path; the real defect is the degenerate score half of the AND-gate on large scenes), #2 gained the cross-scene-comparability consequence (architectural debt blocking SPEC §12), #3 retracted as factually incorrect (`EpsilonGrowthFactor` is live in the Exponential phase; ladder is an independent log-uniform progression).

### 2026-08-03 — PR #6 (Block 1.5) reviewed and squash-merged; Block 2.1 evidence-camera core landed (`feat/block-2.1-evidence-cameras`)

**PR #6 close-out.** Verified exact state before acting: local HEAD, `origin/feat/block-1.5-ghost-visualization` HEAD, and PR #6 `headRefOid` all matched the expected `35bdb6f8d0290b2c7a2023061ccbeeaa1cb8647d`; `mergeStateStatus=CLEAN`, `mergeable=MERGEABLE`, merge base `1bd10113eeeb8376ae31379b391e8c408d2884a8`; tracked worktree clean; the four protected files remained untracked. Independent read-only review of `3f2c93d..35bdb6f` (the interrupt-cleanup commit) confirmed: `CleanupHostOwnedSearch` destroys only the `BugCamGhostEvidenceRunner_TEMP` GameObject by name (no other object touched); `FinishBusy()` clears both Busy and Pending together; `shouldNotify` is captured from `IsSearchBusy` *before* `FinishBusy()` runs, so a second cleanup call is a no-op notify (idempotent); the success path's `Cleanup()` calls `notifyInterrupted:false` so a later `ExitingPlayMode` after a successful run finds `IsSearchBusy` already false and never emits a false "Interrupted" completion; the regression test `PlayModeInterruptCleanupDestroysTempRunnerAndAllowsRestart` is behavioral (creates a real `GameObject`, invokes the real `CleanupInterruptedSearchForTests` seam via reflection, asserts real destruction + real single non-success event + idempotent repeat + accepted restart), not a source-string assertion. Squash-merged as `558d4f1a763808e4eb3e4cbbe675b3d698cb1cf5` with the exact head-SHA merge guard; local `main` fast-forwarded to match `origin/main`.

**Block 2.1 landing scope (this branch).** Per docs/PLAN.md's amended Block 2.1 section: lands `Assets/BugCam/Evidence/EvidenceCameras.cs` (+ `EvidenceCameraMath.cs`, `EvidenceCameraPlanSchema.cs`, `EvidenceCameraPlanWriter.cs`) — deterministic candidate generation, scoring, honest verdict, and `camera-plan.json` — plus EditMode VERIFY. `RetroPlayer` and 2×2 compositing are deferred → Block 2.3 (see Open findings above). This split exists because Unity MCP was unavailable this session (no live Editor to verify a rendering path against), while the selection algorithm is fully checkable in batchmode.

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
- 2026-08-03 — Block 2.1 split into a batchmode-verifiable core (this commit) plus a deferred remainder (RetroPlayer + 2×2 compositing, → Block 2.3), because Unity MCP was unavailable this session and a rendering path cannot be honestly claimed done without a live GPU Editor session to look at it — the same standard applied to Block 1.5's screenshot capture after the `#1F1F24` blank-PNG correction.
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
