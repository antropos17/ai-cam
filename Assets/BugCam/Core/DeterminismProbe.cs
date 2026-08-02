using System;

namespace BugCam.Core
{
    public enum SimulationThreadingMode
    {
        MultiThreaded = 0,
        SingleThreaded = 1
    }

    internal readonly struct RepeatabilityMetrics
    {
        public RepeatabilityMetrics(
            bool bitwiseEqual,
            float maxComponentDelta,
            bool withinGate)
        {
            BitwiseEqual = bitwiseEqual;
            MaxComponentDelta = maxComponentDelta;
            WithinGate = withinGate;
        }

        public bool BitwiseEqual { get; }

        public float MaxComponentDelta { get; }

        public bool WithinGate { get; }
    }

    internal static class RepeatabilityMetricsCalculator
    {
        internal static RepeatabilityMetrics Calculate(float[] first, float[] second)
        {
            var bitwiseEqual = true;
            var maxComponentDelta = 0f;
            for (var valueIndex = 0; valueIndex < first.Length; valueIndex++)
            {
                var firstValue = first[valueIndex];
                var secondValue = second[valueIndex];
                if (BitConverter.SingleToInt32Bits(firstValue) !=
                    BitConverter.SingleToInt32Bits(secondValue))
                {
                    bitwiseEqual = false;
                }

                var componentDelta = Math.Abs(firstValue - secondValue);
                if (componentDelta > maxComponentDelta)
                {
                    maxComponentDelta = componentDelta;
                }
            }

            return new RepeatabilityMetrics(
                bitwiseEqual,
                maxComponentDelta,
                maxComponentDelta <= BugCamConstants.RepeatabilityGate);
        }
    }

    public readonly struct DeterminismProbeResult
    {
        private DeterminismProbeResult(
            bool succeeded,
            string errorReason,
            int bodyCount,
            int stepCount,
            bool repeatBitwiseEqual,
            bool repeatWithinGate,
            float repeatMaxComponentDelta,
            int repeatFirstDivergingStep,
            int repeatFirstDivergingBody,
            bool perturbedBitwiseEqual,
            bool perturbedWithinGate,
            float perturbedMaxComponentDelta,
            int perturbedFirstDivergingStep,
            int perturbedFirstDivergingBody,
            long managedBytesAllocatedInLoop,
            bool localPhysicsSceneValid,
            bool temporaryScenesUnloadRequested,
            SimulationThreadingMode simulationThreadingMode)
        {
            Succeeded = succeeded;
            ErrorReason = errorReason;
            BodyCount = bodyCount;
            StepCount = stepCount;
            RepeatBitwiseEqual = repeatBitwiseEqual;
            RepeatWithinGate = repeatWithinGate;
            RepeatMaxComponentDelta = repeatMaxComponentDelta;
            RepeatFirstDivergingStep = repeatFirstDivergingStep;
            RepeatFirstDivergingBody = repeatFirstDivergingBody;
            PerturbedBitwiseEqual = perturbedBitwiseEqual;
            PerturbedWithinGate = perturbedWithinGate;
            PerturbedMaxComponentDelta = perturbedMaxComponentDelta;
            PerturbedFirstDivergingStep = perturbedFirstDivergingStep;
            PerturbedFirstDivergingBody = perturbedFirstDivergingBody;
            ManagedBytesAllocatedInLoop = managedBytesAllocatedInLoop;
            LocalPhysicsSceneValid = localPhysicsSceneValid;
            TemporaryScenesUnloadRequested = temporaryScenesUnloadRequested;
            SimulationThreadingMode = simulationThreadingMode;
        }

        public bool Succeeded { get; }

        public string ErrorReason { get; }

        public int BodyCount { get; }

        public int StepCount { get; }

        public bool RepeatBitwiseEqual { get; }

        public bool RepeatWithinGate { get; }

        public float RepeatMaxComponentDelta { get; }

        public int RepeatFirstDivergingStep { get; }

        public int RepeatFirstDivergingBody { get; }

        public bool PerturbedBitwiseEqual { get; }

        public bool PerturbedWithinGate { get; }

        public float PerturbedMaxComponentDelta { get; }

        public int PerturbedFirstDivergingStep { get; }

        public int PerturbedFirstDivergingBody { get; }

        public long ManagedBytesAllocatedInLoop { get; }

        public bool LocalPhysicsSceneValid { get; }

        public bool TemporaryScenesUnloadRequested { get; }

        public SimulationThreadingMode SimulationThreadingMode { get; }

        internal static DeterminismProbeResult Success(
            int bodyCount,
            int stepCount,
            bool repeatBitwiseEqual,
            bool repeatWithinGate,
            float repeatMaxComponentDelta,
            int repeatFirstDivergingStep,
            int repeatFirstDivergingBody,
            bool perturbedBitwiseEqual,
            bool perturbedWithinGate,
            float perturbedMaxComponentDelta,
            int perturbedFirstDivergingStep,
            int perturbedFirstDivergingBody,
            long managedBytesAllocatedInLoop,
            bool localPhysicsSceneValid,
            bool temporaryScenesUnloadRequested,
            SimulationThreadingMode simulationThreadingMode)
        {
            return new DeterminismProbeResult(
                true,
                string.Empty,
                bodyCount,
                stepCount,
                repeatBitwiseEqual,
                repeatWithinGate,
                repeatMaxComponentDelta,
                repeatFirstDivergingStep,
                repeatFirstDivergingBody,
                perturbedBitwiseEqual,
                perturbedWithinGate,
                perturbedMaxComponentDelta,
                perturbedFirstDivergingStep,
                perturbedFirstDivergingBody,
                managedBytesAllocatedInLoop,
                localPhysicsSceneValid,
                temporaryScenesUnloadRequested,
                simulationThreadingMode);
        }

        internal static DeterminismProbeResult Failure(string errorReason)
        {
            return new DeterminismProbeResult(
                false,
                errorReason,
                0,
                0,
                false,
                false,
                0f,
                -1,
                -1,
                false,
                false,
                0f,
                -1,
                -1,
                0L,
                false,
                false,
                default);
        }
    }

    public sealed class DeterminismProbe
    {
        public DeterminismProbeResult Run(
            SimulationRequest baselineRequest,
            SimulationRequest perturbedRequest,
            SimulationThreadingMode simulationThreadingMode)
        {
            if (!Enum.IsDefined(typeof(SimulationThreadingMode), simulationThreadingMode))
            {
                return DeterminismProbeResult.Failure(
                    "Simulation threading mode is not supported.");
            }

            var harness = new SimulationHarness();
            var resultA = harness.Run(baselineRequest);
            if (!resultA.Succeeded)
            {
                return DeterminismProbeResult.Failure("Baseline A failed: " + resultA.ErrorReason);
            }

            var resultB = harness.Run(perturbedRequest);
            if (!resultB.Succeeded)
            {
                return DeterminismProbeResult.Failure("Perturbed B failed: " + resultB.ErrorReason);
            }

            var resultAPrime = harness.Run(baselineRequest);
            if (!resultAPrime.Succeeded)
            {
                return DeterminismProbeResult.Failure(
                    "Baseline A-prime failed: " + resultAPrime.ErrorReason);
            }

            if (!HaveMatchingStableBodyIds(
                    resultA.StableBodyIds,
                    resultB.StableBodyIds) ||
                !HaveMatchingStableBodyIds(
                    resultA.StableBodyIds,
                    resultAPrime.StableBodyIds))
            {
                return DeterminismProbeResult.Failure(
                    "A/B/A-prime StableBodyIds must match.");
            }

            if (resultA.StateFrames.Length != resultAPrime.StateFrames.Length ||
                resultA.StateFrames.Length != resultB.StateFrames.Length)
            {
                return DeterminismProbeResult.Failure(
                    "A/B/A-prime state frame lengths must match.");
            }

            if (!TryValidateFiniteFrames(resultA.StateFrames, "A", out var finiteError) ||
                !TryValidateFiniteFrames(resultB.StateFrames, "B", out finiteError) ||
                !TryValidateFiniteFrames(resultAPrime.StateFrames, "A-prime", out finiteError))
            {
                return DeterminismProbeResult.Failure(finiteError);
            }

            var stableBodyIds = GetStableBodyIds(baselineRequest.Bodies);
            if (stableBodyIds.Length == 0 ||
                resultA.StateFrames.Length !=
                baselineRequest.StepCount * stableBodyIds.Length * BugCamConstants.StateStride)
            {
                return DeterminismProbeResult.Failure(
                    "State frames do not match the baseline request dimensions.");
            }

            var repeatability = RepeatabilityMetricsCalculator.Calculate(
                resultA.StateFrames,
                resultAPrime.StateFrames);
            FindFirstDivergence(
                resultA.StateFrames,
                resultAPrime.StateFrames,
                stableBodyIds,
                out var repeatFirstStep,
                out var repeatFirstBody);

            var perturbed = RepeatabilityMetricsCalculator.Calculate(
                resultA.StateFrames,
                resultB.StateFrames);
            FindFirstDivergence(
                resultA.StateFrames,
                resultB.StateFrames,
                stableBodyIds,
                out var perturbedFirstStep,
                out var perturbedFirstBody);

            var managedBytesAllocatedInLoop = Math.Max(
                resultA.ManagedBytesAllocatedInLoop,
                Math.Max(
                    resultB.ManagedBytesAllocatedInLoop,
                    resultAPrime.ManagedBytesAllocatedInLoop));

            var localPhysicsSceneValid =
                resultA.LocalPhysicsSceneWasValid &&
                resultB.LocalPhysicsSceneWasValid &&
                resultAPrime.LocalPhysicsSceneWasValid;
            var unloadRequested =
                resultA.TemporarySceneUnloadRequested &&
                resultB.TemporarySceneUnloadRequested &&
                resultAPrime.TemporarySceneUnloadRequested;

            return DeterminismProbeResult.Success(
                stableBodyIds.Length,
                baselineRequest.StepCount,
                repeatability.BitwiseEqual,
                repeatability.WithinGate,
                repeatability.MaxComponentDelta,
                repeatFirstStep,
                repeatFirstBody,
                perturbed.BitwiseEqual,
                perturbed.WithinGate,
                perturbed.MaxComponentDelta,
                perturbedFirstStep,
                perturbedFirstBody,
                managedBytesAllocatedInLoop,
                localPhysicsSceneValid,
                unloadRequested,
                simulationThreadingMode);
        }

        private static bool TryValidateFiniteFrames(
            float[] stateFrames,
            string runName,
            out string errorReason)
        {
            for (var valueIndex = 0; valueIndex < stateFrames.Length; valueIndex++)
            {
                var value = stateFrames[valueIndex];
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    errorReason = runName + " contains a non-finite state value at index " +
                                  valueIndex + ".";
                    return false;
                }
            }

            errorReason = string.Empty;
            return true;
        }

        private static bool HaveMatchingStableBodyIds(int[] first, int[] second)
        {
            if (first.Length != second.Length)
            {
                return false;
            }

            for (var bodyIndex = 0; bodyIndex < first.Length; bodyIndex++)
            {
                if (first[bodyIndex] != second[bodyIndex])
                {
                    return false;
                }
            }

            return true;
        }

        private static int[] GetStableBodyIds(SimulationBodyDefinition[] bodies)
        {
            if (bodies == null)
            {
                return Array.Empty<int>();
            }

            var stableBodyIds = new int[bodies.Length];
            for (var bodyIndex = 0; bodyIndex < bodies.Length; bodyIndex++)
            {
                var stableId = bodies[bodyIndex].StableId;
                var insertionIndex = bodyIndex;
                while (insertionIndex > 0 && stableBodyIds[insertionIndex - 1] > stableId)
                {
                    stableBodyIds[insertionIndex] = stableBodyIds[insertionIndex - 1];
                    insertionIndex--;
                }

                stableBodyIds[insertionIndex] = stableId;
            }

            return stableBodyIds;
        }

        private static void FindFirstDivergence(
            float[] baselineFrames,
            float[] comparisonFrames,
            int[] stableBodyIds,
            out int firstDivergingStep,
            out int firstDivergingBody)
        {
            firstDivergingStep = -1;
            firstDivergingBody = -1;
            var valuesPerStep = stableBodyIds.Length * BugCamConstants.StateStride;
            var stepCount = baselineFrames.Length / valuesPerStep;

            for (var step = 0; step < stepCount; step++)
            {
                for (var bodyIndex = 0; bodyIndex < stableBodyIds.Length; bodyIndex++)
                {
                    var bodyOffset =
                        (step * valuesPerStep) + (bodyIndex * BugCamConstants.StateStride);
                    for (var component = 0; component < BugCamConstants.StateStride; component++)
                    {
                        if (Math.Abs(
                                baselineFrames[bodyOffset + component] -
                                comparisonFrames[bodyOffset + component]) >
                            BugCamConstants.RepeatabilityGate)
                        {
                            firstDivergingStep = step;
                            firstDivergingBody = stableBodyIds[bodyIndex];
                            return;
                        }
                    }
                }
            }
        }
    }
}
