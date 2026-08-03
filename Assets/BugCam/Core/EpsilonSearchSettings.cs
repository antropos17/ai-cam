using System;

namespace BugCam.Core
{
    /// <summary>
    /// Plain-struct view of Block 1.4 search numbers from <see cref="DivergenceSettings"/>.
    /// Adds no defaults of its own — every value originates in DivergenceSettings.
    /// </summary>
    public readonly struct EpsilonSearchSettings
    {
        /// <summary>Block 1.4 requires a fixed 12-point log-uniform ladder.</summary>
        public const int RequiredLadderPointCount = 12;

        /// <summary>Block 1.4 requires exactly five fan multipliers × three axes = 15 fan runs.</summary>
        public const int RequiredFanMultiplierCount = 5;

        /// <summary>5 multipliers × X/Y/Z.</summary>
        public const int RequiredFanRunCount = 15;

        /// <summary>
        /// Canonical ordered fan multipliers. Compared with exact IEEE-754 <c>float ==</c>
        /// against these same code constants — no tolerance — because they are discrete
        /// contract tokens, not measured values.
        /// </summary>
        private static readonly float[] CanonicalFanMultipliers =
        {
            0.8f,
            0.9f,
            1f,
            1.1f,
            1.2f
        };

        /// <summary>Defensive copy of the canonical fan multiplier sequence.</summary>
        public static float[] RequiredFanMultipliers => (float[])CanonicalFanMultipliers.Clone();

        private readonly float[] _fanMultipliers;

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
            // Defensive copy so caller mutation of the input array cannot alter a validated settings instance.
            _fanMultipliers = fanMultipliers == null || fanMultipliers.Length == 0
                ? Array.Empty<float>()
                : (float[])fanMultipliers.Clone();
        }

        /// <summary>Metres. First magnitude of the search range.</summary>
        public float EpsilonStartMetres { get; }

        public float EpsilonGrowthFactor { get; }

        /// <summary>Metres. Upper bound of the search range.</summary>
        public float EpsilonCeilingMetres { get; }

        public int BisectionIterations { get; }

        public int LadderPointCount { get; }

        /// <summary>Defensive copy of configured fan multipliers (never a live mutable shared array).</summary>
        public float[] FanMultipliers =>
            _fanMultipliers.Length == 0
                ? Array.Empty<float>()
                : (float[])_fanMultipliers.Clone();

        /// <summary>
        /// Characterization may probe up to 1.2 × ceiling via the fan; search range stays
        /// [EpsilonStart, EpsilonCeiling].
        /// </summary>
        public float CharacterizationCeilingMetres
        {
            get
            {
                // Use private storage + canonical max so exposed copies cannot drift reporting.
                if (HasRequiredFanMultipliers(_fanMultipliers))
                {
                    return EpsilonCeilingMetres * CanonicalFanMultipliers[RequiredFanMultiplierCount - 1];
                }

                var maxMultiplier = 0f;
                for (var i = 0; i < _fanMultipliers.Length; i++)
                {
                    if (_fanMultipliers[i] > maxMultiplier)
                    {
                        maxMultiplier = _fanMultipliers[i];
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
                RequiredLadderPointCount,
                RequiredFanMultipliers);

        public static EpsilonSearchSettings FromDivergenceSettings(DivergenceSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var source = settings.FanMultipliers;
            var copy = source == null ? null : (float[])source.Clone();
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

            if (LadderPointCount != RequiredLadderPointCount)
            {
                return "LadderPointCount must be exactly " + RequiredLadderPointCount + ".";
            }

            if (!HasRequiredFanMultipliers(_fanMultipliers))
            {
                return "FanMultipliers must be exactly {0.8, 0.9, 1.0, 1.1, 1.2} in that order.";
            }

            return string.Empty;
        }

        /// <summary>
        /// Exact ordered match against the canonical fan multipliers using IEEE-754
        /// <c>float ==</c> (no tolerance). Rejects null, wrong length, reorder, duplicates,
        /// altered values, non-finite, and non-positive entries.
        /// </summary>
        public static bool HasRequiredFanMultipliers(float[] values)
        {
            if (values == null || values.Length != RequiredFanMultiplierCount)
            {
                return false;
            }

            for (var i = 0; i < RequiredFanMultiplierCount; i++)
            {
                var value = values[i];
                if (!IsPositiveFinite(value) || value != CanonicalFanMultipliers[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Canonical multiplier at index; used by search fan generation (immutable source).</summary>
        internal static float RequiredFanMultiplierAt(int index)
        {
            return CanonicalFanMultipliers[index];
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
