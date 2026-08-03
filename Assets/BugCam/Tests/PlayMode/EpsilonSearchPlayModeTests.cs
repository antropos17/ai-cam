using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BugCam.Tests
{
    /// <summary>
    /// Play Mode integration for Block 1.4 adaptive epsilon search.
    /// Uses reflection so PlayMode tests keep zero asmdef references to BugCam.Core.
    /// </summary>
    public sealed class EpsilonSearchPlayModeTests
    {
        private const int FastStepCount = 40;

        // Measured on TowerScene (batchmode 6000.3.21f1): DefaultStepCount 250 yields
        // DIVERGENT AT SEARCH FLOOR (not STABLE). AscendFromStart X sweep:
        // 31=STABLE, 32–34=THRESHOLD BRACKET FOUND, ≥35=DIVERGENT AT SEARCH FLOOR.
        // 32 is the smallest proven bracket-producing step count (evidence:
        // Library/BugCamEvidence/Block1.4-verify-fix/step-sweep*.txt).
        private const int VerifyStepCount = 32;

        [UnityTest]
        public IEnumerator RunnerExecutesHarnessWithSceneCleanupBetweenProbes()
        {
            Assert.That(Physics.simulationMode, Is.EqualTo(SimulationMode.Script));

            var initialSceneCount = SceneManager.sceneCount;
            var maxSceneCount = initialSceneCount;
            object completed = null;

            yield return RunSearch(
                Vector3.right,
                StrategyAscendFromStart(),
                0f,
                FastStepCount,
                r => completed = r,
                count => maxSceneCount = Math.Max(maxSceneCount, count));

            Assert.That(completed, Is.Not.Null);
            Assert.That(Prop<bool>(completed, "Succeeded"), Is.True, Prop<string>(completed, "ErrorReason"));
            Assert.That(
                SceneManager.sceneCount,
                Is.EqualTo(initialSceneCount),
                "All temporary PhysicsScenes must unload after the search.");
        }

        [UnityTest]
        public IEnumerator RepeatedBaselineIsDeterministicWithinGate()
        {
            Assert.That(Physics.simulationMode, Is.EqualTo(SimulationMode.Script));

            var factoryType = Type.GetType("BugCam.Core.TowerProbeRequestFactory, BugCam.Core");
            var harnessType = Type.GetType("BugCam.Core.SimulationHarness, BugCam.Core");
            var requestType = Type.GetType("BugCam.Core.SimulationRequest, BugCam.Core");
            Assert.That(factoryType, Is.Not.Null);

            var createBaseline = factoryType.GetMethod("CreateBaseline", new[] { typeof(int) });
            var request = createBaseline.Invoke(null, new object[] { FastStepCount });
            var harness = Activator.CreateInstance(harnessType);
            var run = harnessType.GetMethod("Run", new[] { requestType });

            var initialSceneCount = SceneManager.sceneCount;
            var a = run.Invoke(harness, new[] { request });
            yield return WaitCleanup(initialSceneCount);
            var aPrime = run.Invoke(harness, new[] { request });
            yield return WaitCleanup(initialSceneCount);

            Assert.That(Prop<bool>(a, "Succeeded"), Is.True);
            Assert.That(Prop<bool>(aPrime, "Succeeded"), Is.True);

            var framesA = (float[])Prop<object>(a, "StateFrames");
            var framesB = (float[])Prop<object>(aPrime, "StateFrames");
            Assert.That(framesA.Length, Is.EqualTo(framesB.Length));
            var maxDelta = 0f;
            for (var i = 0; i < framesA.Length; i++)
            {
                var delta = Math.Abs(framesA[i] - framesB[i]);
                if (delta > maxDelta)
                {
                    maxDelta = delta;
                }
            }

            Assert.That(maxDelta, Is.LessThanOrEqualTo(1e-6f), "Repeated baseline must stay within gate.");
            Assert.That(SceneManager.sceneCount, Is.EqualTo(initialSceneCount));
        }

        [UnityTest]
        public IEnumerator SameAxisConvergenceFromThreeStartingStrategies()
        {
            Assert.That(Physics.simulationMode, Is.EqualTo(SimulationMode.Script));

            object fromStart = null;
            object fromCustom = null;
            object fromCeiling = null;

            yield return RunSearch(
                Vector3.right,
                StrategyAscendFromStart(),
                0f,
                VerifyStepCount,
                r => fromStart = r);

            yield return RunSearch(
                Vector3.right,
                StrategyAscendFromCustom(),
                2e-5f,
                VerifyStepCount,
                r => fromCustom = r);

            yield return RunSearch(
                Vector3.right,
                StrategyDescendFromCeiling(),
                0f,
                VerifyStepCount,
                r => fromCeiling = r);

            Assert.That(Prop<bool>(fromStart, "Succeeded"), Is.True, Prop<string>(fromStart, "ErrorReason"));
            Assert.That(Prop<bool>(fromCustom, "Succeeded"), Is.True, Prop<string>(fromCustom, "ErrorReason"));
            Assert.That(Prop<bool>(fromCeiling, "Succeeded"), Is.True, Prop<string>(fromCeiling, "ErrorReason"));

            Assert.That(
                Prop<string>(fromStart, "Verdict"),
                Is.EqualTo("THRESHOLD BRACKET FOUND"),
                "Unexpected STABLE or non-bracket verdict for from-start: " + Prop<string>(fromStart, "Verdict"));
            Assert.That(
                Prop<string>(fromCustom, "Verdict"),
                Is.EqualTo("THRESHOLD BRACKET FOUND"),
                "Unexpected STABLE or non-bracket verdict for from-custom: " + Prop<string>(fromCustom, "Verdict"));
            Assert.That(
                Prop<string>(fromCeiling, "Verdict"),
                Is.EqualTo("THRESHOLD BRACKET FOUND"),
                "Unexpected STABLE or non-bracket verdict for from-ceiling: " + Prop<string>(fromCeiling, "Verdict"));

            Assert.That(Prop<bool>(fromStart, "HasThresholdEstimate"), Is.True);
            Assert.That(Prop<bool>(fromCustom, "HasThresholdEstimate"), Is.True);
            Assert.That(Prop<bool>(fromCeiling, "HasThresholdEstimate"), Is.True);

            // Directional X/Y/Z thresholds are not required to match; same-axis strategies must.
            var a = Prop<float>(fromStart, "ThresholdEstimateMetres");
            var b = Prop<float>(fromCustom, "ThresholdEstimateMetres");
            var c = Prop<float>(fromCeiling, "ThresholdEstimateMetres");
            Assert.That(Ratio(a, b), Is.LessThanOrEqualTo(2.0001f));
            Assert.That(Ratio(a, c), Is.LessThanOrEqualTo(2.0001f));
            Assert.That(Ratio(b, c), Is.LessThanOrEqualTo(2.0001f));
        }

        [UnityTest]
        public IEnumerator CharacterizationSucceedsOnXyzAxesWithRetainedFan()
        {
            Assert.That(Physics.simulationMode, Is.EqualTo(SimulationMode.Script));

            object x = null;
            object y = null;
            object z = null;

            yield return RunSearch(Vector3.right, StrategyAscendFromStart(), 0f, VerifyStepCount, r => x = r);
            yield return RunSearch(Vector3.up, StrategyAscendFromStart(), 0f, VerifyStepCount, r => y = r);
            yield return RunSearch(Vector3.forward, StrategyAscendFromStart(), 0f, VerifyStepCount, r => z = r);

            foreach (var result in new[] { x, y, z })
            {
                Assert.That(Prop<bool>(result, "Succeeded"), Is.True, Prop<string>(result, "ErrorReason"));
                var verdict = Prop<string>(result, "Verdict");
                Assert.That(
                    verdict,
                    Is.EqualTo("THRESHOLD BRACKET FOUND"),
                    "Unexpected STABLE or non-bracket verdict: " + verdict);
                Assert.That(Prop<bool>(result, "HasThresholdEstimate"), Is.True);
                Assert.That(Arr(result, "FanRuns").Length, Is.EqualTo(15));
                Assert.That(Arr(result, "FanSummaries").Length, Is.EqualTo(15));
                Assert.That(Prop<bool>(Prop<object>(result, "BaselineRun"), "Succeeded"), Is.True);
            }
        }

        [UnityTest]
        public IEnumerator BaselinePlusExactlyFifteenRetainedFanRunsWhenThresholdFound()
        {
            Assert.That(Physics.simulationMode, Is.EqualTo(SimulationMode.Script));

            object completed = null;
            yield return RunSearch(
                Vector3.right,
                StrategyAscendFromStart(),
                0f,
                VerifyStepCount,
                r => completed = r);

            Assert.That(Prop<bool>(completed, "Succeeded"), Is.True, Prop<string>(completed, "ErrorReason"));
            Assert.That(Prop<bool>(Prop<object>(completed, "BaselineRun"), "Succeeded"), Is.True);

            var verdict = Prop<string>(completed, "Verdict");
            Assert.That(
                verdict,
                Is.EqualTo("THRESHOLD BRACKET FOUND"),
                "Unexpected STABLE or non-bracket verdict: " + verdict);
            Assert.That(Prop<bool>(completed, "HasThresholdEstimate"), Is.True);
            Assert.That(Arr(completed, "FanRuns").Length, Is.EqualTo(15));
            Assert.That(Arr(completed, "FanSummaries").Length, Is.EqualTo(15));
            Assert.That(Arr(completed, "LadderSummaries").Length, Is.EqualTo(12));
        }

        [UnityTest]
        public IEnumerator RunnerFailsAndDoesNotLaunchNextProbeWhenCleanupTimesOut()
        {
            Assert.That(Physics.simulationMode, Is.EqualTo(SimulationMode.Script));

            LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex(
                    "BUGCAM_BLOCK_1_4_EPSILON_SEARCH[\\s\\S]*succeeded=False[\\s\\S]*Temporary scene cleanup timed out"));

            var initialSceneCount = SceneManager.sceneCount;
            object completed = null;

            yield return RunSearch(
                Vector3.right,
                StrategyAscendFromStart(),
                0f,
                FastStepCount,
                r => completed = r,
                alwaysFailCleanup: true);

            Assert.That(completed, Is.Not.Null);
            Assert.That(Prop<bool>(completed, "Succeeded"), Is.False);
            Assert.That(Prop<string>(completed, "Verdict"), Is.EqualTo("FAILED"));
            Assert.That(
                Prop<string>(completed, "ErrorReason"),
                Does.Contain("Temporary scene cleanup timed out"));
            Assert.That(
                Prop<string>(completed, "ErrorReason"),
                Does.Contain("no further epsilon probes were run"));
            Assert.That(
                Prop<int>(completed, "PhysicalProbeCount"),
                Is.EqualTo(0),
                "Timeout must fail before any successful probe is retained; no later probe may run.");
            Assert.That(Arr(completed, "LadderSummaries"), Is.Empty);
            Assert.That(Arr(completed, "FanSummaries"), Is.Empty);

            // Harness still requested unload; allow it to settle.
            yield return WaitCleanup(initialSceneCount);
            Assert.That(SceneManager.sceneCount, Is.EqualTo(initialSceneCount));
        }

        [UnityTest]
        public IEnumerator RunnerLogsFormattedCleanupTimeoutFailure()
        {
            Assert.That(Physics.simulationMode, Is.EqualTo(SimulationMode.Script));

            LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex(
                    "BUGCAM_BLOCK_1_4_EPSILON_SEARCH[\\s\\S]*succeeded=False[\\s\\S]*Temporary scene cleanup timed out"));

            var initialSceneCount = SceneManager.sceneCount;
            object completed = null;
            yield return RunSearch(
                Vector3.right,
                StrategyAscendFromStart(),
                0f,
                FastStepCount,
                r => completed = r,
                alwaysFailCleanup: true);

            Assert.That(Prop<bool>(completed, "Succeeded"), Is.False);
            yield return WaitCleanup(initialSceneCount);
            Assert.That(SceneManager.sceneCount, Is.EqualTo(initialSceneCount));
        }

        [UnityTest]
        public IEnumerator CleanupWaitSucceedsImmediatelyWhenPredicateTrueAtZeroFrameLimit()
        {
            var timedOut = true;
            var runnerType = Type.GetType("BugCam.Core.EpsilonSearchRunner, BugCam.Core");
            var wait = runnerType.GetMethod(
                "WaitForSceneCleanup",
                new[]
                {
                    typeof(int),
                    typeof(Func<int, bool>),
                    typeof(int),
                    typeof(Action<bool>)
                });
            Assert.That(wait, Is.Not.Null);

            Func<int, bool> alwaysClean = _ => true;
            Action<bool> onCompleted = value => timedOut = value;
            var enumerator = (IEnumerator)wait.Invoke(
                null,
                new object[] { SceneManager.sceneCount, alwaysClean, 0, onCompleted });
            while (enumerator.MoveNext())
            {
                yield return enumerator.Current;
            }

            Assert.That(timedOut, Is.False);
        }

        private static IEnumerator RunSearch(
            Vector3 axis,
            object strategy,
            float customStart,
            int stepCount,
            Action<object> onCompleted,
            Action<int> onProbeSceneCount = null,
            bool alwaysFailCleanup = false)
        {
            var searchType = Type.GetType("BugCam.Core.EpsilonSearch, BugCam.Core");
            var settingsType = Type.GetType("BugCam.Core.EpsilonSearchSettings, BugCam.Core");
            var runnerType = Type.GetType("BugCam.Core.EpsilonSearchRunner, BugCam.Core");
            var factoryType = Type.GetType("BugCam.Core.TowerProbeRequestFactory, BugCam.Core");
            var thresholdsType = Type.GetType("BugCam.Core.DivergenceThresholds, BugCam.Core");

            Assert.That(searchType, Is.Not.Null);
            Assert.That(runnerType, Is.Not.Null);

            var settings = settingsType.GetProperty("Default").GetValue(null);
            var search = Activator.CreateInstance(
                searchType,
                settings,
                49,
                axis,
                strategy,
                customStart);

            var baselineRequest = factoryType.GetMethod("CreateBaseline", new[] { typeof(int) })
                .Invoke(null, new object[] { stepCount });
            var bodies = Prop<object>(baselineRequest, "Bodies");
            var thresholds = thresholdsType.GetProperty("Default").GetValue(null);

            var bodyCount = ((Array)bodies).Length;
            var scales = new float[bodyCount];
            for (var i = 0; i < bodyCount; i++)
            {
                scales[i] = 1f;
            }

            object runner;
            if (alwaysFailCleanup)
            {
                Func<int, bool> neverClean = _ => false;
                runner = Activator.CreateInstance(
                    runnerType,
                    neverClean,
                    0);
            }
            else
            {
                runner = Activator.CreateInstance(runnerType);
            }

            var runMethod = runnerType.GetMethod(
                "Run",
                new[]
                {
                    searchType,
                    bodies.GetType(),
                    typeof(int),
                    thresholdsType,
                    typeof(float[])
                });
            Assert.That(runMethod, Is.Not.Null, "EpsilonSearchRunner.Run signature mismatch.");

            var enumerator = (IEnumerator)runMethod.Invoke(
                runner,
                new[]
                {
                    search,
                    bodies,
                    (object)stepCount,
                    thresholds,
                    scales
                });

            while (enumerator.MoveNext())
            {
                onProbeSceneCount?.Invoke(SceneManager.sceneCount);
                yield return enumerator.Current;
            }

            var last = runnerType.GetProperty("LastResult").GetValue(runner);
            onCompleted(last);
        }

        private static IEnumerator WaitCleanup(int sceneCountBefore)
        {
            for (var frame = 0; frame < 120; frame++)
            {
                if (SceneManager.sceneCount <= sceneCountBefore)
                {
                    yield break;
                }

                yield return null;
            }
        }

        private static object StrategyAscendFromStart()
        {
            var type = Type.GetType("BugCam.Core.EpsilonSearchStrategy, BugCam.Core");
            return Enum.ToObject(type, 0);
        }

        private static object StrategyAscendFromCustom()
        {
            var type = Type.GetType("BugCam.Core.EpsilonSearchStrategy, BugCam.Core");
            return Enum.ToObject(type, 1);
        }

        private static object StrategyDescendFromCeiling()
        {
            var type = Type.GetType("BugCam.Core.EpsilonSearchStrategy, BugCam.Core");
            return Enum.ToObject(type, 2);
        }

        private static float Ratio(float a, float b)
        {
            var lo = Math.Min(a, b);
            var hi = Math.Max(a, b);
            if (lo <= 0f)
            {
                return float.PositiveInfinity;
            }

            return hi / lo;
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
