using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace BugCam.Tests
{
    /// <summary>
    /// Block 2.1 deterministic evidence-camera selection EditMode contracts.
    /// Reflection zero-ref pattern — BugCam.Tests asmdef references no production assemblies
    /// (matches GhostEvidenceTests.cs). Every fixture is a pure recorded-frame construction; no
    /// Play Mode, no live scene, no Camera/Collider GameObjects anywhere in this file.
    /// </summary>
    public sealed class EvidenceCameraTests
    {
        private const int Stride = 14;

        [Test]
        public void EvidenceAssemblyStillReferencesOnlyCoreAfterBlock21()
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies();
            Type evidenceType = null;
            foreach (var a in asm)
            {
                if (a.GetName().Name == "BugCam.Evidence")
                {
                    evidenceType = a.GetType("BugCam.Evidence.EvidenceCameras");
                    if (evidenceType != null)
                    {
                        break;
                    }
                }
            }

            Assert.That(evidenceType, Is.Not.Null, "BugCam.Evidence.EvidenceCameras must compile.");

            var refs = evidenceType.Assembly.GetReferencedAssemblies();
            var names = new string[refs.Length];
            for (var i = 0; i < refs.Length; i++)
            {
                names[i] = refs[i].Name;
            }

            Assert.That(Array.IndexOf(names, "BugCam.Core") >= 0, Is.True);
            Assert.That(Array.IndexOf(names, "UnityEditor") < 0, Is.True);
            Assert.That(Array.IndexOf(names, "UnityEditor.CoreModule") < 0, Is.True);
        }

        [Test]
        public void RepeatedPlanOverIdenticalRecordedRunsYieldsByteIdenticalCameraPlanJson()
        {
            var fixture = BuildOccluderFixture();
            var first = InvokePlan(fixture.Baseline, fixture.Perturbed, fixture.Divergence, fixture.Sizes, fixture.Settings);
            var second = InvokePlan(fixture.Baseline, fixture.Perturbed, fixture.Divergence, fixture.Sizes, fixture.Settings);

            Assert.That(Prop<bool>(first, "Succeeded"), Is.True);

            var firstJson = BuildJson(first, "det-check");
            var secondJson = BuildJson(second, "det-check");
            Assert.That(secondJson, Is.EqualTo(firstJson), "Identical inputs must yield byte-identical camera-plan.json.");

            var firstWinners = (Array)Prop<object>(first, "Winners");
            var secondWinners = (Array)Prop<object>(second, "Winners");
            Assert.That(secondWinners.Length, Is.EqualTo(firstWinners.Length));
            for (var i = 0; i < firstWinners.Length; i++)
            {
                Assert.That(
                    Prop<int>(secondWinners.GetValue(i), "CandidateIndex"),
                    Is.EqualTo(Prop<int>(firstWinners.GetValue(i), "CandidateIndex")),
                    "Winner candidate indices must repeat identically.");
            }
        }

        [Test]
        public void AllCandidatesAreReturnedIncludingRejectedOnesForProvenance()
        {
            var fixture = BuildOccluderFixture();
            var result = InvokePlan(fixture.Baseline, fixture.Perturbed, fixture.Divergence, fixture.Sizes, fixture.Settings);
            Assert.That(Prop<bool>(result, "Succeeded"), Is.True);

            var candidates = (Array)Prop<object>(result, "Candidates");
            var candidateCount = Prop<int>(result, "CandidateCount");
            Assert.That(candidates.Length, Is.EqualTo(candidateCount));
        }

        [Test]
        public void Camera1IsTheHighestScoringNonRejectedCandidate()
        {
            var fixture = BuildOccluderFixture();
            var result = InvokePlan(fixture.Baseline, fixture.Perturbed, fixture.Divergence, fixture.Sizes, fixture.Settings);
            Assert.That(Prop<bool>(result, "Succeeded"), Is.True);

            var candidates = (Array)Prop<object>(result, "Candidates");
            var bestIndex = -1;
            var bestScore = 0f;
            for (var i = 0; i < candidates.Length; i++)
            {
                var c = candidates.GetValue(i);
                if (Prop<bool>(c, "RejectedBelowGroundPlane"))
                {
                    continue;
                }

                var score = Prop<float>(c, "TotalScore");
                if (bestIndex < 0 || score > bestScore)
                {
                    bestIndex = i;
                    bestScore = score;
                }
            }

            var winners = (Array)Prop<object>(result, "Winners");
            Assert.That(winners.Length, Is.GreaterThanOrEqualTo(1));
            var winner0 = winners.GetValue(0);
            Assert.That(Prop<int>(winner0, "Slot"), Is.EqualTo(1));
            Assert.That(Prop<int>(winner0, "CandidateIndex"), Is.EqualTo(bestIndex));
        }

        [Test]
        public void WinnersBeyondCamera1AreDistinctAndExcludeCamera1()
        {
            var fixture = BuildOccluderFixture();
            var result = InvokePlan(fixture.Baseline, fixture.Perturbed, fixture.Divergence, fixture.Sizes, fixture.Settings);
            Assert.That(Prop<bool>(result, "Succeeded"), Is.True);

            var winners = (Array)Prop<object>(result, "Winners");
            Assert.That(winners.Length, Is.InRange(1, 4));

            var camera1Index = Prop<int>(winners.GetValue(0), "CandidateIndex");
            var seen = new System.Collections.Generic.HashSet<int> { camera1Index };
            for (var i = 1; i < winners.Length; i++)
            {
                var w = winners.GetValue(i);
                Assert.That(Prop<int>(w, "Slot"), Is.EqualTo(i + 1));
                var idx = Prop<int>(w, "CandidateIndex");
                Assert.That(seen.Add(idx), Is.True, "Winner candidate indices must be distinct and exclude camera 1.");
            }
        }

        [Test]
        public void VisibilityScoreIsFractionalWhenAnAdjacentBodyPartiallyOccludes()
        {
            // PLAN requires occlusion to be "fractional ... never binary" (hits/9, not 0-or-1).
            // This fixture's adjacent occluder (body 2) is close enough to body 1 that every
            // surviving candidate sees at least some of the 9 sample rays blocked — the property
            // under test is that the value is a genuine fraction, not that some candidate must
            // also see a fully-clear 9/9, which PLAN does not require.
            var fixture = BuildOccluderFixture();
            var result = InvokePlan(fixture.Baseline, fixture.Perturbed, fixture.Divergence, fixture.Sizes, fixture.Settings);
            Assert.That(Prop<bool>(result, "Succeeded"), Is.True);

            var candidates = (Array)Prop<object>(result, "Candidates");
            var foundFractional = false;
            for (var i = 0; i < candidates.Length; i++)
            {
                var c = candidates.GetValue(i);
                if (Prop<bool>(c, "RejectedBelowGroundPlane"))
                {
                    continue;
                }

                var visibility = Prop<float>(c, "VisibilityScore");
                if (visibility > 0.0001f && visibility < 0.9999f)
                {
                    foundFractional = true;
                    break;
                }
            }

            Assert.That(
                foundFractional,
                Is.True,
                "Never binary — at least one candidate must show partial occlusion (0 < visibility < 1).");
        }

        [Test]
        public void GroundPlaneCandidatesAreRejectedAndUnscored()
        {
            var fixture = BuildLowAltitudeFixture();
            var result = InvokePlan(fixture.Baseline, fixture.Perturbed, fixture.Divergence, fixture.Sizes, fixture.Settings);
            Assert.That(Prop<bool>(result, "Succeeded"), Is.True);

            var candidates = (Array)Prop<object>(result, "Candidates");
            var rejectedCount = 0;
            var survivorCount = 0;
            for (var i = 0; i < candidates.Length; i++)
            {
                var c = candidates.GetValue(i);
                if (Prop<bool>(c, "RejectedBelowGroundPlane"))
                {
                    rejectedCount++;
                    Assert.That(Prop<float>(c, "InFrustumScore"), Is.EqualTo(0f));
                    Assert.That(Prop<float>(c, "VisibilityScore"), Is.EqualTo(0f));
                    Assert.That(Prop<float>(c, "TotalScore"), Is.EqualTo(0f));
                }
                else
                {
                    survivorCount++;
                }
            }

            Assert.That(rejectedCount, Is.GreaterThan(0), "This fixture must produce at least one below-ground candidate.");
            Assert.That(survivorCount, Is.GreaterThan(0), "This fixture must also produce at least one surviving candidate.");
        }

        [Test]
        public void HonestVerdictFlipsToLowWhenCoverageGateIsRaised()
        {
            var fixture = BuildOccluderFixture();
            var defaultResult = InvokePlan(fixture.Baseline, fixture.Perturbed, fixture.Divergence, fixture.Sizes, fixture.Settings);
            Assert.That(Prop<bool>(defaultResult, "Succeeded"), Is.True);
            Assert.That(Prop<string>(defaultResult, "Verdict"), Is.EqualTo("EVIDENCE COVERAGE: OK"));
            Assert.That(Prop<bool>(defaultResult, "HasAdequateCoverage"), Is.True);

            var settingsType = Type.GetType("BugCam.Core.DivergenceSettings, BugCam.Core");
            var field = settingsType.GetField("minEvidenceCoverageScore", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null);
            field.SetValue(fixture.Settings, 1000f);

            var strictResult = InvokePlan(fixture.Baseline, fixture.Perturbed, fixture.Divergence, fixture.Sizes, fixture.Settings);
            Assert.That(Prop<bool>(strictResult, "Succeeded"), Is.True);
            Assert.That(Prop<string>(strictResult, "Verdict"), Is.EqualTo("EVIDENCE COVERAGE: LOW"));
            Assert.That(Prop<bool>(strictResult, "HasAdequateCoverage"), Is.False);

            // Raising the gate must not change the underlying candidate scores, only the verdict.
            Assert.That(
                Prop<float>(strictResult, "BestScorePerBody"),
                Is.EqualTo(Prop<float>(defaultResult, "BestScorePerBody")).Within(1e-6f));
        }

        [Test]
        public void PlanFailsHonestlyWithNoFabricationWhenThereIsNoSignificantDivergence()
        {
            var settingsType = Type.GetType("BugCam.Core.DivergenceSettings, BugCam.Core");
            var settings = settingsType.GetMethod("CreateDefault", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, null);

            var positions = new[] { new Vector3(0f, 3f, 0f), new Vector3(3f, 3f, 3f) };
            var frames = BuildStaticFrames(6, positions);
            var stableIds = new[] { 1, 2 };

            var baseline = BuildRunResultSuccess(frames, stableIds, 0f, 6);
            var perturbed = BuildRunResultSuccess((float[])frames.Clone(), stableIds, 0f, 6);

            var divergenceType = Type.GetType("BugCam.Core.DivergenceEngine, BugCam.Core");
            var runResultType = Type.GetType("BugCam.Core.RunResult, BugCam.Core");
            var analyze = divergenceType.GetMethod(
                "Analyze",
                new[] { runResultType, runResultType, typeof(float[]) });
            var divergence = analyze.Invoke(null, new object[] { baseline, perturbed, null });
            Assert.That(Prop<bool>(divergence, "HasSignificantDivergence"), Is.False);

            var sizes = new[] { Vector3.one, Vector3.one };
            var result = InvokePlan(baseline, perturbed, divergence, sizes, settings);
            Assert.That(Prop<bool>(result, "Succeeded"), Is.False);
            Assert.That(Prop<string>(result, "ErrorReason"), Is.Not.Empty);

            var json = BuildJson(result, "no-divergence");
            Assert.That(json, Does.Contain("\"success\":false"));
            Assert.That(json, Does.Contain("\"eventBounds\":null"));
            Assert.That(json, Does.Contain("\"bestScorePerBody\":null"));
            Assert.That(json, Does.Contain("\"candidates\":[]"));
            Assert.That(json, Does.Contain("\"winners\":[]"));
        }

        // ------------------------------------------------------------------------------------
        // Fixtures
        // ------------------------------------------------------------------------------------

        private struct Fixture
        {
            public object Baseline;
            public object Perturbed;
            public object Divergence;
            public Vector3[] Sizes;
            public object Settings;
        }

        /// <summary>
        /// 3 bodies. Body 1 (id=1) diverges 2m in +Z from step 0, held for the whole run — a
        /// step function, so it is affected at every step and comfortably exceeds both the scene
        /// score gate and SustainedSteps=5 with FirstDivergenceFrame=0. Event bounds therefore
        /// center on body 1's perturbed position (0,3,2), boundingRadius ~0.866m, candidate
        /// sphere radius ~2.165m at the default EvidenceEventBoundsRadiusMultiplier. Body 2 (id=2)
        /// sits immediately adjacent to that same perturbed position, offset 0.7m in +X — well
        /// inside the candidate sphere, close enough to partially occlude camera angles roughly
        /// aligned with +X while leaving angles from the opposite/perpendicular side clear. Body 3
        /// (id=3) sits far outside the candidate sphere and unaffected — present only to exercise
        /// the "every other body" occlusion loop with a body that should essentially never block.
        /// </summary>
        private static Fixture BuildOccluderFixture()
        {
            const int stepCount = 6;
            var baselinePositions = new[]
            {
                new Vector3(0f, 3f, 0f),
                new Vector3(0.7f, 3f, 2f),
                new Vector3(6f, 3f, 6f),
            };
            var perturbedPositions = new[]
            {
                new Vector3(0f, 3f, 2f),
                new Vector3(0.7f, 3f, 2f),
                new Vector3(6f, 3f, 6f),
            };

            return BuildFixture(stepCount, baselinePositions, perturbedPositions);
        }

        /// <summary>
        /// Same shape as <see cref="BuildOccluderFixture"/> but the diverging body sits at y=1,
        /// close enough to the ground plane (y=0) that the candidate sphere (radius ~2.16m at the
        /// default EvidenceEventBoundsRadiusMultiplier) is guaranteed to punch below y=0 for some
        /// Fibonacci directions while others stay comfortably above it.
        /// </summary>
        private static Fixture BuildLowAltitudeFixture()
        {
            const int stepCount = 6;
            var baselinePositions = new[]
            {
                new Vector3(0f, 1f, 0f),
                new Vector3(6f, 1f, 6f),
            };
            var perturbedPositions = new[]
            {
                new Vector3(0f, 1f, 2f),
                new Vector3(6f, 1f, 6f),
            };

            return BuildFixture(stepCount, baselinePositions, perturbedPositions);
        }

        private static Fixture BuildFixture(int stepCount, Vector3[] baselinePositions, Vector3[] perturbedPositions)
        {
            var bodyCount = baselinePositions.Length;
            var stableIds = new int[bodyCount];
            for (var i = 0; i < bodyCount; i++)
            {
                stableIds[i] = i + 1;
            }

            var baselineFrames = BuildStaticFrames(stepCount, baselinePositions);
            var perturbedFrames = BuildStaticFrames(stepCount, perturbedPositions);

            var baseline = BuildRunResultSuccess(baselineFrames, stableIds, 0f, stepCount);
            var perturbed = BuildRunResultSuccess(perturbedFrames, stableIds, 2f, stepCount);

            var divergenceType = Type.GetType("BugCam.Core.DivergenceEngine, BugCam.Core");
            var runResultType = Type.GetType("BugCam.Core.RunResult, BugCam.Core");
            var analyze = divergenceType.GetMethod(
                "Analyze",
                new[] { runResultType, runResultType, typeof(float[]) });
            var divergence = analyze.Invoke(null, new object[] { baseline, perturbed, null });
            Assert.That(
                Prop<bool>(divergence, "HasSignificantDivergence"),
                Is.True,
                "Fixture must produce significant divergence — check the constants above.");
            Assert.That(Prop<int>(divergence, "FirstDivergenceFrame"), Is.EqualTo(0));

            var sizes = new Vector3[bodyCount];
            for (var i = 0; i < bodyCount; i++)
            {
                sizes[i] = Vector3.one;
            }

            var settingsType = Type.GetType("BugCam.Core.DivergenceSettings, BugCam.Core");
            var settings = settingsType.GetMethod("CreateDefault", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, null);

            return new Fixture
            {
                Baseline = baseline,
                Perturbed = perturbed,
                Divergence = divergence,
                Sizes = sizes,
                Settings = settings,
            };
        }

        private static float[] BuildStaticFrames(int stepCount, Vector3[] positions)
        {
            var bodyCount = positions.Length;
            var frames = new float[stepCount * bodyCount * Stride];
            for (var step = 0; step < stepCount; step++)
            {
                for (var b = 0; b < bodyCount; b++)
                {
                    var offset = ((step * bodyCount) + b) * Stride;
                    frames[offset + 0] = positions[b].x;
                    frames[offset + 1] = positions[b].y;
                    frames[offset + 2] = positions[b].z;
                    frames[offset + 3] = 0f;
                    frames[offset + 4] = 0f;
                    frames[offset + 5] = 0f;
                    frames[offset + 6] = 1f; // identity rotation
                    // vel.xyz, angVel.xyz, sleeping all default to 0.
                }
            }

            return frames;
        }

        private static object BuildRunResultSuccess(
            float[] frames, int[] stableIds, float epsilonMetres, int stepCount)
        {
            var runResultType = Type.GetType("BugCam.Core.RunResult, BugCam.Core");
            var perturbationType = Type.GetType("BugCam.Core.SimulationPerturbation, BugCam.Core");
            var perturbation = Activator.CreateInstance(perturbationType);

            var success = runResultType.GetMethod("Success", BindingFlags.Public | BindingFlags.Static);
            return success.Invoke(
                null,
                new object[] { frames, stableIds, epsilonMetres, perturbation, stepCount, 0, 0L });
        }

        private static object InvokePlan(
            object baseline, object perturbed, object divergence, Vector3[] sizes, object settings)
        {
            var evidenceCamerasType = Type.GetType("BugCam.Evidence.EvidenceCameras, BugCam.Evidence");
            Assert.That(evidenceCamerasType, Is.Not.Null);
            var plan = evidenceCamerasType.GetMethod("Plan", BindingFlags.Public | BindingFlags.Static);
            Assert.That(plan, Is.Not.Null);
            return plan.Invoke(null, new[] { baseline, perturbed, divergence, sizes, settings });
        }

        private static string BuildJson(object planResult, string runId)
        {
            var writerType = Type.GetType("BugCam.Evidence.EvidenceCameraPlanWriter, BugCam.Evidence");
            Assert.That(writerType, Is.Not.Null);
            var buildJson = writerType.GetMethod("BuildJson", BindingFlags.Public | BindingFlags.Static);
            Assert.That(buildJson, Is.Not.Null);
            return (string)buildJson.Invoke(null, new[] { planResult, runId });
        }

        private static T Prop<T>(object target, string name)
        {
            return (T)target.GetType().GetProperty(name).GetValue(target);
        }
    }
}
