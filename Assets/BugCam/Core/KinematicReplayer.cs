using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BugCam.Core
{
    public readonly struct KinematicReplayResult
    {
        private KinematicReplayResult(
            bool succeeded,
            string errorReason,
            float maxComponentDelta,
            bool temporarySceneUnloadRequested)
        {
            Succeeded = succeeded;
            ErrorReason = errorReason;
            MaxComponentDelta = maxComponentDelta;
            TemporarySceneUnloadRequested = temporarySceneUnloadRequested;
        }

        public bool Succeeded { get; }

        public string ErrorReason { get; }

        /// <summary>
        /// Maximum absolute delta across recorded vs read-back transform components
        /// (pos.xyz + rot.xyzw). Block 1.2 VERIFY requires this to be exactly 0.
        /// </summary>
        public float MaxComponentDelta { get; }

        public bool TemporarySceneUnloadRequested { get; }

        internal static KinematicReplayResult Success(
            float maxComponentDelta,
            bool temporarySceneUnloadRequested)
        {
            return new KinematicReplayResult(
                true,
                string.Empty,
                maxComponentDelta,
                temporarySceneUnloadRequested);
        }

        internal static KinematicReplayResult Failure(string errorReason)
        {
            return new KinematicReplayResult(false, errorReason, 0f, false);
        }
    }

    /// <summary>
    /// Frame-by-frame kinematic transform replay used by Block 1.2 VERIFY.
    /// Uses Transform only (no Rigidbody/PhysX write path) so recorded floats can
    /// round-trip with maxComponentDelta == 0.
    /// </summary>
    public static class KinematicReplayer
    {
        private static int nextSceneId;

        public static KinematicReplayResult ReplayTransforms(RunResult run)
        {
            if (!run.Succeeded)
            {
                return KinematicReplayResult.Failure(
                    "Cannot replay a failed RunResult: " + run.ErrorReason);
            }

            return ReplayTransforms(run.StateFrames, run.StepCount, run.BodyCount);
        }

        public static KinematicReplayResult ReplayTransforms(
            float[] recordedFrames,
            int stepCount,
            int bodyCount)
        {
            if (recordedFrames == null)
            {
                return KinematicReplayResult.Failure("Recorded frames are required.");
            }

            if (stepCount <= 0)
            {
                return KinematicReplayResult.Failure("StepCount must be greater than zero.");
            }

            if (bodyCount <= 0)
            {
                return KinematicReplayResult.Failure("BodyCount must be greater than zero.");
            }

            var expectedLength = stepCount * bodyCount * BugCamConstants.StateStride;
            if (recordedFrames.Length != expectedLength)
            {
                return KinematicReplayResult.Failure(
                    "Recorded frames length must equal stepCount × bodyCount × 14.");
            }

            if (!Application.isPlaying)
            {
                return KinematicReplayResult.Failure(
                    "KinematicReplayer requires Play Mode because it creates a temporary replay scene.");
            }

            var replayScene = default(Scene);
            var temporarySceneUnloadRequested = false;
            var pendingResult = KinematicReplayResult.Failure(
                "Kinematic replay did not produce a result.");
            var hasPendingSuccess = false;
            try
            {
                replayScene = SceneManager.CreateScene(
                    "BugCam Kinematic Replay " + nextSceneId++);
                if (!replayScene.IsValid() || !replayScene.isLoaded)
                {
                    return KinematicReplayResult.Failure(
                        "Failed to create a loaded kinematic replay scene.");
                }

                var transforms = new Transform[bodyCount];
                for (var bodyIndex = 0; bodyIndex < bodyCount; bodyIndex++)
                {
                    var gameObject = new GameObject("BugCam Kinematic Body " + bodyIndex);
                    SceneManager.MoveGameObjectToScene(gameObject, replayScene);
                    transforms[bodyIndex] = gameObject.transform;
                }

                var maxComponentDelta = 0f;
                for (var step = 0; step < stepCount; step++)
                {
                    for (var bodyIndex = 0; bodyIndex < bodyCount; bodyIndex++)
                    {
                        var offset =
                            ((step * bodyCount) + bodyIndex) * BugCamConstants.StateStride;
                        var position = new Vector3(
                            recordedFrames[offset],
                            recordedFrames[offset + 1],
                            recordedFrames[offset + 2]);
                        var rotation = new Quaternion(
                            recordedFrames[offset + 3],
                            recordedFrames[offset + 4],
                            recordedFrames[offset + 5],
                            recordedFrames[offset + 6]);

                        var transform = transforms[bodyIndex];
                        transform.SetLocalPositionAndRotation(position, rotation);

                        var readPosition = transform.localPosition;
                        var readRotation = transform.localRotation;
                        maxComponentDelta = MaxAbs(
                            maxComponentDelta,
                            readPosition.x - recordedFrames[offset]);
                        maxComponentDelta = MaxAbs(
                            maxComponentDelta,
                            readPosition.y - recordedFrames[offset + 1]);
                        maxComponentDelta = MaxAbs(
                            maxComponentDelta,
                            readPosition.z - recordedFrames[offset + 2]);
                        maxComponentDelta = MaxAbs(
                            maxComponentDelta,
                            readRotation.x - recordedFrames[offset + 3]);
                        maxComponentDelta = MaxAbs(
                            maxComponentDelta,
                            readRotation.y - recordedFrames[offset + 4]);
                        maxComponentDelta = MaxAbs(
                            maxComponentDelta,
                            readRotation.z - recordedFrames[offset + 5]);
                        maxComponentDelta = MaxAbs(
                            maxComponentDelta,
                            readRotation.w - recordedFrames[offset + 6]);
                    }
                }

                pendingResult = KinematicReplayResult.Success(
                    maxComponentDelta,
                    temporarySceneUnloadRequested: false);
                hasPendingSuccess = true;
            }
            catch (Exception exception)
            {
                return KinematicReplayResult.Failure(
                    "Kinematic replay failed: " + exception.Message);
            }
            finally
            {
                if (replayScene.IsValid() && replayScene.isLoaded)
                {
                    SceneManager.UnloadSceneAsync(replayScene);
                    temporarySceneUnloadRequested = true;
                }
            }

            if (!hasPendingSuccess)
            {
                return pendingResult;
            }

            return KinematicReplayResult.Success(
                pendingResult.MaxComponentDelta,
                temporarySceneUnloadRequested);
        }

        private static float MaxAbs(float current, float delta)
        {
            var absolute = Math.Abs(delta);
            return absolute > current ? absolute : current;
        }
    }
}
