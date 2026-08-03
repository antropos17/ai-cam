namespace BugCam.Evidence
{
    /// <summary>
    /// Block 1.5 evidence bundle path and identity constants.
    /// Machine-readable kind/version for AI and tooling consumers of metrics.json.
    ///
    /// Canonical run layout under Library/BugCamEvidence/Runs/&lt;run-id&gt;/:
    ///   manifest.json, metrics.json, summary.md,
    ///   report/console-report.txt,
    ///   runs/baseline.json, runs/fan-00.json … fan-14.json (when retained),
    ///   visuals/overview.png, first-sustained-divergence.png,
    ///           maximum-spread.png, final-state.png
    /// Console report path is report/console-report.txt (not report.txt) — keeps
    /// console text distinct from other report formats.
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

        /// <summary>Per-run retained simulation JSON directory (baseline + fans).</summary>
        public const string RunsDirectoryName = "runs";

        public const string BaselineRunFileName = "baseline.json";

        public const string OverviewPngFileName = "overview.png";

        public const string FirstDivergencePngFileName = "first-sustained-divergence.png";

        public const string MaxSpreadPngFileName = "maximum-spread.png";

        public const string FinalPngFileName = "final-state.png";

        /// <summary>
        /// Relative fan-epsilon vs ReferenceEpsilon×multiplier tolerance.
        /// Fail closed when |ε - ref×m| / max(ref×m, eps) exceeds this.
        /// </summary>
        public const float FanEpsilonRelativeTolerance = 1e-4f;

        public static string RunRelativeDirectory(string runId)
        {
            return RunsRelativeRoot + "/" + (runId ?? string.Empty);
        }

        public static string FanRunFileName(int fanIndex)
        {
            return "fan-" + fanIndex.ToString("00") + ".json";
        }

        public static string ConsoleReportRelativePath =>
            ReportDirectoryName + "/" + ConsoleReportFileName;

        public static string BaselineRunRelativePath =>
            RunsDirectoryName + "/" + BaselineRunFileName;

        public static string FanRunRelativePath(int fanIndex)
        {
            return RunsDirectoryName + "/" + FanRunFileName(fanIndex);
        }

        public static string VisualRelativePath(string fileName)
        {
            return VisualsDirectoryName + "/" + fileName;
        }
    }

    /// <summary>Stable machine-readable error codes for failed evidence bundles (§15).</summary>
    public static class GhostEvidenceErrorCodes
    {
        public const string None = "";

        public const string SearchFailed = "SEARCH_FAILED";

        public const string CleanupTimeout = "CLEANUP_TIMEOUT";

        public const string BuildFailed = "BUILD_FAILED";

        public const string WriteFailed = "WRITE_FAILED";

        /// <summary>
        /// Block 2.2.1 A1: the persisted search entry could not be resolved into settings
        /// (e.g. the assigned DivergenceSettings asset vanished before the run) — the run
        /// fails closed instead of silently falling back to defaults.
        /// </summary>
        public const string SettingsResolveFailed = "SETTINGS_RESOLVE_FAILED";
    }
}
