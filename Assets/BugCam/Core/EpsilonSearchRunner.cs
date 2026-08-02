using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BugCam.Core
{
    /// <summary>
    /// Play Mode driver for <see cref="EpsilonSearch"/>. Executes probes sequentially and
    /// waits for the previous local PhysicsScene unload before launching the next probe.
    /// </summary>
    public sealed class EpsilonSearchRunner
    {
        private readonly SimulationHarness _harness = new SimulationHarness();

        /// <summary>Result of the most recent <see cref="Run"/> completion.</summary>
        public EpsilonSearchResult LastResult { get; private set; }

        /// <summary>
        /// Run the search to completion. Must be started as a Play Mode coroutine.
        /// Read <see cref="LastResult"/> after the enumerator finishes.
        /// </summary>
        public IEnumerator Run(
            EpsilonSearch search,
            SimulationBodyDefinition[] bodies,
            int stepCount,
            DivergenceThresholds thresholds,
            float[] bodyScalesMetres)
        {
            LastResult = default;

            if (search == null)
            {
                LastResult = EpsilonSearchResult.Failure("EpsilonSearch is required.");
                yield break;
            }

            if (bodies == null || bodies.Length == 0)
            {
                LastResult = EpsilonSearchResult.Failure("At least one body is required.");
                yield break;
            }

            if (stepCount <= 0)
            {
                LastResult = EpsilonSearchResult.Failure("StepCount must be greater than zero.");
                yield break;
            }

            var thresholdError = thresholds.Validate();
            if (!string.IsNullOrEmpty(thresholdError))
            {
                LastResult = EpsilonSearchResult.Failure(thresholdError);
                yield break;
            }

            RunResult baselineRun = default;
            var hasBaseline = false;

            while (search.TryGetNextProbe(out var request))
            {
                var sceneCountBefore = SceneManager.sceneCount;
                var perturbation = request.IsBaseline
                    ? default
                    : new SimulationPerturbation(
                        request.TargetBodyId,
                        request.Axis,
                        request.EpsilonMetres);

                var simRequest = new SimulationRequest(bodies, stepCount, perturbation);
                var harnessResult = _harness.Run(simRequest);
                yield return WaitForSceneCleanup(sceneCountBefore);

                if (!harnessResult.Succeeded)
                {
                    search.SubmitProbeResult(
                        request,
                        EpsilonProbeOutcome.Failure(harnessResult.ErrorReason));
                    break;
                }

                if (!harnessResult.TemporarySceneUnloadRequested)
                {
                    search.SubmitProbeResult(
                        request,
                        EpsilonProbeOutcome.Failure(
                            "Harness did not request temporary scene unload between probes."));
                    break;
                }

                var runResult = RunResult.FromSimulationRunResult(harnessResult);
                if (!runResult.Succeeded)
                {
                    search.SubmitProbeResult(
                        request,
                        EpsilonProbeOutcome.Failure(runResult.ErrorReason));
                    break;
                }

                if (request.IsBaseline)
                {
                    baselineRun = runResult;
                    hasBaseline = true;
                    search.SubmitProbeResult(
                        request,
                        EpsilonProbeOutcome.Success(
                            hasSignificantDivergence: false,
                            firstDivergenceFrame: -1,
                            maxSpreadMetres: 0f,
                            runResult));
                    continue;
                }

                if (!hasBaseline)
                {
                    search.SubmitProbeResult(
                        request,
                        EpsilonProbeOutcome.Failure("Baseline must run before perturbed probes."));
                    break;
                }

                var divergence = DivergenceEngine.Analyze(
                    baselineRun,
                    runResult,
                    bodyScalesMetres,
                    thresholds);

                search.SubmitProbeResult(
                    request,
                    EpsilonProbeOutcome.FromDivergence(divergence, runResult));
            }

            LastResult = search.BuildResult();
        }

        /// <summary>
        /// Wait until Unity finishes unloading the previous local Physics3D scene.
        /// </summary>
        public static IEnumerator WaitForSceneCleanup(int sceneCountBeforeProbe)
        {
            // UnloadSceneAsync is deferred; allow several frames for the count to return.
            for (var frame = 0; frame < 120; frame++)
            {
                if (SceneManager.sceneCount <= sceneCountBeforeProbe)
                {
                    yield break;
                }

                yield return null;
            }
        }
    }
}
