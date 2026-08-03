using System;
using System.Globalization;
using BugCam.Core;
using UnityEngine;

namespace BugCam.Evidence
{
    /// <summary>
    /// Single source of truth for Block 1.5 ghost evidence.
    /// Re-analyzes baseline vs each retained fan; never fabricates fans for STABLE;
    /// primary selection is deterministic (1.0× search-axis fan preferred).
    /// Failed searches / build failures still produce a valid success=false bundle (§15).
    /// </summary>
    public static class GhostEvidenceBuilder
    {
        private static readonly Vector3[] FanAxes =
        {
            Vector3.right,
            Vector3.up,
            Vector3.forward
        };

        public static GhostEvidenceBuildResult Build(
            EpsilonSearchResult searchResult,
            GhostSearchIdentity searchIdentity,
            DivergenceSettings settings,
            float[] bodyScalesMetres = null,
            string runId = null,
            GhostRunEnvironment environment = default,
            GhostSettingsProvenance settingsSource = default)
        {
            if (settings == null)
            {
                return GhostEvidenceBuildResult.Failure("DivergenceSettings is required.");
            }

            return Build(
                searchResult,
                searchIdentity,
                settings.ToThresholds(),
                settings.GhostBodyLimit,
                bodyScalesMetres,
                runId,
                environment,
                settingsSource);
        }

        public static GhostEvidenceBuildResult Build(
            EpsilonSearchResult searchResult,
            GhostSearchIdentity searchIdentity,
            DivergenceThresholds thresholds,
            int ghostBodyLimit,
            float[] bodyScalesMetres = null,
            string runId = null,
            GhostRunEnvironment environment = default,
            GhostSettingsProvenance settingsSource = default)
        {
            if (!searchResult.Succeeded)
            {
                return GhostEvidenceBuildResult.Success(
                    CreateFailureDocument(
                        searchResult,
                        searchIdentity,
                        ResolveSearchErrorCode(searchResult.ErrorReason),
                        searchResult.ErrorReason,
                        ghostBodyLimit > 0 ? ghostBodyLimit : 10,
                        runId,
                        environment,
                        settingsSource));
            }

            if (!searchResult.BaselineRun.Succeeded)
            {
                return GhostEvidenceBuildResult.Success(
                    CreateFailureDocument(
                        searchResult,
                        searchIdentity,
                        GhostEvidenceErrorCodes.BuildFailed,
                        "Baseline run is required for ghost evidence.",
                        ghostBodyLimit > 0 ? ghostBodyLimit : 10,
                        runId,
                        environment,
                        settingsSource));
            }

            var thresholdError = thresholds.Validate();
            if (!string.IsNullOrEmpty(thresholdError))
            {
                return GhostEvidenceBuildResult.Success(
                    CreateFailureDocument(
                        searchResult,
                        searchIdentity,
                        GhostEvidenceErrorCodes.BuildFailed,
                        thresholdError,
                        ghostBodyLimit > 0 ? ghostBodyLimit : 10,
                        runId,
                        environment,
                        settingsSource));
            }

            if (ghostBodyLimit <= 0)
            {
                return GhostEvidenceBuildResult.Success(
                    CreateFailureDocument(
                        searchResult,
                        searchIdentity,
                        GhostEvidenceErrorCodes.BuildFailed,
                        "GhostBodyLimit must be positive.",
                        10,
                        runId,
                        environment,
                        settingsSource));
            }

            // STABLE ⇒ no fabricated fans. Honor Core retention exactly.
            var retainedFans = searchResult.FanRuns ?? Array.Empty<RunResult>();
            var fanSummaries = searchResult.FanSummaries ?? Array.Empty<EpsilonProbeSummary>();

            if (searchResult.VerdictKind == EpsilonSearchVerdictKind.StableWithinTestedRange)
            {
                if (retainedFans.Length != 0 || fanSummaries.Length != 0)
                {
                    return GhostEvidenceBuildResult.Success(
                        CreateFailureDocument(
                            searchResult,
                            searchIdentity,
                            GhostEvidenceErrorCodes.BuildFailed,
                            "STABLE WITHIN TESTED RANGE must not retain fabricated fan runs.",
                            ghostBodyLimit,
                            runId,
                            environment,
                            settingsSource));
                }
            }

            if (retainedFans.Length != fanSummaries.Length)
            {
                return GhostEvidenceBuildResult.Success(
                    CreateFailureDocument(
                        searchResult,
                        searchIdentity,
                        GhostEvidenceErrorCodes.BuildFailed,
                        "FanRuns and FanSummaries length mismatch (" +
                        retainedFans.Length + " vs " + fanSummaries.Length + ").",
                        ghostBodyLimit,
                        runId,
                        environment,
                        settingsSource));
            }

            if (retainedFans.Length > 0 &&
                retainedFans.Length != EpsilonSearchSettings.RequiredFanRunCount)
            {
                return GhostEvidenceBuildResult.Success(
                    CreateFailureDocument(
                        searchResult,
                        searchIdentity,
                        GhostEvidenceErrorCodes.BuildFailed,
                        "Retained fan count must be 0 or exactly " +
                        EpsilonSearchSettings.RequiredFanRunCount +
                        ", got " + retainedFans.Length + ".",
                        ghostBodyLimit,
                        runId,
                        environment,
                        settingsSource));
            }

            var fans = new GhostFanEvidence[retainedFans.Length];
            var divergences = new DivergenceResult[retainedFans.Length];

            for (var i = 0; i < retainedFans.Length; i++)
            {
                var run = retainedFans[i];
                var summary = fanSummaries[i];
                if (!run.Succeeded)
                {
                    return GhostEvidenceBuildResult.Success(
                        CreateFailureDocument(
                            searchResult,
                            searchIdentity,
                            GhostEvidenceErrorCodes.BuildFailed,
                            "Fan run[" + i + "] did not succeed: " + run.ErrorReason,
                            ghostBodyLimit,
                            runId,
                            environment,
                            settingsSource));
                }

                // Fail closed: OutsideSearchRange must agree with ε > search ceiling.
                var expectedOutside =
                    run.EpsilonMetres > searchResult.SearchRangeCeilingMetres;
                if (summary.OutsideSearchRange != expectedOutside)
                {
                    return GhostEvidenceBuildResult.Success(
                        CreateFailureDocument(
                            searchResult,
                            searchIdentity,
                            GhostEvidenceErrorCodes.BuildFailed,
                            "OutsideSearchRange inconsistency at fan[" + i +
                            "]: summary=" + summary.OutsideSearchRange +
                            " expected=" + expectedOutside +
                            " (epsilon=" + run.EpsilonMetres.ToString("R", CultureInfo.InvariantCulture) +
                            " ceiling=" +
                            searchResult.SearchRangeCeilingMetres.ToString(
                                "R",
                                CultureInfo.InvariantCulture) + ").",
                            ghostBodyLimit,
                            runId,
                            environment,
                            settingsSource));
                }

                var expectedMultiplier = ResolveMultiplier(i, searchResult.ReferenceEpsilonMetres);
                var expectedAxis = FanAxes[i % FanAxes.Length];

                // Fan order contract: BOTH FanSummary.Axis AND Run.Perturbation.Axis must match.
                if (!AxesEqual(summary.Axis, expectedAxis) ||
                    !AxesEqual(run.Perturbation.Axis, expectedAxis))
                {
                    return GhostEvidenceBuildResult.Success(
                        CreateFailureDocument(
                            searchResult,
                            searchIdentity,
                            GhostEvidenceErrorCodes.BuildFailed,
                            "Fan order mismatch at index " + i +
                            ": expected axis " + AxisName(expectedAxis) +
                            " on both FanSummary and Run.Perturbation.",
                            ghostBodyLimit,
                            runId,
                            environment,
                            settingsSource));
                }

                // Fail closed: fan epsilon ≈ ReferenceEpsilon × multiplier.
                if (!FanEpsilonMatchesReference(
                        run.EpsilonMetres,
                        searchResult.ReferenceEpsilonMetres,
                        expectedMultiplier))
                {
                    return GhostEvidenceBuildResult.Success(
                        CreateFailureDocument(
                            searchResult,
                            searchIdentity,
                            GhostEvidenceErrorCodes.BuildFailed,
                            "Fan epsilon mismatch at index " + i +
                            ": epsilon=" +
                            run.EpsilonMetres.ToString("R", CultureInfo.InvariantCulture) +
                            " expected≈" +
                            (searchResult.ReferenceEpsilonMetres * expectedMultiplier)
                                .ToString("R", CultureInfo.InvariantCulture) +
                            " (ref×" +
                            expectedMultiplier.ToString("R", CultureInfo.InvariantCulture) +
                            ", tol=" +
                            GhostEvidenceSchema.FanEpsilonRelativeTolerance.ToString(
                                "R",
                                CultureInfo.InvariantCulture) + ").",
                            ghostBodyLimit,
                            runId,
                            environment,
                            settingsSource));
                }

                var divergence = DivergenceEngine.Analyze(
                    searchResult.BaselineRun,
                    run,
                    bodyScalesMetres,
                    thresholds);
                if (!divergence.Succeeded)
                {
                    return GhostEvidenceBuildResult.Success(
                        CreateFailureDocument(
                            searchResult,
                            searchIdentity,
                            GhostEvidenceErrorCodes.BuildFailed,
                            "Re-analyze fan[" + i + "] failed: " + divergence.ErrorReason,
                            ghostBodyLimit,
                            runId,
                            environment,
                            settingsSource));
                }

                divergences[i] = divergence;
                fans[i] = new GhostFanEvidence(
                    i,
                    expectedMultiplier,
                    expectedAxis,
                    run.EpsilonMetres,
                    summary.OutsideSearchRange,
                    run,
                    divergence);
            }

            var primaryIndex = SelectPrimaryFanIndex(fans, searchIdentity.SearchAxis);
            var hasPrimary = primaryIndex >= 0;
            // No primary (STABLE / empty fan): compare baseline to itself so metrics stay honest.
            var primaryDivergence = hasPrimary
                ? fans[primaryIndex].Divergence
                : DivergenceEngine.Analyze(
                    searchResult.BaselineRun,
                    searchResult.BaselineRun,
                    bodyScalesMetres,
                    thresholds);

            var ranked = GhostBodyRanking.Rank(
                divergences,
                searchResult.BaselineRun.StableBodyIds,
                ghostBodyLimit);

            var resolvedRunId = string.IsNullOrEmpty(runId)
                ? CreateRunId(searchResult, searchIdentity)
                : runId;

            var document = new GhostEvidenceDocument(
                resolvedRunId,
                searchResult,
                searchIdentity,
                ghostBodyLimit,
                primaryIndex,
                hasPrimary,
                fans,
                ranked,
                primaryDivergence,
                GhostDrawSet.Empty,
                true,
                GhostEvidenceErrorCodes.None,
                string.Empty,
                environment,
                settingsSource);

            var drawSet = GhostRenderer.BuildDrawSet(document);
            document = new GhostEvidenceDocument(
                resolvedRunId,
                searchResult,
                searchIdentity,
                ghostBodyLimit,
                primaryIndex,
                hasPrimary,
                fans,
                ranked,
                primaryDivergence,
                drawSet,
                true,
                GhostEvidenceErrorCodes.None,
                string.Empty,
                environment,
                settingsSource);

            return GhostEvidenceBuildResult.Success(document);
        }

        /// <summary>
        /// §15 failure bundle: valid run document with success=false, empty fans,
        /// null thresholds via writer honesty, no fabricated fan/threshold data.
        /// </summary>
        public static GhostEvidenceDocument CreateFailureDocument(
            EpsilonSearchResult searchResult,
            GhostSearchIdentity searchIdentity,
            string errorCode,
            string errorReason,
            int ghostBodyLimit = 10,
            string runId = null,
            GhostRunEnvironment environment = default,
            GhostSettingsProvenance settingsSource = default)
        {
            var resolvedRunId = string.IsNullOrEmpty(runId)
                ? CreateFailureRunId(searchIdentity, errorCode)
                : runId;

            return new GhostEvidenceDocument(
                resolvedRunId,
                searchResult,
                searchIdentity,
                ghostBodyLimit > 0 ? ghostBodyLimit : 10,
                -1,
                false,
                Array.Empty<GhostFanEvidence>(),
                Array.Empty<GhostRankedBody>(),
                DivergenceResult.Failure(errorReason ?? string.Empty),
                GhostDrawSet.Empty,
                false,
                string.IsNullOrEmpty(errorCode)
                    ? GhostEvidenceErrorCodes.SearchFailed
                    : errorCode,
                errorReason ?? string.Empty,
                environment,
                settingsSource);
        }

        public static string ResolveSearchErrorCode(string errorReason)
        {
            if (!string.IsNullOrEmpty(errorReason) &&
                errorReason.IndexOf(
                    EpsilonSearchRunner.CleanupTimeoutErrorMarker,
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return GhostEvidenceErrorCodes.CleanupTimeout;
            }

            return GhostEvidenceErrorCodes.SearchFailed;
        }

        /// <summary>
        /// Primary = fan with multiplier closest to 1.0 on the search axis;
        /// tie-break by fan index ascending. Returns -1 when no fans.
        /// </summary>
        public static int SelectPrimaryFanIndex(GhostFanEvidence[] fans, Vector3 searchAxis)
        {
            if (fans == null || fans.Length == 0)
            {
                return -1;
            }

            var axis = NormalizeAxis(searchAxis);
            var bestIndex = -1;
            var bestMultiplierDelta = float.MaxValue;
            var bestAxisScore = float.MinValue;

            for (var i = 0; i < fans.Length; i++)
            {
                var fan = fans[i];
                var axisScore = Vector3.Dot(NormalizeAxis(fan.Axis), axis);
                var multiplierDelta = Math.Abs(fan.Multiplier - 1f);

                var better =
                    axisScore > bestAxisScore + 1e-6f ||
                    (Math.Abs(axisScore - bestAxisScore) <= 1e-6f &&
                     (multiplierDelta < bestMultiplierDelta - 1e-9f ||
                      (Math.Abs(multiplierDelta - bestMultiplierDelta) <= 1e-9f &&
                       (bestIndex < 0 || i < bestIndex))));

                if (better)
                {
                    bestIndex = i;
                    bestMultiplierDelta = multiplierDelta;
                    bestAxisScore = axisScore;
                }
            }

            return bestIndex;
        }

        public static float ResolveMultiplier(int fanIndex, float referenceEpsilonMetres)
        {
            var multipliers = EpsilonSearchSettings.RequiredFanMultipliers;
            var multiplierIndex = fanIndex / FanAxes.Length;
            if (multiplierIndex < 0 || multiplierIndex >= multipliers.Length)
            {
                return 0f;
            }

            return multipliers[multiplierIndex];
        }

        private static string CreateRunId(
            EpsilonSearchResult searchResult,
            GhostSearchIdentity identity)
        {
            var stamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff", CultureInfo.InvariantCulture);
            return "ghost-" + stamp +
                   "-body" + identity.TargetBodyId +
                   "-" + AxisName(identity.SearchAxis) +
                   "-" + identity.Strategy;
        }

        private static string CreateFailureRunId(GhostSearchIdentity identity, string errorCode)
        {
            var stamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff", CultureInfo.InvariantCulture);
            var code = string.IsNullOrEmpty(errorCode) ? "FAILED" : errorCode;
            return "ghost-fail-" + stamp +
                   "-body" + identity.TargetBodyId +
                   "-" + AxisName(identity.SearchAxis) +
                   "-" + code;
        }

        private static Vector3 NormalizeAxis(Vector3 axis)
        {
            if (axis == Vector3.zero ||
                float.IsNaN(axis.x) || float.IsNaN(axis.y) || float.IsNaN(axis.z) ||
                float.IsInfinity(axis.x) || float.IsInfinity(axis.y) || float.IsInfinity(axis.z))
            {
                return Vector3.right;
            }

            return axis.normalized;
        }

        /// <summary>
        /// Relative tolerance on |ε − ref×m| / max(ref×m, 1e-30).
        /// Documented constant: <see cref="GhostEvidenceSchema.FanEpsilonRelativeTolerance"/>.
        /// </summary>
        public static bool FanEpsilonMatchesReference(
            float epsilonMetres,
            float referenceEpsilonMetres,
            float multiplier)
        {
            if (float.IsNaN(epsilonMetres) || float.IsInfinity(epsilonMetres) ||
                float.IsNaN(referenceEpsilonMetres) || float.IsInfinity(referenceEpsilonMetres) ||
                float.IsNaN(multiplier) || float.IsInfinity(multiplier) ||
                referenceEpsilonMetres <= 0f || multiplier <= 0f || epsilonMetres < 0f)
            {
                return false;
            }

            var expected = referenceEpsilonMetres * multiplier;
            var denom = expected > 1e-30f ? expected : 1e-30f;
            var relative = Math.Abs(epsilonMetres - expected) / denom;
            return relative <= GhostEvidenceSchema.FanEpsilonRelativeTolerance;
        }

        private static bool AxesEqual(Vector3 a, Vector3 b)
        {
            return (a - b).sqrMagnitude < 1e-12f;
        }

        private static string AxisName(Vector3 axis)
        {
            if (AxesEqual(axis, Vector3.right))
            {
                return "X";
            }

            if (AxesEqual(axis, Vector3.up))
            {
                return "Y";
            }

            if (AxesEqual(axis, Vector3.forward))
            {
                return "Z";
            }

            return "custom";
        }
    }
}
