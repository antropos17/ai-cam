using System;
using System.Globalization;
using System.IO;
using System.Text;
using BugCam.Core;
using UnityEngine;

namespace BugCam.Evidence
{
    /// <summary>
    /// Atomic evidence-bundle writer. Invariant culture. No NaN/Inf in JSON —
    /// unavailable values use null / boolean flags. Never fabricate zeros (§15).
    ///
    /// Canonical layout under Library/BugCamEvidence/Runs/&lt;run-id&gt;/:
    ///   manifest.json, metrics.json, summary.md,
    ///   report/console-report.txt,
    ///   runs/baseline.json (+ fan-00…fan-14 when retained),
    ///   visuals/ (PNG filenames from schema; written by capture after success).
    /// Console report stays at report/console-report.txt (not report.txt).
    /// </summary>
    public static class GhostEvidenceWriter
    {
        public static GhostEvidenceWriteResult Write(
            GhostEvidenceDocument document,
            string projectRoot)
        {
            if (document == null)
            {
                return GhostEvidenceWriteResult.Failure("Document is required.");
            }

            if (string.IsNullOrEmpty(projectRoot))
            {
                return GhostEvidenceWriteResult.Failure("Project root is required.");
            }

            var runDir = Path.Combine(
                projectRoot,
                GhostEvidenceSchema.RunsRelativeRoot.Replace('/', Path.DirectorySeparatorChar),
                document.RunId);
            var checkpointDir = Path.Combine(
                projectRoot,
                GhostEvidenceSchema.CheckpointRelativeRoot.Replace('/', Path.DirectorySeparatorChar));

            try
            {
                Directory.CreateDirectory(runDir);
                Directory.CreateDirectory(Path.Combine(runDir, GhostEvidenceSchema.ReportDirectoryName));
                Directory.CreateDirectory(Path.Combine(runDir, GhostEvidenceSchema.VisualsDirectoryName));
                Directory.CreateDirectory(Path.Combine(runDir, GhostEvidenceSchema.RunsDirectoryName));
                Directory.CreateDirectory(checkpointDir);

                var metricsPath = Path.Combine(runDir, GhostEvidenceSchema.MetricsFileName);
                var manifestPath = Path.Combine(runDir, GhostEvidenceSchema.ManifestFileName);
                var summaryPath = Path.Combine(runDir, GhostEvidenceSchema.SummaryFileName);
                var consolePath = Path.Combine(
                    runDir,
                    GhostEvidenceSchema.ReportDirectoryName,
                    GhostEvidenceSchema.ConsoleReportFileName);

                WriteRetainedRuns(document, runDir);

                AtomicWrite(metricsPath, BuildMetricsJson(document));
                AtomicWrite(manifestPath, BuildManifestJson(document, runDir));
                AtomicWrite(summaryPath, BuildSummaryMarkdown(document));
                AtomicWrite(
                    consolePath,
                    FormatHonestConsoleReport(document));

                // Checkpoint pointer: last-run path for Block 1.5 gate evidence.
                var checkpointPointer = Path.Combine(checkpointDir, "last-run.txt");
                AtomicWrite(checkpointPointer, runDir + Environment.NewLine);

                var checkpointSummary = Path.Combine(checkpointDir, GhostEvidenceSchema.SummaryFileName);
                AtomicWrite(checkpointSummary, BuildSummaryMarkdown(document));

                return GhostEvidenceWriteResult.Success(
                    runDir,
                    metricsPath,
                    summaryPath,
                    consolePath);
            }
            catch (Exception ex)
            {
                return GhostEvidenceWriteResult.Failure(ex.Message);
            }
        }

        /// <summary>
        /// Serialize retained runs from the canonical model (BaselineRun + Fans[i].Run).
        /// STABLE → baseline only. Failure → no fake retained runs. No independent metric recompute.
        /// </summary>
        private static void WriteRetainedRuns(GhostEvidenceDocument document, string runDir)
        {
            var runsDir = Path.Combine(runDir, GhostEvidenceSchema.RunsDirectoryName);
            if (!document.Success)
            {
                // Failure: do not fabricate retained-run JSON.
                return;
            }

            var search = document.SearchResult;
            if (search.Succeeded && search.BaselineRun.Succeeded)
            {
                AtomicWrite(
                    Path.Combine(runsDir, GhostEvidenceSchema.BaselineRunFileName),
                    BuildRunJson(
                        "baseline",
                        -1,
                        0f,
                        Vector3.zero,
                        search.BaselineRun.EpsilonMetres,
                        false,
                        search.BaselineRun));
            }

            // STABLE fabricates no fans — Fans.Length is already 0.
            for (var i = 0; i < document.Fans.Length; i++)
            {
                var fan = document.Fans[i];
                AtomicWrite(
                    Path.Combine(runsDir, GhostEvidenceSchema.FanRunFileName(fan.FanIndex)),
                    BuildRunJson(
                        "fan",
                        fan.FanIndex,
                        fan.Multiplier,
                        fan.Axis,
                        fan.EpsilonMetres,
                        fan.OutsideSearchRange,
                        fan.Run));
            }
        }

        public static string BuildRunJson(
            string kind,
            int fanIndex,
            float multiplier,
            Vector3 axis,
            float epsilonMetres,
            bool outsideSearchRange,
            RunResult run)
        {
            var sb = new StringBuilder(Math.Max(1024, (run.StateFrames?.Length ?? 0) * 12));
            sb.Append('{');
            WriteString(sb, "kind", kind, true);
            WriteInt(sb, "fanIndex", fanIndex);
            WriteFloat(sb, "multiplier", multiplier);
            WriteVec3(sb, "axis", axis);
            WriteFloat(sb, "epsilonMetres", epsilonMetres);
            WriteBool(sb, "outsideSearchRange", outsideSearchRange);
            WriteBool(sb, "succeeded", run.Succeeded);
            WriteString(sb, "errorReason", run.ErrorReason ?? string.Empty);
            WriteInt(sb, "stepCount", run.StepCount);
            WriteInt(sb, "bodyCount", run.BodyCount);
            WriteInt(sb, "stateStride", BugCamConstants.StateStride);
            WriteFloat(sb, "simulatedTime", run.SimulatedTime);
            WriteInt(sb, "seed", run.Seed);

            sb.Append(",\"perturbation\":{");
            WriteInt(sb, "targetBodyId", run.Perturbation.TargetBodyId, true);
            WriteVec3(sb, "axis", run.Perturbation.Axis);
            WriteFloat(sb, "magnitudeMetres", run.Perturbation.MagnitudeMetres);
            sb.Append('}');

            sb.Append(",\"stableBodyIds\":[");
            var ids = run.StableBodyIds ?? Array.Empty<int>();
            for (var i = 0; i < ids.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                sb.Append(ids[i].ToString(CultureInfo.InvariantCulture));
            }

            sb.Append(']');

            // stateFrames: full [steps × bodies × 14] for viz reproduce. Invariant culture.
            sb.Append(",\"stateFrames\":[");
            var frames = run.StateFrames ?? Array.Empty<float>();
            for (var i = 0; i < frames.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                var v = frames[i];
                if (float.IsNaN(v) || float.IsInfinity(v))
                {
                    sb.Append("null");
                }
                else
                {
                    sb.Append(v.ToString("R", CultureInfo.InvariantCulture));
                }
            }

            sb.Append("]}");
            return sb.ToString();
        }

        public static string BuildMetricsJson(GhostEvidenceDocument document)
        {
            var sb = new StringBuilder(4096);
            var search = document.SearchResult;
            var identity = document.SearchIdentity;
            var primary = document.PrimaryDivergence;
            var env = document.Environment;
            var hasReference = HasReferenceEpsilon(search);
            var hasBracketWidth = HasFinalBracketWidth(search);
            var primaryAvailable = HasPrimaryDivergenceMetrics(document);

            sb.Append('{');
            WriteInt(sb, "schemaVersion", document.SchemaVersion, true);
            WriteString(sb, "kind", document.Kind);
            WriteString(sb, "runId", document.RunId);
            WriteString(sb, "builtUtc", document.BuiltUtc.ToString("o", CultureInfo.InvariantCulture));
            WriteBool(sb, "success", document.Success);
            WriteString(sb, "errorCode", document.ErrorCode ?? string.Empty);
            WriteString(sb, "errorReason", document.ErrorReason ?? string.Empty);
            WriteString(sb, "verdict", search.Verdict);
            WriteString(sb, "verdictKind", search.VerdictKind.ToString());

            WriteString(sb, "unityVersion", env.UnityVersion ?? string.Empty);
            WriteString(sb, "gitCommitSha", env.GitCommitSha ?? string.Empty);
            WriteString(sb, "gitBranch", env.GitBranch ?? string.Empty);
            WriteString(sb, "scenePath", env.ScenePath ?? string.Empty);

            sb.Append(",\"searchIdentity\":{");
            WriteInt(sb, "targetBodyId", identity.TargetBodyId, true);
            WriteVec3(sb, "searchAxis", identity.SearchAxis);
            WriteString(sb, "strategy", identity.Strategy.ToString());
            sb.Append('}');

            sb.Append(",\"ranges\":{");
            if (document.Success && search.Succeeded)
            {
                WriteFloat(sb, "searchFloorMetres", search.SearchRangeStartMetres, true);
                WriteFloat(sb, "searchRangeStartMetres", search.SearchRangeStartMetres);
                WriteFloat(sb, "searchRangeCeilingMetres", search.SearchRangeCeilingMetres);
                WriteFloat(sb, "characterizationCeilingMetres", search.CharacterizationCeilingMetres);
            }
            else
            {
                WriteNull(sb, "searchFloorMetres", true);
                WriteNull(sb, "searchRangeStartMetres");
                WriteNull(sb, "searchRangeCeilingMetres");
                WriteNull(sb, "characterizationCeilingMetres");
            }

            sb.Append('}');

            WriteBool(sb, "hasThresholdEstimate", search.HasThresholdEstimate && document.Success);
            if (search.HasThresholdEstimate && document.Success)
            {
                WriteFloat(sb, "thresholdEstimateMetres", search.ThresholdEstimateMetres);
            }
            else
            {
                WriteNull(sb, "thresholdEstimateMetres");
            }

            WriteBool(sb, "hasReferenceEpsilon", hasReference && document.Success);
            if (hasReference && document.Success)
            {
                WriteFloat(sb, "referenceEpsilonMetres", search.ReferenceEpsilonMetres);
            }
            else
            {
                WriteNull(sb, "referenceEpsilonMetres");
            }

            WriteBool(sb, "referenceIsExactThreshold", false);

            WriteBool(sb, "hasLargestStableEpsilon", search.HasLargestStableEpsilon && document.Success);
            if (search.HasLargestStableEpsilon && document.Success)
            {
                WriteFloat(sb, "largestStableEpsilonMetres", search.LargestStableEpsilonMetres);
            }
            else
            {
                WriteNull(sb, "largestStableEpsilonMetres");
            }

            WriteBool(
                sb,
                "hasSmallestDivergentEpsilon",
                search.HasSmallestDivergentEpsilon && document.Success);
            if (search.HasSmallestDivergentEpsilon && document.Success)
            {
                WriteFloat(sb, "smallestDivergentEpsilonMetres", search.SmallestDivergentEpsilonMetres);
            }
            else
            {
                WriteNull(sb, "smallestDivergentEpsilonMetres");
            }

            WriteBool(sb, "hasFinalBracketWidth", hasBracketWidth && document.Success);
            if (hasBracketWidth && document.Success)
            {
                WriteFloat(sb, "finalBracketWidthMetres", search.FinalBracketWidthMetres);
            }
            else
            {
                WriteNull(sb, "finalBracketWidthMetres");
            }

            WriteInt(sb, "ghostBodyLimit", document.GhostBodyLimit);
            WriteBool(sb, "hasPrimaryFan", document.HasPrimaryFan);
            WriteInt(sb, "primaryFanIndex", document.PrimaryFanIndex);
            WriteInt(sb, "retainedFanCount", document.Fans.Length);
            if (document.Success && search.Succeeded)
            {
                WriteInt(sb, "physicalProbeCount", search.PhysicalProbeCount);
                WriteInt(sb, "cacheHitCount", search.CacheHitCount);
            }
            else
            {
                WriteNull(sb, "physicalProbeCount");
                WriteNull(sb, "cacheHitCount");
            }

            sb.Append(",\"primary\":{");
            WriteBool(sb, "analyzeSucceeded", primary.Succeeded && document.Success, true);
            WriteBool(sb, "hasSignificantDivergence", primaryAvailable && primary.HasSignificantDivergence);
            WriteBool(sb, "hasFirstDivergenceFrame", primaryAvailable && primary.FirstDivergenceFrame >= 0);
            WriteBool(sb, "hasFirstDivergenceBodyId", primaryAvailable && primary.FirstDivergenceBodyId >= 0);
            WriteBool(sb, "hasMaxSpread", primaryAvailable && primary.MaxSpreadMetres > 0f);
            WriteBool(sb, "hasMaxSpreadStep", primaryAvailable && primary.MaxSpreadStep >= 0);
            WriteBool(sb, "hasMaxSpreadBodyId", primaryAvailable && primary.MaxSpreadBodyId >= 0);

            if (primaryAvailable && primary.FirstDivergenceFrame >= 0)
            {
                WriteInt(sb, "firstDivergenceFrame", primary.FirstDivergenceFrame);
            }
            else
            {
                WriteNull(sb, "firstDivergenceFrame");
            }

            if (primaryAvailable && primary.FirstDivergenceBodyId >= 0)
            {
                WriteInt(sb, "firstDivergenceBodyId", primary.FirstDivergenceBodyId);
            }
            else
            {
                WriteNull(sb, "firstDivergenceBodyId");
            }

            if (primaryAvailable && primary.MaxSpreadMetres > 0f)
            {
                WriteFloat(sb, "maxSpreadMetres", primary.MaxSpreadMetres);
            }
            else
            {
                WriteNull(sb, "maxSpreadMetres");
            }

            if (primaryAvailable && primary.MaxSpreadStep >= 0)
            {
                WriteInt(sb, "maxSpreadStep", primary.MaxSpreadStep);
            }
            else
            {
                WriteNull(sb, "maxSpreadStep");
            }

            if (primaryAvailable && primary.MaxSpreadBodyId >= 0)
            {
                WriteInt(sb, "maxSpreadBodyId", primary.MaxSpreadBodyId);
            }
            else
            {
                WriteNull(sb, "maxSpreadBodyId");
            }

            if (primaryAvailable)
            {
                WriteInt(sb, "affectedBodyCount", primary.AffectedBodyCount);
            }
            else
            {
                WriteNull(sb, "affectedBodyCount");
            }

            WriteBool(sb, "amplificationDefined", primaryAvailable && primary.AmplificationDefined);
            if (primaryAvailable && primary.AmplificationDefined)
            {
                WriteFloat(sb, "amplification", primary.Amplification);
            }
            else
            {
                WriteNull(sb, "amplification");
            }

            // Sentinel note: Core DivergenceResult still uses -1 for unavailable int frames/ids
            // in-memory; machine-facing JSON prefers null when has* is false.
            WriteString(
                sb,
                "unavailableSentinelNote",
                "JSON uses null when has*=false; Core in-memory ints may still be -1");

            sb.Append(",\"affectedBodyIds\":[");
            if (primaryAvailable)
            {
                var affected = primary.AffectedBodyIds ?? Array.Empty<int>();
                for (var i = 0; i < affected.Length; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(',');
                    }

                    sb.Append(affected[i].ToString(CultureInfo.InvariantCulture));
                }
            }

            sb.Append("]}");

            sb.Append(",\"rankedBodies\":[");
            for (var i = 0; i < document.RankedBodies.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                var body = document.RankedBodies[i];
                sb.Append('{');
                WriteInt(sb, "rank", body.Rank, true);
                WriteInt(sb, "bodyId", body.BodyId);
                WriteFloat(sb, "maxPositionErrorMetres", body.MaxPositionErrorMetres);
                sb.Append('}');
            }

            sb.Append(']');

            sb.Append(",\"fans\":[");
            for (var i = 0; i < document.Fans.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                var fan = document.Fans[i];
                var fanPrimary = fan.Divergence.Succeeded && fan.Divergence.HasSignificantDivergence;
                sb.Append('{');
                WriteInt(sb, "fanIndex", fan.FanIndex, true);
                WriteFloat(sb, "multiplier", fan.Multiplier);
                WriteVec3(sb, "axis", fan.Axis);
                WriteFloat(sb, "epsilonMetres", fan.EpsilonMetres);
                WriteBool(sb, "outsideSearchRange", fan.OutsideSearchRange);
                WriteBool(sb, "hasSignificantDivergence", fan.Divergence.HasSignificantDivergence);
                if (fanPrimary && fan.Divergence.FirstDivergenceFrame >= 0)
                {
                    WriteInt(sb, "firstDivergenceFrame", fan.Divergence.FirstDivergenceFrame);
                }
                else
                {
                    WriteNull(sb, "firstDivergenceFrame");
                }

                if (fanPrimary && fan.Divergence.FirstDivergenceBodyId >= 0)
                {
                    WriteInt(sb, "firstDivergenceBodyId", fan.Divergence.FirstDivergenceBodyId);
                }
                else
                {
                    WriteNull(sb, "firstDivergenceBodyId");
                }

                if (fanPrimary && fan.Divergence.MaxSpreadMetres > 0f)
                {
                    WriteFloat(sb, "maxSpreadMetres", fan.Divergence.MaxSpreadMetres);
                }
                else
                {
                    WriteNull(sb, "maxSpreadMetres");
                }

                WriteBool(sb, "amplificationDefined", fan.Divergence.AmplificationDefined);
                if (fan.Divergence.AmplificationDefined)
                {
                    WriteFloat(sb, "amplification", fan.Divergence.Amplification);
                }
                else
                {
                    WriteNull(sb, "amplification");
                }

                WriteString(sb, "runFile", GhostEvidenceSchema.FanRunRelativePath(fan.FanIndex));
                sb.Append('}');
            }

            sb.Append(']');

            sb.Append(",\"drawSet\":{");
            WriteInt(sb, "polylineCount", document.DrawSet.Polylines.Length, true);
            WriteBool(sb, "hasFirstDivergence", document.DrawSet.HasFirstDivergence);
            WriteBool(sb, "hasMaxSpread", document.DrawSet.HasMaxSpread);
            WriteBool(sb, "hasBounds", document.DrawSet.HasBounds);
            if (document.DrawSet.HasFirstDivergence && document.DrawSet.FirstDivergenceBodyId >= 0)
            {
                WriteInt(sb, "firstDivergenceBodyId", document.DrawSet.FirstDivergenceBodyId);
            }
            else
            {
                WriteNull(sb, "firstDivergenceBodyId");
            }

            if (document.DrawSet.HasMaxSpread && document.DrawSet.MaxSpreadBodyId >= 0)
            {
                WriteInt(sb, "maxSpreadBodyId", document.DrawSet.MaxSpreadBodyId);
            }
            else
            {
                WriteNull(sb, "maxSpreadBodyId");
            }

            if (document.DrawSet.HasFirstDivergence)
            {
                WriteVec3(sb, "firstDivergenceWorld", document.DrawSet.FirstDivergenceWorld);
            }

            if (document.DrawSet.HasMaxSpread)
            {
                WriteVec3(sb, "maxSpreadWorld", document.DrawSet.MaxSpreadWorld);
            }

            sb.Append('}');

            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>
        /// Primary divergence metrics are available only for a successful document whose
        /// primary analyze succeeded AND reported significant divergence. STABLE / failure
        /// emit null + has*=false — never fabricated zeros.
        /// </summary>
        public static bool HasPrimaryDivergenceMetrics(GhostEvidenceDocument document)
        {
            if (document == null || !document.Success)
            {
                return false;
            }

            var primary = document.PrimaryDivergence;
            return primary.Succeeded && primary.HasSignificantDivergence;
        }

        public static string BuildSummaryMarkdown(GhostEvidenceDocument document)
        {
            var search = document.SearchResult;
            var primary = document.PrimaryDivergence;
            var sb = new StringBuilder(2048);
            sb.AppendLine("# BugCam Ghost Evidence");
            sb.AppendLine();
            sb.AppendLine("- **Kind:** " + document.Kind);
            sb.AppendLine("- **Schema:** " + document.SchemaVersion);
            sb.AppendLine("- **Run id:** `" + document.RunId + "`");
            sb.AppendLine("- **Success:** " + document.Success);
            if (!document.Success)
            {
                sb.AppendLine("- **Error code:** `" + document.ErrorCode + "`");
                sb.AppendLine("- **Error reason:** " + document.ErrorReason);
            }

            sb.AppendLine("- **Verdict:** " + search.Verdict);
            if (document.SceneCapture.Performed)
            {
                // A2: the freeze caveat sits with the verdict — the evidence consumer
                // must see it without the window.
                for (var i = 0; i < document.SceneCapture.KinematicFreezeWarnings.Length; i++)
                {
                    sb.AppendLine(
                        "- **ВНИМАНИЕ:** " + document.SceneCapture.KinematicFreezeWarnings[i]);
                }

                sb.AppendLine(
                    "- **Scene capture:** " +
                    (document.SceneCapture.Succeeded ? "captured" : "fail-closed") +
                    ", " + document.SceneCapture.Bodies.Length + " bodies, hash `" +
                    document.SceneCapture.CaptureHash + "`");
            }

            sb.AppendLine(
                "- **Search identity:** body " + document.SearchIdentity.TargetBodyId +
                ", axis " + FormatAxisLabel(document.SearchIdentity.SearchAxis) +
                ", strategy " + document.SearchIdentity.Strategy);
            sb.AppendLine("- **Unity version:** " + (document.Environment.UnityVersion ?? string.Empty));
            sb.AppendLine("- **Git commit:** `" + (document.Environment.GitCommitSha ?? string.Empty) + "`");
            sb.AppendLine("- **Git branch:** `" + (document.Environment.GitBranch ?? string.Empty) + "`");
            sb.AppendLine("- **Scene:** `" + (document.Environment.ScenePath ?? string.Empty) + "`");

            if (document.Success && search.Succeeded)
            {
                sb.AppendLine(
                    "- **Search floor / range:** " +
                    MetresLabel(search.SearchRangeStartMetres) + " … " +
                    MetresLabel(search.SearchRangeCeilingMetres));
                sb.AppendLine(
                    "- **Characterization range ceiling:** " +
                    MetresLabel(search.CharacterizationCeilingMetres));
            }
            else
            {
                sb.AppendLine("- **Search floor / range:** unavailable");
                sb.AppendLine("- **Characterization range ceiling:** unavailable");
            }

            if (search.HasThresholdEstimate && document.Success)
            {
                sb.AppendLine(
                    "- **Threshold Estimate:** " +
                    MetresLabel(search.ThresholdEstimateMetres) +
                    " (estimate — not an exact mathematical threshold)");
            }
            else
            {
                sb.AppendLine("- **Threshold Estimate:** unavailable (`hasThresholdEstimate=false`)");
            }

            if (HasReferenceEpsilon(search) && document.Success)
            {
                sb.AppendLine(
                    "- **Reference Epsilon:** " +
                    MetresLabel(search.ReferenceEpsilonMetres) +
                    " (`referenceIsExactThreshold=false`)");
            }
            else
            {
                sb.AppendLine("- **Reference Epsilon:** unavailable");
            }

            sb.AppendLine(
                "- **Retained fans:** " + document.Fans.Length +
                " (STABLE fabricates none; failures emit empty fans)");
            sb.AppendLine("- **Ghost body limit:** " + document.GhostBodyLimit);
            sb.AppendLine("- **Ranked bodies drawn:** " + document.RankedBodies.Length);

            if (HasPrimaryDivergenceMetrics(document))
            {
                sb.AppendLine("- **First divergence frame:** " + primary.FirstDivergenceFrame);
                sb.AppendLine("- **First divergence body id:** " + primary.FirstDivergenceBodyId);
                sb.AppendLine("- **Max spread:** " + MetresLabel(primary.MaxSpreadMetres));
                sb.AppendLine("- **Max spread body id:** " + primary.MaxSpreadBodyId);
                if (primary.AmplificationDefined)
                {
                    sb.AppendLine("- **Amplification:** " + Invariant(primary.Amplification));
                }
                else
                {
                    sb.AppendLine("- **Amplification:** unavailable");
                }
            }
            else
            {
                sb.AppendLine("- **First divergence / spread:** unavailable for this verdict");
            }

            sb.AppendLine();
            sb.AppendLine("## Labels");
            sb.AppendLine();
            sb.AppendLine("| Label | Meaning |");
            sb.AppendLine("|---|---|");
            sb.AppendLine("| Threshold Estimate | Smallest tested divergent epsilon in a monotonic bracket |");
            sb.AppendLine("| Reference Epsilon | Fan center; not claimed as an exact threshold |");
            sb.AppendLine("| Search Floor | Lower bound of the tested search range |");
            sb.AppendLine("| Search Range | `EpsilonStart`…`EpsilonCeiling` |");
            sb.AppendLine("| Characterization Range | Fan may reach `1.2 × EpsilonCeiling`; samples above ceiling set `OutsideSearchRange` |");
            sb.AppendLine();
            sb.AppendLine("Machine-readable metrics: `" + GhostEvidenceSchema.MetricsFileName + "`.");
            sb.AppendLine(
                "Retained runs: `" + GhostEvidenceSchema.BaselineRunRelativePath +
                "` + `runs/fan-XX.json` when characterization retains fans.");
            sb.AppendLine(
                "Console report: `" + GhostEvidenceSchema.ConsoleReportRelativePath + "`.");
            return sb.ToString();
        }

        /// <summary>
        /// Console report that agrees with metrics.json — never embeds fabricated
        /// thresholdEstimateMetres=0 when hasThresholdEstimate=false.
        /// </summary>
        public static string FormatHonestConsoleReport(GhostEvidenceDocument document)
        {
            return FormatHonestSearchReport(document.SearchResult, document.Success) +
                   Environment.NewLine +
                   GhostEvidenceReport.Format(document);
        }

        public static string FormatHonestSearchReport(EpsilonSearchResult result, bool documentSuccess)
        {
            var sb = new StringBuilder(1024);
            sb.AppendLine("BUGCAM_BLOCK_1_4_EPSILON_SEARCH");
            sb.AppendLine("succeeded=" + (result.Succeeded && documentSuccess));
            if (!result.Succeeded)
            {
                sb.AppendLine("errorReason=" + (result.ErrorReason ?? string.Empty));
                return sb.ToString();
            }

            if (!documentSuccess)
            {
                sb.AppendLine("errorReason=document success=false");
                sb.AppendLine("verdict=" + result.Verdict);
                sb.AppendLine("searchRangeStartMetres=null");
                sb.AppendLine("searchRangeCeilingMetres=null");
                sb.AppendLine("searchRangeStartMillimetres=null");
                sb.AppendLine("searchRangeCeilingMillimetres=null");
                sb.AppendLine("characterizationCeilingMetres=null");
                sb.AppendLine("characterizationCeilingMillimetres=null");
                sb.AppendLine("hasLargestStableEpsilon=False");
                sb.AppendLine("largestStableEpsilonMetres=null");
                sb.AppendLine("hasSmallestDivergentEpsilon=False");
                sb.AppendLine("smallestDivergentEpsilonMetres=null");
                sb.AppendLine("hasThresholdEstimate=False");
                sb.AppendLine("thresholdEstimateMetres=null");
                sb.AppendLine("hasReferenceEpsilon=False");
                sb.AppendLine("referenceEpsilonMetres=null");
                sb.AppendLine("referenceIsExactThreshold=False");
                sb.AppendLine("hasFinalBracketWidth=False");
                sb.AppendLine("finalBracketWidthMetres=null");
                sb.AppendLine("ladderCount=0");
                sb.AppendLine("exponentialCount=0");
                sb.AppendLine("bisectionCount=0");
                sb.AppendLine("fanCount=0");
                sb.AppendLine("retainedFanRunCount=0");
                sb.AppendLine("cacheHitCount=null");
                sb.AppendLine("physicalProbeCount=null");
                return sb.ToString();
            }

            sb.AppendLine("verdict=" + result.Verdict);
            sb.AppendLine("searchRangeStartMetres=" + InvariantOrNull(result.SearchRangeStartMetres));
            sb.AppendLine("searchRangeCeilingMetres=" + InvariantOrNull(result.SearchRangeCeilingMetres));
            sb.AppendLine(
                "searchRangeStartMillimetres=" +
                InvariantOrNull(result.SearchRangeStartMetres * 1000f));
            sb.AppendLine(
                "searchRangeCeilingMillimetres=" +
                InvariantOrNull(result.SearchRangeCeilingMetres * 1000f));
            sb.AppendLine(
                "characterizationCeilingMetres=" +
                InvariantOrNull(result.CharacterizationCeilingMetres));
            sb.AppendLine(
                "characterizationCeilingMillimetres=" +
                InvariantOrNull(result.CharacterizationCeilingMetres * 1000f));
            sb.AppendLine("hasLargestStableEpsilon=" + result.HasLargestStableEpsilon);
            sb.AppendLine(
                "largestStableEpsilonMetres=" +
                (result.HasLargestStableEpsilon
                    ? InvariantOrNull(result.LargestStableEpsilonMetres)
                    : "null"));
            sb.AppendLine("hasSmallestDivergentEpsilon=" + result.HasSmallestDivergentEpsilon);
            sb.AppendLine(
                "smallestDivergentEpsilonMetres=" +
                (result.HasSmallestDivergentEpsilon
                    ? InvariantOrNull(result.SmallestDivergentEpsilonMetres)
                    : "null"));
            sb.AppendLine("hasThresholdEstimate=" + result.HasThresholdEstimate);
            sb.AppendLine(
                "thresholdEstimateMetres=" +
                (result.HasThresholdEstimate
                    ? InvariantOrNull(result.ThresholdEstimateMetres)
                    : "null"));
            sb.AppendLine("hasReferenceEpsilon=" + HasReferenceEpsilon(result));
            sb.AppendLine(
                "referenceEpsilonMetres=" +
                (HasReferenceEpsilon(result)
                    ? InvariantOrNull(result.ReferenceEpsilonMetres)
                    : "null"));
            sb.AppendLine("referenceIsExactThreshold=False");
            sb.AppendLine("hasFinalBracketWidth=" + HasFinalBracketWidth(result));
            sb.AppendLine(
                "finalBracketWidthMetres=" +
                (HasFinalBracketWidth(result)
                    ? InvariantOrNull(result.FinalBracketWidthMetres)
                    : "null"));
            sb.AppendLine("ladderCount=" + result.LadderSummaries.Length);
            sb.AppendLine("exponentialCount=" + result.ExponentialSummaries.Length);
            sb.AppendLine("bisectionCount=" + result.BisectionSummaries.Length);
            sb.AppendLine("fanCount=" + result.FanSummaries.Length);
            sb.AppendLine("retainedFanRunCount=" + result.FanRuns.Length);
            sb.AppendLine("cacheHitCount=" + result.CacheHitCount);
            sb.AppendLine("physicalProbeCount=" + result.PhysicalProbeCount);
            return sb.ToString();
        }

        public static bool HasReferenceEpsilon(EpsilonSearchResult search)
        {
            if (!search.Succeeded)
            {
                return false;
            }

            var value = search.ReferenceEpsilonMetres;
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public static bool HasFinalBracketWidth(EpsilonSearchResult search)
        {
            if (!search.Succeeded || !search.HasThresholdEstimate)
            {
                return false;
            }

            var value = search.FinalBracketWidthMetres;
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }

        private static string BuildManifestJson(GhostEvidenceDocument document, string runDir)
        {
            var sb = new StringBuilder(2048);
            var search = document.SearchResult;
            var hasBaseline =
                document.Success &&
                search.Succeeded &&
                search.BaselineRun.Succeeded;
            var fanCount = document.Success ? document.Fans.Length : 0;

            sb.Append('{');
            WriteInt(sb, "schemaVersion", document.SchemaVersion, true);
            WriteString(sb, "kind", document.Kind);
            WriteString(sb, "runId", document.RunId);
            WriteBool(sb, "success", document.Success);
            WriteString(sb, "errorCode", document.ErrorCode ?? string.Empty);
            WriteString(sb, "runDirectory", runDir.Replace('\\', '/'));

            AppendPhysicsSnapshot(sb, document.Environment);
            AppendSettingsSource(sb, document.SettingsSource);
            if (document.SceneCapture.Performed)
            {
                AppendSceneCapture(sb, document.SceneCapture);
            }

            sb.Append(",\"artifacts\":[");
            AppendArtifact(sb, GhostEvidenceSchema.MetricsFileName, "metrics", true, true);
            sb.Append(',');
            AppendArtifact(sb, GhostEvidenceSchema.ManifestFileName, "manifest", true, true);
            sb.Append(',');
            AppendArtifact(sb, GhostEvidenceSchema.SummaryFileName, "summary", true, true);
            sb.Append(',');
            AppendArtifact(
                sb,
                GhostEvidenceSchema.ConsoleReportRelativePath,
                "consoleReport",
                true,
                true);
            sb.Append(',');
            AppendArtifact(
                sb,
                GhostEvidenceSchema.BaselineRunRelativePath,
                "baselineRun",
                hasBaseline,
                hasBaseline);
            for (var i = 0; i < fanCount; i++)
            {
                sb.Append(',');
                AppendArtifact(
                    sb,
                    GhostEvidenceSchema.FanRunRelativePath(i),
                    "fanRun",
                    true,
                    true);
            }

            // Visuals: capture writes after Write(); manifest records expected paths + pending status.
            sb.Append(',');
            AppendArtifact(
                sb,
                GhostEvidenceSchema.VisualRelativePath(GhostEvidenceSchema.OverviewPngFileName),
                "visualOverview",
                document.Success,
                false);
            sb.Append(',');
            AppendArtifact(
                sb,
                GhostEvidenceSchema.VisualRelativePath(
                    GhostEvidenceSchema.FirstDivergencePngFileName),
                "visualFirstSustainedDivergence",
                document.Success,
                false);
            sb.Append(',');
            AppendArtifact(
                sb,
                GhostEvidenceSchema.VisualRelativePath(GhostEvidenceSchema.MaxSpreadPngFileName),
                "visualMaximumSpread",
                document.Success,
                false);
            sb.Append(',');
            AppendArtifact(
                sb,
                GhostEvidenceSchema.VisualRelativePath(GhostEvidenceSchema.FinalPngFileName),
                "visualFinalState",
                document.Success,
                false);

            sb.Append(']');

            WriteString(sb, "metricsFile", GhostEvidenceSchema.MetricsFileName);
            WriteString(sb, "summaryFile", GhostEvidenceSchema.SummaryFileName);
            WriteString(sb, "consoleReportFile", GhostEvidenceSchema.ConsoleReportRelativePath);
            WriteString(sb, "visualsDirectory", GhostEvidenceSchema.VisualsDirectoryName);
            WriteString(sb, "runsDirectory", GhostEvidenceSchema.RunsDirectoryName);
            WriteInt(sb, "retainedFanCount", fanCount);
            WriteBool(sb, "baselineRunWritten", hasBaseline);
            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>
        /// Block 2.2.1 A1 settings provenance: source kind, asset identity, override flags
        /// and effective epsilon bounds (full "R" precision). captured=false is honest for
        /// pre-parameterization callers — never fabricate a source.
        /// </summary>
        private static void AppendSettingsSource(StringBuilder sb, GhostSettingsProvenance source)
        {
            sb.Append(",\"settingsSource\":{");
            WriteBool(sb, "captured", source.Captured, true);
            if (source.Captured)
            {
                WriteString(sb, "sourceKind", source.SourceKind);
                WriteString(sb, "description", source.Description);
                WriteString(sb, "assetName", source.AssetName);
                WriteString(sb, "assetGuid", source.AssetGuid);
                WriteBool(sb, "floorOverridden", source.FloorOverridden);
                WriteBool(sb, "ceilingOverridden", source.CeilingOverridden);
                WriteFloat(sb, "effectiveFloorMetres", source.EffectiveFloorMetres);
                WriteFloat(sb, "effectiveCeilingMetres", source.EffectiveCeilingMetres);
            }

            sb.Append('}');
        }

        /// <summary>
        /// Block 2.2.1 A2 sceneCapture section — present only when a capture happened
        /// (tower manifests stay unchanged). Carries the three-outcome per-object records,
        /// the id↔name map, the capture hash, and the kinematic-freeze warnings the
        /// evidence consumer must see without the window.
        /// </summary>
        private static void AppendSceneCapture(StringBuilder sb, SceneCaptureResult capture)
        {
            sb.Append(",\"sceneCapture\":{");
            WriteBool(sb, "captured", capture.Succeeded, true);
            WriteString(sb, "scenePath", capture.ScenePath);
            WriteString(sb, "captureHash", capture.CaptureHash);
            if (!capture.Succeeded)
            {
                WriteString(sb, "failureSummary", capture.FailureSummary);
            }

            var capturedStatics = 0;
            var frozenKinematics = 0;
            var excluded = 0;
            for (var i = 0; i < capture.Objects.Length; i++)
            {
                switch (capture.Objects[i].Status)
                {
                    case SceneCaptureObjectStatus.CapturedStatic:
                        capturedStatics++;
                        break;
                    case SceneCaptureObjectStatus.FrozenKinematic:
                        frozenKinematics++;
                        break;
                    case SceneCaptureObjectStatus.ExcludedSafely:
                        excluded++;
                        break;
                }
            }

            WriteInt(sb, "capturedBodyCount", capture.Bodies.Length);
            WriteInt(sb, "capturedStaticObjectCount", capturedStatics);
            WriteInt(sb, "frozenKinematicCount", frozenKinematics);
            WriteInt(sb, "excludedCount", excluded);

            sb.Append(",\"kinematicFreezeWarnings\":[");
            for (var i = 0; i < capture.KinematicFreezeWarnings.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                sb.Append('"').Append(Escape(capture.KinematicFreezeWarnings[i])).Append('"');
            }

            sb.Append(']');

            sb.Append(",\"bodyMap\":[");
            var firstBody = true;
            for (var i = 0; i < capture.Objects.Length; i++)
            {
                var record = capture.Objects[i];
                if (record.Status != SceneCaptureObjectStatus.CapturedDynamic)
                {
                    continue;
                }

                if (!firstBody)
                {
                    sb.Append(',');
                }

                firstBody = false;
                sb.Append('{');
                WriteInt(sb, "stableId", record.StableId, true);
                WriteString(sb, "hierarchyPath", record.HierarchyPath);
                // Display paths may repeat (48 bricks named identically); the order key
                // (sibling-index chain) is the unique identity behind the stable ID.
                WriteString(sb, "orderKey", record.OrderKey);
                sb.Append('}');
            }

            sb.Append(']');

            sb.Append(",\"objects\":[");
            for (var i = 0; i < capture.Objects.Length; i++)
            {
                var record = capture.Objects[i];
                if (i > 0)
                {
                    sb.Append(',');
                }

                sb.Append('{');
                WriteString(sb, "hierarchyPath", record.HierarchyPath, true);
                WriteString(sb, "status", record.Status.ToString());
                if (record.StableId >= 0)
                {
                    WriteInt(sb, "stableId", record.StableId);
                }
                else
                {
                    WriteNull(sb, "stableId");
                }

                WriteString(sb, "reason", record.Reason);
                sb.Append('}');
            }

            sb.Append("]}");
        }

        /// <summary>
        /// Live physics values captured at build time — never BugCam constants.
        /// captured=false (or a missing editor-serialized value) emits honest nulls.
        /// </summary>
        private static void AppendPhysicsSnapshot(StringBuilder sb, GhostRunEnvironment env)
        {
            var physics = env.Physics;
            sb.Append(",\"physicsSnapshot\":{");
            WriteBool(sb, "captured", physics.Captured, true);
            WriteString(sb, "unityVersion", env.UnityVersion ?? string.Empty);
            if (physics.Captured)
            {
                WriteFloat(sb, "fixedDeltaTime", physics.FixedDeltaTime);
                WriteString(sb, "simulationMode", physics.SimulationMode);
                WriteInt(sb, "solverIterations", physics.SolverIterations);
                WriteInt(sb, "solverVelocityIterations", physics.SolverVelocityIterations);
                WriteFloat(sb, "defaultContactOffset", physics.DefaultContactOffset);
                WriteFloat(
                    sb,
                    "defaultMaxDepenetrationVelocity",
                    physics.DefaultMaxDepenetrationVelocity);
                WriteFloat(sb, "sleepThreshold", physics.SleepThreshold);
                WriteFloat(sb, "bounceThreshold", physics.BounceThreshold);
                WriteVec3(sb, "gravity", physics.Gravity);
            }
            else
            {
                WriteNull(sb, "fixedDeltaTime");
                WriteNull(sb, "simulationMode");
                WriteNull(sb, "solverIterations");
                WriteNull(sb, "solverVelocityIterations");
                WriteNull(sb, "defaultContactOffset");
                WriteNull(sb, "defaultMaxDepenetrationVelocity");
                WriteNull(sb, "sleepThreshold");
                WriteNull(sb, "bounceThreshold");
                WriteNull(sb, "gravity");
            }

            if (physics.Captured && physics.HasEnhancedDeterminism)
            {
                WriteBool(sb, "enhancedDeterminism", physics.EnhancedDeterminism);
            }
            else
            {
                WriteNull(sb, "enhancedDeterminism");
            }

            if (physics.Captured && physics.HasThreadingMode)
            {
                WriteInt(sb, "threadingModeSerialized", physics.ThreadingModeSerialized);
                WriteString(sb, "threadingModeName", physics.ThreadingModeName);
            }
            else
            {
                WriteNull(sb, "threadingModeSerialized");
                WriteNull(sb, "threadingModeName");
            }

            sb.Append('}');
        }

        private static void AppendArtifact(
            StringBuilder sb,
            string relativePath,
            string role,
            bool required,
            bool available)
        {
            sb.Append('{');
            WriteString(sb, "path", relativePath, true);
            WriteString(sb, "role", role);
            WriteBool(sb, "required", required);
            WriteBool(sb, "available", available);
            WriteString(
                sb,
                "status",
                available ? "present" : (required ? "pending" : "omitted"));
            sb.Append('}');
        }

        private static void AtomicWrite(string path, string contents)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
            File.WriteAllText(temp, contents ?? string.Empty, new UTF8Encoding(false));
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(temp, path);
        }

        private static void WriteInt(StringBuilder sb, string name, int value, bool first = false)
        {
            if (!first)
            {
                sb.Append(',');
            }

            sb.Append('"').Append(name).Append("\":")
                .Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void WriteBool(StringBuilder sb, string name, bool value, bool first = false)
        {
            if (!first)
            {
                sb.Append(',');
            }

            sb.Append('"').Append(name).Append("\":").Append(value ? "true" : "false");
        }

        private static void WriteString(StringBuilder sb, string name, string value, bool first = false)
        {
            if (!first)
            {
                sb.Append(',');
            }

            sb.Append('"').Append(name).Append("\":\"").Append(Escape(value)).Append('"');
        }

        private static void WriteNull(StringBuilder sb, string name, bool first = false)
        {
            if (!first)
            {
                sb.Append(',');
            }

            sb.Append('"').Append(name).Append("\":null");
        }

        private static void WriteFloat(StringBuilder sb, string name, float value, bool first = false)
        {
            if (!first)
            {
                sb.Append(',');
            }

            sb.Append('"').Append(name).Append("\":");
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                sb.Append("null");
            }
            else
            {
                sb.Append(value.ToString("R", CultureInfo.InvariantCulture));
            }
        }

        private static void WriteVec3(StringBuilder sb, string name, Vector3 value)
        {
            sb.Append(",\"").Append(name).Append("\":{");
            WriteFloat(sb, "x", value.x, true);
            WriteFloat(sb, "y", value.y);
            WriteFloat(sb, "z", value.z);
            sb.Append('}');
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        private static string Invariant(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return "n/a";
            }

            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string InvariantOrNull(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return "null";
            }

            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string MetresLabel(float metres)
        {
            if (float.IsNaN(metres) || float.IsInfinity(metres))
            {
                return "n/a";
            }

            return Invariant(metres) + " m (" + Invariant(metres * 1000f) + " mm)";
        }

        private static string FormatAxisLabel(Vector3 axis)
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

            return Invariant(axis.x) + "," + Invariant(axis.y) + "," + Invariant(axis.z);
        }
    }
}
