using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

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

        [UnityTest]
        public IEnumerator RunSimulatesOneBodyInFreshLocalPhysicsScene()
        {
            yield return new EnterPlayMode();

            Assert.That(
                Physics.simulationMode,
                Is.EqualTo(SimulationMode.Script),
                "SimulationHarness.Run requires Physics.simulationMode to be Script before simulation begins.");

            var bodyType = Type.GetType("BugCam.Core.SimulationBodyDefinition, BugCam.Core");
            var perturbationType = Type.GetType("BugCam.Core.SimulationPerturbation, BugCam.Core");
            var requestType = Type.GetType("BugCam.Core.SimulationRequest, BugCam.Core");
            var resultType = Type.GetType("BugCam.Core.SimulationRunResult, BugCam.Core");
            var harnessType = Type.GetType("BugCam.Core.SimulationHarness, BugCam.Core");

            Assert.That(bodyType, Is.Not.Null, "SimulationBodyDefinition must be available to construct a body.");
            Assert.That(perturbationType, Is.Not.Null, "SimulationPerturbation must be available for the baseline run.");
            Assert.That(requestType, Is.Not.Null, "SimulationRequest must be available to configure the run.");
            Assert.That(resultType, Is.Not.Null, "SimulationRunResult must be available to inspect the run outcome.");
            Assert.That(harnessType, Is.Not.Null, "SimulationHarness must be available to execute the run.");

            var bodyConstructor = bodyType.GetConstructor(new[]
            {
                typeof(int),
                typeof(Vector3),
                typeof(Quaternion),
                typeof(Vector3),
                typeof(float)
            });
            Assert.That(
                bodyConstructor,
                Is.Not.Null,
                "SimulationBodyDefinition must expose the (int, Vector3, Quaternion, Vector3, float) constructor.");

            var body = bodyConstructor.Invoke(new object[]
            {
                7,
                new Vector3(0f, 2f, 0f),
                Quaternion.identity,
                Vector3.one,
                1f
            });
            var bodies = Array.CreateInstance(bodyType, 1);
            bodies.SetValue(body, 0);

            var perturbation = Activator.CreateInstance(perturbationType);
            var requestConstructor = requestType.GetConstructor(new[]
            {
                bodyType.MakeArrayType(),
                typeof(int),
                perturbationType
            });
            Assert.That(
                requestConstructor,
                Is.Not.Null,
                "SimulationRequest must expose the (SimulationBodyDefinition[], int, SimulationPerturbation) constructor.");

            var request = requestConstructor.Invoke(new[] { bodies, (object)10, perturbation });
            var runMethod = harnessType.GetMethod("Run", new[] { requestType });
            Assert.That(
                runMethod,
                Is.Not.Null,
                "SimulationHarness must expose Run(SimulationRequest).");

            var initialSceneCount = SceneManager.sceneCount;
            var harness = Activator.CreateInstance(harnessType);
            var result = runMethod.Invoke(harness, new[] { request });

            var succeededProperty = resultType.GetProperty("Succeeded");
            var errorReasonProperty = resultType.GetProperty("ErrorReason");
            var finalBodyPositionsProperty = resultType.GetProperty("FinalBodyPositions");
            var stateFramesProperty = resultType.GetProperty("StateFrames");
            var allocatedBytesProperty = resultType.GetProperty("ManagedBytesAllocatedInLoop");

            Assert.That(succeededProperty, Is.Not.Null, "SimulationRunResult must expose bool Succeeded.");
            Assert.That(errorReasonProperty, Is.Not.Null, "SimulationRunResult must expose string ErrorReason.");
            Assert.That(
                finalBodyPositionsProperty,
                Is.Not.Null,
                "SimulationRunResult must expose Vector3[] FinalBodyPositions.");
            Assert.That(
                stateFramesProperty,
                Is.Not.Null,
                "SimulationRunResult must expose the preallocated 14-float state frames.");
            Assert.That(
                allocatedBytesProperty,
                Is.Not.Null,
                "SimulationRunResult must report managed bytes allocated inside the step loop.");
            var errorReason = errorReasonProperty.GetValue(result) as string;
            Assert.That(
                succeededProperty.GetValue(result),
                Is.EqualTo(true),
                "A valid one-body simulation must report Succeeded=true. ErrorReason: " + errorReason);
            Assert.That(
                errorReason,
                Is.Empty,
                "A successful one-body simulation must return an empty ErrorReason.");

            var finalBodyPositions = finalBodyPositionsProperty.GetValue(result) as Vector3[];
            Assert.That(
                finalBodyPositions,
                Is.Not.Null,
                "FinalBodyPositions must be a Vector3 array.");
            Assert.That(
                finalBodyPositions,
                Has.Length.EqualTo(1),
                "The result must contain exactly one final position for the one requested body.");
            Assert.That(
                finalBodyPositions[0].y,
                Is.LessThan(2f),
                "The dynamic cube must fall below its initial y position after exactly 10 fixed steps.");

            var stateFrames = stateFramesProperty.GetValue(result) as float[];
            Assert.That(
                stateFrames,
                Has.Length.EqualTo(10 * 1 * 14),
                "Ten steps for one body must produce ten 14-float state frames.");
            const int finalFrameOffset = 9 * 14;
            Assert.That(
                stateFrames[finalFrameOffset],
                Is.EqualTo(finalBodyPositions[0].x).Within(1e-6f),
                "State-frame position X must match FinalBodyPositions.");
            Assert.That(
                stateFrames[finalFrameOffset + 1],
                Is.EqualTo(finalBodyPositions[0].y).Within(1e-6f),
                "State-frame position Y must match FinalBodyPositions.");
            Assert.That(
                stateFrames[finalFrameOffset + 2],
                Is.EqualTo(finalBodyPositions[0].z).Within(1e-6f),
                "State-frame position Z must match FinalBodyPositions.");
            Assert.That(stateFrames[finalFrameOffset + 3], Is.EqualTo(0f).Within(1e-6f));
            Assert.That(stateFrames[finalFrameOffset + 4], Is.EqualTo(0f).Within(1e-6f));
            Assert.That(stateFrames[finalFrameOffset + 5], Is.EqualTo(0f).Within(1e-6f));
            Assert.That(stateFrames[finalFrameOffset + 6], Is.EqualTo(1f).Within(1e-6f));
            Assert.That(stateFrames[finalFrameOffset + 7], Is.EqualTo(0f).Within(1e-6f));
            Assert.That(
                stateFrames[finalFrameOffset + 8],
                Is.EqualTo(Physics.gravity.y * 10f * 0.02f).Within(1e-6f),
                "State-frame linear velocity Y must include all ten gravity steps.");
            Assert.That(stateFrames[finalFrameOffset + 9], Is.EqualTo(0f).Within(1e-6f));
            Assert.That(stateFrames[finalFrameOffset + 10], Is.EqualTo(0f).Within(1e-6f));
            Assert.That(stateFrames[finalFrameOffset + 11], Is.EqualTo(0f).Within(1e-6f));
            Assert.That(stateFrames[finalFrameOffset + 12], Is.EqualTo(0f).Within(1e-6f));
            Assert.That(
                stateFrames[finalFrameOffset + 13],
                Is.EqualTo(0f),
                "The falling body must record sleeping as 0f at stride offset 13.");
            Assert.That(
                allocatedBytesProperty.GetValue(result),
                Is.EqualTo(0L),
                "The explicit physics step loop must allocate zero managed bytes.");

            for (var frame = 0; frame < 10 && SceneManager.sceneCount != initialSceneCount; frame++)
            {
                yield return null;
            }

            Assert.That(
                SceneManager.sceneCount,
                Is.EqualTo(initialSceneCount),
                "SimulationHarness.Run must unload its fresh local physics scene within 10 editor frames.");

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RunPublishesAllBodyArraysInStableIdOrder()
        {
            yield return new EnterPlayMode();

            var bodyType = Type.GetType("BugCam.Core.SimulationBodyDefinition, BugCam.Core");
            var perturbationType = Type.GetType("BugCam.Core.SimulationPerturbation, BugCam.Core");
            var requestType = Type.GetType("BugCam.Core.SimulationRequest, BugCam.Core");
            var resultType = Type.GetType("BugCam.Core.SimulationRunResult, BugCam.Core");
            var harnessType = Type.GetType("BugCam.Core.SimulationHarness, BugCam.Core");
            var bodyConstructor = bodyType.GetConstructor(new[]
            {
                typeof(int),
                typeof(Vector3),
                typeof(Quaternion),
                typeof(Vector3),
                typeof(float)
            });
            var requestConstructor = requestType.GetConstructor(new[]
            {
                bodyType.MakeArrayType(),
                typeof(int),
                perturbationType
            });
            var runMethod = harnessType.GetMethod("Run", new[] { requestType });

            var bodies = Array.CreateInstance(bodyType, 2);
            bodies.SetValue(bodyConstructor.Invoke(new object[]
            {
                9,
                new Vector3(9f, 4f, 0f),
                Quaternion.identity,
                Vector3.one,
                1f
            }), 0);
            bodies.SetValue(bodyConstructor.Invoke(new object[]
            {
                3,
                new Vector3(3f, 2f, 0f),
                Quaternion.identity,
                Vector3.one,
                1f
            }), 1);

            var request = requestConstructor.Invoke(new[]
            {
                bodies,
                (object)1,
                Activator.CreateInstance(perturbationType)
            });
            var initialSceneCount = SceneManager.sceneCount;
            var result = runMethod.Invoke(Activator.CreateInstance(harnessType), new[] { request });

            Assert.That(
                resultType.GetProperty("Succeeded")?.GetValue(result),
                Is.EqualTo(true),
                "The reversed two-body request must run successfully.");

            var stableBodyIdsProperty = resultType.GetProperty("StableBodyIds");
            Assert.That(
                stableBodyIdsProperty,
                Is.Not.Null,
                "SimulationRunResult must publish the StableBodyIds that index its result arrays.");
            var stableBodyIds = stableBodyIdsProperty.GetValue(result) as int[];
            Assert.That(stableBodyIds, Is.EqualTo(new[] { 3, 9 }));

            var finalBodyPositions =
                resultType.GetProperty("FinalBodyPositions")?.GetValue(result) as Vector3[];
            Assert.That(finalBodyPositions, Has.Length.EqualTo(2));
            Assert.That(finalBodyPositions[0].x, Is.EqualTo(3f).Within(1e-6f));
            Assert.That(finalBodyPositions[1].x, Is.EqualTo(9f).Within(1e-6f));

            var stateFrames = resultType.GetProperty("StateFrames")?.GetValue(result) as float[];
            Assert.That(stateFrames, Has.Length.EqualTo(2 * 14));
            Assert.That(stateFrames[0], Is.EqualTo(3f).Within(1e-6f));
            Assert.That(stateFrames[14], Is.EqualTo(9f).Within(1e-6f));

            for (var frame = 0; frame < 10 && SceneManager.sceneCount != initialSceneCount; frame++)
            {
                yield return null;
            }

            Assert.That(SceneManager.sceneCount, Is.EqualTo(initialSceneCount));
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator DeterminismProbeRejectsMismatchedStableBodyIdSets()
        {
            yield return new EnterPlayMode();

            var bodyType = Type.GetType("BugCam.Core.SimulationBodyDefinition, BugCam.Core");
            var perturbationType = Type.GetType("BugCam.Core.SimulationPerturbation, BugCam.Core");
            var requestType = Type.GetType("BugCam.Core.SimulationRequest, BugCam.Core");
            var probeType = Type.GetType("BugCam.Core.DeterminismProbe, BugCam.Core");
            var probeResultType = Type.GetType("BugCam.Core.DeterminismProbeResult, BugCam.Core");
            var threadingModeType = Type.GetType(
                "BugCam.Core.SimulationThreadingMode, BugCam.Core");
            var bodyConstructor = bodyType.GetConstructor(new[]
            {
                typeof(int),
                typeof(Vector3),
                typeof(Quaternion),
                typeof(Vector3),
                typeof(float)
            });
            var requestConstructor = requestType.GetConstructor(new[]
            {
                bodyType.MakeArrayType(),
                typeof(int),
                perturbationType
            });
            var runMethod = probeType.GetMethod(
                "Run",
                new[] { requestType, requestType, threadingModeType });

            var baselineBodies = Array.CreateInstance(bodyType, 2);
            baselineBodies.SetValue(bodyConstructor.Invoke(new object[]
            {
                3, new Vector3(3f, 2f, 0f), Quaternion.identity, Vector3.one, 1f
            }), 0);
            baselineBodies.SetValue(bodyConstructor.Invoke(new object[]
            {
                9, new Vector3(9f, 2f, 0f), Quaternion.identity, Vector3.one, 1f
            }), 1);
            var mismatchedBodies = Array.CreateInstance(bodyType, 2);
            mismatchedBodies.SetValue(bodyConstructor.Invoke(new object[]
            {
                3, new Vector3(3f, 2f, 0f), Quaternion.identity, Vector3.one, 1f
            }), 0);
            mismatchedBodies.SetValue(bodyConstructor.Invoke(new object[]
            {
                7, new Vector3(9f, 2f, 0f), Quaternion.identity, Vector3.one, 1f
            }), 1);

            var noPerturbation = Activator.CreateInstance(perturbationType);
            var baselineRequest = requestConstructor.Invoke(new[]
            {
                baselineBodies, (object)1, noPerturbation
            });
            var mismatchedRequest = requestConstructor.Invoke(new[]
            {
                mismatchedBodies, (object)1, noPerturbation
            });
            var result = runMethod.Invoke(
                Activator.CreateInstance(probeType),
                new[]
                {
                    baselineRequest,
                    mismatchedRequest,
                    Enum.Parse(threadingModeType, "MultiThreaded")
                });

            Assert.That(
                probeResultType.GetProperty("Succeeded")?.GetValue(result),
                Is.EqualTo(false),
                "The probe must reject A and B when their StableBodyIds do not match.");
            Assert.That(
                probeResultType.GetProperty("ErrorReason")?.GetValue(result),
                Does.Contain("StableBodyIds"),
                "The failure must identify StableBodyIds as the incompatible comparison dimensions.");

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RunRejectsNonFiniteBodyTransformStates()
        {
            yield return new EnterPlayMode();

            var bodyType = Type.GetType("BugCam.Core.SimulationBodyDefinition, BugCam.Core");
            var perturbationType = Type.GetType("BugCam.Core.SimulationPerturbation, BugCam.Core");
            var requestType = Type.GetType("BugCam.Core.SimulationRequest, BugCam.Core");
            var resultType = Type.GetType("BugCam.Core.SimulationRunResult, BugCam.Core");
            var harnessType = Type.GetType("BugCam.Core.SimulationHarness, BugCam.Core");
            var bodyConstructor = bodyType.GetConstructor(new[]
            {
                typeof(int),
                typeof(Vector3),
                typeof(Quaternion),
                typeof(Vector3),
                typeof(float)
            });
            var requestConstructor = requestType.GetConstructor(new[]
            {
                bodyType.MakeArrayType(),
                typeof(int),
                perturbationType
            });
            var runMethod = harnessType.GetMethod("Run", new[] { requestType });
            var noPerturbation = Activator.CreateInstance(perturbationType);

            var nonFinitePositionBodies = Array.CreateInstance(bodyType, 1);
            nonFinitePositionBodies.SetValue(bodyConstructor.Invoke(new object[]
            {
                3,
                new Vector3(float.NaN, 2f, 0f),
                Quaternion.identity,
                Vector3.one,
                1f
            }), 0);
            var nonFiniteRotationBodies = Array.CreateInstance(bodyType, 1);
            nonFiniteRotationBodies.SetValue(bodyConstructor.Invoke(new object[]
            {
                3,
                new Vector3(0f, 2f, 0f),
                new Quaternion(0f, 0f, float.PositiveInfinity, 1f),
                Vector3.one,
                1f
            }), 0);

            var positionResult = runMethod.Invoke(
                Activator.CreateInstance(harnessType),
                new[]
                {
                    requestConstructor.Invoke(new[]
                    {
                        nonFinitePositionBodies, (object)1, noPerturbation
                    })
                });
            var rotationResult = runMethod.Invoke(
                Activator.CreateInstance(harnessType),
                new[]
                {
                    requestConstructor.Invoke(new[]
                    {
                        nonFiniteRotationBodies, (object)1, noPerturbation
                    })
                });

            Assert.That(resultType.GetProperty("Succeeded")?.GetValue(positionResult), Is.EqualTo(false));
            Assert.That(resultType.GetProperty("Succeeded")?.GetValue(rotationResult), Is.EqualTo(false));
            Assert.That(
                resultType.GetProperty("ErrorReason")?.GetValue(positionResult),
                Does.Contain("finite"),
                "NaN position must be rejected before entering the simulation loop.");
            Assert.That(
                resultType.GetProperty("ErrorReason")?.GetValue(rotationResult),
                Does.Contain("finite"),
                "Infinity rotation must be rejected before entering the simulation loop.");

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RunAppliesInitialLinearVelocityToProjectile()
        {
            yield return new EnterPlayMode();

            var bodyType = Type.GetType("BugCam.Core.SimulationBodyDefinition, BugCam.Core");
            var perturbationType = Type.GetType("BugCam.Core.SimulationPerturbation, BugCam.Core");
            var requestType = Type.GetType("BugCam.Core.SimulationRequest, BugCam.Core");
            var resultType = Type.GetType("BugCam.Core.SimulationRunResult, BugCam.Core");
            var harnessType = Type.GetType("BugCam.Core.SimulationHarness, BugCam.Core");
            var bodyConstructor = bodyType.GetConstructor(new[]
            {
                typeof(int),
                typeof(Vector3),
                typeof(Quaternion),
                typeof(Vector3),
                typeof(float),
                typeof(Vector3)
            });
            Assert.That(
                bodyConstructor,
                Is.Not.Null,
                "A projectile body must accept a recorded initial linear velocity.");

            var body = bodyConstructor.Invoke(new object[]
            {
                49,
                new Vector3(0f, 10f, 0f),
                Quaternion.identity,
                Vector3.one,
                2f,
                new Vector3(5f, 0f, 0f)
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
                (object)1,
                Activator.CreateInstance(perturbationType)
            });
            var result = harnessType.GetMethod("Run", new[] { requestType }).Invoke(
                Activator.CreateInstance(harnessType),
                new[] { request });

            Assert.That(resultType.GetProperty("Succeeded")?.GetValue(result), Is.EqualTo(true));
            var stateFrames = resultType.GetProperty("StateFrames")?.GetValue(result) as float[];
            Assert.That(stateFrames[0], Is.EqualTo(0.1f).Within(1e-6f));
            Assert.That(stateFrames[7], Is.EqualTo(5f).Within(1e-6f));

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RunAppliesPerturbationWithoutCrossRunLeakage()
        {
            yield return new EnterPlayMode();

            var bodyType = Type.GetType("BugCam.Core.SimulationBodyDefinition, BugCam.Core");
            var perturbationType = Type.GetType("BugCam.Core.SimulationPerturbation, BugCam.Core");
            var requestType = Type.GetType("BugCam.Core.SimulationRequest, BugCam.Core");
            var resultType = Type.GetType("BugCam.Core.SimulationRunResult, BugCam.Core");
            var harnessType = Type.GetType("BugCam.Core.SimulationHarness, BugCam.Core");

            var perturbationConstructor = perturbationType.GetConstructor(new[]
            {
                typeof(int),
                typeof(Vector3),
                typeof(float)
            });
            Assert.That(
                perturbationConstructor,
                Is.Not.Null,
                "SimulationPerturbation must expose the (int, Vector3, float) constructor.");

            var bodyConstructor = bodyType.GetConstructor(new[]
            {
                typeof(int),
                typeof(Vector3),
                typeof(Quaternion),
                typeof(Vector3),
                typeof(float)
            });
            var requestConstructor = requestType.GetConstructor(new[]
            {
                bodyType.MakeArrayType(),
                typeof(int),
                perturbationType
            });
            var runMethod = harnessType.GetMethod("Run", new[] { requestType });

            var body = bodyConstructor.Invoke(new object[]
            {
                7,
                new Vector3(0f, 2f, 0f),
                Quaternion.identity,
                Vector3.one,
                1f
            });
            var bodies = Array.CreateInstance(bodyType, 1);
            bodies.SetValue(body, 0);

            const float magnitudeMetres = 0.001f;
            var baselinePerturbation = Activator.CreateInstance(perturbationType);
            var appliedPerturbation = perturbationConstructor.Invoke(new object[]
            {
                7,
                Vector3.right,
                magnitudeMetres
            });
            var baselineRequest = requestConstructor.Invoke(
                new[] { bodies, (object)10, baselinePerturbation });
            var perturbedRequest = requestConstructor.Invoke(
                new[] { bodies, (object)10, appliedPerturbation });

            var initialSceneCount = SceneManager.sceneCount;
            var harness = Activator.CreateInstance(harnessType);
            var resultA = runMethod.Invoke(harness, new[] { baselineRequest });
            var resultB = runMethod.Invoke(harness, new[] { perturbedRequest });
            var resultAPrime = runMethod.Invoke(harness, new[] { baselineRequest });

            var succeededProperty = resultType.GetProperty("Succeeded");
            var errorReasonProperty = resultType.GetProperty("ErrorReason");
            var finalBodyPositionsProperty = resultType.GetProperty("FinalBodyPositions");
            var appliedPerturbationProperty = resultType.GetProperty("AppliedPerturbation");
            Assert.That(
                appliedPerturbationProperty,
                Is.Not.Null,
                "SimulationRunResult must record the perturbation applied before the run.");

            Assert.That(
                succeededProperty.GetValue(resultA),
                Is.EqualTo(true),
                "Baseline A must succeed. ErrorReason: " + errorReasonProperty.GetValue(resultA));
            Assert.That(
                succeededProperty.GetValue(resultB),
                Is.EqualTo(true),
                "Perturbed B must succeed. ErrorReason: " + errorReasonProperty.GetValue(resultB));
            Assert.That(
                succeededProperty.GetValue(resultAPrime),
                Is.EqualTo(true),
                "Baseline A-prime must succeed. ErrorReason: " + errorReasonProperty.GetValue(resultAPrime));

            var positionA = ((Vector3[])finalBodyPositionsProperty.GetValue(resultA))[0];
            var positionB = ((Vector3[])finalBodyPositionsProperty.GetValue(resultB))[0];
            var positionAPrime = ((Vector3[])finalBodyPositionsProperty.GetValue(resultAPrime))[0];
            Assert.That(
                Vector3.Distance(positionA, positionAPrime),
                Is.LessThanOrEqualTo(1e-6f),
                "A-prime must repeat baseline A within the Block 1.1 component gate.");
            Assert.That(
                positionB.x - positionA.x,
                Is.EqualTo(magnitudeMetres).Within(1e-6f),
                "B must apply the requested X-axis position perturbation exactly once.");
            Assert.That(
                positionB.y,
                Is.EqualTo(positionA.y).Within(1e-6f),
                "An X-axis perturbation must not change the independent Y trajectory.");

            var recordedPerturbation = appliedPerturbationProperty.GetValue(resultB);
            Assert.That(
                perturbationType.GetProperty("TargetBodyId")?.GetValue(recordedPerturbation),
                Is.EqualTo(7),
                "Run metadata must record the perturbed body ID.");
            Assert.That(
                perturbationType.GetProperty("Axis")?.GetValue(recordedPerturbation),
                Is.EqualTo(Vector3.right),
                "Run metadata must record the applied axis.");
            Assert.That(
                perturbationType.GetProperty("MagnitudeMetres")?.GetValue(recordedPerturbation),
                Is.EqualTo(magnitudeMetres),
                "Run metadata must record the applied magnitude in metres.");

            for (var frame = 0; frame < 20 && SceneManager.sceneCount != initialSceneCount; frame++)
            {
                yield return null;
            }

            Assert.That(
                SceneManager.sceneCount,
                Is.EqualTo(initialSceneCount),
                "A/B/A-prime must unload all three temporary physics scenes.");

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator DeterminismProbeReportsAbaMetrics()
        {
            yield return new EnterPlayMode();

            var bodyType = Type.GetType("BugCam.Core.SimulationBodyDefinition, BugCam.Core");
            var perturbationType = Type.GetType("BugCam.Core.SimulationPerturbation, BugCam.Core");
            var requestType = Type.GetType("BugCam.Core.SimulationRequest, BugCam.Core");
            var probeType = Type.GetType("BugCam.Core.DeterminismProbe, BugCam.Core");
            var probeResultType = Type.GetType("BugCam.Core.DeterminismProbeResult, BugCam.Core");
            var threadingModeType = Type.GetType(
                "BugCam.Core.SimulationThreadingMode, BugCam.Core");
            Assert.That(probeType, Is.Not.Null, "Block 1.1 requires a Core DeterminismProbe.");
            Assert.That(
                probeResultType,
                Is.Not.Null,
                "DeterminismProbe must return a readonly Core result.");
            Assert.That(
                threadingModeType,
                Is.Not.Null,
                "Threading mode metadata must use a closed Core enum, not arbitrary text.");

            var bodyConstructor = bodyType.GetConstructor(new[]
            {
                typeof(int),
                typeof(Vector3),
                typeof(Quaternion),
                typeof(Vector3),
                typeof(float)
            });
            var perturbationConstructor = perturbationType.GetConstructor(new[]
            {
                typeof(int),
                typeof(Vector3),
                typeof(float)
            });
            var requestConstructor = requestType.GetConstructor(new[]
            {
                bodyType.MakeArrayType(),
                typeof(int),
                perturbationType
            });

            var body = bodyConstructor.Invoke(new object[]
            {
                7,
                new Vector3(0f, 2f, 0f),
                Quaternion.identity,
                Vector3.one,
                1f
            });
            var bodies = Array.CreateInstance(bodyType, 1);
            bodies.SetValue(body, 0);
            var baselineRequest = requestConstructor.Invoke(new[]
            {
                bodies,
                (object)10,
                Activator.CreateInstance(perturbationType)
            });
            var perturbedRequest = requestConstructor.Invoke(new[]
            {
                bodies,
                (object)10,
                perturbationConstructor.Invoke(new object[] { 7, Vector3.right, 0.001f })
            });

            var runMethod = probeType.GetMethod(
                "Run",
                new[] { requestType, requestType, threadingModeType });
            Assert.That(
                runMethod,
                Is.Not.Null,
                "DeterminismProbe.Run must accept baseline, perturbed, and threading mode inputs.");

            var initialSceneCount = SceneManager.sceneCount;
            var multiThreadedMode = Enum.Parse(threadingModeType, "MultiThreaded");
            var result = runMethod.Invoke(
                Activator.CreateInstance(probeType),
                new[] { baselineRequest, perturbedRequest, multiThreadedMode });

            Assert.That(
                probeResultType.GetProperty("Succeeded")?.GetValue(result),
                Is.EqualTo(true),
                "A/B/A-prime probe must complete successfully: " +
                probeResultType.GetProperty("ErrorReason")?.GetValue(result));
            Assert.That(
                probeResultType.GetProperty("RepeatBitwiseEqual")?.GetValue(result),
                Is.EqualTo(true),
                "Identical A and A-prime traces must report bitwise equality separately from the gate.");
            Assert.That(
                probeResultType.GetProperty("RepeatMaxComponentDelta")?.GetValue(result),
                Is.EqualTo(0f),
                "A and A-prime must report their measured maximum component delta.");
            Assert.That(
                probeResultType.GetProperty("RepeatWithinGate")?.GetValue(result),
                Is.EqualTo(true),
                "The 1e-6 repeatability gate must be exposed separately from bitwise equality.");
            Assert.That(
                probeResultType.GetProperty("PerturbedFirstDivergingStep")?.GetValue(result),
                Is.EqualTo(0),
                "The initial X perturbation must first appear in state frame zero.");
            Assert.That(
                probeResultType.GetProperty("PerturbedFirstDivergingBody")?.GetValue(result),
                Is.EqualTo(7),
                "The first diverging body must be reported by stable ID.");
            Assert.That(
                probeResultType.GetProperty("ManagedBytesAllocatedInLoop")?.GetValue(result),
                Is.EqualTo(0L),
                "The probe must report the maximum observed allocation count from A/B/A-prime.");
            Assert.That(
                probeResultType.GetProperty("SimulationThreadingMode")?.GetValue(result),
                Is.EqualTo(multiThreadedMode),
                "The measured threading mode must be carried into probe metadata.");

            for (var frame = 0; frame < 20 && SceneManager.sceneCount != initialSceneCount; frame++)
            {
                yield return null;
            }

            Assert.That(
                SceneManager.sceneCount,
                Is.EqualTo(initialSceneCount),
                "The determinism probe must unload all A/B/A-prime scenes.");

            yield return new ExitPlayMode();
        }
    }
}
