using System;
using UnityEngine;

namespace BugCam.Evidence
{
    /// <summary>One polyline trajectory for Scene View / screenshot rendering.</summary>
    public sealed class GhostPolyline
    {
        public GhostPolyline(
            string runLabel,
            int fanIndex,
            int bodyId,
            bool isBaseline,
            Color color,
            Vector3[] points)
        {
            RunLabel = runLabel ?? string.Empty;
            FanIndex = fanIndex;
            BodyId = bodyId;
            IsBaseline = isBaseline;
            Color = color;
            Points = points ?? Array.Empty<Vector3>();
        }

        public string RunLabel { get; }

        /// <summary>-1 for baseline.</summary>
        public int FanIndex { get; }

        public int BodyId { get; }

        public bool IsBaseline { get; }

        public Color Color { get; }

        public Vector3[] Points { get; }
    }

    /// <summary>World-space marker (first divergence / max spread).</summary>
    public sealed class GhostMarker
    {
        public GhostMarker(string kind, Vector3 position, Color color, bool available, int bodyId = -1)
        {
            Kind = kind ?? string.Empty;
            Position = position;
            Color = color;
            Available = available;
            BodyId = bodyId;
        }

        public string Kind { get; }

        public Vector3 Position { get; }

        public Color Color { get; }

        public bool Available { get; }

        /// <summary>Stable body id sampled for this marker, or -1 when unavailable.</summary>
        public int BodyId { get; }
    }

    /// <summary>
    /// Pure-data draw set for Scene View Handles — no Editor APIs, no GameObjects.
    /// First-divergence marker uses FirstDivergenceBodyId @ FirstDivergenceFrame;
    /// max-spread marker uses MaxSpreadBodyId @ MaxSpreadStep — independent selections.
    /// </summary>
    public sealed class GhostDrawSet
    {
        private static readonly GhostPolyline[] EmptyPolylines = Array.Empty<GhostPolyline>();
        private static readonly GhostMarker[] EmptyMarkers = Array.Empty<GhostMarker>();

        public static GhostDrawSet Empty { get; } = new GhostDrawSet(
            EmptyPolylines,
            EmptyMarkers,
            false,
            default,
            default,
            false,
            -1,
            default,
            false,
            -1);

        public GhostDrawSet(
            GhostPolyline[] polylines,
            GhostMarker[] markers,
            bool hasBounds,
            Bounds worldBounds,
            Vector3 firstDivergenceWorld,
            bool hasFirstDivergence,
            int firstDivergenceBodyId,
            Vector3 maxSpreadWorld,
            bool hasMaxSpread,
            int maxSpreadBodyId)
        {
            Polylines = polylines ?? EmptyPolylines;
            Markers = markers ?? EmptyMarkers;
            HasBounds = hasBounds;
            WorldBounds = worldBounds;
            FirstDivergenceWorld = firstDivergenceWorld;
            HasFirstDivergence = hasFirstDivergence;
            FirstDivergenceBodyId = firstDivergenceBodyId;
            MaxSpreadWorld = maxSpreadWorld;
            HasMaxSpread = hasMaxSpread;
            MaxSpreadBodyId = maxSpreadBodyId;
        }

        public GhostPolyline[] Polylines { get; }

        public GhostMarker[] Markers { get; }

        public bool HasBounds { get; }

        public Bounds WorldBounds { get; }

        public Vector3 FirstDivergenceWorld { get; }

        public bool HasFirstDivergence { get; }

        public int FirstDivergenceBodyId { get; }

        public Vector3 MaxSpreadWorld { get; }

        public bool HasMaxSpread { get; }

        public int MaxSpreadBodyId { get; }
    }
}
