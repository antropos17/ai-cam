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
            string runId = null)
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
                runId);
        }

        public static GhostEvidenceBuildResult Build(
            EpsilonSearchResult searchResult,
            GhostSearchIdentity searchIdentity,
            DivergenceThresholds thresholds,
            int ghostBodyLimit,
            float[] bodyScalesMetres = null,
            string runId = null)
        {
            if (!searchResult.Succeeded)
            {
                return GhostEvidenceBuildResult.Failure(
                    "Search did not succeed: " + searchResult.ErrorReason);
            }

            if (!searchResult.BaselineRun.Succeeded)
            {
                return GhostEvidenceBuildResult.Failure(
                    "Baseline run is required for ghost evidence.");
            }

            var thresholdError = thresholds.Validate();
            if (!string.IsNullOrEmpty(thresholdError))
            {
                return GhostEvidenceBuildResult.Failure(thresholdError);
            }

            if (ghostBodyLimit <= 0)
            {
                return GhostEvidenceBuildResult.Failure("GhostBodyLimit must be positive.");
            }

            // STABLE ⇒ no fabricated fans. Honor Core retention exactly.
            var retainedFans = searchResult.FanRuns ?? Array.Empty<RunResult>();
            var fanSummaries = searchResult.FanSummaries ?? Array.Empty<EpsilonProbeSummary>();

            if (searchResult.VerdictKind == EpsilonSearchVerdictKind.StableWithinTestedRange)
            {
                if (retainedFans.Length != 0 || fanSummaries.Length != 0)
                {
                    return GhostEvidenceBuildResult.Failure(
                        "STABLE WITHIN TESTED RANGE must not retain fabricated fan runs.");
                }
            }

            if (retainedFans.Length != fanSummaries.Length)
            {
                return GhostEvidenceBuildResult.Failure(
                    "FanRuns and FanSummaries length mismatch (" +
                    retainedFans.Length + " vs " + fanSummaries.Length + ").");
            }

            if (retainedFans.Length > 0 &&
                retainedFans.Length != EpsilonSearchSettings.RequiredFanRunCount)
            {
                return GhostEvidenceBuildResult.Failure(
                    "Retained fan count must be 0 or exactly " +
                    EpsilonSearchSettings.RequiredFanRunCount +
                    ", got " + retainedFans.Length + ".");
            }

            var fans = new GhostFanEvidence[retainedFans.Length];
            var divergences = new DivergenceResult[retainedFans.Length];

            for (var i = 0; i < retainedFans.Length; i++)
            {
                var run = retainedFans[i];
                var summary = fanSummaries[i];
                if (!run.Succeeded)
                {
                    return GhostEvidenceBuildResult.Failure(
                        "Fan run[" + i + "] did not succeed: " + run.ErrorReason);
                }

                // Preserve OutsideSearchRange from Core — never clamp.
                if (summary.OutsideSearchRange != (run.EpsilonMetres > searchResult.SearchRangeCeilingMetres))
                {
                    // Prefer Core summary flag; magnitude check is informational honesty only.
                }

                var expectedMultiplier = ResolveMultiplier(i, searchResult.ReferenceEpsilonMetres);
                var expectedAxis = FanAxes[i % FanAxes.Length];

                // Fan order contract: multiplier-major × X/Y/Z (matches Core BuildFanTables).
                if (!AxesEqual(summary.Axis, expectedAxis) && !AxesEqual(run.Perturbation.Axis, expectedAxis))
                {
                    return GhostEvidenceBuildResult.Failure(
                        "Fan order mismatch at index " + i +
                        ": expected axis " + AxisName(expectedAxis) + ".");
                }

                var divergence = DivergenceEngine.Analyze(
                    searchResult.BaselineRun,
                    run,
                    bodyScalesMetres,
                    thresholds);
                if (!divergence.Succeeded)
                {
                    return GhostEvidenceBuildResult.Failure(
                        "Re-analyze fan[" + i + "] failed: " + divergence.ErrorReason);
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
                GhostDrawSet.Empty);

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
                drawSet);

            return GhostEvidenceBuildResult.Success(document);
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
