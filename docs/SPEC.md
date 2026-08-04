# BUGCAM: FINAL PRODUCT SPEC v1.0

> Scope note for the agent: this file describes the FULL product. Only PLAN.md defines what is built now.
> Where SPEC and PLAN differ (e.g. contacts tracking, attribution, capsule, CI), PLAN wins for v0.1 and the SPEC item is backlog.

## 1. What it is

**BugCam** is a chaos-testing, repeatability-diagnostics and visual-evidence system for Unity scenes.

It answers three questions:
1. Does the same gameplay scenario repeat identically?
2. How small a change can drastically alter the outcome?
3. At which moment did results diverge, and what could have caused it?

Business positioning:
> BugCam finds out why your gameplay tests are flaky and proves it with a replay.

Public-content positioning:
> The butterfly effect. Now you can watch it.

Brand line:
> The camera looks into the past.

## 2. Why it exists

A normal test says "Test failed." BugCam says:
> First sustained divergence at physics frame 183. Smallest initial-position change: 0.27 mm.
> After 3 seconds the spread reached 1.74 m. 17 of 40 bodies affected.
> Here is the replay, the trajectories, and the observed writes to physics state.

The product sells: repeatability, reproducibility, fragility measurement, first-divergence localization, and an evidence artifact for CI / Jira / Discord. It does not sell cameras, AI, or a pretty replay.

## 3. Category

**Chaos engineering for game scenes.** Controlled perturbation of initial state → rerun → measure deviation → find sensitivity threshold → store evidence → gate unstable commits in CI.

Never claim: "first ever", "proves the bug in all cases", "makes PhysX deterministic", "finds the true cause with 100% accuracy".

## 4. Modes

### 4.1 🦋 Butterfly Test (free, viral)
User picks: scene/prefab, tracked Rigidbodies, perturbation target + parameter, duration, epsilon range.
BugCam: baseline → original run → small perturbation → compare → grow epsilon until significant divergence → binary-search the threshold → ghost trajectories → first divergence frame → replay from new angles → export evidence card + video.

Result block:
```
BUTTERFLY TEST
Smallest meaningful perturbation: 0.27 mm
First divergence: physics frame 183
Maximum output spread: 1.74 m
Amplification: 6,444×
Affected bodies: 17 / 40
```
If no divergence:
```
STABLE WITHIN TESTED RANGE
Perturbation range: 0.01 mm to 10 mm · Duration: 5 s · Tracked bodies: 40
```
BugCam never guarantees a pretty fan. It guarantees an honest result: a threshold found, or stability confirmed within the tested range.

### 4.2 Repeat Test (paid, engineering)
N identical runs: same initial state, seed, timestep, build, environment.
Result: `REPEATABILITY PASS 20/20` or `FAIL: 13/20 equivalent, first divergence frame 411, 4 outcome clusters`.
This mode does not accuse PhysX; it tests repeatability of this scene + code + config + environment.

### 4.3 Flaky Test Detector (main revenue)
Unity Test Framework integration:
```csharp
[BugCamRepeat(20)]
[UnityTest]
public IEnumerator VehicleCollision_ShouldReachExpectedState() { ... }
```
On instability: auto `.bugcam` capsule, ghost overlay, divergence timeline, evidence summary, CI artifact, hosted-viewer link if enabled.

### 4.4 Root-Cause Attribution (Pro)
Instruments observed managed writes to physics state (AddForce, MovePosition, velocity assignment, Transform changes, Unity Random, spawn/despawn, Rigidbody state switches).

Mandatory wording: "Observed managed writers" + "Instrumentation coverage: N%". Never "All writers".
Known blind spots: PhysX internals, native plugins, Burst jobs, DOTS Physics, precompiled assemblies, reflection, root motion.
When no observed writer explains a divergence:
```
Cause unresolved
No observed managed writer explains divergence
Possible solver/order sensitivity
```

## 5. Divergence Engine

Tracked per physics step: position, rotation, linear velocity, angular velocity, sleeping, active state*, contacts*, collision pair IDs*, selected custom state*. (* = backlog beyond v0.1; v0.1 tracks pos/rot/vel/angVel/sleeping only — see PLAN.md.)

Per-body normalized metrics: position error / object scale, rotation error / threshold, velocity error / threshold, contact mismatch*, state mismatch*. Scene Divergence Score = weighted sum over tracked bodies. (Superseded for v0.1 on 2026-08-03, Block 2.2.1 A3: the score is the MAX per-body weighted norm — the sum was measured degenerate and scene-size-dependent on the 49-body tower; PLAN.md Block 1.3 and docs/CONTRACT-2.2.1.md carry the ratified formula and threshold.)

Divergence is significant only when it: exceeds threshold AND persists several physics steps AND affects an important body or sufficient share of the scene*. This filters numerical noise. (* = the share-of-scene half of this gate is backlog beyond v0.1: the implementation uses a fixed ≥1-affected-body check per PLAN.md Block 1.3 — recorded 2026-08-03 in STATUS.md Known issues #2.)

## 6. Adaptive Epsilon Search

1. Baseline capture.
2. Exponential search: 0.01 mm × 2 per step until significant divergence (ceiling 10 mm).
3. Binary search between last stable and first unstable.
4. Fan generation around threshold: {0.8, 0.9, 1.0, 1.1, 1.2} × threshold (per-axis; exact run count fixed in PLAN.md).

## 7. Retroactive Evidence

Retro cameras are not the unique technology (state-based replay with free camera already exists, e.g. Ultimate Replay). BugCam's role: automatically turn the first divergence into evidence — restore recorded states, place 4 cameras at the divergence point (overview, contact close-up, opposite angle, trajectory view), show baseline vs perturbed run with numbers, produce slow-motion replay.

## 8. `.bugcam` format (backlog for v0.1 beyond a minimal export)

```
example.bugcam
├── manifest.json ├── initial-state.bin ├── runs.bin ├── perturbations.json
├── divergence.json ├── events.json ├── camera-plan.json ├── environment.json
├── scene-hashes.json ├── preview.glb ├── thumbnail.webp └── report.json
```
Stores: Unity version, package versions, platform, scripting backend, physics settings, fixed timestep, seed, scene GUID, asset hashes, initial state, perturbations, outcomes, divergence points.

Two reproduction levels: visual (browser, from stored geometry+trajectories) and exact simulation (only with a compatible project + matching environment). Viewer must honestly show "Visual evidence available / Exact simulation environment unavailable".

## 9. Browser Viewer (backlog)

Replays recorded `.bugcam` only. Never use another physics engine (e.g. Rapier) to "prove" Unity behavior. A separate Rapier "Butterfly Playground" is allowed later as an educational toy, clearly labeled, not a validator.

## 10. Viral loop

Free Butterfly Test → ghost overlay, one number, MP4, evidence card, shareable capsule.
Public format: `BUTTERFLY TEST #NNN · Initial change: X mm · Output spread: Y m · Amplification: Z×`.
Content series on own scenes, open-source demos, permissively licensed free assets, user-submitted scenes. Never built on humiliating specific developers.
Hook: "Move one object by less than a millimetre. Watch the entire scene change."

## 11. Pricing

| Tier | Contents | Price |
|---|---|---|
| Free — BugCam Butterfly | one perturbation type, limited bodies, ghost overlay, evidence card, MP4, local, watermark on public video only | CA$0 |
| Asset — BugCam Developer | adaptive epsilon, Repeat Test, retro cameras, .bugcam export, extended metrics, profiles, no watermark | CA$79–119 one-time |
| Pro — BugCam CI | UTF integration, CLI/batch, flaky detection, CI gate, trends, badge, attribution, hosted links | CA$39–59 / month |
| Studio | team dashboard, retention, cross-platform comparison, custom rules, support, audit exports, private hosting | from CA$1,200 / year |

## 12. Moat

Not the moat: replay, cameras, RenderTexture, ghost trails, MCP, AI explanations, Cinemachine.
The moat: Divergence Engine, Adaptive Perturbation Search, Attribution Coverage, `.bugcam` ecosystem, historical stability dataset (which tests are flaky, which objects fragile, when fragility appeared, which commit moved the threshold).

## 13. v0.1 technical scope

Supported: Unity 6, built-in 3D physics, Rigidbody, Collider, local PhysicsScene, pure-physics simulations, fixed step.
Not in v0.1: DOTS Physics, 2D Physics, Cloth, complex root motion, multiplayer sync, arbitrary gameplay code inside local PhysicsScene, absolute cross-platform repeatability. A slower Play-Mode replay path for full MonoBehaviour scenes comes later.

## 14. Honest caveats (README)

- Sensitivity is not automatically a bug (fragile tower can be intended design).
- **BugCam does not claim cross-platform repeatability.** A PhysX bit-identical result is a **within-platform and within-version claim only** — it is valid for the pinned Unity version, platform, hardware, timestep, seed, config and launch order under which it was recorded, and for nothing else. Divergence across CPU vendors (Intel versus AMD) or from GPU operation ordering is **expected, not a defect**, and must never be reported as one. (Amended 2026-08-04, replacing the earlier softer "repeatability is environment-scoped" wording; source of the constraint: `docs/RESEARCH-2026-08-04-placement.md`, Hard constraints discovered. Consistent with §13, which already excludes absolute cross-platform repeatability from v0.1.)
- **Raw `Physics.ComputePenetration` output is not trusted without sanitization.** The API has a documented defect class in which a shallow overlap returns an absurd depth (the recorded instance: **743.8444** for a capsule against a non-convex mesh) or returns `false` at small depth. Any detector built on it must sanitize before reporting, and must price in the multi-sample cost of doing so. (Recorded 2026-08-04; source and the documented workaround: `docs/RESEARCH-2026-08-04-placement.md`, Hard constraints discovered. Not re-verified in this repository — the blocking measurement is `docs/PLAN.md` Block 3.0, probe 2.)
- Attribution is partial (observed writers + coverage, not guaranteed cause).
- Solver effects exist (some divergence may originate inside the solver or processing order; label as "Cause unresolved").

## 15. What not to do

No camera swarm as the main product; no own MCP bridge; no voice; no Higgsfield inside the product; no AI in the deterministic core; no universal gameplay capture first; no simultaneous PhysX+DOTS+2D; no cloud before a working `.bugcam`; no auto-fix promises; no week on pretty Editor UI before a correct Divergence Engine.
**No AI in the triage layer** (added 2026-08-04, see §21): grouping, severity ordering and suppression are deterministic or they are not shipped.
**No automated attempt to distinguish a bug from designer intent** (added 2026-08-04). A leaning tower may be authored on purpose; nothing in BugCam may guess which. The only permitted mechanism is a **user-maintained exclusion list stored as plain text in the project** — reviewable, diffable, and owned by the user.
AI is optional only for: phrasing reports, explaining likely cause, test setup help, issue titles. All measurements work without AI, locally.

## 16. Build order

Day 1 Hero Proof → Day 2–3 Real Package → Day 4–7 Capsule → Week 2 Repeatability → Week 3 Test Framework → Week 4+ Attribution. (Compressed 2-day execution: see PLAN.md — PLAN.md is authoritative for the current sprint.)

## 17. Hero Demo — fixed 22 s script

Scene: tower or physical mechanism, tuned to be sensitive to a small change. Do not claim the scene was chosen randomly.

```
0–2 s   LOOKS IDENTICAL.
2–4 s   ONE OBJECT MOVED BY 0.27 MM        (use actual measured number)
4–8 s   Ghost trajectories flow together, then fan apart
8–11 s  FIRST DIVERGENCE · PHYSICS FRAME 183
11–16 s Replay from four new angles
16–20 s INPUT CHANGE: 0.27 MM · OUTPUT SPREAD: 1.74 M · AMPLIFICATION: 6,444×
20–22 s THE BUTTERFLY EFFECT. NOW YOU CAN WATCH IT.
Logo:   BUGCAM · Chaos testing for game scenes
```
All overlay numbers must be the real measured values from the actual run — no invented figures. Zero occurrences of the word "AI".

## 18. v0.1 success criteria (the only 8)

On a fresh pure-physics scene: 1) save baseline; 2) apply measurable perturbation; 3) reproduce a run series; 4) find first sustained divergence; 5) show ghost trajectories; 6) report threshold and output spread; 7) export evidence; 8) repeat without manual editing.
Until all 8 pass: no subscription, no hosted platform, no AI agents, no Asset Store submission, no team features.

## 19. Formula

Attracts: Butterfly Test · Proves value: Repeat Test · Earns: Flaky Test Detector + CI · Retains: historical fragility data · Spreads: `.bugcam` evidence links · Explains: Root-Cause Attribution.

BugCam is not a camera tool. It is a system that breaks a scene's repeatability in a controlled way, finds the moment of divergence, and turns it into verifiable evidence.

## 20. Unified comparison model

*(Added 2026-08-04 in the product reframe. Additive: sections 1–19 keep their numbering and their meaning.)*

**Every BugCam measurement is a comparison of two states that differ only in the reference point.** There is no second measurement technology hiding behind the product's separate-looking features — there is one comparison, run against four different reference points:

| # | Reference point | What the comparison yields |
|---|---|---|
| 1 | **Frame zero against itself** | **Geometric findings** — penetration, clearance, duplicates. No simulation has run; the two states are the scene and the scene, compared pairwise. |
| 2 | **A run against frame zero** | **Settle findings** — what moved after release. |
| 3 | **A run against another run** | **Repeatability** — §4.2 Repeat Test, §4.3 Flaky Test Detector. |
| 4 | **A run against a perturbed run** | **Fragility and threshold** — §4.1 Butterfly Test, §6 Adaptive Epsilon Search. |

**The divergence engine (§5) is shared across all four.** Its per-body normalized metrics, its significance gate and its first-divergence localization do not change with the reference point; only the reference point changes.

**Only the epsilon threshold search (§6) is specific to the fourth reference point** — because at epsilon zero there is no threshold to search. Reference points 1–3 use the divergence engine without any search phase; the search exists precisely because reference point 4 introduces a magnitude that can be varied.

Consequence for the product story: geometric findings, settle findings, repeatability and fragility are one instrument reported four ways, not four instruments. Consequence for engineering: the evidence layer (Blocks 2.2.3–2.6 in `docs/PLAN.md`) is required under **every** one of the four — a finding without a camera, a number and an evidence artifact is not a BugCam finding, whichever reference point produced it.

## 21. Placement detector family

*(Added 2026-08-04. **SCOPE EXPANSION — explicitly NOT part of v0.1.** Nothing in this section is ratified, nothing here licenses code. v0.1 scope stays exactly as §13 and `docs/PLAN.md` define it; the plan entries live under `docs/PLAN.md` Phase 3, every one of them marked PLANNED-NOT-RATIFIED. Research provenance, including sources and their re-verification status: `docs/RESEARCH-2026-08-04-placement.md`.)*

This family applies reference point 1 and reference point 2 of §20 to placement errors — objects that intersect, float, are unstable, or leave the level unsealed.

**1. Static clash probe.** `Physics.ComputePenetration` over static-versus-static pairs — objects that carry **no Rigidbody** and therefore **cannot be detected by simulation at all**: nothing ever moves them, so a settle probe sees nothing. This is reference point 1: frame zero against itself, penetration in millimetres. Published definitions to adopt rather than invent: PhyScene's collision rate (Col_obj / Col_scene). Raw API output is sanitized before it is reported (§14 caveat).

**2. Settle probe.** Reference point 2 on **existing Rigidbodies**: release, simulate, compare against frame zero, report what moved and how far. The industry mental model already exists — Bethesda Creation Kit's `Don't Havok Settle` flag has made modders do this by hand for over a decade — so the naming is reused, not reinvented.

**3. Support-polygon stability margin.** A body is statically stable when its centre of mass projects inside the convex hull of its contact points; the margin is the distance from that projection to the hull boundary, **in millimetres, without running physics**. Necessary, not sufficient — stated as such in any output. The rigorous treatment for stacked rigid blocks (equilibrium, friction-cone and compression-only interface forces) is the Whiting/Ochsendorf/Durand masonry work cited in the research record.

**4. Gap and leak detector.** Modelled on Valve Source Hammer: a level not sealed against the void produces a `leaked!` diagnostic plus a pointfile drawing a line from the leaking entity through the gap to the outside, and the documented workflow is to follow that line. Both halves are reusable — the detection **and** the "here is where to look" line, which maps directly onto our camera placement (§7). Note carried from `docs/PLAN.md` Block 3.4: this is architecturally a **separate product line with a different audience** and must not be bundled into the first placement release.

**5. Triage layer — deterministic grouping, numeric severity ordering, user-maintained exclusion list, no AI.** Borrowed wholesale from BIM clash-detection practice: separate hard clash from clearance clash from workflow clash; apply a numeric tolerance; group repeated clashes into a single issue; apply ignore rules for same-file and same-layer pairs. BIM practice explicitly treats an enormous clash report as a **process failure, not a finding**.

**The triage layer is not optional and must ship together with the first detector.** An ungrouped detector produces an unusable volume of findings on a real scene — the detector without triage is not a smaller product, it is a broken one. Its mechanisms are constrained by §15: no AI anywhere in it, no automated attempt to tell a bug from designer intent, and the only suppression channel is a user-maintained plain-text exclusion list stored in the project.
