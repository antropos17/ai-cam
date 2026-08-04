using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BugCam.Core
{
    /// <summary>Per-object outcome inside one capture pass (Block 2.2.1 A2).</summary>
    public enum SceneCaptureObjectStatus
    {
        /// <summary>Non-kinematic Rigidbody with one supported collider → tracked body.</summary>
        CapturedDynamic = 0,

        /// <summary>Collider without Rigidbody → static collider in every probe scene.</summary>
        CapturedStatic = 1,

        /// <summary>Kinematic Rigidbody frozen to a static collider (ratified A2 rule).</summary>
        FrozenKinematic = 2,

        /// <summary>Physically safe to omit (contactless / inactive / trigger-only).</summary>
        ExcludedSafely = 3,

        /// <summary>Not representable — the whole capture fails closed with this reason.</summary>
        Failed = 4
    }

    /// <summary>One physics-relevant scene object and what the capture did with it.</summary>
    public readonly struct SceneCaptureObjectRecord
    {
        public SceneCaptureObjectRecord(
            string hierarchyPath,
            string orderKey,
            SceneCaptureObjectStatus status,
            int stableId,
            string reason)
            : this(hierarchyPath, orderKey, status, stableId, reason, default)
        {
        }

        public SceneCaptureObjectRecord(
            string hierarchyPath,
            string orderKey,
            SceneCaptureObjectStatus status,
            int stableId,
            string reason,
            SimulationMeshReference meshReference)
        {
            HierarchyPath = hierarchyPath ?? string.Empty;
            OrderKey = orderKey ?? string.Empty;
            Status = status;
            StableId = stableId;
            Reason = reason ?? string.Empty;
            MeshReference = meshReference;
        }

        /// <summary>Human-readable path: object names joined with '/'.</summary>
        public string HierarchyPath { get; }

        /// <summary>
        /// Deterministic identity key: per level "siblingIndex(D4):name", levels joined
        /// with '/'. Unique even for duplicate names — the stable-ID assignment sorts by
        /// this key (ratified: stable ID = hierarchy path + sibling index).
        /// </summary>
        public string OrderKey { get; }

        public SceneCaptureObjectStatus Status { get; }

        /// <summary>Assigned stable ID for captured dynamic bodies; -1 otherwise.</summary>
        public int StableId { get; }

        /// <summary>Exclusion / failure / freeze note. Empty for plain captures.</summary>
        public string Reason { get; }

        /// <summary>
        /// 2.2.2: set for captured mesh-shaped objects (the first mesh collider of a
        /// multi-collider static). Default (unset) for Box/Sphere — the manifest omits
        /// the meshRef field entirely, keeping tower/domino manifests byte-unchanged.
        /// </summary>
        public SimulationMeshReference MeshReference { get; }

        public bool HasMeshReference => MeshReference.IsSet;
    }

    /// <summary>
    /// Outcome of <see cref="SceneCapture.Capture"/>. Two capture outcomes exist at this
    /// level: Succeeded=true (bodies + statics + per-object records, possibly with safe
    /// exclusions and kinematic-freeze warnings) or Succeeded=false (fail-closed: at least
    /// one contact-capable object is not representable; per-object reasons retained, no
    /// simulation input produced). The third contracted outcome — excluded-safely — is a
    /// per-object status inside a successful capture.
    /// </summary>
    public readonly struct SceneCaptureResult
    {
        private static readonly SimulationBodyDefinition[] EmptyBodies =
            Array.Empty<SimulationBodyDefinition>();

        private static readonly SimulationStaticColliderDefinition[] EmptyStatics =
            Array.Empty<SimulationStaticColliderDefinition>();

        private static readonly SceneCaptureObjectRecord[] EmptyRecords =
            Array.Empty<SceneCaptureObjectRecord>();

        private static readonly string[] EmptyWarnings = Array.Empty<string>();

        private SceneCaptureResult(
            bool succeeded,
            string failureSummary,
            string scenePath,
            SimulationBodyDefinition[] bodies,
            SimulationStaticColliderDefinition[] staticColliders,
            SceneCaptureObjectRecord[] objects,
            string[] kinematicFreezeWarnings,
            string[] sleepingBodyWarnings,
            string captureHash)
        {
            Performed = true;
            Succeeded = succeeded;
            FailureSummary = failureSummary ?? string.Empty;
            ScenePath = scenePath ?? string.Empty;
            Bodies = bodies ?? EmptyBodies;
            StaticColliders = staticColliders ?? EmptyStatics;
            Objects = objects ?? EmptyRecords;
            KinematicFreezeWarnings = kinematicFreezeWarnings ?? EmptyWarnings;
            SleepingBodyWarnings = sleepingBodyWarnings ?? EmptyWarnings;
            CaptureHash = captureHash ?? string.Empty;
        }

        /// <summary>
        /// False only for <c>default</c> (no capture happened — e.g. a procedural tower
        /// run); both factory results set it. Lets evidence distinguish "no capture" from
        /// "capture failed".
        /// </summary>
        public bool Performed { get; }

        public bool Succeeded { get; }

        /// <summary>First failure reason (fail-closed); empty on success.</summary>
        public string FailureSummary { get; }

        public string ScenePath { get; }

        /// <summary>Captured dynamic bodies sorted by stable ID (1…N by order key).</summary>
        public SimulationBodyDefinition[] Bodies { get; }

        /// <summary>Captured statics, including frozen kinematic bodies.</summary>
        public SimulationStaticColliderDefinition[] StaticColliders { get; }

        /// <summary>Every physics-relevant object, including excluded and failed ones.</summary>
        public SceneCaptureObjectRecord[] Objects { get; }

        /// <summary>
        /// Verbatim kinematic-with-Animator warnings. Contract: these must reach the
        /// window AND the result verdict AND the manifest/evidence.
        /// </summary>
        public string[] KinematicFreezeWarnings { get; }

        /// <summary>
        /// Adjudicated A2 follow-up (2026-08-03): bodies asleep at capture time start
        /// awake in the simulation — notice only, to the capture report and the manifest;
        /// never the verdict (the body stays dynamic, unlike the kinematic freeze).
        /// </summary>
        public string[] SleepingBodyWarnings { get; }

        /// <summary>SHA-256 (hex) over the canonical capture serialization.</summary>
        public string CaptureHash { get; }

        public static SceneCaptureResult Success(
            string scenePath,
            SimulationBodyDefinition[] bodies,
            SimulationStaticColliderDefinition[] staticColliders,
            SceneCaptureObjectRecord[] objects,
            string[] kinematicFreezeWarnings,
            string[] sleepingBodyWarnings,
            string captureHash)
        {
            return new SceneCaptureResult(
                true,
                string.Empty,
                scenePath,
                bodies,
                staticColliders,
                objects,
                kinematicFreezeWarnings,
                sleepingBodyWarnings,
                captureHash);
        }

        public static SceneCaptureResult Failure(
            string failureSummary,
            string scenePath,
            SceneCaptureObjectRecord[] objects,
            string captureHash)
        {
            return new SceneCaptureResult(
                false,
                failureSummary,
                scenePath,
                EmptyBodies,
                EmptyStatics,
                objects,
                EmptyWarnings,
                EmptyWarnings,
                captureHash);
        }

        public bool ContainsBodyId(int stableId)
        {
            for (var i = 0; i < Bodies.Length; i++)
            {
                if (Bodies[i].StableId == stableId)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Block 2.2.1 A2 arbitrary-scene capture (docs/CONTRACT-2.2.1.md). A clean scene is
    /// one where every contact-capable object is representable by the harness: Box or
    /// Sphere primitive collider (sphere Size = diameter), no joints, no shear, default
    /// dynamics (no physic material, default damping, gravity on, discrete collision,
    /// automatic COM/inertia, no constraints). Anything contact-capable outside that set
    /// fails the whole capture closed with per-object reasons — never a silent
    /// approximation. Initial sleep state is not reproduced (bodies are recreated awake in
    /// both baseline and perturbed runs identically).
    /// </summary>
    public static class SceneCapture
    {
        private sealed class DynamicCandidate
        {
            public string Path;
            public string Key;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Size;
            public float Mass;
            public Vector3 InitialLinearVelocity;
            public SimulationColliderShape Shape;
            public SimulationMeshReference MeshReference;
            public Vector3 FullScale;
        }

        private sealed class StaticCandidate
        {
            public string Key;
            public SimulationStaticColliderDefinition Definition;
        }

        private sealed class RecordCandidate
        {
            public string Path;
            public string Key;
            public SceneCaptureObjectStatus Status;
            public string Reason;
            public SimulationMeshReference MeshReference;
        }

        /// <summary>
        /// Unity's factory default for <see cref="MeshCollider.cookingOptions"/>; anything
        /// else fails closed (ratified «нестандартная настройка ⇒ отказ» convention).
        /// Pinned by test against a freshly created MeshCollider.
        /// </summary>
        public const MeshColliderCookingOptions DefaultMeshCookingOptions =
            MeshColliderCookingOptions.CookForFasterSimulation |
            MeshColliderCookingOptions.EnableMeshCleaning |
            MeshColliderCookingOptions.WeldColocatedVertices |
            MeshColliderCookingOptions.UseFastMidphase;

        public static SceneCaptureResult Capture(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return SceneCaptureResult.Failure(
                    "сцена не загружена — захват невозможен",
                    scene.path,
                    null,
                    string.Empty);
            }

            var dynamics = new List<DynamicCandidate>();
            var statics = new List<StaticCandidate>();
            var records = new List<RecordCandidate>();
            var warnings = new List<string>();
            var sleepingWarnings = new List<string>();

            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                Walk(
                    roots[i].transform,
                    BuildKeySegment(roots[i].transform),
                    roots[i].name,
                    dynamics,
                    statics,
                    records,
                    warnings,
                    sleepingWarnings);
            }

            // Deterministic order for everything downstream: sort by the identity key.
            dynamics.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            statics.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            records.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            warnings.Sort(string.CompareOrdinal);
            sleepingWarnings.Sort(string.CompareOrdinal);

            var stableIdByKey = new Dictionary<string, int>(dynamics.Count);
            for (var i = 0; i < dynamics.Count; i++)
            {
                stableIdByKey[dynamics[i].Key] = i + 1;
            }

            var objectRecords = new SceneCaptureObjectRecord[records.Count];
            var failed = false;
            var firstFailure = string.Empty;
            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                var stableId = -1;
                if (record.Status == SceneCaptureObjectStatus.CapturedDynamic &&
                    stableIdByKey.TryGetValue(record.Key, out var id))
                {
                    stableId = id;
                }

                if (record.Status == SceneCaptureObjectStatus.Failed && !failed)
                {
                    failed = true;
                    firstFailure = "«" + record.Path + "»: " + record.Reason;
                }

                objectRecords[i] = new SceneCaptureObjectRecord(
                    record.Path,
                    record.Key,
                    record.Status,
                    stableId,
                    record.Reason,
                    record.MeshReference);
            }

            var hash = ComputeHash(objectRecords, dynamics, statics);
            if (failed)
            {
                return SceneCaptureResult.Failure(
                    "захват сцены fail-closed: " + firstFailure,
                    scene.path,
                    objectRecords,
                    hash);
            }

            if (dynamics.Count == 0)
            {
                return SceneCaptureResult.Failure(
                    "в сцене нет захватываемых динамических тел — поиску нечего возмущать",
                    scene.path,
                    objectRecords,
                    hash);
            }

            var bodies = new SimulationBodyDefinition[dynamics.Count];
            for (var i = 0; i < dynamics.Count; i++)
            {
                var candidate = dynamics[i];
                bodies[i] = new SimulationBodyDefinition(
                    i + 1,
                    candidate.Position,
                    candidate.Rotation,
                    candidate.Size,
                    candidate.Mass,
                    candidate.InitialLinearVelocity,
                    candidate.Shape,
                    candidate.MeshReference,
                    candidate.FullScale);
            }

            var staticDefinitions = new SimulationStaticColliderDefinition[statics.Count];
            for (var i = 0; i < statics.Count; i++)
            {
                staticDefinitions[i] = statics[i].Definition;
            }

            return SceneCaptureResult.Success(
                scene.path,
                bodies,
                staticDefinitions,
                objectRecords,
                warnings.ToArray(),
                sleepingWarnings.ToArray(),
                hash);
        }

        private static void Walk(
            Transform transform,
            string key,
            string path,
            List<DynamicCandidate> dynamics,
            List<StaticCandidate> statics,
            List<RecordCandidate> records,
            List<string> warnings,
            List<string> sleepingWarnings)
        {
            ClassifyObject(
                transform, key, path, dynamics, statics, records, warnings, sleepingWarnings);

            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                Walk(
                    child,
                    key + "/" + BuildKeySegment(child),
                    path + "/" + child.name,
                    dynamics,
                    statics,
                    records,
                    warnings,
                    sleepingWarnings);
            }
        }

        private static string BuildKeySegment(Transform transform)
        {
            return transform.GetSiblingIndex().ToString("D4", CultureInfo.InvariantCulture) +
                   ":" + transform.name;
        }

        private static void ClassifyObject(
            Transform transform,
            string key,
            string path,
            List<DynamicCandidate> dynamics,
            List<StaticCandidate> statics,
            List<RecordCandidate> records,
            List<string> warnings,
            List<string> sleepingWarnings)
        {
            var gameObject = transform.gameObject;
            var rigidbody = gameObject.GetComponent<Rigidbody>();
            var colliders = gameObject.GetComponents<Collider>();
            var isRelevant = rigidbody != null || colliders.Length > 0 ||
                             gameObject.GetComponent<ArticulationBody>() != null;
            if (!isRelevant)
            {
                return;
            }

            if (!gameObject.activeInHierarchy)
            {
                AddRecord(records, path, key, SceneCaptureObjectStatus.ExcludedSafely,
                    "объект неактивен — в физике не участвует");
                return;
            }

            if (gameObject.GetComponent<ArticulationBody>() != null)
            {
                AddRecord(records, path, key, SceneCaptureObjectStatus.Failed,
                    "ArticulationBody не поддерживается в v0.1");
                return;
            }

            if (gameObject.GetComponent<Joint>() != null)
            {
                AddRecord(records, path, key, SceneCaptureObjectStatus.Failed,
                    "джойнт (" + gameObject.GetComponent<Joint>().GetType().Name +
                    ") не поддерживается в v0.1");
                return;
            }

            if (rigidbody == null)
            {
                ClassifyStaticObject(transform, key, path, colliders, statics, records);
                return;
            }

            ClassifyRigidbodyObject(
                transform, key, path, rigidbody, colliders, dynamics, statics, records,
                warnings, sleepingWarnings);
        }

        private static void ClassifyStaticObject(
            Transform transform,
            string key,
            string path,
            Collider[] colliders,
            List<StaticCandidate> statics,
            List<RecordCandidate> records)
        {
            var capturedAny = false;
            var firstMeshReference = default(SimulationMeshReference);
            for (var i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (collider.attachedRigidbody != null)
                {
                    AddRecord(records, path, key, SceneCaptureObjectStatus.Failed,
                        "коллайдер принадлежит Rigidbody родителя — составные коллайдеры " +
                        "не поддерживаются");
                    return;
                }

                if (!collider.enabled || collider.isTrigger)
                {
                    continue;
                }

                if (!TryDescribeSupportedCollider(
                        transform, collider, out var position, out var rotation,
                        out var size, out var shape, out var meshReference,
                        out var fullScale, out var reason))
                {
                    AddRecord(records, path, key, SceneCaptureObjectStatus.Failed, reason);
                    return;
                }

                statics.Add(new StaticCandidate
                {
                    Key = key + "#" + i.ToString("D2", CultureInfo.InvariantCulture),
                    Definition = new SimulationStaticColliderDefinition(
                        position, rotation, size, shape, meshReference, fullScale)
                });
                if (!firstMeshReference.IsSet && meshReference.IsSet)
                {
                    firstMeshReference = meshReference;
                }

                capturedAny = true;
            }

            AddRecord(
                records,
                path,
                key,
                capturedAny
                    ? SceneCaptureObjectStatus.CapturedStatic
                    : SceneCaptureObjectStatus.ExcludedSafely,
                capturedAny
                    ? string.Empty
                    : "бесконтактный объект: нет активного контактного коллайдера",
                firstMeshReference);
        }

        private static void ClassifyRigidbodyObject(
            Transform transform,
            string key,
            string path,
            Rigidbody rigidbody,
            Collider[] colliders,
            List<DynamicCandidate> dynamics,
            List<StaticCandidate> statics,
            List<RecordCandidate> records,
            List<string> warnings,
            List<string> sleepingWarnings)
        {
            Collider contactCollider = null;
            var contactColliderCount = 0;
            for (var i = 0; i < colliders.Length; i++)
            {
                if (colliders[i].enabled && !colliders[i].isTrigger)
                {
                    contactCollider = colliders[i];
                    contactColliderCount++;
                }
            }

            if (contactColliderCount == 0)
            {
                AddRecord(records, path, key, SceneCaptureObjectStatus.ExcludedSafely,
                    "бесконтактное тело: нет активного контактного коллайдера");
                return;
            }

            if (contactColliderCount > 1)
            {
                AddRecord(records, path, key, SceneCaptureObjectStatus.Failed,
                    "несколько контактных коллайдеров на одном теле не поддерживаются");
                return;
            }

            if (!TryDescribeSupportedCollider(
                    transform, contactCollider, out var position, out var rotation,
                    out var size, out var shape, out var meshReference,
                    out var fullScale, out var colliderReason))
            {
                AddRecord(records, path, key, SceneCaptureObjectStatus.Failed, colliderReason);
                return;
            }

            if (rigidbody.isKinematic)
            {
                // 2.2.2: kinematic mesh bodies freeze to static by the existing A2 rule;
                // the static extension accepts any convex flag, so no extra gate here.
                statics.Add(new StaticCandidate
                {
                    Key = key + "#rb",
                    Definition = new SimulationStaticColliderDefinition(
                        position, rotation, size, shape, meshReference, fullScale)
                });

                var hasAnimator = HasAnimatorInParents(transform);
                if (hasAnimator)
                {
                    warnings.Add(
                        "кинематическое тело «" + path + "» заморожено в статику, им " +
                        "управляет Animator — его движение в симуляции не воспроизводится");
                }

                AddRecord(records, path, key, SceneCaptureObjectStatus.FrozenKinematic,
                    hasAnimator
                        ? "заморожено в статику; управляется Animator (см. предупреждение)"
                        : "заморожено в статику",
                    meshReference);
                return;
            }

            if (shape == SimulationColliderShape.Mesh && !meshReference.Convex)
            {
                AddRecord(records, path, key, SceneCaptureObjectStatus.Failed,
                    "non-convex MeshCollider на динамическом теле не поддерживается " +
                    "PhysX — включите convex или это тело не захватывается");
                return;
            }

            if (!TryDescribeDynamics(rigidbody, out var dynamicsReason))
            {
                AddRecord(records, path, key, SceneCaptureObjectStatus.Failed, dynamicsReason);
                return;
            }

            // Adjudicated notice (2026-08-03): a body asleep at capture time starts awake
            // in the simulation — the captured scene can behave differently from the live
            // one. Notice only: the body stays dynamic and the verdict is untouched.
            var isSleeping = rigidbody.IsSleeping();
            if (isSleeping)
            {
                sleepingWarnings.Add(
                    "тело «" + path + "» спит на момент захвата — в симуляции оно " +
                    "стартует бодрствующим; поведение может отличаться от живой сцены");
            }

            dynamics.Add(new DynamicCandidate
            {
                Path = path,
                Key = key,
                Position = position,
                Rotation = rotation,
                Size = size,
                Mass = rigidbody.mass,
                InitialLinearVelocity = rigidbody.linearVelocity,
                Shape = shape,
                MeshReference = meshReference,
                FullScale = fullScale
            });
            AddRecord(
                records,
                path,
                key,
                SceneCaptureObjectStatus.CapturedDynamic,
                isSleeping
                    ? "спит на момент захвата (см. предупреждение)"
                    : string.Empty,
                meshReference);
        }

        /// <summary>
        /// Dynamics the harness cannot reproduce fail closed — capturing them silently
        /// would change the simulation semantics without telling anyone.
        /// </summary>
        private static bool TryDescribeDynamics(Rigidbody rigidbody, out string reason)
        {
            if (!(rigidbody.mass > 0f) || float.IsInfinity(rigidbody.mass) ||
                float.IsNaN(rigidbody.mass))
            {
                reason = "масса тела должна быть положительной и конечной";
                return false;
            }

            if (!rigidbody.useGravity)
            {
                reason = "useGravity=false не поддерживается — харнесс включает гравитацию";
                return false;
            }

            if (rigidbody.constraints != RigidbodyConstraints.None)
            {
                reason = "Rigidbody constraints не поддерживаются в v0.1";
                return false;
            }

            if (rigidbody.linearDamping != 0f || rigidbody.angularDamping != 0.05f)
            {
                reason = "нестандартный damping (linear " +
                         rigidbody.linearDamping.ToString("R", CultureInfo.InvariantCulture) +
                         ", angular " +
                         rigidbody.angularDamping.ToString("R", CultureInfo.InvariantCulture) +
                         ") не воспроизводится харнессом";
                return false;
            }

            if (rigidbody.collisionDetectionMode != CollisionDetectionMode.Discrete)
            {
                reason = "collisionDetectionMode отличен от Discrete — не поддерживается";
                return false;
            }

            if (!rigidbody.automaticCenterOfMass || !rigidbody.automaticInertiaTensor)
            {
                reason = "ручной центр масс / тензор инерции не поддерживается";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static bool TryDescribeSupportedCollider(
            Transform transform,
            Collider collider,
            out Vector3 position,
            out Quaternion rotation,
            out Vector3 size,
            out SimulationColliderShape shape,
            out SimulationMeshReference meshReference,
            out Vector3 fullScale,
            out string reason)
        {
            position = default;
            rotation = default;
            size = default;
            shape = SimulationColliderShape.Box;
            meshReference = default;
            fullScale = default;

            if (collider.sharedMaterial != null)
            {
                reason = "PhysicMaterial на коллайдере не поддерживается — трение/отскок " +
                         "не воспроизводятся";
                return false;
            }

            if (HasShear(transform))
            {
                reason = "трансформ содержит shear (поворот между неравномерными " +
                         "масштабами) — примитивом не воспроизводится";
                return false;
            }

            var lossyScale = transform.lossyScale;
            if (collider is BoxCollider box)
            {
                shape = SimulationColliderShape.Box;
                position = transform.TransformPoint(box.center);
                rotation = transform.rotation;
                size = new Vector3(
                    Mathf.Abs(lossyScale.x * box.size.x),
                    Mathf.Abs(lossyScale.y * box.size.y),
                    Mathf.Abs(lossyScale.z * box.size.z));
            }
            else if (collider is SphereCollider sphere)
            {
                shape = SimulationColliderShape.Sphere;
                position = transform.TransformPoint(sphere.center);
                rotation = transform.rotation;
                // PhysX sphere radius scales by the largest |lossyScale| component; the
                // ratified Size convention stores the resulting diameter uniformly.
                var maxScale = Mathf.Max(
                    Mathf.Abs(lossyScale.x),
                    Mathf.Max(Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z)));
                var diameter = 2f * sphere.radius * maxScale;
                size = new Vector3(diameter, diameter, diameter);
            }
            else if (collider is MeshCollider meshCollider)
            {
                // Block 2.2.2 (docs/CONTRACT-2.2.2.md): mesh captured by asset reference;
                // geometry + contentHash read at THIS edit-mode capture point only
                // (Amendment 2026-08-04) — the simulation point never reads geometry.
                shape = SimulationColliderShape.Mesh;
                var mesh = meshCollider.sharedMesh;
                if (mesh == null)
                {
                    reason = "MeshCollider без sharedMesh — захватывать нечего";
                    return false;
                }

                if (meshCollider.cookingOptions != DefaultMeshCookingOptions)
                {
                    reason = "нестандартные MeshCollider cookingOptions не воспроизводятся";
                    return false;
                }

                if (lossyScale.x < 0f || lossyScale.y < 0f || lossyScale.z < 0f)
                {
                    // Адъюдикация 2026-08-04, вопрос №4: fail-closed без эмпирики.
                    reason = "отрицательный масштаб (зеркалирование) на MeshCollider не " +
                             "поддерживается в v0.1";
                    return false;
                }

                var provider = SceneMeshResolve.Provider;
                if (provider == null)
                {
                    reason = SceneMeshResolve.ProviderMissingReason;
                    return false;
                }

                if (!provider.TryDescribeMeshAsset(
                        mesh, out var assetGuid, out var localFileId, out var describeReason))
                {
                    reason = string.IsNullOrEmpty(describeReason)
                        ? "меш-ассет недоступен: /" + mesh.name + " не найден в проекте"
                        : describeReason;
                    return false;
                }

                if (!TryComputeMeshContentHash(mesh, out var contentHash))
                {
                    reason = "геометрия меша нечитаема (Read/Write выключен): " + mesh.name +
                             " — hash геометрии невычислим";
                    return false;
                }

                position = transform.position;
                rotation = transform.rotation;
                var bounds = mesh.bounds;
                // Адъюдикация №6: Size для меша = мировой AABB (mesh.bounds × |lossyScale|);
                // objectScale = max компонента этого канала.
                size = new Vector3(
                    Mathf.Abs(lossyScale.x) * bounds.size.x,
                    Mathf.Abs(lossyScale.y) * bounds.size.y,
                    Mathf.Abs(lossyScale.z) * bounds.size.z);
                fullScale = lossyScale;
                meshReference = new SimulationMeshReference(
                    assetGuid,
                    localFileId,
                    mesh.name,
                    contentHash,
                    meshCollider.convex,
                    mesh.vertexCount,
                    mesh.subMeshCount,
                    bounds.center,
                    bounds.size);
            }
            else
            {
                reason = "неподдерживаемый контактный шейп: " + collider.GetType().Name +
                         " (v0.1 поддерживает Box, Sphere и Mesh)";
                return false;
            }

            if (!(size.x > 0f) || !(size.y > 0f) || !(size.z > 0f) ||
                float.IsInfinity(size.x) || float.IsInfinity(size.y) ||
                float.IsInfinity(size.z) ||
                float.IsNaN(size.x) || float.IsNaN(size.y) || float.IsNaN(size.z))
            {
                reason = "невалидный размер коллайдера (нулевой, отрицательный или " +
                         "неконечный после масштаба)";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        /// <summary>
        /// Shear detection: with shear present, position+rotation+lossyScale cannot rebuild
        /// localToWorldMatrix — compare the two matrices with a relative tolerance.
        /// </summary>
        private static bool HasShear(Transform transform)
        {
            var actual = transform.localToWorldMatrix;
            var rebuilt = Matrix4x4.TRS(
                transform.position,
                transform.rotation,
                transform.lossyScale);
            for (var i = 0; i < 16; i++)
            {
                var a = actual[i];
                var b = rebuilt[i];
                var tolerance = 1e-4f * Mathf.Max(1f, Mathf.Max(Mathf.Abs(a), Mathf.Abs(b)));
                if (Mathf.Abs(a - b) > tolerance)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasAnimatorInParents(Transform transform)
        {
            var current = transform;
            while (current != null)
            {
                if (current.GetComponent<Animator>() != null)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static void AddRecord(
            List<RecordCandidate> records,
            string path,
            string key,
            SceneCaptureObjectStatus status,
            string reason,
            SimulationMeshReference meshReference = default)
        {
            records.Add(new RecordCandidate
            {
                Path = path,
                Key = key,
                Status = status,
                Reason = reason,
                MeshReference = meshReference
            });
        }

        /// <summary>
        /// contentHash (адъюдикация №2/№10): SHA-256 over runtime geometry — vertices then
        /// the triangles of ALL submeshes in fixed order, floats "R" invariant. False when
        /// the geometry is unreadable (Read/Write off outside the edit-mode capture point,
        /// or a runtime mesh stripped of its CPU copy): Unity returns silently empty
        /// arrays there, which this check refuses to hash.
        /// </summary>
        private static bool TryComputeMeshContentHash(Mesh mesh, out string contentHash)
        {
            contentHash = string.Empty;
            Vector3[] vertices;
            try
            {
                vertices = mesh.vertices;
            }
            catch (Exception)
            {
                return false;
            }

            if (vertices == null || vertices.Length == 0 ||
                vertices.Length != mesh.vertexCount)
            {
                return false;
            }

            var sb = new StringBuilder(vertices.Length * 24);
            for (var i = 0; i < vertices.Length; i++)
            {
                sb.Append(Invariant(vertices[i])).Append('\n');
            }

            for (var subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                int[] triangles;
                try
                {
                    triangles = mesh.GetTriangles(subMesh);
                }
                catch (Exception)
                {
                    return false;
                }

                sb.Append("sub").Append(subMesh.ToString(CultureInfo.InvariantCulture))
                    .Append('|');
                for (var i = 0; i < triangles.Length; i++)
                {
                    sb.Append(triangles[i].ToString(CultureInfo.InvariantCulture))
                        .Append(',');
                }

                sb.Append('\n');
            }

            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                var hex = new StringBuilder(bytes.Length * 2);
                for (var i = 0; i < bytes.Length; i++)
                {
                    hex.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
                }

                contentHash = hex.ToString();
                return true;
            }
        }

        private static string ComputeHash(
            SceneCaptureObjectRecord[] records,
            List<DynamicCandidate> dynamics,
            List<StaticCandidate> statics)
        {
            var sb = new StringBuilder(1024);
            for (var i = 0; i < records.Length; i++)
            {
                sb.Append("O|").Append(records[i].OrderKey)
                    .Append('|').Append((int)records[i].Status)
                    .Append('|').Append(records[i].Reason)
                    .Append('\n');
            }

            for (var i = 0; i < dynamics.Count; i++)
            {
                var d = dynamics[i];
                sb.Append("D|").Append(d.Key)
                    .Append('|').Append((int)d.Shape)
                    .Append('|').Append(Invariant(d.Position))
                    .Append('|').Append(Invariant(d.Rotation))
                    .Append('|').Append(Invariant(d.Size))
                    .Append('|').Append(Invariant(d.Mass))
                    .Append('|').Append(Invariant(d.InitialLinearVelocity));
                // 2.2.2 ratified additive extension: Box/Sphere lines stay byte-identical;
                // only mesh shapes gain the trailing |mesh: segment.
                if (d.Shape == SimulationColliderShape.Mesh)
                {
                    AppendMeshHashTail(sb, d.MeshReference);
                }

                sb.Append('\n');
            }

            for (var i = 0; i < statics.Count; i++)
            {
                var s = statics[i];
                sb.Append("S|").Append(s.Key)
                    .Append('|').Append((int)s.Definition.Shape)
                    .Append('|').Append(Invariant(s.Definition.Position))
                    .Append('|').Append(Invariant(s.Definition.Rotation))
                    .Append('|').Append(Invariant(s.Definition.Size));
                if (s.Definition.Shape == SimulationColliderShape.Mesh)
                {
                    AppendMeshHashTail(sb, s.Definition.MeshReference);
                }

                sb.Append('\n');
            }

            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                var hex = new StringBuilder(bytes.Length * 2);
                for (var i = 0; i < bytes.Length; i++)
                {
                    hex.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
                }

                return hex.ToString();
            }
        }

        private static void AppendMeshHashTail(
            StringBuilder sb,
            SimulationMeshReference reference)
        {
            sb.Append("|mesh:").Append(reference.AssetGuid)
                .Append(':').Append(reference.LocalFileId.ToString(CultureInfo.InvariantCulture))
                .Append(':').Append(reference.ContentHash)
                .Append(':').Append(reference.Convex ? '1' : '0');
        }

        private static string Invariant(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string Invariant(Vector3 value)
        {
            return Invariant(value.x) + "," + Invariant(value.y) + "," + Invariant(value.z);
        }

        private static string Invariant(Quaternion value)
        {
            return Invariant(value.x) + "," + Invariant(value.y) + "," +
                   Invariant(value.z) + "," + Invariant(value.w);
        }
    }
}
