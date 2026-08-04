# Research record — placement-error detection (2026-08-04)

> **Provenance and standing of this file.** Everything below came from a research session on
> **2026-08-04**. None of it was independently re-verified inside this repository: no asset was
> installed, no competing product was run, no cited paper was reproduced, and no number here was
> measured on this project's Unity version or hardware. **Every specific number must be
> re-verified in this repository before any code depends on it.** This file is a record of what
> the research found and where it came from — it is not evidence, and it ratifies nothing. Each
> entry carries its source exactly as the research reported it; vague attributions are left vague
> on purpose and were not upgraded into precise-looking citations.
>
> Precedence unchanged: `docs/PLAN.md` wins for v0.1, ratified contracts
> (`docs/CONTRACT-2.2.1.md`, `docs/CONTRACT-2.2.2.md`, `docs/CONTRACT-2.2.3.md`) win over
> everything in this file. A contradiction between this file and a ratified contract = STOP and
> adjudicate, never guess.

## Competitive verdicts

- **Stan's Assets / KAPPS Scene Validator** — *source: Unity Asset Store, asset 132569, last
  updated 2019.* A hierarchy and component linter with one-click auto-resolution and per-component
  rule muting. It does not read geometry. **Verdict: adjacent niche, not a competitor.**

- **madsbangh/scene-validation** — *source: GitHub repository `madsbangh/scene-validation`.*
  Per-scene user validator scripts matched by a scene-path attribute; no releases; self-described
  as early in development. **Verdict: not a competitor.**

- **Odin Validator (Sirenix)** — *source: Sirenix Odin Validator product.* Scans serialized
  fields, attributes and asset references — not collider geometry. **Verdict: not a competitor.**

- **RealTransforms (Adam Napper)** — *source: coverage by 80.lv, 2025–2026.* Uses built-in PhysX
  for editor-time drop and arrange of props. It is a placement tool that prevents floating and
  clipping **during authoring**; it does not search for and report errors that already exist.
  **Verdict: adjacent niche.**

- **Unity Asset Store physics-drop placement assets** — *source: Unity Asset Store listings for
  Pro Drag & Drop + Grid Snap, Object Placement Tool, Kinetic Tools, Help me Place.* All of them
  are placement, not detection. **Verdict: adjacent niche.**

- **modl.ai** — *source: modl.ai product description.* Uses AI exploratory bots plus VLM
  screenshot analysis **at runtime in a build** to find geometry bugs such as holes in level
  geometry. **Verdict: opposite philosophy** — we allow no AI in the measurement core — **and it
  is not editor-time.**

- **Unity Physics Debugger (Window > Analysis)** — *source: Unity Editor built-in tooling.*
  Visualizes colliders and contacts only. It produces no detection and no report.

- **Unreal Map Check** — *source: Unreal Engine built-in Map Check tooling.* Detects duplicate
  light GUIDs, contradictory static-plus-physics flags, zero DrawScale and deprecated actors, but
  does not compute geometric penetration and does not find floating objects. **The positional gap
  exists even in Unreal.**

- **CONCLUSION RECORDED BY THE RESEARCH SESSION** — *source: the 2026-08-04 research session's own
  synthesis over the verdicts above.* **No verified product performs deterministic editor-time
  detection of placement errors that measures penetration in millimetres, runs physics to test
  settling, and points a camera at the finding with a numeric report.** Standing of this claim: it
  is a negative result over the products surveyed above, not a proof of absence across the market;
  per `docs/SPEC.md` §3 the language policy forbids a public "first ever" claim regardless.

## Borrowed industry patterns

- **Autodesk Navisworks and BIM clash-detection practice** — *source: Autodesk Navisworks and
  general BIM clash-detection practice; the tolerance figure is industry guidance, not a measured
  value.* The practice separates **hard clash** from **clearance clash** and from **workflow
  clash**; applies a numeric tolerance (industry guidance commonly **0.001 to 0.01 m** for hard
  clash); groups repeated clashes into single issues; and applies ignore rules for same-file and
  same-layer pairs. BIM practice explicitly treats an enormous clash report as a **process
  failure** rather than a finding. **This is the false-positive suppression layer BugCam lacks.**

- **Bethesda Creation Kit** — *source: Bethesda Creation Kit and long-standing modding practice.*
  There is a per-object flag **Don't Havok Settle**; when it is not set, the object is dropped by
  physics on load so that it comes to rest correctly, and a badly placed object ends up below an
  accessible surface. Modders have been performing our Settle Test **manually for over a decade**,
  which means the mental model already exists in the target audience and **should be reused as
  naming, not reinvented.**

- **Valve Source Hammer** — *source: Valve Source engine / Hammer editor VBSP compile behaviour
  and its documented leak workflow.* VBSP emits **`leaked!`** during compile when the level is not
  sealed against the void, and generates a **pointfile (`.lin`/`.pts`)** drawing a line from the
  leaking entity through the gap to the outside; the documented workflow is to follow that line to
  find the gap. This is a reusable model **both** for gap detection **and** for explaining to the
  user where to look — which maps onto our camera placement.

## Mathematical foundations

- **PhyScene** — *source: Yang, Jia, Zhi, Huang; CVPR 2024 Highlight; arXiv:2404.09465.* Defines
  collision rate **Col_obj** and **Col_scene**, out-of-floor-plan rate **R_out**, plus
  **R_walkable** and **R_reach**. Reported bedroom results: Col_obj **0.187** and Col_scene
  **0.36**, versus ATISS **0.248** and **0.46**.

- **PhyMix** — *source: arXiv 2604.10125 (recorded exactly as the research session reported the
  identifier).* Defines violation rates for **collision, floating, unanchored, static instability,
  dynamic instability, misorientation, scale instability and unreachable navigation**.

- **PhyRecon** — *source: arXiv:2404.16666.* Computes a **Stability Ratio** via dropping
  simulation.

- **Consequence recorded for the three above** — *source: the 2026-08-04 research session's
  synthesis.* These are **published numeric definitions of exactly the bug classes in our
  placement catalogue** and should be **adopted as our definitions rather than invented.**

- **Static stability criterion** — *source: standard rigid-body statics; the rigorous treatment
  cited is Whiting, Ochsendorf and Durand, "Procedural Modeling of Structurally-Sound Masonry
  Buildings", ACM TOG 28(5) article 112, SIGGRAPH Asia 2009, DOI 10.1145/1618452.1618458.* A body
  is statically stable when the projection of its centre of mass lies inside the convex hull of
  its contact points (the **support polygon**); this is a **necessary, not sufficient**, condition.
  The cited paper solves interface forces under equilibrium, friction-cone and compression-only
  constraints for stacked rigid blocks. **This gives a stability margin in millimetres without
  running physics.**

- **Penetration depth** — *source: standard GJK/EPA literature, plus Wei Gao, "Efficient
  Incremental Penetration Depth Estimation between Convex Geometries", arXiv:2304.07357, IROS
  2024.* **GJK** finds the closest point in the Minkowski difference but **does not yield depth on
  penetration**; **EPA** is the de facto standard for expanding the terminating simplex into
  depth. The cited paper reports a **5× to 30× speedup over EPA** using warm-started
  spatio-temporal coherence.

- **Amplification ↔ finite-time Lyapunov exponent** — *source: standard dynamical-systems
  literature on finite-time Lyapunov exponents.* Our amplification metric corresponds to the
  **finite-time Lyapunov exponent**, `Λ = (1/T) · ln(‖final perturbation‖ / ‖initial
  perturbation‖)`. The literature is explicit that finite-time exponents **fluctuate**, **depend
  on the reference trajectory**, and **do not converge to the asymptotic Lyapunov exponent over a
  short window**. Therefore the metric **must always be labelled finite-time and never presented
  as the Lyapunov exponent without that qualifier.**

## Hard constraints discovered

- **`Physics.ComputePenetration` returns untrustworthy raw values** — *source: a documented Unity
  Issue Tracker defect, plus a community workaround; no issue number was recorded by the research
  session.* A capsule against a non-convex mesh returns an **absurd depth value of 743.8444** for a
  shallow overlap, or returns **false** at small depth. The community workaround is to call the
  function **16 or more times** at slightly varied positions. **Cost implication: the flagship
  static detector may be up to 16× more expensive than assumed, and raw values must be sanitized
  rather than trusted.**

- **PhysX determinism is a within-platform claim** — *source: PhysX determinism guarantees, with
  the cross-vendor divergence reported on GameDev.net.* Determinism holds for **identical hardware
  and identical PhysX version**; results **diverge across CPU vendors** (Intel versus AMD,
  reported on GameDev.net) and due to **GPU operation ordering**. **Bit-identical repeatability is
  a within-platform claim only.**
