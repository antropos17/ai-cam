#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BugCam.Tests
{
    /// <summary>
    /// Block 2.2.1 A2 scene-capture contract (docs/CONTRACT-2.2.1.md). Zero-ref
    /// reflection style over BugCam.Core.SceneCapture; every test builds its fixture in
    /// an isolated additive empty scene so the test-runner scene never leaks into a
    /// capture.
    /// </summary>
    public sealed class SceneCaptureTests
    {
        private Scene _scene;

        [SetUp]
        public void SetUp()
        {
            // Preview scenes carry no "untitled scene unsaved" restriction (additive
            // NewScene refuses when the runner's active scene is untitled, both in a
            // live editor and in batchmode).
            _scene = EditorSceneManager.NewPreviewScene();
        }

        [TearDown]
        public void TearDown()
        {
            if (_scene.IsValid())
            {
                EditorSceneManager.ClosePreviewScene(_scene);
            }
        }

        // --- Reflection plumbing (BugCam.Tests has zero asmdef references) ---

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

        private object Capture()
        {
            var method = CoreType("SceneCapture").GetMethod(
                "Capture",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "SceneCapture.Capture must exist.");
            return method.Invoke(null, new object[] { _scene });
        }

        private GameObject Create(string name, Vector3 position)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, _scene);
            go.transform.position = position;
            return go;
        }

        private GameObject CreateDynamicBox(string name, Vector3 position, float mass = 1f)
        {
            var go = Create(name, position);
            go.AddComponent<BoxCollider>();
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            return go;
        }

        private GameObject CreateStaticGround()
        {
            var go = Create("Ground", new Vector3(0f, -0.5f, 0f));
            go.transform.localScale = new Vector3(20f, 1f, 20f);
            go.AddComponent<BoxCollider>();
            return go;
        }

        private static List<object> Records(object capture)
        {
            var records = new List<object>();
            foreach (var record in Prop<Array>(capture, "Objects"))
            {
                records.Add(record);
            }

            return records;
        }

        private static object RecordFor(object capture, string path)
        {
            foreach (var record in Records(capture))
            {
                if (Prop<string>(record, "HierarchyPath") == path)
                {
                    return record;
                }
            }

            Assert.Fail("No capture record for path " + path);
            return null;
        }

        private static string StatusName(object record)
        {
            return Prop<object>(record, "Status").ToString();
        }

        // --- Three outcomes + stable IDs + determinism ---

        [Test]
        public void CleanSceneCapturesBodiesStaticsAndPathOrderedStableIds()
        {
            CreateStaticGround();
            CreateDynamicBox("BoxBody", new Vector3(0f, 1f, 0f), mass: 2f);
            var sphereGo = Create("SphereBody", new Vector3(2f, 1f, 0f));
            sphereGo.transform.localScale = new Vector3(3f, 3f, 3f);
            sphereGo.AddComponent<SphereCollider>();
            sphereGo.AddComponent<Rigidbody>();

            var capture = Capture();
            Assert.That(Prop<bool>(capture, "Performed"), Is.True);
            Assert.That(Prop<bool>(capture, "Succeeded"), Is.True,
                Prop<string>(capture, "FailureSummary"));

            var bodies = Prop<Array>(capture, "Bodies");
            Assert.That(bodies.Length, Is.EqualTo(2));

            // Sibling order: Ground(0), BoxBody(1), SphereBody(2) ⇒ ids follow the
            // hierarchy-path order key, 1-based.
            var first = bodies.GetValue(0);
            var second = bodies.GetValue(1);
            Assert.That(Prop<int>(first, "StableId"), Is.EqualTo(1));
            Assert.That(Prop<object>(first, "Shape").ToString(), Is.EqualTo("Box"));
            Assert.That(Prop<float>(first, "Mass"), Is.EqualTo(2f));
            Assert.That(Prop<int>(second, "StableId"), Is.EqualTo(2));
            Assert.That(Prop<object>(second, "Shape").ToString(), Is.EqualTo("Sphere"));
            // Ratified: sphere Size = diameter. Unit sphere radius 0.5 × scale 3 ⇒ 3.
            Assert.That(Prop<Vector3>(second, "Size"), Is.EqualTo(new Vector3(3f, 3f, 3f)));

            Assert.That(Prop<Array>(capture, "StaticColliders").Length, Is.EqualTo(1));

            Assert.That(StatusName(RecordFor(capture, "Ground")), Is.EqualTo("CapturedStatic"));
            Assert.That(StatusName(RecordFor(capture, "BoxBody")), Is.EqualTo("CapturedDynamic"));
            Assert.That(Prop<int>(RecordFor(capture, "BoxBody"), "StableId"), Is.EqualTo(1));
            Assert.That(Prop<int>(RecordFor(capture, "SphereBody"), "StableId"), Is.EqualTo(2));

            var hash = Prop<string>(capture, "CaptureHash");
            Assert.That(hash, Has.Length.EqualTo(64), "SHA-256 hex hash expected.");
            Assert.That(Prop<string[]>(capture, "SleepingBodyWarnings"), Is.Empty,
                "Awake bodies must emit no sleeping notice.");
        }

        [Test]
        public void SleepingBodyEmitsNoticeToCaptureChannelAndManifestButNotVerdict()
        {
            CreateDynamicBox("AwakeBody", new Vector3(0f, 1f, 0f));
            var sleeper = CreateDynamicBox("Sleeper", new Vector3(3f, 1f, 0f));
            sleeper.GetComponent<Rigidbody>().Sleep();
            Assert.That(sleeper.GetComponent<Rigidbody>().IsSleeping(), Is.True,
                "Fixture precondition: Sleep() must stick in the capture scene.");

            var capture = Capture();
            Assert.That(Prop<bool>(capture, "Succeeded"), Is.True,
                Prop<string>(capture, "FailureSummary"));
            // Notice only: the body stays a captured dynamic body.
            Assert.That(Prop<Array>(capture, "Bodies").Length, Is.EqualTo(2));

            var sleeping = Prop<string[]>(capture, "SleepingBodyWarnings");
            Assert.That(sleeping, Has.Length.EqualTo(1));
            Assert.That(sleeping[0], Does.Contain("Sleeper"));
            Assert.That(sleeping[0], Does.Not.Contain("AwakeBody"));
            Assert.That(StatusName(RecordFor(capture, "Sleeper")),
                Is.EqualTo("CapturedDynamic"));
            Assert.That(Prop<string>(RecordFor(capture, "Sleeper"), "Reason"),
                Does.Contain("спит"));

            var document = CreateFailureDocumentWithCapture(capture, "sleeping-notice");
            var tempRoot = Path.Combine(
                Path.GetTempPath(),
                "BugCamSceneCaptureTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                var writerType = Type.GetType(
                    "BugCam.Evidence.GhostEvidenceWriter, BugCam.Evidence");
                var write = writerType.GetMethod(
                    "Write",
                    new[] { document.GetType(), typeof(string) }).Invoke(
                    null,
                    new object[] { document, tempRoot });
                Assert.That(Prop<bool>(write, "Succeeded"), Is.True,
                    Prop<string>(write, "ErrorReason"));

                var manifest = File.ReadAllText(
                    Path.Combine(Prop<string>(write, "RunDirectory"), "manifest.json"));
                Assert.That(manifest, Does.Contain("\"sleepingBodyWarnings\":[\""));
                Assert.That(manifest, Does.Contain("Sleeper"));

                // Adjudication: the notice never reaches the verdict channel — the
                // console report keeps only the kinematic-freeze lines.
                var reportType = Type.GetType(
                    "BugCam.Evidence.GhostEvidenceReport, BugCam.Evidence");
                var report = (string)reportType.GetMethod("Format")
                    .Invoke(null, new object[] { document });
                Assert.That(report, Does.Not.Contain("sleepingBodyWarning"));
                Assert.That(report, Does.Not.Contain("Sleeper"));
            }
            finally
            {
                Directory.Delete(tempRoot, true);
            }
        }

        [Test]
        public void CaptureIsDeterministicAcrossRepeatedCalls()
        {
            CreateStaticGround();
            CreateDynamicBox("Body", new Vector3(0f, 1f, 0f));

            var first = Capture();
            var second = Capture();
            Assert.That(Prop<bool>(first, "Succeeded"), Is.True);
            Assert.That(
                Prop<string>(second, "CaptureHash"),
                Is.EqualTo(Prop<string>(first, "CaptureHash")));
            Assert.That(
                Prop<Array>(second, "Bodies").Length,
                Is.EqualTo(Prop<Array>(first, "Bodies").Length));
        }

        [Test]
        public void ContactlessAndTriggerOnlyBodiesAreExcludedSafely()
        {
            CreateDynamicBox("RealBody", new Vector3(0f, 1f, 0f));

            var bare = Create("BareRigidbody", new Vector3(5f, 1f, 0f));
            bare.AddComponent<Rigidbody>();

            var trigger = Create("TriggerOnly", new Vector3(7f, 1f, 0f));
            trigger.AddComponent<BoxCollider>().isTrigger = true;
            trigger.AddComponent<Rigidbody>();

            var capture = Capture();
            Assert.That(Prop<bool>(capture, "Succeeded"), Is.True,
                Prop<string>(capture, "FailureSummary"));
            Assert.That(Prop<Array>(capture, "Bodies").Length, Is.EqualTo(1));
            Assert.That(StatusName(RecordFor(capture, "BareRigidbody")),
                Is.EqualTo("ExcludedSafely"));
            Assert.That(StatusName(RecordFor(capture, "TriggerOnly")),
                Is.EqualTo("ExcludedSafely"));
        }

        [Test]
        public void InactiveObjectIsExcludedSafely()
        {
            CreateDynamicBox("RealBody", new Vector3(0f, 1f, 0f));
            var inactive = CreateDynamicBox("Sleeper", new Vector3(3f, 1f, 0f));
            inactive.SetActive(false);

            var capture = Capture();
            Assert.That(Prop<bool>(capture, "Succeeded"), Is.True);
            Assert.That(Prop<Array>(capture, "Bodies").Length, Is.EqualTo(1));
            Assert.That(StatusName(RecordFor(capture, "Sleeper")),
                Is.EqualTo("ExcludedSafely"));
        }

        // --- Kinematic freeze + Animator warning ---

        [Test]
        public void KinematicBodyFreezesToStaticWithoutWarningWhenNoAnimator()
        {
            CreateDynamicBox("RealBody", new Vector3(0f, 1f, 0f));
            var kinematic = CreateDynamicBox("Platform", new Vector3(3f, 1f, 0f));
            kinematic.GetComponent<Rigidbody>().isKinematic = true;

            var capture = Capture();
            Assert.That(Prop<bool>(capture, "Succeeded"), Is.True);
            Assert.That(Prop<Array>(capture, "Bodies").Length, Is.EqualTo(1));
            Assert.That(Prop<Array>(capture, "StaticColliders").Length, Is.EqualTo(1));
            Assert.That(StatusName(RecordFor(capture, "Platform")),
                Is.EqualTo("FrozenKinematic"));
            Assert.That(Prop<string[]>(capture, "KinematicFreezeWarnings"), Is.Empty);
        }

        [Test]
        public void KinematicBodyUnderAnimatorPropagatesFreezeWarning()
        {
            CreateDynamicBox("RealBody", new Vector3(0f, 1f, 0f));

            var rig = Create("Rig", new Vector3(3f, 1f, 0f));
            rig.AddComponent<Animator>();
            var platform = new GameObject("MovingPlatform");
            platform.transform.SetParent(rig.transform, false);
            platform.AddComponent<BoxCollider>();
            platform.AddComponent<Rigidbody>().isKinematic = true;

            var capture = Capture();
            Assert.That(Prop<bool>(capture, "Succeeded"), Is.True);
            var warnings = Prop<string[]>(capture, "KinematicFreezeWarnings");
            Assert.That(warnings, Has.Length.EqualTo(1));
            Assert.That(warnings[0], Does.Contain("Rig/MovingPlatform"));
            Assert.That(warnings[0], Does.Contain("Animator"));
            Assert.That(StatusName(RecordFor(capture, "Rig/MovingPlatform")),
                Is.EqualTo("FrozenKinematic"));
        }

        // --- Fail-closed paths (per-object reasons) ---

        [Test]
        public void UnsupportedContactShapeFailsClosedWithPerObjectReason()
        {
            CreateDynamicBox("RealBody", new Vector3(0f, 1f, 0f));
            var capsule = Create("CapsuleBody", new Vector3(3f, 1f, 0f));
            capsule.AddComponent<CapsuleCollider>();
            capsule.AddComponent<Rigidbody>();

            var capture = Capture();
            Assert.That(Prop<bool>(capture, "Succeeded"), Is.False);
            Assert.That(Prop<string>(capture, "FailureSummary"), Does.Contain("CapsuleBody"));
            var record = RecordFor(capture, "CapsuleBody");
            Assert.That(StatusName(record), Is.EqualTo("Failed"));
            Assert.That(Prop<string>(record, "Reason"), Does.Contain("CapsuleCollider"));
            Assert.That(Prop<Array>(capture, "Bodies"), Is.Empty,
                "Fail-closed capture must produce no simulation input.");
        }

        [Test]
        public void StaticMeshColliderFailsClosed()
        {
            CreateDynamicBox("RealBody", new Vector3(0f, 1f, 0f));
            var mesh = Create("MeshGround", new Vector3(0f, -0.5f, 0f));
            mesh.AddComponent<MeshCollider>();

            var capture = Capture();
            Assert.That(Prop<bool>(capture, "Succeeded"), Is.False);
            Assert.That(Prop<string>(RecordFor(capture, "MeshGround"), "Reason"),
                Does.Contain("MeshCollider"));
        }

        [Test]
        public void JointFailsClosed()
        {
            CreateDynamicBox("RealBody", new Vector3(0f, 1f, 0f));
            var jointed = CreateDynamicBox("Jointed", new Vector3(3f, 1f, 0f));
            jointed.AddComponent<FixedJoint>();

            var capture = Capture();
            Assert.That(Prop<bool>(capture, "Succeeded"), Is.False);
            Assert.That(Prop<string>(RecordFor(capture, "Jointed"), "Reason"),
                Does.Contain("джойнт"));
        }

        [Test]
        public void MultipleContactCollidersOnOneBodyFailClosed()
        {
            CreateDynamicBox("RealBody", new Vector3(0f, 1f, 0f));
            var doubled = CreateDynamicBox("Doubled", new Vector3(3f, 1f, 0f));
            doubled.AddComponent<BoxCollider>();

            var capture = Capture();
            Assert.That(Prop<bool>(capture, "Succeeded"), Is.False);
            Assert.That(Prop<string>(RecordFor(capture, "Doubled"), "Reason"),
                Does.Contain("несколько"));
        }

        [Test]
        public void ChildColliderOfRigidbodyFailsClosedAsCompound()
        {
            var parent = CreateDynamicBox("Compound", new Vector3(0f, 1f, 0f));
            var child = new GameObject("Part");
            child.transform.SetParent(parent.transform, false);
            child.AddComponent<BoxCollider>();

            var capture = Capture();
            Assert.That(Prop<bool>(capture, "Succeeded"), Is.False);
            Assert.That(Prop<string>(RecordFor(capture, "Compound/Part"), "Reason"),
                Does.Contain("составные"));
        }

        [Test]
        public void ShearedTransformFailsClosed()
        {
            CreateDynamicBox("RealBody", new Vector3(0f, 1f, 0f));

            var parent = Create("Squash", new Vector3(3f, 1f, 0f));
            parent.transform.localScale = new Vector3(2f, 1f, 1f);
            var sheared = new GameObject("Sheared");
            sheared.transform.SetParent(parent.transform, false);
            sheared.transform.localRotation = Quaternion.Euler(0f, 0f, 30f);
            sheared.AddComponent<BoxCollider>();
            sheared.AddComponent<Rigidbody>();

            var capture = Capture();
            Assert.That(Prop<bool>(capture, "Succeeded"), Is.False);
            Assert.That(Prop<string>(RecordFor(capture, "Squash/Sheared"), "Reason"),
                Does.Contain("shear"));
        }

        [Test]
        public void NonDefaultDampingFailsClosed()
        {
            var body = CreateDynamicBox("Damped", new Vector3(0f, 1f, 0f));
            body.GetComponent<Rigidbody>().linearDamping = 0.5f;

            var capture = Capture();
            Assert.That(Prop<bool>(capture, "Succeeded"), Is.False);
            Assert.That(Prop<string>(RecordFor(capture, "Damped"), "Reason"),
                Does.Contain("damping"));
        }

        [Test]
        public void SceneWithoutDynamicBodiesFailsClosed()
        {
            CreateStaticGround();

            var capture = Capture();
            Assert.That(Prop<bool>(capture, "Succeeded"), Is.False);
            Assert.That(Prop<string>(capture, "FailureSummary"),
                Does.Contain("нет захватываемых динамических тел"));
        }

        [Test]
        public void BoxColliderCenterOffsetIsCapturedAtWorldCenter()
        {
            var body = CreateDynamicBox("Offset", new Vector3(1f, 2f, 3f));
            body.GetComponent<BoxCollider>().center = new Vector3(0f, 1f, 0f);

            var capture = Capture();
            Assert.That(Prop<bool>(capture, "Succeeded"), Is.True,
                Prop<string>(capture, "FailureSummary"));
            var captured = Prop<Array>(capture, "Bodies").GetValue(0);
            Assert.That(Prop<Vector3>(captured, "Position"),
                Is.EqualTo(new Vector3(1f, 3f, 3f)));
        }

        // --- Evidence propagation: manifest section + console report next to verdict ---

        [Test]
        public void ManifestCarriesSceneCaptureSectionWithMapHashAndWarnings()
        {
            CreateDynamicBox("RealBody", new Vector3(0f, 1f, 0f));
            var rig = Create("Rig", new Vector3(3f, 1f, 0f));
            rig.AddComponent<Animator>();
            var platform = new GameObject("MovingPlatform");
            platform.transform.SetParent(rig.transform, false);
            platform.AddComponent<BoxCollider>();
            platform.AddComponent<Rigidbody>().isKinematic = true;

            var capture = Capture();
            Assert.That(Prop<bool>(capture, "Succeeded"), Is.True);

            var document = CreateFailureDocumentWithCapture(capture, "scene-capture-manifest");
            var tempRoot = Path.Combine(
                Path.GetTempPath(),
                "BugCamSceneCaptureTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                var writerType = Type.GetType(
                    "BugCam.Evidence.GhostEvidenceWriter, BugCam.Evidence");
                var write = writerType.GetMethod(
                    "Write",
                    new[] { document.GetType(), typeof(string) }).Invoke(
                    null,
                    new object[] { document, tempRoot });
                Assert.That(Prop<bool>(write, "Succeeded"), Is.True,
                    Prop<string>(write, "ErrorReason"));

                var manifest = File.ReadAllText(
                    Path.Combine(Prop<string>(write, "RunDirectory"), "manifest.json"));
                Assert.That(manifest, Does.Contain("\"sceneCapture\":{"));
                Assert.That(manifest, Does.Contain("\"captured\":true"));
                Assert.That(manifest, Does.Contain("\"captureHash\":\"" +
                    Prop<string>(capture, "CaptureHash") + "\""));
                Assert.That(manifest, Does.Contain("\"kinematicFreezeWarnings\":[\""));
                Assert.That(manifest, Does.Contain("Rig/MovingPlatform"));
                Assert.That(manifest, Does.Contain("\"bodyMap\":[{\"stableId\":1"));
                Assert.That(manifest, Does.Contain("\"hierarchyPath\":\"RealBody\""));
                Assert.That(manifest, Does.Contain("\"status\":\"FrozenKinematic\""));

                // The console report places the warnings next to the verdict — the
                // evidence consumer must see the caveat without the window.
                var reportType = Type.GetType(
                    "BugCam.Evidence.GhostEvidenceReport, BugCam.Evidence");
                var report = (string)reportType.GetMethod("Format")
                    .Invoke(null, new object[] { document });
                Assert.That(report, Does.Contain("sceneCaptured=True"));
                Assert.That(report, Does.Contain("sceneCaptureWarningCount=1"));
                Assert.That(report, Does.Contain("sceneCaptureWarning[0]="));
                Assert.That(report, Does.Contain("Rig/MovingPlatform"));
            }
            finally
            {
                Directory.Delete(tempRoot, true);
            }
        }

        [Test]
        public void TowerDocumentCarriesNoSceneCaptureSection()
        {
            var document = CreateFailureDocumentWithCapture(null, "tower-no-capture");
            var tempRoot = Path.Combine(
                Path.GetTempPath(),
                "BugCamSceneCaptureTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                var writerType = Type.GetType(
                    "BugCam.Evidence.GhostEvidenceWriter, BugCam.Evidence");
                var write = writerType.GetMethod(
                    "Write",
                    new[] { document.GetType(), typeof(string) }).Invoke(
                    null,
                    new object[] { document, tempRoot });
                Assert.That(Prop<bool>(write, "Succeeded"), Is.True);

                var manifest = File.ReadAllText(
                    Path.Combine(Prop<string>(write, "RunDirectory"), "manifest.json"));
                Assert.That(manifest, Does.Not.Contain("sceneCapture"));

                var reportType = Type.GetType(
                    "BugCam.Evidence.GhostEvidenceReport, BugCam.Evidence");
                var report = (string)reportType.GetMethod("Format")
                    .Invoke(null, new object[] { document });
                Assert.That(report, Does.Not.Contain("sceneCaptured="));
            }
            finally
            {
                Directory.Delete(tempRoot, true);
            }
        }

        /// <summary>
        /// Minimal writer-ready document via the public failure factory; capture=null
        /// exercises the tower path (default SceneCaptureResult, Performed=false).
        /// </summary>
        private static object CreateFailureDocumentWithCapture(object capture, string runId)
        {
            var builderType = Type.GetType(
                "BugCam.Evidence.GhostEvidenceBuilder, BugCam.Evidence");
            var identityType = Type.GetType(
                "BugCam.Evidence.GhostSearchIdentity, BugCam.Evidence");
            var strategyType = CoreType("EpsilonSearchStrategy");
            var envType = Type.GetType("BugCam.Evidence.GhostRunEnvironment, BugCam.Evidence");
            var provenanceType = Type.GetType(
                "BugCam.Evidence.GhostSettingsProvenance, BugCam.Evidence");
            var captureType = CoreType("SceneCaptureResult");
            var searchResultType = CoreType("EpsilonSearchResult");

            var identity = Activator.CreateInstance(
                identityType,
                1,
                Vector3.right,
                Enum.ToObject(strategyType, 0));
            var environment = Activator.CreateInstance(
                envType,
                "test-unity",
                "test-sha",
                "test-branch",
                "Assets/TestScene.unity");
            var searchResult = searchResultType
                .GetMethod("Failure", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { "scene capture test fixture" });

            return builderType.GetMethod(
                    "CreateFailureDocument",
                    BindingFlags.Public | BindingFlags.Static)
                .Invoke(
                    null,
                    new object[]
                    {
                        searchResult,
                        identity,
                        "SEARCH_FAILED",
                        "scene capture test fixture",
                        10,
                        runId,
                        environment,
                        Activator.CreateInstance(provenanceType),
                        capture ?? Activator.CreateInstance(captureType)
                    });
        }
    }
}
#endif
