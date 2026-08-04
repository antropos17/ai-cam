using System;
using BugCam.Core;
using UnityEditor;
using UnityEngine;

namespace BugCam.Editor
{
    /// <summary>
    /// Block 2.2.2 Amendment 2026-08-04 run-path invariant: the authoritative scene
    /// capture (geometry reads + contentHash) happens in EDIT MODE before Play Mode
    /// entry, and the result is handed to the runner across the domain reload through
    /// SessionState. The runner never re-captures in Play Mode; a scene-kind run that
    /// finds no handover fails closed with SCENE_MESH_RESOLVE_FAILED.
    /// Floats round-trip exactly (pinned by SceneCaptureHandoverRoundTripsBitIdentically)
    /// — any drift here would shift simulation inputs and break the domino gate pins.
    /// </summary>
    internal static class SceneCaptureHandover
    {
        private const string Key = "BugCam.GhostHost.SceneCaptureHandover";

        [Serializable]
        private sealed class MeshRefDto
        {
            public bool isSet;
            public string assetGuid;
            public long localFileId;
            public string meshName;
            public string contentHash;
            public bool convex;
            public int vertexCount;
            public int subMeshCount;
            public Vector3 boundsCenter;
            public Vector3 boundsSize;
        }

        [Serializable]
        private sealed class BodyDto
        {
            public int stableId;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 size;
            public float mass;
            public Vector3 velocity;
            public int shape;
            public MeshRefDto meshRef;
            public Vector3 fullScale;
        }

        [Serializable]
        private sealed class StaticDto
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 size;
            public int shape;
            public MeshRefDto meshRef;
            public Vector3 fullScale;
        }

        [Serializable]
        private sealed class RecordDto
        {
            public string hierarchyPath;
            public string orderKey;
            public int status;
            public int stableId;
            public string reason;
            public MeshRefDto meshRef;
        }

        [Serializable]
        private sealed class CaptureDto
        {
            public bool succeeded;
            public string failureSummary;
            public string scenePath;
            public string captureHash;
            public string[] kinematicFreezeWarnings;
            public string[] sleepingBodyWarnings;
            public BodyDto[] bodies;
            public StaticDto[] statics;
            public RecordDto[] records;
        }

        public static void Store(in SceneCaptureResult capture)
        {
            SessionState.SetString(Key, ToJson(capture));
        }

        public static void Clear()
        {
            SessionState.SetString(Key, string.Empty);
        }

        public static bool TryRead(out SceneCaptureResult capture)
        {
            var json = SessionState.GetString(Key, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                capture = default;
                return false;
            }

            capture = FromJson(json);
            return true;
        }

        internal static string ToJson(in SceneCaptureResult capture)
        {
            var dto = new CaptureDto
            {
                succeeded = capture.Succeeded,
                failureSummary = capture.FailureSummary,
                scenePath = capture.ScenePath,
                captureHash = capture.CaptureHash,
                kinematicFreezeWarnings = capture.KinematicFreezeWarnings,
                sleepingBodyWarnings = capture.SleepingBodyWarnings,
                bodies = new BodyDto[capture.Bodies.Length],
                statics = new StaticDto[capture.StaticColliders.Length],
                records = new RecordDto[capture.Objects.Length]
            };
            for (var i = 0; i < capture.Bodies.Length; i++)
            {
                var body = capture.Bodies[i];
                dto.bodies[i] = new BodyDto
                {
                    stableId = body.StableId,
                    position = body.Position,
                    rotation = body.Rotation,
                    size = body.Size,
                    mass = body.Mass,
                    velocity = body.InitialLinearVelocity,
                    shape = (int)body.Shape,
                    meshRef = ToDto(body.MeshReference),
                    fullScale = body.FullScale
                };
            }

            for (var i = 0; i < capture.StaticColliders.Length; i++)
            {
                var staticCollider = capture.StaticColliders[i];
                dto.statics[i] = new StaticDto
                {
                    position = staticCollider.Position,
                    rotation = staticCollider.Rotation,
                    size = staticCollider.Size,
                    shape = (int)staticCollider.Shape,
                    meshRef = ToDto(staticCollider.MeshReference),
                    fullScale = staticCollider.FullScale
                };
            }

            for (var i = 0; i < capture.Objects.Length; i++)
            {
                var record = capture.Objects[i];
                dto.records[i] = new RecordDto
                {
                    hierarchyPath = record.HierarchyPath,
                    orderKey = record.OrderKey,
                    status = (int)record.Status,
                    stableId = record.StableId,
                    reason = record.Reason,
                    meshRef = ToDto(record.MeshReference)
                };
            }

            return JsonUtility.ToJson(dto);
        }

        internal static SceneCaptureResult FromJson(string json)
        {
            var dto = JsonUtility.FromJson<CaptureDto>(json);
            var records = new SceneCaptureObjectRecord[
                dto.records == null ? 0 : dto.records.Length];
            for (var i = 0; i < records.Length; i++)
            {
                var record = dto.records[i];
                records[i] = new SceneCaptureObjectRecord(
                    record.hierarchyPath,
                    record.orderKey,
                    (SceneCaptureObjectStatus)record.status,
                    record.stableId,
                    record.reason,
                    FromDto(record.meshRef));
            }

            if (!dto.succeeded)
            {
                return SceneCaptureResult.Failure(
                    dto.failureSummary,
                    dto.scenePath,
                    records,
                    dto.captureHash);
            }

            var bodies = new SimulationBodyDefinition[
                dto.bodies == null ? 0 : dto.bodies.Length];
            for (var i = 0; i < bodies.Length; i++)
            {
                var body = dto.bodies[i];
                bodies[i] = new SimulationBodyDefinition(
                    body.stableId,
                    body.position,
                    body.rotation,
                    body.size,
                    body.mass,
                    body.velocity,
                    (SimulationColliderShape)body.shape,
                    FromDto(body.meshRef),
                    body.fullScale);
            }

            var statics = new SimulationStaticColliderDefinition[
                dto.statics == null ? 0 : dto.statics.Length];
            for (var i = 0; i < statics.Length; i++)
            {
                var staticCollider = dto.statics[i];
                statics[i] = new SimulationStaticColliderDefinition(
                    staticCollider.position,
                    staticCollider.rotation,
                    staticCollider.size,
                    (SimulationColliderShape)staticCollider.shape,
                    FromDto(staticCollider.meshRef),
                    staticCollider.fullScale);
            }

            return SceneCaptureResult.Success(
                dto.scenePath,
                bodies,
                statics,
                records,
                dto.kinematicFreezeWarnings,
                dto.sleepingBodyWarnings,
                dto.captureHash);
        }

        private static MeshRefDto ToDto(in SimulationMeshReference reference)
        {
            if (!reference.IsSet)
            {
                return new MeshRefDto();
            }

            return new MeshRefDto
            {
                isSet = true,
                assetGuid = reference.AssetGuid,
                localFileId = reference.LocalFileId,
                meshName = reference.MeshName,
                contentHash = reference.ContentHash,
                convex = reference.Convex,
                vertexCount = reference.VertexCount,
                subMeshCount = reference.SubMeshCount,
                boundsCenter = reference.BoundsCenter,
                boundsSize = reference.BoundsSize
            };
        }

        private static SimulationMeshReference FromDto(MeshRefDto dto)
        {
            if (dto == null || !dto.isSet)
            {
                return default;
            }

            return new SimulationMeshReference(
                dto.assetGuid,
                dto.localFileId,
                dto.meshName,
                dto.contentHash,
                dto.convex,
                dto.vertexCount,
                dto.subMeshCount,
                dto.boundsCenter,
                dto.boundsSize);
        }
    }
}
