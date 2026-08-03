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

        public const string SourceWindow = "window";
        public const string SourceMenu = "menu";

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
            StartTowerSearch(32, EpsilonSearchStrategy.AscendFromStart, Vector3.right, SourceMenu);
        }

        /// <summary>True when a Window or Host search is pending or running.</summary>
        public static bool IsSearchBusy => SessionState.GetBool(BusyKey, false);

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

        public static void StartTowerSearch(
            int stepCount = 32,
            EpsilonSearchStrategy strategy = EpsilonSearchStrategy.AscendFromStart,
            Vector3? searchAxis = null,
            string source = SourceMenu)
        {
            var axis = searchAxis ?? Vector3.right;
            SessionState.SetBool(BusyKey, true);
            SessionState.SetString(SourceKey, source ?? SourceMenu);

            if (!EditorApplication.isPlaying)
            {
                SessionState.SetBool(PendingKey, true);
                SessionState.SetInt("BugCam.GhostHost.StepCount", stepCount);
                SessionState.SetInt("BugCam.GhostHost.Strategy", (int)strategy);
                SessionState.SetFloat("BugCam.GhostHost.AxisX", axis.x);
                SessionState.SetFloat("BugCam.GhostHost.AxisY", axis.y);
                SessionState.SetFloat("BugCam.GhostHost.AxisZ", axis.z);
                EditorApplication.isPlaying = true;
                return;
            }

            EnsureRunner(stepCount, strategy, axis);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                TryResumePending();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                // Leaving play without a completed run: clear busy so Edit Mode can retry.
                if (SessionState.GetBool(PendingKey, false))
                {
                    // Still pending into play — keep busy.
                }
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
            var existing = GameObject.Find(GoName);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing);
            }

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
                FinishBusy();
                Destroy(gameObject);
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
