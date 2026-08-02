using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BugCam.Tests
{
    /// <summary>
    /// Minimal integration: DivergenceEngine accepts real RunResult data from the harness.
    /// </summary>
    public sealed class DivergenceEnginePlayModeTests
    {
        [UnityTest]
        public IEnumerator AnalyzeAcceptsRealRunResultsFromHarness()
        {
            Assert.That(Physics.simulationMode, Is.EqualTo(SimulationMode.Script));

            var bodyType = Type.GetType("BugCam.Core.SimulationBodyDefinition, BugCam.Core");
            var perturbationType = Type.GetType("BugCam.Core.SimulationPerturbation, BugCam.Core");
            var requestType = Type.GetType("BugCam.Core.SimulationRequest, BugCam.Core");
            var harnessType = Type.GetType("BugCam.Core.SimulationHarness, BugCam.Core");
            var runResultType = Type.GetType("BugCam.Core.RunResult, BugCam.Core");
            var engineType = Type.GetType("BugCam.Core.DivergenceEngine, BugCam.Core");
            var divergenceResultType = Type.GetType("BugCam.Core.DivergenceResult, BugCam.Core");
            var thresholdsType = Type.GetType("BugCam.Core.DivergenceThresholds, BugCam.Core");

            Assert.That(engineType, Is.Not.Null);
            Assert.That(runResultType, Is.Not.Null);

            var bodyCtor = bodyType.GetConstructor(new[]
            {
                typeof(int),
                typeof(Vector3),
                typeof(Quaternion),
                typeof(Vector3),
                typeof(float)
            });
            var body = bodyCtor.Invoke(new object[]
            {
                1,
                new Vector3(0f, 2f, 0f),
                Quaternion.identity,
                Vector3.one,
                1f
            });
            var bodies = Array.CreateInstance(bodyType, 1);
            bodies.SetValue(body, 0);

            var requestCtor = requestType.GetConstructor(new[]
            {
                bodyType.MakeArrayType(),
                typeof(int),
                perturbationType
            });
            var baselineRequest = requestCtor.Invoke(new[]
            {
                bodies,
                (object)20,
                Activator.CreateInstance(perturbationType)
            });

            var perturbationCtor = perturbationType.GetConstructor(new[]
            {
                typeof(int),
                typeof(Vector3),
                typeof(float)
            });
            var perturbation = perturbationCtor.Invoke(new object[]
            {
                1,
                Vector3.right,
                0.01f
            });
            var perturbedRequest = requestCtor.Invoke(new[]
            {
                bodies,
                (object)20,
                perturbation
            });

            var harness = Activator.CreateInstance(harnessType);
            var run = harnessType.GetMethod("Run", new[] { requestType });
            var initialSceneCount = SceneManager.sceneCount;

            var harnessBaseline = run.Invoke(harness, new[] { baselineRequest });
            var harnessPerturbed = run.Invoke(harness, new[] { perturbedRequest });

            var fromHarness = runResultType.GetMethod(
                "FromSimulationRunResult",
                new[]
                {
                    Type.GetType("BugCam.Core.SimulationRunResult, BugCam.Core"),
                    typeof(int),
                    typeof(long)
                });
            var baseline = fromHarness.Invoke(null, new[] { harnessBaseline, (object)0, 0L });
            var perturbed = fromHarness.Invoke(null, new[] { harnessPerturbed, (object)0, 0L });

            Assert.That(
                runResultType.GetProperty("Succeeded").GetValue(baseline),
                Is.True);
            Assert.That(
                runResultType.GetProperty("Succeeded").GetValue(perturbed),
                Is.True);

            var analyze = engineType.GetMethod(
                "Analyze",
                new[]
                {
                    runResultType,
                    runResultType,
                    typeof(float[]),
                    thresholdsType
                });
            var thresholds = thresholdsType.GetProperty("Default").GetValue(null);
            var result = analyze.Invoke(
                null,
                new[] { baseline, perturbed, new[] { 1f }, thresholds });

            Assert.That(
                divergenceResultType.GetProperty("Succeeded").GetValue(result),
                Is.True,
                "Engine must accept real RunResult pairs without structured failure.");
            Assert.That(
                divergenceResultType.GetProperty("EpsilonMetres").GetValue(result),
                Is.EqualTo(0.01f).Within(1e-6f));
            Assert.That(
                divergenceResultType.GetProperty("StepCount").GetValue(result),
                Is.EqualTo(20));
            Assert.That(
                divergenceResultType.GetProperty("SceneScorePerStep").GetValue(result),
                Is.Not.Null);

            for (var frame = 0; frame < 60 && SceneManager.sceneCount != initialSceneCount; frame++)
            {
                yield return null;
            }

            Assert.That(
                SceneManager.sceneCount,
                Is.EqualTo(initialSceneCount),
                "Harness temporary scenes must unload after integration analyze.");
        }
    }
}
