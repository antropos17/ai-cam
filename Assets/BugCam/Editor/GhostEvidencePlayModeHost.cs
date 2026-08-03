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
        private const string EnteredPlayModeKey = "BugCam.GhostHost.EnteredPlayMode";
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

        /// <summary>
        /// Raised on the main thread whenever the live search advances (phase / step /
        /// epsilon change). Block 2.2: fed by the runner MonoBehaviour polling the Core
        /// read-only progress accessors once per frame — the window repaints only on
        /// these events, never on editor ticks. Real probe steps only, no synthesis.
        /// </summary>
        public static event Action<GhostSearchProgress> SearchProgress;

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
                    GhostSearchEntry.Tower(
                        32,
                        EpsilonSearchStrategy.AscendFromStart,
                        Vector3.right),
                    SourceMenu,
                    out var rejectReason))
            {
                Debug.LogWarning(rejectReason);
            }
        }

        /// <summary>True when a Window or Host search is pending or running.</summary>
        public static bool IsSearchBusy => SessionState.GetBool(BusyKey, false);

        /// <summary>
        /// Sole public entry for Window / menu tower search. Rejects when Busy or when the
        /// entry fails the ratified A1 validation (fail-closed — the window pre-validates,
        /// this is the pipeline's own gate).
        /// </summary>
        public static bool TryStartTowerSearch(
            GhostSearchEntry entry,
            string source,
            out string rejectReason)
        {
            if (IsSearchBusy)
            {
                rejectReason = "A ghost search is already pending or running (Window and Host share one pipeline).";
                return false;
            }

            var resolution = GhostSearchEntryResolver.Resolve(entry);
            if (!resolution.IsValid)
            {
                rejectReason = resolution.FirstReason;
                return false;
            }

            StartTowerSearch(entry, source ?? SourceMenu);
            rejectReason = null;
            return true;
        }

        private static void StartTowerSearch(GhostSearchEntry entry, string source)
        {
            SessionState.SetBool(BusyKey, true);
            SessionState.SetString(SourceKey, source ?? SourceMenu);

            if (!EditorApplication.isPlaying)
            {
                SessionState.SetBool(PendingKey, true);
                PersistEntry(entry);
                if (AllowPlayModeEntry)
                {
                    // Host-initiated Play Mode entry: remember it so completion can exit
                    // the Play Mode the host itself started — and only that one. A search
                    // launched inside a user-started Play Mode session never sets this.
                    SessionState.SetBool(EnteredPlayModeKey, true);
                    EditorApplication.isPlaying = true;
                }

                return;
            }

            EnsureRunner(entry);
        }

        /// <summary>
        /// A1 entry persistence across the Play Mode domain reload: every parameter the
        /// runner needs, the settings asset as a GUID, epsilon overrides as canonical
        /// full-precision metres.
        /// </summary>
        private static void PersistEntry(in GhostSearchEntry entry)
        {
            SessionState.SetInt("BugCam.GhostHost.StepCount", entry.StepCount);
            SessionState.SetInt("BugCam.GhostHost.Strategy", (int)entry.Strategy);
            SessionState.SetFloat("BugCam.GhostHost.AxisX", entry.SearchAxis.x);
            SessionState.SetFloat("BugCam.GhostHost.AxisY", entry.SearchAxis.y);
            SessionState.SetFloat("BugCam.GhostHost.AxisZ", entry.SearchAxis.z);
            SessionState.SetInt("BugCam.GhostHost.TargetBodyId", entry.TargetBodyId);
            SessionState.SetString("BugCam.GhostHost.SettingsAssetGuid", entry.SettingsAssetGuid);
            SessionState.SetBool("BugCam.GhostHost.HasFloorOverride", entry.HasFloorOverride);
            SessionState.SetFloat("BugCam.GhostHost.FloorOverrideMetres", entry.FloorOverrideMetres);
            SessionState.SetBool("BugCam.GhostHost.HasCeilingOverride", entry.HasCeilingOverride);
            SessionState.SetFloat("BugCam.GhostHost.CeilingOverrideMetres", entry.CeilingOverrideMetres);
        }

        private static GhostSearchEntry ReadPersistedEntry()
        {
            return new GhostSearchEntry(
                SessionState.GetInt("BugCam.GhostHost.StepCount", 32),
                (EpsilonSearchStrategy)SessionState.GetInt(
                    "BugCam.GhostHost.Strategy",
                    (int)EpsilonSearchStrategy.AscendFromStart),
                new Vector3(
                    SessionState.GetFloat("BugCam.GhostHost.AxisX", 1f),
                    SessionState.GetFloat("BugCam.GhostHost.AxisY", 0f),
                    SessionState.GetFloat("BugCam.GhostHost.AxisZ", 0f)),
                SessionState.GetInt(
                    "BugCam.GhostHost.TargetBodyId",
                    GhostSearchTargetCatalog.TowerDefaultTargetBodyId),
                SessionState.GetString("BugCam.GhostHost.SettingsAssetGuid", string.Empty),
                SessionState.GetBool("BugCam.GhostHost.HasFloorOverride", false),
                SessionState.GetFloat("BugCam.GhostHost.FloorOverrideMetres", 0f),
                SessionState.GetBool("BugCam.GhostHost.HasCeilingOverride", false),
                SessionState.GetFloat("BugCam.GhostHost.CeilingOverrideMetres", 0f));
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                TryResumePending();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                // Play Mode is ending regardless of who requested it — drop the marker so a
                // stale flag can never auto-exit a future user-started play session.
                SessionState.SetBool(EnteredPlayModeKey, false);
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
            EnsureRunner(ReadPersistedEntry());
        }

        private static void EnsureRunner(GhostSearchEntry entry)
        {
            DestroyTempRunnerIfPresent(preferDeferredDestroy: false);

            var go = new GameObject(GoName);
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.DontSave;
            var runner = go.AddComponent<GhostEvidenceRunnerBehaviour>();
            runner.Begin(entry, SessionState.GetString(SourceKey, SourceMenu));
        }

        private static void FinishBusy()
        {
            SessionState.SetBool(BusyKey, false);
            SessionState.SetBool(PendingKey, false);
        }

        /// <summary>
        /// Exit the Play Mode session the host itself started, once the completion has been
        /// delivered. Must run AFTER Busy is cleared (Cleanup): the ExitingPlayMode handler
        /// then sees Busy=false and cannot emit a false "Interrupted" completion. A search
        /// that ran inside a user-started Play Mode session never set the marker, so the
        /// user's session is left untouched.
        /// </summary>
        private static void ExitHostEnteredPlayMode()
        {
            if (!SessionState.GetBool(EnteredPlayModeKey, false))
            {
                return;
            }

            SessionState.SetBool(EnteredPlayModeKey, false);
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }
        }

        /// <summary>
        /// Live physics snapshot for the evidence capsule. Runtime values always
        /// capture; editor-serialized values (threading mode, enhanced determinism)
        /// come from <see cref="PhysicsSettingsProbe"/> and degrade to honest
        /// unavailability (nulls in manifest.json) if the probe throws.
        /// </summary>
        private static PhysicsRuntimeSnapshot CapturePhysicsSnapshot()
        {
            try
            {
                var threadingSerialized = PhysicsSettingsProbe.ReadThreadingModeSerialized();
                var threadingName = PhysicsSettingsProbe.ReadThreadingMode().ToString();
                var enhanced = PhysicsSettingsProbe.ReadEnhancedDeterminism();
                return PhysicsRuntimeSnapshot.CaptureLive(
                    true,
                    enhanced,
                    true,
                    threadingSerialized,
                    threadingName);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "BugCam: editor-serialized physics values unavailable for evidence " +
                    "snapshot (" + ex.Message + "); manifest will carry nulls for them.");
                return PhysicsRuntimeSnapshot.CaptureLive();
            }
        }

        private sealed class GhostEvidenceRunnerBehaviour : MonoBehaviour
        {
            private EpsilonSearch _liveSearch;
            private EpsilonSearchPhase _lastPhase = EpsilonSearchPhase.NotStarted;
            private int _lastStep = -1;
            private int _lastStepTotal = -1;
            private float _lastEpsilonMetres = -1f;

            public void Begin(GhostSearchEntry entry, string source)
            {
                StartCoroutine(Run(entry, source));
            }

            private void Update()
            {
                // Poll the Core read-only accessors; raise SearchProgress only on change.
                // Field compares only — zero allocations on quiet frames.
                var search = _liveSearch;
                if (search == null)
                {
                    return;
                }

                var phase = search.Phase;
                var step = search.CurrentPhaseStep;
                var stepTotal = search.PhaseStepTotal;
                var epsilon = search.CurrentEpsilonMetres;
                if (phase == _lastPhase &&
                    step == _lastStep &&
                    stepTotal == _lastStepTotal &&
                    epsilon == _lastEpsilonMetres)
                {
                    return;
                }

                _lastPhase = phase;
                _lastStep = step;
                _lastStepTotal = stepTotal;
                _lastEpsilonMetres = epsilon;
                NotifyProgress(new GhostSearchProgress(
                    phase,
                    step,
                    stepTotal,
                    epsilon,
                    hasEpsilon: search.HasOutstandingProbe && phase != EpsilonSearchPhase.Baseline));
            }

            private void OnDestroy()
            {
                _liveSearch = null;
                // Torn down by Host cleanup or domain reload — ensure Busy/Pending clear
                // without emitting a second SearchCompleted (interrupt notify owns that).
                if (IsSearchBusy)
                {
                    FinishBusy();
                }
            }

            private IEnumerator Run(GhostSearchEntry entry, string source)
            {
                var identity = new GhostSearchIdentity(
                    entry.TargetBodyId,
                    entry.SearchAxis.normalized,
                    entry.Strategy);
                var environment = GhostRunEnvironment.Capture(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
                    CapturePhysicsSnapshot());

                // The single settings path (docs/CONTRACT-2.2.1.md A1): re-resolve the
                // persisted entry; a vanished asset fails closed — never a silent
                // fall-back to defaults.
                var resolution = GhostSearchEntryResolver.Resolve(entry);
                var provenance = new GhostSettingsProvenance(
                    resolution.SourceKind,
                    resolution.SourceDescription,
                    resolution.AssetName,
                    entry.SettingsAssetGuid,
                    entry.HasFloorOverride,
                    entry.HasCeilingOverride,
                    resolution.EffectiveFloorMetres,
                    resolution.EffectiveCeilingMetres);

                GhostEvidenceDocument document;
                var searchResult = default(EpsilonSearchResult);
                if (!GhostSearchEntryResolver.TryCreateRuntimeSettings(
                        resolution,
                        out var settings,
                        out var effectiveSearchSettings,
                        out var resolveFailReason))
                {
                    Debug.LogError("BugCam: search entry settings resolve failed: " + resolveFailReason);
                    document = GhostEvidenceBuilder.CreateFailureDocument(
                        searchResult,
                        identity,
                        GhostEvidenceErrorCodes.SettingsResolveFailed,
                        resolveFailReason,
                        DivergenceSettings.DefaultGhostBodyLimit,
                        null,
                        environment,
                        provenance);
                }
                else
                {
                    var search = new EpsilonSearch(
                        effectiveSearchSettings,
                        identity.TargetBodyId,
                        identity.SearchAxis,
                        identity.Strategy);
                    _liveSearch = search;
                    var bodies = TowerProbeRequestFactory.CreateBaseline(entry.StepCount).Bodies;
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
                        entry.StepCount,
                        settings.ToThresholds(),
                        scales);

                    _liveSearch = null;
                    searchResult = runner.LastResult;
                    Debug.Log(GhostEvidenceWriter.FormatHonestSearchReport(
                        searchResult,
                        searchResult.Succeeded));

                    var build = GhostEvidenceBuilder.Build(
                        searchResult,
                        identity,
                        settings,
                        scales,
                        null,
                        environment,
                        provenance);
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
                            environment,
                            provenance);
                    }
                    else
                    {
                        document = build.Document;
                    }
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
                    ExitHostEnteredPlayMode();
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
                ExitHostEnteredPlayMode();
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

        private static void NotifyProgress(GhostSearchProgress progress)
        {
            try
            {
                SearchProgress?.Invoke(progress);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }

    /// <summary>
    /// Live progress snapshot of the hosted search. Real probe state only —
    /// phase and step come verbatim from the Core accessors, never synthesized.
    /// </summary>
    public readonly struct GhostSearchProgress
    {
        public GhostSearchProgress(
            EpsilonSearchPhase phase,
            int currentStep,
            int stepTotal,
            float epsilonMetres,
            bool hasEpsilon)
        {
            Phase = phase;
            CurrentStep = currentStep;
            StepTotal = stepTotal;
            EpsilonMetres = epsilonMetres;
            HasEpsilon = hasEpsilon;
        }

        public EpsilonSearchPhase Phase { get; }

        /// <summary>1-based probe number within the phase.</summary>
        public int CurrentStep { get; }

        /// <summary>Total probes in the phase, or -1 when honestly unknown (Exponential).</summary>
        public int StepTotal { get; }

        public float EpsilonMetres { get; }

        /// <summary>False for baseline / between probes — epsilon row keeps its last value.</summary>
        public bool HasEpsilon { get; }
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
