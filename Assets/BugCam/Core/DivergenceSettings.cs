using UnityEngine;

namespace BugCam.Core
{
    /// <summary>
    /// The single home for every BugCam threshold and weight, per the
    /// "all thresholds/weights live in one serializable DivergenceSettings asset"
    /// rule in CLAUDE.md. Field names and units mirror the DivergenceSettings field
    /// contract in docs/PLAN.md Block 1.3 exactly. Defaults live in code — there is no
    /// hand-written .asset YAML.
    /// All lengths are metres; millimetres exist only at the display layer.
    /// </summary>
    [CreateAssetMenu(
        fileName = "DivergenceSettings",
        menuName = "BugCam/Divergence Settings",
        order = 0)]
    public sealed class DivergenceSettings : ScriptableObject
    {
        // ---------------------------------------------------------------------------------
        // Block 1.3 — divergence detection
        // ---------------------------------------------------------------------------------

        // 1e-3 m (1 mm). Why: 1000x the 1e-6 m repeatability gate that Block 1.1 measured at
        // maxComponentDelta == 0 on identical runs, so identical-run numerical noise can never
        // satisfy the "meaningfully affected" condition.
        public const float DefaultPerBodyPositionThreshold = 1e-3f;

        // 1 degree. Why: the smallest orientation difference that is unambiguous in a replay
        // frame; the Block 1.1 A/A' probe produced 0 quaternion delta, so this sits far above
        // observed float noise.
        public const float DefaultPerBodyRotationThreshold = 1f;

        // 0.05 m/s. Why: DefaultPerBodyPositionThreshold (1 mm) accumulated over exactly one
        // BugCamConstants.FixedStep (0.02 s) is 0.05 m/s, so the position and velocity gates
        // describe the same physical event instead of two unrelated ones.
        public const float DefaultPerBodyVelocityThreshold = 0.05f;

        // 1.0. Why: the Scene Divergence Score is the weighted sum of normalized per-body terms,
        // so 1.0 means one tracked body has reached a full normalization unit — its own size in
        // position, or twice the rotation/velocity threshold at the default weights. The Block
        // 1.1 identical-run noise floor (1e-6 m over 49 bodies) sums to ~1e-4, four orders of
        // magnitude below this, so the gate cannot fire on numerical noise. A mean-based gate was
        // rejected: it hides the first one or two bodies that move, which is exactly the moment
        // the product exists to find.
        public const float DefaultSceneScoreThreshold = 1f;

        // 5 steps = 0.1 s at the fixed step. Why: fixed by docs/PLAN.md Block 1.3; long enough
        // that a single-frame solver blip can never be reported as divergence.
        public const int DefaultSustainedSteps = 5;

        // 1.0. Why: position is the quantity the product actually reports (spread in metres),
        // so it carries full weight and every other term is scaled relative to it.
        public const float DefaultWeightPosition = 1f;

        // 0.5. Why: rotation leads position when a body starts to topple, but it is not the
        // reported outcome; at half weight it cannot alone push the scene score over the gate.
        public const float DefaultWeightRotation = 0.5f;

        // 0.5. Why: velocity also leads position by a few steps; same reasoning as rotation —
        // a leading indicator must not be able to declare divergence on its own.
        public const float DefaultWeightVelocity = 0.5f;

        // 1.0. Why: a sleeping/awake flip is a discrete outcome change rather than numeric
        // drift, so it counts as much as a full position-threshold breach.
        public const float DefaultWeightSleep = 1f;

        // 0.25. Why: RE-RATIFIED in the 2026-08-03 live calibration. The gate now compares
        // camera 1's OCCLUSION COVERAGE — the fraction of unoccluded sample points averaged over
        // affected bodies (VisibilityScore / AffectedBodyCount, 0..1) — never the total ranking
        // score. The previous total-score gate at 0.5 was degenerate: the in-frustum term alone
        // contributed ~0.83–0.98 per body, so a candidate with visibility 0.00 (measured live:
        // candidate 0, one white face filling the frame, zero affected bodies visible) passed as
        // OK. Calibrated data on the dense 49-body tower: good cameras cluster at 0.354–0.376
        // coverage, the fully-occluded one at 0.000. 0.25 sits at roughly two thirds of the
        // best-known-good coverage — LOW fires decisively for occluded shots while a one-third
        // visible dense-scene shot still passes.
        public const float DefaultMinEvidenceCoverageScore = 0.25f;

        // ---------------------------------------------------------------------------------
        // Block 1.4 — adaptive epsilon search
        // ---------------------------------------------------------------------------------

        // 1e-5 m (0.01 mm). Why: docs/PLAN.md Block 1.4 fixes the first magnitude of the
        // exponential search at 0.01 mm.
        public const float DefaultEpsilonStart = 1e-5f;

        // 2. Why: docs/PLAN.md Block 1.4 fixes the exponential multiplier at x2, which reaches
        // the 10 mm ceiling from 0.01 mm in 10 steps.
        public const float DefaultEpsilonGrowthFactor = 2f;

        // 1e-2 m (10 mm). Why: docs/PLAN.md Block 1.4 fixes the upper bound of the tested
        // range; above it the honest verdict is STABLE WITHIN TESTED RANGE.
        public const float DefaultEpsilonCeiling = 1e-2f;

        // 7. Why: docs/PLAN.md Block 1.4 fixes BisectionIterations = 7 (prior unnarrowed
        // 6–8 range narrowed). 7 resolves a bracket to ~1/128 of its width.
        public const int DefaultBisectionIterations = 7;

        // 12. Why: docs/PLAN.md Block 1.4 fixes a 12-point log-uniform ladder from 0.01 mm to
        // 10 mm, run before bisection is trusted, so non-monotonicity shows up as data.
        public const int DefaultLadderPointCount = 12;

        // ---------------------------------------------------------------------------------
        // Blocks 1.5 / 2.1 — evidence
        // ---------------------------------------------------------------------------------

        // 10 bodies. Why: docs/PLAN.md Block 1.5 caps ghosts at the top-10 diverging bodies
        // plus baseline, which is the stated mitigation for "800-line ghost spaghetti".
        public const int DefaultGhostBodyLimit = 10;

        // 128 candidates. Why: RATIFIED in Block 2.1 and recorded in camera-plan.json. 128
        // Fibonacci-sphere points put neighbouring candidates roughly 13 degrees apart, which is
        // finer than the orthogonality constraint needs, while keeping the O(N * AffectedBodyCount
        // * EvidenceOcclusionRays) scoring pass cheap enough to run synchronously.
        public const int DefaultEvidenceCandidateCount = 128;

        // 9 rays. Why: docs/PLAN.md Block 2.1 fixes fractional occlusion at AABB centre plus
        // 8 corners; the score is hits/9 and is never binary.
        public const int DefaultEvidenceOcclusionRays = 9;

        // 0.25. Why: docs/PLAN.md Block 2.1 fixes the survivor filter — cameras 2–4 are
        // optimized only within the top 25% of candidates by score.
        public const float DefaultEvidenceTopScoreFraction = 0.25f;

        // 0.25. Why: RATIFIED in Block 2.1. A quarter weight biases the winner away from the
        // frame edge without letting centrality outrank in-frustum coverage or occlusion, whose
        // per-body terms are each in the 0..1 range.
        public const float DefaultWeightEvidenceCentrality = 0.25f;

        // ---------------------------------------------------------------------------------
        // Block 2.1 — evidence camera geometry (EvidenceCameras.cs)
        // ---------------------------------------------------------------------------------

        // 50 degrees vertical FOV. Why: wide enough to frame a multi-body event from
        // EvidenceEventBoundsRadiusMultiplier distance without excessive wide-angle distortion;
        // matches common third-person framing rather than a telephoto or fisheye extreme.
        public const float DefaultEvidenceCameraVerticalFovDegrees = 50f;

        // 0.05 m near clip. Why: TowerScene bodies are ~1 m cubes and candidates can sit close to
        // the event bounds; 5 cm keeps near-plane clipping from ever discarding a body that is
        // genuinely in view.
        public const float DefaultEvidenceCameraNearClip = 0.05f;

        // 500 m far clip. Why: far beyond any plausible TowerScene extent (49 bodies within a few
        // metres of the origin), so the far plane never participates in the frustum test.
        public const float DefaultEvidenceCameraFarClip = 500f;

        // 1920x1080. Why: canonical single aspect ratio used only to convert viewport fractions
        // into a pixel-space screen separation and centrality measurement, matching the 1920x1080
        // landscape export target docs/PLAN.md Block 2.2 already commits to. Using one fixed
        // resolution (not the Editor Game View size) keeps candidate scoring reproducible on any
        // machine, per docs/PLAN.md Block 2.1 VERIFY.
        public const int DefaultEvidenceRenderWidth = 1920;

        public const int DefaultEvidenceRenderHeight = 1080;

        // 2.5x. Why: candidate distance from the divergence-event bounds center is
        // bounds.extents.magnitude * this multiplier. 2.5x keeps the whole event bounds sphere
        // inside frame at DefaultEvidenceCameraVerticalFovDegrees with margin for the body extents
        // added on top of each AABB, without pushing every candidate so far out that occlusion by
        // nearby bodies becomes physically implausible.
        public const float DefaultEvidenceEventBoundsRadiusMultiplier = 2.5f;

        // REMOVED in the 2026-08-03 live calibration (no dead weights may remain):
        // WeightCameraOrthogonality/WeightContactProximity/WeightTrajectoryAlignment (100/10/1)
        // and ScreenSpaceSeparationNormalizer (500). Measured on the live tower run: contact
        // proximity was constant across ALL candidates by construction (every candidate sits on
        // the same sphere radius, 1/(1+10.92)=0.0839 each), trajectory alignment never influenced
        // a single winner (identical winners with weight 1 vs 0; adjacent orthogonality rank gaps
        // >= 3 units vs a <= 1 trajectory range), and screen-space separation contributed
        // 0.00036–0.00074 total (~0.29 px over 21 bodies) — 4–5 orders below the other terms,
        // because the offset at the first sustained divergence is ~1 mm, sub-pixel at 1920 from
        // ~11 m. Cameras 2–4 rank by orthogonality to camera 1 alone (a single scale factor on a
        // single term cannot change an ordering, so it carries no weight field either). Close-up
        // contact cameras (SPEC §7) need contact data + multi-radius candidates — backlog.

        [Header("Block 1.3 — per-body gates")]
        [SerializeField]
        [Tooltip("Metres. The \"meaningfully affected\" condition for a single tracked body.")]
        private float perBodyPositionThreshold = DefaultPerBodyPositionThreshold;

        [SerializeField]
        [Tooltip("Degrees. Per-body rotation gate; also normalizes the rotation error term.")]
        private float perBodyRotationThreshold = DefaultPerBodyRotationThreshold;

        [SerializeField]
        [Tooltip("Metres per second. Per-body velocity gate; also normalizes the velocity term.")]
        private float perBodyVelocityThreshold = DefaultPerBodyVelocityThreshold;

        [Header("Block 1.3 — scene score")]
        [SerializeField]
        [Tooltip("Dimensionless. Weighted-sum gate for the Scene Divergence Score.")]
        private float sceneScoreThreshold = DefaultSceneScoreThreshold;

        [SerializeField]
        [Tooltip("Consecutive physics steps a divergence must persist before it is reported.")]
        private int sustainedSteps = DefaultSustainedSteps;

        [SerializeField]
        private float weightPosition = DefaultWeightPosition;

        [SerializeField]
        private float weightRotation = DefaultWeightRotation;

        [SerializeField]
        private float weightVelocity = DefaultWeightVelocity;

        [SerializeField]
        private float weightSleep = DefaultWeightSleep;

        [Header("Block 1.4 — adaptive epsilon search")]
        [SerializeField]
        [Tooltip("Metres. First magnitude of the exponential search.")]
        private float epsilonStart = DefaultEpsilonStart;

        [SerializeField]
        private float epsilonGrowthFactor = DefaultEpsilonGrowthFactor;

        [SerializeField]
        [Tooltip("Metres. Upper bound of the tested range.")]
        private float epsilonCeiling = DefaultEpsilonCeiling;

        [SerializeField]
        private int bisectionIterations = DefaultBisectionIterations;

        [SerializeField]
        [Tooltip("Log-uniform monotonicity ladder points run before bisection is trusted.")]
        private int ladderPointCount = DefaultLadderPointCount;

        [SerializeField]
        [Tooltip("Multipliers of the found threshold; 5 multipliers x 3 axes = 15 fan runs.")]
        private float[] fanMultipliers = { 0.8f, 0.9f, 1f, 1.1f, 1.2f };

        [Header("Blocks 1.5 / 2.1 — evidence")]
        [SerializeField]
        [Tooltip("Top-N diverging bodies drawn as ghosts, plus baseline.")]
        private int ghostBodyLimit = DefaultGhostBodyLimit;

        [SerializeField]
        [Tooltip("0..1. Camera 1 occlusion coverage (visible sample-point fraction per affected body) below this is EVIDENCE COVERAGE: LOW.")]
        private float minEvidenceCoverageScore = DefaultMinEvidenceCoverageScore;

        [SerializeField]
        [Tooltip("Fibonacci-sphere candidate count; recorded in camera-plan.json.")]
        private int evidenceCandidateCount = DefaultEvidenceCandidateCount;

        [SerializeField]
        [Tooltip("AABB centre + 8 corners; fractional occlusion = hits / rays.")]
        private int evidenceOcclusionRays = DefaultEvidenceOcclusionRays;

        [SerializeField]
        [Tooltip("Survivor filter applied before optimizing cameras 2-4.")]
        private float evidenceTopScoreFraction = DefaultEvidenceTopScoreFraction;

        [SerializeField]
        [Tooltip("Frame-edge penalty weight in evidence candidate scoring.")]
        private float weightEvidenceCentrality = DefaultWeightEvidenceCentrality;

        [Header("Block 2.1 — evidence camera geometry")]
        [SerializeField]
        [Tooltip("Degrees. Vertical field of view used to build each candidate's virtual camera.")]
        private float evidenceCameraVerticalFovDegrees = DefaultEvidenceCameraVerticalFovDegrees;

        [SerializeField]
        [Tooltip("Metres. Near clip plane for candidate frustum construction.")]
        private float evidenceCameraNearClip = DefaultEvidenceCameraNearClip;

        [SerializeField]
        [Tooltip("Metres. Far clip plane for candidate frustum construction.")]
        private float evidenceCameraFarClip = DefaultEvidenceCameraFarClip;

        [SerializeField]
        [Tooltip("Pixels. Canonical render width used only for pixel-space scoring.")]
        private int evidenceRenderWidth = DefaultEvidenceRenderWidth;

        [SerializeField]
        [Tooltip("Pixels. Canonical render height used only for pixel-space scoring.")]
        private int evidenceRenderHeight = DefaultEvidenceRenderHeight;

        [SerializeField]
        [Tooltip("Candidate distance from event bounds center = bounds.extents.magnitude x this.")]
        private float evidenceEventBoundsRadiusMultiplier = DefaultEvidenceEventBoundsRadiusMultiplier;

        /// <summary>Metres. The "meaningfully affected" condition.</summary>
        public float PerBodyPositionThreshold => perBodyPositionThreshold;

        /// <summary>Degrees. Per-body rotation gate.</summary>
        public float PerBodyRotationThreshold => perBodyRotationThreshold;

        /// <summary>Metres per second. Per-body velocity gate.</summary>
        public float PerBodyVelocityThreshold => perBodyVelocityThreshold;

        /// <summary>Dimensionless weighted-sum gate.</summary>
        public float SceneScoreThreshold => sceneScoreThreshold;

        /// <summary>Consecutive physics steps required.</summary>
        public int SustainedSteps => sustainedSteps;

        public float WeightPosition => weightPosition;

        public float WeightRotation => weightRotation;

        public float WeightVelocity => weightVelocity;

        public float WeightSleep => weightSleep;

        /// <summary>Metres. First magnitude of the exponential search.</summary>
        public float EpsilonStart => epsilonStart;

        public float EpsilonGrowthFactor => epsilonGrowthFactor;

        /// <summary>Metres. Upper bound of the tested range.</summary>
        public float EpsilonCeiling => epsilonCeiling;

        public int BisectionIterations => bisectionIterations;

        public int LadderPointCount => ladderPointCount;

        /// <summary>Multipliers of the threshold used to build the fan.</summary>
        public float[] FanMultipliers => fanMultipliers;

        public int GhostBodyLimit => ghostBodyLimit;

        /// <summary>
        /// Block 2.1 honest-verdict gate over camera 1's occlusion coverage — the fraction of
        /// unoccluded sample points averaged over affected bodies. Distinct from the ranking score.
        /// </summary>
        public float MinEvidenceCoverageScore => minEvidenceCoverageScore;

        public int EvidenceCandidateCount => evidenceCandidateCount;

        public int EvidenceOcclusionRays => evidenceOcclusionRays;

        public float EvidenceTopScoreFraction => evidenceTopScoreFraction;

        public float WeightEvidenceCentrality => weightEvidenceCentrality;

        /// <summary>Degrees. Vertical FOV for candidate virtual cameras.</summary>
        public float EvidenceCameraVerticalFovDegrees => evidenceCameraVerticalFovDegrees;

        /// <summary>Metres. Near clip plane for candidate frustum construction.</summary>
        public float EvidenceCameraNearClip => evidenceCameraNearClip;

        /// <summary>Metres. Far clip plane for candidate frustum construction.</summary>
        public float EvidenceCameraFarClip => evidenceCameraFarClip;

        /// <summary>Pixels. Canonical render width for pixel-space scoring only.</summary>
        public int EvidenceRenderWidth => evidenceRenderWidth;

        /// <summary>Pixels. Canonical render height for pixel-space scoring only.</summary>
        public int EvidenceRenderHeight => evidenceRenderHeight;

        /// <summary>Candidate distance multiplier applied to the event bounds extents magnitude.</summary>
        public float EvidenceEventBoundsRadiusMultiplier => evidenceEventBoundsRadiusMultiplier;

        /// <summary>
        /// A fresh in-memory instance carrying the code defaults. Used by tests and by any
        /// caller that has not been handed a project asset.
        /// </summary>
        public static DivergenceSettings CreateDefault()
        {
            return CreateInstance<DivergenceSettings>();
        }

        /// <summary>
        /// The plain-struct view the Core engine consumes, so DivergenceEngine never depends on
        /// a UnityEngine.Object instance being alive.
        /// </summary>
        public DivergenceThresholds ToThresholds()
        {
            return new DivergenceThresholds(
                perBodyPositionThreshold,
                perBodyRotationThreshold,
                perBodyVelocityThreshold,
                sceneScoreThreshold,
                sustainedSteps,
                weightPosition,
                weightRotation,
                weightVelocity,
                weightSleep);
        }

        /// <summary>
        /// Plain-struct view of Block 1.4 adaptive epsilon search fields.
        /// </summary>
        public EpsilonSearchSettings ToSearchSettings()
        {
            return EpsilonSearchSettings.FromDivergenceSettings(this);
        }

        /// <summary>
        /// Empty string when Block 1.4 search fields are usable; otherwise the validation reason.
        /// </summary>
        public string ValidateSearchSettings()
        {
            return ToSearchSettings().Validate();
        }
    }
}
