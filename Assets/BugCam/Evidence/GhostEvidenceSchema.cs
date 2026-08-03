namespace BugCam.Evidence
{
    /// <summary>
    /// Block 1.5 evidence bundle path and identity constants.
    /// Machine-readable kind/version for AI and tooling consumers of metrics.json.
    /// </summary>
    public static class GhostEvidenceSchema
    {
        public const int SchemaVersion = 1;

        public const string Kind = "BugCam.GhostEvidence";

        /// <summary>Checkpoint aggregate root under the Unity project Library folder.</summary>
        public const string CheckpointRelativeRoot = "Library/BugCamEvidence/Block1.5";

        /// <summary>Per-run evidence root under Library (run id appended).</summary>
        public const string RunsRelativeRoot = "Library/BugCamEvidence/Runs";

        public const string MetricsFileName = "metrics.json";

        public const string ManifestFileName = "manifest.json";

        public const string SummaryFileName = "summary.md";

        public const string ConsoleReportFileName = "console-report.txt";

        public const string ReportDirectoryName = "report";

        public const string VisualsDirectoryName = "visuals";

        public const string OverviewPngFileName = "overview.png";

        public const string FirstDivergencePngFileName = "first-divergence.png";

        public const string MaxSpreadPngFileName = "max-spread.png";

        public const string FinalPngFileName = "final.png";

        public static string RunRelativeDirectory(string runId)
        {
            return RunsRelativeRoot + "/" + (runId ?? string.Empty);
        }
    }
}
