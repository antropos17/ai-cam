using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BugCam.Tests
{
    /// <summary>
    /// Block 2.2.1 A5 — the bit-identical identical-config search-repeat pin that closes
    /// PLAN.md Block 1.4 VERIFY (a). Two complete default searches over the procedural
    /// tower with identical configuration must agree bit-for-bit: same verdict, same
    /// threshold/bracket floats (compared by raw bits), and byte-identical retained state
    /// frames for the baseline and every retained fan run. This is a determinism
    /// regression test, not a physics-claim gate (CLAUDE.md bitwise clause exception).
    /// </summary>
    public sealed class SearchRepeatPlayModeTests
    {
        private const int VerifyStepCount = 32;

        private static Type CoreType(string name)
        {
            var type = Type.GetType("BugCam.Core." + name + ", BugCam.Core");
            Assert.That(type, Is.Not.Null, "BugCam.Core." + name + " must exist.");
            return type;
        }

        private static T Prop<T>(object target, string name)
        {
            var property = target.GetType().GetProperty(
                name,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, target.GetType().Name + "." + name);
            return (T)property.GetValue(target);
        }

        private static object Prop(object target, string name)
        {
            return Prop<object>(target, name);
        }

        [UnityTest]
        public IEnumerator IdenticalConfigFullSearchRepeatsBitIdentically()
        {
            var initialSceneCount = SceneManager.sceneCount;

            object first = null;
            object second = null;
            yield return RunFullDefaultSearch(result => first = result);
            yield return RunFullDefaultSearch(result => second = result);
            yield return WaitCleanup(initialSceneCount);

            Assert.That(Prop<bool>(first, "Succeeded"), Is.True,
                Prop<string>(first, "ErrorReason"));
            Assert.That(Prop<bool>(second, "Succeeded"), Is.True,
                Prop<string>(second, "ErrorReason"));

            Assert.That(Prop<string>(second, "Verdict"),
                Is.EqualTo(Prop<string>(first, "Verdict")));

            AssertBitEqual(first, second, "ThresholdEstimateMetres");
            AssertBitEqual(first, second, "LargestStableEpsilonMetres");
            AssertBitEqual(first, second, "SmallestDivergentEpsilonMetres");
            AssertBitEqual(first, second, "FinalBracketWidthMetres");
            AssertBitEqual(first, second, "ReferenceEpsilonMetres");

            AssertRunBitIdentical(
                Prop(first, "BaselineRun"),
                Prop(second, "BaselineRun"),
                "baseline");

            var firstFans = Prop<Array>(first, "FanRuns");
            var secondFans = Prop<Array>(second, "FanRuns");
            Assert.That(secondFans.Length, Is.EqualTo(firstFans.Length),
                "Retained fan counts must match between repeats.");
            for (var i = 0; i < firstFans.Length; i++)
            {
                AssertRunBitIdentical(
                    firstFans.GetValue(i),
                    secondFans.GetValue(i),
                    "fan[" + i + "]");
            }
        }

        private static void AssertBitEqual(object first, object second, string property)
        {
            var a = Prop<float>(first, property);
            var b = Prop<float>(second, property);
            Assert.That(
                BitConverter.ToInt32(BitConverter.GetBytes(b), 0),
                Is.EqualTo(BitConverter.ToInt32(BitConverter.GetBytes(a), 0)),
                property + " must be bit-identical between identical-config repeats " +
                "(first=" + a.ToString("R") + ", second=" + b.ToString("R") + ").");
        }

        private static void AssertRunBitIdentical(object firstRun, object secondRun, string label)
        {
            AssertBitEqual(firstRun, secondRun, "EpsilonMetres");

            var firstFrames = Prop<float[]>(firstRun, "StateFrames");
            var secondFrames = Prop<float[]>(secondRun, "StateFrames");
            Assert.That(secondFrames.Length, Is.EqualTo(firstFrames.Length),
                label + " state frame lengths must match.");
            for (var i = 0; i < firstFrames.Length; i++)
            {
                if (BitConverter.ToInt32(BitConverter.GetBytes(firstFrames[i]), 0) !=
                    BitConverter.ToInt32(BitConverter.GetBytes(secondFrames[i]), 0))
                {
                    Assert.Fail(
                        label + " state frames diverge at flat index " + i +
                        ": first=" + firstFrames[i].ToString("R") +
                        " second=" + secondFrames[i].ToString("R") +
                        " — identical-config repeat must be bit-identical.");
                }
            }
        }

        private static IEnumerator RunFullDefaultSearch(Action<object> onCompleted)
        {
            var searchType = CoreType("EpsilonSearch");
            var settingsType = CoreType("EpsilonSearchSettings");
            var runnerType = CoreType("EpsilonSearchRunner");
            var factoryType = CoreType("TowerProbeRequestFactory");
            var thresholdsType = CoreType("DivergenceThresholds");
            var strategyType = CoreType("EpsilonSearchStrategy");

            var settings = settingsType.GetProperty("Default").GetValue(null);
            var search = Activator.CreateInstance(
                searchType,
                settings,
                49,
                Vector3.right,
                Enum.ToObject(strategyType, 0),
                0f);

            var baselineRequest = factoryType.GetMethod("CreateBaseline", new[] { typeof(int) })
                .Invoke(null, new object[] { VerifyStepCount });
            var bodies = Prop(baselineRequest, "Bodies");
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
                new object[] { search, bodies, VerifyStepCount, thresholds, scales });
            while (enumerator.MoveNext())
            {
                yield return enumerator.Current;
            }

            onCompleted(Prop(runner, "LastResult"));
        }

        private static IEnumerator WaitCleanup(int initialSceneCount)
        {
            var runnerType = CoreType("EpsilonSearchRunner");
            var wait = runnerType.GetMethod("WaitForSceneCleanup", new[] { typeof(int) });
            var enumerator = (IEnumerator)wait.Invoke(null, new object[] { initialSceneCount });
            while (enumerator.MoveNext())
            {
                yield return enumerator.Current;
            }
        }
    }
}
