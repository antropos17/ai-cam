using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BugCam.Tests
{
    public sealed class SimulationHarnessContractTests
    {
        [Test]
        public void CoreAssemblyExposesSimulationHarness()
        {
            var harnessType = Type.GetType("BugCam.Core.SimulationHarness, BugCam.Core");

            Assert.That(
                harnessType,
                Is.Not.Null,
                "Block 1.1 requires BugCam.Core.SimulationHarness in the BugCam.Core assembly.");
        }

        [Test]
        public void CoreAssemblyExposesBlockOneSimulationContract()
        {
            var constantsType = Type.GetType("BugCam.Core.BugCamConstants, BugCam.Core");
            var bodyType = Type.GetType("BugCam.Core.SimulationBodyDefinition, BugCam.Core");
            var perturbationType = Type.GetType("BugCam.Core.SimulationPerturbation, BugCam.Core");
            var requestType = Type.GetType("BugCam.Core.SimulationRequest, BugCam.Core");
            var resultType = Type.GetType("BugCam.Core.SimulationRunResult, BugCam.Core");
            var harnessType = Type.GetType("BugCam.Core.SimulationHarness, BugCam.Core");

            Assert.That(constantsType, Is.Not.Null, "BugCamConstants is part of the Core contract.");
            Assert.That(bodyType, Is.Not.Null, "SimulationBodyDefinition is part of the Core contract.");
            Assert.That(perturbationType, Is.Not.Null, "SimulationPerturbation is part of the Core contract.");
            Assert.That(requestType, Is.Not.Null, "SimulationRequest is part of the Core contract.");
            Assert.That(resultType, Is.Not.Null, "SimulationRunResult is part of the Core contract.");
            Assert.That(
                constantsType.GetField("FixedStep")?.GetRawConstantValue(),
                Is.EqualTo(0.02f),
                "The only physics step is 0.02 seconds.");
            Assert.That(
                harnessType?.GetMethod("Run"),
                Is.Not.Null,
                "SimulationHarness.Run is the Block 1.1 entry point.");
        }

        [Test]
        public void EditorProbeReadsActualPhysicsThreadingMode()
        {
            var threadingModeType = Type.GetType(
                "BugCam.Core.SimulationThreadingMode, BugCam.Core");
            var requestType = Type.GetType("BugCam.Core.SimulationRequest, BugCam.Core");
            var settingsProbeType = Type.GetType(
                "BugCam.Editor.PhysicsSettingsProbe, Assembly-CSharp-Editor");
            var probeRunnerType = Type.GetType(
                "BugCam.Editor.DeterminismProbeRunner, Assembly-CSharp-Editor");

            Assert.That(settingsProbeType, Is.Not.Null, "The Editor settings probe must exist.");
            var readMethod = settingsProbeType.GetMethod("ReadThreadingMode", Type.EmptyTypes);
            Assert.That(
                readMethod,
                Is.Not.Null,
                "The threading mode must be read from DynamicsManager.asset.");

            var settingsAssets = AssetDatabase.LoadAllAssetsAtPath(
                "ProjectSettings/DynamicsManager.asset");
            Assert.That(settingsAssets, Is.Not.Empty, "Unity must expose DynamicsManager.asset.");
            var serializedSettings = new SerializedObject(settingsAssets[0]);
            var threadingModeProperty = serializedSettings.FindProperty("m_ThreadingMode");
            Assert.That(
                threadingModeProperty,
                Is.Not.Null,
                "The active Unity version must expose m_ThreadingMode.");

            var expectedModeName = threadingModeProperty.intValue == 0
                ? "MultiThreaded"
                : threadingModeProperty.intValue == 1
                    ? "SingleThreaded"
                    : throw new AssertionException(
                        "Unsupported m_ThreadingMode value: " + threadingModeProperty.intValue);
            var expectedMode = Enum.Parse(threadingModeType, expectedModeName);
            Assert.That(
                readMethod.Invoke(null, null),
                Is.EqualTo(expectedMode),
                "Reported metadata must match the project's actual physics threading mode.");

            Assert.That(
                probeRunnerType?.GetMethod(
                    "RunCurrentMode",
                    new[] { requestType, requestType }),
                Is.Not.Null,
                "The Editor runner must inject the measured mode without a caller-supplied label.");
        }

        [Test]
        public void RepeatabilityMetricsRejectComponentBeyondGate()
        {
            var calculatorType = Type.GetType(
                "BugCam.Core.RepeatabilityMetricsCalculator, BugCam.Core");
            Assert.That(
                calculatorType,
                Is.Not.Null,
                "Core must calculate repeatability metrics from recorded state frames.");
            var calculateMethod = calculatorType.GetMethod(
                "Calculate",
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                new[] { typeof(float[]), typeof(float[]) },
                null);
            Assert.That(
                calculateMethod,
                Is.Not.Null,
                "Repeatability metrics must compare two same-shaped state frame arrays.");

            var metrics = calculateMethod.Invoke(
                null,
                new object[] { new[] { 0f }, new[] { 0.000002f } });
            var metricsType = metrics.GetType();
            Assert.That(metricsType.GetProperty("BitwiseEqual")?.GetValue(metrics), Is.EqualTo(false));
            Assert.That(
                metricsType.GetProperty("MaxComponentDelta")?.GetValue(metrics),
                Is.EqualTo(0.000002f).Within(1e-9f));
            Assert.That(
                metricsType.GetProperty("WithinGate")?.GetValue(metrics),
                Is.EqualTo(false),
                "A component above the 1e-6 gate must fail repeatability.");
        }

        [Test]
        public void TowerSceneGeneratorCreatesFortyEightCubeTowerAndProjectile()
        {
            var generatorType = Type.GetType(
                "BugCam.Editor.TowerSceneGenerator, Assembly-CSharp-Editor");
            Assert.That(
                generatorType,
                Is.Not.Null,
                "Block 1.1 requires an Editor generator for the procedural TowerScene.");
            var generateMethod = generatorType.GetMethod(
                "GenerateTowerScene",
                BindingFlags.Static | BindingFlags.Public);
            Assert.That(
                generateMethod,
                Is.Not.Null,
                "TowerSceneGenerator must expose GenerateTowerScene for reproducible scene generation.");

            generateMethod.Invoke(null, null);

            const string towerScenePath = "Assets/BugCam/Tests/TowerScene.unity";
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(towerScenePath), Is.Not.Null);
            var towerScene = SceneManager.GetSceneByPath(towerScenePath);
            if (!towerScene.isLoaded)
            {
                towerScene = EditorSceneManager.OpenScene(towerScenePath, OpenSceneMode.Additive);
            }
            var rigidbodies = new System.Collections.Generic.List<Rigidbody>();
            foreach (var root in towerScene.GetRootGameObjects())
            {
                rigidbodies.AddRange(root.GetComponentsInChildren<Rigidbody>());
            }

            Assert.That(rigidbodies, Has.Count.EqualTo(49));
            var projectile = towerScene.GetRootGameObjects();
            GameObject projectileObject = null;
            for (var rootIndex = 0; rootIndex < projectile.Length; rootIndex++)
            {
                if (projectile[rootIndex].name == "Projectile")
                {
                    projectileObject = projectile[rootIndex];
                    break;
                }
            }

            Assert.That(projectileObject, Is.Not.Null);
            Assert.That(projectileObject.GetComponent<Rigidbody>().linearVelocity.x, Is.GreaterThan(0f));
        }

        [Test]
        public void TowerProbeRequestFactoryCreatesFortyNineBodyProjectileScenario()
        {
            var factoryType = Type.GetType(
                "BugCam.Editor.TowerProbeRequestFactory, Assembly-CSharp-Editor");
            Assert.That(factoryType, Is.Not.Null);
            var request = factoryType.GetMethod(
                "CreateBaseline",
                BindingFlags.Static | BindingFlags.Public)?.Invoke(null, new object[] { 250 });
            Assert.That(request, Is.Not.Null);

            var requestType = Type.GetType("BugCam.Core.SimulationRequest, BugCam.Core");
            var bodies = requestType.GetProperty("Bodies")?.GetValue(request) as Array;
            Assert.That(bodies, Has.Length.EqualTo(49));
            var bodyType = Type.GetType("BugCam.Core.SimulationBodyDefinition, BugCam.Core");
            var projectile = bodies.GetValue(48);
            Assert.That(bodyType.GetProperty("StableId")?.GetValue(projectile), Is.EqualTo(49));
            Assert.That(
                bodyType.GetProperty("InitialLinearVelocity")?.GetValue(projectile),
                Is.EqualTo(Vector3.right * 12f));
        }

        [Test]
        public void TowerProbeRequestFactoryRecordsProjectilePerturbation()
        {
            var factoryType = Type.GetType(
                "BugCam.Editor.TowerProbeRequestFactory, Assembly-CSharp-Editor");
            var request = factoryType.GetMethod(
                "CreatePerturbed",
                BindingFlags.Static | BindingFlags.Public)?.Invoke(
                null,
                new object[] { 250, 0.001f });
            Assert.That(request, Is.Not.Null);

            var requestType = Type.GetType("BugCam.Core.SimulationRequest, BugCam.Core");
            var perturbation = requestType.GetProperty("Perturbation")?.GetValue(request);
            var perturbationType = Type.GetType(
                "BugCam.Core.SimulationPerturbation, BugCam.Core");
            Assert.That(
                perturbationType.GetProperty("TargetBodyId")?.GetValue(perturbation),
                Is.EqualTo(49));
            Assert.That(
                perturbationType.GetProperty("Axis")?.GetValue(perturbation),
                Is.EqualTo(Vector3.right));
            Assert.That(
                perturbationType.GetProperty("MagnitudeMetres")?.GetValue(perturbation),
                Is.EqualTo(0.001f));
        }
    }
}
