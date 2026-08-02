using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace BugCam.Tests
{
    public sealed class StateRecorderContractTests
    {
        [Test]
        public void CoreAssemblyExposesBlock12StateRecordingContract()
        {
            var recorderType = Type.GetType("BugCam.Core.StateRecorder, BugCam.Core");
            var runResultType = Type.GetType("BugCam.Core.RunResult, BugCam.Core");
            var replayerType = Type.GetType("BugCam.Core.KinematicReplayer, BugCam.Core");
            var replayResultType = Type.GetType("BugCam.Core.KinematicReplayResult, BugCam.Core");

            Assert.That(recorderType, Is.Not.Null, "Block 1.2 requires StateRecorder.");
            Assert.That(runResultType, Is.Not.Null, "Block 1.2 requires RunResult.");
            Assert.That(replayerType, Is.Not.Null, "Block 1.2 requires KinematicReplayer.");
            Assert.That(replayResultType, Is.Not.Null, "Block 1.2 requires KinematicReplayResult.");
        }

        [Test]
        public void StateRecorderAllocatesRunsStepsBodiesTimesFourteen()
        {
            var recorderType = Type.GetType("BugCam.Core.StateRecorder, BugCam.Core");
            var allocate = recorderType.GetMethod(
                "Allocate",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(int), typeof(int), typeof(int) },
                null);
            Assert.That(allocate, Is.Not.Null);

            var recorder = allocate.Invoke(null, new object[] { 2, 3, 4 });
            var buffer = recorderType.GetProperty("Buffer")?.GetValue(recorder) as float[];
            Assert.That(buffer, Has.Length.EqualTo(2 * 3 * 4 * 14));
            Assert.That(recorderType.GetProperty("RunCount")?.GetValue(recorder), Is.EqualTo(2));
            Assert.That(recorderType.GetProperty("StepCount")?.GetValue(recorder), Is.EqualTo(3));
            Assert.That(recorderType.GetProperty("BodyCount")?.GetValue(recorder), Is.EqualTo(4));
        }

        [Test]
        public void StateRecorderWriteBodyUsesCanonicalFourteenFloatStride()
        {
            var recorderType = Type.GetType("BugCam.Core.StateRecorder, BugCam.Core");
            var allocate = recorderType.GetMethod(
                "Allocate",
                BindingFlags.Public | BindingFlags.Static);
            var writeBody = recorderType.GetMethod(
                "WriteBody",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(writeBody, Is.Not.Null);

            var recorder = allocate.Invoke(null, new object[] { 2, 5, 3 });
            writeBody.Invoke(
                recorder,
                new object[]
                {
                    1,
                    2,
                    1,
                    new Vector3(1.5f, 2.5f, 3.5f),
                    new Quaternion(0.1f, 0.2f, 0.3f, 0.4f),
                    new Vector3(4f, 5f, 6f),
                    new Vector3(7f, 8f, 9f),
                    true
                });

            var buffer = recorderType.GetProperty("Buffer")?.GetValue(recorder) as float[];
            var indexOf = recorderType.GetMethod(
                "IndexOf",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(int), typeof(int), typeof(int) },
                null);
            var offset = (int)indexOf.Invoke(recorder, new object[] { 1, 2, 1 });
            Assert.That(offset, Is.EqualTo((((1 * 5) + 2) * 3 + 1) * 14));
            Assert.That(buffer[offset], Is.EqualTo(1.5f));
            Assert.That(buffer[offset + 1], Is.EqualTo(2.5f));
            Assert.That(buffer[offset + 2], Is.EqualTo(3.5f));
            var normalized = new Quaternion(0.1f, 0.2f, 0.3f, 0.4f).normalized;
            Assert.That(buffer[offset + 3], Is.EqualTo(normalized.x));
            Assert.That(buffer[offset + 4], Is.EqualTo(normalized.y));
            Assert.That(buffer[offset + 5], Is.EqualTo(normalized.z));
            Assert.That(buffer[offset + 6], Is.EqualTo(normalized.w));
            Assert.That(buffer[offset + 7], Is.EqualTo(4f));
            Assert.That(buffer[offset + 8], Is.EqualTo(5f));
            Assert.That(buffer[offset + 9], Is.EqualTo(6f));
            Assert.That(buffer[offset + 10], Is.EqualTo(7f));
            Assert.That(buffer[offset + 11], Is.EqualTo(8f));
            Assert.That(buffer[offset + 12], Is.EqualTo(9f));
            Assert.That(buffer[offset + 13], Is.EqualTo(1f));
        }

        [Test]
        public void RunResultRecordsMetadataAndSimulatedTime()
        {
            var runResultType = Type.GetType("BugCam.Core.RunResult, BugCam.Core");
            var perturbationType = Type.GetType("BugCam.Core.SimulationPerturbation, BugCam.Core");
            var success = runResultType.GetMethod(
                "Success",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(success, Is.Not.Null);

            var perturbation = Activator.CreateInstance(
                perturbationType,
                7,
                Vector3.right,
                0.001f);
            var frames = new float[10 * 1 * 14];
            frames[0] = 1f;
            var result = success.Invoke(
                null,
                new object[]
                {
                    frames,
                    new[] { 7 },
                    0.001f,
                    perturbation,
                    10,
                    0,
                    42L
                });

            Assert.That(runResultType.GetProperty("Succeeded")?.GetValue(result), Is.EqualTo(true));
            Assert.That(
                runResultType.GetProperty("EpsilonMetres")?.GetValue(result),
                Is.EqualTo(0.001f));
            Assert.That(runResultType.GetProperty("StepCount")?.GetValue(result), Is.EqualTo(10));
            Assert.That(
                runResultType.GetProperty("SimulatedTime")?.GetValue(result),
                Is.EqualTo(0.2f).Within(1e-6f));
            Assert.That(runResultType.GetProperty("Seed")?.GetValue(result), Is.EqualTo(0));
            Assert.That(runResultType.GetProperty("WallClockMs")?.GetValue(result), Is.EqualTo(42L));
            Assert.That(
                perturbationType.GetProperty("TargetBodyId")?.GetValue(
                    runResultType.GetProperty("Perturbation")?.GetValue(result)),
                Is.EqualTo(7));
        }

        [Test]
        public void KinematicReplayerFailsDeterministicallyOutsidePlayMode()
        {
            Assert.That(Application.isPlaying, Is.False);

            var replayerType = Type.GetType("BugCam.Core.KinematicReplayer, BugCam.Core");
            var resultType = Type.GetType("BugCam.Core.KinematicReplayResult, BugCam.Core");
            var replay = replayerType.GetMethod(
                "ReplayTransforms",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(float[]), typeof(int), typeof(int) },
                null);
            Assert.That(replay, Is.Not.Null);

            var frames = new float[2 * 1 * 14];
            var result = replay.Invoke(null, new object[] { frames, 2, 1 });
            Assert.That(resultType.GetProperty("Succeeded")?.GetValue(result), Is.EqualTo(false));
            Assert.That(
                resultType.GetProperty("ErrorReason")?.GetValue(result) as string,
                Does.Contain("requires Play Mode"));
        }
    }
}
