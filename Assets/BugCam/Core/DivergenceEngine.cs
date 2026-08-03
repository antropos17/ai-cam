using System;
using UnityEngine;

namespace BugCam.Core
{
    /// <summary>
    /// Compares two recorded runs and reports the first sustained divergence.
    ///
    /// Object-scale convention: each body has a characteristic size in metres
    /// (for a box, the largest axis of its local scale). Null scales mean 1 m for every
    /// body. Non-positive or non-finite scales fall back to 1 m so normalized terms never
    /// become NaN/Infinity.
    ///
    /// Per body, per frame:
    ///   posNorm = positionErrorMetres / objectScale
    ///   rotNorm = rotationErrorDegrees / PerBodyRotationThreshold
    ///   velNorm = velocityErrorMetresPerSecond / PerBodyVelocityThreshold
    ///   sleep   = 0 or 1
    ///
    /// Scene Divergence Score (per step) = MAX over tracked bodies of the per-body
    /// weighted norm
    ///   max_i (WeightPosition*posNorm_i + WeightRotation*rotNorm_i
    ///        + WeightVelocity*velNorm_i + WeightSleep*sleep_i)
    /// (Re-ratified 2026-08-03, Block 2.2.1 A3, over measured tower distributions: the
    /// previous sum over bodies reached 3.39 on steps with zero affected bodies on the
    /// 49-body tower, making its threshold vacuous and scene-size-dependent. Max compares
    /// one body's norm regardless of body count. Default SceneScoreThreshold=0.2 — see
    /// the DivergenceSettings why-comment.)
    ///
    /// Significant only when score &gt; SceneScoreThreshold AND at least one body exceeds
    /// PerBodyPositionThreshold AND both hold for SustainedSteps consecutive frames.
    /// firstDivergenceFrame is the first frame of that sustained window.
    /// Rotation/velocity/sleep alone cannot bypass the position condition.
    ///
    /// Zero-epsilon policy: AmplificationDefined=false and Amplification=0 (never Infinity).
    /// </summary>
    public static class DivergenceEngine
    {
        /// <summary>
        /// Compare two single-run state buffers laid out as [steps × bodies × 14].
        /// When <paramref name="stableBodyIds"/> is null, affected ids are body indices.
        /// </summary>
        public static DivergenceResult Analyze(
            float[] baselineFrames,
            float[] perturbedFrames,
            int stepCount,
            int bodyCount,
            float[] bodyScalesMetres,
            float epsilonMetres,
            DivergenceThresholds thresholds,
            int[] stableBodyIds = null)
        {
            if (baselineFrames == null)
            {
                return DivergenceResult.Failure("Baseline state frames are required.");
            }

            if (perturbedFrames == null)
            {
                return DivergenceResult.Failure("Perturbed state frames are required.");
            }

            if (stepCount <= 0)
            {
                return DivergenceResult.Failure("StepCount must be greater than zero.");
            }

            if (bodyCount <= 0)
            {
                return DivergenceResult.Failure("BodyCount must be greater than zero.");
            }

            if (BugCamConstants.StateStride != 14)
            {
                return DivergenceResult.Failure("State stride must be exactly 14 floats.");
            }

            var expectedLength = stepCount * bodyCount * BugCamConstants.StateStride;
            if (baselineFrames.Length != expectedLength ||
                perturbedFrames.Length != expectedLength)
            {
                return DivergenceResult.Failure(
                    "Both runs must hold exactly stepCount × bodyCount × 14 floats.");
            }

            if (bodyScalesMetres != null && bodyScalesMetres.Length != bodyCount)
            {
                return DivergenceResult.Failure(
                    "bodyScalesMetres must be null or hold one entry per tracked body.");
            }

            if (stableBodyIds != null && stableBodyIds.Length != bodyCount)
            {
                return DivergenceResult.Failure(
                    "stableBodyIds must be null or hold one entry per tracked body.");
            }

            if (float.IsNaN(epsilonMetres) || float.IsInfinity(epsilonMetres) ||
                epsilonMetres < 0f)
            {
                return DivergenceResult.Failure(
                    "epsilonMetres must be a non-negative finite number of metres.");
            }

            var thresholdError = thresholds.Validate();
            if (!string.IsNullOrEmpty(thresholdError))
            {
                return DivergenceResult.Failure(thresholdError);
            }

            var perBodyMaxPositionError = new float[bodyCount];
            var sceneScorePerStep = new float[stepCount];

            var maxSpreadMetres = 0f;
            var maxSpreadStep = -1;
            var maxSpreadBodyIndex = -1;

            var firstDivergenceFrame = -1;
            var qualifyingRunStart = -1;
            var qualifyingRunLength = 0;

            for (var step = 0; step < stepCount; step++)
            {
                var sceneScore = 0f;
                var stepAffectedBodies = 0;

                for (var bodyIndex = 0; bodyIndex < bodyCount; bodyIndex++)
                {
                    var offset = StateRecorder.IndexOf(
                        0,
                        step,
                        bodyIndex,
                        stepCount,
                        bodyCount);

                    var positionError = Magnitude(
                        baselineFrames[offset] - perturbedFrames[offset],
                        baselineFrames[offset + 1] - perturbedFrames[offset + 1],
                        baselineFrames[offset + 2] - perturbedFrames[offset + 2]);

                    if (!AreQuaternionComponentsFinite(
                            baselineFrames[offset + 3],
                            baselineFrames[offset + 4],
                            baselineFrames[offset + 5],
                            baselineFrames[offset + 6],
                            perturbedFrames[offset + 3],
                            perturbedFrames[offset + 4],
                            perturbedFrames[offset + 5],
                            perturbedFrames[offset + 6]))
                    {
                        return DivergenceResult.Failure(
                            "Recorded state contains non-finite quaternion components; comparison aborted at step " +
                            step + ", body " + bodyIndex + ".");
                    }

                    var rotationErrorDegrees = QuaternionAngleDegrees(
                        baselineFrames[offset + 3],
                        baselineFrames[offset + 4],
                        baselineFrames[offset + 5],
                        baselineFrames[offset + 6],
                        perturbedFrames[offset + 3],
                        perturbedFrames[offset + 4],
                        perturbedFrames[offset + 5],
                        perturbedFrames[offset + 6]);

                    var velocityError = Magnitude(
                        baselineFrames[offset + 7] - perturbedFrames[offset + 7],
                        baselineFrames[offset + 8] - perturbedFrames[offset + 8],
                        baselineFrames[offset + 9] - perturbedFrames[offset + 9]);

                    var sleepMismatch =
                        baselineFrames[offset + 13] == perturbedFrames[offset + 13] ? 0f : 1f;

                    if (!IsFinite(positionError) ||
                        !IsFinite(rotationErrorDegrees) ||
                        !IsFinite(velocityError))
                    {
                        return DivergenceResult.Failure(
                            "Recorded state contains non-finite values; comparison aborted at step " +
                            step + ", body " + bodyIndex + ".");
                    }

                    var objectScale = ResolveObjectScale(bodyScalesMetres, bodyIndex);
                    var posNorm = SanitizeNormalized(positionError / objectScale, positionError);
                    var rotNorm = SanitizeNormalized(
                        rotationErrorDegrees / thresholds.PerBodyRotationThreshold,
                        0f);
                    var velNorm = SanitizeNormalized(
                        velocityError / thresholds.PerBodyVelocityThreshold,
                        0f);

                    var bodyNorm =
                        thresholds.WeightPosition * posNorm +
                        thresholds.WeightRotation * rotNorm +
                        thresholds.WeightVelocity * velNorm +
                        thresholds.WeightSleep * sleepMismatch;
                    if (bodyNorm > sceneScore)
                    {
                        sceneScore = bodyNorm;
                    }

                    if (positionError > thresholds.PerBodyPositionThreshold)
                    {
                        stepAffectedBodies++;
                    }

                    if (positionError > perBodyMaxPositionError[bodyIndex])
                    {
                        perBodyMaxPositionError[bodyIndex] = positionError;
                    }

                    if (positionError > maxSpreadMetres)
                    {
                        maxSpreadMetres = positionError;
                        maxSpreadStep = step;
                        maxSpreadBodyIndex = bodyIndex;
                    }
                }

                if (!IsFinite(sceneScore))
                {
                    return DivergenceResult.Failure(
                        "Scene Divergence Score became non-finite at step " + step + ".");
                }

                sceneScorePerStep[step] = sceneScore;

                var stepQualifies =
                    sceneScore > thresholds.SceneScoreThreshold && stepAffectedBodies >= 1;

                if (stepQualifies)
                {
                    if (qualifyingRunLength == 0)
                    {
                        qualifyingRunStart = step;
                    }

                    qualifyingRunLength++;
                    if (firstDivergenceFrame < 0 &&
                        qualifyingRunLength >= thresholds.SustainedSteps)
                    {
                        // First frame of the sustained window, not the confirmation frame.
                        firstDivergenceFrame = qualifyingRunStart;
                    }
                }
                else
                {
                    qualifyingRunLength = 0;
                    qualifyingRunStart = -1;
                }
            }

            var affectedBodyIds = CollectAffectedBodyIds(
                perBodyMaxPositionError,
                thresholds.PerBodyPositionThreshold,
                stableBodyIds);

            var maxSpreadBodyId = maxSpreadBodyIndex < 0
                ? -1
                : ResolveBodyId(stableBodyIds, maxSpreadBodyIndex);

            var firstDivergenceBodyId = ResolveFirstDivergenceBodyId(
                baselineFrames,
                perturbedFrames,
                stepCount,
                bodyCount,
                firstDivergenceFrame,
                stableBodyIds);

            var amplificationDefined = epsilonMetres > 0f;
            var amplification = 0f;
            if (amplificationDefined)
            {
                amplification = maxSpreadMetres / epsilonMetres;
                if (!IsFinite(amplification))
                {
                    return DivergenceResult.Failure(
                        "Amplification became non-finite; check epsilon and max spread.");
                }
            }

            return DivergenceResult.Success(
                stepCount,
                bodyCount,
                epsilonMetres,
                firstDivergenceFrame >= 0,
                firstDivergenceFrame,
                firstDivergenceBodyId,
                maxSpreadMetres,
                maxSpreadStep,
                maxSpreadBodyId,
                affectedBodyIds,
                amplificationDefined,
                amplification,
                perBodyMaxPositionError,
                sceneScorePerStep);
        }

        /// <summary>
        /// Body with largest |Δpos| at the first sustained-divergence frame.
        /// Tie-break: lower bodyIndex wins. Independent of global MaxSpreadBodyId.
        /// </summary>
        private static int ResolveFirstDivergenceBodyId(
            float[] baselineFrames,
            float[] perturbedFrames,
            int stepCount,
            int bodyCount,
            int firstDivergenceFrame,
            int[] stableBodyIds)
        {
            if (firstDivergenceFrame < 0 || firstDivergenceFrame >= stepCount)
            {
                return -1;
            }

            var bestError = -1f;
            var bestBodyIndex = -1;
            for (var bodyIndex = 0; bodyIndex < bodyCount; bodyIndex++)
            {
                var offset = StateRecorder.IndexOf(
                    0,
                    firstDivergenceFrame,
                    bodyIndex,
                    stepCount,
                    bodyCount);
                var positionError = Magnitude(
                    baselineFrames[offset] - perturbedFrames[offset],
                    baselineFrames[offset + 1] - perturbedFrames[offset + 1],
                    baselineFrames[offset + 2] - perturbedFrames[offset + 2]);
                if (!IsFinite(positionError))
                {
                    continue;
                }

                // Strict greater: equal errors keep the lower bodyIndex (ascending tie-break).
                if (bestBodyIndex < 0 || positionError > bestError)
                {
                    bestError = positionError;
                    bestBodyIndex = bodyIndex;
                }
            }

            return bestBodyIndex < 0
                ? -1
                : ResolveBodyId(stableBodyIds, bestBodyIndex);
        }

        /// <summary>
        /// Compare two recorded runs. Epsilon comes from the perturbed run metadata.
        /// </summary>
        public static DivergenceResult Analyze(
            RunResult baseline,
            RunResult perturbed,
            float[] bodyScalesMetres,
            DivergenceThresholds thresholds)
        {
            if (!baseline.Succeeded)
            {
                return DivergenceResult.Failure(
                    "Baseline run did not succeed: " + baseline.ErrorReason);
            }

            if (!perturbed.Succeeded)
            {
                return DivergenceResult.Failure(
                    "Perturbed run did not succeed: " + perturbed.ErrorReason);
            }

            if (baseline.StateFrames == null || perturbed.StateFrames == null)
            {
                return DivergenceResult.Failure("Frame arrays must be non-null.");
            }

            if (baseline.StepCount != perturbed.StepCount)
            {
                return DivergenceResult.Failure(
                    "Both runs must record the same number of steps.");
            }

            if (baseline.BodyCount != perturbed.BodyCount)
            {
                return DivergenceResult.Failure(
                    "Both runs must record the same number of bodies.");
            }

            for (var bodyIndex = 0; bodyIndex < baseline.BodyCount; bodyIndex++)
            {
                if (baseline.StableBodyIds[bodyIndex] != perturbed.StableBodyIds[bodyIndex])
                {
                    return DivergenceResult.Failure(
                        "Stable body ids differ between the two runs; comparison would be meaningless.");
                }
            }

            return Analyze(
                baseline.StateFrames,
                perturbed.StateFrames,
                baseline.StepCount,
                baseline.BodyCount,
                bodyScalesMetres,
                perturbed.EpsilonMetres,
                thresholds,
                baseline.StableBodyIds);
        }

        /// <summary>
        /// Convenience overload using <see cref="DivergenceSettings"/> code defaults.
        /// </summary>
        public static DivergenceResult Analyze(
            RunResult baseline,
            RunResult perturbed,
            float[] bodyScalesMetres = null)
        {
            return Analyze(
                baseline,
                perturbed,
                bodyScalesMetres,
                DivergenceThresholds.Default);
        }

        private static float ResolveObjectScale(float[] bodyScalesMetres, int bodyIndex)
        {
            if (bodyScalesMetres == null)
            {
                return 1f;
            }

            var declaredScale = bodyScalesMetres[bodyIndex];
            if (declaredScale > 0f && IsFinite(declaredScale))
            {
                return declaredScale;
            }

            // Zero / near-invalid scale: fall back to 1 m so posNorm stays finite.
            return 1f;
        }

        private static float SanitizeNormalized(float value, float fallback)
        {
            return IsFinite(value) ? value : fallback;
        }

        private static int[] CollectAffectedBodyIds(
            float[] perBodyMaxPositionError,
            float positionThreshold,
            int[] stableBodyIds)
        {
            var count = 0;
            for (var bodyIndex = 0; bodyIndex < perBodyMaxPositionError.Length; bodyIndex++)
            {
                if (perBodyMaxPositionError[bodyIndex] > positionThreshold)
                {
                    count++;
                }
            }

            var ids = new int[count];
            var write = 0;
            for (var bodyIndex = 0; bodyIndex < perBodyMaxPositionError.Length; bodyIndex++)
            {
                if (perBodyMaxPositionError[bodyIndex] > positionThreshold)
                {
                    ids[write++] = ResolveBodyId(stableBodyIds, bodyIndex);
                }
            }

            // Insertion sort keeps order deterministic without Dictionary iteration.
            for (var i = 1; i < ids.Length; i++)
            {
                var value = ids[i];
                var j = i;
                while (j > 0 && ids[j - 1] > value)
                {
                    ids[j] = ids[j - 1];
                    j--;
                }

                ids[j] = value;
            }

            return ids;
        }

        private static int ResolveBodyId(int[] stableBodyIds, int bodyIndex)
        {
            return stableBodyIds != null ? stableBodyIds[bodyIndex] : bodyIndex;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool AreQuaternionComponentsFinite(
            float ax,
            float ay,
            float az,
            float aw,
            float bx,
            float by,
            float bz,
            float bw)
        {
            return IsFinite(ax) && IsFinite(ay) && IsFinite(az) && IsFinite(aw) &&
                   IsFinite(bx) && IsFinite(by) && IsFinite(bz) && IsFinite(bw);
        }

        private static float Magnitude(float x, float y, float z)
        {
            return Mathf.Sqrt((x * x) + (y * y) + (z * z));
        }

        /// <summary>
        /// Quaternion angular difference in degrees. Treats q and -q as identical.
        /// Caller must ensure all eight components are finite before calling.
        /// </summary>
        private static float QuaternionAngleDegrees(
            float ax,
            float ay,
            float az,
            float aw,
            float bx,
            float by,
            float bz,
            float bw)
        {
            var dot = (ax * bx) + (ay * by) + (az * bz) + (aw * bw);
            if (dot < 0f)
            {
                dot = -dot;
            }

            // Finite overshoot from floating-point noise only — never Inf→1→0°.
            if (dot > 1f)
            {
                dot = 1f;
            }

            return 2f * Mathf.Acos(dot) * Mathf.Rad2Deg;
        }
    }
}
