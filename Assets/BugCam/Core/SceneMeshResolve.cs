using UnityEngine;

namespace BugCam.Core
{
    /// <summary>
    /// Block 2.2.2 mesh-reference resolve seam (docs/CONTRACT-2.2.2.md, адъюдикация №3):
    /// no <c>#if UNITY_EDITOR</c> inside Core — the interface lives here, the
    /// implementation lives in BugCam.Editor, and a missing provider is fail-closed at
    /// both resolve points.
    /// </summary>
    public interface ISceneMeshResolveProvider
    {
        /// <summary>
        /// Capture point: identify the asset behind a scene mesh. On failure the reason
        /// must be one of the ratified fail-closed literals (asset / sub-asset not found).
        /// </summary>
        bool TryDescribeMeshAsset(
            Mesh mesh,
            out string assetGuid,
            out long localFileId,
            out string failureReason);

        /// <summary>
        /// Simulation point: resolve the referenced mesh again. Geometry is NOT read here
        /// (Amendment 2026-08-04) — the harness verifies the structural fingerprint only.
        /// <paramref name="meshName"/> is diagnostics-only for the ratified failure
        /// literals; it never participates in the resolve itself.
        /// </summary>
        bool TryResolveMesh(
            string assetGuid,
            long localFileId,
            string meshName,
            out Mesh mesh,
            out string failureReason);
    }

    public static class SceneMeshResolve
    {
        /// <summary>
        /// Injected by BugCam.Editor at editor load; null in player builds and headless
        /// contexts without the editor assembly — every mesh capture/resolve then fails
        /// closed with the ratified provider-missing literal.
        /// </summary>
        public static ISceneMeshResolveProvider Provider { get; set; }

        /// <summary>
        /// Simulation-point resolve failure code (адъюдикация №5): distinct from
        /// SCENE_CAPTURE_FAILED because it signals a broken evidence capsule — the asset
        /// changed or vanished AFTER capture.
        /// </summary>
        public const string SimulationResolveFailedCode = "SCENE_MESH_RESOLVE_FAILED";

        /// <summary>Ratified provider-missing fail-closed literal (both resolve points).</summary>
        public const string ProviderMissingReason =
            "провайдер меш-резолва отсутствует — захват мешей недоступен в этом окружении";
    }
}
