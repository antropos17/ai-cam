using System;
using BugCam.Core;
using UnityEngine;

namespace BugCam.Evidence
{
    /// <summary>
    /// Samples world-space position trajectories from retained <see cref="RunResult"/> frames.
    /// Pure data — no Editor APIs.
    /// </summary>
    public static class GhostTrajectorySampler
    {
        public static Vector3[] SampleBodyPositions(RunResult run, int bodyIndex)
        {
            if (!run.Succeeded ||
                run.StateFrames == null ||
                run.StepCount <= 0 ||
                run.BodyCount <= 0 ||
                bodyIndex < 0 ||
                bodyIndex >= run.BodyCount)
            {
                return Array.Empty<Vector3>();
            }

            var points = new Vector3[run.StepCount];
            for (var step = 0; step < run.StepCount; step++)
            {
                var offset = StateRecorder.IndexOf(
                    0,
                    step,
                    bodyIndex,
                    run.StepCount,
                    run.BodyCount);
                points[step] = new Vector3(
                    run.StateFrames[offset],
                    run.StateFrames[offset + 1],
                    run.StateFrames[offset + 2]);
            }

            return points;
        }

        public static bool TryGetBodyPosition(
            RunResult run,
            int bodyIndex,
            int step,
            out Vector3 position)
        {
            position = default;
            if (!run.Succeeded ||
                run.StateFrames == null ||
                step < 0 ||
                step >= run.StepCount ||
                bodyIndex < 0 ||
                bodyIndex >= run.BodyCount)
            {
                return false;
            }

            var offset = StateRecorder.IndexOf(
                0,
                step,
                bodyIndex,
                run.StepCount,
                run.BodyCount);
            position = new Vector3(
                run.StateFrames[offset],
                run.StateFrames[offset + 1],
                run.StateFrames[offset + 2]);
            return IsFinite(position);
        }

        public static int FindBodyIndex(RunResult run, int bodyId)
        {
            if (!run.Succeeded || run.StableBodyIds == null)
            {
                return -1;
            }

            for (var i = 0; i < run.StableBodyIds.Length; i++)
            {
                if (run.StableBodyIds[i] == bodyId)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
                   !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        }
    }
}
