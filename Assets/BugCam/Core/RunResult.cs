using System;

namespace BugCam.Core
{
    /// <summary>
    /// One recorded simulation run: state frames plus comparison-safe metadata.
    /// Wall-clock lives only in <see cref="WallClockMs"/> and is excluded from
    /// every comparison and hash.
    /// </summary>
    public readonly struct RunResult
    {
        private static readonly float[] EmptyFrames = Array.Empty<float>();
        private static readonly int[] EmptyStableBodyIds = Array.Empty<int>();

        private RunResult(
            bool succeeded,
            string errorReason,
            float[] stateFrames,
            int[] stableBodyIds,
            float epsilonMetres,
            SimulationPerturbation perturbation,
            int stepCount,
            int seed,
            long wallClockMs)
        {
            Succeeded = succeeded;
            ErrorReason = errorReason;
            StateFrames = stateFrames;
            StableBodyIds = stableBodyIds;
            EpsilonMetres = epsilonMetres;
            Perturbation = perturbation;
            StepCount = stepCount;
            Seed = seed;
            WallClockMs = wallClockMs;
        }

        public bool Succeeded { get; }

        public string ErrorReason { get; }

        public float[] StateFrames { get; }

        public int[] StableBodyIds { get; }

        public int BodyCount => StableBodyIds.Length;

        /// <summary>Perturbation magnitude in metres (same as epsilon for v0.1 position probes).</summary>
        public float EpsilonMetres { get; }

        public SimulationPerturbation Perturbation { get; }

        public int StepCount { get; }

        public float SimulatedTime => StepCount * BugCamConstants.FixedStep;

        /// <summary>Reserved; non-zero only for a randomized perturbation mode.</summary>
        public int Seed { get; }

        /// <summary>Optional wall-clock; never used in comparisons or hashes.</summary>
        public long WallClockMs { get; }

        public static RunResult Success(
            float[] stateFrames,
            int[] stableBodyIds,
            float epsilonMetres,
            SimulationPerturbation perturbation,
            int stepCount,
            int seed = 0,
            long wallClockMs = 0L)
        {
            if (stateFrames == null)
            {
                throw new ArgumentNullException(nameof(stateFrames));
            }

            if (stableBodyIds == null)
            {
                throw new ArgumentNullException(nameof(stableBodyIds));
            }

            if (stepCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(stepCount));
            }

            if (stableBodyIds.Length == 0)
            {
                throw new ArgumentException(
                    "At least one stable body id is required.",
                    nameof(stableBodyIds));
            }

            var expectedLength =
                stepCount * stableBodyIds.Length * BugCamConstants.StateStride;
            if (stateFrames.Length != expectedLength)
            {
                throw new ArgumentException(
                    "StateFrames length must equal stepCount × bodyCount × 14.",
                    nameof(stateFrames));
            }

            return new RunResult(
                true,
                string.Empty,
                stateFrames,
                stableBodyIds,
                epsilonMetres,
                perturbation,
                stepCount,
                seed,
                wallClockMs);
        }

        public static RunResult Failure(string errorReason)
        {
            return new RunResult(
                false,
                errorReason ?? string.Empty,
                EmptyFrames,
                EmptyStableBodyIds,
                0f,
                default,
                0,
                0,
                0L);
        }

        public static RunResult FromSimulationRunResult(
            SimulationRunResult harnessResult,
            int seed = 0,
            long wallClockMs = 0L)
        {
            if (!harnessResult.Succeeded)
            {
                return Failure(harnessResult.ErrorReason);
            }

            var bodyCount = harnessResult.StableBodyIds.Length;
            if (bodyCount == 0)
            {
                return Failure("SimulationRunResult has no stable body ids.");
            }

            var valuesPerStep = bodyCount * BugCamConstants.StateStride;
            if (valuesPerStep == 0 ||
                harnessResult.StateFrames.Length % valuesPerStep != 0)
            {
                return Failure("SimulationRunResult state frames are not a whole number of steps.");
            }

            var stepCount = harnessResult.StateFrames.Length / valuesPerStep;
            return Success(
                harnessResult.StateFrames,
                harnessResult.StableBodyIds,
                harnessResult.AppliedPerturbation.MagnitudeMetres,
                harnessResult.AppliedPerturbation,
                stepCount,
                seed,
                wallClockMs);
        }
    }
}
