using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BugCam.Tests
{
    /// <summary>
    /// Optional PlayMode smoke: real TowerScene epsilon search → ghost evidence bundle.
    /// Uses FastStepCount (not VerifyStepCount=32) so this stays a smoke, not VERIFY step-32.
    /// Reflection zero-ref pattern.
    /// </summary>
    public sealed class GhostEvidencePlayModeTests
    {
        private const int FastStepCount = 40;

        [UnityTest]
        public IEnumerator TowerSearchWritesGhostEvidenceBundleAndLogsReports()
        {
            Assert.That(Physics.simulationMode, Is.EqualTo(SimulationMode.Script));

            var initialSceneCount = SceneManager.sceneCount;
            object searchResult = null;

            yield return RunSearch(r => searchResult = r);

            Assert.That(searchResult, Is.Not.Null);
            Assert.That(Prop<bool>(searchResult, "Succeeded"), Is.True, Prop<string>(searchResult, "ErrorReason"));

            var settingsType = Type.GetType("BugCam.Core.DivergenceSettings, BugCam.Core");
            var builderType = Type.GetType("BugCam.Evidence.GhostEvidenceBuilder, BugCam.Evidence");
            var identityType = Type.GetType("BugCam.Evidence.GhostSearchIdentity, BugCam.Evidence");
            var writerType = Type.GetType("BugCam.Evidence.GhostEvidenceWriter, BugCam.Evidence");
            var reportType = Type.GetType("BugCam.Evidence.GhostEvidenceReport, BugCam.Evidence");
            var epsilonReportType = Type.GetType("BugCam.Core.EpsilonSearchReport, BugCam.Core");
            var strategyType = Type.GetType("BugCam.Core.EpsilonSearchStrategy, BugCam.Core");

            var settings = settingsType.GetMethod("CreateDefault").Invoke(null, null);
            var identity = Activator.CreateInstance(
                identityType,
                49,
                Vector3.right,
                Enum.ToObject(strategyType, 0));
            var envType = Type.GetType("BugCam.Evidence.GhostRunEnvironment, BugCam.Evidence");
            var environment = Activator.CreateInstance(
                envType,
                Application.unityVersion ?? string.Empty,
                string.Empty,
                string.Empty,
                SceneManager.GetActiveScene().path ?? string.Empty);

            object document;
            try
            {
                var bodyCount = Prop<int>(Prop<object>(searchResult, "BaselineRun"), "BodyCount");
                var scales = new float[bodyCount];
                for (var i = 0; i < bodyCount; i++)
                {
                    scales[i] = 1f;
                }

                var provenanceType = Type.GetType(
                    "BugCam.Evidence.GhostSettingsProvenance, BugCam.Evidence");
                var build = builderType.GetMethod(
                    "Build",
                    new[]
                    {
                        searchResult.GetType(),
                        identityType,
                        settingsType,
                        typeof(float[]),
                        typeof(string),
                        envType,
                        provenanceType
                    }).Invoke(
                    null,
                    new object[]
                    {
                        searchResult,
                        identity,
                        settings,
                        scales,
                        "playmode-smoke-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
                        environment,
                        Activator.CreateInstance(provenanceType)
                    });

                Assert.That(Prop<bool>(build, "Succeeded"), Is.True, Prop<string>(build, "ErrorReason"));
                document = Prop<object>(build, "Document");
                Assert.That(Prop<bool>(document, "Success"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate((UnityEngine.Object)settings);
            }

            var epsilonText = (string)epsilonReportType.GetMethod("Format")
                .Invoke(null, new object[] { searchResult });
            var ghostText = (string)reportType.GetMethod("Format")
                .Invoke(null, new object[] { document });
            Debug.Log(epsilonText);
            Debug.Log(ghostText);
            Assert.That(ghostText, Does.Contain("BUGCAM_BLOCK_1_5_GHOST_EVIDENCE"));

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var write = writerType.GetMethod(
                "Write",
                new[] { document.GetType(), typeof(string) }).Invoke(
                null,
                new object[] { document, projectRoot });
            Assert.That(Prop<bool>(write, "Succeeded"), Is.True, Prop<string>(write, "ErrorReason"));

            var runDir = Prop<string>(write, "RunDirectory");
            Assert.That(Directory.Exists(runDir), Is.True);
            Assert.That(File.Exists(Path.Combine(runDir, "metrics.json")), Is.True);
            Assert.That(
                File.Exists(Path.Combine(runDir, "report", "console-report.txt")),
                Is.True);
            Assert.That(File.Exists(Path.Combine(runDir, "runs", "baseline.json")), Is.True);

            // STABLE ⇒ 0 fans; otherwise Core retains exactly 15.
            var fanCount = Arr(document, "Fans").Length;
            var verdict = Prop<string>(searchResult, "Verdict");
            if (verdict == "STABLE WITHIN TESTED RANGE")
            {
                Assert.That(fanCount, Is.EqualTo(0));
                Assert.That(
                    Directory.GetFiles(Path.Combine(runDir, "runs"), "fan-*.json").Length,
                    Is.EqualTo(0));
            }
            else
            {
                Assert.That(fanCount, Is.EqualTo(15));
                for (var i = 0; i < 15; i++)
                {
                    Assert.That(
                        File.Exists(Path.Combine(runDir, "runs", "fan-" + i.ToString("00") + ".json")),
                        Is.True);
                }
            }

            Assert.That(Prop<bool>(searchResult, "Succeeded"), Is.True);

            // FastStepCount=40 on this tower → DIVERGENT AT SEARCH FLOOR (not VERIFY step 32).
            // Honest success path: no threshold estimate; characterization fans retained.
            if (verdict == "DIVERGENT AT SEARCH FLOOR")
            {
                Assert.That(Prop<bool>(searchResult, "HasThresholdEstimate"), Is.False);
                Assert.That(fanCount, Is.EqualTo(15));
                var json = (string)writerType.GetMethod("BuildMetricsJson")
                    .Invoke(null, new object[] { document });
                Assert.That(json, Does.Contain("\"hasThresholdEstimate\":false"));
                Assert.That(json, Does.Contain("\"thresholdEstimateMetres\":null"));
                Assert.That(json, Does.Contain("\"retainedFanCount\":15"));
                Assert.That(ghostText, Does.Contain("hasThresholdEstimate=False"));
                Assert.That(ghostText, Does.Contain("thresholdEstimateMetres=null"));
            }

            yield return WaitCleanup(initialSceneCount);
            Assert.That(SceneManager.sceneCount, Is.EqualTo(initialSceneCount));
        }

        [UnityTest]
        public IEnumerator NestedRunnerYieldReturnFullyExecutesAcrossEditorUpdates()
        {
            // Proves the Host MonoBehaviour path semantics: yield return runner.Run(...)
            // fully executes nested waits (same pump Unity uses for GhostEvidencePlayModeHost).
            Assert.That(Physics.simulationMode, Is.EqualTo(SimulationMode.Script));
            var initialSceneCount = SceneManager.sceneCount;
            object searchResult = null;

            yield return RunSearch(r => searchResult = r);

            Assert.That(searchResult, Is.Not.Null);
            Assert.That(
                Prop<bool>(searchResult, "Succeeded"),
                Is.True,
                Prop<string>(searchResult, "ErrorReason"));

            var fanCount = Arr(searchResult, "FanRuns").Length;
            var verdict = Prop<string>(searchResult, "Verdict");
            if (verdict == "STABLE WITHIN TESTED RANGE")
            {
                Assert.That(fanCount, Is.EqualTo(0));
            }
            else
            {
                Assert.That(
                    fanCount,
                    Is.EqualTo(15),
                    "Characterization must retain exactly 15 fans when not STABLE.");
            }

            yield return WaitCleanup(initialSceneCount);
        }

        private static IEnumerator RunSearch(Action<object> onCompleted)
        {
            var searchType = Type.GetType("BugCam.Core.EpsilonSearch, BugCam.Core");
            var settingsType = Type.GetType("BugCam.Core.EpsilonSearchSettings, BugCam.Core");
            var runnerType = Type.GetType("BugCam.Core.EpsilonSearchRunner, BugCam.Core");
            var factoryType = Type.GetType("BugCam.Core.TowerProbeRequestFactory, BugCam.Core");
            var thresholdsType = Type.GetType("BugCam.Core.DivergenceThresholds, BugCam.Core");
            var strategyType = Type.GetType("BugCam.Core.EpsilonSearchStrategy, BugCam.Core");

            var settings = settingsType.GetProperty("Default").GetValue(null);
            var strategy = Enum.ToObject(strategyType, 0);
            var search = Activator.CreateInstance(
                searchType,
                settings,
                49,
                Vector3.right,
                strategy,
                0f);

            var baselineRequest = factoryType.GetMethod("CreateBaseline", new[] { typeof(int) })
                .Invoke(null, new object[] { FastStepCount });
            var bodies = Prop<object>(baselineRequest, "Bodies");
            var thresholds = thresholdsType.GetProperty("Default").GetValue(null);
            var bodyCount = ((Array)bodies).Length;
            var scales = new float[bodyCount];
            for (var i = 0; i < bodyCount; i++)
            {
                scales[i] = 1f;
            }

            var runner = Activator.CreateInstance(runnerType);
            var run = runnerType.GetMethod("Run");
            var enumerator = (IEnumerator)run.Invoke(
                runner,
                new object[] { search, bodies, FastStepCount, thresholds, scales });
            while (enumerator.MoveNext())
            {
                yield return enumerator.Current;
            }

            onCompleted(Prop<object>(runner, "LastResult"));
        }

        private static IEnumerator WaitCleanup(int initialSceneCount)
        {
            var runnerType = Type.GetType("BugCam.Core.EpsilonSearchRunner, BugCam.Core");
            var wait = runnerType.GetMethod("WaitForSceneCleanup", new[] { typeof(int) });
            var enumerator = (IEnumerator)wait.Invoke(null, new object[] { initialSceneCount });
            while (enumerator.MoveNext())
            {
                yield return enumerator.Current;
            }
        }

        private static Array Arr(object target, string name)
        {
            return (Array)target.GetType().GetProperty(name).GetValue(target);
        }

        private static T Prop<T>(object target, string name)
        {
            return (T)target.GetType().GetProperty(name).GetValue(target);
        }
    }
}
