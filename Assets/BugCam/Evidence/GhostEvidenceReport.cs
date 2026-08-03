using System.Globalization;
using System.Text;
using BugCam.Core;

namespace BugCam.Evidence
{
    /// <summary>
    /// Console numbers for Block 1.5 ghost evidence.
    /// Pair with <see cref="GhostEvidenceWriter.FormatHonestSearchReport"/> after a search completes.
    /// </summary>
    public static class GhostEvidenceReport
    {
        public static string Format(GhostEvidenceDocument document)
        {
            var sb = new StringBuilder(1024);
            sb.AppendLine("BUGCAM_BLOCK_1_5_GHOST_EVIDENCE");
            if (document == null)
            {
                sb.AppendLine("succeeded=False");
                sb.AppendLine("errorCode=" + GhostEvidenceErrorCodes.BuildFailed);
                sb.AppendLine("errorReason=document is null");
                return sb.ToString();
            }

            var search = document.SearchResult;
            sb.AppendLine("succeeded=" + document.Success);
            sb.AppendLine("errorCode=" + (document.ErrorCode ?? string.Empty));
            sb.AppendLine("errorReason=" + (document.ErrorReason ?? string.Empty));
            sb.AppendLine("schemaVersion=" + document.SchemaVersion);
            sb.AppendLine("kind=" + document.Kind);
            sb.AppendLine("runId=" + document.RunId);
            sb.AppendLine("unityVersion=" + (document.Environment.UnityVersion ?? string.Empty));
            sb.AppendLine("gitCommitSha=" + (document.Environment.GitCommitSha ?? string.Empty));
            sb.AppendLine("gitBranch=" + (document.Environment.GitBranch ?? string.Empty));
            sb.AppendLine("scenePath=" + (document.Environment.ScenePath ?? string.Empty));
            sb.AppendLine("verdict=" + search.Verdict);
            sb.AppendLine("targetBodyId=" + document.SearchIdentity.TargetBodyId);
            sb.AppendLine(
                "searchAxis=" +
                FormatAxis(document.SearchIdentity.SearchAxis));
            sb.AppendLine("strategy=" + document.SearchIdentity.Strategy);
            sb.AppendLine("ghostBodyLimit=" + document.GhostBodyLimit);
            sb.AppendLine("rankedBodyCount=" + document.RankedBodies.Length);
            sb.AppendLine("retainedFanCount=" + document.Fans.Length);
            sb.AppendLine("hasPrimaryFan=" + document.HasPrimaryFan);
            sb.AppendLine("primaryFanIndex=" + document.PrimaryFanIndex);

            sb.AppendLine("hasThresholdEstimate=" + search.HasThresholdEstimate);
            if (search.HasThresholdEstimate && document.Success)
            {
                sb.AppendLine(
                    "thresholdEstimateMetres=" + Invariant(search.ThresholdEstimateMetres));
            }
            else
            {
                sb.AppendLine("thresholdEstimateMetres=null");
            }

            var hasReference = GhostEvidenceWriter.HasReferenceEpsilon(search) && document.Success;
            sb.AppendLine("hasReferenceEpsilon=" + hasReference);
            if (hasReference)
            {
                sb.AppendLine(
                    "referenceEpsilonMetres=" + Invariant(search.ReferenceEpsilonMetres));
            }
            else
            {
                sb.AppendLine("referenceEpsilonMetres=null");
            }

            sb.AppendLine("referenceIsExactThreshold=False");

            var hasBracket = GhostEvidenceWriter.HasFinalBracketWidth(search) && document.Success;
            sb.AppendLine("hasFinalBracketWidth=" + hasBracket);
            if (hasBracket)
            {
                sb.AppendLine(
                    "finalBracketWidthMetres=" + Invariant(search.FinalBracketWidthMetres));
            }
            else
            {
                sb.AppendLine("finalBracketWidthMetres=null");
            }

            if (document.Success && search.Succeeded)
            {
                sb.AppendLine(
                    "searchRangeStartMetres=" + Invariant(search.SearchRangeStartMetres));
                sb.AppendLine(
                    "searchRangeCeilingMetres=" + Invariant(search.SearchRangeCeilingMetres));
                sb.AppendLine(
                    "characterizationCeilingMetres=" +
                    Invariant(search.CharacterizationCeilingMetres));
                sb.AppendLine("searchFloorMetres=" + Invariant(search.SearchRangeStartMetres));
            }
            else
            {
                sb.AppendLine("searchRangeStartMetres=null");
                sb.AppendLine("searchRangeCeilingMetres=null");
                sb.AppendLine("characterizationCeilingMetres=null");
                sb.AppendLine("searchFloorMetres=null");
            }

            var primary = document.PrimaryDivergence;
            sb.AppendLine("primaryAnalyzeSucceeded=" + primary.Succeeded);
            sb.AppendLine("hasSignificantDivergence=" + primary.HasSignificantDivergence);
            sb.AppendLine("firstDivergenceFrame=" + primary.FirstDivergenceFrame);
            sb.AppendLine("maxSpreadMetres=" + Invariant(primary.MaxSpreadMetres));
            sb.AppendLine("maxSpreadStep=" + primary.MaxSpreadStep);
            sb.AppendLine("maxSpreadBodyId=" + primary.MaxSpreadBodyId);
            sb.AppendLine("affectedBodyCount=" + primary.AffectedBodyCount);
            sb.AppendLine("amplificationDefined=" + primary.AmplificationDefined);
            if (primary.AmplificationDefined)
            {
                sb.AppendLine("amplification=" + Invariant(primary.Amplification));
            }
            else
            {
                sb.AppendLine("amplification=null");
            }

            sb.AppendLine("drawPolylineCount=" + document.DrawSet.Polylines.Length);
            sb.AppendLine("hasFirstDivergenceMarker=" + document.DrawSet.HasFirstDivergence);
            sb.AppendLine("hasMaxSpreadMarker=" + document.DrawSet.HasMaxSpread);

            sb.Append("rankedBodyIds=");
            for (var i = 0; i < document.RankedBodies.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                sb.Append(document.RankedBodies[i].BodyId);
            }

            sb.AppendLine();

            for (var i = 0; i < document.Fans.Length; i++)
            {
                var fan = document.Fans[i];
                sb.Append("fan[");
                sb.Append(i);
                sb.Append("]=multiplier=");
                sb.Append(Invariant(fan.Multiplier));
                sb.Append(" axis=");
                sb.Append(FormatAxis(fan.Axis));
                sb.Append(" epsilonMetres=");
                sb.Append(Invariant(fan.EpsilonMetres));
                sb.Append(" outsideSearchRange=");
                sb.Append(fan.OutsideSearchRange);
                sb.Append(" diverged=");
                sb.Append(fan.Divergence.HasSignificantDivergence);
                sb.Append(" firstDivergenceFrame=");
                sb.Append(fan.Divergence.FirstDivergenceFrame);
                sb.Append(" maxSpreadMetres=");
                sb.Append(Invariant(fan.Divergence.MaxSpreadMetres));
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static string FormatAxis(UnityEngine.Vector3 axis)
        {
            return Invariant(axis.x) + "," + Invariant(axis.y) + "," + Invariant(axis.z);
        }

        private static string Invariant(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return "null";
            }

            return value.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
