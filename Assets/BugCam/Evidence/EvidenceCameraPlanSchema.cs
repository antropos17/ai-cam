namespace BugCam.Evidence
{
    /// <summary>
    /// Block 2.1 camera-plan schema and path constants. Machine-readable kind/version for AI and
    /// tooling consumers of camera-plan.json.
    ///
    /// Shares the per-run root with Block 1.5 (<see cref="GhostEvidenceSchema.RunsRelativeRoot"/>):
    /// Library/BugCamEvidence/Runs/&lt;run-id&gt;/camera-plan.json. Not yet wired into the Ghost
    /// Visualization Window/Host pipeline — that wiring, plus RetroPlayer and 2x2 compositing, is
    /// deferred to a follow-up commit (docs/PLAN.md Block 2.1 "landing scope" note).
    /// </summary>
    public static class EvidenceCameraPlanSchema
    {
        public const int SchemaVersion = 1;

        public const string Kind = "BugCam.EvidenceCameraPlan";

        public const string FileName = "camera-plan.json";

        public static string RelativePath(string runId)
        {
            return GhostEvidenceSchema.RunRelativeDirectory(runId) + "/" + FileName;
        }
    }
}
