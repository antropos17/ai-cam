using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BugCam.Tests
{
    public sealed class StateRecorderPlayModeTests
    {
        [UnityTest]
        public IEnumerator KinematicReplayReproducesRecordedTransformsWithZeroDelta()
        {
            Assert.That(
                Physics.simulationMode,
                Is.EqualTo(SimulationMode.Script),
                "SimulationHarness.Run requires Physics.simulationMode to be Script.");

            var bodyType = Type.GetType("BugCam.Core.SimulationBodyDefinition, BugCam.Core");
            var perturbationType = Type.GetType("BugCam.Core.SimulationPerturbation, BugCam.Core");
            var requestType = Type.GetType("BugCam.Core.SimulationRequest, BugCam.Core");
            var harnessResultType = Type.GetType("BugCam.Core.SimulationRunResult, BugCam.Core");
            var harnessType = Type.GetType("BugCam.Core.SimulationHarness, BugCam.Core");
            var runResultType = Type.GetType("BugCam.Core.RunResult, BugCam.Core");
            var replayerType = Type.GetType("BugCam.Core.KinematicReplayer, BugCam.Core");
            var replayResultType = Type.GetType("BugCam.Core.KinematicReplayResult, BugCam.Core");

            Assert.That(bodyType, Is.Not.Null);
            Assert.That(runResultType, Is.Not.Null);
            Assert.That(replayerType, Is.Not.Null);

            var bodyConstructor = bodyType.GetConstructor(new[]
            {
                typeof(int),
                typeof(Vector3),
                typeof(Quaternion),
                typeof(Vector3),
                typeof(float)
            });
            // Identity rotation keeps transform round-trip bit-stable; arbitrary Euler
            // quaternions can pick up ~1 ULP when Unity renormalizes on readback.
            var body = bodyConstructor.Invoke(new object[]
            {
                7,
                new Vector3(0.25f, 3f, -0.5f),
                Quaternion.identity,
                Vector3.one,
                1f
            });
            var bodies = Array.CreateInstance(bodyType, 1);
            bodies.SetValue(body, 0);

            var requestConstructor = requestType.GetConstructor(new[]
            {
                bodyType.MakeArrayType(),
                typeof(int),
                perturbationType
            });
            var request = requestConstructor.Invoke(new[]
            {
                bodies,
                (object)12,
                Activator.CreateInstance(perturbationType)
            });

            var initialSceneCount = SceneManager.sceneCount;
            var harnessResult = harnessType.GetMethod("Run", new[] { requestType }).Invoke(
                Activator.CreateInstance(harnessType),
                new[] { request });

            Assert.That(
                harnessResultType.GetProperty("Succeeded")?.GetValue(harnessResult),
                Is.EqualTo(true),
                "Recording run must succeed: " +
                harnessResultType.GetProperty("ErrorReason")?.GetValue(harnessResult));

            var fromHarness = runResultType.GetMethod(
                "FromSimulationRunResult",
                new[] { harnessResultType, typeof(int), typeof(long) });
            Assert.That(
                fromHarness,
                Is.Not.Null,
                "RunResult must wrap SimulationRunResult with Block 1.2 metadata.");

            var runResult = fromHarness.Invoke(null, new[] { harnessResult, (object)0, 0L });
            Assert.That(runResultType.GetProperty("Succeeded")?.GetValue(runResult), Is.EqualTo(true));
            Assert.That(runResultType.GetProperty("StepCount")?.GetValue(runResult), Is.EqualTo(12));
            Assert.That(
                runResultType.GetProperty("SimulatedTime")?.GetValue(runResult),
                Is.EqualTo(0.24f).Within(1e-6f));
            Assert.That(runResultType.GetProperty("Seed")?.GetValue(runResult), Is.EqualTo(0));
            Assert.That(
                runResultType.GetProperty("EpsilonMetres")?.GetValue(runResult),
                Is.EqualTo(0f));

            var replayMethod = replayerType.GetMethod(
                "ReplayTransforms",
                new[] { runResultType });
            Assert.That(replayMethod, Is.Not.Null);

            var replayResult = replayMethod.Invoke(null, new[] { runResult });
            Assert.That(
                replayResultType.GetProperty("Succeeded")?.GetValue(replayResult),
                Is.EqualTo(true),
                "Kinematic replay must succeed: " +
                replayResultType.GetProperty("ErrorReason")?.GetValue(replayResult));
            Assert.That(
                replayResultType.GetProperty("MaxComponentDelta")?.GetValue(replayResult),
                Is.EqualTo(0f),
                "Block 1.2 VERIFY: kinematic replay must reproduce transforms with maxComponentDelta == 0.");
            Assert.That(
                replayResultType.GetProperty("TemporarySceneUnloadRequested")?.GetValue(replayResult),
                Is.EqualTo(true));

            for (var frame = 0; frame < 20 && SceneManager.sceneCount != initialSceneCount; frame++)
            {
                yield return null;
            }

            Assert.That(
                SceneManager.sceneCount,
                Is.EqualTo(initialSceneCount),
                "Recording and kinematic replay must unload temporary local physics scenes.");
        }

        [UnityTest]
        public IEnumerator StateRecorderCapturesMultiRunBufferWithoutCrossRunOverwrite()
        {
            var recorderType = Type.GetType("BugCam.Core.StateRecorder, BugCam.Core");
            var allocate = recorderType.GetMethod(
                "Allocate",
                new[] { typeof(int), typeof(int), typeof(int) });
            var writeBody = recorderType.GetMethod("WriteBody");
            var createRunCopy = recorderType.GetMethod("CreateRunCopy", new[] { typeof(int) });

            var recorder = allocate.Invoke(null, new object[] { 2, 2, 1 });
            writeBody.Invoke(
                recorder,
                new object[]
                {
                    0,
                    0,
                    0,
                    new Vector3(1f, 0f, 0f),
                    Quaternion.identity,
                    Vector3.zero,
                    Vector3.zero,
                    false
                });
            writeBody.Invoke(
                recorder,
                new object[]
                {
                    1,
                    0,
                    0,
                    new Vector3(2f, 0f, 0f),
                    Quaternion.identity,
                    Vector3.zero,
                    Vector3.zero,
                    false
                });

            var run0 = createRunCopy.Invoke(recorder, new object[] { 0 }) as float[];
            var run1 = createRunCopy.Invoke(recorder, new object[] { 1 }) as float[];
            Assert.That(run0[0], Is.EqualTo(1f));
            Assert.That(run1[0], Is.EqualTo(2f));
            Assert.That(run0, Has.Length.EqualTo(2 * 1 * 14));
            Assert.That(run1, Has.Length.EqualTo(2 * 1 * 14));

            yield return null;
        }
    }
}
