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
    /// Block 2.2.1 A2 harness contracts for captured scenes: Sphere bodies simulate
    /// (Size = diameter), and a non-null StaticColliders array replaces the legacy
    /// procedural ground exactly (empty array ⇒ no ground at all). The null-statics
    /// tower path is pinned by the existing tower determinism suites.
    /// </summary>
    public sealed class SceneCapturePlayModeTests
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

        private static object SphereBody(int stableId, Vector3 position, float diameter)
        {
            var bodyType = CoreType("SimulationBodyDefinition");
            var shapeType = CoreType("SimulationColliderShape");
            var constructor = bodyType.GetConstructor(new[]
            {
                typeof(int),
                typeof(Vector3),
                typeof(Quaternion),
                typeof(Vector3),
                typeof(float),
                typeof(Vector3),
                shapeType
            });
            Assert.That(constructor, Is.Not.Null,
                "SimulationBodyDefinition must expose the 7-argument shape constructor.");
            return constructor.Invoke(new object[]
            {
                stableId,
                position,
                Quaternion.identity,
                new Vector3(diameter, diameter, diameter),
                1f,
                Vector3.zero,
                Enum.Parse(shapeType, "Sphere")
            });
        }

        private static object BoxBody(int stableId, Vector3 position)
        {
            var bodyType = CoreType("SimulationBodyDefinition");
            var constructor = bodyType.GetConstructor(new[]
            {
                typeof(int),
                typeof(Vector3),
                typeof(Quaternion),
                typeof(Vector3),
                typeof(float)
            });
            return constructor.Invoke(new object[]
            {
                stableId,
                position,
                Quaternion.identity,
                Vector3.one,
                1f
            });
        }

        private static object StaticBox(Vector3 position, Vector3 size)
        {
            var staticType = CoreType("SimulationStaticColliderDefinition");
            var shapeType = CoreType("SimulationColliderShape");
            return Activator.CreateInstance(
                staticType,
                position,
                Quaternion.identity,
                size,
                Enum.Parse(shapeType, "Box"));
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
            Assert.That(requestConstructor, Is.Not.Null,
                "SimulationRequest must expose the 4-argument statics constructor.");

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

        private static float FinalY(object result)
        {
            var positions = Prop<Vector3[]>(result, "FinalBodyPositions");
            Assert.That(positions, Has.Length.EqualTo(1));
            return positions[0].y;
        }

        [UnityTest]
        public IEnumerator SphereBodyRestsOnCapturedStaticGround()
        {
            var initialSceneCount = SceneManager.sceneCount;

            var bodyType = CoreType("SimulationBodyDefinition");
            var bodies = Array.CreateInstance(bodyType, 1);
            bodies.SetValue(SphereBody(1, new Vector3(0f, 2f, 0f), 1f), 0);

            var staticType = CoreType("SimulationStaticColliderDefinition");
            var statics = Array.CreateInstance(staticType, 1);
            statics.SetValue(StaticBox(new Vector3(0f, -0.5f, 0f), new Vector3(20f, 1f, 20f)), 0);

            var result = RunHarness(bodies, statics, 200);
            Assert.That(Prop<bool>(result, "Succeeded"), Is.True,
                Prop<string>(result, "ErrorReason"));

            // Sphere of diameter 1 resting on the ground top (y=0) ⇒ centre y ≈ 0.5.
            Assert.That(FinalY(result), Is.EqualTo(0.5f).Within(0.05f),
                "Sphere must rest on the captured static ground.");

            yield return WaitCleanup(initialSceneCount);
        }

        [UnityTest]
        public IEnumerator EmptyStaticsArraySuppressesLegacyGround()
        {
            var initialSceneCount = SceneManager.sceneCount;

            var bodyType = CoreType("SimulationBodyDefinition");
            var bodies = Array.CreateInstance(bodyType, 1);
            bodies.SetValue(BoxBody(1, new Vector3(0f, 0.5f, 0f)), 0);

            var staticType = CoreType("SimulationStaticColliderDefinition");
            var statics = Array.CreateInstance(staticType, 0);

            var result = RunHarness(bodies, statics, 200);
            Assert.That(Prop<bool>(result, "Succeeded"), Is.True,
                Prop<string>(result, "ErrorReason"));

            // No implicit ground: the body must be in free fall far below the origin.
            Assert.That(FinalY(result), Is.LessThan(-5f),
                "A non-null empty statics array must not create the legacy ground.");

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
