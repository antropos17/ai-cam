using System;
using System.Collections.Generic;
using BugCam.Core;

namespace BugCam.Evidence
{
    /// <summary>
    /// Ranks bodies for ghost visualization: PerBodyMaxPositionErrorMetres descending,
    /// bodyId ascending tie-break. Omits zero-error bodies. Caps at GhostBodyLimit.
    /// </summary>
    public static class GhostBodyRanking
    {
        public static GhostRankedBody[] Rank(
            DivergenceResult[] fanDivergences,
            int[] stableBodyIds,
            int ghostBodyLimit)
        {
            if (ghostBodyLimit <= 0)
            {
                return Array.Empty<GhostRankedBody>();
            }

            if (stableBodyIds == null || stableBodyIds.Length == 0)
            {
                return Array.Empty<GhostRankedBody>();
            }

            var bodyCount = stableBodyIds.Length;
            var maxError = new float[bodyCount];

            if (fanDivergences != null)
            {
                for (var f = 0; f < fanDivergences.Length; f++)
                {
                    var divergence = fanDivergences[f];
                    if (!divergence.Succeeded ||
                        divergence.PerBodyMaxPositionErrorMetres == null ||
                        divergence.PerBodyMaxPositionErrorMetres.Length != bodyCount)
                    {
                        continue;
                    }

                    for (var b = 0; b < bodyCount; b++)
                    {
                        var error = divergence.PerBodyMaxPositionErrorMetres[b];
                        if (IsFinitePositive(error) && error > maxError[b])
                        {
                            maxError[b] = error;
                        }
                    }
                }
            }

            return Rank(stableBodyIds, maxError, ghostBodyLimit);
        }

        /// <summary>
        /// Rank from explicit per-body max errors aligned with <paramref name="bodyIds"/>.
        /// Omits non-positive / non-finite errors. Deterministic: error desc, bodyId asc.
        /// </summary>
        public static GhostRankedBody[] Rank(
            int[] bodyIds,
            float[] maxPositionErrorMetres,
            int ghostBodyLimit)
        {
            if (ghostBodyLimit <= 0 ||
                bodyIds == null ||
                bodyIds.Length == 0 ||
                maxPositionErrorMetres == null ||
                maxPositionErrorMetres.Length != bodyIds.Length)
            {
                return Array.Empty<GhostRankedBody>();
            }

            var candidates = new List<(int bodyId, float error, int index)>(bodyIds.Length);
            for (var b = 0; b < bodyIds.Length; b++)
            {
                var error = maxPositionErrorMetres[b];
                if (IsFinitePositive(error))
                {
                    candidates.Add((bodyIds[b], error, b));
                }
            }

            candidates.Sort(Compare);

            var take = Math.Min(ghostBodyLimit, candidates.Count);
            var ranked = new GhostRankedBody[take];
            for (var i = 0; i < take; i++)
            {
                ranked[i] = new GhostRankedBody(candidates[i].bodyId, candidates[i].error, i);
            }

            return ranked;
        }

        /// <summary>
        /// Comparator: max position error descending, then bodyId ascending.
        /// Exposed for EditMode contract tests.
        /// </summary>
        public static int Compare(
            (int bodyId, float error, int index) a,
            (int bodyId, float error, int index) b)
        {
            var errorCmp = b.error.CompareTo(a.error);
            if (errorCmp != 0)
            {
                return errorCmp;
            }

            return a.bodyId.CompareTo(b.bodyId);
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
