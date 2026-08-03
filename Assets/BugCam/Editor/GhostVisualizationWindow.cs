#if UNITY_EDITOR
using System;
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
    /// Search always routes through <see cref="GhostEvidencePlayModeHost"/> (MonoBehaviour nested
    /// coroutines). Does not use EditorApplication.update wrappers that skip nested IEnumerators.
    /// </summary>
    public sealed class GhostVisualizationWindow : EditorWindow
    {
        private const int DefaultRunStepCount = 32;

        private Vector2 _scroll;
        private string _status = "Idle.";
        private string _summaryText = string.Empty;
        private string _metricsPath = string.Empty;
        private string _evidenceDir = string.Empty;
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

            // Idempotent subscribe — avoid double-fire after domain reload / re-enable.
            GhostEvidencePlayModeHost.SearchCompleted -= OnHostSearchCompleted;
            GhostEvidencePlayModeHost.SearchCompleted += OnHostSearchCompleted;
        }

        private void OnDisable()
        {
            GhostEvidencePlayModeHost.SearchCompleted -= OnHostSearchCompleted;
            // Session survives window close for Scene View; do not dispose here.
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("BugCam — Ghost Visualization (Block 1.5)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Runs adaptive epsilon search on TowerScene via GhostEvidencePlayModeHost " +
                "(MonoBehaviour nested coroutines), builds ghost evidence, " +
                "draws Scene View trajectories (Handles.DrawAAPolyLine), and writes " +
                "Library/BugCamEvidence/Runs/<run-id>/ + Block1.5 checkpoint pointer.",
                MessageType.Info);

            DrawLabelLegend();

            var busy = GhostEvidencePlayModeHost.IsSearchBusy;
            EditorGUI.BeginDisabledGroup(busy);
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
            EditorGUILayout.HelpBox(
                busy ? "Running epsilon search via Host MonoBehaviour…" : _status,
                MessageType.None);

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
            var primaryAvailable = GhostEvidenceWriter.HasPrimaryDivergenceMetrics(document);
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Metric panel", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Verdict", search.Verdict);
            EditorGUILayout.LabelField(
                "Search identity",
                "body " + document.SearchIdentity.TargetBodyId +
                " / " + AxisName(document.SearchIdentity.SearchAxis) +
                " / " + document.SearchIdentity.Strategy);

            if (document.Success && search.Succeeded)
            {
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
            }
            else
            {
                EditorGUILayout.LabelField("Search Floor", "unavailable");
                EditorGUILayout.LabelField("Search Range", "unavailable");
                EditorGUILayout.LabelField("Characterization Ceiling", "unavailable");
            }

            if (search.HasThresholdEstimate && document.Success)
            {
                EditorGUILayout.LabelField(
                    "Threshold Estimate",
                    FormatMetres(search.ThresholdEstimateMetres));
            }
            else
            {
                EditorGUILayout.LabelField("Threshold Estimate", "unavailable");
            }

            if (GhostEvidenceWriter.HasReferenceEpsilon(search) && document.Success)
            {
                EditorGUILayout.LabelField(
                    "Reference Epsilon",
                    FormatMetres(search.ReferenceEpsilonMetres) +
                    " (exact=" + search.ReferenceIsExactThreshold + ")");
            }
            else
            {
                EditorGUILayout.LabelField("Reference Epsilon", "unavailable");
            }

            EditorGUILayout.LabelField("Retained fans", document.Fans.Length.ToString());
            EditorGUILayout.LabelField("Ranked ghost bodies", document.RankedBodies.Length.ToString());

            if (primaryAvailable)
            {
                EditorGUILayout.LabelField(
                    "First divergence frame",
                    primary.FirstDivergenceFrame.ToString(CultureInfo.InvariantCulture));
                EditorGUILayout.LabelField(
                    "First divergence body",
                    primary.FirstDivergenceBodyId.ToString(CultureInfo.InvariantCulture));
                EditorGUILayout.LabelField("Max spread", FormatMetres(primary.MaxSpreadMetres));
                EditorGUILayout.LabelField(
                    "Max spread body",
                    primary.MaxSpreadBodyId.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                EditorGUILayout.LabelField("First divergence frame", "unavailable");
                EditorGUILayout.LabelField("First divergence body", "unavailable");
                EditorGUILayout.LabelField("Max spread", "unavailable");
                EditorGUILayout.LabelField("Max spread body", "unavailable");
            }

            EditorGUILayout.LabelField(
                "Amplification",
                primaryAvailable && primary.AmplificationDefined
                    ? primary.Amplification.ToString("R", CultureInfo.InvariantCulture)
                    : "unavailable");
        }

        private void StartSearch()
        {
            if (!GhostEvidencePlayModeHost.TryStartTowerSearch(
                    _stepCount,
                    _strategy,
                    NormalizeAxis(_searchAxis),
                    GhostEvidencePlayModeHost.SourceWindow,
                    out var rejectReason))
            {
                _status = rejectReason;
                Repaint();
                return;
            }

            _status = EditorApplication.isPlaying
                ? "Running epsilon search via Host MonoBehaviour…"
                : "Entering Play Mode to run epsilon search via Host…";
            Repaint();
        }

        private void OnHostSearchCompleted(GhostSearchCompletion completion)
        {
            if (completion.Document != null && completion.WriteSucceeded)
            {
                _document = completion.Document;
                _evidenceDir = completion.Write.RunDirectory;
                _metricsPath = completion.Write.MetricsPath;
                _summaryText = GhostEvidenceWriter.BuildSummaryMarkdown(completion.Document);

                var session = GhostVisualizationSession.Ensure();
                session.ShowBaseline = _showBaseline;
                session.ShowFans = _showFans;
            }

            _status = completion.Status;
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
}
#endif
