using System;
using System.Collections.Generic;
using UnityEngine;

namespace BugCam.Core
{
    /// <summary>
    /// Step-driven adaptive epsilon search state machine.
    /// Unity (or a synthetic test) drives probes via <see cref="TryGetNextProbe"/> /
    /// <see cref="SubmitProbeResult"/> — there is no synchronous search loop in Core.
    /// </summary>
    public sealed class EpsilonSearch
    {
        private static readonly Vector3[] FanAxes =
        {
            Vector3.right,
            Vector3.up,
            Vector3.forward
        };

        private readonly EpsilonSearchSettings _settings;
        private readonly int _targetBodyId;
        private readonly Vector3 _searchAxis;
        private readonly EpsilonSearchStrategy _strategy;
        private readonly float _customExponentialStartMetres;

        private readonly List<EpsilonProbeSummary> _ladder = new List<EpsilonProbeSummary>(16);
        private readonly List<EpsilonProbeSummary> _exponential = new List<EpsilonProbeSummary>(16);
        private readonly List<EpsilonProbeSummary> _bisection = new List<EpsilonProbeSummary>(16);
        private readonly List<EpsilonProbeSummary> _fan = new List<EpsilonProbeSummary>(16);
        private readonly List<RunResult> _fanRuns = new List<RunResult>(16);
        private readonly Dictionary<long, CachedProbe> _cache = new Dictionary<long, CachedProbe>(64);

        private EpsilonSearchPhase _phase = EpsilonSearchPhase.NotStarted;
        private EpsilonSearchVerdictKind _verdict = EpsilonSearchVerdictKind.Incomplete;
        private string _errorReason = string.Empty;
        private bool _complete;
        private int _sequenceIndex;
        private int _physicalProbeCount;
        private int _cacheHitCount;
        private int _ladderIndex;
        private int _bisectionIndex;
        private int _fanIndex;
        private float _exponentialCursor;
        private bool _exponentialStarted;
        private bool _exponentialFinished;
        private bool _hasLargestStable;
        private float _largestStable;
        private bool _hasSmallestDivergent;
        private float _smallestDivergent;
        private float _referenceEpsilon;
        private RunResult _baselineRun;
        private EpsilonProbeRequest _outstanding;
        private bool _hasOutstanding;

        private float[] _ladderEpsilons;
        private float[] _fanEpsilons;
        private Vector3[] _fanAxes;
        private bool[] _fanOutside;

        public EpsilonSearch(
            EpsilonSearchSettings settings,
            int targetBodyId,
            Vector3 searchAxis,
            EpsilonSearchStrategy strategy = EpsilonSearchStrategy.AscendFromStart,
            float customExponentialStartMetres = 0f)
        {
            var validation = settings.Validate();
            if (!string.IsNullOrEmpty(validation))
            {
                Fail(validation);
                _settings = settings;
                _targetBodyId = targetBodyId;
                _searchAxis = Vector3.right;
                _strategy = strategy;
                _customExponentialStartMetres = customExponentialStartMetres;
                return;
            }

            if (targetBodyId == 0)
            {
                Fail("TargetBodyId must be non-zero.");
                _settings = settings;
                _targetBodyId = targetBodyId;
                _searchAxis = Vector3.right;
                _strategy = strategy;
                _customExponentialStartMetres = customExponentialStartMetres;
                return;
            }

            if (searchAxis == Vector3.zero ||
                float.IsNaN(searchAxis.x) || float.IsNaN(searchAxis.y) || float.IsNaN(searchAxis.z) ||
                float.IsInfinity(searchAxis.x) || float.IsInfinity(searchAxis.y) ||
                float.IsInfinity(searchAxis.z))
            {
                Fail("SearchAxis must be a non-zero finite vector.");
                _settings = settings;
                _targetBodyId = targetBodyId;
                _searchAxis = Vector3.right;
                _strategy = strategy;
                _customExponentialStartMetres = customExponentialStartMetres;
                return;
            }

            if (strategy == EpsilonSearchStrategy.AscendFromCustomStart)
            {
                if (!(customExponentialStartMetres > 0f) ||
                    float.IsNaN(customExponentialStartMetres) ||
                    float.IsInfinity(customExponentialStartMetres))
                {
                    Fail("CustomExponentialStartMetres must be a positive finite number of metres.");
                    _settings = settings;
                    _targetBodyId = targetBodyId;
                    _searchAxis = searchAxis.normalized;
                    _strategy = strategy;
                    _customExponentialStartMetres = customExponentialStartMetres;
                    return;
                }

                if (customExponentialStartMetres < settings.EpsilonStartMetres ||
                    customExponentialStartMetres > settings.EpsilonCeilingMetres)
                {
                    Fail("CustomExponentialStartMetres must lie inside the search range.");
                    _settings = settings;
                    _targetBodyId = targetBodyId;
                    _searchAxis = searchAxis.normalized;
                    _strategy = strategy;
                    _customExponentialStartMetres = customExponentialStartMetres;
                    return;
                }
            }

            _settings = settings;
            _targetBodyId = targetBodyId;
            _searchAxis = searchAxis.normalized;
            _strategy = strategy;
            _customExponentialStartMetres = customExponentialStartMetres;
            _ladderEpsilons = BuildLogUniformLadder(
                settings.EpsilonStartMetres,
                settings.EpsilonCeilingMetres,
                settings.LadderPointCount);
            _fanEpsilons = Array.Empty<float>();
            _fanAxes = Array.Empty<Vector3>();
            _fanOutside = Array.Empty<bool>();
            _phase = EpsilonSearchPhase.Baseline;
        }

        public bool IsComplete => _complete;

        public EpsilonSearchPhase Phase => _phase;

        public bool TryGetNextProbe(out EpsilonProbeRequest request)
        {
            request = default;
            if (_complete)
            {
                return false;
            }

            if (_hasOutstanding)
            {
                request = _outstanding;
                return true;
            }

            while (!_complete)
            {
                switch (_phase)
                {
                    case EpsilonSearchPhase.Baseline:
                        return Offer(
                            new EpsilonProbeRequest(
                                EpsilonSearchPhase.Baseline,
                                _sequenceIndex,
                                0f,
                                Vector3.zero,
                                _targetBodyId,
                                isBaseline: true,
                                outsideSearchRange: false),
                            out request);

                    case EpsilonSearchPhase.Ladder:
                        if (_ladderIndex >= _ladderEpsilons.Length)
                        {
                            if (!ClassifyLadderAndAdvance())
                            {
                                return false;
                            }

                            continue;
                        }

                        return OfferOrCache(
                            EpsilonSearchPhase.Ladder,
                            _ladderEpsilons[_ladderIndex],
                            _searchAxis,
                            outsideSearchRange: false,
                            out request);

                    case EpsilonSearchPhase.Exponential:
                        if (_exponentialFinished)
                        {
                            BeginBisectionOrFail();
                            continue;
                        }

                        if (!_exponentialStarted)
                        {
                            _exponentialCursor = InitialExponentialEpsilon();
                            _exponentialStarted = true;
                        }

                        if (_exponentialCursor < _settings.EpsilonStartMetres - 1e-12f ||
                            _exponentialCursor > _settings.EpsilonCeilingMetres + 1e-12f)
                        {
                            _exponentialFinished = true;
                            continue;
                        }

                        var expEps = _exponentialCursor;
                        AdvanceExponentialCursor();
                        return OfferOrCache(
                            EpsilonSearchPhase.Exponential,
                            expEps,
                            _searchAxis,
                            outsideSearchRange: false,
                            out request);

                    case EpsilonSearchPhase.Bisection:
                        if (_bisectionIndex >= _settings.BisectionIterations ||
                            !_hasLargestStable ||
                            !_hasSmallestDivergent ||
                            _smallestDivergent <= _largestStable)
                        {
                            FinishMonotonicBracket();
                            continue;
                        }

                        var mid = GeometricMean(_largestStable, _smallestDivergent);
                        if (mid <= _largestStable || mid >= _smallestDivergent)
                        {
                            FinishMonotonicBracket();
                            continue;
                        }

                        return OfferOrCache(
                            EpsilonSearchPhase.Bisection,
                            mid,
                            _searchAxis,
                            outsideSearchRange: false,
                            out request);

                    case EpsilonSearchPhase.Fan:
                        if (_fanIndex >= _fanEpsilons.Length)
                        {
                            CompleteSuccess();
                            return false;
                        }

                        var fanEps = _fanEpsilons[_fanIndex];
                        var fanAxis = _fanAxes[_fanIndex];
                        var outside = _fanOutside[_fanIndex];
                        return OfferOrCache(
                            EpsilonSearchPhase.Fan,
                            fanEps,
                            fanAxis,
                            outside,
                            out request);

                    case EpsilonSearchPhase.Failed:
                    case EpsilonSearchPhase.Completed:
                        _complete = true;
                        return false;

                    default:
                        Fail("EpsilonSearch entered an unknown phase.");
                        return false;
                }
            }

            return false;
        }

        public void SubmitProbeResult(EpsilonProbeRequest request, EpsilonProbeOutcome outcome)
        {
            if (_complete)
            {
                return;
            }

            if (!_hasOutstanding)
            {
                Fail("SubmitProbeResult called without an outstanding probe.");
                return;
            }

            if (!RequestsMatch(_outstanding, request))
            {
                Fail("SubmitProbeResult request does not match the outstanding probe.");
                return;
            }

            _hasOutstanding = false;

            if (!outcome.Succeeded)
            {
                Fail(string.IsNullOrEmpty(outcome.ErrorReason)
                    ? "Probe failed without a reason."
                    : outcome.ErrorReason);
                return;
            }

            _physicalProbeCount++;
            var summary = new EpsilonProbeSummary(
                request.Phase,
                request.EpsilonMetres,
                request.Axis,
                outcome.HasSignificantDivergence,
                outcome.FirstDivergenceFrame,
                outcome.MaxSpreadMetres,
                request.OutsideSearchRange,
                servedFromCache: false);

            Remember(summary);

            switch (request.Phase)
            {
                case EpsilonSearchPhase.Baseline:
                    if (!outcome.RunResult.Succeeded)
                    {
                        Fail("Baseline probe must supply a successful RunResult with retained frames.");
                        return;
                    }

                    _baselineRun = outcome.RunResult;
                    _phase = EpsilonSearchPhase.Ladder;
                    _ladderIndex = 0;
                    break;

                case EpsilonSearchPhase.Ladder:
                    _ladder.Add(summary);
                    _ladderIndex++;
                    break;

                case EpsilonSearchPhase.Exponential:
                    _exponential.Add(summary);
                    ApplyBracketSample(summary.EpsilonMetres, summary.HasSignificantDivergence);
                    if (_strategy == EpsilonSearchStrategy.DescendFromCeiling)
                    {
                        if (!summary.HasSignificantDivergence)
                        {
                            _exponentialFinished = true;
                        }
                    }
                    else if (summary.HasSignificantDivergence)
                    {
                        _exponentialFinished = true;
                    }

                    if (_exponentialCursor < _settings.EpsilonStartMetres - 1e-12f ||
                        _exponentialCursor > _settings.EpsilonCeilingMetres + 1e-12f)
                    {
                        _exponentialFinished = true;
                    }

                    break;

                case EpsilonSearchPhase.Bisection:
                    _bisection.Add(summary);
                    ApplyBracketSample(summary.EpsilonMetres, summary.HasSignificantDivergence);
                    _bisectionIndex++;
                    break;

                case EpsilonSearchPhase.Fan:
                    _fan.Add(summary);
                    if (!outcome.RunResult.Succeeded)
                    {
                        Fail("Fan probe must supply a successful RunResult with retained frames.");
                        return;
                    }

                    _fanRuns.Add(outcome.RunResult);
                    _fanIndex++;
                    break;
            }
        }

        public EpsilonSearchResult BuildResult()
        {
            if (!_complete && _phase != EpsilonSearchPhase.Failed)
            {
                return EpsilonSearchResult.Failure(
                    "Search is incomplete; continue TryGetNextProbe / SubmitProbeResult.");
            }

            if (_verdict == EpsilonSearchVerdictKind.Failed || !string.IsNullOrEmpty(_errorReason))
            {
                return EpsilonSearchResult.Failure(_errorReason);
            }

            var hasThreshold = _verdict == EpsilonSearchVerdictKind.ThresholdBracketFound;
            var thresholdEstimate = hasThreshold ? _smallestDivergent : 0f;
            var bracketWidth = 0f;
            if (_hasLargestStable && _hasSmallestDivergent)
            {
                bracketWidth = _smallestDivergent - _largestStable;
            }
            else if (_hasSmallestDivergent)
            {
                bracketWidth = _smallestDivergent - _settings.EpsilonStartMetres;
            }

            return new EpsilonSearchResult(
                true,
                string.Empty,
                _verdict,
                _settings.EpsilonStartMetres,
                _settings.EpsilonCeilingMetres,
                _settings.CharacterizationCeilingMetres,
                _hasLargestStable,
                _largestStable,
                _hasSmallestDivergent,
                _smallestDivergent,
                hasThreshold,
                thresholdEstimate,
                _referenceEpsilon,
                referenceIsExactThreshold: false,
                bracketWidth,
                _ladder.ToArray(),
                _exponential.ToArray(),
                _bisection.ToArray(),
                _fan.ToArray(),
                _baselineRun,
                _fanRuns.ToArray(),
                _cacheHitCount,
                _physicalProbeCount);
        }

        private bool Offer(EpsilonProbeRequest request, out EpsilonProbeRequest offered)
        {
            _outstanding = request;
            _hasOutstanding = true;
            _sequenceIndex++;
            offered = request;
            return true;
        }

        private bool OfferOrCache(
            EpsilonSearchPhase phase,
            float epsilonMetres,
            Vector3 axis,
            bool outsideSearchRange,
            out EpsilonProbeRequest request)
        {
            var key = CacheKey(epsilonMetres, axis);
            // Fan retains full frames — never satisfy from cache.
            if (phase != EpsilonSearchPhase.Fan &&
                _cache.TryGetValue(key, out var cached))
            {
                _cacheHitCount++;
                var summary = new EpsilonProbeSummary(
                    phase,
                    epsilonMetres,
                    axis,
                    cached.HasSignificantDivergence,
                    cached.FirstDivergenceFrame,
                    cached.MaxSpreadMetres,
                    outsideSearchRange,
                    servedFromCache: true);

                switch (phase)
                {
                    case EpsilonSearchPhase.Ladder:
                        _ladder.Add(summary);
                        _ladderIndex++;
                        break;
                    case EpsilonSearchPhase.Exponential:
                        _exponential.Add(summary);
                        ApplyBracketSample(epsilonMetres, cached.HasSignificantDivergence);
                        if (_strategy == EpsilonSearchStrategy.DescendFromCeiling)
                        {
                            if (!cached.HasSignificantDivergence)
                            {
                                _exponentialFinished = true;
                            }
                        }
                        else if (cached.HasSignificantDivergence)
                        {
                            _exponentialFinished = true;
                        }

                        if (_exponentialCursor < _settings.EpsilonStartMetres - 1e-12f ||
                            _exponentialCursor > _settings.EpsilonCeilingMetres + 1e-12f)
                        {
                            _exponentialFinished = true;
                        }

                        break;
                    case EpsilonSearchPhase.Bisection:
                        _bisection.Add(summary);
                        ApplyBracketSample(epsilonMetres, cached.HasSignificantDivergence);
                        _bisectionIndex++;
                        break;
                }

                request = default;
                return TryGetNextProbe(out request);
            }

            return Offer(
                new EpsilonProbeRequest(
                    phase,
                    _sequenceIndex,
                    epsilonMetres,
                    axis,
                    _targetBodyId,
                    isBaseline: false,
                    outsideSearchRange),
                out request);
        }

        private bool ClassifyLadderAndAdvance()
        {
            if (_ladder.Count != _settings.LadderPointCount)
            {
                Fail("Ladder ended with unexpected sample count.");
                return false;
            }

            var sawDivergent = false;
            var nonMonotonic = false;
            _hasLargestStable = false;
            _hasSmallestDivergent = false;
            _largestStable = 0f;
            _smallestDivergent = 0f;

            for (var i = 0; i < _ladder.Count; i++)
            {
                var sample = _ladder[i];
                if (sample.HasSignificantDivergence)
                {
                    if (!_hasSmallestDivergent || sample.EpsilonMetres < _smallestDivergent)
                    {
                        _hasSmallestDivergent = true;
                        _smallestDivergent = sample.EpsilonMetres;
                    }

                    sawDivergent = true;
                }
                else
                {
                    if (sawDivergent)
                    {
                        nonMonotonic = true;
                    }

                    if (!_hasLargestStable || sample.EpsilonMetres > _largestStable)
                    {
                        _hasLargestStable = true;
                        _largestStable = sample.EpsilonMetres;
                    }
                }
            }

            if (!sawDivergent)
            {
                _verdict = EpsilonSearchVerdictKind.StableWithinTestedRange;
                _referenceEpsilon = 0f;
                CompleteSuccess();
                return false;
            }

            if (nonMonotonic)
            {
                _verdict = EpsilonSearchVerdictKind.NonMonotonicWithinTestedRange;
                _referenceEpsilon = _smallestDivergent;
                // Reference epsilon is not an exact threshold.
                BeginFan(_referenceEpsilon);
                return true;
            }

            // Monotonic with divergence — exponential refines the bracket for the chosen strategy.
            _phase = EpsilonSearchPhase.Exponential;
            _exponentialStarted = false;
            _exponentialFinished = false;
            // Reset strategy bracket; ladder numbers remain in summaries but exponential owns the
            // working bracket used for bisection (re-seeded from exponential + cache hits).
            _hasLargestStable = false;
            _hasSmallestDivergent = false;
            _largestStable = 0f;
            _smallestDivergent = 0f;
            return true;
        }

        private void BeginBisectionOrFail()
        {
            // Rebuild bracket from exponential summaries; fall back to ladder if exponential
            // was fully satisfied from cache without updating (should not happen).
            if (!_hasSmallestDivergent)
            {
                SeedBracketFromSummaries(_exponential);
            }

            if (!_hasSmallestDivergent)
            {
                SeedBracketFromSummaries(_ladder);
            }

            if (!_hasSmallestDivergent)
            {
                _verdict = EpsilonSearchVerdictKind.StableWithinTestedRange;
                _referenceEpsilon = 0f;
                CompleteSuccess();
                return;
            }

            if (!_hasLargestStable)
            {
                // Divergent at/under every exponential sample — threshold estimate is the
                // smallest divergent tested; no bisection below the search floor.
                _verdict = EpsilonSearchVerdictKind.ThresholdBracketFound;
                _referenceEpsilon = _smallestDivergent;
                BeginFan(_referenceEpsilon);
                return;
            }

            _phase = EpsilonSearchPhase.Bisection;
            _bisectionIndex = 0;
        }

        private void FinishMonotonicBracket()
        {
            _verdict = EpsilonSearchVerdictKind.ThresholdBracketFound;
            _referenceEpsilon = _smallestDivergent;
            BeginFan(_referenceEpsilon);
        }

        private void BeginFan(float referenceEpsilon)
        {
            if (!(referenceEpsilon > 0f))
            {
                Fail("Fan reference epsilon must be positive.");
                return;
            }

            BuildFanTables(referenceEpsilon);
            _phase = EpsilonSearchPhase.Fan;
            _fanIndex = 0;
        }

        private void BuildFanTables(float referenceEpsilon)
        {
            var multipliers = _settings.FanMultipliers;
            var count = multipliers.Length * FanAxes.Length;
            _fanEpsilons = new float[count];
            _fanAxes = new Vector3[count];
            _fanOutside = new bool[count];
            var index = 0;
            for (var m = 0; m < multipliers.Length; m++)
            {
                var epsilon = referenceEpsilon * multipliers[m];
                for (var a = 0; a < FanAxes.Length; a++)
                {
                    _fanEpsilons[index] = epsilon;
                    _fanAxes[index] = FanAxes[a];
                    // Do not clamp. Mark every sample above the search ceiling.
                    _fanOutside[index] = epsilon > _settings.EpsilonCeilingMetres;
                    index++;
                }
            }
        }

        private float InitialExponentialEpsilon()
        {
            switch (_strategy)
            {
                case EpsilonSearchStrategy.AscendFromCustomStart:
                    return _customExponentialStartMetres;
                case EpsilonSearchStrategy.DescendFromCeiling:
                    return _settings.EpsilonCeilingMetres;
                default:
                    return _settings.EpsilonStartMetres;
            }
        }

        private void AdvanceExponentialCursor()
        {
            if (_strategy == EpsilonSearchStrategy.DescendFromCeiling)
            {
                _exponentialCursor /= _settings.EpsilonGrowthFactor;
            }
            else
            {
                var next = _exponentialCursor * _settings.EpsilonGrowthFactor;
                if (next > _settings.EpsilonCeilingMetres &&
                    _exponentialCursor < _settings.EpsilonCeilingMetres - 1e-12f)
                {
                    _exponentialCursor = _settings.EpsilonCeilingMetres;
                }
                else
                {
                    _exponentialCursor = next;
                }
            }
        }

        private void ApplyBracketSample(float epsilonMetres, bool diverged)
        {
            if (diverged)
            {
                if (!_hasSmallestDivergent || epsilonMetres < _smallestDivergent)
                {
                    _hasSmallestDivergent = true;
                    _smallestDivergent = epsilonMetres;
                }
            }
            else if (!_hasLargestStable || epsilonMetres > _largestStable)
            {
                _hasLargestStable = true;
                _largestStable = epsilonMetres;
            }
        }

        private void SeedBracketFromSummaries(List<EpsilonProbeSummary> summaries)
        {
            for (var i = 0; i < summaries.Count; i++)
            {
                ApplyBracketSample(summaries[i].EpsilonMetres, summaries[i].HasSignificantDivergence);
            }
        }

        private void Remember(EpsilonProbeSummary summary)
        {
            // Baseline (epsilon 0 / zero axis) is not a cache key for perturbed probes.
            if (summary.Phase == EpsilonSearchPhase.Baseline)
            {
                return;
            }

            _cache[CacheKey(summary.EpsilonMetres, summary.Axis)] = new CachedProbe(
                summary.HasSignificantDivergence,
                summary.FirstDivergenceFrame,
                summary.MaxSpreadMetres);
        }

        private void CompleteSuccess()
        {
            _phase = EpsilonSearchPhase.Completed;
            _complete = true;
            _hasOutstanding = false;
        }

        private void Fail(string reason)
        {
            _errorReason = reason ?? "EpsilonSearch failed.";
            _verdict = EpsilonSearchVerdictKind.Failed;
            _phase = EpsilonSearchPhase.Failed;
            _complete = true;
            _hasOutstanding = false;
        }

        private static float[] BuildLogUniformLadder(float start, float ceiling, int count)
        {
            var points = new float[count];
            var logStart = Math.Log(start);
            var logCeiling = Math.Log(ceiling);
            for (var i = 0; i < count; i++)
            {
                var t = i / (double)(count - 1);
                points[i] = (float)Math.Exp(logStart + (logCeiling - logStart) * t);
            }

            points[0] = start;
            points[count - 1] = ceiling;
            return points;
        }

        private static float GeometricMean(float a, float b)
        {
            return (float)Math.Sqrt((double)a * b);
        }

        private static long CacheKey(float epsilonMetres, Vector3 axis)
        {
            // Exact float bits so geometrically identical values reuse the same probe outcome.
            var epsBits = (long)BitConverter.ToInt32(BitConverter.GetBytes(epsilonMetres), 0);
            var axisCode = AxisCode(axis);
            return (epsBits << 2) ^ axisCode;
        }

        private static long AxisCode(Vector3 axis)
        {
            if (axis == Vector3.right)
            {
                return 1;
            }

            if (axis == Vector3.up)
            {
                return 2;
            }

            if (axis == Vector3.forward)
            {
                return 3;
            }

            // Normalized arbitrary axis — pack signs of dominant component.
            var ax = Math.Abs(axis.x);
            var ay = Math.Abs(axis.y);
            var az = Math.Abs(axis.z);
            if (ax >= ay && ax >= az)
            {
                return axis.x >= 0f ? 1 : 5;
            }

            if (ay >= az)
            {
                return axis.y >= 0f ? 2 : 6;
            }

            return axis.z >= 0f ? 3 : 7;
        }

        private static bool RequestsMatch(EpsilonProbeRequest a, EpsilonProbeRequest b)
        {
            return a.Phase == b.Phase &&
                   a.SequenceIndex == b.SequenceIndex &&
                   a.IsBaseline == b.IsBaseline &&
                   a.OutsideSearchRange == b.OutsideSearchRange &&
                   a.TargetBodyId == b.TargetBodyId &&
                   Mathf.Approximately(a.EpsilonMetres, b.EpsilonMetres) &&
                   a.Axis == b.Axis;
        }

        private readonly struct CachedProbe
        {
            public CachedProbe(
                bool hasSignificantDivergence,
                int firstDivergenceFrame,
                float maxSpreadMetres)
            {
                HasSignificantDivergence = hasSignificantDivergence;
                FirstDivergenceFrame = firstDivergenceFrame;
                MaxSpreadMetres = maxSpreadMetres;
            }

            public bool HasSignificantDivergence { get; }

            public int FirstDivergenceFrame { get; }

            public float MaxSpreadMetres { get; }
        }
    }
}
