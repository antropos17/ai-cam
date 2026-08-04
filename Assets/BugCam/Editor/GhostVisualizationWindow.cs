#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Text;
using BugCam.Core;
using BugCam.Evidence;
using UnityEditor;
using UnityEngine;

namespace BugCam.Editor
{
    /// <summary>
    /// Block 2.2 Ghost Visualization window (UX pass over the Block 1.5 window).
    /// Menu: BugCam/Ghost Visualization. Search always routes through
    /// <see cref="GhostEvidencePlayModeHost"/> (MonoBehaviour nested coroutines).
    ///
    /// The UI is driven by one state machine — the single source of truth:
    /// IDLE (transient blockers only) → READY → SEARCHING → DONE(verdict | interrupted).
    /// Every control's visibility/enabled state derives from it; a disabled main button
    /// always renders its reasons as explicit rows.
    ///
    /// Honesty rules: verdicts verbatim from the engine; undefined numbers render no row
    /// (no "unavailable" placeholders); INTERRUPTED is neutral; progress shows only real
    /// probe steps. Performance: progress strings are rebuilt only on host events
    /// (StringBuilder, cached GUIContent) and the window repaints only on events,
    /// never per editor tick.
    /// </summary>
    public sealed class GhostVisualizationWindow : EditorWindow
    {
        private const int DefaultRunStepCount = 32;
        private const string TutorialHiddenKey = "BugCam.GhostWindow.TutorialHidden";
        private const string SetupCollapsedKey = "BugCam.GhostWindow.SetupCollapsed";

        // A1 entry persistence (docs/CONTRACT-2.2.1.md): SessionState, asset by GUID,
        // epsilon overrides as canonical full-precision metres.
        private const string SceneKindKey = "BugCam.GhostWindow.SceneKind";
        private const string AssetGuidKey = "BugCam.GhostWindow.SettingsAssetGuid";
        private const string HasFloorOverrideKey = "BugCam.GhostWindow.HasFloorOverride";
        private const string FloorOverrideKey = "BugCam.GhostWindow.FloorOverrideMetres";
        private const string HasCeilingOverrideKey = "BugCam.GhostWindow.HasCeilingOverride";
        private const string CeilingOverrideKey = "BugCam.GhostWindow.CeilingOverrideMetres";
        private const string TargetBodyIdKey = "BugCam.GhostWindow.TargetBodyId";

        private enum WindowState
        {
            Idle,
            Ready,
            Searching,
            Done
        }

        private readonly struct ResultRow
        {
            public ResultRow(string label, string value)
            {
                Label = label;
                Value = value;
            }

            public readonly string Label;
            public readonly string Value;
        }

        // --- Static UI text (cached once; no per-frame GUIContent construction) ---

        private static readonly GUIContent StatusContent = new GUIContent();
        private static readonly GUIContent HelpButtonContent =
            new GUIContent("?", "Показать вводные 3 шага снова.");
        private static readonly GUIContent RunButtonContent = new GUIContent("Запустить поиск");
        private static readonly GUIContent InterruptContent = new GUIContent("Прервать");
        private static readonly GUIContent InterruptingContent = new GUIContent("Прерывание…");
        private static readonly GUIContent SceneKindLabel = new GUIContent(
            "Сцена",
            "Что симулируется: процедурная башня (гейт-путь блока 2.2) или захват " +
            "открытой сцены (A2: Box/Sphere, кинематика замораживается в статику, " +
            "неподдерживаемое — fail-closed).");
        private static readonly GUIContent[] SceneKindOptions =
        {
            new GUIContent("Башня (процедурная)"),
            new GUIContent("Открытая сцена (захват)")
        };
        private static readonly GUIContent TargetLabel = new GUIContent(
            "Цель",
            "Возмущаемое тело. Башня — тела процедурной TowerScene; открытая сцена — " +
            "захваченные динамические тела. Список приходит из display-name provider'а.");
        private static readonly GUIContent AssetLabel = new GUIContent(
            "Ассет настроек",
            "DivergenceSettings-ассет как источник настроек поиска; пусто = дефолты кода. " +
            "Хранится по GUID (SessionState).");
        private static readonly GUIContent ResetToSourceContent = new GUIContent(
            "Сбросить к источнику",
            "Убрать правки окна — вернуть ε-диапазон к значениям ассета или дефолтов.");
        private static readonly GUIContent AxisLabel = new GUIContent(
            "Ось",
            "Единичный вектор. Направление начального смещения снаряда при возмущении.");
        private static readonly GUIContent StrategyLabel = new GUIContent(
            "Стратегия",
            "Порядок проб по диапазону: AscendFromStart — вверх от нижней границы; " +
            "AscendFromCustomStart — вверх от заданного старта; DescendFromCeiling — " +
            "вниз от потолка. Диапазон не меняет.");
        private static readonly GUIContent RangeLabel = new GUIContent(
            "Диапазон ε, мм",
            "Ввод в миллиметрах (invariant culture, разделитель — точка); хранение в метрах " +
            "полной точности. Приоритет: правка окна > ассет > дефолты.");
        private static readonly GUIContent[] AxisOptions =
        {
            new GUIContent("X"),
            new GUIContent("Y"),
            new GUIContent("Z")
        };
        private static readonly Vector3[] AxisVectors =
        {
            Vector3.right,
            Vector3.up,
            Vector3.forward
        };

        private const string ReasonSearching = "Причина: идёт поиск";
        private const string ReasonCompiling = "Причина: идёт компиляция скриптов";
        private const string ReasonPlayTransition = "Причина: редактор переключает Play Mode";
        private const string ReasonForeignLock =
            "Причина: пайплайн поиска занят (Busy/Pending-лок хоста — запуск из меню BugCam " +
            "или незавершённый прогон)";
        private const string ReasonForeignPlayMode =
            "Причина: Play Mode запущен вручную, не BugCam — остановите его перед поиском";
        private const string ReasonEvidenceNotWritten =
            "Причина: улики этого прогона не записаны";
        private const string ReasonEvidenceWriteFailed =
            "Причина: папка улик не записана (ошибка выше)";
        private const string ReasonEvidenceDirMissing = "Причина: папка не найдена";
        private const string ReasonNoFans = "Причина: фанов нет — нечего показывать";

        private const string StatusIdle = "Запуск недоступен — см. причину под кнопкой";
        private const string StatusEnteringPlayMode = "Поиск: входим в Play Mode…";
        private const string StatusInterrupting = "Прерывание — выход из Play Mode…";
        private const string InterruptedExplanation =
            "Поиск прерван до завершения — чисел нет. Не ошибка и не вердикт.";

        // Phase strip variants indexed by EpsilonSearchPhase (real engine phase names).
        private static readonly string[] PhaseStripByPhase =
        {
            "Baseline > Ladder > Exponential > Bisection > Fan",              // NotStarted
            "[Baseline] > Ladder > Exponential > Bisection > Fan",            // Baseline
            "Baseline > [Ladder] > Exponential > Bisection > Fan",            // Ladder
            "Baseline > Ladder > [Exponential] > Bisection > Fan",            // Exponential
            "Baseline > Ladder > Exponential > [Bisection] > Fan",            // Bisection
            "Baseline > Ladder > Exponential > Bisection > [Fan]",            // Fan
            "Baseline > Ladder > Exponential > Bisection > Fan",              // Completed
            "Baseline > Ladder > Exponential > Bisection > Fan"               // Failed
        };

        private static readonly string[] PhaseNameByPhase =
        {
            "NotStarted", "Baseline", "Ladder", "Exponential", "Bisection", "Fan",
            "Completed", "Failed"
        };

        private static GUIStyle _verdictStyle;
        private static GUIStyle _wrapLabelStyle;
        private static GUIStyle _reasonStyle;

        // --- Serialized state (survives domain reload) ---

        [SerializeField] private int _axisIndex;
        [SerializeField] private EpsilonSearchStrategy _strategy = EpsilonSearchStrategy.AscendFromStart;
        [SerializeField] private int _stepCount = DefaultRunStepCount;
        [SerializeField] private string _evidenceDir = string.Empty;
        [SerializeField] private bool _showBaseline = true;
        [SerializeField] private bool _showFans = true;
        [SerializeField] private bool _startedFromThisWindow;
        [SerializeField] private Vector2 _scroll;

        // --- Volatile state (dies with domain reload — stale numbers must not survive) ---

        [NonSerialized] private GhostEvidenceDocument _document;
        [NonSerialized] private bool _hasCompletion;
        [NonSerialized] private bool _writeSucceeded;
        [NonSerialized] private string _completionStatus = string.Empty;
        [NonSerialized] private bool _interrupting;
        [NonSerialized] private ResultRow[] _resultRows = Array.Empty<ResultRow>();
        [NonSerialized] private string _verdictText = string.Empty;
        [NonSerialized] private string _verdictMeaning = string.Empty;

        // Progress lines are rebuilt only inside OnHostSearchProgress.
        [NonSerialized] private bool _hasProgress;
        [NonSerialized] private string _progressStatusLine = StatusEnteringPlayMode;
        [NonSerialized] private string _progressStepLine = string.Empty;
        [NonSerialized] private string _progressEpsilonLine = string.Empty;
        [NonSerialized] private int _progressPhaseIndex;

        private readonly StringBuilder _sb = new StringBuilder(160);

        private bool _tutorialHidden;
        private bool _setupExpanded = true;
        private bool _evidenceDirExists;
        private string _readyStatus = string.Empty;
        private string _setupSummary = string.Empty;
        private string _rangeText = string.Empty;
        private string _priorRunLabel = string.Empty;
        private GUIContent _stepsLabel;

        // --- A1 search entry state (canonical store = SessionState; fields mirror it) ---
        [NonSerialized] private DivergenceSettings _settingsAsset;
        private string _settingsAssetGuid = string.Empty;
        private bool _hasFloorOverride;
        private float _floorOverrideMetres;
        private bool _hasCeilingOverride;
        private float _ceilingOverrideMetres;
        private int _targetBodyId;
        private int _targetIndex;
        private GhostSearchTargetOption[] _targetOptions;
        private GUIContent[] _targetOptionContents;
        // A2 scene kind + edit-mode capture (dropdown + report; the runner re-captures
        // authoritatively at run start and that capture goes to the manifest).
        private int _sceneKindIndex;
        [NonSerialized] private SceneCaptureResult _sceneCapture;
        [NonSerialized] private string _captureSummaryRow = string.Empty;
        [NonSerialized] private string[] _captureDetailRows = Array.Empty<string>();
        // Edit buffers: committed via DelayedTextField; invalid text is kept on screen
        // with the field's verbatim reason (fail-closed, no silent revert).
        private string _floorText = string.Empty;
        private string _ceilingText = string.Empty;
        private bool _floorTextInvalid;
        private bool _ceilingTextInvalid;
        // Asset-drift watch: cheap per-frame float compares, re-resolve only on change.
        private float _assetFloorSeen;
        private float _assetCeilingSeen;
        [NonSerialized] private GhostSearchEntryResolution _resolution;

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
            _tutorialHidden = EditorPrefs.GetBool(TutorialHiddenKey, false);
            _setupExpanded = !EditorPrefs.GetBool(SetupCollapsedKey, false);

            LoadEntryState();
            RebuildStepsTooltip();
            RefreshEntry();
            RefreshEvidenceDirState();

            var session = GhostVisualizationSession.Ensure();
            session.ShowBaseline = _showBaseline;
            session.ShowFans = _showFans;
            if (session.Document != null)
            {
                // Live document survived (no domain reload) — restore the result block.
                _document = session.Document;
                _evidenceDir = session.EvidenceDirectory;
                _hasCompletion = true;
                _writeSucceeded = !string.IsNullOrEmpty(session.EvidenceDirectory);
                _completionStatus = _document.SearchResult.Verdict;
                BuildResultPresentation(_document);
                RefreshEvidenceDirState();
            }

            // Idempotent subscribe — avoid double-fire after domain reload / re-enable.
            GhostEvidencePlayModeHost.SearchCompleted -= OnHostSearchCompleted;
            GhostEvidencePlayModeHost.SearchCompleted += OnHostSearchCompleted;
            GhostEvidencePlayModeHost.SearchProgress -= OnHostSearchProgress;
            GhostEvidencePlayModeHost.SearchProgress += OnHostSearchProgress;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            UnityEditor.Compilation.CompilationPipeline.compilationStarted += OnCompilationEvent;
            UnityEditor.Compilation.CompilationPipeline.compilationFinished += OnCompilationEvent;
            // Asset deletion/move must re-validate the entry (fail-closed asset row).
            EditorApplication.projectChanged += OnProjectChanged;
            // A2: scene edits must re-capture (dropdown + report stay honest).
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        private void OnDisable()
        {
            GhostEvidencePlayModeHost.SearchCompleted -= OnHostSearchCompleted;
            GhostEvidencePlayModeHost.SearchProgress -= OnHostSearchProgress;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            UnityEditor.Compilation.CompilationPipeline.compilationStarted -= OnCompilationEvent;
            UnityEditor.Compilation.CompilationPipeline.compilationFinished -= OnCompilationEvent;
            EditorApplication.projectChanged -= OnProjectChanged;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            // Session survives window close for Scene View; do not dispose here.
        }

        private void OnProjectChanged()
        {
            RefreshEntry();
            Repaint();
        }

        private void OnHierarchyChanged()
        {
            // Only the captured-scene mode depends on the hierarchy; never re-capture
            // while the search pipeline runs (temporary physics scenes churn objects).
            if (_sceneKindIndex != (int)GhostSearchSceneKind.CapturedScene ||
                EditorApplication.isPlayingOrWillChangePlaymode ||
                GhostEvidencePlayModeHost.IsSearchBusy)
            {
                return;
            }

            RefreshEntry();
            Repaint();
        }

        // --- State machine (single source of truth) ---

        private WindowState ResolveState()
        {
            if (GhostEvidencePlayModeHost.IsSearchBusy && _startedFromThisWindow)
            {
                return WindowState.Searching;
            }

            if (IsCompiling() || IsPlayModeTransition() || IsForeignSearchLock() ||
                IsForeignPlayMode())
            {
                return WindowState.Idle;
            }

            return _hasCompletion ? WindowState.Done : WindowState.Ready;
        }

        private static bool IsCompiling()
        {
            return EditorApplication.isCompiling;
        }

        private static bool IsPlayModeTransition()
        {
            return EditorApplication.isPlayingOrWillChangePlaymode != EditorApplication.isPlaying;
        }

        private bool IsForeignSearchLock()
        {
            return GhostEvidencePlayModeHost.IsSearchBusy && !_startedFromThisWindow;
        }

        private bool IsForeignPlayMode()
        {
            return EditorApplication.isPlaying && !GhostEvidencePlayModeHost.IsSearchBusy;
        }

        // --- GUI ---

        private void OnGUI()
        {
            EnsureStyles();
            var state = ResolveState();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawStatusLine(state);

            // Result (or live progress) sits directly under the status line — the verdict
            // is the product; setup and the intro live below it.
            if (state == WindowState.Searching)
            {
                DrawProgress();
            }
            else if (_hasCompletion)
            {
                DrawResult();
            }
            else if (!string.IsNullOrEmpty(_evidenceDir))
            {
                DrawPriorRunRow();
            }

            if (!_tutorialHidden && state != WindowState.Searching)
            {
                DrawTutorial();
            }

            DrawSetup(state);
            DrawMainButton(state);

            EditorGUILayout.EndScrollView();
        }

        private void DrawStatusLine(WindowState state)
        {
            string status;
            if (state == WindowState.Searching)
            {
                status = _interrupting
                    ? StatusInterrupting
                    : (_hasProgress ? _progressStatusLine : StatusEnteringPlayMode);
            }
            else if (_hasCompletion)
            {
                // The verdict owns the status line even while a transient blocker is active
                // (e.g. the host-initiated Play Mode exit right after completion) — the
                // blocker still renders as a reason row under the disabled button.
                status = _document != null ? _verdictText : _completionStatus;
            }
            else if (state == WindowState.Idle)
            {
                status = StatusIdle;
            }
            else
            {
                status = _readyStatus;
            }

            EditorGUILayout.BeginHorizontal();
            StatusContent.text = status;
            EditorGUILayout.LabelField(StatusContent, EditorStyles.boldLabel);
            if (GUILayout.Button(HelpButtonContent, GUILayout.Width(24f)))
            {
                _tutorialHidden = false;
                EditorPrefs.SetBool(TutorialHiddenKey, false);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2f);
        }

        private void DrawTutorial()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                "3 шага: настрой параметры → запусти → смотри улики",
                EditorStyles.boldLabel);
            if (GUILayout.Button("скрыть", GUILayout.Width(60f)))
            {
                _tutorialHidden = true;
                EditorPrefs.SetBool(TutorialHiddenKey, true);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("1. Параметры — блок «Настройка» ниже");
            EditorGUILayout.LabelField("2. «Запустить поиск» — окно само войдёт в Play Mode");
            EditorGUILayout.LabelField("3. После вердикта — «Открыть папку улик»");
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2f);
        }

        private void DrawSetup(WindowState state)
        {
            var searching = state == WindowState.Searching;
            var expanded = _setupExpanded && !searching;

            var newExpanded = EditorGUILayout.Foldout(expanded, _setupSummary, true);
            if (!searching && newExpanded != _setupExpanded)
            {
                _setupExpanded = newExpanded;
                EditorPrefs.SetBool(SetupCollapsedKey, !newExpanded);
            }

            if (!expanded)
            {
                return;
            }

            if (_resolution == null)
            {
                RefreshEntry();
            }

            WatchAssetDrift();

            using (new EditorGUI.DisabledScope(state == WindowState.Idle))
            {
                EditorGUI.indentLevel++;

                EditorGUI.BeginChangeCheck();
                _sceneKindIndex = EditorGUILayout.Popup(
                    SceneKindLabel,
                    _sceneKindIndex,
                    SceneKindOptions);
                if (EditorGUI.EndChangeCheck())
                {
                    SessionState.SetInt(SceneKindKey, _sceneKindIndex);
                    RefreshEntry();
                }

                if (_sceneKindIndex == (int)GhostSearchSceneKind.CapturedScene)
                {
                    DrawCaptureReport();
                }

                EditorGUI.BeginChangeCheck();
                var newAsset = (DivergenceSettings)EditorGUILayout.ObjectField(
                    AssetLabel,
                    _settingsAsset,
                    typeof(DivergenceSettings),
                    false);
                if (EditorGUI.EndChangeCheck())
                {
                    ApplyAssetSelection(newAsset);
                }

                DrawFieldReason(_resolution.AssetReason);

                EditorGUI.BeginChangeCheck();
                _targetIndex = EditorGUILayout.Popup(TargetLabel, _targetIndex, _targetOptionContents);
                if (EditorGUI.EndChangeCheck() &&
                    _targetIndex >= 0 && _targetIndex < _targetOptions.Length)
                {
                    _targetBodyId = _targetOptions[_targetIndex].BodyId;
                    SessionState.SetInt(TargetBodyIdKey, _targetBodyId);
                    RefreshEntry();
                }

                DrawFieldReason(_resolution.TargetReason);

                EditorGUI.BeginChangeCheck();
                _axisIndex = EditorGUILayout.Popup(AxisLabel, _axisIndex, AxisOptions);
                _strategy = (EpsilonSearchStrategy)EditorGUILayout.EnumPopup(StrategyLabel, _strategy);
                _stepCount = EditorGUILayout.IntField(_stepsLabel, _stepCount);
                if (EditorGUI.EndChangeCheck())
                {
                    // No silent reset to a default step count (contract table): an invalid
                    // value stays on screen, the reason renders, the button disables.
                    RebuildStepsTooltip();
                    RefreshEntry();
                }

                DrawFieldReason(_resolution.StepsReason);

                DrawRangeRow();
                DrawFieldReason(_floorTextInvalid
                    ? GhostSearchEntryResolver.ReasonFloor
                    : _resolution.FloorReason);
                DrawFieldReason(_ceilingTextInvalid
                    ? GhostSearchEntryResolver.ReasonCeiling
                    : _resolution.CeilingReason);
                DrawFieldReason(_resolution.RatioReason);

                EditorGUILayout.LabelField(
                    "Источник: " + _resolution.SourceDescription,
                    _reasonStyle);

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(2f);
        }

        /// <summary>
        /// A2 capture report inside the setup block: counts + hash, kinematic-freeze
        /// warnings, every fail-closed reason, and a capped excluded/frozen list.
        /// </summary>
        private void DrawCaptureReport()
        {
            if (string.IsNullOrEmpty(_captureSummaryRow))
            {
                return;
            }

            EditorGUILayout.LabelField(_captureSummaryRow, _wrapLabelStyle);
            for (var i = 0; i < _captureDetailRows.Length; i++)
            {
                EditorGUILayout.LabelField(_captureDetailRows[i], _reasonStyle);
            }
        }

        private void DrawRangeRow()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(RangeLabel);

            var newFloorText = EditorGUILayout.DelayedTextField(_floorText);
            GUILayout.Label("…", GUILayout.Width(16f));
            var newCeilingText = EditorGUILayout.DelayedTextField(_ceilingText);

            using (new EditorGUI.DisabledScope(!_hasFloorOverride && !_hasCeilingOverride))
            {
                if (GUILayout.Button(ResetToSourceContent, GUILayout.Width(150f)))
                {
                    _hasFloorOverride = false;
                    _hasCeilingOverride = false;
                    _floorTextInvalid = false;
                    _ceilingTextInvalid = false;
                    SessionState.SetBool(HasFloorOverrideKey, false);
                    SessionState.SetBool(HasCeilingOverrideKey, false);
                    RefreshEntry();
                }
            }

            EditorGUILayout.EndHorizontal();

            if (!string.Equals(newFloorText, _floorText, StringComparison.Ordinal))
            {
                CommitRangeField(
                    newFloorText,
                    isFloor: true);
            }

            if (!string.Equals(newCeilingText, _ceilingText, StringComparison.Ordinal))
            {
                CommitRangeField(
                    newCeilingText,
                    isFloor: false);
            }
        }

        /// <summary>
        /// Commit one ε bound typed in millimetres. Unparseable input keeps the raw text
        /// and reports the field's verbatim table reason — never a silent revert. A parsed
        /// value equal to the current source value clears the override instead of pinning
        /// it (an explicit no-op edit is not a window override).
        /// </summary>
        private void CommitRangeField(string text, bool isFloor)
        {
            if (isFloor)
            {
                _floorText = text;
            }
            else
            {
                _ceilingText = text;
            }

            if (!GhostSearchEntryResolver.TryParseMillimetresToMetres(text, out var metres))
            {
                if (isFloor)
                {
                    _floorTextInvalid = true;
                }
                else
                {
                    _ceilingTextInvalid = true;
                }

                Repaint();
                return;
            }

            var sourceValue = isFloor ? SourceFloorMetres() : SourceCeilingMetres();
            var isOverride = metres != sourceValue;
            if (isFloor)
            {
                _floorTextInvalid = false;
                _hasFloorOverride = isOverride;
                _floorOverrideMetres = metres;
                SessionState.SetBool(HasFloorOverrideKey, isOverride);
                SessionState.SetFloat(FloorOverrideKey, metres);
            }
            else
            {
                _ceilingTextInvalid = false;
                _hasCeilingOverride = isOverride;
                _ceilingOverrideMetres = metres;
                SessionState.SetBool(HasCeilingOverrideKey, isOverride);
                SessionState.SetFloat(CeilingOverrideKey, metres);
            }

            RefreshEntry();
        }

        private float SourceFloorMetres()
        {
            return _settingsAsset != null
                ? _settingsAsset.EpsilonStart
                : DivergenceSettings.DefaultEpsilonStart;
        }

        private float SourceCeilingMetres()
        {
            return _settingsAsset != null
                ? _settingsAsset.EpsilonCeiling
                : DivergenceSettings.DefaultEpsilonCeiling;
        }

        private void DrawFieldReason(string reason)
        {
            if (!string.IsNullOrEmpty(reason))
            {
                EditorGUILayout.LabelField("⚠ " + reason, _reasonStyle);
            }
        }

        /// <summary>
        /// External edits to the assigned asset (Inspector) must re-validate and refresh
        /// non-overridden field texts. Two float reads per frame, re-resolve only on change;
        /// a destroyed asset reference re-resolves into the fail-closed asset row.
        /// </summary>
        private void WatchAssetDrift()
        {
            if (string.IsNullOrEmpty(_settingsAssetGuid))
            {
                return;
            }

            if (_settingsAsset == null)
            {
                if (_resolution != null && _resolution.AssetReason.Length == 0)
                {
                    RefreshEntry();
                }

                return;
            }

            if (_settingsAsset.EpsilonStart != _assetFloorSeen ||
                _settingsAsset.EpsilonCeiling != _assetCeilingSeen)
            {
                RefreshEntry();
            }
        }

        private void ApplyAssetSelection(DivergenceSettings newAsset)
        {
            if (newAsset == null)
            {
                _settingsAsset = null;
                _settingsAssetGuid = string.Empty;
            }
            else
            {
                var path = AssetDatabase.GetAssetPath(newAsset);
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid))
                {
                    // Not a persisted asset (e.g. a runtime instance) — cannot travel by
                    // GUID through the Play Mode reload; refuse instead of half-working.
                    Debug.LogWarning(
                        "BugCam: настройки должны быть ассетом проекта (GUID); " +
                        "runtime-экземпляр не принят.");
                    return;
                }

                _settingsAsset = newAsset;
                _settingsAssetGuid = guid;
            }

            SessionState.SetString(AssetGuidKey, _settingsAssetGuid);
            RefreshEntry();
        }

        private void DrawMainButton(WindowState state)
        {
            var entryValid = IsEntryValid();
            var runnable = (state == WindowState.Ready || state == WindowState.Done) && entryValid;
            using (new EditorGUI.DisabledScope(!runnable))
            {
                if (GUILayout.Button(RunButtonContent, GUILayout.Height(32f)))
                {
                    StartSearch();
                }
            }

            if ((state == WindowState.Ready || state == WindowState.Done) && !entryValid)
            {
                EditorGUILayout.LabelField(
                    "Причина: параметры невалидны — " + FirstEntryReason(),
                    _reasonStyle);
            }

            if (state == WindowState.Searching)
            {
                EditorGUILayout.LabelField(ReasonSearching, _reasonStyle);
            }
            else if (state == WindowState.Idle)
            {
                // Exhaustive transient blockers, one row each; several can be active at once.
                if (IsCompiling())
                {
                    EditorGUILayout.LabelField(ReasonCompiling, _reasonStyle);
                }

                if (IsPlayModeTransition())
                {
                    EditorGUILayout.LabelField(ReasonPlayTransition, _reasonStyle);
                }

                if (IsForeignSearchLock())
                {
                    EditorGUILayout.LabelField(ReasonForeignLock, _reasonStyle);
                }

                if (IsForeignPlayMode())
                {
                    EditorGUILayout.LabelField(ReasonForeignPlayMode, _reasonStyle);
                }
            }

            EditorGUILayout.Space(4f);
        }

        private void DrawProgress()
        {
            EditorGUILayout.LabelField("Прогресс", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField(PhaseStripByPhase[_progressPhaseIndex]);
            if (_hasProgress)
            {
                EditorGUILayout.LabelField(_progressStepLine);
                if (!string.IsNullOrEmpty(_progressEpsilonLine))
                {
                    EditorGUILayout.LabelField(_progressEpsilonLine);
                }
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4f);

            using (new EditorGUI.DisabledScope(_interrupting))
            {
                if (GUILayout.Button(
                        _interrupting ? InterruptingContent : InterruptContent,
                        GUILayout.Width(120f)))
                {
                    _interrupting = true;
                    // Host cleanup on ExitingPlayMode owns the completion notification —
                    // the verdict line stays verbatim from the host, never synthesized here.
                    EditorApplication.isPlaying = false;
                    Repaint();
                }
            }
        }

        private void DrawResult()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Результат", EditorStyles.boldLabel);

            if (_document == null)
            {
                // Interrupted: neutral — no invented verdict, no numbers.
                EditorGUILayout.LabelField(_completionStatus, _wrapLabelStyle);
                EditorGUILayout.LabelField(InterruptedExplanation, _wrapLabelStyle);
                EditorGUILayout.Space(4f);
                using (new EditorGUI.DisabledScope(true))
                {
                    GUILayout.Button("Открыть папку улик");
                }

                EditorGUILayout.LabelField(ReasonEvidenceNotWritten, _reasonStyle);
                return;
            }

            EditorGUILayout.LabelField(_verdictText, _verdictStyle);
            // A2 contract: kinematic-freeze warnings belong to the result verdict block.
            if (_document.SceneCapture.Performed)
            {
                var captureWarnings = _document.SceneCapture.KinematicFreezeWarnings;
                for (var i = 0; i < captureWarnings.Length; i++)
                {
                    EditorGUILayout.HelpBox(captureWarnings[i], MessageType.Warning);
                }
            }

            if (!string.IsNullOrEmpty(_verdictMeaning))
            {
                EditorGUILayout.LabelField(_verdictMeaning, _wrapLabelStyle);
            }

            EditorGUILayout.Space(4f);
            for (var i = 0; i < _resultRows.Length; i++)
            {
                EditorGUILayout.LabelField(_resultRows[i].Label, _resultRows[i].Value);
            }

            if (!_writeSucceeded)
            {
                EditorGUILayout.Space(2f);
                EditorGUILayout.HelpBox(_completionStatus, MessageType.Error);
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();

            var canOpenFolder = _writeSucceeded && _evidenceDirExists;
            using (new EditorGUI.DisabledScope(!canOpenFolder))
            {
                if (GUILayout.Button("Открыть папку улик"))
                {
                    OpenEvidenceFolder();
                }
            }

            var hasFans = _document.Fans != null && _document.Fans.Length > 0;
            using (new EditorGUI.DisabledScope(!hasFans))
            {
                if (GUILayout.Button("Показать в Scene View"))
                {
                    ShowInSceneView();
                }
            }

            EditorGUILayout.EndHorizontal();

            if (!canOpenFolder)
            {
                EditorGUILayout.LabelField(
                    !_writeSucceeded ? ReasonEvidenceWriteFailed : ReasonEvidenceDirMissing,
                    _reasonStyle);
            }

            if (!hasFans)
            {
                EditorGUILayout.LabelField(ReasonNoFans, _reasonStyle);
            }
            else
            {
                DrawSceneViewControls();
            }
        }

        private void DrawSceneViewControls()
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Scene View", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            EditorGUI.BeginChangeCheck();
            _showBaseline = EditorGUILayout.Toggle("Baseline (белый)", _showBaseline);
            _showFans = EditorGUILayout.Toggle("Fans (цветные)", _showFans);
            if (EditorGUI.EndChangeCheck())
            {
                var session = GhostVisualizationSession.Ensure();
                session.ShowBaseline = _showBaseline;
                session.ShowFans = _showFans;
                session.IsVisible = true;
                SceneView.RepaintAll();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("К первому расхождению"))
            {
                GhostVisualizationSession.Ensure().FrameFirstDivergence();
            }

            if (GUILayout.Button("К макс. разбросу"))
            {
                GhostVisualizationSession.Ensure().FrameMaxSpread();
            }

            if (GUILayout.Button("Скрыть"))
            {
                GhostVisualizationSession.Ensure().IsVisible = false;
                SceneView.RepaintAll();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel--;
        }

        private void DrawPriorRunRow()
        {
            // Domain reload killed the document: no stale numbers — only the saved path.
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(_priorRunLabel);
            using (new EditorGUI.DisabledScope(!_evidenceDirExists))
            {
                if (GUILayout.Button("Открыть папку улик", GUILayout.Width(180f)))
                {
                    OpenEvidenceFolder();
                }
            }

            if (!_evidenceDirExists)
            {
                EditorGUILayout.LabelField(ReasonEvidenceDirMissing, _reasonStyle);
            }
        }

        // --- Actions ---

        private void StartSearch()
        {
            // Authoritative re-validation at click time (the button gate is UI state and
            // may be one event stale — the host rejects too, fail-closed all the way).
            RefreshEntry();
            if (!IsEntryValid())
            {
                Repaint();
                return;
            }

            if (!GhostEvidencePlayModeHost.TryStartTowerSearch(
                    BuildEntry(),
                    GhostEvidencePlayModeHost.SourceWindow,
                    out var rejectReason))
            {
                // Race with a menu-started search: the state machine now renders the
                // foreign-lock reason; the verbatim reject goes to the console.
                Debug.LogWarning(rejectReason);
                Repaint();
                return;
            }

            _startedFromThisWindow = true;
            _hasCompletion = false;
            _document = null;
            _resultRows = Array.Empty<ResultRow>();
            _hasProgress = false;
            _interrupting = false;
            _progressPhaseIndex = (int)EpsilonSearchPhase.NotStarted;
            _progressStatusLine = StatusEnteringPlayMode;
            _progressStepLine = string.Empty;
            _progressEpsilonLine = string.Empty;
            Repaint();
        }

        private void ShowInSceneView()
        {
            var session = GhostVisualizationSession.Ensure();
            session.IsVisible = true;
            if (_document != null && _document.DrawSet.HasFirstDivergence)
            {
                session.FrameFirstDivergence();
            }
            else
            {
                session.FrameOverview();
            }

            SceneView.RepaintAll();
        }

        private void OpenEvidenceFolder()
        {
            RefreshEvidenceDirState();
            if (!_evidenceDirExists)
            {
                Repaint();
                return;
            }

            EditorUtility.RevealInFinder(_evidenceDir);
        }

        // --- Host events (the only repaint sources besides user interaction) ---

        private void OnHostSearchProgress(GhostSearchProgress progress)
        {
            if (!_startedFromThisWindow)
            {
                return;
            }

            _hasProgress = true;
            var phaseIndex = (int)progress.Phase;
            if (phaseIndex < 0 || phaseIndex >= PhaseStripByPhase.Length)
            {
                phaseIndex = 0;
            }

            _progressPhaseIndex = phaseIndex;

            _sb.Clear();
            _sb.Append("Поиск: фаза ").Append(PhaseNameByPhase[phaseIndex]);
            if (progress.CurrentStep > 0)
            {
                _sb.Append(", шаг ").Append(progress.CurrentStep);
                _sb.Append(progress.StepTotal > 0 ? "/" : " / ");
                if (progress.StepTotal > 0)
                {
                    _sb.Append(progress.StepTotal);
                }
                else
                {
                    _sb.Append('—');
                }
            }

            _progressStatusLine = _sb.ToString();

            _sb.Clear();
            _sb.Append("Шаг: ");
            if (progress.CurrentStep > 0)
            {
                _sb.Append(progress.CurrentStep);
                if (progress.StepTotal > 0)
                {
                    _sb.Append('/').Append(progress.StepTotal);
                }
                else
                {
                    _sb.Append(" / —");
                }
            }
            else
            {
                _sb.Append('—');
            }

            _progressStepLine = _sb.ToString();

            if (progress.HasEpsilon)
            {
                _sb.Clear();
                _sb.Append("Epsilon: ").Append(FormatMetres(progress.EpsilonMetres));
                _progressEpsilonLine = _sb.ToString();
            }

            Repaint();
        }

        private void OnHostSearchCompleted(GhostSearchCompletion completion)
        {
            _interrupting = false;
            _hasProgress = false;
            _startedFromThisWindow = false;
            _hasCompletion = true;
            _completionStatus = completion.Status ?? string.Empty;
            _writeSucceeded = completion.WriteSucceeded;
            _document = completion.Document;

            if (completion.Document != null && completion.WriteSucceeded)
            {
                _evidenceDir = completion.Write.RunDirectory ?? string.Empty;

                var session = GhostVisualizationSession.Ensure();
                session.ShowBaseline = _showBaseline;
                session.ShowFans = _showFans;

                if (completion.Document.Success)
                {
                    // First successful run collapses the setup block (state in EditorPrefs).
                    _setupExpanded = false;
                    EditorPrefs.SetBool(SetupCollapsedKey, true);
                }
            }

            if (completion.Document != null)
            {
                BuildResultPresentation(completion.Document);
            }
            else
            {
                _resultRows = Array.Empty<ResultRow>();
                _verdictText = string.Empty;
                _verdictMeaning = string.Empty;
            }

            RefreshEvidenceDirState();
            Repaint();
            SceneView.RepaintAll();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            Repaint();
        }

        private void OnCompilationEvent(object context)
        {
            Repaint();
        }

        // --- Presentation building (event-time only; never per frame) ---

        private void BuildResultPresentation(GhostEvidenceDocument document)
        {
            var search = document.SearchResult;
            _verdictText = search.Verdict;
            _verdictMeaning = MeaningFor(search.VerdictKind);

            var rows = new System.Collections.Generic.List<ResultRow>(8);

            if (!document.Success)
            {
                // Engine FAILED / INCOMPLETE: verbatim verdict + error reason, no numbers.
                if (!string.IsNullOrEmpty(document.ErrorReason))
                {
                    rows.Add(new ResultRow("Ошибка", document.ErrorCode + ": " + document.ErrorReason));
                }

                AddSettingsSourceRows(rows, document);
                _resultRows = rows.ToArray();
                return;
            }

            var rangeValue = FormatRange(search.SearchRangeStartMetres, search.SearchRangeCeilingMetres);
            switch (search.VerdictKind)
            {
                case EpsilonSearchVerdictKind.ThresholdBracketFound:
                    if (search.HasThresholdEstimate)
                    {
                        rows.Add(new ResultRow("Порог (оценка)", FormatMetres(search.ThresholdEstimateMetres)));
                        rows.Add(new ResultRow(
                            "Вилка",
                            FormatRange(search.LargestStableEpsilonMetres, search.SmallestDivergentEpsilonMetres)));
                        rows.Add(new ResultRow("Ширина вилки", FormatMetres(search.FinalBracketWidthMetres)));
                    }

                    break;

                case EpsilonSearchVerdictKind.StableWithinTestedRange:
                    rows.Add(new ResultRow("Проверенный диапазон", rangeValue));
                    if (search.HasLargestStableEpsilon)
                    {
                        rows.Add(new ResultRow("Макс. стабильный ε", FormatMetres(search.LargestStableEpsilonMetres)));
                    }

                    rows.Add(new ResultRow(
                        "Тел разошлось",
                        "0 из " + (document.SceneCapture.Performed
                            ? document.SceneCapture.Bodies.Length
                            : BugCam.Core.TowerProbeRequestFactory.ExpectedBodyCount)
                        .ToString(CultureInfo.InvariantCulture)));
                    break;

                case EpsilonSearchVerdictKind.NonMonotonicWithinTestedRange:
                    rows.Add(new ResultRow("Проверенный диапазон", rangeValue));
                    if (GhostEvidenceWriter.HasReferenceEpsilon(search))
                    {
                        rows.Add(new ResultRow(
                            "Эталонный ε (фан)",
                            FormatMetres(search.ReferenceEpsilonMetres) + " — не порог"));
                    }

                    break;

                case EpsilonSearchVerdictKind.DivergentAtSearchFloor:
                    rows.Add(new ResultRow("Проверенный диапазон", rangeValue));
                    if (search.HasSmallestDivergentEpsilon)
                    {
                        rows.Add(new ResultRow(
                            "Мин. расходящийся ε",
                            FormatMetres(search.SmallestDivergentEpsilonMetres) + " (= пол поиска)"));
                    }

                    break;
            }

            if (GhostEvidenceWriter.HasPrimaryDivergenceMetrics(document))
            {
                var primary = document.PrimaryDivergence;
                rows.Add(new ResultRow(
                    "Первый кадр",
                    primary.FirstDivergenceFrame.ToString(CultureInfo.InvariantCulture) +
                    (primary.FirstDivergenceBodyId >= 0
                        ? " (body " + primary.FirstDivergenceBodyId.ToString(CultureInfo.InvariantCulture) + ")"
                        : string.Empty)));
                rows.Add(new ResultRow(
                    "Разброс (макс)",
                    FormatMetres(primary.MaxSpreadMetres) +
                    " (body " + primary.MaxSpreadBodyId.ToString(CultureInfo.InvariantCulture) + ")"));
                if (primary.AmplificationDefined)
                {
                    rows.Add(new ResultRow(
                        "Усиление",
                        Math.Round((double)primary.Amplification)
                            .ToString("0", CultureInfo.InvariantCulture) + "×"));
                }

                rows.Add(new ResultRow(
                    "Тел разошлось",
                    primary.AffectedBodyCount.ToString(CultureInfo.InvariantCulture) +
                    " из " + primary.BodyCount.ToString(CultureInfo.InvariantCulture) +
                    " — полный список в уликах"));
            }

            AddSceneCaptureRows(rows, document);
            AddSettingsSourceRows(rows, document);
            _resultRows = rows.ToArray();
        }

        /// <summary>
        /// A1 contract: the settings source and the effective epsilon bounds belong to the
        /// result, not only to the manifest. Captured=false (pre-A1 document) adds no rows —
        /// never fabricate a source.
        /// </summary>
        /// <summary>
        /// A2: scene-capture provenance belongs to the result, not only to the manifest.
        /// Performed=false (tower run) adds no rows.
        /// </summary>
        private static void AddSceneCaptureRows(
            System.Collections.Generic.List<ResultRow> rows,
            GhostEvidenceDocument document)
        {
            var capture = document.SceneCapture;
            if (!capture.Performed)
            {
                return;
            }

            rows.Add(new ResultRow(
                "Захват сцены",
                (capture.Succeeded ? "захвачено" : "fail-closed") + ", " +
                capture.Bodies.Length.ToString(CultureInfo.InvariantCulture) + " тел, hash " +
                (capture.CaptureHash.Length >= 12
                    ? capture.CaptureHash.Substring(0, 12) + "…"
                    : capture.CaptureHash)));
        }

        private static void AddSettingsSourceRows(
            System.Collections.Generic.List<ResultRow> rows,
            GhostEvidenceDocument document)
        {
            var source = document.SettingsSource;
            if (!source.Captured)
            {
                return;
            }

            rows.Add(new ResultRow("Источник настроек", source.Description));
            rows.Add(new ResultRow(
                "Диапазон ε (эффективный)",
                FormatRange(source.EffectiveFloorMetres, source.EffectiveCeilingMetres)));
        }

        private static string MeaningFor(EpsilonSearchVerdictKind kind)
        {
            switch (kind)
            {
                case EpsilonSearchVerdictKind.ThresholdBracketFound:
                    return "Найдена вилка порога: ниже нижней границы вилки сцена стабильна, " +
                           "выше верхней — устойчиво расходится.";
                case EpsilonSearchVerdictKind.StableWithinTestedRange:
                    return "Во всём проверенном диапазоне возмущений устойчивого расхождения нет. " +
                           "Это полный, валидный результат.";
                case EpsilonSearchVerdictKind.NonMonotonicWithinTestedRange:
                    return "Расхождение в диапазоне есть, но без монотонной границы: вилку порога " +
                           "построить нельзя. Фан построен вокруг эталонного ε — это не порог.";
                case EpsilonSearchVerdictKind.DivergentAtSearchFloor:
                    return "Расхождение уже при минимальном проверенном возмущении — стабильной " +
                           "нижней границы в диапазоне нет. Порог, если существует, ниже пола поиска.";
                default:
                    return string.Empty;
            }
        }

        /// <summary>Restore the A1 entry from SessionState (asset by GUID) on enable.</summary>
        private void LoadEntryState()
        {
            _settingsAssetGuid = SessionState.GetString(AssetGuidKey, string.Empty);
            _settingsAsset = null;
            if (!string.IsNullOrEmpty(_settingsAssetGuid))
            {
                var path = AssetDatabase.GUIDToAssetPath(_settingsAssetGuid);
                if (!string.IsNullOrEmpty(path))
                {
                    _settingsAsset = AssetDatabase.LoadAssetAtPath<DivergenceSettings>(path);
                }
            }

            _hasFloorOverride = SessionState.GetBool(HasFloorOverrideKey, false);
            _floorOverrideMetres = SessionState.GetFloat(FloorOverrideKey, 0f);
            _hasCeilingOverride = SessionState.GetBool(HasCeilingOverrideKey, false);
            _ceilingOverrideMetres = SessionState.GetFloat(CeilingOverrideKey, 0f);
            _targetBodyId = SessionState.GetInt(
                TargetBodyIdKey,
                GhostSearchTargetCatalog.TowerDefaultTargetBodyId);
            _sceneKindIndex = SessionState.GetInt(
                SceneKindKey,
                (int)GhostSearchSceneKind.Tower);
        }

        /// <summary>
        /// Re-capture (scene mode) and rebuild the target dropdown from the mode's
        /// display-name provider. A stored target missing from the new option set snaps
        /// to the mode default visibly (the dropdown shows the new selection) and is
        /// persisted — the catalogs of the two modes are disjoint by design.
        /// </summary>
        private void RebuildTargetOptions()
        {
            if (_sceneKindIndex == (int)GhostSearchSceneKind.CapturedScene)
            {
                _sceneCapture = SceneCapture.Capture(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                _targetOptions = GhostSearchTargetCatalog.SceneOptions(_sceneCapture);
            }
            else
            {
                _sceneCapture = default;
                _targetOptions = GhostSearchTargetCatalog.TowerOptions();
            }

            _targetOptionContents = new GUIContent[_targetOptions.Length];
            var found = false;
            for (var i = 0; i < _targetOptions.Length; i++)
            {
                _targetOptionContents[i] = new GUIContent(_targetOptions[i].DisplayName);
                if (_targetOptions[i].BodyId == _targetBodyId)
                {
                    _targetIndex = i;
                    found = true;
                }
            }

            if (!found && _targetOptions.Length > 0)
            {
                _targetIndex = _sceneKindIndex == (int)GhostSearchSceneKind.Tower
                    ? _targetOptions.Length - 1
                    : 0;
                for (var i = 0; i < _targetOptions.Length; i++)
                {
                    if (_targetOptions[i].BodyId ==
                        GhostSearchTargetCatalog.TowerDefaultTargetBodyId &&
                        _sceneKindIndex == (int)GhostSearchSceneKind.Tower)
                    {
                        _targetIndex = i;
                        break;
                    }
                }

                _targetBodyId = _targetOptions[_targetIndex].BodyId;
                SessionState.SetInt(TargetBodyIdKey, _targetBodyId);
            }
            else if (!found)
            {
                _targetIndex = 0;
            }

            BuildCaptureReportRows();
        }

        /// <summary>Event-time capture report strings for the setup block (scene mode).</summary>
        private void BuildCaptureReportRows()
        {
            if (_sceneKindIndex != (int)GhostSearchSceneKind.CapturedScene ||
                !_sceneCapture.Performed)
            {
                _captureSummaryRow = string.Empty;
                _captureDetailRows = Array.Empty<string>();
                return;
            }

            var frozen = 0;
            var excluded = 0;
            var failed = 0;
            var statics = 0;
            for (var i = 0; i < _sceneCapture.Objects.Length; i++)
            {
                switch (_sceneCapture.Objects[i].Status)
                {
                    case SceneCaptureObjectStatus.FrozenKinematic:
                        frozen++;
                        break;
                    case SceneCaptureObjectStatus.ExcludedSafely:
                        excluded++;
                        break;
                    case SceneCaptureObjectStatus.Failed:
                        failed++;
                        break;
                    case SceneCaptureObjectStatus.CapturedStatic:
                        statics++;
                        break;
                }
            }

            _sb.Clear();
            _sb.Append(_sceneCapture.Succeeded ? "Захват: " : "Захват fail-closed: ");
            _sb.Append(_sceneCapture.Bodies.Length).Append(" тел, ");
            _sb.Append(statics).Append(" статик, ");
            _sb.Append(frozen).Append(" заморожено, ");
            _sb.Append(excluded).Append(" исключено");
            if (failed > 0)
            {
                _sb.Append(", ").Append(failed).Append(" непредставимо");
            }

            if (_sceneCapture.CaptureHash.Length >= 12)
            {
                _sb.Append("   hash ").Append(_sceneCapture.CaptureHash, 0, 12).Append('…');
            }

            _captureSummaryRow = _sb.ToString();

            // Warnings + every fail-closed reason + non-plain rows, capped for the UI —
            // the manifest of the next run carries the full list.
            var rows = new System.Collections.Generic.List<string>(8);
            for (var i = 0; i < _sceneCapture.KinematicFreezeWarnings.Length; i++)
            {
                rows.Add("⚠ " + _sceneCapture.KinematicFreezeWarnings[i]);
            }

            // Sleeping-body notice: capture report + manifest only, never the verdict.
            for (var i = 0; i < _sceneCapture.SleepingBodyWarnings.Length; i++)
            {
                rows.Add("⚠ " + _sceneCapture.SleepingBodyWarnings[i]);
            }

            for (var i = 0; i < _sceneCapture.Objects.Length; i++)
            {
                var record = _sceneCapture.Objects[i];
                if (record.Status == SceneCaptureObjectStatus.Failed)
                {
                    rows.Add("✗ «" + record.HierarchyPath + "»: " + record.Reason);
                }
            }

            // 2.2.2: mesh-reference rows — the capture report names every captured mesh
            // with its asset identity and geometry-hash prefix (full values in manifest).
            var meshShown = 0;
            var meshTotal = 0;
            for (var i = 0; i < _sceneCapture.Objects.Length; i++)
            {
                var record = _sceneCapture.Objects[i];
                if (!record.HasMeshReference)
                {
                    continue;
                }

                meshTotal++;
                if (meshShown < 8)
                {
                    var reference = record.MeshReference;
                    var hashPrefix = reference.ContentHash.Length >= 12
                        ? reference.ContentHash.Substring(0, 12) + "…"
                        : reference.ContentHash;
                    rows.Add("◆ меш «" + record.HierarchyPath + "»: " +
                             reference.MeshName + " (" +
                             (reference.Convex ? "convex" : "non-convex") + ") " +
                             reference.AssetGuid + "/" +
                             reference.LocalFileId.ToString(CultureInfo.InvariantCulture) +
                             "  hash " + hashPrefix);
                    meshShown++;
                }
            }

            if (meshTotal > meshShown)
            {
                rows.Add("◆ … ещё " + (meshTotal - meshShown) +
                         " меш-ссылок — полный список в manifest прогона");
            }

            var otherShown = 0;
            var otherTotal = 0;
            for (var i = 0; i < _sceneCapture.Objects.Length; i++)
            {
                var record = _sceneCapture.Objects[i];
                if (record.Status != SceneCaptureObjectStatus.ExcludedSafely &&
                    record.Status != SceneCaptureObjectStatus.FrozenKinematic)
                {
                    continue;
                }

                otherTotal++;
                if (otherShown < 8)
                {
                    rows.Add("· «" + record.HierarchyPath + "»: " + record.Reason);
                    otherShown++;
                }
            }

            if (otherTotal > otherShown)
            {
                rows.Add("· … ещё " + (otherTotal - otherShown) +
                         " исключённых/замороженных — полный список в manifest прогона");
            }

            _captureDetailRows = rows.ToArray();
        }

        private GhostSearchEntry BuildEntry()
        {
            return new GhostSearchEntry(
                _stepCount,
                _strategy,
                AxisVectors[_axisIndex],
                _targetBodyId,
                _settingsAssetGuid,
                _hasFloorOverride,
                _floorOverrideMetres,
                _hasCeilingOverride,
                _ceilingOverrideMetres,
                (GhostSearchSceneKind)_sceneKindIndex);
        }

        /// <summary>
        /// Re-resolve the entry (single settings path), refresh the effective-range texts
        /// for bounds without an active override or a pending invalid edit, and rebuild
        /// the cached summary strings. Event-time only — never per frame.
        /// </summary>
        private void RefreshEntry()
        {
            RebuildTargetOptions();
            _resolution = GhostSearchEntryResolver.Resolve(BuildEntry());
            _assetFloorSeen = _settingsAsset != null ? _settingsAsset.EpsilonStart : 0f;
            _assetCeilingSeen = _settingsAsset != null ? _settingsAsset.EpsilonCeiling : 0f;
            if (_sceneKindIndex == (int)GhostSearchSceneKind.CapturedScene)
            {
                _readyStatus = _sceneCapture.Succeeded
                    ? "Готово к поиску: " +
                      _sceneCapture.Bodies.Length.ToString(CultureInfo.InvariantCulture) +
                      " тел (захват сцены)"
                    : "Захват сцены fail-closed — причины в блоке «Настройка»";
            }
            else
            {
                _readyStatus = "Готово к поиску: " +
                    BugCam.Core.TowerProbeRequestFactory.ExpectedBodyCount.ToString(
                        CultureInfo.InvariantCulture) + " тел";
            }

            if (!_floorTextInvalid)
            {
                _floorText = GhostSearchEntryResolver.MillimetresTextFromMetres(
                    _resolution.EffectiveFloorMetres);
            }

            if (!_ceilingTextInvalid)
            {
                _ceilingText = GhostSearchEntryResolver.MillimetresTextFromMetres(
                    _resolution.EffectiveCeilingMetres);
            }

            _rangeText = FormatRange(
                _resolution.EffectiveFloorMetres,
                _resolution.EffectiveCeilingMetres);
            RebuildSetupSummary();
        }

        private bool IsEntryValid()
        {
            return _resolution != null &&
                   _resolution.IsValid &&
                   !_floorTextInvalid &&
                   !_ceilingTextInvalid;
        }

        /// <summary>First reason in table order, including pending invalid edit buffers.</summary>
        private string FirstEntryReason()
        {
            if (_floorTextInvalid)
            {
                return GhostSearchEntryResolver.ReasonFloor;
            }

            if (_ceilingTextInvalid)
            {
                return GhostSearchEntryResolver.ReasonCeiling;
            }

            return _resolution != null ? _resolution.FirstReason : string.Empty;
        }

        private static string SourceShort(string sourceKind)
        {
            switch (sourceKind)
            {
                case "asset":
                    return "ассет";
                case "asset+window":
                    return "ассет+правка";
                case "defaults+window":
                    return "дефолты+правка";
                default:
                    return "дефолты";
            }
        }

        private void RebuildStepsTooltip()
        {
            // The harness steps with BugCamConstants.FixedStep — a constant, not the
            // project's Time.fixedDeltaTime — so the duration must be computed from it.
            var seconds = _stepCount * BugCamConstants.FixedStep;
            _stepsLabel = new GUIContent(
                "Шагов",
                "Шаги физики × BugCamConstants.FixedStep (" +
                BugCamConstants.FixedStep.ToString("R", CultureInfo.InvariantCulture) + " с). " +
                _stepCount.ToString(CultureInfo.InvariantCulture) + " шага(-ов) = " +
                ThreeSignificant(seconds) + " с симуляции.");
        }

        private void RebuildSetupSummary()
        {
            _sb.Clear();
            _sb.Append("Настройка   (");
            _sb.Append(_sceneKindIndex == (int)GhostSearchSceneKind.CapturedScene
                ? "сцена, b"
                : "башня, b");
            _sb.Append(_targetBodyId);
            _sb.Append(", ").Append(AxisOptions[_axisIndex].text);
            _sb.Append(", ").Append(_strategy);
            _sb.Append(", ").Append(_stepCount).Append(" шагов, ε ").Append(_rangeText);
            _sb.Append(", ").Append(SourceShort(_resolution != null ? _resolution.SourceKind : "defaults"));
            _sb.Append(')');
            _setupSummary = _sb.ToString();
        }

        private void RefreshEvidenceDirState()
        {
            _evidenceDirExists = !string.IsNullOrEmpty(_evidenceDir) && Directory.Exists(_evidenceDir);
            _priorRunLabel = string.IsNullOrEmpty(_evidenceDir)
                ? string.Empty
                : "Прошлый прогон: " + Path.GetFileName(_evidenceDir.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar));
        }

        private static void EnsureStyles()
        {
            if (_verdictStyle == null)
            {
                _verdictStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 18,
                    wordWrap = true
                };
            }

            if (_wrapLabelStyle == null)
            {
                _wrapLabelStyle = new GUIStyle(EditorStyles.label) { wordWrap = true };
            }

            if (_reasonStyle == null)
            {
                _reasonStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            }
        }

        // Display formatting only (SPEC §17 style: "0.27 mm", "1.74 m", "6444×").
        // Full "R" precision stays in the evidence bundle and metrics files.

        private static string FormatRange(float floorMetres, float ceilingMetres)
        {
            // One shared unit per range, chosen by the larger bound, so the two numbers
            // stay directly comparable ("0.1 … 10 mm", never "100 µm … 10 mm").
            var unitScale = UnitFor(Math.Max(Math.Abs(floorMetres), Math.Abs(ceilingMetres)), out var unitName);
            return ThreeSignificant(floorMetres * unitScale) + " … " +
                   ThreeSignificant(ceilingMetres * unitScale) + " " + unitName;
        }

        private static string FormatMetres(float metres)
        {
            var unitScale = UnitFor(Math.Abs(metres), out var unitName);
            return ThreeSignificant(metres * unitScale) + " " + unitName;
        }

        private static float UnitFor(float absMetres, out string unitName)
        {
            if (absMetres >= 0.1f || absMetres == 0f)
            {
                unitName = "m";
                return 1f;
            }

            if (absMetres >= 0.0001f)
            {
                unitName = "mm";
                return 1000f;
            }

            unitName = "µm";
            return 1000000f;
        }

        private static string ThreeSignificant(double value)
        {
            if (value == 0d)
            {
                return "0";
            }

            var abs = Math.Abs(value);
            var digits = (int)Math.Floor(Math.Log10(abs)) + 1;
            if (digits >= 3)
            {
                var scale = Math.Pow(10d, digits - 3);
                return (Math.Round(value / scale) * scale)
                    .ToString("0", CultureInfo.InvariantCulture);
            }

            var decimals = 3 - digits;
            if (decimals > 15)
            {
                decimals = 15;
            }

            return Math.Round(value, decimals)
                .ToString("0." + new string('#', decimals), CultureInfo.InvariantCulture);
        }
    }
}
#endif
