using System;

namespace BugCam.Core
{
    /// <summary>
    /// Plain-struct view of Block 1.4 search numbers from <see cref="DivergenceSettings"/>.
    /// Adds no defaults of its own — every value originates in DivergenceSettings.
    /// </summary>
    public readonly struct EpsilonSearchSettings
    {
        public EpsilonSearchSettings(
            float epsilonStartMetres,
            float epsilonGrowthFactor,
            float epsilonCeilingMetres,
            int bisectionIterations,
            int ladderPointCount,
            float[] fanMultipliers)
        {
            EpsilonStartMetres = epsilonStartMetres;
            EpsilonGrowthFactor = epsilonGrowthFactor;
            EpsilonCeilingMetres = epsilonCeilingMetres;
            BisectionIterations = bisectionIterations;
            LadderPointCount = ladderPointCount;
            FanMultipliers = fanMultipliers ?? Array.Empty<float>();
        }

        /// <summary>Metres. First magnitude of the search range.</summary>
        public float EpsilonStartMetres { get; }

        public float EpsilonGrowthFactor { get; }

        /// <summary>Metres. Upper bound of the search range.</summary>
        public float EpsilonCeilingMetres { get; }

        public int BisectionIterations { get; }

        public int LadderPointCount { get; }

        public float[] FanMultipliers { get; }

        /// <summary>
        /// Characterization may probe up to 1.2 × ceiling via the fan; search range stays
        /// [EpsilonStart, EpsilonCeiling].
        /// </summary>
        public float CharacterizationCeilingMetres
        {
            get
            {
                var maxMultiplier = 0f;
                for (var i = 0; i < FanMultipliers.Length; i++)
                {
                    if (FanMultipliers[i] > maxMultiplier)
                    {
                        maxMultiplier = FanMultipliers[i];
                    }
                }

                return EpsilonCeilingMetres * maxMultiplier;
            }
        }

        /// <summary>Code defaults matching <see cref="DivergenceSettings"/> field defaults.</summary>
        public static EpsilonSearchSettings Default =>
            new EpsilonSearchSettings(
                DivergenceSettings.DefaultEpsilonStart,
                DivergenceSettings.DefaultEpsilonGrowthFactor,
                DivergenceSettings.DefaultEpsilonCeiling,
                DivergenceSettings.DefaultBisectionIterations,
                DivergenceSettings.DefaultLadderPointCount,
                new[] { 0.8f, 0.9f, 1f, 1.1f, 1.2f });

        public static EpsilonSearchSettings FromDivergenceSettings(DivergenceSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var source = settings.FanMultipliers;
            var copy = new float[source.Length];
            Array.Copy(source, copy, source.Length);
            return new EpsilonSearchSettings(
                settings.EpsilonStart,
                settings.EpsilonGrowthFactor,
                settings.EpsilonCeiling,
                settings.BisectionIterations,
                settings.LadderPointCount,
                copy);
        }

        /// <summary>
        /// Empty string when usable; otherwise the reason this configuration cannot be used.
        /// </summary>
        public string Validate()
        {
            if (!IsPositiveFinite(EpsilonStartMetres))
            {
                return "EpsilonStart must be a positive finite number of metres.";
            }

            if (!IsPositiveFinite(EpsilonCeilingMetres))
            {
                return "EpsilonCeiling must be a positive finite number of metres.";
            }

            if (EpsilonCeilingMetres <= EpsilonStartMetres)
            {
                return "EpsilonCeiling must be greater than EpsilonStart.";
            }

            if (!(EpsilonGrowthFactor > 1f) ||
                float.IsNaN(EpsilonGrowthFactor) ||
                float.IsInfinity(EpsilonGrowthFactor))
            {
                return "EpsilonGrowthFactor must be a finite number greater than 1.";
            }

            if (BisectionIterations < 1)
            {
                return "BisectionIterations must be at least 1.";
            }

            if (LadderPointCount < 2)
            {
                return "LadderPointCount must be at least 2.";
            }

            if (FanMultipliers == null || FanMultipliers.Length == 0)
            {
                return "FanMultipliers must contain at least one multiplier.";
            }

            for (var i = 0; i < FanMultipliers.Length; i++)
            {
                if (!IsPositiveFinite(FanMultipliers[i]))
                {
                    return "Every FanMultipliers entry must be a positive finite number.";
                }
            }

            return string.Empty;
        }

        private static bool IsPositiveFinite(float value)
        {
            return value > 0f && !float.IsInfinity(value) && !float.IsNaN(value);
        }
    }

    /// <summary>
    /// How the exponential phase walks the search range after the ladder accepts monotonicity.
    /// Convergence VERIFY compares these strategies on the same axis / scene / body / config.
    /// </summary>
    public enum EpsilonSearchStrategy
    {
        /// <summary>Ascend from <see cref="EpsilonSearchSettings.EpsilonStartMetres"/> × growth.</summary>
        AscendFromStart = 0,

        /// <summary>Ascend from a caller-supplied start × growth (e.g. 0.02 mm).</summary>
        AscendFromCustomStart = 1,

        /// <summary>Descend from <see cref="EpsilonSearchSettings.EpsilonCeilingMetres"/> ÷ growth.</summary>
        DescendFromCeiling = 2
    }
}
