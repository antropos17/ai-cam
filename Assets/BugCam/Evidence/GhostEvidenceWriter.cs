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
    /// unavailable values use null / boolean flags.
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
                    EpsilonSearchReport.Format(document.SearchResult) +
                    Environment.NewLine +
                    GhostEvidenceReport.Format(document));

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

            sb.Append('{');
            WriteInt(sb, "schemaVersion", document.SchemaVersion, true);
            WriteString(sb, "kind", document.Kind);
            WriteString(sb, "runId", document.RunId);
            WriteString(sb, "builtUtc", document.BuiltUtc.ToString("o", CultureInfo.InvariantCulture));
            WriteString(sb, "verdict", search.Verdict);
            WriteString(sb, "verdictKind", search.VerdictKind.ToString());

            sb.Append(",\"searchIdentity\":{");
            WriteInt(sb, "targetBodyId", identity.TargetBodyId, true);
            WriteVec3(sb, "searchAxis", identity.SearchAxis);
            WriteString(sb, "strategy", identity.Strategy.ToString());
            sb.Append('}');

            sb.Append(",\"ranges\":{");
            WriteFloat(sb, "searchFloorMetres", search.SearchRangeStartMetres, true);
            WriteFloat(sb, "searchRangeStartMetres", search.SearchRangeStartMetres);
            WriteFloat(sb, "searchRangeCeilingMetres", search.SearchRangeCeilingMetres);
            WriteFloat(sb, "characterizationCeilingMetres", search.CharacterizationCeilingMetres);
            sb.Append('}');

            WriteBool(sb, "hasThresholdEstimate", search.HasThresholdEstimate);
            if (search.HasThresholdEstimate)
            {
                WriteFloat(sb, "thresholdEstimateMetres", search.ThresholdEstimateMetres);
            }
            else
            {
                WriteNull(sb, "thresholdEstimateMetres");
            }

            WriteFloat(sb, "referenceEpsilonMetres", search.ReferenceEpsilonMetres);
            WriteBool(sb, "referenceIsExactThreshold", search.ReferenceIsExactThreshold);
            // Contract: always false — never claim an exact mathematical threshold.
            if (search.ReferenceIsExactThreshold)
            {
                // Honesty: still emit the Core value, but builders must keep this false.
            }

            WriteBool(sb, "hasLargestStableEpsilon", search.HasLargestStableEpsilon);
            if (search.HasLargestStableEpsilon)
            {
                WriteFloat(sb, "largestStableEpsilonMetres", search.LargestStableEpsilonMetres);
            }
            else
            {
                WriteNull(sb, "largestStableEpsilonMetres");
            }

            WriteBool(sb, "hasSmallestDivergentEpsilon", search.HasSmallestDivergentEpsilon);
            if (search.HasSmallestDivergentEpsilon)
            {
                WriteFloat(sb, "smallestDivergentEpsilonMetres", search.SmallestDivergentEpsilonMetres);
            }
            else
            {
                WriteNull(sb, "smallestDivergentEpsilonMetres");
            }

            WriteFloat(sb, "finalBracketWidthMetres", search.FinalBracketWidthMetres);
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
            for (var i = 0; i < primary.AffectedBodyIds.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                sb.Append(primary.AffectedBodyIds[i].ToString(CultureInfo.InvariantCulture));
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
            sb.AppendLine("- **Verdict:** " + search.Verdict);
            sb.AppendLine(
                "- **Search identity:** body " + document.SearchIdentity.TargetBodyId +
                ", axis " + FormatAxisLabel(document.SearchIdentity.SearchAxis) +
                ", strategy " + document.SearchIdentity.Strategy);
            sb.AppendLine(
                "- **Search floor / range:** " +
                MetresLabel(search.SearchRangeStartMetres) + " … " +
                MetresLabel(search.SearchRangeCeilingMetres));
            sb.AppendLine(
                "- **Characterization range ceiling:** " +
                MetresLabel(search.CharacterizationCeilingMetres));

            if (search.HasThresholdEstimate)
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

            sb.AppendLine(
                "- **Reference Epsilon:** " +
                MetresLabel(search.ReferenceEpsilonMetres) +
                " (`referenceIsExactThreshold=" + search.ReferenceIsExactThreshold + "`)");
            sb.AppendLine(
                "- **Retained fans:** " + document.Fans.Length +
                " (STABLE fabricates none)");
            sb.AppendLine("- **Ghost body limit:** " + document.GhostBodyLimit);
            sb.AppendLine("- **Ranked bodies drawn:** " + document.RankedBodies.Length);

            if (primary.Succeeded && primary.HasSignificantDivergence)
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

        private static string BuildManifestJson(GhostEvidenceDocument document, string runDir)
        {
            var sb = new StringBuilder(512);
            sb.Append('{');
            WriteInt(sb, "schemaVersion", document.SchemaVersion, true);
            WriteString(sb, "kind", document.Kind);
            WriteString(sb, "runId", document.RunId);
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

        private static void WriteNull(StringBuilder sb, string name)
        {
            sb.Append(",\"").Append(name).Append("\":null");
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
