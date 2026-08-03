using System;
using NUnit.Framework;
using UnityEngine;

namespace BugCam.Tests
{
    /// <summary>
    /// Synthetic Block 1.3 DivergenceEngine cases. No PhysicsScene / Play Mode required.
    /// Uses reflection so EditMode tests keep zero asmdef references to BugCam.Core.
    /// </summary>
    public sealed class DivergenceEngineTests
    {
        private const int Stride = 14;

        private readonly struct FrameBuffer
        {
            public FrameBuffer(float[] frames, int stepCount, int bodyCount)
            {
                Frames = frames;
                StepCount = stepCount;
                BodyCount = bodyCount;
            }

            public float[] Frames { get; }
            public int StepCount { get; }
            public int BodyCount { get; }
        }

        [Test]
        public void IdenticalTrajectoriesProduceNoDivergence()
        {
            var frames = BuildFrames(8, 1, null);
            var result = Analyze(frames, frames, 0.001f);

            Assert.That(Prop<bool>(result, "Succeeded"), Is.True);
            Assert.That(Prop<bool>(result, "HasSignificantDivergence"), Is.False);
            Assert.That(Prop<int>(result, "FirstDivergenceFrame"), Is.EqualTo(-1));
            Assert.That(Prop<int>(result, "FirstDivergenceBodyId"), Is.EqualTo(-1));
            Assert.That(Prop<float>(result, "MaxSpreadMetres"), Is.EqualTo(0f));
            Assert.That(Prop<int[]>(result, "AffectedBodyIds"), Is.Empty);
        }

        [Test]
        public void FirstDivergenceBodyIdIsArgMaxAtFirstFrameIndependentOfMaxSpread()
        {
            // Bodies 1,2,49: at first-div frame body2 leads; later body49 owns global max.
            const int steps = 12;
            const int bodies = 3;
            var ids = new[] { 1, 2, 49 };
            var baseline = BuildFrames(steps, bodies, null);
            var perturbed = BuildFrames(steps, bodies, (step, body, offset, buffer) =>
            {
                if (step < 2)
                {
                    return;
                }

                if (body == 0)
                {
                    buffer[offset + 1] = 0.5f;
                }
                else if (body == 1)
                {
                    buffer[offset + 1] = 1.2f;
                }
                else
                {
                    buffer[offset + 1] = step < 7 ? 0.4f : 5.0f;
                }
            });

            var result = Analyze(baseline, perturbed, 0.001f, null, null, ids);
            Assert.That(Prop<bool>(result, "Succeeded"), Is.True, Prop<string>(result, "ErrorReason"));
            Assert.That(Prop<bool>(result, "HasSignificantDivergence"), Is.True);
            Assert.That(Prop<int>(result, "FirstDivergenceFrame"), Is.EqualTo(2));
            Assert.That(Prop<int>(result, "FirstDivergenceBodyId"), Is.EqualTo(2));
            Assert.That(Prop<int>(result, "MaxSpreadBodyId"), Is.EqualTo(49));
            Assert.That(Prop<int[]>(result, "AffectedBodyIds")[0], Is.EqualTo(1));
            Assert.That(
                Prop<int>(result, "FirstDivergenceBodyId"),
                Is.Not.EqualTo(Prop<int>(result, "MaxSpreadBodyId")));
        }

        [Test]
        public void KnownDivergenceBeginningAtFrameNReturnsExactlyFrameN()
        {
            const int divergeAt = 3;
            var baseline = BuildFrames(10, 1, null);
            var perturbed = BuildFrames(10, 1, (step, body, offset, buffer) =>
            {
                if (step >= divergeAt)
                {
                    // 1.1 m on a 1 m scale ⇒ scene score 1.1 > default SceneScoreThreshold 1.
                    buffer[offset] = 1.1f;
                }
            });

            var result = Analyze(baseline, perturbed, 0.001f);
            Assert.That(
                Prop<bool>(result, "Succeeded"),
                Is.True,
                Prop<string>(result, "ErrorReason"));
            Assert.That(Prop<bool>(result, "HasSignificantDivergence"), Is.True);
            Assert.That(
                Prop<int>(result, "FirstDivergenceFrame"),
                Is.EqualTo(divergeAt),
                "firstDivergenceFrame must be the first frame of the sustained window.");
        }

        [Test]
        public void OneFrameSpikeDoesNotPassSustainedGate()
        {
            var baseline = BuildFrames(10, 1, null);
            var perturbed = BuildFrames(10, 1, (step, body, offset, buffer) =>
            {
                if (step == 4)
                {
                    buffer[offset] = 1.1f;
                }
            });

            var result = Analyze(baseline, perturbed, 0.001f);
            Assert.That(Prop<bool>(result, "HasSignificantDivergence"), Is.False);
            Assert.That(Prop<int>(result, "FirstDivergenceFrame"), Is.EqualTo(-1));
        }

        [Test]
        public void RunShorterThanSustainedStepsProducesNoDivergence()
        {
            var thresholds = DefaultThresholds();
            var sustained = (int)Prop<object>(thresholds, "SustainedSteps");
            Assert.That(sustained, Is.EqualTo(5));

            var baseline = BuildFrames(sustained - 1, 1, null);
            var perturbed = BuildFrames(
                sustained - 1,
                1,
                (step, body, offset, buffer) => { buffer[offset] = 1.1f; });

            var result = Analyze(baseline, perturbed, 0.001f, thresholds);
            Assert.That(Prop<bool>(result, "HasSignificantDivergence"), Is.False);
        }

        [Test]
        public void PureSubThresholdNoiseProducesNoDivergence()
        {
            var thresholds = DefaultThresholds();
            var positionGate = (float)Prop<object>(thresholds, "PerBodyPositionThreshold");
            var noise = positionGate * 0.5f;
            var baseline = BuildFrames(10, 1, null);
            var perturbed = BuildFrames(10, 1, (step, body, offset, buffer) =>
            {
                buffer[offset] = noise;
            });

            var result = Analyze(baseline, perturbed, 0.001f, thresholds);
            Assert.That(Prop<bool>(result, "HasSignificantDivergence"), Is.False);
            Assert.That(Prop<float>(result, "MaxSpreadMetres"), Is.EqualTo(noise).Within(1e-6f));
        }

        [Test]
        public void SceneScoreWithoutPositionCrossingIsNotSignificant()
        {
            var thresholds = MakeThresholds(
                perBodyPositionThreshold: 1e-3f,
                perBodyRotationThreshold: 1f,
                perBodyVelocityThreshold: 0.05f,
                sceneScoreThreshold: 0.01f,
                sustainedSteps: 5,
                weightPosition: 0f,
                weightRotation: 1f,
                weightVelocity: 0f,
                weightSleep: 0f);

            var baseline = BuildFrames(10, 1, null);
            var q = Quaternion.Euler(0f, 90f, 0f);
            var perturbed = BuildFrames(10, 1, (step, body, offset, buffer) =>
            {
                WriteRotationAt(buffer, offset, q);
            });

            var result = Analyze(baseline, perturbed, 0.001f, thresholds);
            var scores = Prop<float[]>(result, "SceneScorePerStep");
            Assert.That(scores[0], Is.GreaterThan(0.01f));
            Assert.That(Prop<bool>(result, "HasSignificantDivergence"), Is.False);
            Assert.That(Prop<int[]>(result, "AffectedBodyIds"), Is.Empty);
        }

        [Test]
        public void PositionCrossingWithoutSceneScoreCrossingIsNotSignificant()
        {
            // Position clearly above PerBodyPositionThreshold for SustainedSteps,
            // while scene score stays at/below SceneScoreThreshold (WeightPosition=0).
            // Fails if production drops the `sceneScore > SceneScoreThreshold` AND half.
            const float positionGate = 0.01f;
            const float scoreGate = 1f;
            var thresholds = MakeThresholds(
                perBodyPositionThreshold: positionGate,
                perBodyRotationThreshold: 1f,
                perBodyVelocityThreshold: 0.05f,
                sceneScoreThreshold: scoreGate,
                sustainedSteps: 5,
                weightPosition: 0f,
                weightRotation: 0f,
                weightVelocity: 0f,
                weightSleep: 0f);

            var baseline = BuildFrames(10, 1, null);
            var perturbed = BuildFrames(10, 1, (step, body, offset, buffer) =>
            {
                buffer[offset] = positionGate * 10f;
            });

            var result = Analyze(baseline, perturbed, 0.001f, thresholds);
            Assert.That(Prop<bool>(result, "Succeeded"), Is.True, Prop<string>(result, "ErrorReason"));
            Assert.That(Prop<float[]>(result, "SceneScorePerStep")[0], Is.EqualTo(0f).Within(1e-6f));
            Assert.That(Prop<float>(result, "MaxSpreadMetres"), Is.GreaterThan(positionGate));
            Assert.That(Prop<bool>(result, "HasSignificantDivergence"), Is.False);
            Assert.That(Prop<int>(result, "FirstDivergenceFrame"), Is.EqualTo(-1));
        }

        [Test]
        public void SceneScoreExactlyAtThresholdDoesNotQualify()
        {
            // Strict `>` : score == SceneScoreThreshold must not qualify.
            var thresholds = MakeThresholds(
                perBodyPositionThreshold: 0.01f,
                perBodyRotationThreshold: 1f,
                perBodyVelocityThreshold: 0.05f,
                sceneScoreThreshold: 1f,
                sustainedSteps: 5,
                weightPosition: 1f,
                weightRotation: 0f,
                weightVelocity: 0f,
                weightSleep: 0f);

            var baseline = BuildFrames(10, 1, null);
            var perturbed = BuildFrames(10, 1, (step, body, offset, buffer) =>
            {
                // |Δpos| = 1 m on 1 m scale ⇒ posNorm = 1 ⇒ score = 1 == threshold.
                buffer[offset] = 1f;
            });

            var result = Analyze(baseline, perturbed, 0.001f, thresholds);
            Assert.That(Prop<bool>(result, "Succeeded"), Is.True, Prop<string>(result, "ErrorReason"));
            Assert.That(Prop<float[]>(result, "SceneScorePerStep")[0], Is.EqualTo(1f).Within(1e-5f));
            Assert.That(Prop<bool>(result, "HasSignificantDivergence"), Is.False);
            Assert.That(Prop<int>(result, "FirstDivergenceFrame"), Is.EqualTo(-1));
        }

        [Test]
        public void PositionExactlyAtThresholdDoesNotQualify()
        {
            // Strict `>` : position == PerBodyPositionThreshold must not count as affected.
            const float positionGate = 0.05f;
            var thresholds = MakeThresholds(
                perBodyPositionThreshold: positionGate,
                perBodyRotationThreshold: 1f,
                perBodyVelocityThreshold: 0.05f,
                sceneScoreThreshold: 0.01f,
                sustainedSteps: 5,
                weightPosition: 1f,
                weightRotation: 0f,
                weightVelocity: 0f,
                weightSleep: 0f);

            var baseline = BuildFrames(10, 1, null);
            var perturbed = BuildFrames(10, 1, (step, body, offset, buffer) =>
            {
                buffer[offset] = positionGate;
            });

            var result = Analyze(baseline, perturbed, 0.001f, thresholds);
            Assert.That(Prop<bool>(result, "Succeeded"), Is.True, Prop<string>(result, "ErrorReason"));
            Assert.That(Prop<float>(result, "MaxSpreadMetres"), Is.EqualTo(positionGate).Within(1e-6f));
            Assert.That(Prop<bool>(result, "HasSignificantDivergence"), Is.False);
            Assert.That(Prop<int[]>(result, "AffectedBodyIds"), Is.Empty);
            Assert.That(Prop<int>(result, "FirstDivergenceFrame"), Is.EqualTo(-1));
        }

        [Test]
        public void RotationOnlyDifferenceCannotBypassPositionCondition()
        {
            var baseline = BuildFrames(10, 1, null);
            var q = Quaternion.Euler(45f, 0f, 0f);
            var perturbed = BuildFrames(10, 1, (step, body, offset, buffer) =>
            {
                WriteRotationAt(buffer, offset, q);
            });

            var result = Analyze(baseline, perturbed, 0.001f);
            Assert.That(Prop<bool>(result, "HasSignificantDivergence"), Is.False);
        }

        [Test]
        public void VelocityOnlyDifferenceCannotBypassPositionCondition()
        {
            var baseline = BuildFrames(10, 1, null);
            var perturbed = BuildFrames(10, 1, (step, body, offset, buffer) =>
            {
                buffer[offset + 7] = 10f;
            });

            var result = Analyze(baseline, perturbed, 0.001f);
            Assert.That(Prop<bool>(result, "HasSignificantDivergence"), Is.False);
        }

        [Test]
        public void SleepOnlyDifferenceCannotBypassPositionCondition()
        {
            var baseline = BuildFrames(10, 1, null);
            var perturbed = BuildFrames(10, 1, (step, body, offset, buffer) =>
            {
                buffer[offset + 13] = 1f;
            });

            var result = Analyze(baseline, perturbed, 0.001f);
            Assert.That(Prop<bool>(result, "HasSignificantDivergence"), Is.False);
        }

        [Test]
        public void QuaternionAndNegativeQuaternionProduceZeroRotationError()
        {
            var baseline = BuildFrames(1, 1, null);
            var q = Quaternion.Euler(20f, 30f, 40f).normalized;
            WriteRotationAt(baseline.Frames, 0, q);
            var perturbedFrames = (float[])baseline.Frames.Clone();
            WriteRotationAt(
                perturbedFrames,
                0,
                new Quaternion(-q.x, -q.y, -q.z, -q.w));
            var perturbed = new FrameBuffer(perturbedFrames, 1, 1);

            var result = Analyze(baseline, perturbed, 0.001f);
            Assert.That(Prop<bool>(result, "Succeeded"), Is.True);
            Assert.That(Prop<float>(result, "MaxSpreadMetres"), Is.EqualTo(0f));
            Assert.That(
                Prop<float[]>(result, "SceneScorePerStep")[0],
                Is.EqualTo(0f).Within(1e-5f));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void NonFiniteQuaternionComponentReturnsStructuredFailure(float badComponent)
        {
            var baseline = BuildFrames(1, 1, null);
            var perturbedFrames = (float[])baseline.Frames.Clone();
            perturbedFrames[3] = badComponent;
            var perturbed = new FrameBuffer(perturbedFrames, 1, 1);

            var result = Analyze(baseline, perturbed, 0.001f);
            Assert.That(Prop<bool>(result, "Succeeded"), Is.False);
            Assert.That(Prop<string>(result, "ErrorReason"), Is.Not.Null.And.Not.Empty);
            Assert.That(
                Prop<string>(result, "ErrorReason"),
                Does.Contain("non-finite").IgnoreCase);
        }

        [Test]
        public void KnownQuaternionAngleProducesExpectedDegreeError()
        {
            var thresholds = MakeThresholds(
                1e-3f, 1f, 0.05f, 999f, 5, 0f, 1f, 0f, 0f);
            var baseline = BuildFrames(1, 1, null);
            var perturbed = BuildFrames(1, 1, null);
            WriteRotationAt(perturbed.Frames, 0, Quaternion.Euler(0f, 90f, 0f));

            var result = Analyze(baseline, perturbed, 0.001f, thresholds);
            Assert.That(
                Prop<float[]>(result, "SceneScorePerStep")[0],
                Is.EqualTo(90f).Within(0.05f));
        }

        [Test]
        public void PositionNormalizationUsesDocumentedObjectScaleConvention()
        {
            var thresholds = MakeThresholds(
                1e-3f, 1f, 0.05f, 999f, 5, 1f, 0f, 0f, 0f);
            var baseline = BuildFrames(1, 1, null);
            var perturbed = BuildFrames(1, 1, (step, body, offset, buffer) =>
            {
                buffer[offset] = 0.2f;
            });

            var unit = Analyze(baseline, perturbed, 0.001f, thresholds, new[] { 1f });
            var doubled = Analyze(baseline, perturbed, 0.001f, thresholds, new[] { 2f });

            Assert.That(Prop<float[]>(unit, "SceneScorePerStep")[0], Is.EqualTo(0.2f).Within(1e-5f));
            Assert.That(Prop<float[]>(doubled, "SceneScorePerStep")[0], Is.EqualTo(0.1f).Within(1e-5f));
            Assert.That(Prop<float>(unit, "MaxSpreadMetres"), Is.EqualTo(0.2f).Within(1e-5f));
        }

        [Test]
        public void ZeroObjectScaleDoesNotProduceNaNOrInfinity()
        {
            var baseline = BuildFrames(1, 1, null);
            var perturbed = BuildFrames(1, 1, (step, body, offset, buffer) =>
            {
                buffer[offset] = 0.05f;
            });

            var result = Analyze(baseline, perturbed, 0.001f, scales: new[] { 0f });
            var score = Prop<float[]>(result, "SceneScorePerStep")[0];
            Assert.That(Prop<bool>(result, "Succeeded"), Is.True);
            Assert.That(float.IsNaN(score), Is.False);
            Assert.That(float.IsInfinity(score), Is.False);
            Assert.That(float.IsNaN(Prop<float>(result, "MaxSpreadMetres")), Is.False);
        }

        [Test]
        public void MultipleAffectedBodiesReturnedInDeterministicOrder()
        {
            var baseline = BuildFrames(8, 3, null);
            var perturbed = BuildFrames(8, 3, (step, body, offset, buffer) =>
            {
                if (body == 0)
                {
                    buffer[offset] = 0.5f;
                }

                if (body == 1)
                {
                    buffer[offset] = 0.7f;
                }

                if (body == 2)
                {
                    buffer[offset] = 0.6f;
                }
            });

            var result = Analyze(
                baseline,
                perturbed,
                0.001f,
                stableBodyIds: new[] { 30, 10, 20 });

            Assert.That(
                Prop<bool>(result, "Succeeded"),
                Is.True,
                Prop<string>(result, "ErrorReason"));
            Assert.That(Prop<bool>(result, "HasSignificantDivergence"), Is.True);
            Assert.That(Prop<int[]>(result, "AffectedBodyIds"), Is.EqualTo(new[] { 10, 20, 30 }));
        }

        [Test]
        public void MaxSpreadIsReportedInMetres()
        {
            var baseline = BuildFrames(8, 1, null);
            var perturbed = BuildFrames(8, 1, (step, body, offset, buffer) =>
            {
                buffer[offset] = 0.042f;
            });

            var result = Analyze(baseline, perturbed, 0.001f);
            Assert.That(Prop<float>(result, "MaxSpreadMetres"), Is.EqualTo(0.042f).Within(1e-6f));
        }

        [Test]
        public void AmplificationCalculatedCorrectlyForNonZeroEpsilon()
        {
            var baseline = BuildFrames(8, 1, null);
            var perturbed = BuildFrames(8, 1, (step, body, offset, buffer) =>
            {
                buffer[offset] = 0.05f;
            });

            var result = Analyze(baseline, perturbed, 0.001f);
            Assert.That(Prop<bool>(result, "AmplificationDefined"), Is.True);
            Assert.That(Prop<float>(result, "Amplification"), Is.EqualTo(50f).Within(1e-4f));
        }

        [Test]
        public void ZeroEpsilonHandledWithoutNaNOrInfinity()
        {
            var baseline = BuildFrames(8, 1, null);
            var perturbed = BuildFrames(8, 1, (step, body, offset, buffer) =>
            {
                buffer[offset] = 0.05f;
            });

            var result = Analyze(baseline, perturbed, 0f);
            Assert.That(Prop<bool>(result, "AmplificationDefined"), Is.False);
            Assert.That(Prop<float>(result, "Amplification"), Is.EqualTo(0f));
            Assert.That(float.IsInfinity(Prop<float>(result, "Amplification")), Is.False);
        }

        [Test]
        public void InvalidInputDimensionsReturnStructuredFailure()
        {
            var shortBuffer = new FrameBuffer(new float[3], 2, 1);
            var result = Analyze(shortBuffer, shortBuffer, 0.001f);
            Assert.That(Prop<bool>(result, "Succeeded"), Is.False);
            Assert.That(Prop<string>(result, "ErrorReason"), Does.Contain("14"));
        }

        [Test]
        public void RepeatedAnalysisOfIdenticalInputsReturnsIdenticalResultData()
        {
            var baseline = BuildFrames(8, 2, null);
            var perturbed = BuildFrames(8, 2, (step, body, offset, buffer) =>
            {
                if (step >= 2)
                {
                    buffer[offset] = 0.8f + (body * 0.3f);
                }
            });

            var a = Analyze(baseline, perturbed, 0.002f, stableBodyIds: new[] { 5, 9 });
            var b = Analyze(baseline, perturbed, 0.002f, stableBodyIds: new[] { 5, 9 });

            Assert.That(
                Prop<bool>(a, "HasSignificantDivergence"),
                Is.EqualTo(Prop<bool>(b, "HasSignificantDivergence")));
            Assert.That(
                Prop<int>(a, "FirstDivergenceFrame"),
                Is.EqualTo(Prop<int>(b, "FirstDivergenceFrame")));
            Assert.That(
                Prop<float>(a, "MaxSpreadMetres"),
                Is.EqualTo(Prop<float>(b, "MaxSpreadMetres")));
            Assert.That(
                Prop<float>(a, "Amplification"),
                Is.EqualTo(Prop<float>(b, "Amplification")));
            Assert.That(
                Prop<int[]>(a, "AffectedBodyIds"),
                Is.EqualTo(Prop<int[]>(b, "AffectedBodyIds")));
            Assert.That(
                Prop<float[]>(a, "SceneScorePerStep"),
                Is.EqualTo(Prop<float[]>(b, "SceneScorePerStep")));
        }

        [Test]
        public void InputFrameArraysAreNotModified()
        {
            var baseline = BuildFrames(8, 1, null);
            var perturbed = BuildFrames(8, 1, (step, body, offset, buffer) =>
            {
                buffer[offset] = 0.05f;
            });
            var baselineCopy = (float[])baseline.Frames.Clone();
            var perturbedCopy = (float[])perturbed.Frames.Clone();

            Analyze(baseline, perturbed, 0.001f);

            Assert.That(baseline.Frames, Is.EqualTo(baselineCopy));
            Assert.That(perturbed.Frames, Is.EqualTo(perturbedCopy));
        }

        [Test]
        public void DivergenceSettingsExposeContractDefaults()
        {
            var settingsType = Type.GetType("BugCam.Core.DivergenceSettings, BugCam.Core");
            Assert.That(settingsType, Is.Not.Null);
            Assert.That(settingsType.IsSubclassOf(typeof(ScriptableObject)), Is.True);

            var settings = settingsType.GetMethod("CreateDefault").Invoke(null, null);
            Assert.That(Prop<object>(settings, "SustainedSteps"), Is.EqualTo(5));
            Assert.That(Prop<object>(settings, "EpsilonStart"), Is.EqualTo(1e-5f));
            Assert.That(Prop<object>(settings, "EpsilonCeiling"), Is.EqualTo(1e-2f));
            Assert.That(Prop<object>(settings, "LadderPointCount"), Is.EqualTo(12));
            Assert.That(Prop<object>(settings, "GhostBodyLimit"), Is.EqualTo(10));
            Assert.That(Prop<object>(settings, "EvidenceOcclusionRays"), Is.EqualTo(9));
            Assert.That(
                Prop<object>(settings, "FanMultipliers"),
                Is.EqualTo(new[] { 0.8f, 0.9f, 1f, 1.1f, 1.2f }));
            UnityEngine.Object.DestroyImmediate((UnityEngine.Object)settings);
        }

        [Test]
        public void AnalyzeRunResultFailedBaselineReturnsStructuredFailure()
        {
            var failed = RunResultFailure("baseline boom");
            var ok = RunResultSuccess(BuildFrames(2, 1, null), 0.001f, new[] { 1 });
            var result = AnalyzeRunResults(failed, ok);
            Assert.That(Prop<bool>(result, "Succeeded"), Is.False);
            Assert.That(Prop<string>(result, "ErrorReason"), Does.Contain("Baseline"));
        }

        [Test]
        public void AnalyzeRunResultFailedPerturbedReturnsStructuredFailure()
        {
            var ok = RunResultSuccess(BuildFrames(2, 1, null), 0.001f, new[] { 1 });
            var failed = RunResultFailure("perturbed boom");
            var result = AnalyzeRunResults(ok, failed);
            Assert.That(Prop<bool>(result, "Succeeded"), Is.False);
            Assert.That(Prop<string>(result, "ErrorReason"), Does.Contain("Perturbed"));
        }

        [Test]
        public void AnalyzeRunResultMismatchedStepCountReturnsStructuredFailure()
        {
            var baseline = RunResultSuccess(BuildFrames(3, 1, null), 0.001f, new[] { 1 });
            var perturbed = RunResultSuccess(BuildFrames(5, 1, null), 0.001f, new[] { 1 });
            var result = AnalyzeRunResults(baseline, perturbed);
            Assert.That(Prop<bool>(result, "Succeeded"), Is.False);
            Assert.That(Prop<string>(result, "ErrorReason"), Does.Contain("steps").IgnoreCase);
        }

        [Test]
        public void AnalyzeRunResultMismatchedBodyCountReturnsStructuredFailure()
        {
            var baseline = RunResultSuccess(BuildFrames(2, 1, null), 0.001f, new[] { 1 });
            var perturbed = RunResultSuccess(BuildFrames(2, 2, null), 0.001f, new[] { 1, 2 });
            var result = AnalyzeRunResults(baseline, perturbed);
            Assert.That(Prop<bool>(result, "Succeeded"), Is.False);
            Assert.That(Prop<string>(result, "ErrorReason"), Does.Contain("bodies").IgnoreCase);
        }

        [Test]
        public void AnalyzeRunResultMismatchedStableBodyIdsReturnsStructuredFailure()
        {
            var frames = BuildFrames(2, 2, null);
            var baseline = RunResultSuccess(frames, 0.001f, new[] { 10, 20 });
            var perturbed = RunResultSuccess(
                new FrameBuffer((float[])frames.Frames.Clone(), 2, 2),
                0.002f,
                new[] { 10, 99 });
            var result = AnalyzeRunResults(baseline, perturbed);
            Assert.That(Prop<bool>(result, "Succeeded"), Is.False);
            Assert.That(Prop<string>(result, "ErrorReason"), Does.Contain("Stable body ids"));
        }

        private static object Analyze(
            FrameBuffer baseline,
            FrameBuffer perturbed,
            float epsilon,
            object thresholds = null,
            float[] scales = null,
            int[] stableBodyIds = null)
        {
            var engineType = Type.GetType("BugCam.Core.DivergenceEngine, BugCam.Core");
            var thresholdsType = Type.GetType("BugCam.Core.DivergenceThresholds, BugCam.Core");
            Assert.That(engineType, Is.Not.Null);

            var method = engineType.GetMethod(
                "Analyze",
                new[]
                {
                    typeof(float[]),
                    typeof(float[]),
                    typeof(int),
                    typeof(int),
                    typeof(float[]),
                    typeof(float),
                    thresholdsType,
                    typeof(int[])
                });
            Assert.That(method, Is.Not.Null);

            return method.Invoke(
                null,
                new object[]
                {
                    baseline.Frames,
                    perturbed.Frames,
                    baseline.StepCount,
                    baseline.BodyCount,
                    scales,
                    epsilon,
                    thresholds ?? DefaultThresholds(),
                    stableBodyIds
                });
        }

        private static FrameBuffer BuildFrames(
            int stepCount,
            int bodyCount,
            Action<int, int, int, float[]> configure)
        {
            var frames = new float[stepCount * bodyCount * Stride];
            for (var step = 0; step < stepCount; step++)
            {
                for (var body = 0; body < bodyCount; body++)
                {
                    var offset = ((step * bodyCount) + body) * Stride;
                    frames[offset + 6] = 1f;
                    configure?.Invoke(step, body, offset, frames);
                }
            }

            return new FrameBuffer(frames, stepCount, bodyCount);
        }

        private static void WriteRotationAt(float[] frames, int offset, Quaternion rotation)
        {
            var q = rotation.normalized;
            frames[offset + 3] = q.x;
            frames[offset + 4] = q.y;
            frames[offset + 5] = q.z;
            frames[offset + 6] = q.w;
        }

        private static object DefaultThresholds()
        {
            var type = Type.GetType("BugCam.Core.DivergenceThresholds, BugCam.Core");
            return type.GetProperty("Default").GetValue(null);
        }

        private static object MakeThresholds(
            float perBodyPositionThreshold,
            float perBodyRotationThreshold,
            float perBodyVelocityThreshold,
            float sceneScoreThreshold,
            int sustainedSteps,
            float weightPosition,
            float weightRotation,
            float weightVelocity,
            float weightSleep)
        {
            var type = Type.GetType("BugCam.Core.DivergenceThresholds, BugCam.Core");
            return Activator.CreateInstance(
                type,
                perBodyPositionThreshold,
                perBodyRotationThreshold,
                perBodyVelocityThreshold,
                sceneScoreThreshold,
                sustainedSteps,
                weightPosition,
                weightRotation,
                weightVelocity,
                weightSleep);
        }

        private static object AnalyzeRunResults(object baseline, object perturbed)
        {
            var engineType = Type.GetType("BugCam.Core.DivergenceEngine, BugCam.Core");
            var runResultType = Type.GetType("BugCam.Core.RunResult, BugCam.Core");
            var thresholdsType = Type.GetType("BugCam.Core.DivergenceThresholds, BugCam.Core");
            Assert.That(engineType, Is.Not.Null);
            Assert.That(runResultType, Is.Not.Null);

            var method = engineType.GetMethod(
                "Analyze",
                new[]
                {
                    runResultType,
                    runResultType,
                    typeof(float[]),
                    thresholdsType
                });
            Assert.That(method, Is.Not.Null);

            return method.Invoke(
                null,
                new object[]
                {
                    baseline,
                    perturbed,
                    null,
                    DefaultThresholds()
                });
        }

        private static object RunResultFailure(string reason)
        {
            var runResultType = Type.GetType("BugCam.Core.RunResult, BugCam.Core");
            Assert.That(runResultType, Is.Not.Null);
            return runResultType.GetMethod("Failure").Invoke(null, new object[] { reason });
        }

        private static object RunResultSuccess(
            FrameBuffer frames,
            float epsilonMetres,
            int[] stableBodyIds)
        {
            var runResultType = Type.GetType("BugCam.Core.RunResult, BugCam.Core");
            var perturbationType = Type.GetType("BugCam.Core.SimulationPerturbation, BugCam.Core");
            Assert.That(runResultType, Is.Not.Null);
            Assert.That(perturbationType, Is.Not.Null);

            var perturbation = Activator.CreateInstance(perturbationType);
            var success = runResultType.GetMethod(
                "Success",
                new[]
                {
                    typeof(float[]),
                    typeof(int[]),
                    typeof(float),
                    perturbationType,
                    typeof(int),
                    typeof(int),
                    typeof(long)
                });
            Assert.That(success, Is.Not.Null);

            return success.Invoke(
                null,
                new object[]
                {
                    frames.Frames,
                    stableBodyIds,
                    epsilonMetres,
                    perturbation,
                    frames.StepCount,
                    0,
                    0L
                });
        }

        private static T Prop<T>(object target, string name)
        {
            return (T)target.GetType().GetProperty(name).GetValue(target);
        }
    }
}
