using System;
using BugCam.Core;
using UnityEngine;

namespace BugCam.Evidence
{
    /// <summary>
    /// Search identity captured at evidence build time — not reconstructed from Core results
    /// (EpsilonSearchResult does not retain these fields).
    /// </summary>
    public readonly struct GhostSearchIdentity
    {
        public GhostSearchIdentity(
            int targetBodyId,
            Vector3 searchAxis,
            EpsilonSearchStrategy strategy)
        {
            TargetBodyId = targetBodyId;
            SearchAxis = searchAxis;
            Strategy = strategy;
        }

        public int TargetBodyId { get; }

        public Vector3 SearchAxis { get; }

        public EpsilonSearchStrategy Strategy { get; }
    }

    /// <summary>
    /// Block 2.2.1 A1 settings provenance: where the effective search settings came from
    /// (window override &gt; asset &gt; defaults) and the effective epsilon bounds, recorded
    /// in the manifest and the window result. <c>default</c> (Captured=false) means the
    /// caller predates parameterization — the writer emits an honest captured=false.
    /// </summary>
    public readonly struct GhostSettingsProvenance
    {
        public GhostSettingsProvenance(
            string sourceKind,
            string description,
            string assetName,
            string assetGuid,
            bool floorOverridden,
            bool ceilingOverridden,
            float effectiveFloorMetres,
            float effectiveCeilingMetres)
        {
            Captured = true;
            SourceKind = sourceKind ?? string.Empty;
            Description = description ?? string.Empty;
            AssetName = assetName ?? string.Empty;
            AssetGuid = assetGuid ?? string.Empty;
            FloorOverridden = floorOverridden;
            CeilingOverridden = ceilingOverridden;
            EffectiveFloorMetres = effectiveFloorMetres;
            EffectiveCeilingMetres = effectiveCeilingMetres;
        }

        public bool Captured { get; }

        /// <summary>"defaults" | "asset" | "defaults+window" | "asset+window".</summary>
        public string SourceKind { get; }

        /// <summary>Human-readable source line, verbatim what the window shows.</summary>
        public string Description { get; }

        public string AssetName { get; }

        public string AssetGuid { get; }

        public bool FloorOverridden { get; }

        public bool CeilingOverridden { get; }

        /// <summary>Metres, full precision.</summary>
        public float EffectiveFloorMetres { get; }

        /// <summary>Metres, full precision.</summary>
        public float EffectiveCeilingMetres { get; }
    }

    /// <summary>
    /// Live physics-settings snapshot taken at evidence-build time from the running
    /// Unity instance — never sourced from BugCam constants. Runtime-readable values
    /// come from <c>Physics.*</c> / <c>Time.fixedDeltaTime</c>; editor-serialized values
    /// (enhanced determinism, threading mode) must be supplied by an Editor-side caller.
    /// False has-flags are honest unavailability — never fabricate.
    /// </summary>
    public readonly struct PhysicsRuntimeSnapshot
    {
        public PhysicsRuntimeSnapshot(
            bool captured,
            float fixedDeltaTime,
            string simulationMode,
            int solverIterations,
            int solverVelocityIterations,
            float defaultContactOffset,
            float defaultMaxDepenetrationVelocity,
            float sleepThreshold,
            float bounceThreshold,
            Vector3 gravity,
            bool hasEnhancedDeterminism,
            bool enhancedDeterminism,
            bool hasThreadingMode,
            int threadingModeSerialized,
            string threadingModeName)
        {
            Captured = captured;
            FixedDeltaTime = fixedDeltaTime;
            SimulationMode = simulationMode ?? string.Empty;
            SolverIterations = solverIterations;
            SolverVelocityIterations = solverVelocityIterations;
            DefaultContactOffset = defaultContactOffset;
            DefaultMaxDepenetrationVelocity = defaultMaxDepenetrationVelocity;
            SleepThreshold = sleepThreshold;
            BounceThreshold = bounceThreshold;
            Gravity = gravity;
            HasEnhancedDeterminism = hasEnhancedDeterminism;
            EnhancedDeterminism = enhancedDeterminism;
            HasThreadingMode = hasThreadingMode;
            ThreadingModeSerialized = threadingModeSerialized;
            ThreadingModeName = threadingModeName ?? string.Empty;
        }

        public bool Captured { get; }

        public float FixedDeltaTime { get; }

        public string SimulationMode { get; }

        public int SolverIterations { get; }

        public int SolverVelocityIterations { get; }

        public float DefaultContactOffset { get; }

        public float DefaultMaxDepenetrationVelocity { get; }

        public float SleepThreshold { get; }

        public float BounceThreshold { get; }

        public Vector3 Gravity { get; }

        public bool HasEnhancedDeterminism { get; }

        public bool EnhancedDeterminism { get; }

        public bool HasThreadingMode { get; }

        public int ThreadingModeSerialized { get; }

        public string ThreadingModeName { get; }

        public static PhysicsRuntimeSnapshot Empty =>
            new PhysicsRuntimeSnapshot(
                false, 0f, string.Empty, 0, 0, 0f, 0f, 0f, 0f, Vector3.zero,
                false, false, false, 0, string.Empty);

        /// <summary>Capture the runtime-readable values only; editor-serialized values unavailable.</summary>
        public static PhysicsRuntimeSnapshot CaptureLive()
        {
            return CaptureLive(false, false, false, 0, string.Empty);
        }

        /// <summary>
        /// Capture the runtime-readable values live and merge editor-serialized values
        /// read by an Editor-side caller (e.g. PhysicsSettingsProbe).
        /// </summary>
        public static PhysicsRuntimeSnapshot CaptureLive(
            bool hasEnhancedDeterminism,
            bool enhancedDeterminism,
            bool hasThreadingMode,
            int threadingModeSerialized,
            string threadingModeName)
        {
            return new PhysicsRuntimeSnapshot(
                true,
                Time.fixedDeltaTime,
                Physics.simulationMode.ToString(),
                Physics.defaultSolverIterations,
                Physics.defaultSolverVelocityIterations,
                Physics.defaultContactOffset,
                Physics.defaultMaxDepenetrationVelocity,
                Physics.sleepThreshold,
                Physics.bounceThreshold,
                Physics.gravity,
                hasEnhancedDeterminism,
                enhancedDeterminism,
                hasThreadingMode,
                threadingModeSerialized,
                threadingModeName);
        }
    }

    /// <summary>
    /// §14 identity fields. Empty strings are honest when a value is unavailable.
    /// </summary>
    public readonly struct GhostRunEnvironment
    {
        // Keep the 4-parameter signature intact: EditMode tests construct this struct
        // through reflection with exactly these four argument types.
        public GhostRunEnvironment(
            string unityVersion,
            string gitCommitSha,
            string gitBranch,
            string scenePath)
            : this(unityVersion, gitCommitSha, gitBranch, scenePath, default)
        {
        }

        public GhostRunEnvironment(
            string unityVersion,
            string gitCommitSha,
            string gitBranch,
            string scenePath,
            PhysicsRuntimeSnapshot physics)
        {
            UnityVersion = unityVersion ?? string.Empty;
            GitCommitSha = gitCommitSha ?? string.Empty;
            GitBranch = gitBranch ?? string.Empty;
            ScenePath = scenePath ?? string.Empty;
            Physics = physics;
        }

        public string UnityVersion { get; }

        public string GitCommitSha { get; }

        public string GitBranch { get; }

        public string ScenePath { get; }

        /// <summary>
        /// Live physics snapshot; <c>default</c> (Captured=false) when the caller
        /// did not capture one — the writer emits honest nulls then.
        /// </summary>
        public PhysicsRuntimeSnapshot Physics { get; }

        public static GhostRunEnvironment Empty =>
            new GhostRunEnvironment(string.Empty, string.Empty, string.Empty, string.Empty);

        /// <summary>
        /// Capture Unity version, live physics values, plus optional git env vars /
        /// scene path. Empty string when unavailable — never fabricate.
        /// </summary>
        public static GhostRunEnvironment Capture(string scenePath = null)
        {
            return Capture(scenePath, PhysicsRuntimeSnapshot.CaptureLive());
        }

        /// <summary>
        /// Capture with a caller-supplied physics snapshot (Editor callers merge
        /// editor-serialized values the runtime API cannot read).
        /// </summary>
        public static GhostRunEnvironment Capture(
            string scenePath,
            PhysicsRuntimeSnapshot physics)
        {
            var commit = Environment.GetEnvironmentVariable("BUGCAM_GIT_COMMIT") ?? string.Empty;
            if (string.IsNullOrEmpty(commit))
            {
                commit = Environment.GetEnvironmentVariable("GIT_COMMIT") ?? string.Empty;
            }

            var branch = Environment.GetEnvironmentVariable("BUGCAM_GIT_BRANCH") ?? string.Empty;
            if (string.IsNullOrEmpty(branch))
            {
                branch = Environment.GetEnvironmentVariable("GIT_BRANCH") ?? string.Empty;
            }

            return new GhostRunEnvironment(
                Application.unityVersion ?? string.Empty,
                commit,
                branch,
                scenePath ?? string.Empty,
                physics);
        }
    }

    /// <summary>One retained fan run plus its re-analyzed divergence vs baseline.</summary>
    public sealed class GhostFanEvidence
    {
        public GhostFanEvidence(
            int fanIndex,
            float multiplier,
            Vector3 axis,
            float epsilonMetres,
            bool outsideSearchRange,
            RunResult run,
            DivergenceResult divergence)
        {
            FanIndex = fanIndex;
            Multiplier = multiplier;
            Axis = axis;
            EpsilonMetres = epsilonMetres;
            OutsideSearchRange = outsideSearchRange;
            Run = run;
            Divergence = divergence;
        }

        public int FanIndex { get; }

        public float Multiplier { get; }

        public Vector3 Axis { get; }

        public float EpsilonMetres { get; }

        public bool OutsideSearchRange { get; }

        public RunResult Run { get; }

        public DivergenceResult Divergence { get; }
    }

    /// <summary>Top-N ranked ghost body for visualization and metrics.</summary>
    public readonly struct GhostRankedBody
    {
        public GhostRankedBody(int bodyId, float maxPositionErrorMetres, int rank)
        {
            BodyId = bodyId;
            MaxPositionErrorMetres = maxPositionErrorMetres;
            Rank = rank;
        }

        public int BodyId { get; }

        public float MaxPositionErrorMetres { get; }

        public int Rank { get; }
    }

    /// <summary>
    /// Block 1.5 evidence document — single source of truth after
    /// <see cref="GhostEvidenceBuilder.Build"/>.
    /// </summary>
    public sealed class GhostEvidenceDocument
    {
        private static readonly GhostFanEvidence[] EmptyFans = Array.Empty<GhostFanEvidence>();
        private static readonly GhostRankedBody[] EmptyBodies = Array.Empty<GhostRankedBody>();

        public GhostEvidenceDocument(
            string runId,
            EpsilonSearchResult searchResult,
            GhostSearchIdentity searchIdentity,
            int ghostBodyLimit,
            int primaryFanIndex,
            bool hasPrimaryFan,
            GhostFanEvidence[] fans,
            GhostRankedBody[] rankedBodies,
            DivergenceResult primaryDivergence,
            GhostDrawSet drawSet,
            bool success = true,
            string errorCode = null,
            string errorReason = null,
            GhostRunEnvironment environment = default,
            GhostSettingsProvenance settingsSource = default)
        {
            SchemaVersion = GhostEvidenceSchema.SchemaVersion;
            Kind = GhostEvidenceSchema.Kind;
            RunId = runId ?? string.Empty;
            SearchResult = searchResult;
            SearchIdentity = searchIdentity;
            GhostBodyLimit = ghostBodyLimit;
            PrimaryFanIndex = primaryFanIndex;
            HasPrimaryFan = hasPrimaryFan;
            Fans = fans ?? EmptyFans;
            RankedBodies = rankedBodies ?? EmptyBodies;
            PrimaryDivergence = primaryDivergence;
            DrawSet = drawSet ?? GhostDrawSet.Empty;
            BuiltUtc = DateTime.UtcNow;
            Success = success;
            ErrorCode = errorCode ?? GhostEvidenceErrorCodes.None;
            ErrorReason = errorReason ?? string.Empty;
            Environment = environment;
            SettingsSource = settingsSource;
        }

        public int SchemaVersion { get; }

        public string Kind { get; }

        public string RunId { get; }

        public DateTime BuiltUtc { get; }

        /// <summary>False for failed/cleanup-timeout/build-failure bundles (§15).</summary>
        public bool Success { get; }

        public string ErrorCode { get; }

        public string ErrorReason { get; }

        public GhostRunEnvironment Environment { get; }

        /// <summary>A1 settings provenance; Captured=false for pre-A1 callers.</summary>
        public GhostSettingsProvenance SettingsSource { get; }

        public EpsilonSearchResult SearchResult { get; }

        public GhostSearchIdentity SearchIdentity { get; }

        public int GhostBodyLimit { get; }

        /// <summary>Index into <see cref="Fans"/>, or -1 when no primary.</summary>
        public int PrimaryFanIndex { get; }

        public bool HasPrimaryFan { get; }

        public GhostFanEvidence[] Fans { get; }

        public GhostRankedBody[] RankedBodies { get; }

        public DivergenceResult PrimaryDivergence { get; }

        public GhostDrawSet DrawSet { get; }
    }

    /// <summary>Outcome of <see cref="GhostEvidenceBuilder.Build"/>.</summary>
    public readonly struct GhostEvidenceBuildResult
    {
        private GhostEvidenceBuildResult(bool succeeded, string errorReason, GhostEvidenceDocument document)
        {
            Succeeded = succeeded;
            ErrorReason = errorReason ?? string.Empty;
            Document = document;
        }

        /// <summary>
        /// True when a writer-ready document was produced (including success=false failure bundles).
        /// </summary>
        public bool Succeeded { get; }

        public string ErrorReason { get; }

        public GhostEvidenceDocument Document { get; }

        public static GhostEvidenceBuildResult Success(GhostEvidenceDocument document)
        {
            return new GhostEvidenceBuildResult(true, string.Empty, document);
        }

        public static GhostEvidenceBuildResult Failure(string errorReason)
        {
            return new GhostEvidenceBuildResult(false, errorReason, null);
        }
    }

    /// <summary>Outcome of <see cref="GhostEvidenceWriter.Write"/>.</summary>
    public readonly struct GhostEvidenceWriteResult
    {
        private GhostEvidenceWriteResult(
            bool succeeded,
            string errorReason,
            string runDirectory,
            string metricsPath,
            string summaryPath,
            string consoleReportPath)
        {
            Succeeded = succeeded;
            ErrorReason = errorReason ?? string.Empty;
            RunDirectory = runDirectory ?? string.Empty;
            MetricsPath = metricsPath ?? string.Empty;
            SummaryPath = summaryPath ?? string.Empty;
            ConsoleReportPath = consoleReportPath ?? string.Empty;
        }

        public bool Succeeded { get; }

        public string ErrorReason { get; }

        public string RunDirectory { get; }

        public string MetricsPath { get; }

        public string SummaryPath { get; }

        public string ConsoleReportPath { get; }

        public static GhostEvidenceWriteResult Success(
            string runDirectory,
            string metricsPath,
            string summaryPath,
            string consoleReportPath)
        {
            return new GhostEvidenceWriteResult(
                true,
                string.Empty,
                runDirectory,
                metricsPath,
                summaryPath,
                consoleReportPath);
        }

        public static GhostEvidenceWriteResult Failure(string errorReason)
        {
            return new GhostEvidenceWriteResult(
                false,
                errorReason,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);
        }
    }
}
