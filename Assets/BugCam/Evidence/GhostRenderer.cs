using System;
using System.Collections.Generic;
using BugCam.Core;
using UnityEngine;

namespace BugCam.Evidence
{
    /// <summary>
    /// Builds a pure-data <see cref="GhostDrawSet"/> from a ghost evidence document.
    /// Baseline white; fans colored by index; red first-divergence; distinct max-spread.
    /// No Editor APIs, no permanent GameObjects.
    /// </summary>
    public static class GhostRenderer
    {
        public static readonly Color BaselineColor = Color.white;

        public static readonly Color FirstDivergenceColor = new Color(1f, 0.15f, 0.15f, 1f);

        public static readonly Color MaxSpreadColor = new Color(1f, 0.85f, 0.1f, 1f);

        private static readonly Color[] FanPalette =
        {
            new Color(0.2f, 0.55f, 1f, 1f),
            new Color(0.15f, 0.85f, 0.55f, 1f),
            new Color(0.95f, 0.55f, 0.15f, 1f),
            new Color(0.75f, 0.35f, 0.95f, 1f),
            new Color(0.1f, 0.8f, 0.85f, 1f),
            new Color(0.95f, 0.35f, 0.55f, 1f),
            new Color(0.55f, 0.75f, 0.2f, 1f),
            new Color(0.35f, 0.45f, 0.95f, 1f),
            new Color(0.9f, 0.7f, 0.2f, 1f),
            new Color(0.4f, 0.9f, 0.4f, 1f),
            new Color(0.85f, 0.4f, 0.7f, 1f),
            new Color(0.25f, 0.65f, 0.75f, 1f),
            new Color(0.7f, 0.5f, 0.2f, 1f),
            new Color(0.5f, 0.3f, 0.85f, 1f),
            new Color(0.2f, 0.75f, 0.35f, 1f)
        };

        public static GhostDrawSet BuildDrawSet(GhostEvidenceDocument document)
        {
            if (document == null)
            {
                return GhostDrawSet.Empty;
            }

            var search = document.SearchResult;
            if (!search.Succeeded ||
                !search.BaselineRun.Succeeded ||
                document.RankedBodies == null ||
                document.RankedBodies.Length == 0)
            {
                return GhostDrawSet.Empty;
            }

            var polylines = new List<GhostPolyline>(
                (1 + document.Fans.Length) * document.RankedBodies.Length);
            var hasBounds = false;
            var bounds = new Bounds();

            for (var r = 0; r < document.RankedBodies.Length; r++)
            {
                var bodyId = document.RankedBodies[r].BodyId;
                var baselineIndex = GhostTrajectorySampler.FindBodyIndex(search.BaselineRun, bodyId);
                if (baselineIndex < 0)
                {
                    continue;
                }

                var baselinePoints = GhostTrajectorySampler.SampleBodyPositions(
                    search.BaselineRun,
                    baselineIndex);
                if (baselinePoints.Length > 0)
                {
                    polylines.Add(new GhostPolyline(
                        "baseline",
                        -1,
                        bodyId,
                        true,
                        BaselineColor,
                        baselinePoints));
                    ExpandBounds(ref hasBounds, ref bounds, baselinePoints);
                }

                for (var f = 0; f < document.Fans.Length; f++)
                {
                    var fan = document.Fans[f];
                    if (!fan.Run.Succeeded)
                    {
                        continue;
                    }

                    var fanBodyIndex = GhostTrajectorySampler.FindBodyIndex(fan.Run, bodyId);
                    if (fanBodyIndex < 0)
                    {
                        continue;
                    }

                    var fanPoints = GhostTrajectorySampler.SampleBodyPositions(
                        fan.Run,
                        fanBodyIndex);
                    if (fanPoints.Length == 0)
                    {
                        continue;
                    }

                    polylines.Add(new GhostPolyline(
                        FormatFanLabel(fan),
                        fan.FanIndex,
                        bodyId,
                        false,
                        FanColor(fan.FanIndex),
                        fanPoints));
                    ExpandBounds(ref hasBounds, ref bounds, fanPoints);
                }
            }

            var hasFirst = false;
            var firstWorld = Vector3.zero;
            var firstBodyId = -1;
            var hasMax = false;
            var maxWorld = Vector3.zero;
            var maxBodyId = -1;
            var markers = new List<GhostMarker>(2);

            if (document.HasPrimaryFan)
            {
                var primary = document.Fans[document.PrimaryFanIndex];
                var divergence = primary.Divergence;
                if (divergence.Succeeded && divergence.HasSignificantDivergence)
                {
                    // First-divergence marker: FirstDivergenceBodyId @ FirstDivergenceFrame.
                    // Never proxy MaxSpreadBodyId or AffectedBodyIds[0] (ID-sorted, not first-to-diverge).
                    firstBodyId = divergence.FirstDivergenceBodyId;
                    if (firstBodyId >= 0)
                    {
                        var bodyIndex = GhostTrajectorySampler.FindBodyIndex(
                            primary.Run,
                            firstBodyId);
                        if (bodyIndex >= 0 &&
                            GhostTrajectorySampler.TryGetBodyPosition(
                                primary.Run,
                                bodyIndex,
                                divergence.FirstDivergenceFrame,
                                out firstWorld))
                        {
                            hasFirst = true;
                            markers.Add(new GhostMarker(
                                "firstDivergence",
                                firstWorld,
                                FirstDivergenceColor,
                                true,
                                firstBodyId));
                            ExpandBounds(ref hasBounds, ref bounds, firstWorld);
                        }
                    }

                    maxBodyId = divergence.MaxSpreadBodyId;
                    var maxBodyIndex = GhostTrajectorySampler.FindBodyIndex(
                        primary.Run,
                        maxBodyId);
                    if (maxBodyIndex >= 0 &&
                        GhostTrajectorySampler.TryGetBodyPosition(
                            primary.Run,
                            maxBodyIndex,
                            divergence.MaxSpreadStep,
                            out maxWorld))
                    {
                        hasMax = true;
                        markers.Add(new GhostMarker(
                            "maxSpread",
                            maxWorld,
                            MaxSpreadColor,
                            true,
                            maxBodyId));
                        ExpandBounds(ref hasBounds, ref bounds, maxWorld);
                    }
                }
            }

            if (!hasFirst)
            {
                markers.Add(new GhostMarker(
                    "firstDivergence",
                    Vector3.zero,
                    FirstDivergenceColor,
                    false,
                    -1));
            }

            if (!hasMax)
            {
                markers.Add(new GhostMarker(
                    "maxSpread",
                    Vector3.zero,
                    MaxSpreadColor,
                    false,
                    -1));
            }

            return new GhostDrawSet(
                polylines.ToArray(),
                markers.ToArray(),
                hasBounds,
                bounds,
                firstWorld,
                hasFirst,
                firstBodyId,
                maxWorld,
                hasMax,
                maxBodyId);
        }

        public static Color FanColor(int fanIndex)
        {
            if (fanIndex < 0)
            {
                return BaselineColor;
            }

            return FanPalette[fanIndex % FanPalette.Length];
        }

        private static string FormatFanLabel(GhostFanEvidence fan)
        {
            return "fan[" + fan.FanIndex + "]×" + fan.Multiplier.ToString("0.###") +
                   AxisSuffix(fan.Axis);
        }

        private static string AxisSuffix(Vector3 axis)
        {
            if (axis == Vector3.right)
            {
                return "X";
            }

            if (axis == Vector3.up)
            {
                return "Y";
            }

            if (axis == Vector3.forward)
            {
                return "Z";
            }

            return "A";
        }

        private static void ExpandBounds(ref bool hasBounds, ref Bounds bounds, Vector3[] points)
        {
            for (var i = 0; i < points.Length; i++)
            {
                ExpandBounds(ref hasBounds, ref bounds, points[i]);
            }
        }

        private static void ExpandBounds(ref bool hasBounds, ref Bounds bounds, Vector3 point)
        {
            if (float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsNaN(point.z) ||
                float.IsInfinity(point.x) || float.IsInfinity(point.y) || float.IsInfinity(point.z))
            {
                return;
            }

            if (!hasBounds)
            {
                bounds = new Bounds(point, Vector3.zero);
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(point);
            }
        }
    }
}
