using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace BugCam.Core
{
    /// <summary>
    /// Structured Block 1.1 TowerScene evidence lines written under Library/ (gitignored).
    /// </summary>
    public static class TowerCheckpointMetricsWriter
    {
        public static string Format(
            DeterminismProbeResult probe,
            int bodyCount,
            int stepCount,
            string phase,
            string gitCommit,
            string unityVersion,
            string platform,
            string operatingSystem,
            string scriptingBackend,
            string gravity,
            int solverIterations,
            int solverVelocityIterations,
            bool autoSyncTransforms,
            bool enhancedDeterminism,
            int threadingModeSerialized,
            bool sceneCleanupSucceeded,
            bool localPhysicsSceneValid)
        {
            var sb = new StringBuilder(2048);
            sb.AppendLine("BUGCAM_BLOCK_1_1_TOWER_CHECKPOINT");
            sb.AppendLine("phase=" + phase);
            sb.AppendLine("succeeded=" + probe.Succeeded);
            sb.AppendLine("errorReason=" + (probe.ErrorReason ?? string.Empty));
            sb.AppendLine("bodyCount=" + bodyCount);
            sb.AppendLine("simulatedStepCount=" + stepCount);
            sb.AppendLine(
                "fixedTimestep=" +
                BugCamConstants.FixedStep.ToString("R", CultureInfo.InvariantCulture));
            sb.AppendLine("stateStride=" + BugCamConstants.StateStride);
            sb.AppendLine("repeatabilityGate=" +
                          BugCamConstants.RepeatabilityGate.ToString(
                              "R",
                              CultureInfo.InvariantCulture));
            sb.AppendLine("threadingMode=" + probe.SimulationThreadingMode);
            sb.AppendLine("threadingModeSerialized=" + threadingModeSerialized);
            sb.AppendLine("unityVersion=" + unityVersion);
            sb.AppendLine("platform=" + platform);
            sb.AppendLine("operatingSystem=" + operatingSystem);
            sb.AppendLine("scriptingBackend=" + scriptingBackend);
            sb.AppendLine("gravity=" + gravity);
            sb.AppendLine("solverIterations=" + solverIterations);
            sb.AppendLine("solverVelocityIterations=" + solverVelocityIterations);
            sb.AppendLine("autoSyncTransforms=" + autoSyncTransforms);
            sb.AppendLine("enhancedDeterminism=" + enhancedDeterminism);
            sb.AppendLine("gitCommit=" + gitCommit);
            sb.AppendLine("sceneValidity=true");
            sb.AppendLine("physicsSceneValidity=" + localPhysicsSceneValid);
            sb.AppendLine("sceneCleanupResult=" + sceneCleanupSucceeded);
            sb.AppendLine("managedBytesAllocatedInLoop=" + probe.ManagedBytesAllocatedInLoop);

            sb.AppendLine("comparison=A_vs_Aprime");
            sb.AppendLine("bitwiseEqual=" + probe.RepeatBitwiseEqual);
            sb.AppendLine(
                "maxComponentDelta=" +
                probe.RepeatMaxComponentDelta.ToString("R", CultureInfo.InvariantCulture));
            sb.AppendLine("withinGate=" + probe.RepeatWithinGate);
            sb.AppendLine("firstDivergingStep=" + probe.RepeatFirstDivergingStep);
            sb.AppendLine("firstDivergingBody=" + probe.RepeatFirstDivergingBody);

            sb.AppendLine("comparison=A_vs_B");
            sb.AppendLine("bitwiseEqual=" + probe.PerturbedBitwiseEqual);
            sb.AppendLine(
                "maxComponentDelta=" +
                probe.PerturbedMaxComponentDelta.ToString("R", CultureInfo.InvariantCulture));
            sb.AppendLine("withinGate=" + probe.PerturbedWithinGate);
            sb.AppendLine("firstDivergingStep=" + probe.PerturbedFirstDivergingStep);
            sb.AppendLine("firstDivergingBody=" + probe.PerturbedFirstDivergingBody);
            return sb.ToString();
        }

        public static void WriteAtomic(string path, string contents)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = path + ".tmp";
            File.WriteAllText(tempPath, contents, Encoding.UTF8);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(tempPath, path);
        }
    }
}
