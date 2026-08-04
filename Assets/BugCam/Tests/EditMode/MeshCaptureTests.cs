#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BugCam.Tests
{
    /// <summary>
    /// Block 2.2.2 mesh-capture contract (docs/CONTRACT-2.2.2.md, ratified 2026-08-04).
    /// Zero-ref reflection style over BugCam.Core; mesh fixtures are real temporary
    /// assets (AssetDatabase) so the REAL editor resolve provider is exercised, deleted
    /// in TearDown. Pins: both capture extensions, every new fail-closed table row
    /// verbatim, hash additivity (Box/Sphere lines byte-identical + ratified |mesh: tail),
    /// the P.7 amendment pair, ruling 6 objectScale channel, ruling 10 submesh coverage.
    /// </summary>
    public sealed class MeshCaptureTests
    {
        private Scene _scene;
        private readonly List<string> _createdAssetPaths = new List<string>();

        [SetUp]
        public void SetUp()
        {
            _scene = EditorSceneManager.NewPreviewScene();
        }

        [TearDown]
        public void TearDown()
        {
            if (_scene.IsValid())
            {
                EditorSceneManager.ClosePreviewScene(_scene);
            }

            for (var i = 0; i < _createdAssetPaths.Count; i++)
            {
                AssetDatabase.DeleteAsset(_createdAssetPaths[i]);
            }

            _createdAssetPaths.Clear();
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
            return method.Invoke(null, new object[] { _scene });
        }

        private GameObject Create(string name, Vector3 position)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, _scene);
            go.transform.position = position;
            return go;
        }

        private static object RecordFor(object capture, string path)
        {
            foreach (var record in Prop<Array>(capture, "Objects"))
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

        // --- Mesh asset fixtures ---

        /// <summary>
        /// Triangle-soup mesh: 8 cube corners (±0.5) plus one interior vertex, two
        /// submeshes. Interior vertex position is a parameter so the P.7 amendment pair
        /// can edit geometry while preserving vertexCount, subMeshCount, and bounds.
        /// </summary>
        private Mesh CreateMeshAsset(
            string meshName,
            Vector3 interiorVertex,
            bool swapSecondSubmeshWinding = false)
        {
            var mesh = new Mesh { name = meshName };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, 0.5f),
                interiorVertex
            };
            mesh.subMeshCount = 2;
            mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7 }, 0);
            mesh.SetTriangles(
                swapSecondSubmeshWinding
                    ? new[] { 0, 1, 8, 1, 2, 8 }
                    : new[] { 0, 8, 1, 1, 8, 2 },
                1);
            mesh.RecalculateBounds();

            var path = "Assets/BugCamMeshCaptureTest_" + Guid.NewGuid().ToString("N") +
                       ".asset";
            AssetDatabase.CreateAsset(mesh, path);
            _createdAssetPaths.Add(path);
            return mesh;
        }

        private GameObject CreateStaticMeshObject(
            string name,
            Mesh mesh,
            bool convex = false,
            Vector3? scale = null)
        {
            var go = Create(name, new Vector3(0f, 0f, 0f));
            if (scale.HasValue)
            {
                go.transform.localScale = scale.Value;
            }

            var collider = go.AddComponent<MeshCollider>();
            collider.convex = convex;
            collider.sharedMesh = mesh;
            return go;
        }

        private GameObject CreateDynamicMeshObject(
            string name,
            Mesh mesh,
            bool convex = true,
            Vector3? scale = null)
        {
            var go = CreateStaticMeshObject(name, mesh, convex, scale);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 1f;
            return go;
        }

        private GameObject CreateDynamicBox(string name, Vector3 position)
        {
            var go = Create(name, position);
            go.AddComponent<BoxCollider>();
            go.AddComponent<Rigidbody>().mass = 1f;
            return go;
        }

        // --- Extension 1: static MeshCollider (any convex flag) → CapturedStatic ---

        [Test]
        public void StaticNonConvexMeshColliderIsCapturedStatic()
        {
            CreateDynamicBox("B", new Vector3(0f, 1f, 0f));
            var mesh = CreateMeshAsset("GroundMesh", new Vector3(0.1f, 0f, 0f));
            CreateStaticMeshObject("M", mesh, convex: false);

            var capture = Capture();
            Assert.That(Prop<bool>(capture, "Succeeded"), Is.True,
                Prop<string>(capture, "FailureSummary"));
            Assert.That(StatusName(RecordFor(capture, "M")), Is.EqualTo("CapturedStatic"));

            var statics = Prop<Array>(capture, "StaticColliders");
            Assert.That(statics.Length, Is.EqualTo(1));
            var definition = statics.GetValue(0);
            Assert.That(Prop<object>(definition, "Shape").ToString(), Is.EqualTo("Mesh"));
            var reference = Prop<object>(definition, "MeshReference");
            Assert.That(Prop<bool>(reference, "Convex"), Is.False);
            Assert.That(Prop<bool>(reference, "IsSet"), Is.True);

            var record = RecordFor(capture, "M");
            Assert.That(Prop<bool>(record, "HasMeshReference"), Is.True);
            Assert.That(
                Prop<string>(Prop<object>(record, "MeshReference"), "ContentHash"),
                Has.Length.EqualTo(64));

            // Ratified hash tail: |mesh:<guid>:<fileId>:<contentHash>:<convex 0|1>.
            Assert.That(Prop<string>(capture, "CaptureHash"), Has.Length.EqualTo(64));
        }

        [Test]
        public void StaticConvexMeshColliderIsCapturedStaticToo()
        {
            // Адъюдикация №1: convex-статика захватывается наравне с non-convex.
            CreateDynamicBox("B", new Vector3(0f, 1f, 0f));
            var mesh = CreateMeshAsset("ConvexStaticMesh", new Vector3(0.1f, 0f, 0f));
            CreateStaticMeshObject("M", mesh, convex: true);

            var capture = Capture();
            Assert.That(Prop<bool>(capture, "Succeeded"), Is.True,
                Prop<string>(capture, "FailureSummary"));
            Assert.That(StatusName(RecordFor(capture, "M")), Is.EqualTo("CapturedStatic"));
            var reference = Prop<object>(
                Prop<Array>(capture, "StaticColliders").GetValue(0), "MeshReference");
            Assert.That(Prop<bool>(reference, "Convex"), Is.True);
        }

        // --- Extension 2: dynamic Rigidbody + convex MeshCollider → CapturedDynamic ---

        [Test]
        public void DynamicConvexMeshBodyCapturesWorldAabbSizeFullScaleAndFingerprint()
        {
            var mesh = CreateMeshAsset("DynamicMesh", new Vector3(0.1f, 0f, 0f));
            var go = CreateDynamicMeshObject(
                "D", mesh, convex: true, scale: new Vector3(2f, 3f, 4f));
            go.transform.position = new Vector3(0f, 5f, 0f);

            var capture = Capture();
            Assert.That(Prop<bool>(capture, "Succeeded"), Is.True,
                Prop<string>(capture, "FailureSummary"));
            Assert.That(StatusName(RecordFor(capture, "D")), Is.EqualTo("CapturedDynamic"));

            var body = Prop<Array>(capture, "Bodies").GetValue(0);
            Assert.That(Prop<object>(body, "Shape").ToString(), Is.EqualTo("Mesh"));

            // Адъюдикация №6, bit-identical: Size = mesh.bounds × |lossyScale|.
            var bounds = mesh.bounds;
            var size = Prop<Vector3>(body, "Size");
            Assert.That(size.x, Is.EqualTo(2f * bounds.size.x));
            Assert.That(size.y, Is.EqualTo(3f * bounds.size.y));
            Assert.That(size.z, Is.EqualTo(4f * bounds.size.z));
            Assert.That(Prop<Vector3>(body, "FullScale"),
                Is.EqualTo(new Vector3(2f, 3f, 4f)));

            var reference = Prop<object>(body, "MeshReference");
            Assert.That(Prop<int>(reference, "VertexCount"), Is.EqualTo(mesh.vertexCount));
            Assert.That(Prop<int>(reference, "SubMeshCount"), Is.EqualTo(2));
            Assert.That(Prop<Vector3>(reference, "BoundsCenter"),
                Is.EqualTo(bounds.center));
            Assert.That(Prop<Vector3>(reference, "BoundsSize"), Is.EqualTo(bounds.size));
            Assert.That(Prop<string>(reference, "ContentHash"), Has.Length.EqualTo(64));
            Assert.That(Prop<string>(reference, "AssetGuid"), Is.Not.Empty);
        }

        [Test]
        public void NonConvexMeshOnDynamicBodyFailsClosedVerbatim()
        {
            var mesh = CreateMeshAsset("SoupMesh", new Vector3(0.1f, 0f, 0f));
            CreateDynamicMeshObject("D", mesh, convex: false);

            var capture = Capture();
            Assert.That(Prop<bool>(capture, "Succeeded"), Is.False);
            Assert.That(Prop<string>(RecordFor(capture, "D"), "Reason"), Is.EqualTo(
                "non-convex MeshCollider на динамическом теле не поддерживается PhysX — " +
                "включите convex или это тело не захватывается"));
        }

        [Test]
        public void KinematicNonConvexMeshBodyFreezesToStatic()
        {
            CreateDynamicBox("B", new Vector3(0f, 1f, 0f));
            var mesh = CreateMeshAsset("KinematicMesh", new Vector3(0.1f, 0f, 0f));
            var go = CreateDynamicMeshObject("K", mesh, convex: false);
            go.GetComponent<Rigidbody>().isKinematic = true;

            var capture = Capture();
            Assert.That(Prop<bool>(capture, "Succeeded"), Is.True,
                Prop<string>(capture, "FailureSummary"));
            Assert.That(StatusName(RecordFor(capture, "K")), Is.EqualTo("FrozenKinematic"));
            var statics = Prop<Array>(capture, "StaticColliders");
            Assert.That(statics.Length, Is.EqualTo(1));
            Assert.That(Prop<object>(statics.GetValue(0), "Shape").ToString(),
                Is.EqualTo("Mesh"));
        }

        // --- Ratified fail-closed table rows (verbatim literals) ---

        [Test]
        public void MeshColliderWithoutSharedMeshFailsClosedVerbatim()
        {
            CreateDynamicBox("B", new Vector3(0f, 1f, 0f));
            var go = Create("M", Vector3.zero);
            go.AddComponent<MeshCollider>();

            var capture = Capture();
            Assert.That(Prop<bool>(capture, "Succeeded"), Is.False);
            Assert.That(Prop<string>(RecordFor(capture, "M"), "Reason"),
                Is.EqualTo("MeshCollider без sharedMesh — захватывать нечего"));
        }

        [Test]
        public void NonDefaultCookingOptionsFailClosedVerbatim()
        {
            CreateDynamicBox("B", new Vector3(0f, 1f, 0f));
            var mesh = CreateMeshAsset("CookedMesh", new Vector3(0.1f, 0f, 0f));
            var go = CreateStaticMeshObject("M", mesh);
            go.GetComponent<MeshCollider>().cookingOptions = MeshColliderCookingOptions.None;

            var capture = Capture();
            Assert.That(Prop<bool>(capture, "Succeeded"), Is.False);
            Assert.That(Prop<string>(RecordFor(capture, "M"), "Reason"),
                Is.EqualTo("нестандартные MeshCollider cookingOptions не воспроизводятся"));
        }

        [Test]
        public void NegativeScaleOnMeshFailsClosedVerbatim()
        {
            CreateDynamicBox("B", new Vector3(0f, 1f, 0f));
            var mesh = CreateMeshAsset("MirroredMesh", new Vector3(0.1f, 0f, 0f));
            CreateStaticMeshObject("M", mesh, convex: false, scale: new Vector3(-1f, 1f, 1f));

            var capture = Capture();
            Assert.That(Prop<bool>(capture, "Succeeded"), Is.False);
            Assert.That(Prop<string>(RecordFor(capture, "M"), "Reason"), Is.EqualTo(
                "отрицательный масштаб (зеркалирование) на MeshCollider не поддерживается " +
                "в v0.1"));
        }

        [Test]
        public void MissingProviderFailsClosedVerbatim()
        {
            CreateDynamicBox("B", new Vector3(0f, 1f, 0f));
            var mesh = CreateMeshAsset("NoProviderMesh", new Vector3(0.1f, 0f, 0f));
            CreateStaticMeshObject("M", mesh);

            var providerProperty = CoreType("SceneMeshResolve").GetProperty(
                "Provider", BindingFlags.Public | BindingFlags.Static);
            var saved = providerProperty.GetValue(null);
            providerProperty.SetValue(null, null);
            try
            {
                var capture = Capture();
                Assert.That(Prop<bool>(capture, "Succeeded"), Is.False);
                Assert.That(Prop<string>(RecordFor(capture, "M"), "Reason"), Is.EqualTo(
                    "провайдер меш-резолва отсутствует — захват мешей недоступен в этом " +
                    "окружении"));
            }
            finally
            {
                providerProperty.SetValue(null, saved);
            }
        }

        [Test]
        public void NonAssetMeshFailsClosedAsUnavailableAsset()
        {
            CreateDynamicBox("B", new Vector3(0f, 1f, 0f));
            var runtimeMesh = new Mesh { name = "RuntimeOnlyMesh" };
            runtimeMesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f)
            };
            runtimeMesh.triangles = new[] { 0, 1, 2 };
            runtimeMesh.RecalculateBounds();
            CreateStaticMeshObject("M", runtimeMesh);

            var capture = Capture();
            Assert.That(Prop<bool>(capture, "Succeeded"), Is.False);
            var reason = Prop<string>(RecordFor(capture, "M"), "Reason");
            Assert.That(reason, Does.Contain("меш-ассет недоступен"));
            Assert.That(reason, Does.Contain("RuntimeOnlyMesh"));
            Assert.That(reason, Does.Contain("не найден в проекте"));
        }

        [Test]
        public void UnreadableGeometryRowGuardsCaptureAndEditModeAlwaysReadsMeasuredFact()
        {
            // Measured 2026-08-04 (readability precondition + this fixture): in EDIT MODE
            // mesh geometry stays readable even after UploadMeshData(true) — the editor
            // keeps the CPU copy — so the fail-closed row's condition cannot be
            // constructed in an EditMode test. This test (1) documents that measured
            // fact behaviorally and (2) pins the ratified verbatim literal plus the
            // TryComputeMeshContentHash guard at source level, matching the house
            // source-scan pin precedent.
            LogAssert.ignoreFailingMessages = true;
            CreateDynamicBox("B", new Vector3(0f, 1f, 0f));
            var mesh = CreateMeshAsset("StrippedMesh", new Vector3(0.1f, 0f, 0f));
            mesh.UploadMeshData(true);
            CreateStaticMeshObject("M", mesh);

            var capture = Capture();
            Assert.That(Prop<bool>(capture, "Succeeded"), Is.True,
                "Edit mode keeps geometry readable (measured) — capture must succeed: " +
                Prop<string>(capture, "FailureSummary"));

            var source = System.IO.File.ReadAllText(
                System.IO.Path.Combine(
                    Application.dataPath, "BugCam", "Core", "SceneCapture.cs"));
            Assert.That(source,
                Does.Contain("геометрия меша нечитаема (Read/Write выключен): "));
            Assert.That(source, Does.Contain(" — hash геометрии невычислим"));
            Assert.That(source, Does.Contain("TryComputeMeshContentHash(mesh"),
                "The unreadable-geometry guard must gate the capture-point hash.");
        }

        [Test]
        public void CapsuleFailureLiteralCarriesRatifiedUpdatedTail()
        {
            // Объявленная и одобренная правка литерала (решение №9).
            var go = Create("C", new Vector3(0f, 1f, 0f));
            go.AddComponent<CapsuleCollider>();
            go.AddComponent<Rigidbody>();

            var capture = Capture();
            Assert.That(Prop<bool>(capture, "Succeeded"), Is.False);
            Assert.That(Prop<string>(RecordFor(capture, "C"), "Reason"), Is.EqualTo(
                "неподдерживаемый контактный шейп: CapsuleCollider " +
                "(v0.1 поддерживает Box, Sphere и Mesh)"));
        }

        // --- Hash additivity: byte-format pins ---

        [Test]
        public void BoxSphereHashLinesStayByteIdenticalFormatPin()
        {
            // Reconstructs the ratified pre-2.2.2 canonical serialization for a
            // Box/Sphere-only fixture and asserts the capture hash equals its SHA-256 —
            // proving the mesh extension left Box/Sphere lines byte-identical.
            CreateDynamicBox("B", new Vector3(0f, 1f, 0f));
            var ground = Create("G", new Vector3(0f, -0.5f, 0f));
            ground.transform.localScale = new Vector3(20f, 1f, 20f);
            ground.AddComponent<BoxCollider>();

            var capture = Capture();
            Assert.That(Prop<bool>(capture, "Succeeded"), Is.True,
                Prop<string>(capture, "FailureSummary"));

            var canonical =
                "O|0000:B|0|\n" +
                "O|0001:G|1|\n" +
                "D|0000:B|0|0,1,0|0,0,0,1|1,1,1|1|0,0,0\n" +
                "S|0001:G#00|0|0,-0.5,0|0,0,0,1|20,1,20\n";
            Assert.That(Prop<string>(capture, "CaptureHash"), Is.EqualTo(Sha256(canonical)));
        }

        [Test]
        public void MeshHashLineCarriesRatifiedTailFormatPin()
        {
            CreateDynamicBox("B", new Vector3(0f, 1f, 0f));
            var mesh = CreateMeshAsset("TailMesh", new Vector3(0.1f, 0f, 0f));
            CreateStaticMeshObject("M", mesh, convex: false);

            var capture = Capture();
            Assert.That(Prop<bool>(capture, "Succeeded"), Is.True,
                Prop<string>(capture, "FailureSummary"));

            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                mesh, out var guid, out long fileId), Is.True);
            var contentHash = Prop<string>(
                Prop<object>(RecordFor(capture, "M"), "MeshReference"), "ContentHash");
            var bounds = mesh.bounds;

            var canonical =
                "O|0000:B|0|\n" +
                "O|0001:M|1|\n" +
                "D|0000:B|0|0,1,0|0,0,0,1|1,1,1|1|0,0,0\n" +
                "S|0001:M#00|2|0,0,0|0,0,0,1|" +
                Invariant(bounds.size.x) + "," + Invariant(bounds.size.y) + "," +
                Invariant(bounds.size.z) +
                "|mesh:" + guid + ":" +
                fileId.ToString(CultureInfo.InvariantCulture) + ":" + contentHash + ":0\n";
            Assert.That(Prop<string>(capture, "CaptureHash"), Is.EqualTo(Sha256(canonical)));
        }

        // --- Amendment P.7 pair + ruling 10 ---

        [Test]
        public void ContentHashChangesOnTopologyPreservingGeometryEdit()
        {
            // P.7 (а): moved interior vertex — vertexCount, subMeshCount and bounds all
            // preserved — MUST change the capture-point contentHash.
            var original = CaptureSingleMeshReference(
                CreateMeshAsset("EditA", new Vector3(0.1f, 0f, 0f)));
            var edited = CaptureSingleMeshReference(
                CreateMeshAsset("EditB", new Vector3(0.12f, 0.02f, 0.03f)));

            Assert.That(Prop<string>(edited, "ContentHash"),
                Is.Not.EqualTo(Prop<string>(original, "ContentHash")));
        }

        [Test]
        public void StructuralFingerprintDoesNotSeeTopologyPreservingEditKnownBlindSpot()
        {
            // P.7 (б): the SAME edit leaves the structural fingerprint bit-identical.
            // This test documents the ratified blind spot of the simulation-point check —
            // it does not assert the blind spot away.
            var original = CaptureSingleMeshReference(
                CreateMeshAsset("BlindA", new Vector3(0.1f, 0f, 0f)));
            var edited = CaptureSingleMeshReference(
                CreateMeshAsset("BlindB", new Vector3(0.12f, 0.02f, 0.03f)));

            Assert.That(Prop<int>(edited, "VertexCount"),
                Is.EqualTo(Prop<int>(original, "VertexCount")));
            Assert.That(Prop<int>(edited, "SubMeshCount"),
                Is.EqualTo(Prop<int>(original, "SubMeshCount")));
            var boundsCenterA = Prop<Vector3>(original, "BoundsCenter");
            var boundsCenterB = Prop<Vector3>(edited, "BoundsCenter");
            var boundsSizeA = Prop<Vector3>(original, "BoundsSize");
            var boundsSizeB = Prop<Vector3>(edited, "BoundsSize");
            Assert.That(boundsCenterB.x, Is.EqualTo(boundsCenterA.x));
            Assert.That(boundsCenterB.y, Is.EqualTo(boundsCenterA.y));
            Assert.That(boundsCenterB.z, Is.EqualTo(boundsCenterA.z));
            Assert.That(boundsSizeB.x, Is.EqualTo(boundsSizeA.x));
            Assert.That(boundsSizeB.y, Is.EqualTo(boundsSizeA.y));
            Assert.That(boundsSizeB.z, Is.EqualTo(boundsSizeA.z));
        }

        [Test]
        public void ContentHashCoversTrianglesOfAllSubmeshes()
        {
            // Адъюдикация №10: changing only submesh 1's triangles (same vertices, same
            // counts, same bounds) must change the contentHash.
            var original = CaptureSingleMeshReference(
                CreateMeshAsset("SubA", new Vector3(0.1f, 0f, 0f)));
            var rewound = CaptureSingleMeshReference(
                CreateMeshAsset("SubB", new Vector3(0.1f, 0f, 0f),
                    swapSecondSubmeshWinding: true));

            Assert.That(Prop<string>(rewound, "ContentHash"),
                Is.Not.EqualTo(Prop<string>(original, "ContentHash")));
        }

        private object CaptureSingleMeshReference(Mesh mesh)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var body = new GameObject("B");
                SceneManager.MoveGameObjectToScene(body, scene);
                body.transform.position = new Vector3(0f, 1f, 0f);
                body.AddComponent<BoxCollider>();
                body.AddComponent<Rigidbody>().mass = 1f;

                var holder = new GameObject("M");
                SceneManager.MoveGameObjectToScene(holder, scene);
                var collider = holder.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;

                var method = CoreType("SceneCapture").GetMethod(
                    "Capture",
                    BindingFlags.Public | BindingFlags.Static);
                var capture = method.Invoke(null, new object[] { scene });
                Assert.That(Prop<bool>(capture, "Succeeded"), Is.True,
                    Prop<string>(capture, "FailureSummary"));
                return Prop<object>(RecordFor(capture, "M"), "MeshReference");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        // --- Ruling 6 objectScale channel + cooking-default pin ---

        [Test]
        public void ObjectScaleChannelBitIdentityPin()
        {
            // Ruling 6: the score's objectScale = max component of the Size channel.
            // Box/Sphere Size semantics must stay bit-identical to the pre-2.2.2 formula;
            // mesh Size is the world AABB whose max component becomes objectScale.
            var box = CreateDynamicBox("B", new Vector3(0f, 1f, 0f));
            box.transform.localScale = new Vector3(1.5f, 2f, 0.5f);
            var mesh = CreateMeshAsset("ScaleMesh", new Vector3(0.1f, 0f, 0f));
            CreateDynamicMeshObject("D", mesh, convex: true, scale: new Vector3(3f, 1f, 1f));

            var capture = Capture();
            Assert.That(Prop<bool>(capture, "Succeeded"), Is.True,
                Prop<string>(capture, "FailureSummary"));

            var bodies = Prop<Array>(capture, "Bodies");
            var boxSize = Prop<Vector3>(bodies.GetValue(0), "Size");
            Assert.That(boxSize.x, Is.EqualTo(1.5f));
            Assert.That(boxSize.y, Is.EqualTo(2f));
            Assert.That(boxSize.z, Is.EqualTo(0.5f));
            Assert.That(Mathf.Max(boxSize.x, Mathf.Max(boxSize.y, boxSize.z)),
                Is.EqualTo(2f), "Box objectScale must stay max(Size) bit-identically.");

            var meshSize = Prop<Vector3>(bodies.GetValue(1), "Size");
            var bounds = mesh.bounds;
            Assert.That(meshSize.x, Is.EqualTo(3f * bounds.size.x));
            Assert.That(meshSize.y, Is.EqualTo(bounds.size.y));
            Assert.That(meshSize.z, Is.EqualTo(bounds.size.z));
            Assert.That(Mathf.Max(meshSize.x, Mathf.Max(meshSize.y, meshSize.z)),
                Is.EqualTo(3f * bounds.size.x),
                "Mesh objectScale must be the max world-AABB component.");
        }

        [Test]
        public void FreshMeshColliderCookingOptionsMatchPinnedDefault()
        {
            var pinned = CoreType("SceneCapture")
                .GetField("DefaultMeshCookingOptions",
                    BindingFlags.Public | BindingFlags.Static)
                .GetValue(null);
            var go = Create("Probe", Vector3.zero);
            var collider = go.AddComponent<MeshCollider>();
            Assert.That(collider.cookingOptions, Is.EqualTo(pinned),
                "Unity's factory default cookingOptions drifted from the pinned constant.");
        }

        // --- Evidence: manifest meshRef + error-code mapping ---

        [Test]
        public void ManifestObjectsCarryMeshRefForMeshShapesOnly()
        {
            CreateDynamicBox("B", new Vector3(0f, 1f, 0f));
            var mesh = CreateMeshAsset("ManifestMesh", new Vector3(0.1f, 0f, 0f));
            CreateStaticMeshObject("M", mesh, convex: false);

            var capture = Capture();
            Assert.That(Prop<bool>(capture, "Succeeded"), Is.True,
                Prop<string>(capture, "FailureSummary"));
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                mesh, out var guid, out long fileId), Is.True);

            var manifest = WriteManifestFor(capture, "meshref-manifest");
            // AssetDatabase.CreateAsset renames the main asset to the file name — the
            // manifest carries the asset-derived mesh.name, so expect it dynamically.
            Assert.That(manifest, Does.Contain(
                "\"meshRef\":{\"assetGuid\":\"" + guid + "\",\"localFileId\":" +
                fileId.ToString(CultureInfo.InvariantCulture) +
                ",\"meshName\":\"" + mesh.name + "\",\"contentHash\":\""));
            Assert.That(manifest, Does.Contain("\"convex\":false}"));

            // The Box object's record must NOT carry the field (absent, not null-ed).
            var boxEntryStart = manifest.IndexOf(
                "\"hierarchyPath\":\"B\",\"status\":\"CapturedDynamic\"",
                StringComparison.Ordinal);
            Assert.That(boxEntryStart, Is.GreaterThanOrEqualTo(0));
            var boxEntryEnd = manifest.IndexOf('}', boxEntryStart);
            Assert.That(
                manifest.Substring(boxEntryStart, boxEntryEnd - boxEntryStart),
                Does.Not.Contain("meshRef"));
        }

        [Test]
        public void BoxSphereOnlyManifestCarriesNoMeshRefKey()
        {
            CreateDynamicBox("B", new Vector3(0f, 1f, 0f));
            var ground = Create("G", new Vector3(0f, -0.5f, 0f));
            ground.transform.localScale = new Vector3(20f, 1f, 20f);
            ground.AddComponent<BoxCollider>();

            var capture = Capture();
            Assert.That(Prop<bool>(capture, "Succeeded"), Is.True);
            var manifest = WriteManifestFor(capture, "no-meshref-manifest");
            Assert.That(manifest, Does.Not.Contain("meshRef"),
                "Box/Sphere manifests must stay byte-free of the additive mesh field.");
        }

        [Test]
        public void HarnessMeshResolveFailureMapsToDistinctErrorCode()
        {
            var builderType = Type.GetType(
                "BugCam.Evidence.GhostEvidenceBuilder, BugCam.Evidence");
            var method = builderType.GetMethod(
                "ResolveSearchErrorCode",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method.Invoke(null, new object[]
            {
                "SCENE_MESH_RESOLVE_FAILED: меш-ассет изменился с момента захвата"
            }), Is.EqualTo("SCENE_MESH_RESOLVE_FAILED"));
            Assert.That(method.Invoke(null, new object[] { "any other failure" }),
                Is.EqualTo("SEARCH_FAILED"));
        }

        // --- Amendment П.6 handover: bit-identical float round-trip ---

        [Test]
        public void SceneCaptureHandoverRoundTripsBitIdentically()
        {
            // The handover carries simulation inputs across the Play Mode domain reload;
            // ANY float drift here would shift the captured-scene pins. Messy transform
            // values on purpose — nothing about this fixture is representable exactly.
            var box = CreateDynamicBox("B", new Vector3(0.1f, 1f / 3f, 2.7f));
            box.transform.rotation = Quaternion.Euler(10.3f, 20.7f, 30.9f);
            var mesh = CreateMeshAsset("HandoverMesh", new Vector3(0.1f, 0f, 0f));
            CreateDynamicMeshObject(
                "D", mesh, convex: true, scale: new Vector3(1.1f, 0.3f, 2.2f));

            var capture = Capture();
            Assert.That(Prop<bool>(capture, "Succeeded"), Is.True,
                Prop<string>(capture, "FailureSummary"));

            var handoverType = Type.GetType(
                "BugCam.Editor.SceneCaptureHandover, BugCam.Editor");
            Assert.That(handoverType, Is.Not.Null);
            var toJson = handoverType.GetMethod(
                "ToJson", BindingFlags.NonPublic | BindingFlags.Static);
            var fromJson = handoverType.GetMethod(
                "FromJson", BindingFlags.NonPublic | BindingFlags.Static);
            var json = (string)toJson.Invoke(null, new[] { capture });
            var restored = fromJson.Invoke(null, new object[] { json });

            Assert.That(Prop<string>(restored, "CaptureHash"),
                Is.EqualTo(Prop<string>(capture, "CaptureHash")));
            Assert.That(Prop<bool>(restored, "Succeeded"), Is.True);

            var originalBodies = Prop<Array>(capture, "Bodies");
            var restoredBodies = Prop<Array>(restored, "Bodies");
            Assert.That(restoredBodies.Length, Is.EqualTo(originalBodies.Length));
            for (var i = 0; i < originalBodies.Length; i++)
            {
                var original = originalBodies.GetValue(i);
                var roundTripped = restoredBodies.GetValue(i);
                AssertBitIdentical(Prop<Vector3>(original, "Position"),
                    Prop<Vector3>(roundTripped, "Position"), "Position[" + i + "]");
                AssertBitIdentical(Prop<Vector3>(original, "Size"),
                    Prop<Vector3>(roundTripped, "Size"), "Size[" + i + "]");
                AssertBitIdentical(Prop<Vector3>(original, "FullScale"),
                    Prop<Vector3>(roundTripped, "FullScale"), "FullScale[" + i + "]");
                var originalRotation = Prop<Quaternion>(original, "Rotation");
                var roundTrippedRotation = Prop<Quaternion>(roundTripped, "Rotation");
                Assert.That(
                    BitConverter.SingleToInt32Bits(roundTrippedRotation.x),
                    Is.EqualTo(BitConverter.SingleToInt32Bits(originalRotation.x)));
                Assert.That(
                    BitConverter.SingleToInt32Bits(roundTrippedRotation.y),
                    Is.EqualTo(BitConverter.SingleToInt32Bits(originalRotation.y)));
                Assert.That(
                    BitConverter.SingleToInt32Bits(roundTrippedRotation.z),
                    Is.EqualTo(BitConverter.SingleToInt32Bits(originalRotation.z)));
                Assert.That(
                    BitConverter.SingleToInt32Bits(roundTrippedRotation.w),
                    Is.EqualTo(BitConverter.SingleToInt32Bits(originalRotation.w)));
                Assert.That(
                    BitConverter.SingleToInt32Bits(Prop<float>(roundTripped, "Mass")),
                    Is.EqualTo(BitConverter.SingleToInt32Bits(Prop<float>(original, "Mass"))));

                var originalReference = Prop<object>(original, "MeshReference");
                var roundTrippedReference = Prop<object>(roundTripped, "MeshReference");
                Assert.That(Prop<bool>(roundTrippedReference, "IsSet"),
                    Is.EqualTo(Prop<bool>(originalReference, "IsSet")));
                if (Prop<bool>(originalReference, "IsSet"))
                {
                    Assert.That(Prop<string>(roundTrippedReference, "ContentHash"),
                        Is.EqualTo(Prop<string>(originalReference, "ContentHash")));
                    Assert.That(Prop<long>(roundTrippedReference, "LocalFileId"),
                        Is.EqualTo(Prop<long>(originalReference, "LocalFileId")));
                    AssertBitIdentical(
                        Prop<Vector3>(originalReference, "BoundsCenter"),
                        Prop<Vector3>(roundTrippedReference, "BoundsCenter"),
                        "BoundsCenter[" + i + "]");
                    AssertBitIdentical(
                        Prop<Vector3>(originalReference, "BoundsSize"),
                        Prop<Vector3>(roundTrippedReference, "BoundsSize"),
                        "BoundsSize[" + i + "]");
                }
            }

            var originalRecords = Prop<Array>(capture, "Objects");
            var restoredRecords = Prop<Array>(restored, "Objects");
            Assert.That(restoredRecords.Length, Is.EqualTo(originalRecords.Length));
            for (var i = 0; i < originalRecords.Length; i++)
            {
                Assert.That(
                    Prop<string>(restoredRecords.GetValue(i), "OrderKey"),
                    Is.EqualTo(Prop<string>(originalRecords.GetValue(i), "OrderKey")));
                Assert.That(
                    StatusName(restoredRecords.GetValue(i)),
                    Is.EqualTo(StatusName(originalRecords.GetValue(i))));
            }
        }

        private static void AssertBitIdentical(Vector3 expected, Vector3 actual, string label)
        {
            Assert.That(BitConverter.SingleToInt32Bits(actual.x),
                Is.EqualTo(BitConverter.SingleToInt32Bits(expected.x)), label + ".x");
            Assert.That(BitConverter.SingleToInt32Bits(actual.y),
                Is.EqualTo(BitConverter.SingleToInt32Bits(expected.y)), label + ".y");
            Assert.That(BitConverter.SingleToInt32Bits(actual.z),
                Is.EqualTo(BitConverter.SingleToInt32Bits(expected.z)), label + ".z");
        }

        private string WriteManifestFor(object capture, string runId)
        {
            var builderType = Type.GetType(
                "BugCam.Evidence.GhostEvidenceBuilder, BugCam.Evidence");
            var identityType = Type.GetType(
                "BugCam.Evidence.GhostSearchIdentity, BugCam.Evidence");
            var strategyType = CoreType("EpsilonSearchStrategy");
            var envType = Type.GetType("BugCam.Evidence.GhostRunEnvironment, BugCam.Evidence");
            var provenanceType = Type.GetType(
                "BugCam.Evidence.GhostSettingsProvenance, BugCam.Evidence");
            var searchResultType = CoreType("EpsilonSearchResult");

            var identity = Activator.CreateInstance(
                identityType, 1, Vector3.right, Enum.ToObject(strategyType, 0));
            var environment = Activator.CreateInstance(
                envType, "test-unity", "test-sha", "test-branch", "Assets/TestScene.unity");
            var searchResult = searchResultType
                .GetMethod("Failure", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { "mesh capture manifest fixture" });
            var document = builderType.GetMethod(
                    "CreateFailureDocument", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[]
                {
                    searchResult, identity, "SEARCH_FAILED",
                    "mesh capture manifest fixture", 10, runId, environment,
                    Activator.CreateInstance(provenanceType), capture
                });

            var tempRoot = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "BugCamMeshCaptureTest-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(tempRoot);
            try
            {
                var writerType = Type.GetType(
                    "BugCam.Evidence.GhostEvidenceWriter, BugCam.Evidence");
                var write = writerType.GetMethod(
                    "Write", new[] { document.GetType(), typeof(string) }).Invoke(
                    null, new object[] { document, tempRoot });
                Assert.That(Prop<bool>(write, "Succeeded"), Is.True,
                    Prop<string>(write, "ErrorReason"));
                return System.IO.File.ReadAllText(
                    System.IO.Path.Combine(
                        Prop<string>(write, "RunDirectory"), "manifest.json"));
            }
            finally
            {
                System.IO.Directory.Delete(tempRoot, true);
            }
        }

        // --- helpers ---

        private static string Invariant(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string Sha256(string canonical)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
                var hex = new StringBuilder(bytes.Length * 2);
                for (var i = 0; i < bytes.Length; i++)
                {
                    hex.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
                }

                return hex.ToString();
            }
        }
    }
}
#endif
