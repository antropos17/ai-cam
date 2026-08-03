using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace BugCam.Evidence
{
    /// <summary>
    /// Atomic camera-plan.json writer. Invariant culture, null on unavailable values — never a
    /// fabricated zero (matches <see cref="GhostEvidenceWriter"/> conventions). Every candidate is
    /// written, including ground-plane-rejected ones, so provenance includes the losers
    /// (docs/PLAN.md Block 2.1 manifest requirement).
    /// </summary>
    public static class EvidenceCameraPlanWriter
    {
        public readonly struct WriteResult
        {
            private WriteResult(bool succeeded, string errorReason, string path)
            {
                Succeeded = succeeded;
                ErrorReason = errorReason ?? string.Empty;
                Path = path ?? string.Empty;
            }

            public bool Succeeded { get; }

            public string ErrorReason { get; }

            public string Path { get; }

            public static WriteResult Success(string path)
            {
                return new WriteResult(true, string.Empty, path);
            }

            public static WriteResult Failure(string errorReason)
            {
                return new WriteResult(false, errorReason, string.Empty);
            }
        }

        public static WriteResult Write(EvidenceCameraPlanResult plan, string runId, string projectRoot)
        {
            if (string.IsNullOrEmpty(runId))
            {
                return WriteResult.Failure("runId is required.");
            }

            if (string.IsNullOrEmpty(projectRoot))
            {
                return WriteResult.Failure("projectRoot is required.");
            }

            try
            {
                var path = Path.Combine(
                    projectRoot,
                    EvidenceCameraPlanSchema.RelativePath(runId).Replace('/', Path.DirectorySeparatorChar));
                AtomicWrite(path, BuildJson(plan, runId));
                return WriteResult.Success(path);
            }
            catch (Exception ex)
            {
                return WriteResult.Failure(ex.Message);
            }
        }

        /// <summary>Exposed for EditMode VERIFY — byte-identical JSON from identical inputs.</summary>
        public static string BuildJson(EvidenceCameraPlanResult plan, string runId)
        {
            var sb = new StringBuilder(2048 + (plan.Candidates.Length * 160));
            sb.Append('{');
            WriteInt(sb, "schemaVersion", EvidenceCameraPlanSchema.SchemaVersion, true);
            WriteString(sb, "kind", EvidenceCameraPlanSchema.Kind);
            WriteString(sb, "runId", runId ?? string.Empty);
            WriteInt(sb, "algorithmVersion", plan.AlgorithmVersion);
            WriteBool(sb, "success", plan.Succeeded);
            WriteString(sb, "errorReason", plan.ErrorReason ?? string.Empty);
            WriteString(sb, "verdict", plan.Verdict ?? string.Empty);
            WriteBool(sb, "hasAdequateCoverage", plan.HasAdequateCoverage);

            if (plan.Succeeded)
            {
                // occlusionCoveragePerBody is the verdict gate; bestScorePerBody is the ranking
                // score kept for provenance — the two are deliberately distinct (2026-08-03).
                WriteFloat(sb, "occlusionCoveragePerBody", plan.OcclusionCoveragePerBody);
                WriteFloat(sb, "bestScorePerBody", plan.BestScorePerBody);
                WriteInt(sb, "firstDivergenceFrame", plan.FirstDivergenceFrame);
                sb.Append(",\"eventBounds\":{");
                WriteVec3(sb, "center", plan.EventBoundsCenter, true);
                WriteFloat(sb, "radiusMetres", plan.EventBoundsRadius);
                sb.Append('}');
            }
            else
            {
                WriteNull(sb, "occlusionCoveragePerBody");
                WriteNull(sb, "bestScorePerBody");
                WriteNull(sb, "firstDivergenceFrame");
                WriteNull(sb, "eventBounds");
            }

            sb.Append(",\"affectedBodyIds\":[");
            for (var i = 0; i < plan.AffectedBodyIds.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                sb.Append(plan.AffectedBodyIds[i].ToString(CultureInfo.InvariantCulture));
            }

            sb.Append(']');

            WriteInt(sb, "candidateCount", plan.CandidateCount);

            sb.Append(",\"candidates\":[");
            for (var i = 0; i < plan.Candidates.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                WriteCandidate(sb, plan.Candidates[i]);
            }

            sb.Append(']');

            sb.Append(",\"winners\":[");
            for (var i = 0; i < plan.Winners.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                WriteWinner(sb, plan.Winners[i]);
            }

            sb.Append(']');
            sb.Append('}');
            return sb.ToString();
        }

        private static void WriteCandidate(StringBuilder sb, EvidenceCameraCandidateResult candidate)
        {
            sb.Append('{');
            WriteInt(sb, "index", candidate.Index, true);
            WriteVec3(sb, "position", candidate.Position);
            WriteBool(sb, "rejectedBelowGroundPlane", candidate.RejectedBelowGroundPlane);
            WriteFloat(sb, "inFrustumScore", candidate.InFrustumScore);
            WriteFloat(sb, "visibilityScore", candidate.VisibilityScore);
            WriteFloat(sb, "centralityPenalty", candidate.CentralityPenalty);
            WriteFloat(sb, "totalScore", candidate.TotalScore);
            sb.Append('}');
        }

        private static void WriteWinner(StringBuilder sb, EvidenceCameraWinner winner)
        {
            sb.Append('{');
            WriteInt(sb, "slot", winner.Slot, true);
            WriteInt(sb, "candidateIndex", winner.CandidateIndex);

            // Camera 1 (slot 1) is chosen by raw TotalScore, not the orthogonality ranking —
            // its orthogonality field is not applicable. Honest null, not a fabricated zero.
            if (winner.Slot == 1)
            {
                WriteNull(sb, "orthogonalityToCamera1");
            }
            else
            {
                WriteFloat(sb, "orthogonalityToCamera1", winner.OrthogonalityToCamera1);
            }

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

        private static void WriteVec3(StringBuilder sb, string name, Vector3 value, bool first = false)
        {
            if (!first)
            {
                sb.Append(',');
            }

            sb.Append('"').Append(name).Append("\":{");
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

            var sb = new StringBuilder(value.Length + 8);
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }
    }
}
