using BugCam.Core;
using UnityEditor;
using UnityEngine;

namespace BugCam.Editor
{
    /// <summary>
    /// Block 2.2.2 editor implementation of the Core mesh-resolve seam
    /// (docs/CONTRACT-2.2.2.md, адъюдикация №3: interface in Core, implementation here,
    /// no <c>#if UNITY_EDITOR</c> inside Core). Player builds and editor-less contexts
    /// have no provider, so every mesh capture/resolve there fails closed with the
    /// ratified provider-missing literal.
    /// </summary>
    internal sealed class EditorSceneMeshResolveProvider : ISceneMeshResolveProvider
    {
        public bool TryDescribeMeshAsset(
            Mesh mesh,
            out string assetGuid,
            out long localFileId,
            out string failureReason)
        {
            assetGuid = string.Empty;
            localFileId = 0L;
            if (mesh == null ||
                !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    mesh, out assetGuid, out localFileId) ||
                string.IsNullOrEmpty(assetGuid) ||
                string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(assetGuid)))
            {
                failureReason = "меш-ассет недоступен: " + (assetGuid ?? string.Empty) +
                                "/" + (mesh == null ? string.Empty : mesh.name) +
                                " не найден в проекте";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        public bool TryResolveMesh(
            string assetGuid,
            long localFileId,
            string meshName,
            out Mesh mesh,
            out string failureReason)
        {
            mesh = null;
            var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
            if (string.IsNullOrEmpty(assetPath))
            {
                failureReason = "меш-ассет недоступен: " + assetGuid + "/" + meshName +
                                " не найден в проекте";
                return false;
            }

            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (var i = 0; i < assets.Length; i++)
            {
                var candidate = assets[i] as Mesh;
                if (candidate == null)
                {
                    continue;
                }

                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        candidate, out var candidateGuid, out long candidateFileId) &&
                    candidateGuid == assetGuid &&
                    candidateFileId == localFileId)
                {
                    mesh = candidate;
                    failureReason = string.Empty;
                    return true;
                }
            }

            failureReason = "меш " + meshName + " не найден внутри ассета " + assetGuid;
            return false;
        }
    }

    internal static class SceneMeshResolveProviderInstaller
    {
        [InitializeOnLoadMethod]
        private static void Install()
        {
            SceneMeshResolve.Provider = new EditorSceneMeshResolveProvider();
        }
    }
}
