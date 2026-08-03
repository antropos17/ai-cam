using System;
using BugCam.Core;
using UnityEngine;

namespace BugCam.Evidence
{
    /// <summary>
    /// Search identity captured at evidence build time — not reconstructed from Core results
    /// (EpsilonSearchResult does not retain these fields).
    /// </summary>
    public readonly struct GhostSearchIdentity
    {
        public GhostSearchIdentity(
            int targetBodyId,
            Vector3 searchAxis,
            EpsilonSearchStrategy strategy)
        {
            TargetBodyId = targetBodyId;
            SearchAxis = searchAxis;
            Strategy = strategy;
        }

        public int TargetBodyId { get; }

        public Vector3 SearchAxis { get; }

        public EpsilonSearchStrategy Strategy { get; }
    }

    /// <summary>One retained fan run plus its re-analyzed divergence vs baseline.</summary>
    public sealed class GhostFanEvidence
    {
        public GhostFanEvidence(
            int fanIndex,
            float multiplier,
            Vector3 axis,
            float epsilonMetres,
            bool outsideSearchRange,
            RunResult run,
            DivergenceResult divergence)
        {
            FanIndex = fanIndex;
            Multiplier = multiplier;
            Axis = axis;
            EpsilonMetres = epsilonMetres;
            OutsideSearchRange = outsideSearchRange;
            Run = run;
            Divergence = divergence;
        }

        public int FanIndex { get; }

        public float Multiplier { get; }

        public Vector3 Axis { get; }

        public float EpsilonMetres { get; }

        public bool OutsideSearchRange { get; }

        public RunResult Run { get; }

        public DivergenceResult Divergence { get; }
    }

    /// <summary>Top-N ranked ghost body for visualization and metrics.</summary>
    public readonly struct GhostRankedBody
    {
        public GhostRankedBody(int bodyId, float maxPositionErrorMetres, int rank)
        {
            BodyId = bodyId;
            MaxPositionErrorMetres = maxPositionErrorMetres;
            Rank = rank;
        }

        public int BodyId { get; }

        public float MaxPositionErrorMetres { get; }

        public int Rank { get; }
    }

    /// <summary>
    /// Block 1.5 evidence document — single source of truth after
    /// <see cref="GhostEvidenceBuilder.Build"/>.
    /// </summary>
    public sealed class GhostEvidenceDocument
    {
        private static readonly GhostFanEvidence[] EmptyFans = Array.Empty<GhostFanEvidence>();
        private static readonly GhostRankedBody[] EmptyBodies = Array.Empty<GhostRankedBody>();

        public GhostEvidenceDocument(
            string runId,
            EpsilonSearchResult searchResult,
            GhostSearchIdentity searchIdentity,
            int ghostBodyLimit,
            int primaryFanIndex,
            bool hasPrimaryFan,
            GhostFanEvidence[] fans,
            GhostRankedBody[] rankedBodies,
            DivergenceResult primaryDivergence,
            GhostDrawSet drawSet)
        {
            SchemaVersion = GhostEvidenceSchema.SchemaVersion;
            Kind = GhostEvidenceSchema.Kind;
            RunId = runId ?? string.Empty;
            SearchResult = searchResult;
            SearchIdentity = searchIdentity;
            GhostBodyLimit = ghostBodyLimit;
            PrimaryFanIndex = primaryFanIndex;
            HasPrimaryFan = hasPrimaryFan;
            Fans = fans ?? EmptyFans;
            RankedBodies = rankedBodies ?? EmptyBodies;
            PrimaryDivergence = primaryDivergence;
            DrawSet = drawSet ?? GhostDrawSet.Empty;
            BuiltUtc = DateTime.UtcNow;
        }

        public int SchemaVersion { get; }

        public string Kind { get; }

        public string RunId { get; }

        public DateTime BuiltUtc { get; }

        public EpsilonSearchResult SearchResult { get; }

        public GhostSearchIdentity SearchIdentity { get; }

        public int GhostBodyLimit { get; }

        /// <summary>Index into <see cref="Fans"/>, or -1 when no primary.</summary>
        public int PrimaryFanIndex { get; }

        public bool HasPrimaryFan { get; }

        public GhostFanEvidence[] Fans { get; }

        public GhostRankedBody[] RankedBodies { get; }

        public DivergenceResult PrimaryDivergence { get; }

        public GhostDrawSet DrawSet { get; }
    }

    /// <summary>Outcome of <see cref="GhostEvidenceBuilder.Build"/>.</summary>
    public readonly struct GhostEvidenceBuildResult
    {
        private GhostEvidenceBuildResult(bool succeeded, string errorReason, GhostEvidenceDocument document)
        {
            Succeeded = succeeded;
            ErrorReason = errorReason ?? string.Empty;
            Document = document;
        }

        public bool Succeeded { get; }

        public string ErrorReason { get; }

        public GhostEvidenceDocument Document { get; }

        public static GhostEvidenceBuildResult Success(GhostEvidenceDocument document)
        {
            return new GhostEvidenceBuildResult(true, string.Empty, document);
        }

        public static GhostEvidenceBuildResult Failure(string errorReason)
        {
            return new GhostEvidenceBuildResult(false, errorReason, null);
        }
    }

    /// <summary>Outcome of <see cref="GhostEvidenceWriter.Write"/>.</summary>
    public readonly struct GhostEvidenceWriteResult
    {
        private GhostEvidenceWriteResult(
            bool succeeded,
            string errorReason,
            string runDirectory,
            string metricsPath,
            string summaryPath,
            string consoleReportPath)
        {
            Succeeded = succeeded;
            ErrorReason = errorReason ?? string.Empty;
            RunDirectory = runDirectory ?? string.Empty;
            MetricsPath = metricsPath ?? string.Empty;
            SummaryPath = summaryPath ?? string.Empty;
            ConsoleReportPath = consoleReportPath ?? string.Empty;
        }

        public bool Succeeded { get; }

        public string ErrorReason { get; }

        public string RunDirectory { get; }

        public string MetricsPath { get; }

        public string SummaryPath { get; }

        public string ConsoleReportPath { get; }

        public static GhostEvidenceWriteResult Success(
            string runDirectory,
            string metricsPath,
            string summaryPath,
            string consoleReportPath)
        {
            return new GhostEvidenceWriteResult(
                true,
                string.Empty,
                runDirectory,
                metricsPath,
                summaryPath,
                consoleReportPath);
        }

        public static GhostEvidenceWriteResult Failure(string errorReason)
        {
            return new GhostEvidenceWriteResult(
                false,
                errorReason,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);
        }
    }
}
