using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace BugCam.Core
{
    public enum EpsilonSearchPhase
    {
        NotStarted = 0,
        Baseline = 1,
        Ladder = 2,
        Exponential = 3,
        Bisection = 4,
        Fan = 5,
        Completed = 6,
        Failed = 7
    }

    public enum EpsilonSearchVerdictKind
    {
        Incomplete = 0,
        Failed = 1,
        StableWithinTestedRange = 2,
        NonMonotonicWithinTestedRange = 3,
        ThresholdBracketFound = 4,
        /// <summary>
        /// Divergence was observed, but no stable lower bound exists inside the tested range.
        /// Not a threshold bracket; fan may still characterize around the smallest divergent sample.
        /// </summary>
        DivergentAtSearchFloor = 5
    }

    /// <summary>
    /// Next probe the Unity runner (or a synthetic test) must execute.
    /// </summary>
    public readonly struct EpsilonProbeRequest
    {
        public EpsilonProbeRequest(
            EpsilonSearchPhase phase,
            int sequenceIndex,
            float epsilonMetres,
            Vector3 axis,
            int targetBodyId,
            bool isBaseline,
            bool outsideSearchRange)
        {
            Phase = phase;
            SequenceIndex = sequenceIndex;
            EpsilonMetres = epsilonMetres;
            Axis = axis;
            TargetBodyId = targetBodyId;
            IsBaseline = isBaseline;
            OutsideSearchRange = outsideSearchRange;
        }

        public EpsilonSearchPhase Phase { get; }

        /// <summary>Monotonic request counter for determinism / ordering asserts.</summary>
        public int SequenceIndex { get; }

        public float EpsilonMetres { get; }

        public Vector3 Axis { get; }

        public int TargetBodyId { get; }

        public bool IsBaseline { get; }

        /// <summary>
        /// True when epsilon is above the search ceiling (fan characterization only).
        /// Search-range probes never set this.
        /// </summary>
        public bool OutsideSearchRange { get; }
    }

    /// <summary>
    /// Compact probe outcome retained for ladder / exponential / bisection (and fan metadata).
    /// Full <see cref="RunResult"/> frames are kept only for baseline + fan runs.
    /// </summary>
    public readonly struct EpsilonProbeSummary
    {
        public EpsilonProbeSummary(
            EpsilonSearchPhase phase,
            float epsilonMetres,
            Vector3 axis,
            bool hasSignificantDivergence,
            int firstDivergenceFrame,
            float maxSpreadMetres,
            bool outsideSearchRange,
            bool servedFromCache)
        {
            Phase = phase;
            EpsilonMetres = epsilonMetres;
            Axis = axis;
            HasSignificantDivergence = hasSignificantDivergence;
            FirstDivergenceFrame = firstDivergenceFrame;
            MaxSpreadMetres = maxSpreadMetres;
            OutsideSearchRange = outsideSearchRange;
            ServedFromCache = servedFromCache;
        }

        public EpsilonSearchPhase Phase { get; }

        public float EpsilonMetres { get; }

        public Vector3 Axis { get; }

        public bool HasSignificantDivergence { get; }

        public int FirstDivergenceFrame { get; }

        public float MaxSpreadMetres { get; }

        public bool OutsideSearchRange { get; }

        public bool ServedFromCache { get; }
    }

    /// <summary>
    /// Outcome submitted by the runner or a synthetic EditMode harness.
    /// </summary>
    public readonly struct EpsilonProbeOutcome
    {
        private static readonly RunResult EmptyRun = RunResult.Failure("no run");

        private EpsilonProbeOutcome(
            bool succeeded,
            string errorReason,
            bool hasSignificantDivergence,
            int firstDivergenceFrame,
            float maxSpreadMetres,
            RunResult runResult)
        {
            Succeeded = succeeded;
            ErrorReason = errorReason;
            HasSignificantDivergence = hasSignificantDivergence;
            FirstDivergenceFrame = firstDivergenceFrame;
            MaxSpreadMetres = maxSpreadMetres;
            RunResult = runResult;
        }

        public bool Succeeded { get; }

        public string ErrorReason { get; }

        public bool HasSignificantDivergence { get; }

        public int FirstDivergenceFrame { get; }

        public float MaxSpreadMetres { get; }

        public RunResult RunResult { get; }

        /// <summary>
        /// Synthetic / compact success. Pass a successful <paramref name="runResult"/> when the
        /// phase retains full frames (baseline and fan); otherwise omit it.
        /// </summary>
        public static EpsilonProbeOutcome Success(
            bool hasSignificantDivergence,
            int firstDivergenceFrame,
            float maxSpreadMetres)
        {
            return new EpsilonProbeOutcome(
                true,
                string.Empty,
                hasSignificantDivergence,
                firstDivergenceFrame,
                maxSpreadMetres,
                EmptyRun);
        }

        public static EpsilonProbeOutcome Success(
            bool hasSignificantDivergence,
            int firstDivergenceFrame,
            float maxSpreadMetres,
            RunResult runResult)
        {
            return new EpsilonProbeOutcome(
                true,
                string.Empty,
                hasSignificantDivergence,
                firstDivergenceFrame,
                maxSpreadMetres,
                runResult);
        }

        public static EpsilonProbeOutcome FromDivergence(
            DivergenceResult divergence,
            RunResult runResult)
        {
            if (!divergence.Succeeded)
            {
                return Failure(divergence.ErrorReason);
            }

            if (!runResult.Succeeded)
            {
                return Failure(runResult.ErrorReason);
            }

            return new EpsilonProbeOutcome(
                true,
                string.Empty,
                divergence.HasSignificantDivergence,
                divergence.FirstDivergenceFrame,
                divergence.MaxSpreadMetres,
                runResult);
        }

        public static EpsilonProbeOutcome Failure(string errorReason)
        {
            return new EpsilonProbeOutcome(
                false,
                errorReason ?? string.Empty,
                false,
                -1,
                0f,
                RunResult.Failure(errorReason ?? string.Empty));
        }
    }

    /// <summary>
    /// Final Block 1.4 search product. Never claims an exact mathematical threshold.
    /// </summary>
    public readonly struct EpsilonSearchResult
    {
        private static readonly EpsilonProbeSummary[] EmptySummaries = Array.Empty<EpsilonProbeSummary>();
        private static readonly RunResult[] EmptyRuns = Array.Empty<RunResult>();

        public EpsilonSearchResult(
            bool succeeded,
            string errorReason,
            EpsilonSearchVerdictKind verdictKind,
            float searchRangeStartMetres,
            float searchRangeCeilingMetres,
            float characterizationCeilingMetres,
            bool hasLargestStableEpsilon,
            float largestStableEpsilonMetres,
            bool hasSmallestDivergentEpsilon,
            float smallestDivergentEpsilonMetres,
            bool hasThresholdEstimate,
            float thresholdEstimateMetres,
            float referenceEpsilonMetres,
            bool referenceIsExactThreshold,
            float finalBracketWidthMetres,
            EpsilonProbeSummary[] ladderSummaries,
            EpsilonProbeSummary[] exponentialSummaries,
            EpsilonProbeSummary[] bisectionSummaries,
            EpsilonProbeSummary[] fanSummaries,
            RunResult baselineRun,
            RunResult[] fanRuns,
            int cacheHitCount,
            int physicalProbeCount)
        {
            Succeeded = succeeded;
            ErrorReason = errorReason ?? string.Empty;
            VerdictKind = verdictKind;
            SearchRangeStartMetres = searchRangeStartMetres;
            SearchRangeCeilingMetres = searchRangeCeilingMetres;
            CharacterizationCeilingMetres = characterizationCeilingMetres;
            HasLargestStableEpsilon = hasLargestStableEpsilon;
            LargestStableEpsilonMetres = largestStableEpsilonMetres;
            HasSmallestDivergentEpsilon = hasSmallestDivergentEpsilon;
            SmallestDivergentEpsilonMetres = smallestDivergentEpsilonMetres;
            HasThresholdEstimate = hasThresholdEstimate;
            ThresholdEstimateMetres = thresholdEstimateMetres;
            ReferenceEpsilonMetres = referenceEpsilonMetres;
            ReferenceIsExactThreshold = referenceIsExactThreshold;
            FinalBracketWidthMetres = finalBracketWidthMetres;
            LadderSummaries = ladderSummaries ?? EmptySummaries;
            ExponentialSummaries = exponentialSummaries ?? EmptySummaries;
            BisectionSummaries = bisectionSummaries ?? EmptySummaries;
            FanSummaries = fanSummaries ?? EmptySummaries;
            BaselineRun = baselineRun;
            FanRuns = fanRuns ?? EmptyRuns;
            CacheHitCount = cacheHitCount;
            PhysicalProbeCount = physicalProbeCount;
        }

        public bool Succeeded { get; }

        public string ErrorReason { get; }

        public EpsilonSearchVerdictKind VerdictKind { get; }

        public string Verdict
        {
            get
            {
                switch (VerdictKind)
                {
                    case EpsilonSearchVerdictKind.StableWithinTestedRange:
                        return "STABLE WITHIN TESTED RANGE";
                    case EpsilonSearchVerdictKind.NonMonotonicWithinTestedRange:
                        return "NON-MONOTONIC WITHIN TESTED RANGE";
                    case EpsilonSearchVerdictKind.ThresholdBracketFound:
                        return "THRESHOLD BRACKET FOUND";
                    case EpsilonSearchVerdictKind.DivergentAtSearchFloor:
                        return "DIVERGENT AT SEARCH FLOOR";
                    case EpsilonSearchVerdictKind.Failed:
                        return "FAILED";
                    default:
                        return "INCOMPLETE";
                }
            }
        }

        /// <summary>Search range lower bound (metres). Distinct from characterization range.</summary>
        public float SearchRangeStartMetres { get; }

        /// <summary>Search range upper bound (metres).</summary>
        public float SearchRangeCeilingMetres { get; }

        /// <summary>Max fan magnitude bound (metres), typically 1.2 × search ceiling.</summary>
        public float CharacterizationCeilingMetres { get; }

        public bool HasLargestStableEpsilon { get; }

        public float LargestStableEpsilonMetres { get; }

        public bool HasSmallestDivergentEpsilon { get; }

        public float SmallestDivergentEpsilonMetres { get; }

        /// <summary>
        /// True only for a valid monotonic bracket: finite stable bound, finite divergent bound,
        /// and <c>stable &lt; divergent</c>. Estimate equals the smallest tested divergent
        /// epsilon — not an exact mathematical threshold. Never true when only a divergent
        /// bound exists (see <see cref="EpsilonSearchVerdictKind.DivergentAtSearchFloor"/>).
        /// </summary>
        public bool HasThresholdEstimate { get; }

        public float ThresholdEstimateMetres { get; }

        /// <summary>
        /// Epsilon used to build the fan. For non-monotonic results this is a reference
        /// epsilon (smallest observed divergent ladder sample), not an exact threshold.
        /// </summary>
        public float ReferenceEpsilonMetres { get; }

        /// <summary>Always false — BugCam never claims an exact mathematical threshold.</summary>
        public bool ReferenceIsExactThreshold { get; }

        public float FinalBracketWidthMetres { get; }

        public EpsilonProbeSummary[] LadderSummaries { get; }

        public EpsilonProbeSummary[] ExponentialSummaries { get; }

        public EpsilonProbeSummary[] BisectionSummaries { get; }

        public EpsilonProbeSummary[] FanSummaries { get; }

        public RunResult BaselineRun { get; }

        public RunResult[] FanRuns { get; }

        public int CacheHitCount { get; }

        public int PhysicalProbeCount { get; }

        public static EpsilonSearchResult Failure(string errorReason)
        {
            return new EpsilonSearchResult(
                false,
                errorReason,
                EpsilonSearchVerdictKind.Failed,
                0f,
                0f,
                0f,
                false,
                0f,
                false,
                0f,
                false,
                0f,
                0f,
                false,
                0f,
                EmptySummaries,
                EmptySummaries,
                EmptySummaries,
                EmptySummaries,
                RunResult.Failure(errorReason ?? string.Empty),
                EmptyRuns,
                0,
                0);
        }
    }

    /// <summary>
    /// Console / evidence formatting for Block 1.4. Numbers, not adjectives.
    /// Millimetres appear here and nowhere else in Core.
    /// </summary>
    public static class EpsilonSearchReport
    {
        public static string Format(EpsilonSearchResult result)
        {
            var sb = new StringBuilder(1024);
            sb.AppendLine("BUGCAM_BLOCK_1_4_EPSILON_SEARCH");
            sb.AppendLine("succeeded=" + result.Succeeded);
            if (!result.Succeeded)
            {
                sb.AppendLine("errorReason=" + result.ErrorReason);
                return sb.ToString();
            }

            sb.AppendLine("verdict=" + result.Verdict);
            sb.AppendLine("searchRangeStartMetres=" + Invariant(result.SearchRangeStartMetres));
            sb.AppendLine("searchRangeCeilingMetres=" + Invariant(result.SearchRangeCeilingMetres));
            sb.AppendLine(
                "searchRangeStartMillimetres=" + Invariant(result.SearchRangeStartMetres * 1000f));
            sb.AppendLine(
                "searchRangeCeilingMillimetres=" +
                Invariant(result.SearchRangeCeilingMetres * 1000f));
            sb.AppendLine(
                "characterizationCeilingMetres=" + Invariant(result.CharacterizationCeilingMetres));
            sb.AppendLine(
                "characterizationCeilingMillimetres=" +
                Invariant(result.CharacterizationCeilingMetres * 1000f));
            sb.AppendLine("hasLargestStableEpsilon=" + result.HasLargestStableEpsilon);
            sb.AppendLine(
                "largestStableEpsilonMetres=" + Invariant(result.LargestStableEpsilonMetres));
            sb.AppendLine("hasSmallestDivergentEpsilon=" + result.HasSmallestDivergentEpsilon);
            sb.AppendLine(
                "smallestDivergentEpsilonMetres=" +
                Invariant(result.SmallestDivergentEpsilonMetres));
            sb.AppendLine("hasThresholdEstimate=" + result.HasThresholdEstimate);
            sb.AppendLine(
                "thresholdEstimateMetres=" + Invariant(result.ThresholdEstimateMetres));
            sb.AppendLine(
                "referenceEpsilonMetres=" + Invariant(result.ReferenceEpsilonMetres));
            sb.AppendLine("referenceIsExactThreshold=" + result.ReferenceIsExactThreshold);
            sb.AppendLine("finalBracketWidthMetres=" + Invariant(result.FinalBracketWidthMetres));
            sb.AppendLine("ladderCount=" + result.LadderSummaries.Length);
            sb.AppendLine("exponentialCount=" + result.ExponentialSummaries.Length);
            sb.AppendLine("bisectionCount=" + result.BisectionSummaries.Length);
            sb.AppendLine("fanCount=" + result.FanSummaries.Length);
            sb.AppendLine("retainedFanRunCount=" + result.FanRuns.Length);
            sb.AppendLine("cacheHitCount=" + result.CacheHitCount);
            sb.AppendLine("physicalProbeCount=" + result.PhysicalProbeCount);

            AppendPhase(sb, "ladder", result.LadderSummaries);
            AppendPhase(sb, "exponential", result.ExponentialSummaries);
            AppendPhase(sb, "bisection", result.BisectionSummaries);
            AppendPhase(sb, "fan", result.FanSummaries);
            return sb.ToString();
        }

        private static void AppendPhase(
            StringBuilder sb,
            string name,
            EpsilonProbeSummary[] summaries)
        {
            for (var i = 0; i < summaries.Length; i++)
            {
                var s = summaries[i];
                sb.Append(name);
                sb.Append('[');
                sb.Append(i);
                sb.Append("]=epsilonMetres=");
                sb.Append(Invariant(s.EpsilonMetres));
                sb.Append(" firstDivergenceFrame=");
                sb.Append(s.FirstDivergenceFrame);
                sb.Append(" maxSpreadMetres=");
                sb.Append(Invariant(s.MaxSpreadMetres));
                sb.Append(" diverged=");
                sb.Append(s.HasSignificantDivergence);
                sb.Append(" outsideSearchRange=");
                sb.Append(s.OutsideSearchRange);
                sb.Append(" cached=");
                sb.Append(s.ServedFromCache);
                sb.AppendLine();
            }
        }

        private static string Invariant(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
