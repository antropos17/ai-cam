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
    /// Block 2.2.2 harness contracts for mesh shapes in the local PhysicsScene
    /// (docs/CONTRACT-2.2.2.md): a convex mesh body simulates over a non-convex mesh
    /// static; the simulation point verifies the structural fingerprint bit-identically
    /// and fails closed with SCENE_MESH_RESOLVE_FAILED on mismatch; a mesh definition
    /// without a capture-provided reference fails closed per the Amendment 2026-08-04
    /// run-path invariant. Uses the built-in Cube.fbx mesh — a real resolvable asset —
    /// so the REAL editor resolve provider is exercised end to end.
    /// </summary>
    public sealed class MeshSimulationPlayModeTests
    {
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

        private static Mesh BuiltinCube()
        {
            var mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            Assert.That(mesh, Is.Not.Null, "Built-in Cube.fbx mesh must exist.");
            return mesh;
        }

        private static void DescribeMesh(Mesh mesh, out string assetGuid, out long localFileId)
        {
            var provider = CoreType("SceneMeshResolve")
                .GetProperty("Provider", BindingFlags.Public | BindingFlags.Static)
                .GetValue(null);
            Assert.That(provider, Is.Not.Null,
                "The editor resolve provider must be installed in the editor.");
            var describe = provider.GetType().GetMethod("TryDescribeMeshAsset");
            var args = new object[] { mesh, null, null, null };
            Assert.That((bool)describe.Invoke(provider, args), Is.True,
                "Built-in cube must be describable: " + args[3]);
            assetGuid = (string)args[1];
            localFileId = (long)args[2];
        }

        private static object MeshReference(
            Mesh mesh,
            string assetGuid,
            long localFileId,
            bool convex,
            int vertexCountOverride = -1)
        {
            return Activator.CreateInstance(
                CoreType("SimulationMeshReference"),
                assetGuid,
                localFileId,
                mesh.name,
                "hash-verified-at-capture-point-not-here",
                convex,
                vertexCountOverride >= 0 ? vertexCountOverride : mesh.vertexCount,
                mesh.subMeshCount,
                mesh.bounds.center,
                mesh.bounds.size);
        }

        private static object MeshBody(
            int stableId,
            Vector3 position,
            object meshReference,
            Vector3 size,
            Vector3 fullScale)
        {
            var bodyType = CoreType("SimulationBodyDefinition");
            var shapeType = CoreType("SimulationColliderShape");
            var referenceType = CoreType("SimulationMeshReference");
            var constructor = bodyType.GetConstructor(new[]
            {
                typeof(int),
                typeof(Vector3),
                typeof(Quaternion),
                typeof(Vector3),
                typeof(float),
                typeof(Vector3),
                shapeType,
                referenceType,
                typeof(Vector3)
            });
            Assert.That(constructor, Is.Not.Null,
                "SimulationBodyDefinition must expose the 9-argument mesh constructor.");
            return constructor.Invoke(new[]
            {
                (object)stableId,
                position,
                Quaternion.identity,
                size,
                1f,
                Vector3.zero,
                Enum.Parse(shapeType, "Mesh"),
                meshReference,
                fullScale
            });
        }

        private static object MeshStatic(
            Vector3 position,
            object meshReference,
            Vector3 size,
            Vector3 fullScale)
        {
            var staticType = CoreType("SimulationStaticColliderDefinition");
            var shapeType = CoreType("SimulationColliderShape");
            var referenceType = CoreType("SimulationMeshReference");
            var constructor = staticType.GetConstructor(new[]
            {
                typeof(Vector3),
                typeof(Quaternion),
                typeof(Vector3),
                shapeType,
                referenceType,
                typeof(Vector3)
            });
            Assert.That(constructor, Is.Not.Null,
                "SimulationStaticColliderDefinition must expose the 6-argument mesh " +
                "constructor.");
            return constructor.Invoke(new[]
            {
                (object)position,
                Quaternion.identity,
                size,
                Enum.Parse(shapeType, "Mesh"),
                meshReference,
                fullScale
            });
        }

        private static object RunHarness(object bodiesArray, object staticsArray, int steps)
        {
            var bodyType = CoreType("SimulationBodyDefinition");
            var staticType = CoreType("SimulationStaticColliderDefinition");
            var requestType = CoreType("SimulationRequest");
            var perturbationType = CoreType("SimulationPerturbation");
            var harnessType = CoreType("SimulationHarness");

            var requestConstructor = requestType.GetConstructor(new[]
            {
                bodyType.MakeArrayType(),
                typeof(int),
                perturbationType,
                staticType.MakeArrayType()
            });
            var request = requestConstructor.Invoke(new[]
            {
                bodiesArray,
                steps,
                Activator.CreateInstance(perturbationType),
                staticsArray
            });
            var harness = Activator.CreateInstance(harnessType);
            return harnessType.GetMethod("Run", new[] { requestType })
                .Invoke(harness, new[] { request });
        }

        [UnityTest]
        public IEnumerator ConvexMeshBodyRestsOnNonConvexMeshStatic()
        {
            var initialSceneCount = SceneManager.sceneCount;
            var mesh = BuiltinCube();
            DescribeMesh(mesh, out var assetGuid, out var localFileId);

            var bodyType = CoreType("SimulationBodyDefinition");
            var bodies = Array.CreateInstance(bodyType, 1);
            bodies.SetValue(
                MeshBody(
                    1,
                    new Vector3(0f, 2f, 0f),
                    MeshReference(mesh, assetGuid, localFileId, convex: true),
                    size: Vector3.one,
                    fullScale: Vector3.one),
                0);

            var staticType = CoreType("SimulationStaticColliderDefinition");
            var statics = Array.CreateInstance(staticType, 1);
            statics.SetValue(
                MeshStatic(
                    new Vector3(0f, -0.5f, 0f),
                    MeshReference(mesh, assetGuid, localFileId, convex: false),
                    size: new Vector3(10f, 1f, 10f),
                    fullScale: new Vector3(10f, 1f, 10f)),
                0);

            var result = RunHarness(bodies, statics, 200);
            Assert.That(Prop<bool>(result, "Succeeded"), Is.True,
                Prop<string>(result, "ErrorReason"));

            // Unit cube resting on the static mesh top (y=0) ⇒ centre y ≈ 0.5.
            var positions = Prop<Vector3[]>(result, "FinalBodyPositions");
            Assert.That(positions[0].y, Is.EqualTo(0.5f).Within(0.05f),
                "Convex mesh body must rest on the non-convex mesh static.");

            yield return WaitCleanup(initialSceneCount);
        }

        [UnityTest]
        public IEnumerator FingerprintMismatchFailsClosedWithMeshResolveCode()
        {
            var initialSceneCount = SceneManager.sceneCount;
            var mesh = BuiltinCube();
            DescribeMesh(mesh, out var assetGuid, out var localFileId);

            var bodyType = CoreType("SimulationBodyDefinition");
            var bodies = Array.CreateInstance(bodyType, 1);
            bodies.SetValue(
                MeshBody(
                    1,
                    new Vector3(0f, 2f, 0f),
                    MeshReference(
                        mesh, assetGuid, localFileId, convex: true,
                        vertexCountOverride: mesh.vertexCount + 1),
                    size: Vector3.one,
                    fullScale: Vector3.one),
                0);

            var staticType = CoreType("SimulationStaticColliderDefinition");
            var statics = Array.CreateInstance(staticType, 0);

            var result = RunHarness(bodies, statics, 10);
            Assert.That(Prop<bool>(result, "Succeeded"), Is.False,
                "A tampered structural fingerprint must fail the run closed.");
            var reason = Prop<string>(result, "ErrorReason");
            Assert.That(reason, Does.StartWith("SCENE_MESH_RESOLVE_FAILED: "));
            Assert.That(reason, Does.Contain(
                "меш-ассет изменился с момента захвата: структурный отпечаток не совпадает"));

            yield return WaitCleanup(initialSceneCount);
        }

        [UnityTest]
        public IEnumerator MeshDefinitionWithoutCaptureReferenceFailsClosedPerInvariant()
        {
            var initialSceneCount = SceneManager.sceneCount;

            var bodyType = CoreType("SimulationBodyDefinition");
            var shapeType = CoreType("SimulationColliderShape");
            var referenceType = CoreType("SimulationMeshReference");
            var bodies = Array.CreateInstance(bodyType, 1);
            bodies.SetValue(
                bodyType.GetConstructor(new[]
                {
                    typeof(int), typeof(Vector3), typeof(Quaternion), typeof(Vector3),
                    typeof(float), typeof(Vector3), shapeType, referenceType,
                    typeof(Vector3)
                }).Invoke(new[]
                {
                    (object)1,
                    new Vector3(0f, 2f, 0f),
                    Quaternion.identity,
                    Vector3.one,
                    1f,
                    Vector3.zero,
                    Enum.Parse(shapeType, "Mesh"),
                    Activator.CreateInstance(referenceType),
                    Vector3.one
                }),
                0);

            var staticType = CoreType("SimulationStaticColliderDefinition");
            var statics = Array.CreateInstance(staticType, 0);

            var result = RunHarness(bodies, statics, 10);
            Assert.That(Prop<bool>(result, "Succeeded"), Is.False,
                "A mesh definition without a capture-provided reference must fail closed.");
            var reason = Prop<string>(result, "ErrorReason");
            Assert.That(reason, Does.StartWith("SCENE_MESH_RESOLVE_FAILED: "));
            Assert.That(reason, Does.Contain("меш-ссылка отсутствует"));
            Assert.That(reason, Does.Contain("инвариант Поправки 2026-08-04"));

            yield return WaitCleanup(initialSceneCount);
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
