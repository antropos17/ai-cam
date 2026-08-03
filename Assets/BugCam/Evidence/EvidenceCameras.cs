using System;
using BugCam.Core;
using UnityEngine;

namespace BugCam.Evidence
{
    /// <summary>One scored candidate camera position — every candidate, chosen or not (§ provenance).</summary>
    public readonly struct EvidenceCameraCandidateResult
    {
        public EvidenceCameraCandidateResult(
            int index,
            Vector3 position,
            bool rejectedBelowGroundPlane,
            float inFrustumScore,
            float visibilityScore,
            float separationScore,
            float centralityPenalty,
            float totalScore)
        {
            Index = index;
            Position = position;
            RejectedBelowGroundPlane = rejectedBelowGroundPlane;
            InFrustumScore = inFrustumScore;
            VisibilityScore = visibilityScore;
            SeparationScore = separationScore;
            CentralityPenalty = centralityPenalty;
            TotalScore = totalScore;
        }

        public int Index { get; }

        public Vector3 Position { get; }

        /// <summary>True when this candidate sat at or below the ground plane and was never scored.</summary>
        public bool RejectedBelowGroundPlane { get; }

        /// <summary>Sum over affected bodies of the in-frustum term (0 or 1 per body).</summary>
        public float InFrustumScore { get; }

        /// <summary>Sum over affected bodies of (1 - fractional occlusion).</summary>
        public float VisibilityScore { get; }

        /// <summary>Sum over affected bodies of baseline/perturbed screen-space pixel separation, normalized.</summary>
        public float SeparationScore { get; }

        /// <summary>Sum over affected bodies of the frame-edge distance penalty (unweighted).</summary>
        public float CentralityPenalty { get; }

        /// <summary>InFrustumScore + VisibilityScore + SeparationScore - WeightEvidenceCentrality * CentralityPenalty.</summary>
        public float TotalScore { get; }
    }

    /// <summary>One of cameras 2-4: a candidate plus the (a)/(b)/(c) terms that ranked it.</summary>
    public readonly struct EvidenceCameraWinner
    {
        public EvidenceCameraWinner(
            int slot,
            int candidateIndex,
            float orthogonalityToCamera1,
            float contactProximity,
            float trajectoryAlignment,
            float rankScore)
        {
            Slot = slot;
            CandidateIndex = candidateIndex;
            OrthogonalityToCamera1 = orthogonalityToCamera1;
            ContactProximity = contactProximity;
            TrajectoryAlignment = trajectoryAlignment;
            RankScore = rankScore;
        }

        /// <summary>1-based camera slot; slot 1 is always the highest raw-score candidate.</summary>
        public int Slot { get; }

        public int CandidateIndex { get; }

        /// <summary>0 (same axis as camera 1) .. 1 (perpendicular). Always 0 for slot 1 itself.</summary>
        public float OrthogonalityToCamera1 { get; }

        /// <summary>Criterion (b) approximation: 1 / (1 + distance to the event bounds center).</summary>
        public float ContactProximity { get; }

        /// <summary>Criterion (c): 0 (aligned with average affected-body velocity) .. 1 (perpendicular).</summary>
        public float TrajectoryAlignment { get; }

        /// <summary>WeightCameraOrthogonality*Orthogonality + WeightContactProximity*ContactProximity + WeightTrajectoryAlignment*TrajectoryAlignment.</summary>
        public float RankScore { get; }
    }

    /// <summary>Outcome of <see cref="EvidenceCameras.Plan"/> — the single source of truth for camera-plan.json.</summary>
    public readonly struct EvidenceCameraPlanResult
    {
        private static readonly EvidenceCameraCandidateResult[] EmptyCandidates =
            Array.Empty<EvidenceCameraCandidateResult>();

        private static readonly EvidenceCameraWinner[] EmptyWinners =
            Array.Empty<EvidenceCameraWinner>();

        private static readonly int[] EmptyIds = Array.Empty<int>();

        private EvidenceCameraPlanResult(
            bool succeeded,
            string errorReason,
            int algorithmVersion,
            int candidateCount,
            Vector3 eventBoundsCenter,
            float eventBoundsRadius,
            int firstDivergenceFrame,
            int[] affectedBodyIds,
            EvidenceCameraCandidateResult[] candidates,
            EvidenceCameraWinner[] winners,
            bool hasAdequateCoverage,
            float bestScorePerBody,
            string verdict)
        {
            Succeeded = succeeded;
            ErrorReason = errorReason ?? string.Empty;
            AlgorithmVersion = algorithmVersion;
            CandidateCount = candidateCount;
            EventBoundsCenter = eventBoundsCenter;
            EventBoundsRadius = eventBoundsRadius;
            FirstDivergenceFrame = firstDivergenceFrame;
            AffectedBodyIds = affectedBodyIds ?? EmptyIds;
            Candidates = candidates ?? EmptyCandidates;
            Winners = winners ?? EmptyWinners;
            HasAdequateCoverage = hasAdequateCoverage;
            BestScorePerBody = bestScorePerBody;
            Verdict = verdict ?? string.Empty;
        }

        public bool Succeeded { get; }

        public string ErrorReason { get; }

        public int AlgorithmVersion { get; }

        public int CandidateCount { get; }

        public Vector3 EventBoundsCenter { get; }

        public float EventBoundsRadius { get; }

        public int FirstDivergenceFrame { get; }

        public int[] AffectedBodyIds { get; }

        /// <summary>Every generated candidate, index-ordered, including rejected ones (provenance).</summary>
        public EvidenceCameraCandidateResult[] Candidates { get; }

        /// <summary>1-4 winners, slot ascending. Winners[0] is always camera 1 (highest raw score).</summary>
        public EvidenceCameraWinner[] Winners { get; }

        /// <summary>False when the honest verdict is EVIDENCE COVERAGE: LOW.</summary>
        public bool HasAdequateCoverage { get; }

        /// <summary>Camera 1's TotalScore divided by AffectedBodyIds.Length — the honest-verdict gate value.</summary>
        public float BestScorePerBody { get; }

        /// <summary><see cref="EvidenceCameras.VerdictLow"/> or <see cref="EvidenceCameras.VerdictOk"/>.</summary>
        public string Verdict { get; }

        public static EvidenceCameraPlanResult Failure(string errorReason)
        {
            return new EvidenceCameraPlanResult(
                false, errorReason, 0, 0, Vector3.zero, 0f, -1,
                EmptyIds, EmptyCandidates, EmptyWinners, false, 0f, string.Empty);
        }

        public static EvidenceCameraPlanResult Success(
            int algorithmVersion,
            int candidateCount,
            Vector3 eventBoundsCenter,
            float eventBoundsRadius,
            int firstDivergenceFrame,
            int[] affectedBodyIds,
            EvidenceCameraCandidateResult[] candidates,
            EvidenceCameraWinner[] winners,
            bool hasAdequateCoverage,
            float bestScorePerBody,
            string verdict)
        {
            return new EvidenceCameraPlanResult(
                true, string.Empty, algorithmVersion, candidateCount, eventBoundsCenter,
                eventBoundsRadius, firstDivergenceFrame, affectedBodyIds, candidates, winners,
                hasAdequateCoverage, bestScorePerBody, verdict);
        }
    }

    /// <summary>
    /// Block 2.1 deterministic evidence-camera selection. Pure post-process over two recorded
    /// <see cref="RunResult"/>s and their <see cref="DivergenceResult"/> — no live scene, no
    /// Physics.Raycast, no Camera GameObject (see EvidenceCameraMath), so a third party can
    /// reproduce the exact candidate scores and winner indices from the same recorded runs alone.
    /// Nothing here lands in Core/ (docs/PLAN.md Block 2.1 constraint).
    /// </summary>
    public static class EvidenceCameras
    {
        public const int AlgorithmVersion = 1;

        public const string VerdictLow = "EVIDENCE COVERAGE: LOW";

        public const string VerdictOk = "EVIDENCE COVERAGE: OK";

        private const int SamplePointsPerBody = 9;

        /// <summary>
        /// Select up to 4 evidence cameras around the first sustained divergence.
        /// <paramref name="bodySizesByIndex"/> holds each body's full local size (e.g.
        /// <c>SimulationBodyDefinition.Size</c>), aligned to <c>perturbedRun.StableBodyIds</c>
        /// index order; half of it is used as the world-axis-aligned half-extent for every body
        /// at every queried frame (a documented simplification — bodies are not treated as
        /// rotation-aware OBBs, per docs/PLAN.md Block 2.1 "reproducibility" note).
        /// </summary>
        public static EvidenceCameraPlanResult Plan(
            RunResult baselineRun,
            RunResult perturbedRun,
            DivergenceResult divergence,
            Vector3[] bodySizesByIndex,
            DivergenceSettings settings)
        {
            var validationError = Validate(baselineRun, perturbedRun, divergence, bodySizesByIndex, settings);
            if (validationError != null)
            {
                return EvidenceCameraPlanResult.Failure(validationError);
            }

            var bodyCount = perturbedRun.BodyCount;
            var frame = divergence.FirstDivergenceFrame;
            var stepCount = perturbedRun.StepCount;

            var allBodyBounds = new EvidenceBounds[bodyCount];
            for (var bodyIndex = 0; bodyIndex < bodyCount; bodyIndex++)
            {
                allBodyBounds[bodyIndex] = BoundsAtFrame(
                    perturbedRun.StateFrames, frame, bodyIndex, stepCount, bodyCount, bodySizesByIndex[bodyIndex]);
            }

            var affectedIndices = ResolveAffectedIndices(perturbedRun.StableBodyIds, divergence.AffectedBodyIds);
            if (affectedIndices.Length == 0)
            {
                return EvidenceCameraPlanResult.Failure(
                    "None of DivergenceResult.AffectedBodyIds matched perturbedRun.StableBodyIds.");
            }

            var eventBounds = EvidenceBounds.Encapsulate(
                allBodyBounds[affectedIndices[0]], allBodyBounds[affectedIndices[0]]);
            for (var i = 1; i < affectedIndices.Length; i++)
            {
                eventBounds = EvidenceBounds.Encapsulate(eventBounds, allBodyBounds[affectedIndices[i]]);
            }

            var candidateCount = settings.EvidenceCandidateCount;
            var sphereRadius = Mathf.Max(eventBounds.BoundingRadius, 1e-4f) *
                               settings.EvidenceEventBoundsRadiusMultiplier;
            var aspect = (float)settings.EvidenceRenderWidth / settings.EvidenceRenderHeight;
            var averageVelocityDirection = AverageVelocityDirection(
                perturbedRun.StateFrames, frame, affectedIndices, stepCount, bodyCount);

            var candidates = new EvidenceCameraCandidateResult[candidateCount];
            var samplePoints = new Vector3[SamplePointsPerBody];

            var bestIndex = -1;
            var bestScore = 0f;

            for (var i = 0; i < candidateCount; i++)
            {
                var direction = EvidenceCameraMath.FibonacciSpherePoint(i, candidateCount);
                var position = eventBounds.Center + (direction * sphereRadius);

                if (position.y <= 0f)
                {
                    // TowerScene bodies sit at y = 0.5 + level (TowerProbeRequestFactory); the
                    // ground plane is y = 0, so a candidate at or below it is discarded before
                    // scoring (docs/PLAN.md Block 2.1: "Candidates below the ground plane are
                    // discarded before scoring").
                    candidates[i] = new EvidenceCameraCandidateResult(i, position, true, 0f, 0f, 0f, 0f, 0f);
                    continue;
                }

                var worldToCamera = EvidenceCameraMath.WorldToCameraMatrix(position, eventBounds.Center);
                var projection = EvidenceCameraMath.ProjectionMatrix(
                    settings.EvidenceCameraVerticalFovDegrees, aspect,
                    settings.EvidenceCameraNearClip, settings.EvidenceCameraFarClip);
                var viewProjection = projection * worldToCamera;
                var frustumPlanes = GeometryUtility.CalculateFrustumPlanes(viewProjection);

                var inFrustumScore = 0f;
                var visibilityScore = 0f;
                var separationScore = 0f;
                var centralityPenalty = 0f;

                for (var a = 0; a < affectedIndices.Length; a++)
                {
                    var bodyIndex = affectedIndices[a];
                    var bodyBounds = allBodyBounds[bodyIndex];

                    var unityBounds = new Bounds(bodyBounds.Center, bodyBounds.HalfExtents * 2f);
                    if (GeometryUtility.TestPlanesAABB(frustumPlanes, unityBounds))
                    {
                        inFrustumScore += 1f;
                    }

                    bodyBounds.CopySamplePointsTo(samplePoints);
                    var hits = 0;
                    for (var s = 0; s < SamplePointsPerBody; s++)
                    {
                        var toSample = samplePoints[s] - position;
                        var distance = toSample.magnitude;
                        if (distance < 1e-6f)
                        {
                            continue;
                        }

                        var rayDir = toSample / distance;
                        for (var other = 0; other < bodyCount; other++)
                        {
                            if (other == bodyIndex)
                            {
                                continue;
                            }

                            if (allBodyBounds[other].BlocksRay(position, rayDir, distance))
                            {
                                hits++;
                                break;
                            }
                        }
                    }

                    visibilityScore += 1f - (hits / (float)SamplePointsPerBody);

                    var perturbedPos = PositionAtFrame(
                        perturbedRun.StateFrames, frame, bodyIndex, stepCount, bodyCount);
                    var baselinePos = PositionAtFrame(
                        baselineRun.StateFrames, frame, bodyIndex, stepCount, bodyCount);

                    var perturbedViewport = EvidenceCameraMath.WorldToViewport(
                        viewProjection, perturbedPos, out var perturbedInFront);
                    var baselineViewport = EvidenceCameraMath.WorldToViewport(
                        viewProjection, baselinePos, out var baselineInFront);

                    // A point behind the near plane produces inverted/garbage NDC — never fold
                    // it into separation or centrality (0 contribution instead of a false
                    // number). Such a body should also fail TestPlanesAABB above in the normal
                    // case, but that is a separate, coarser test; guard this one directly too.
                    if (perturbedInFront && baselineInFront)
                    {
                        var pixelDelta = new Vector2(
                            (perturbedViewport.x - baselineViewport.x) * settings.EvidenceRenderWidth,
                            (perturbedViewport.y - baselineViewport.y) * settings.EvidenceRenderHeight);
                        separationScore += pixelDelta.magnitude / settings.ScreenSpaceSeparationNormalizer;

                        var centralityOffset = new Vector2(perturbedViewport.x - 0.5f, perturbedViewport.y - 0.5f);
                        centralityPenalty += centralityOffset.magnitude;
                    }
                }

                var totalScore = inFrustumScore + visibilityScore + separationScore -
                                  (settings.WeightEvidenceCentrality * centralityPenalty);

                candidates[i] = new EvidenceCameraCandidateResult(
                    i, position, false, inFrustumScore, visibilityScore, separationScore,
                    centralityPenalty, totalScore);

                // Strict greater-than: ties keep the lower index (ascending tie-break, never a
                // float equality compare), matching every other BugCam ranking rule.
                if (bestIndex < 0 || totalScore > bestScore)
                {
                    bestIndex = i;
                    bestScore = totalScore;
                }
            }

            if (bestIndex < 0)
            {
                return EvidenceCameraPlanResult.Failure(
                    "All candidates were rejected by the ground-plane cull.");
            }

            var bestScorePerBody = bestScore / affectedIndices.Length;
            var verdict = bestScorePerBody < settings.MinEvidenceCoverageScore
                ? VerdictLow
                : VerdictOk;

            var winners = SelectWinners(
                candidates, bestIndex, eventBounds, averageVelocityDirection, settings);

            return EvidenceCameraPlanResult.Success(
                AlgorithmVersion,
                candidateCount,
                eventBounds.Center,
                sphereRadius,
                frame,
                divergence.AffectedBodyIds,
                candidates,
                winners,
                verdict == VerdictOk,
                bestScorePerBody,
                verdict);
        }

        private static EvidenceCameraWinner[] SelectWinners(
            EvidenceCameraCandidateResult[] candidates,
            int camera1Index,
            EvidenceBounds eventBounds,
            Vector3 averageVelocityDirection,
            DivergenceSettings settings)
        {
            var survivorCount = 0;
            for (var i = 0; i < candidates.Length; i++)
            {
                if (!candidates[i].RejectedBelowGroundPlane)
                {
                    survivorCount++;
                }
            }

            // "filter to the top 25% by score FIRST" — the constraint, applied before optimizing.
            var topCount = Mathf.Max(1, Mathf.CeilToInt(survivorCount * settings.EvidenceTopScoreFraction));
            var order = SortIndicesByScoreDescending(candidates);

            var camera1Direction = (candidates[camera1Index].Position - eventBounds.Center).normalized;

            var winners = new EvidenceCameraWinner[Mathf.Min(4, survivorCount)];
            // Camera 1 is chosen by raw TotalScore, not by the (a)/(b)/(c) ranking scheme used
            // for cameras 2-4, so its ranking-term fields are not applicable here. Left as inert
            // zeros in-memory; EvidenceCameraPlanWriter writes them as honest null for slot 1
            // rather than a fabricated zero (matches the repo's null + has* convention).
            winners[0] = new EvidenceCameraWinner(1, camera1Index, 0f, 0f, 0f, 0f);

            // order[0] is always camera1Index (it is the max-TotalScore survivor by construction,
            // with the same ascending-index tie-break used to pick bestIndex above), so the top-
            // 25% slice order[0..topCount) minus camera1 is exactly order[1..topCount) — no
            // backfill from beyond the cutoff.
            var sliceEnd = Mathf.Min(topCount, order.Length);
            var poolSize = Mathf.Max(0, sliceEnd - 1);
            var rankedPool = new int[poolSize];
            for (var i = 0; i < poolSize; i++)
            {
                rankedPool[i] = order[i + 1];
            }

            var scored = new (int index, float orth, float contact, float traj, float rank)[poolSize];
            for (var i = 0; i < poolSize; i++)
            {
                var candidateIndex = rankedPool[i];
                var candidate = candidates[candidateIndex];
                var direction = (candidate.Position - eventBounds.Center).normalized;

                var orthogonality = 1f - Mathf.Abs(Vector3.Dot(direction, camera1Direction));
                var distanceToCenter = Vector3.Distance(candidate.Position, eventBounds.Center);
                var contactProximity = 1f / (1f + distanceToCenter);
                var forward = (eventBounds.Center - candidate.Position).normalized;
                var trajectoryAlignment = averageVelocityDirection == Vector3.zero
                    ? 0f
                    : 1f - Mathf.Abs(Vector3.Dot(forward, averageVelocityDirection));

                var rankScore =
                    (settings.WeightCameraOrthogonality * orthogonality) +
                    (settings.WeightContactProximity * contactProximity) +
                    (settings.WeightTrajectoryAlignment * trajectoryAlignment);

                scored[i] = (candidateIndex, orthogonality, contactProximity, trajectoryAlignment, rankScore);
            }

            // Selection sort by rank descending, index ascending on ties — small N (<= candidate
            // count * EvidenceTopScoreFraction), determinism matters more than asymptotic cost.
            for (var i = 0; i < scored.Length; i++)
            {
                var bestJ = i;
                for (var j = i + 1; j < scored.Length; j++)
                {
                    if (scored[j].rank > scored[bestJ].rank ||
                        (scored[j].rank == scored[bestJ].rank && scored[j].index < scored[bestJ].index))
                    {
                        bestJ = j;
                    }
                }

                (scored[i], scored[bestJ]) = (scored[bestJ], scored[i]);
            }

            var slotsToFill = Mathf.Min(winners.Length - 1, scored.Length);
            for (var slot = 0; slot < slotsToFill; slot++)
            {
                var s = scored[slot];
                winners[slot + 1] = new EvidenceCameraWinner(
                    slot + 2, s.index, s.orth, s.contact, s.traj, s.rank);
            }

            if (slotsToFill < winners.Length - 1)
            {
                Array.Resize(ref winners, slotsToFill + 1);
            }

            return winners;
        }

        private static int[] SortIndicesByScoreDescending(EvidenceCameraCandidateResult[] candidates)
        {
            var survivors = new System.Collections.Generic.List<int>(candidates.Length);
            for (var i = 0; i < candidates.Length; i++)
            {
                if (!candidates[i].RejectedBelowGroundPlane)
                {
                    survivors.Add(i);
                }
            }

            survivors.Sort((a, b) =>
            {
                var scoreCompare = candidates[b].TotalScore.CompareTo(candidates[a].TotalScore);
                return scoreCompare != 0 ? scoreCompare : a.CompareTo(b);
            });
            return survivors.ToArray();
        }

        private static Vector3 AverageVelocityDirection(
            float[] frames, int frame, int[] affectedIndices, int stepCount, int bodyCount)
        {
            var sum = Vector3.zero;
            for (var i = 0; i < affectedIndices.Length; i++)
            {
                var offset = StateRecorder.IndexOf(0, frame, affectedIndices[i], stepCount, bodyCount);
                sum += new Vector3(frames[offset + 7], frames[offset + 8], frames[offset + 9]);
            }

            return sum.sqrMagnitude < 1e-10f ? Vector3.zero : sum.normalized;
        }

        private static Vector3 PositionAtFrame(
            float[] frames, int frame, int bodyIndex, int stepCount, int bodyCount)
        {
            var offset = StateRecorder.IndexOf(0, frame, bodyIndex, stepCount, bodyCount);
            return new Vector3(frames[offset], frames[offset + 1], frames[offset + 2]);
        }

        private static EvidenceBounds BoundsAtFrame(
            float[] frames, int frame, int bodyIndex, int stepCount, int bodyCount, Vector3 size)
        {
            var position = PositionAtFrame(frames, frame, bodyIndex, stepCount, bodyCount);
            return new EvidenceBounds(position, size * 0.5f);
        }

        private static int[] ResolveAffectedIndices(int[] stableBodyIds, int[] affectedBodyIds)
        {
            var result = new int[affectedBodyIds.Length];
            var count = 0;
            for (var a = 0; a < affectedBodyIds.Length; a++)
            {
                for (var bodyIndex = 0; bodyIndex < stableBodyIds.Length; bodyIndex++)
                {
                    if (stableBodyIds[bodyIndex] == affectedBodyIds[a])
                    {
                        result[count++] = bodyIndex;
                        break;
                    }
                }
            }

            if (count != result.Length)
            {
                Array.Resize(ref result, count);
            }

            return result;
        }

        private static string Validate(
            RunResult baselineRun,
            RunResult perturbedRun,
            DivergenceResult divergence,
            Vector3[] bodySizesByIndex,
            DivergenceSettings settings)
        {
            if (settings == null)
            {
                return "DivergenceSettings is required.";
            }

            // The 9-point sample set (AABB centre + 8 corners) is a fixed geometric structure,
            // not a tunable count, so it is a private const rather than read from settings each
            // call — but settings.EvidenceOcclusionRays exists precisely to name that number in
            // the DivergenceSettings contract (PLAN Block 2.1). Fail closed if the two diverge
            // instead of silently ignoring the settings value.
            if (settings.EvidenceOcclusionRays != SamplePointsPerBody)
            {
                return "DivergenceSettings.EvidenceOcclusionRays must equal " +
                       SamplePointsPerBody + " (AABB centre + 8 corners is a fixed structure).";
            }

            if (!baselineRun.Succeeded)
            {
                return "Baseline run did not succeed: " + baselineRun.ErrorReason;
            }

            if (!perturbedRun.Succeeded)
            {
                return "Perturbed run did not succeed: " + perturbedRun.ErrorReason;
            }

            if (!divergence.Succeeded)
            {
                return "DivergenceResult did not succeed: " + divergence.ErrorReason;
            }

            if (!divergence.HasSignificantDivergence)
            {
                return "No significant divergence — there is no event to frame cameras around.";
            }

            if (baselineRun.StepCount != perturbedRun.StepCount ||
                baselineRun.BodyCount != perturbedRun.BodyCount)
            {
                return "Baseline and perturbed runs must record the same step and body counts.";
            }

            for (var i = 0; i < baselineRun.BodyCount; i++)
            {
                if (baselineRun.StableBodyIds[i] != perturbedRun.StableBodyIds[i])
                {
                    return "Baseline and perturbed runs must share identical stable body ids.";
                }
            }

            if (bodySizesByIndex == null || bodySizesByIndex.Length != perturbedRun.BodyCount)
            {
                return "bodySizesByIndex must hold exactly one entry per tracked body.";
            }

            if (divergence.AffectedBodyIds == null || divergence.AffectedBodyIds.Length == 0)
            {
                return "DivergenceResult has no affected bodies.";
            }

            if (divergence.FirstDivergenceFrame < 0 ||
                divergence.FirstDivergenceFrame >= perturbedRun.StepCount)
            {
                return "FirstDivergenceFrame is out of range for the recorded step count.";
            }

            if (settings.EvidenceCandidateCount <= 0)
            {
                return "EvidenceCandidateCount must be positive.";
            }

            return null;
        }
    }
}
