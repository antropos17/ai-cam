using System;
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
        public const int DefaultCleanupFrameLimit = 120;

        public const string CleanupTimeoutErrorMarker =
            "Temporary scene cleanup timed out";

        private readonly SimulationHarness _harness = new SimulationHarness();
        private readonly Func<int, bool> _isCleanupComplete;
        private readonly int _cleanupFrameLimit;

        /// <summary>Production runner: real scene-count cleanup gate, 120-frame limit.</summary>
        public EpsilonSearchRunner()
            : this(DefaultIsCleanupComplete, DefaultCleanupFrameLimit)
        {
        }

        /// <summary>
        /// Test seam: inject cleanup predicate and frame limit so timeout can be simulated
        /// without waiting 120 real frames (e.g. predicate always false, limit 0).
        /// </summary>
        public EpsilonSearchRunner(Func<int, bool> isCleanupComplete, int cleanupFrameLimit)
        {
            _isCleanupComplete = isCleanupComplete ?? DefaultIsCleanupComplete;
            _cleanupFrameLimit = cleanupFrameLimit < 0 ? 0 : cleanupFrameLimit;
        }

        /// <summary>Result of the most recent <see cref="Run"/> completion.</summary>
        public EpsilonSearchResult LastResult { get; private set; }

        /// <summary>
        /// A2 captured statics for every probe request. Null (default) = legacy tower
        /// ground; set before starting <see cref="Run"/>. A property rather than a Run
        /// parameter so Run keeps its pinned single 5-parameter reflection signature.
        /// </summary>
        public SimulationStaticColliderDefinition[] StaticColliders { get; set; }

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

                var simRequest = new SimulationRequest(
                    bodies,
                    stepCount,
                    perturbation,
                    StaticColliders);
                var harnessResult = _harness.Run(simRequest);

                var cleanupTimedOut = false;
                yield return WaitForSceneCleanup(
                    sceneCountBefore,
                    _isCleanupComplete,
                    _cleanupFrameLimit,
                    timedOut => cleanupTimedOut = timedOut);

                if (cleanupTimedOut)
                {
                    var reason =
                        CleanupTimeoutErrorMarker +
                        " after " +
                        _cleanupFrameLimit +
                        " frames; no further epsilon probes were run.";
                    search.SubmitProbeResult(request, EpsilonProbeOutcome.Failure(reason));
                    LastResult = search.BuildResult();
                    Debug.LogError(EpsilonSearchReport.Format(LastResult));
                    yield break;
                }

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
        /// On timeout, <paramref name="onCompleted"/> is invoked with <c>true</c> and the
        /// caller must stop the search — the next probe must not start.
        /// </summary>
        public static IEnumerator WaitForSceneCleanup(int sceneCountBeforeProbe)
        {
            var timedOut = false;
            yield return WaitForSceneCleanup(
                sceneCountBeforeProbe,
                DefaultIsCleanupComplete,
                DefaultCleanupFrameLimit,
                value => timedOut = value);
            // Legacy callers ignore timeout; production Run uses the overload that reports it.
            if (timedOut)
            {
                Debug.LogError(
                    CleanupTimeoutErrorMarker +
                    " after " +
                    DefaultCleanupFrameLimit +
                    " frames.");
            }
        }

        /// <summary>
        /// Cleanup wait with injectable predicate/limit for deterministic timeout tests.
        /// </summary>
        public static IEnumerator WaitForSceneCleanup(
            int sceneCountBeforeProbe,
            Func<int, bool> isCleanupComplete,
            int frameLimit,
            Action<bool> onCompleted)
        {
            var predicate = isCleanupComplete ?? DefaultIsCleanupComplete;
            var limit = frameLimit < 0 ? 0 : frameLimit;

            for (var frame = 0; frame < limit; frame++)
            {
                if (predicate(sceneCountBeforeProbe))
                {
                    onCompleted?.Invoke(false);
                    yield break;
                }

                yield return null;
            }

            // Zero-frame limit and exhausted waits both take a final check.
            if (predicate(sceneCountBeforeProbe))
            {
                onCompleted?.Invoke(false);
                yield break;
            }

            onCompleted?.Invoke(true);
        }

        private static bool DefaultIsCleanupComplete(int sceneCountBeforeProbe)
        {
            return SceneManager.sceneCount <= sceneCountBeforeProbe;
        }
    }
}
