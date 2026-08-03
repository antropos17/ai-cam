#if UNITY_EDITOR
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using BugCam.Core;
using BugCam.Evidence;
using UnityEditor;
using UnityEngine;

namespace BugCam.Editor
{
    /// <summary>
    /// Block 1.5 Ghost Visualization Editor window.
    /// Menu: BugCam/Ghost Visualization. Not the PR4 inspection window; not Day-2 BugCamWindow.
    /// </summary>
    public sealed class GhostVisualizationWindow : EditorWindow
    {
        private const int DefaultRunStepCount = 32;
        private const string PendingSearchKey = "BugCam.GhostViz.PendingSearch";

        private Vector2 _scroll;
        private string _status = "Idle.";
        private string _summaryText = string.Empty;
        private string _metricsPath = string.Empty;
        private string _evidenceDir = string.Empty;
        private bool _isRunning;
        private bool _showBaseline = true;
        private bool _showFans = true;
        private EpsilonSearchStrategy _strategy = EpsilonSearchStrategy.AscendFromStart;
        private Vector3 _searchAxis = Vector3.right;
        private int _stepCount = DefaultRunStepCount;
        private GhostEvidenceDocument _document;

        [MenuItem("BugCam/Ghost Visualization")]
        public static void Open()
        {
            var window = GetWindow<GhostVisualizationWindow>();
            window.titleContent = new GUIContent("BugCam Ghost Visualization");
            window.minSize = new Vector2(420f, 520f);
            window.Show();
            GhostVisualizationSession.Ensure();
        }

        private void OnEnable()
        {
            var session = GhostVisualizationSession.Ensure();
            session.ShowBaseline = _showBaseline;
            session.ShowFans = _showFans;
            if (session.Document != null)
            {
                _document = session.Document;
                _evidenceDir = session.EvidenceDirectory;
                _metricsPath = session.MetricsPath;
                _summaryText = GhostEvidenceWriter.BuildSummaryMarkdown(_document);
            }

            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            if (EditorApplication.isPlaying && SessionState.GetBool(PendingSearchKey, false))
            {
                SessionState.SetBool(PendingSearchKey, false);
                EditorCoroutineUtility.StartCoroutine(RunSearchCoroutine(), this);
            }
        }

        private void OnDestroy()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            // Session survives window close for Scene View; do not dispose here.
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("BugCam — Ghost Visualization (Block 1.5)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Runs adaptive epsilon search on TowerScene, builds ghost evidence, " +
                "draws Scene View trajectories (Handles.DrawAAPolyLine), and writes " +
                "Library/BugCamEvidence/Runs/<run-id>/ + Block1.5 checkpoint pointer.",
                MessageType.Info);

            DrawLabelLegend();

            EditorGUI.BeginDisabledGroup(_isRunning);
            _searchAxis = EditorGUILayout.Vector3Field("Search Axis", _searchAxis);
            _strategy = (EpsilonSearchStrategy)EditorGUILayout.EnumPopup("Strategy", _strategy);
            _stepCount = EditorGUILayout.IntField("Step Count", _stepCount);
            if (_stepCount <= 0)
            {
                _stepCount = DefaultRunStepCount;
            }

            if (GUILayout.Button("Run / Load Ghost Search", GUILayout.Height(28f)))
            {
                StartSearch();
            }

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Visualization", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _showBaseline = EditorGUILayout.Toggle("Show Baseline (white)", _showBaseline);
            _showFans = EditorGUILayout.Toggle("Show Fans (colored)", _showFans);
            if (EditorGUI.EndChangeCheck())
            {
                var session = GhostVisualizationSession.Ensure();
                session.ShowBaseline = _showBaseline;
                session.ShowFans = _showFans;
                session.IsVisible = true;
                SceneView.RepaintAll();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Show Viz"))
            {
                GhostVisualizationSession.Ensure().IsVisible = true;
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Hide Viz"))
            {
                GhostVisualizationSession.Ensure().IsVisible = false;
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Clear Viz"))
            {
                GhostVisualizationSession.Ensure().Clear();
                _document = null;
                _summaryText = string.Empty;
                SceneView.RepaintAll();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Frame First Divergence"))
            {
                GhostVisualizationSession.Ensure().FrameFirstDivergence();
            }

            if (GUILayout.Button("Frame Max Spread"))
            {
                GhostVisualizationSession.Ensure().FrameMaxSpread();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Evidence", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Open Evidence Folder"))
            {
                OpenEvidenceFolder();
            }

            if (GUILayout.Button("Copy Summary"))
            {
                EditorGUIUtility.systemCopyBuffer = _summaryText ?? string.Empty;
                _status = "Summary copied.";
            }

            if (GUILayout.Button("Copy Metrics Path"))
            {
                EditorGUIUtility.systemCopyBuffer = _metricsPath ?? string.Empty;
                _status = "Metrics path copied.";
            }

            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Regenerate Screenshots"))
            {
                RegenerateScreenshots();
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(_status, MessageType.None);

            if (!string.IsNullOrEmpty(_metricsPath))
            {
                EditorGUILayout.LabelField("Metrics", _metricsPath);
            }

            if (!string.IsNullOrEmpty(_evidenceDir))
            {
                EditorGUILayout.LabelField("Evidence dir", _evidenceDir);
            }

            if (_document != null)
            {
                DrawMetricsPanel(_document);
            }

            if (!string.IsNullOrEmpty(_summaryText))
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(_summaryText, GUILayout.MinHeight(180f));
            }

            EditorGUILayout.EndScrollView();
        }

        private static void DrawLabelLegend()
        {
            EditorGUILayout.LabelField("Honest labels", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("• Threshold Estimate — only when hasThresholdEstimate");
            EditorGUILayout.LabelField("• Reference Epsilon — fan center; never exact threshold");
            EditorGUILayout.LabelField("• Search Floor — search range lower bound");
            EditorGUILayout.LabelField("• Search Range — EpsilonStart…EpsilonCeiling");
            EditorGUILayout.LabelField("• Characterization Range — may exceed ceiling (OutsideSearchRange)");
        }

        private static void DrawMetricsPanel(GhostEvidenceDocument document)
        {
            var search = document.SearchResult;
            var primary = document.PrimaryDivergence;
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Metric panel", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Verdict", search.Verdict);
            EditorGUILayout.LabelField(
                "Search identity",
                "body " + document.SearchIdentity.TargetBodyId +
                " / " + AxisName(document.SearchIdentity.SearchAxis) +
                " / " + document.SearchIdentity.Strategy);
            EditorGUILayout.LabelField(
                "Search Floor",
                FormatMetres(search.SearchRangeStartMetres));
            EditorGUILayout.LabelField(
                "Search Range",
                FormatMetres(search.SearchRangeStartMetres) + " … " +
                FormatMetres(search.SearchRangeCeilingMetres));
            EditorGUILayout.LabelField(
                "Characterization Ceiling",
                FormatMetres(search.CharacterizationCeilingMetres));

            if (search.HasThresholdEstimate)
            {
                EditorGUILayout.LabelField(
                    "Threshold Estimate",
                    FormatMetres(search.ThresholdEstimateMetres));
            }
            else
            {
                EditorGUILayout.LabelField("Threshold Estimate", "unavailable");
            }

            EditorGUILayout.LabelField(
                "Reference Epsilon",
                FormatMetres(search.ReferenceEpsilonMetres) +
                " (exact=" + search.ReferenceIsExactThreshold + ")");
            EditorGUILayout.LabelField("Retained fans", document.Fans.Length.ToString());
            EditorGUILayout.LabelField("Ranked ghost bodies", document.RankedBodies.Length.ToString());
            EditorGUILayout.LabelField("First divergence frame", primary.FirstDivergenceFrame.ToString());
            EditorGUILayout.LabelField("Max spread", FormatMetres(primary.MaxSpreadMetres));
            EditorGUILayout.LabelField(
                "Amplification",
                primary.AmplificationDefined ? primary.Amplification.ToString("R") : "unavailable");
        }

        private void StartSearch()
        {
            if (_isRunning)
            {
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                _status = "Entering Play Mode to run epsilon search…";
                SessionState.SetBool(PendingSearchKey, true);
                SessionState.SetInt("BugCam.GhostViz.StepCount", _stepCount);
                SessionState.SetInt("BugCam.GhostViz.Strategy", (int)_strategy);
                SessionState.SetFloat("BugCam.GhostViz.AxisX", _searchAxis.x);
                SessionState.SetFloat("BugCam.GhostViz.AxisY", _searchAxis.y);
                SessionState.SetFloat("BugCam.GhostViz.AxisZ", _searchAxis.z);
                EditorApplication.isPlaying = true;
                return;
            }

            EditorCoroutineUtility.StartCoroutine(RunSearchCoroutine(), this);
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode)
            {
                return;
            }

            if (!SessionState.GetBool(PendingSearchKey, false))
            {
                return;
            }

            SessionState.SetBool(PendingSearchKey, false);
            _stepCount = SessionState.GetInt("BugCam.GhostViz.StepCount", DefaultRunStepCount);
            _strategy = (EpsilonSearchStrategy)SessionState.GetInt(
                "BugCam.GhostViz.Strategy",
                (int)EpsilonSearchStrategy.AscendFromStart);
            _searchAxis = new Vector3(
                SessionState.GetFloat("BugCam.GhostViz.AxisX", 1f),
                SessionState.GetFloat("BugCam.GhostViz.AxisY", 0f),
                SessionState.GetFloat("BugCam.GhostViz.AxisZ", 0f));
            EditorCoroutineUtility.StartCoroutine(RunSearchCoroutine(), this);
        }

        private IEnumerator RunSearchCoroutine()
        {
            _isRunning = true;
            _status = "Running epsilon search…";
            Repaint();

            GhostEvidenceDocument document = null;
            GhostEvidenceWriteResult write = default;
            string error = null;

            EpsilonSearchResult searchResult = default;
            var identity = new GhostSearchIdentity(49, NormalizeAxis(_searchAxis), _strategy);

            try
            {
                var settings = DivergenceSettings.CreateDefault();
                var searchSettings = settings.ToSearchSettings();
                var search = new EpsilonSearch(
                    searchSettings,
                    identity.TargetBodyId,
                    identity.SearchAxis,
                    identity.Strategy);
                var bodies = TowerProbeRequestFactory.CreateBaseline(_stepCount).Bodies;
                var scales = BuildBodyScales(bodies);
                var runner = new EpsilonSearchRunner();

                yield return runner.Run(
                    search,
                    bodies,
                    _stepCount,
                    settings.ToThresholds(),
                    scales);

                searchResult = runner.LastResult;
                Debug.Log(EpsilonSearchReport.Format(searchResult));

                if (!searchResult.Succeeded)
                {
                    error = searchResult.ErrorReason;
                    yield break;
                }

                var build = GhostEvidenceBuilder.Build(
                    searchResult,
                    identity,
                    settings,
                    scales);
                if (!build.Succeeded)
                {
                    error = build.ErrorReason;
                    yield break;
                }

                document = build.Document;
                Debug.Log(GhostEvidenceReport.Format(document));

                var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                write = GhostEvidenceWriter.Write(document, projectRoot);
                if (!write.Succeeded)
                {
                    error = write.ErrorReason;
                    yield break;
                }

                try
                {
                    GhostScreenshotCapture.Capture(document, write.RunDirectory);
                }
                catch (Exception captureEx)
                {
                    Debug.LogWarning("Ghost screenshot capture: " + captureEx.Message);
                }
            }
            finally
            {
                _isRunning = false;
            }

            if (!string.IsNullOrEmpty(error))
            {
                _status = "Failed: " + error;
                Repaint();
                yield break;
            }

            _document = document;
            _evidenceDir = write.RunDirectory;
            _metricsPath = write.MetricsPath;
            _summaryText = GhostEvidenceWriter.BuildSummaryMarkdown(document);

            var session = GhostVisualizationSession.Ensure();
            session.SetDocument(document, write.RunDirectory, write.MetricsPath);
            session.ShowBaseline = _showBaseline;
            session.ShowFans = _showFans;
            session.IsVisible = true;
            session.FrameOverview();

            _status =
                "Success. Verdict=" + document.SearchResult.Verdict +
                "; fans=" + document.Fans.Length +
                "; rankedBodies=" + document.RankedBodies.Length +
                "; evidence=" + write.RunDirectory;
            Repaint();
            SceneView.RepaintAll();
        }

        private void RegenerateScreenshots()
        {
            if (_document == null || string.IsNullOrEmpty(_evidenceDir))
            {
                _status = "No evidence document loaded.";
                return;
            }

            try
            {
                var result = GhostScreenshotCapture.Capture(_document, _evidenceDir);
                _status =
                    "Screenshots: overview=" + result.OverviewWritten +
                    " firstDiv=" + result.FirstDivergenceWritten +
                    " maxSpread=" + result.MaxSpreadWritten +
                    " final=" + result.FinalWritten;
            }
            catch (Exception ex)
            {
                _status = "Screenshot failed: " + ex.Message;
            }
        }

        private void OpenEvidenceFolder()
        {
            var path = _evidenceDir;
            if (string.IsNullOrEmpty(path))
            {
                var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                path = Path.Combine(
                    projectRoot,
                    GhostEvidenceSchema.CheckpointRelativeRoot.Replace('/', Path.DirectorySeparatorChar));
            }

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            EditorUtility.RevealInFinder(path);
        }

        private static float[] BuildBodyScales(SimulationBodyDefinition[] bodies)
        {
            var scales = new float[bodies.Length];
            for (var i = 0; i < bodies.Length; i++)
            {
                var s = bodies[i].Size;
                scales[i] = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
            }

            return scales;
        }

        private static Vector3 NormalizeAxis(Vector3 axis)
        {
            if (axis == Vector3.zero)
            {
                return Vector3.right;
            }

            return axis.normalized;
        }

        private static string AxisName(Vector3 axis)
        {
            if ((axis - Vector3.right).sqrMagnitude < 1e-12f)
            {
                return "X";
            }

            if ((axis - Vector3.up).sqrMagnitude < 1e-12f)
            {
                return "Y";
            }

            if ((axis - Vector3.forward).sqrMagnitude < 1e-12f)
            {
                return "Z";
            }

            return axis.ToString();
        }

        private static string FormatMetres(float metres)
        {
            return metres.ToString("R", CultureInfo.InvariantCulture) + " m (" +
                   (metres * 1000f).ToString("R", CultureInfo.InvariantCulture) + " mm)";
        }
    }

    /// <summary>
    /// Minimal Editor coroutine host — avoids depending on external EditorCoroutine packages.
    /// </summary>
    internal static class EditorCoroutineUtility
    {
        public static void StartCoroutine(IEnumerator enumerator, EditorWindow host)
        {
            void Tick()
            {
                try
                {
                    if (enumerator == null || !enumerator.MoveNext())
                    {
                        EditorApplication.update -= Tick;
                        if (host != null)
                        {
                            host.Repaint();
                        }
                    }
                }
                catch (Exception ex)
                {
                    EditorApplication.update -= Tick;
                    Debug.LogException(ex);
                }
            }

            EditorApplication.update += Tick;
        }
    }
}
#endif
