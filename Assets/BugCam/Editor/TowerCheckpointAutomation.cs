#if UNITY_EDITOR
using System;
using System.IO;
using BugCam.Core;
using UnityEditor;
using UnityEngine;

namespace BugCam.Editor
{
    /// <summary>
    /// Batchmode helpers for Block 1.1. Dual-mode measurement is orchestrated by external
    /// invocations that set m_ThreadingMode, then run the PlayMode TowerScene checkpoint filter.
    /// </summary>
    public static class TowerCheckpointAutomation
    {
        private static readonly string ProjectRoot = Path.GetFullPath(
            Path.Combine(Application.dataPath, ".."));
        private static readonly string EvidenceRoot = Path.Combine(
            ProjectRoot,
            "Library/BugCamEvidence/Block1.1");

        [MenuItem("BugCam/Write Environment Snapshot")]
        public static void WriteEnvironmentSnapshotMenu()
        {
            WriteEnvironmentSnapshot("manual");
        }

        public static void WriteEnvironmentSnapshotBatch()
        {
            var phase = Environment.GetEnvironmentVariable("BUGCAM_CHECKPOINT_PHASE")
                        ?? "batch";
            WriteEnvironmentSnapshot(phase);
            ExitBatch(0);
        }

        public static void ApplyThreadingModeBatch()
        {
            try
            {
                var raw = Environment.GetEnvironmentVariable("BUGCAM_THREADING_MODE");
                if (string.IsNullOrWhiteSpace(raw))
                {
                    throw new InvalidOperationException(
                        "BUGCAM_THREADING_MODE must be 0 (MultiThreaded) or 1 (SingleThreaded).");
                }

                if (!int.TryParse(raw.Trim(), out var modeValue) ||
                    (modeValue != 0 && modeValue != 1))
                {
                    throw new InvalidOperationException(
                        "BUGCAM_THREADING_MODE must be 0 (MultiThreaded) or 1 (SingleThreaded). Got: " +
                        raw);
                }

                var mode = modeValue == 0
                    ? SimulationThreadingMode.MultiThreaded
                    : SimulationThreadingMode.SingleThreaded;
                PhysicsSettingsProbe.SetThreadingMode(mode);
                var serialized = PhysicsSettingsProbe.ReadThreadingModeSerialized();
                if (serialized != modeValue)
                {
                    throw new InvalidOperationException(
                        "Failed to persist m_ThreadingMode. Expected " + modeValue +
                        " but read " + serialized + ".");
                }

                Debug.Log(
                    "BUGCAM_THREADING_MODE_APPLIED mode=" + mode +
                    " serialized=" + serialized);
                ExitBatch(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("BUGCAM_THREADING_MODE_FAILED " + exception.Message);
                ExitBatch(1);
            }
        }

        public static void RestoreMultiThreadedBatch()
        {
            Environment.SetEnvironmentVariable("BUGCAM_THREADING_MODE", "0");
            ApplyThreadingModeBatch();
        }

        public static void WriteEnvironmentSnapshot(string phase)
        {
            Directory.CreateDirectory(EvidenceRoot);
            var gitCommit = Environment.GetEnvironmentVariable("BUGCAM_GIT_COMMIT")
                            ?? "unknown";
            var path = Path.Combine(EvidenceRoot, phase, "environment.txt");
            PhysicsSettingsProbe.WriteEnvironmentSnapshot(path, gitCommit, phase);
            Debug.Log("BUGCAM_ENVIRONMENT_SNAPSHOT_WRITTEN path=" + path);
        }

        private static void ExitBatch(int code)
        {
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(code);
            }
        }
    }
}
#endif
