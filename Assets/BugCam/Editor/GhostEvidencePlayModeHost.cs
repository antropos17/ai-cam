#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using BugCam.Core;
using BugCam.Evidence;
using UnityEditor;
using UnityEngine;

namespace BugCam.Editor
{
    /// <summary>
    /// Single Play Mode search pipeline for Block 1.5 ghost evidence.
    /// Window and menu both route here — Unity nested coroutines via MonoBehaviour.
    /// Never use EditorApplication.update wrappers that fail to pump nested IEnumerators.
    /// </summary>
    [InitializeOnLoad]
    public static class GhostEvidencePlayModeHost
    {
        private const string GoName = "BugCamGhostEvidenceRunner_TEMP";
        private const string PendingKey = "BugCam.GhostHost.Pending";
        private const string BusyKey = "BugCam.GhostSearch.Busy";
        private const string SourceKey = "BugCam.GhostHost.Source";
        private const string InterruptedStatus =
            "Interrupted: Play Mode exited before search completed.";

        public const string SourceWindow = "window";
        public const string SourceMenu = "menu";

        /// <summary>
        /// EditMode test seam: when false, <see cref="TryStartTowerSearch"/> still accepts and
        /// records Busy/Pending but does not flip <see cref="EditorApplication.isPlaying"/>.
        /// </summary>
        internal static bool AllowPlayModeEntry = true;

        /// <summary>Raised on the main thread when a hosted search finishes (success or failure).</summary>
        public static event Action<GhostSearchCompletion> SearchCompleted;

        static GhostEvidencePlayModeHost()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            // Domain reload mid-transition: resume pending search once Play Mode is live.
            EditorApplication.delayCall += TryResumePending;
        }

        [MenuItem("BugCam/Run Ghost Evidence (Tower / step 32)")]
        public static void MenuRunTowerStep32()
        {
            if (!TryStartTowerSearch(
                    32,
                    EpsilonSearchStrategy.AscendFromStart,
                    Vector3.right,
                    SourceMenu,
                    out var rejectReason))
            {
                Debug.LogWarning(rejectReason);
            }
        }

        /// <summary>True when a Window or Host search is pending or running.</summary>
        public static bool IsSearchBusy => SessionState.GetBool(BusyKey, false);

        /// <summary>
        /// Sole public entry for Window / menu tower search. Rejects when Busy.
        /// </summary>
        public static bool TryStartTowerSearch(
            int stepCount,
            EpsilonSearchStrategy strategy,
            Vector3 searchAxis,
            string source,
            out string rejectReason)
        {
            if (IsSearchBusy)
            {
                rejectReason = "A ghost search is already pending or running (Window and Host share one pipeline).";
                return false;
            }

            StartTowerSearch(stepCount, strategy, searchAxis, source ?? SourceMenu);
            rejectReason = null;
            return true;
        }

        private static void StartTowerSearch(
            int stepCount,
            EpsilonSearchStrategy strategy,
            Vector3 searchAxis,
            string source)
        {
            SessionState.SetBool(BusyKey, true);
            SessionState.SetString(SourceKey, source ?? SourceMenu);

            if (!EditorApplication.isPlaying)
            {
                SessionState.SetBool(PendingKey, true);
                SessionState.SetInt("BugCam.GhostHost.StepCount", stepCount);
                SessionState.SetInt("BugCam.GhostHost.Strategy", (int)strategy);
                SessionState.SetFloat("BugCam.GhostHost.AxisX", searchAxis.x);
                SessionState.SetFloat("BugCam.GhostHost.AxisY", searchAxis.y);
                SessionState.SetFloat("BugCam.GhostHost.AxisZ", searchAxis.z);
                if (AllowPlayModeEntry)
                {
                    EditorApplication.isPlaying = true;
                }

                return;
            }

            EnsureRunner(stepCount, strategy, searchAxis);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                TryResumePending();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                // Interrupted or abandoned run: clear Busy/Pending even if PendingKey
                // was already cleared when the runner started (mid-run exit).
                // Runner uses DontDestroyOnLoad + DontSave, so Play Mode exit alone
                // will not dispose it — destroy explicitly to avoid TEMP leaks.
                // Use deferred Destroy during the exit transition; hard-sweep leftovers
                // on EnteredEditMode (DestroyImmediate mid-exit can strand the next entry).
                CleanupHostOwnedSearch(notifyInterrupted: true, preferDeferredDestroy: true);
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                DestroyTempRunnerIfPresent(preferDeferredDestroy: false);
            }
        }

        /// <summary>
        /// Deterministic seam mirroring <see cref="PlayModeStateChange.ExitingPlayMode"/> cleanup
        /// without requiring an actual Play Mode transition.
        /// </summary>
        internal static void CleanupInterruptedSearchForTests()
        {
            CleanupHostOwnedSearch(notifyInterrupted: true, preferDeferredDestroy: false);
        }

        /// <summary>
        /// Central Host cleanup: clears Busy/Pending, destroys Host TEMP runner (stops its
        /// coroutine), and optionally notifies interruption. Idempotent.
        /// Used by normal completion, write/search failure, Play Mode interruption, and
        /// object-destruction shutdown paths. Never reports WriteSucceeded=true.
        /// </summary>
        private static void CleanupHostOwnedSearch(
            bool notifyInterrupted,
            bool preferDeferredDestroy = false,
            string interruptStatus = null)
        {
            var shouldNotify = notifyInterrupted && IsSearchBusy;
            var source = SessionState.GetString(SourceKey, SourceMenu);

            FinishBusy();
            DestroyTempRunnerIfPresent(preferDeferredDestroy);

            if (!shouldNotify)
            {
                return;
            }

            NotifyComplete(
                new GhostSearchCompletion(
                    source,
                    null,
                    default,
                    default,
                    false,
                    interruptStatus ?? InterruptedStatus));
        }

        private static void DestroyTempRunnerIfPresent(bool preferDeferredDestroy = false)
        {
            var useDeferred = preferDeferredDestroy || EditorApplication.isPlayingOrWillChangePlaymode;
            var existing = GameObject.Find(GoName);
            if (existing != null)
            {
                DestroyHostObject(existing, useDeferred);
            }

            // DontDestroyOnLoad + HideFlags.DontSave can survive Find in edge cases.
            var all = Resources.FindObjectsOfTypeAll<GameObject>();
            for (var i = 0; i < all.Length; i++)
            {
                var go = all[i];
                if (go != null && go.name == GoName)
                {
                    DestroyHostObject(go, useDeferred);
                }
            }
        }

        private static void DestroyHostObject(GameObject go, bool useDeferred)
        {
            if (useDeferred)
            {
                UnityEngine.Object.Destroy(go);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void TryResumePending()
        {
            if (!EditorApplication.isPlaying || !SessionState.GetBool(PendingKey, false))
            {
                return;
            }

            SessionState.SetBool(PendingKey, false);
            var stepCount = SessionState.GetInt("BugCam.GhostHost.StepCount", 32);
            var strategy = (EpsilonSearchStrategy)SessionState.GetInt(
                "BugCam.GhostHost.Strategy",
                (int)EpsilonSearchStrategy.AscendFromStart);
            var axis = new Vector3(
                SessionState.GetFloat("BugCam.GhostHost.AxisX", 1f),
                SessionState.GetFloat("BugCam.GhostHost.AxisY", 0f),
                SessionState.GetFloat("BugCam.GhostHost.AxisZ", 0f));
            EnsureRunner(stepCount, strategy, axis);
        }

        private static void EnsureRunner(
            int stepCount,
            EpsilonSearchStrategy strategy,
            Vector3 axis)
        {
            DestroyTempRunnerIfPresent(preferDeferredDestroy: false);

            var go = new GameObject(GoName);
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.DontSave;
            var runner = go.AddComponent<GhostEvidenceRunnerBehaviour>();
            runner.Begin(stepCount, strategy, axis, SessionState.GetString(SourceKey, SourceMenu));
        }

        private static void FinishBusy()
        {
            SessionState.SetBool(BusyKey, false);
            SessionState.SetBool(PendingKey, false);
        }

        private sealed class GhostEvidenceRunnerBehaviour : MonoBehaviour
        {
            public void Begin(
                int stepCount,
                EpsilonSearchStrategy strategy,
                Vector3 axis,
                string source)
            {
                StartCoroutine(Run(stepCount, strategy, axis, source));
            }

            private void OnDestroy()
            {
                // Torn down by Host cleanup or domain reload — ensure Busy/Pending clear
                // without emitting a second SearchCompleted (interrupt notify owns that).
                if (IsSearchBusy)
                {
                    FinishBusy();
                }
            }

            private IEnumerator Run(
                int stepCount,
                EpsilonSearchStrategy strategy,
                Vector3 axis,
                string source)
            {
                var settings = DivergenceSettings.CreateDefault();
                var identity = new GhostSearchIdentity(49, axis.normalized, strategy);
                var environment = GhostRunEnvironment.Capture(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
                var search = new EpsilonSearch(
                    settings.ToSearchSettings(),
                    identity.TargetBodyId,
                    identity.SearchAxis,
                    identity.Strategy);
                var bodies = TowerProbeRequestFactory.CreateBaseline(stepCount).Bodies;
                var scales = new float[bodies.Length];
                for (var i = 0; i < bodies.Length; i++)
                {
                    var s = bodies[i].Size;
                    scales[i] = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
                }

                var runner = new EpsilonSearchRunner();
                // Unity MonoBehaviour nested-coroutine semantics pump runner.Run fully,
                // including per-frame WaitForSceneCleanup yields.
                yield return runner.Run(
                    search,
                    bodies,
                    stepCount,
                    settings.ToThresholds(),
                    scales);

                var searchResult = runner.LastResult;
                Debug.Log(GhostEvidenceWriter.FormatHonestSearchReport(
                    searchResult,
                    searchResult.Succeeded));

                GhostEvidenceDocument document;
                var build = GhostEvidenceBuilder.Build(
                    searchResult,
                    identity,
                    settings,
                    scales,
                    null,
                    environment);
                if (!build.Succeeded || build.Document == null)
                {
                    document = GhostEvidenceBuilder.CreateFailureDocument(
                        searchResult,
                        identity,
                        searchResult.Succeeded
                            ? GhostEvidenceErrorCodes.BuildFailed
                            : GhostEvidenceBuilder.ResolveSearchErrorCode(searchResult.ErrorReason),
                        build.ErrorReason ?? searchResult.ErrorReason,
                        settings.GhostBodyLimit,
                        null,
                        environment);
                }
                else
                {
                    document = build.Document;
                }

                Debug.Log(GhostEvidenceReport.Format(document));

                var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                var write = GhostEvidenceWriter.Write(document, projectRoot);
                string status;
                if (!write.Succeeded)
                {
                    status = "Ghost evidence write failed: " + write.ErrorReason;
                    Debug.LogError(status);
                    NotifyComplete(
                        new GhostSearchCompletion(
                            source,
                            document,
                            write,
                            searchResult,
                            false,
                            status));
                    Cleanup();
                    yield break;
                }

                if (document.Success)
                {
                    try
                    {
                        GhostScreenshotCapture.Capture(document, write.RunDirectory);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("Ghost screenshot capture: " + ex.Message);
                    }

                    var session = GhostVisualizationSession.Ensure();
                    session.SetDocument(document, write.RunDirectory, write.MetricsPath);
                    session.IsVisible = true;
                    session.FrameOverview();
                    status =
                        "Success. Verdict=" + document.SearchResult.Verdict +
                        "; fans=" + document.Fans.Length +
                        "; rankedBodies=" + document.RankedBodies.Length +
                        "; evidence=" + write.RunDirectory;
                }
                else
                {
                    status =
                        "Failed (evidence written): " + document.ErrorCode + ": " +
                        document.ErrorReason + "; evidence=" + write.RunDirectory;
                    Debug.LogError(
                        "Ghost evidence host failed (" + document.ErrorCode + "): " +
                        document.ErrorReason + " evidenceDir=" + write.RunDirectory);

                    var session = GhostVisualizationSession.Ensure();
                    session.SetDocument(document, write.RunDirectory, write.MetricsPath);
                    session.IsVisible = false;
                }

                Debug.Log(
                    "BUGCAM_BLOCK_1_5_HOST_COMPLETE success=" + document.Success +
                    " source=" + source +
                    " evidenceDir=" + write.RunDirectory +
                    " metrics=" + write.MetricsPath +
                    " lastResultSucceeded=" + searchResult.Succeeded +
                    " fans=" + document.Fans.Length);
                NotifyComplete(
                    new GhostSearchCompletion(
                        source,
                        document,
                        write,
                        searchResult,
                        write.Succeeded,
                        status));
                Cleanup();
            }

            private void Cleanup()
            {
                CleanupHostOwnedSearch(notifyInterrupted: false, preferDeferredDestroy: true);
            }
        }

        private static void NotifyComplete(GhostSearchCompletion completion)
        {
            try
            {
                SearchCompleted?.Invoke(completion);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }

    /// <summary>Completion payload for the single Ghost search pipeline.</summary>
    public readonly struct GhostSearchCompletion
    {
        public GhostSearchCompletion(
            string source,
            GhostEvidenceDocument document,
            GhostEvidenceWriteResult write,
            EpsilonSearchResult searchResult,
            bool writeSucceeded,
            string status)
        {
            Source = source ?? string.Empty;
            Document = document;
            Write = write;
            SearchResult = searchResult;
            WriteSucceeded = writeSucceeded;
            Status = status ?? string.Empty;
        }

        public string Source { get; }

        public GhostEvidenceDocument Document { get; }

        public GhostEvidenceWriteResult Write { get; }

        public EpsilonSearchResult SearchResult { get; }

        public bool WriteSucceeded { get; }

        public string Status { get; }
    }
}
#endif
