using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BugCam.Tests
{
    /// <summary>
    /// Block 1.1 TowerScene A/B/A-prime checkpoint for the active physics threading mode.
    /// Dual-mode and pre/post Editor-restart orchestration is done by batchmode runs that
    /// patch m_ThreadingMode, then invoke this filtered PlayMode test.
    /// </summary>
    public sealed class TowerSceneDeterminismCheckpointTests
    {
        private const string EvidenceRoot = "Library/BugCamEvidence/Block1.1";
        private const string LatestMetricsFileName = "latest-tower-metrics.txt";

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator TowerSceneCheckpoint_RecordsAbaMetricsForCurrentThreadingMode()
        {
            Assert.That(
                Physics.simulationMode,
                Is.EqualTo(SimulationMode.Script),
                "Tower checkpoint requires Physics.simulationMode = Script.");

            var factoryType = Type.GetType(
                "BugCam.Core.TowerProbeRequestFactory, BugCam.Core");
            var probeType = Type.GetType("BugCam.Core.DeterminismProbe, BugCam.Core");
            var probeResultType = Type.GetType(
                "BugCam.Core.DeterminismProbeResult, BugCam.Core");
            var requestType = Type.GetType("BugCam.Core.SimulationRequest, BugCam.Core");
            var threadingModeType = Type.GetType(
                "BugCam.Core.SimulationThreadingMode, BugCam.Core");
            var writerType = Type.GetType(
                "BugCam.Core.TowerCheckpointMetricsWriter, BugCam.Core");
            var settingsProbeType = Type.GetType(
                "BugCam.Editor.PhysicsSettingsProbe, BugCam.Editor");

            Assert.That(factoryType, Is.Not.Null);
            Assert.That(probeType, Is.Not.Null);
            Assert.That(probeResultType, Is.Not.Null);
            Assert.That(writerType, Is.Not.Null);
            Assert.That(
                settingsProbeType,
                Is.Not.Null,
                "Threading mode must be read from DynamicsManager via PhysicsSettingsProbe.");

            var createBaseline = factoryType.GetMethod(
                "CreateBaseline",
                new[] { typeof(int) });
            var createPerturbed = factoryType.GetMethod(
                "CreatePerturbed",
                new[] { typeof(int), typeof(float) });
            var readMode = settingsProbeType.GetMethod("ReadThreadingMode", Type.EmptyTypes);
            var readModeSerialized = settingsProbeType.GetMethod(
                "ReadThreadingModeSerialized",
                Type.EmptyTypes);
            var readEnhanced = settingsProbeType.GetMethod(
                "ReadEnhancedDeterminism",
                Type.EmptyTypes);
            var formatMethod = writerType.GetMethod(
                "Format",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var writeMethod = writerType.GetMethod(
                "WriteAtomic",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

            Assert.That(createBaseline, Is.Not.Null);
            Assert.That(createPerturbed, Is.Not.Null);
            Assert.That(readMode, Is.Not.Null);
            Assert.That(formatMethod, Is.Not.Null);
            Assert.That(writeMethod, Is.Not.Null);

            const int stepCount = 250;
            const float perturbationMetres = 0.001f;
            var baselineRequest = createBaseline.Invoke(null, new object[] { stepCount });
            var perturbedRequest = createPerturbed.Invoke(
                null,
                new object[] { stepCount, perturbationMetres });
            var threadingMode = readMode.Invoke(null, null);
            var threadingModeSerialized = (int)readModeSerialized.Invoke(null, null);
            var enhancedDeterminism = (bool)readEnhanced.Invoke(null, null);
            Assert.That(
                enhancedDeterminism,
                Is.False,
                "Enhanced Determinism must remain disabled for Block 1.1 evidence.");

            var bodies = requestType.GetProperty("Bodies")?.GetValue(baselineRequest) as Array;
            Assert.That(bodies, Has.Length.EqualTo(49));

            var initialSceneCount = SceneManager.sceneCount;
            var runMethod = probeType.GetMethod(
                "Run",
                new[] { requestType, requestType, threadingModeType });
            Assert.That(runMethod, Is.Not.Null);

            var probeResult = runMethod.Invoke(
                Activator.CreateInstance(probeType),
                new[] { baselineRequest, perturbedRequest, threadingMode });

            var succeeded = (bool)probeResultType.GetProperty("Succeeded")!.GetValue(probeResult)!;
            var errorReason =
                probeResultType.GetProperty("ErrorReason")!.GetValue(probeResult) as string;
            Assert.That(
                succeeded,
                Is.True,
                "Tower A/B/A-prime probe must succeed. ErrorReason: " + errorReason);

            Assert.That(
                probeResultType.GetProperty("BodyCount")!.GetValue(probeResult),
                Is.EqualTo(49));
            Assert.That(
                probeResultType.GetProperty("StepCount")!.GetValue(probeResult),
                Is.EqualTo(stepCount));
            Assert.That(
                probeResultType.GetProperty("LocalPhysicsSceneValid")!.GetValue(probeResult),
                Is.True);
            Assert.That(
                probeResultType.GetProperty("TemporaryScenesUnloadRequested")!.GetValue(probeResult),
                Is.True);
            Assert.That(
                probeResultType.GetProperty("RepeatWithinGate")!.GetValue(probeResult),
                Is.True,
                "A vs A-prime must stay within the 1e-6 repeatability gate.");
            Assert.That(
                probeResultType.GetProperty("ManagedBytesAllocatedInLoop")!.GetValue(probeResult),
                Is.EqualTo(0L),
                "Tower simulate loop must allocate zero managed bytes.");
            Assert.That(
                probeResultType.GetProperty("SimulationThreadingMode")!.GetValue(probeResult),
                Is.EqualTo(threadingMode));

            // Sensitivity between A and B is expected and is not a failure of this gate.
            var perturbedFirstStep = (int)probeResultType
                .GetProperty("PerturbedFirstDivergingStep")!
                .GetValue(probeResult)!;
            Assert.That(
                perturbedFirstStep,
                Is.GreaterThanOrEqualTo(0),
                "Documented 1mm projectile perturbation must produce an A/B divergence.");

            for (var frame = 0; frame < 60 && SceneManager.sceneCount != initialSceneCount; frame++)
            {
                yield return null;
            }

            var sceneCleanupSucceeded = SceneManager.sceneCount == initialSceneCount;
            Assert.That(
                sceneCleanupSucceeded,
                Is.True,
                "All temporary Tower Physics3D scenes must unload after A/B/A-prime.");

            var phase = Environment.GetEnvironmentVariable("BUGCAM_CHECKPOINT_PHASE")
                        ?? "unspecified";
            var gitCommit = Environment.GetEnvironmentVariable("BUGCAM_GIT_COMMIT")
                            ?? TryReadGitCommit();
            var scriptingBackend = Environment.GetEnvironmentVariable("BUGCAM_SCRIPTING_BACKEND")
                                   ?? "EditorPlayMode";

            var contents = (string)formatMethod.Invoke(
                null,
                new object[]
                {
                    probeResult,
                    49,
                    stepCount,
                    phase,
                    gitCommit,
                    Application.unityVersion,
                    Application.platform.ToString(),
                    SystemInfo.operatingSystem,
                    scriptingBackend,
                    Physics.gravity.ToString("R", CultureInfo.InvariantCulture),
                    Physics.defaultSolverIterations,
                    Physics.defaultSolverVelocityIterations,
                    Physics.autoSyncTransforms,
                    enhancedDeterminism,
                    threadingModeSerialized,
                    sceneCleanupSucceeded,
                    true
                })!;

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var latestPath = Path.Combine(projectRoot, EvidenceRoot, LatestMetricsFileName);
            writeMethod.Invoke(null, new object[] { latestPath, contents });

            var modeName = threadingMode.ToString();
            var labeledPath = Path.Combine(
                projectRoot,
                EvidenceRoot,
                phase,
                modeName + ".metrics.txt");
            writeMethod.Invoke(null, new object[] { labeledPath, contents });

            UnityEngine.Debug.Log(
                "BUGCAM_TOWER_CHECKPOINT_WRITTEN path=" + labeledPath +
                " mode=" + modeName +
                " phase=" + phase +
                " repeatMaxDelta=" +
                probeResultType.GetProperty("RepeatMaxComponentDelta")!.GetValue(probeResult) +
                " perturbedMaxDelta=" +
                probeResultType.GetProperty("PerturbedMaxComponentDelta")!.GetValue(probeResult));
        }

        private static string TryReadGitCommit()
        {
            try
            {
                var projectRoot = Path.GetFullPath(
                    Path.Combine(Application.dataPath, ".."));
                var startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "rev-parse HEAD",
                    WorkingDirectory = projectRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    return "unknown";
                }

                var output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(5000);
                return string.IsNullOrEmpty(output) ? "unknown" : output;
            }
            catch
            {
                return "unknown";
            }
        }
    }
}
