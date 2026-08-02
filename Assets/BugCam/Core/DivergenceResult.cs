using System;
using System.Globalization;
using System.Text;

namespace BugCam.Core
{
    /// <summary>
    /// Block 1.3 output: first sustained divergence, max spread, affected bodies,
    /// amplification, and per-step scores for later ghost/evidence blocks.
    /// All lengths are metres. Millimetres exist only at the display layer.
    /// </summary>
    public readonly struct DivergenceResult
    {
        private static readonly float[] EmptyFloats = Array.Empty<float>();
        private static readonly int[] EmptyInts = Array.Empty<int>();

        private DivergenceResult(
            bool succeeded,
            string errorReason,
            int stepCount,
            int bodyCount,
            float epsilonMetres,
            bool hasSignificantDivergence,
            int firstDivergenceFrame,
            float maxSpreadMetres,
            int maxSpreadStep,
            int maxSpreadBodyId,
            int[] affectedBodyIds,
            bool amplificationDefined,
            float amplification,
            float[] perBodyMaxPositionErrorMetres,
            float[] sceneScorePerStep)
        {
            Succeeded = succeeded;
            ErrorReason = errorReason;
            StepCount = stepCount;
            BodyCount = bodyCount;
            EpsilonMetres = epsilonMetres;
            HasSignificantDivergence = hasSignificantDivergence;
            FirstDivergenceFrame = firstDivergenceFrame;
            MaxSpreadMetres = maxSpreadMetres;
            MaxSpreadStep = maxSpreadStep;
            MaxSpreadBodyId = maxSpreadBodyId;
            AffectedBodyIds = affectedBodyIds;
            AmplificationDefined = amplificationDefined;
            Amplification = amplification;
            PerBodyMaxPositionErrorMetres = perBodyMaxPositionErrorMetres;
            SceneScorePerStep = sceneScorePerStep;
        }

        public bool Succeeded { get; }

        public string ErrorReason { get; }

        public int StepCount { get; }

        public int BodyCount { get; }

        /// <summary>Perturbation magnitude from the compared run, in metres.</summary>
        public float EpsilonMetres { get; }

        /// <summary>
        /// True only when scene score clears its gate, at least one body exceeds the
        /// per-body position threshold, and both hold for SustainedSteps consecutive frames.
        /// </summary>
        public bool HasSignificantDivergence { get; }

        /// <summary>First frame of the sustained qualifying window, or -1.</summary>
        public int FirstDivergenceFrame { get; }

        /// <summary>Largest position error over all steps and bodies (metres).</summary>
        public float MaxSpreadMetres { get; }

        public int MaxSpreadStep { get; }

        /// <summary>Stable body id (or body index when ids were not supplied) at max spread.</summary>
        public int MaxSpreadBodyId { get; }

        /// <summary>
        /// Unique stable body ids (or indices) whose max position error exceeded the
        /// per-body position threshold, sorted ascending for determinism.
        /// </summary>
        public int[] AffectedBodyIds { get; }

        public int AffectedBodyCount => AffectedBodyIds.Length;

        /// <summary>False when epsilon is zero — amplification is unavailable.</summary>
        public bool AmplificationDefined { get; }

        /// <summary>
        /// MaxSpreadMetres / EpsilonMetres when defined; otherwise 0.
        /// Never NaN or Infinity.
        /// </summary>
        public float Amplification { get; }

        /// <summary>Per body, largest position error over the run (metres).</summary>
        public float[] PerBodyMaxPositionErrorMetres { get; }

        /// <summary>
        /// Per-step Scene Divergence Score =
        /// Σ_i (Wp*posNorm_i + Wr*rotNorm_i + Wv*velNorm_i + Ws*sleep_i).
        /// </summary>
        public float[] SceneScorePerStep { get; }

        internal static DivergenceResult Success(
            int stepCount,
            int bodyCount,
            float epsilonMetres,
            bool hasSignificantDivergence,
            int firstDivergenceFrame,
            float maxSpreadMetres,
            int maxSpreadStep,
            int maxSpreadBodyId,
            int[] affectedBodyIds,
            bool amplificationDefined,
            float amplification,
            float[] perBodyMaxPositionErrorMetres,
            float[] sceneScorePerStep)
        {
            return new DivergenceResult(
                true,
                string.Empty,
                stepCount,
                bodyCount,
                epsilonMetres,
                hasSignificantDivergence,
                firstDivergenceFrame,
                maxSpreadMetres,
                maxSpreadStep,
                maxSpreadBodyId,
                affectedBodyIds,
                amplificationDefined,
                amplification,
                perBodyMaxPositionErrorMetres,
                sceneScorePerStep);
        }

        internal static DivergenceResult Failure(string errorReason)
        {
            return new DivergenceResult(
                false,
                errorReason ?? string.Empty,
                0,
                0,
                0f,
                false,
                -1,
                0f,
                -1,
                -1,
                EmptyInts,
                false,
                0f,
                EmptyFloats,
                EmptyFloats);
        }
    }

    /// <summary>
    /// Console / evidence formatting. Numbers, not adjectives.
    /// Millimetres appear here and nowhere else in Core.
    /// </summary>
    public static class DivergenceReport
    {
        public static string Format(DivergenceResult result)
        {
            var sb = new StringBuilder(512);
            sb.AppendLine("BUGCAM_BLOCK_1_3_DIVERGENCE");
            sb.AppendLine("succeeded=" + result.Succeeded);
            if (!result.Succeeded)
            {
                sb.AppendLine("errorReason=" + result.ErrorReason);
                return sb.ToString();
            }

            sb.AppendLine("stepCount=" + result.StepCount);
            sb.AppendLine("bodyCount=" + result.BodyCount);
            sb.AppendLine("epsilonMetres=" + Invariant(result.EpsilonMetres));
            sb.AppendLine("epsilonMillimetres=" + Invariant(result.EpsilonMetres * 1000f));
            sb.AppendLine("hasSignificantDivergence=" + result.HasSignificantDivergence);
            sb.AppendLine("firstDivergenceFrame=" + result.FirstDivergenceFrame);
            sb.AppendLine("maxSpreadMetres=" + Invariant(result.MaxSpreadMetres));
            sb.AppendLine("maxSpreadStep=" + result.MaxSpreadStep);
            sb.AppendLine("maxSpreadBodyId=" + result.MaxSpreadBodyId);
            sb.AppendLine("affectedBodyCount=" + result.AffectedBodyCount);
            sb.Append("affectedBodyIds=");
            for (var i = 0; i < result.AffectedBodyIds.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                sb.Append(result.AffectedBodyIds[i]);
            }

            sb.AppendLine();
            sb.AppendLine("amplificationDefined=" + result.AmplificationDefined);
            sb.AppendLine("amplification=" + Invariant(result.Amplification));
            sb.AppendLine(
                "verdict=" +
                (result.HasSignificantDivergence ? "DIVERGED" : "STABLE WITHIN TESTED RANGE"));
            return sb.ToString();
        }

        private static string Invariant(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
