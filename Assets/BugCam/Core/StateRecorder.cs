using System;
using UnityEngine;

namespace BugCam.Core
{
    /// <summary>
    /// Flat preallocated state storage: [runs × steps × bodies × 14].
    /// Field order per body: pos.xyz, rot.xyzw, vel.xyz, angVel.xyz, sleeping(0f|1f).
    /// </summary>
    public sealed class StateRecorder
    {
        private readonly float[] buffer;

        private StateRecorder(float[] buffer, int runCount, int stepCount, int bodyCount)
        {
            this.buffer = buffer;
            RunCount = runCount;
            StepCount = stepCount;
            BodyCount = bodyCount;
        }

        public int RunCount { get; }

        public int StepCount { get; }

        public int BodyCount { get; }

        public float[] Buffer => buffer;

        public int ValuesPerRun => StepCount * BodyCount * BugCamConstants.StateStride;

        public static int BufferLength(int runCount, int stepCount, int bodyCount)
        {
            return checked(runCount * stepCount * bodyCount * BugCamConstants.StateStride);
        }

        public static int IndexOf(
            int runIndex,
            int step,
            int bodyIndex,
            int stepCount,
            int bodyCount)
        {
            return (((runIndex * stepCount) + step) * bodyCount + bodyIndex) *
                   BugCamConstants.StateStride;
        }

        public static StateRecorder Allocate(int runCount, int stepCount, int bodyCount)
        {
            if (runCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(runCount));
            }

            if (stepCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(stepCount));
            }

            if (bodyCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bodyCount));
            }

            return new StateRecorder(
                new float[BufferLength(runCount, stepCount, bodyCount)],
                runCount,
                stepCount,
                bodyCount);
        }

        public int IndexOf(int runIndex, int step, int bodyIndex)
        {
            ValidateIndices(runIndex, step, bodyIndex);
            return IndexOf(runIndex, step, bodyIndex, StepCount, BodyCount);
        }

        public void WriteBody(
            int runIndex,
            int step,
            int bodyIndex,
            Vector3 position,
            Quaternion rotation,
            Vector3 linearVelocity,
            Vector3 angularVelocity,
            bool sleeping)
        {
            var offset = IndexOf(runIndex, step, bodyIndex);
            var normalizedRotation = rotation.normalized;
            buffer[offset] = position.x;
            buffer[offset + 1] = position.y;
            buffer[offset + 2] = position.z;
            buffer[offset + 3] = normalizedRotation.x;
            buffer[offset + 4] = normalizedRotation.y;
            buffer[offset + 5] = normalizedRotation.z;
            buffer[offset + 6] = normalizedRotation.w;
            buffer[offset + 7] = linearVelocity.x;
            buffer[offset + 8] = linearVelocity.y;
            buffer[offset + 9] = linearVelocity.z;
            buffer[offset + 10] = angularVelocity.x;
            buffer[offset + 11] = angularVelocity.y;
            buffer[offset + 12] = angularVelocity.z;
            buffer[offset + 13] = sleeping ? 1f : 0f;
        }

        public void WriteRigidbodies(int runIndex, int step, Rigidbody[] bodies)
        {
            if (bodies == null)
            {
                throw new ArgumentNullException(nameof(bodies));
            }

            if (bodies.Length != BodyCount)
            {
                throw new ArgumentException(
                    "Rigidbody count must match the recorder BodyCount.",
                    nameof(bodies));
            }

            ValidateIndices(runIndex, step, 0);
            for (var bodyIndex = 0; bodyIndex < bodies.Length; bodyIndex++)
            {
                var body = bodies[bodyIndex];
                WriteBody(
                    runIndex,
                    step,
                    bodyIndex,
                    body.position,
                    body.rotation,
                    body.linearVelocity,
                    body.angularVelocity,
                    body.IsSleeping());
            }
        }

        public void CopyRunTo(int runIndex, float[] destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            if (runIndex < 0 || runIndex >= RunCount)
            {
                throw new ArgumentOutOfRangeException(nameof(runIndex));
            }

            if (destination.Length < ValuesPerRun)
            {
                throw new ArgumentException(
                    "Destination must hold at least one full run.",
                    nameof(destination));
            }

            var sourceOffset = runIndex * ValuesPerRun;
            Array.Copy(buffer, sourceOffset, destination, 0, ValuesPerRun);
        }

        public float[] CreateRunCopy(int runIndex)
        {
            var copy = new float[ValuesPerRun];
            CopyRunTo(runIndex, copy);
            return copy;
        }

        private void ValidateIndices(int runIndex, int step, int bodyIndex)
        {
            if (runIndex < 0 || runIndex >= RunCount)
            {
                throw new ArgumentOutOfRangeException(nameof(runIndex));
            }

            if (step < 0 || step >= StepCount)
            {
                throw new ArgumentOutOfRangeException(nameof(step));
            }

            if (bodyIndex < 0 || bodyIndex >= BodyCount)
            {
                throw new ArgumentOutOfRangeException(nameof(bodyIndex));
            }
        }
    }
}
