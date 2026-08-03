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
    /// Play Mode host for Block 1.5 ghost search + evidence write.
    /// Survives domain reload via SessionState + InitializeOnLoad + DontDestroyOnLoad runner.
    /// </summary>
    [InitializeOnLoad]
    public static class GhostEvidencePlayModeHost
    {
        private const string GoName = "BugCamGhostEvidenceRunner_TEMP";
        private const string PendingKey = "BugCam.GhostHost.Pending";

        static GhostEvidencePlayModeHost()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            // Domain reload mid-transition: resume pending search once Play Mode is live.
            EditorApplication.delayCall += TryResumePending;
        }

        [MenuItem("BugCam/Run Ghost Evidence (Tower / step 32)")]
        public static void MenuRunTowerStep32()
        {
            StartTowerSearch(32, EpsilonSearchStrategy.AscendFromStart, Vector3.right);
        }

        public static void StartTowerSearch(
            int stepCount = 32,
            EpsilonSearchStrategy strategy = EpsilonSearchStrategy.AscendFromStart,
            Vector3? searchAxis = null)
        {
            var axis = searchAxis ?? Vector3.right;
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
            runner.Begin(stepCount, strategy, axis);
        }

        private sealed class GhostEvidenceRunnerBehaviour : MonoBehaviour
        {
            public void Begin(int stepCount, EpsilonSearchStrategy strategy, Vector3 axis)
            {
                StartCoroutine(Run(stepCount, strategy, axis));
            }

            private IEnumerator Run(int stepCount, EpsilonSearchStrategy strategy, Vector3 axis)
            {
                var settings = DivergenceSettings.CreateDefault();
                var identity = new GhostSearchIdentity(49, axis.normalized, strategy);
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
                yield return runner.Run(
                    search,
                    bodies,
                    stepCount,
                    settings.ToThresholds(),
                    scales);

                var searchResult = runner.LastResult;
                Debug.Log(EpsilonSearchReport.Format(searchResult));

                if (!searchResult.Succeeded)
                {
                    Debug.LogError("Ghost evidence host search failed: " + searchResult.ErrorReason);
                    Cleanup();
                    yield break;
                }

                var build = GhostEvidenceBuilder.Build(
                    searchResult,
                    identity,
                    settings,
                    scales);
                if (!build.Succeeded)
                {
                    Debug.LogError("Ghost evidence build failed: " + build.ErrorReason);
                    Cleanup();
                    yield break;
                }

                Debug.Log(GhostEvidenceReport.Format(build.Document));

                var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                var write = GhostEvidenceWriter.Write(build.Document, projectRoot);
                if (!write.Succeeded)
                {
                    Debug.LogError("Ghost evidence write failed: " + write.ErrorReason);
                    Cleanup();
                    yield break;
                }

                try
                {
                    GhostScreenshotCapture.Capture(build.Document, write.RunDirectory);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("Ghost screenshot capture: " + ex.Message);
                }

                var session = GhostVisualizationSession.Ensure();
                session.SetDocument(build.Document, write.RunDirectory, write.MetricsPath);
                session.IsVisible = true;
                session.FrameOverview();

                Debug.Log(
                    "BUGCAM_BLOCK_1_5_HOST_COMPLETE evidenceDir=" + write.RunDirectory +
                    " metrics=" + write.MetricsPath);
                Cleanup();
            }

            private void Cleanup()
            {
                Destroy(gameObject);
            }
        }
    }
}
#endif
