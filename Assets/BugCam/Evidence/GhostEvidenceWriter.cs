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
                Directory.CreateDirectory(checkpointDir);

                var metricsPath = Path.Combine(runDir, GhostEvidenceSchema.MetricsFileName);
                var manifestPath = Path.Combine(runDir, GhostEvidenceSchema.ManifestFileName);
                var summaryPath = Path.Combine(runDir, GhostEvidenceSchema.SummaryFileName);
                var consolePath = Path.Combine(
                    runDir,
                    GhostEvidenceSchema.ReportDirectoryName,
                    GhostEvidenceSchema.ConsoleReportFileName);

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

        public static string BuildMetricsJson(GhostEvidenceDocument document)
        {
            var sb = new StringBuilder(4096);
            var search = document.SearchResult;
            var identity = document.SearchIdentity;
            var primary = document.PrimaryDivergence;
            var env = document.Environment;
            var hasReference = HasReferenceEpsilon(search);
            var hasBracketWidth = HasFinalBracketWidth(search);

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
            WriteInt(sb, "physicalProbeCount", search.PhysicalProbeCount);
            WriteInt(sb, "cacheHitCount", search.CacheHitCount);

            sb.Append(",\"primary\":{");
            WriteBool(sb, "analyzeSucceeded", primary.Succeeded, true);
            WriteBool(sb, "hasSignificantDivergence", primary.HasSignificantDivergence);
            WriteInt(sb, "firstDivergenceFrame", primary.FirstDivergenceFrame);
            WriteFloat(sb, "maxSpreadMetres", primary.MaxSpreadMetres);
            WriteInt(sb, "maxSpreadStep", primary.MaxSpreadStep);
            WriteInt(sb, "maxSpreadBodyId", primary.MaxSpreadBodyId);
            WriteInt(sb, "affectedBodyCount", primary.AffectedBodyCount);
            WriteBool(sb, "amplificationDefined", primary.AmplificationDefined);
            if (primary.AmplificationDefined)
            {
                WriteFloat(sb, "amplification", primary.Amplification);
            }
            else
            {
                WriteNull(sb, "amplification");
            }

            sb.Append(",\"affectedBodyIds\":[");
            var affected = primary.AffectedBodyIds ?? Array.Empty<int>();
            for (var i = 0; i < affected.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                sb.Append(affected[i].ToString(CultureInfo.InvariantCulture));
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
                sb.Append('{');
                WriteInt(sb, "fanIndex", fan.FanIndex, true);
                WriteFloat(sb, "multiplier", fan.Multiplier);
                WriteVec3(sb, "axis", fan.Axis);
                WriteFloat(sb, "epsilonMetres", fan.EpsilonMetres);
                WriteBool(sb, "outsideSearchRange", fan.OutsideSearchRange);
                WriteBool(sb, "hasSignificantDivergence", fan.Divergence.HasSignificantDivergence);
                WriteInt(sb, "firstDivergenceFrame", fan.Divergence.FirstDivergenceFrame);
                WriteFloat(sb, "maxSpreadMetres", fan.Divergence.MaxSpreadMetres);
                WriteBool(sb, "amplificationDefined", fan.Divergence.AmplificationDefined);
                if (fan.Divergence.AmplificationDefined)
                {
                    WriteFloat(sb, "amplification", fan.Divergence.Amplification);
                }
                else
                {
                    WriteNull(sb, "amplification");
                }

                sb.Append('}');
            }

            sb.Append(']');

            sb.Append(",\"drawSet\":{");
            WriteInt(sb, "polylineCount", document.DrawSet.Polylines.Length, true);
            WriteBool(sb, "hasFirstDivergence", document.DrawSet.HasFirstDivergence);
            WriteBool(sb, "hasMaxSpread", document.DrawSet.HasMaxSpread);
            WriteBool(sb, "hasBounds", document.DrawSet.HasBounds);
            sb.Append('}');

            sb.Append('}');
            return sb.ToString();
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

            if (document.Success && primary.Succeeded && primary.HasSignificantDivergence)
            {
                sb.AppendLine("- **First divergence frame:** " + primary.FirstDivergenceFrame);
                sb.AppendLine("- **Max spread:** " + MetresLabel(primary.MaxSpreadMetres));
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
            var sb = new StringBuilder(512);
            sb.Append('{');
            WriteInt(sb, "schemaVersion", document.SchemaVersion, true);
            WriteString(sb, "kind", document.Kind);
            WriteString(sb, "runId", document.RunId);
            WriteBool(sb, "success", document.Success);
            WriteString(sb, "errorCode", document.ErrorCode ?? string.Empty);
            WriteString(sb, "runDirectory", runDir.Replace('\\', '/'));
            WriteString(sb, "metricsFile", GhostEvidenceSchema.MetricsFileName);
            WriteString(sb, "summaryFile", GhostEvidenceSchema.SummaryFileName);
            WriteString(
                sb,
                "consoleReportFile",
                GhostEvidenceSchema.ReportDirectoryName + "/" +
                GhostEvidenceSchema.ConsoleReportFileName);
            WriteString(sb, "visualsDirectory", GhostEvidenceSchema.VisualsDirectoryName);
            sb.Append('}');
            return sb.ToString();
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
