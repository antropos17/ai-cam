using System;
using UnityEngine;

namespace BugCam.Evidence
{
    /// <summary>
    /// Deterministic geometry helpers for <see cref="EvidenceCameras"/>. Every method here is a
    /// pure function of its inputs — no Unity scene, no live Camera/Collider, no Physics.Raycast —
    /// so candidate scoring reproduces bit-for-bit from recorded run data alone, per docs/PLAN.md
    /// Block 2.1 VERIFY ("reproducible ... by a third party from the same recorded runs").
    /// </summary>
    public readonly struct EvidenceBounds
    {
        public EvidenceBounds(Vector3 center, Vector3 halfExtents)
        {
            Center = center;
            HalfExtents = halfExtents;
        }

        public Vector3 Center { get; }

        /// <summary>Always non-negative on each axis.</summary>
        public Vector3 HalfExtents { get; }

        public Vector3 Min => Center - HalfExtents;

        public Vector3 Max => Center + HalfExtents;

        /// <summary>Radius of the sphere that encloses this box (its half-diagonal length).</summary>
        public float BoundingRadius => HalfExtents.magnitude;

        public static EvidenceBounds Encapsulate(EvidenceBounds a, EvidenceBounds b)
        {
            var min = Vector3.Min(a.Min, b.Min);
            var max = Vector3.Max(a.Max, b.Max);
            return new EvidenceBounds((min + max) * 0.5f, (max - min) * 0.5f);
        }

        /// <summary>
        /// AABB centre plus its 8 corners — the fixed 9-point occlusion sample set
        /// (docs/PLAN.md Block 2.1: "AABB centre + 8 corners"). Destination must hold 9 entries.
        /// </summary>
        public void CopySamplePointsTo(Vector3[] destination)
        {
            if (destination == null || destination.Length != 9)
            {
                throw new ArgumentException(
                    "destination must have exactly 9 entries.",
                    nameof(destination));
            }

            destination[0] = Center;
            var i = 1;
            for (var sx = -1; sx <= 1; sx += 2)
            {
                for (var sy = -1; sy <= 1; sy += 2)
                {
                    for (var sz = -1; sz <= 1; sz += 2)
                    {
                        destination[i] = Center + Vector3.Scale(HalfExtents, new Vector3(sx, sy, sz));
                        i++;
                    }
                }
            }
        }

        /// <summary>
        /// Slab-method ray/AABB intersection. True when the ray from <paramref name="origin"/>
        /// toward <paramref name="direction"/> (unit length) enters this box strictly before
        /// <paramref name="maxDistance"/> — i.e. this box would occlude a target at that distance.
        /// </summary>
        public bool BlocksRay(Vector3 origin, Vector3 direction, float maxDistance)
        {
            var invX = direction.x != 0f ? 1f / direction.x : float.PositiveInfinity;
            var invY = direction.y != 0f ? 1f / direction.y : float.PositiveInfinity;
            var invZ = direction.z != 0f ? 1f / direction.z : float.PositiveInfinity;

            var min = Min;
            var max = Max;

            var tx1 = (min.x - origin.x) * invX;
            var tx2 = (max.x - origin.x) * invX;
            var tMin = Mathf.Min(tx1, tx2);
            var tMax = Mathf.Max(tx1, tx2);

            var ty1 = (min.y - origin.y) * invY;
            var ty2 = (max.y - origin.y) * invY;
            tMin = Mathf.Max(tMin, Mathf.Min(ty1, ty2));
            tMax = Mathf.Min(tMax, Mathf.Max(ty1, ty2));

            var tz1 = (min.z - origin.z) * invZ;
            var tz2 = (max.z - origin.z) * invZ;
            tMin = Mathf.Max(tMin, Mathf.Min(tz1, tz2));
            tMax = Mathf.Min(tMax, Mathf.Max(tz1, tz2));

            // A small epsilon keeps the target body's own surface from "blocking" its own ray.
            const float epsilon = 1e-4f;
            return tMax >= Mathf.Max(tMin, 0f) && tMin < maxDistance - epsilon;
        }
    }

    public static class EvidenceCameraMath
    {
        // pi * (3 - sqrt(5)) — the golden angle in radians, computed in double for determinism
        // across the limited number of operations involved (no accumulation over many steps).
        private const double GoldenAngleRadians = 2.39996322972865332;

        /// <summary>
        /// Deterministic Fibonacci-sphere point <paramref name="index"/> of <paramref name="count"/>
        /// on the unit sphere. No <see cref="UnityEngine.Random"/> anywhere in this path, per
        /// docs/PLAN.md Block 2.1 ("No Random anywhere in the path").
        /// </summary>
        public static Vector3 FibonacciSpherePoint(int index, int count)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (index < 0 || index >= count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var y = 1.0 - (2.0 * index + 1.0) / count;
            var radiusAtY = Math.Sqrt(Math.Max(0.0, 1.0 - y * y));
            var theta = GoldenAngleRadians * index;
            var x = Math.Cos(theta) * radiusAtY;
            var z = Math.Sin(theta) * radiusAtY;
            return new Vector3((float)x, (float)y, (float)z);
        }

        /// <summary>
        /// World-to-camera matrix for a virtual camera at <paramref name="eye"/> looking at
        /// <paramref name="target"/>, matching Unity's <see cref="Camera.worldToCameraMatrix"/>
        /// convention (camera space looks down -Z), built without instantiating a Camera.
        /// Falls back to a world-forward up vector when <paramref name="target"/> sits directly
        /// above or below <paramref name="eye"/> (look direction parallel to world up).
        /// </summary>
        public static Matrix4x4 WorldToCameraMatrix(Vector3 eye, Vector3 target)
        {
            var forward = target - eye;
            if (forward.sqrMagnitude < 1e-10f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            var up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.999f
                ? Vector3.forward
                : Vector3.up;

            var cameraToWorld = Matrix4x4.LookAt(eye, eye + forward, up);
            return Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * cameraToWorld.inverse;
        }

        /// <summary>
        /// Symmetric perspective projection matrix from vertical FOV, aspect, near and far.
        /// </summary>
        public static Matrix4x4 ProjectionMatrix(
            float verticalFovDegrees,
            float aspect,
            float nearClip,
            float farClip)
        {
            return Matrix4x4.Perspective(verticalFovDegrees, aspect, nearClip, farClip);
        }

        /// <summary>
        /// Projects a world point through <paramref name="viewProjection"/> into viewport space
        /// (0..1 on x/y, 0 at bottom-left, matching <see cref="Camera.WorldToViewportPoint"/>).
        /// <paramref name="inFrontOfCamera"/> is false when the point is behind the near plane —
        /// callers must not trust the x/y result in that case.
        /// </summary>
        public static Vector2 WorldToViewport(Matrix4x4 viewProjection, Vector3 worldPoint, out bool inFrontOfCamera)
        {
            // Manual clip-space multiply (not Matrix4x4.MultiplyPoint (3x4), which already
            // divides by w internally — MultiplyPoint4x4 does not) so the single perspective
            // divide below is explicit, deliberate, and not doubled.
            var clipX = (viewProjection.m00 * worldPoint.x) + (viewProjection.m01 * worldPoint.y) +
                        (viewProjection.m02 * worldPoint.z) + viewProjection.m03;
            var clipY = (viewProjection.m10 * worldPoint.x) + (viewProjection.m11 * worldPoint.y) +
                        (viewProjection.m12 * worldPoint.z) + viewProjection.m13;
            var clipW = (viewProjection.m30 * worldPoint.x) + (viewProjection.m31 * worldPoint.y) +
                        (viewProjection.m32 * worldPoint.z) + viewProjection.m33;

            inFrontOfCamera = clipW > 0f;
            if (Mathf.Abs(clipW) < 1e-8f)
            {
                return new Vector2(0.5f, 0.5f);
            }

            var ndcX = clipX / clipW;
            var ndcY = clipY / clipW;
            return new Vector2((ndcX * 0.5f) + 0.5f, (ndcY * 0.5f) + 0.5f);
        }
    }
}
