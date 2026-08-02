namespace BugCam.Core
{
    /// <summary>
    /// Plain-struct view of the numbers <see cref="DivergenceEngine"/> actually reads.
    /// Every value originates in <see cref="DivergenceSettings"/> — this struct adds no
    /// numbers of its own, so the "one home for all thresholds" rule still holds.
    /// </summary>
    public readonly struct DivergenceThresholds
    {
        public DivergenceThresholds(
            float perBodyPositionThreshold,
            float perBodyRotationThreshold,
            float perBodyVelocityThreshold,
            float sceneScoreThreshold,
            int sustainedSteps,
            float weightPosition,
            float weightRotation,
            float weightVelocity,
            float weightSleep)
        {
            PerBodyPositionThreshold = perBodyPositionThreshold;
            PerBodyRotationThreshold = perBodyRotationThreshold;
            PerBodyVelocityThreshold = perBodyVelocityThreshold;
            SceneScoreThreshold = sceneScoreThreshold;
            SustainedSteps = sustainedSteps;
            WeightPosition = weightPosition;
            WeightRotation = weightRotation;
            WeightVelocity = weightVelocity;
            WeightSleep = weightSleep;
        }

        /// <summary>Metres.</summary>
        public float PerBodyPositionThreshold { get; }

        /// <summary>Degrees.</summary>
        public float PerBodyRotationThreshold { get; }

        /// <summary>Metres per second.</summary>
        public float PerBodyVelocityThreshold { get; }

        public float SceneScoreThreshold { get; }

        public int SustainedSteps { get; }

        public float WeightPosition { get; }

        public float WeightRotation { get; }

        public float WeightVelocity { get; }

        public float WeightSleep { get; }

        /// <summary>The code defaults, without allocating a ScriptableObject.</summary>
        public static DivergenceThresholds Default =>
            new DivergenceThresholds(
                DivergenceSettings.DefaultPerBodyPositionThreshold,
                DivergenceSettings.DefaultPerBodyRotationThreshold,
                DivergenceSettings.DefaultPerBodyVelocityThreshold,
                DivergenceSettings.DefaultSceneScoreThreshold,
                DivergenceSettings.DefaultSustainedSteps,
                DivergenceSettings.DefaultWeightPosition,
                DivergenceSettings.DefaultWeightRotation,
                DivergenceSettings.DefaultWeightVelocity,
                DivergenceSettings.DefaultWeightSleep);

        /// <summary>
        /// Empty string when usable; otherwise the reason this configuration cannot be used.
        /// </summary>
        public string Validate()
        {
            if (!IsPositiveFinite(PerBodyPositionThreshold))
            {
                return "PerBodyPositionThreshold must be a positive finite number of metres.";
            }

            if (!IsPositiveFinite(PerBodyRotationThreshold))
            {
                return "PerBodyRotationThreshold must be a positive finite number of degrees.";
            }

            if (!IsPositiveFinite(PerBodyVelocityThreshold))
            {
                return "PerBodyVelocityThreshold must be a positive finite number of m/s.";
            }

            if (!IsNonNegativeFinite(SceneScoreThreshold))
            {
                return "SceneScoreThreshold must be a non-negative finite number.";
            }

            if (SustainedSteps <= 0)
            {
                return "SustainedSteps must be greater than zero.";
            }

            if (!IsNonNegativeFinite(WeightPosition) ||
                !IsNonNegativeFinite(WeightRotation) ||
                !IsNonNegativeFinite(WeightVelocity) ||
                !IsNonNegativeFinite(WeightSleep))
            {
                return "Every divergence weight must be a non-negative finite number.";
            }

            return string.Empty;
        }

        private static bool IsPositiveFinite(float value)
        {
            return value > 0f && !float.IsInfinity(value) && !float.IsNaN(value);
        }

        private static bool IsNonNegativeFinite(float value)
        {
            return value >= 0f && !float.IsInfinity(value) && !float.IsNaN(value);
        }
    }
}
