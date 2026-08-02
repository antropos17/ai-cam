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

        // 0.5. Why: PROVISIONAL — Block 2.1 owns the evidence-camera scoring scale and ratifies
        // this number. It exists here now only because the PLAN contract forbids Core/ or
        // Evidence/ from referencing a threshold that is absent from this file.
        public const float DefaultMinEvidenceCoverageScore = 0.5f;

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

        // 7. Why: PROVISIONAL — docs/PLAN.md carries an unnarrowed 6–8 range and states the
        // single default is fixed in Block 1.4. 7 is the midpoint and resolves a bracket to
        // ~1/128 of its width; Block 1.4 ratifies or replaces it.
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

        // 128 candidates. Why: PROVISIONAL — Block 2.1 ratifies N and records it in
        // camera-plan.json. 128 Fibonacci-sphere points put neighbouring candidates roughly
        // 13 degrees apart, which is finer than the orthogonality constraint needs.
        public const int DefaultEvidenceCandidateCount = 128;

        // 9 rays. Why: docs/PLAN.md Block 2.1 fixes fractional occlusion at AABB centre plus
        // 8 corners; the score is hits/9 and is never binary.
        public const int DefaultEvidenceOcclusionRays = 9;

        // 0.25. Why: docs/PLAN.md Block 2.1 fixes the survivor filter — cameras 2–4 are
        // optimized only within the top 25% of candidates by score.
        public const float DefaultEvidenceTopScoreFraction = 0.25f;

        // 0.25. Why: PROVISIONAL — Block 2.1 ratifies the frame-edge penalty. A quarter weight
        // biases the winner away from the frame edge without letting centrality outrank
        // in-frustum coverage or occlusion.
        public const float DefaultWeightEvidenceCentrality = 0.25f;

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
        [Tooltip("Dimensionless. Below this best-candidate score the verdict is EVIDENCE COVERAGE: LOW.")]
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

        /// <summary>Block 2.1 honest-verdict gate.</summary>
        public float MinEvidenceCoverageScore => minEvidenceCoverageScore;

        public int EvidenceCandidateCount => evidenceCandidateCount;

        public int EvidenceOcclusionRays => evidenceOcclusionRays;

        public float EvidenceTopScoreFraction => evidenceTopScoreFraction;

        public float WeightEvidenceCentrality => weightEvidenceCentrality;

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
    }
}
