using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BugCam.Tests
{
    /// <summary>
    /// Synthetic Block 1.4 EpsilonSearch cases. No PhysicsScene / Play Mode required.
    /// Uses reflection so EditMode tests keep zero asmdef references to BugCam.Core.
    /// </summary>
    public sealed class EpsilonSearchTests
    {
        private const int Stride = 14;

        [Test]
        public void SettingsValidationRejectsInvertedRange()
        {
            var settings = MakeSearchSettings(1e-2f, 2f, 1e-5f, 7, 12, DefaultFan());
            Assert.That(Validate(settings), Does.Contain("EpsilonCeiling"));
        }

        [Test]
        public void SettingsValidationRejectsBadGrowthAndFan()
        {
            var growth = MakeSearchSettings(1e-5f, 1f, 1e-2f, 7, 12, DefaultFan());
            Assert.That(Validate(growth), Does.Contain("EpsilonGrowthFactor"));

            var fan = MakeSearchSettings(1e-5f, 2f, 1e-2f, 7, 12, new[] { 0.8f, -1f });
            Assert.That(Validate(fan), Does.Contain("FanMultipliers"));
        }

        [Test]
        public void DefaultSettingsValidate()
        {
            var settingsType = Type.GetType("BugCam.Core.EpsilonSearchSettings, BugCam.Core");
            var defaults = settingsType.GetProperty("Default").GetValue(null);
            Assert.That(Validate(defaults), Is.Empty);
            Assert.That(Prop<float>(defaults, "EpsilonStartMetres"), Is.EqualTo(1e-5f));
            Assert.That(Prop<float>(defaults, "EpsilonCeilingMetres"), Is.EqualTo(1e-2f));
            Assert.That(Prop<int>(defaults, "LadderPointCount"), Is.EqualTo(12));
            Assert.That(
                Prop<float[]>(defaults, "FanMultipliers"),
                Is.EqualTo(new[] { 0.8f, 0.9f, 1f, 1.1f, 1.2f }));
            Assert.That(
                Prop<float>(defaults, "CharacterizationCeilingMetres"),
                Is.EqualTo(1.2e-2f).Within(1e-9f));
        }

        [Test]
        public void SettingsValidationAcceptsExactLadderTwelveAndCanonicalFan()
        {
            var settings = MakeSearchSettings(1e-5f, 2f, 1e-2f, 7, 12, DefaultFan());
            Assert.That(Validate(settings), Is.Empty);
        }

        [Test]
        public void SettingsValidationRejectsNonExactLadderCounts()
        {
            foreach (var ladder in new[] { 0, 1, 11, 13, int.MaxValue })
            {
                var settings = MakeSearchSettings(1e-5f, 2f, 1e-2f, 7, ladder, DefaultFan());
                Assert.That(
                    Validate(settings),
                    Does.Contain("LadderPointCount"),
                    "ladder=" + ladder);
            }
        }

        [Test]
        public void SettingsValidationRejectsNonCanonicalFanArrays()
        {
            Assert.That(
                Validate(MakeSearchSettings(1e-5f, 2f, 1e-2f, 7, 12, null)),
                Does.Contain("FanMultipliers"));
            Assert.That(
                Validate(MakeSearchSettings(1e-5f, 2f, 1e-2f, 7, 12, Array.Empty<float>())),
                Does.Contain("FanMultipliers"));
            Assert.That(
                Validate(MakeSearchSettings(1e-5f, 2f, 1e-2f, 7, 12, new[] { 0.8f, 0.9f, 1f, 1.1f })),
                Does.Contain("FanMultipliers"));
            Assert.That(
                Validate(MakeSearchSettings(
                    1e-5f, 2f, 1e-2f, 7, 12, new[] { 0.8f, 0.9f, 1f, 1.1f, 1.2f, 1.3f })),
                Does.Contain("FanMultipliers"));
            Assert.That(
                Validate(MakeSearchSettings(
                    1e-5f, 2f, 1e-2f, 7, 12, new[] { 1.2f, 1.1f, 1f, 0.9f, 0.8f })),
                Does.Contain("FanMultipliers"));
            Assert.That(
                Validate(MakeSearchSettings(
                    1e-5f, 2f, 1e-2f, 7, 12, new[] { 0.8f, 0.9f, 1f, 1.1f, 1.1f })),
                Does.Contain("FanMultipliers"));
            Assert.That(
                Validate(MakeSearchSettings(
                    1e-5f, 2f, 1e-2f, 7, 12, new[] { 0.8f, 0.9f, 1f, 1.1f, 1.25f })),
                Does.Contain("FanMultipliers"));
            Assert.That(
                Validate(MakeSearchSettings(
                    1e-5f, 2f, 1e-2f, 7, 12, new[] { 0.8f, 0.9f, 1f, 1.1f, 0f })),
                Does.Contain("FanMultipliers"));
            Assert.That(
                Validate(MakeSearchSettings(
                    1e-5f, 2f, 1e-2f, 7, 12, new[] { 0.8f, 0.9f, 1f, 1.1f, -1.2f })),
                Does.Contain("FanMultipliers"));
            Assert.That(
                Validate(MakeSearchSettings(
                    1e-5f, 2f, 1e-2f, 7, 12, new[] { 0.8f, 0.9f, 1f, 1.1f, float.NaN })),
                Does.Contain("FanMultipliers"));
            Assert.That(
                Validate(MakeSearchSettings(
                    1e-5f,
                    2f,
                    1e-2f,
                    7,
                    12,
                    new[] { 0.8f, 0.9f, 1f, 1.1f, float.PositiveInfinity })),
                Does.Contain("FanMultipliers"));
        }

        [Test]
        public void DivergenceAtEveryTestedEpsilonReportsUnbracketedOutcomeWithoutThresholdEstimate()
        {
            var search = CreateSearch();
            var result = Drive(search, (phase, epsilon, axis, isBaseline) =>
            {
                if (isBaseline)
                {
                    return BaselineOutcome();
                }

                // Every perturbed sample diverges — no stable lower bound exists.
                return NeedsFrames(phase) ? Framed(true, epsilon, axis) : Compact(true);
            });

            Assert.That(Prop<bool>(result, "Succeeded"), Is.True, Prop<string>(result, "ErrorReason"));
            Assert.That(Prop<string>(result, "Verdict"), Is.EqualTo("DIVERGENT AT SEARCH FLOOR"));
            Assert.That(Prop<bool>(result, "HasThresholdEstimate"), Is.False);
            Assert.That(Prop<bool>(result, "HasLargestStableEpsilon"), Is.False);
            Assert.That(Prop<bool>(result, "HasSmallestDivergentEpsilon"), Is.True);
            Assert.That(Prop<float>(result, "ThresholdEstimateMetres"), Is.EqualTo(0f));
            Assert.That(Prop<float>(result, "FinalBracketWidthMetres"), Is.EqualTo(0f));
            Assert.That(Prop<bool>(result, "ReferenceIsExactThreshold"), Is.False);
            Assert.That(Prop<string>(result, "ErrorReason"), Is.Empty);
            Assert.That(Arr(result, "LadderSummaries").Length, Is.EqualTo(12));
            Assert.That(Arr(result, "ExponentialSummaries").Length, Is.GreaterThan(0));
            Assert.That(Arr(result, "BisectionSummaries"), Is.Empty);
            Assert.That(Arr(result, "FanSummaries").Length, Is.EqualTo(15));
            Assert.That(Arr(result, "FanRuns").Length, Is.EqualTo(15));
            Assert.That(
                Prop<float>(result, "ReferenceEpsilonMetres"),
                Is.EqualTo(Prop<float>(result, "SmallestDivergentEpsilonMetres")));
        }

        [Test]
        public void FanRequestGenerationIsExactlyFifteenUniqueAxisMultiplierPairs()
        {
            var search = CreateSearch();
            var fanKeys = new List<string>();
            var result = Drive(search, (phase, epsilon, axis, isBaseline) =>
            {
                if (isBaseline)
                {
                    return BaselineOutcome();
                }

                if (phaseName(phase) == "Fan")
                {
                    fanKeys.Add(AxisLabel(axis) + ":" + epsilon.ToString("R"));
                }

                var diverged = epsilon >= 3e-4f;
                return NeedsFrames(phase) ? Framed(diverged, epsilon, axis) : Compact(diverged);
            });

            Assert.That(fanKeys.Count, Is.EqualTo(15));
            CollectionAssert.AllItemsAreUnique(fanKeys);

            var reference = Prop<float>(result, "ReferenceEpsilonMetres");
            var expected = new List<string>();
            foreach (var m in new[] { 0.8f, 0.9f, 1f, 1.1f, 1.2f })
            {
                var eps = reference * m;
                expected.Add("X:" + eps.ToString("R"));
                expected.Add("Y:" + eps.ToString("R"));
                expected.Add("Z:" + eps.ToString("R"));
            }

            Assert.That(fanKeys, Is.EqualTo(expected));
        }

        [Test]
        public void RetainedFullRunsEqualBaselinePlusFifteenFanRuns()
        {
            var search = CreateSearch();
            var result = Drive(search, (phase, epsilon, axis, isBaseline) =>
            {
                if (isBaseline)
                {
                    return BaselineOutcome();
                }

                var diverged = epsilon >= 3e-4f;
                return NeedsFrames(phase) ? Framed(diverged, epsilon, axis) : Compact(diverged);
            });

            Assert.That(Prop<bool>(Prop<object>(result, "BaselineRun"), "Succeeded"), Is.True);
            Assert.That(Arr(result, "FanRuns").Length, Is.EqualTo(15));
            Assert.That(Arr(result, "FanSummaries").Length, Is.EqualTo(15));
        }

        [Test]
        public void MutatingCallerFanArrayAfterSettingsConstructionDoesNotAlterFanGeneration()
        {
            var callerFan = new[] { 0.8f, 0.9f, 1f, 1.1f, 1.2f };
            var settings = MakeSearchSettings(1e-5f, 2f, 1e-2f, 7, 12, callerFan);
            callerFan[4] = 9f;
            Assert.That(Validate(settings), Is.Empty);
            Assert.That(Prop<float[]>(settings, "FanMultipliers")[4], Is.EqualTo(1.2f));
            Assert.That(
                Prop<float>(settings, "CharacterizationCeilingMetres"),
                Is.EqualTo(1.2e-2f).Within(1e-9f));

            var exposed = Prop<float[]>(settings, "FanMultipliers");
            exposed[4] = 9f;
            Assert.That(
                Prop<float>(settings, "CharacterizationCeilingMetres"),
                Is.EqualTo(1.2e-2f).Within(1e-9f));
            Assert.That(Prop<float[]>(settings, "FanMultipliers")[4], Is.EqualTo(1.2f));

            var search = CreateSearch(settings);
            var fanKeys = new List<string>();
            var result = Drive(search, (phase, epsilon, axis, isBaseline) =>
            {
                if (isBaseline)
                {
                    return BaselineOutcome();
                }

                if (phaseName(phase) == "Fan")
                {
                    fanKeys.Add(AxisLabel(axis) + ":" + epsilon.ToString("R"));
                }

                var diverged = epsilon >= 3e-4f;
                return NeedsFrames(phase) ? Framed(diverged, epsilon, axis) : Compact(diverged);
            });

            Assert.That(Prop<bool>(result, "Succeeded"), Is.True, Prop<string>(result, "ErrorReason"));
            Assert.That(fanKeys.Count, Is.EqualTo(15));
            var reference = Prop<float>(result, "ReferenceEpsilonMetres");
            Assert.That(fanKeys[12], Is.EqualTo("X:" + (reference * 1.2f).ToString("R")));
            Assert.That(fanKeys, Has.None.Contain(":" + (reference * 9f).ToString("R")));
        }

        [Test]
        public void CustomStartDivergentStillUsesLadderStableBoundForThresholdBracket()
        {
            var strategyType = Type.GetType("BugCam.Core.EpsilonSearchStrategy, BugCam.Core");
            var customStrategy = Enum.ToObject(strategyType, 1); // AscendFromCustomStart
            var search = CreateSearch(
                settings: null,
                targetBodyId: 49,
                axis: Vector3.right,
                strategy: customStrategy,
                customStart: 5e-4f);

            var result = Drive(search, (phase, epsilon, axis, isBaseline) =>
            {
                if (isBaseline)
                {
                    return BaselineOutcome();
                }

                // Ladder: low epsilons stable, high divergent. Custom exponential start (5e-4)
                // is already divergent — must still reconcile ladder stables into a bracket.
                var diverged = epsilon >= 3e-4f;
                return NeedsFrames(phase) ? Framed(diverged, epsilon, axis) : Compact(diverged);
            });

            Assert.That(Prop<bool>(result, "Succeeded"), Is.True, Prop<string>(result, "ErrorReason"));
            Assert.That(Prop<string>(result, "Verdict"), Is.EqualTo("THRESHOLD BRACKET FOUND"));
            Assert.That(Prop<bool>(result, "HasThresholdEstimate"), Is.True);
            Assert.That(Prop<bool>(result, "HasLargestStableEpsilon"), Is.True);
            Assert.That(Prop<bool>(result, "HasSmallestDivergentEpsilon"), Is.True);
            Assert.That(
                Prop<float>(result, "LargestStableEpsilonMetres"),
                Is.LessThan(Prop<float>(result, "SmallestDivergentEpsilonMetres")));
            Assert.That(Prop<float>(result, "FinalBracketWidthMetres"), Is.GreaterThanOrEqualTo(0f));
        }

        [Test]
        public void NonMonotonicBracketWidthIsUndefinedSafeZeroWithoutThresholdEstimate()
        {
            var search = CreateSearch();
            var result = Drive(search, (phase, epsilon, axis, isBaseline) =>
            {
                if (isBaseline)
                {
                    return BaselineOutcome();
                }

                bool diverged;
                if (phaseName(phase) == "Ladder")
                {
                    diverged = epsilon >= 1e-4f && epsilon <= 1e-3f;
                }
                else
                {
                    diverged = epsilon >= 1e-4f;
                }

                return NeedsFrames(phase) ? Framed(diverged, epsilon, axis) : Compact(diverged);
            });

            Assert.That(Prop<string>(result, "Verdict"), Is.EqualTo("NON-MONOTONIC WITHIN TESTED RANGE"));
            Assert.That(Prop<bool>(result, "HasThresholdEstimate"), Is.False);
            Assert.That(Prop<float>(result, "FinalBracketWidthMetres"), Is.EqualTo(0f));
        }

        [Test]
        public void StableRangeReturnsStableVerdictWithoutFanOrThreshold()
        {
            var search = CreateSearch();
            var result = Drive(search, (phase, epsilon, axis, isBaseline) =>
            {
                if (isBaseline)
                {
                    return BaselineOutcome();
                }

                return Compact(diverged: false);
            });

            Assert.That(Prop<bool>(result, "Succeeded"), Is.True, Prop<string>(result, "ErrorReason"));
            Assert.That(Prop<string>(result, "Verdict"), Is.EqualTo("STABLE WITHIN TESTED RANGE"));
            Assert.That(Prop<bool>(result, "HasThresholdEstimate"), Is.False);
            Assert.That(Arr(result, "FanSummaries"), Is.Empty);
            Assert.That(Arr(result, "FanRuns"), Is.Empty);
            Assert.That(Arr(result, "LadderSummaries").Length, Is.EqualTo(12));
            Assert.That(Arr(result, "ExponentialSummaries"), Is.Empty);
            Assert.That(Arr(result, "BisectionSummaries"), Is.Empty);
        }

        [Test]
        public void MonotonicBracketReportsThresholdSemanticsWithoutClaimingExact()
        {
            // Ladder: first 6 stable, last 6 divergent (monotonic).
            var search = CreateSearch();
            var result = Drive(search, (phase, epsilon, axis, isBaseline) =>
            {
                if (isBaseline)
                {
                    return BaselineOutcome();
                }

                var diverged = epsilon >= 3e-4f;
                return NeedsFrames(phase) ? Framed(diverged, epsilon, axis) : Compact(diverged);
            });

            Assert.That(Prop<bool>(result, "Succeeded"), Is.True, Prop<string>(result, "ErrorReason"));
            Assert.That(Prop<string>(result, "Verdict"), Is.EqualTo("THRESHOLD BRACKET FOUND"));
            Assert.That(Prop<bool>(result, "HasThresholdEstimate"), Is.True);
            Assert.That(Prop<bool>(result, "ReferenceIsExactThreshold"), Is.False);
            Assert.That(Prop<bool>(result, "HasLargestStableEpsilon"), Is.True);
            Assert.That(Prop<bool>(result, "HasSmallestDivergentEpsilon"), Is.True);

            var largestStable = Prop<float>(result, "LargestStableEpsilonMetres");
            var smallestDivergent = Prop<float>(result, "SmallestDivergentEpsilonMetres");
            var estimate = Prop<float>(result, "ThresholdEstimateMetres");
            var width = Prop<float>(result, "FinalBracketWidthMetres");

            Assert.That(estimate, Is.EqualTo(smallestDivergent));
            Assert.That(estimate, Is.EqualTo(Prop<float>(result, "ReferenceEpsilonMetres")));
            Assert.That(float.IsNaN(largestStable), Is.False);
            Assert.That(float.IsNaN(smallestDivergent), Is.False);
            Assert.That(smallestDivergent, Is.GreaterThan(largestStable));
            Assert.That(width, Is.GreaterThanOrEqualTo(0f));
            Assert.That(width, Is.EqualTo(smallestDivergent - largestStable).Within(1e-9f));
            Assert.That(Arr(result, "FanSummaries").Length, Is.EqualTo(15));
            Assert.That(Arr(result, "FanRuns").Length, Is.EqualTo(15));
            Assert.That(Arr(result, "LadderSummaries").Length, Is.EqualTo(12));
            Assert.That(Arr(result, "BisectionSummaries").Length, Is.GreaterThan(0));
        }

        [Test]
        public void NonMonotonicLadderSkipsBisectionPreservesLadderAndFansAroundReference()
        {
            // Stable, divergent, then stable again at higher epsilon.
            var search = CreateSearch();
            var result = Drive(search, (phase, epsilon, axis, isBaseline) =>
            {
                if (isBaseline)
                {
                    return BaselineOutcome();
                }

                bool diverged;
                if (phaseName(phase) == "Ladder")
                {
                    // Mid ladder points diverge; high end returns to stable → non-monotonic.
                    diverged = epsilon >= 1e-4f && epsilon <= 1e-3f;
                }
                else
                {
                    diverged = epsilon >= 1e-4f;
                }

                return NeedsFrames(phase) ? Framed(diverged, epsilon, axis) : Compact(diverged);
            });

            Assert.That(Prop<bool>(result, "Succeeded"), Is.True, Prop<string>(result, "ErrorReason"));
            Assert.That(
                Prop<string>(result, "Verdict"),
                Is.EqualTo("NON-MONOTONIC WITHIN TESTED RANGE"));
            Assert.That(Prop<bool>(result, "HasThresholdEstimate"), Is.False);
            Assert.That(Prop<bool>(result, "ReferenceIsExactThreshold"), Is.False);
            Assert.That(Arr(result, "LadderSummaries").Length, Is.EqualTo(12));
            Assert.That(Arr(result, "ExponentialSummaries"), Is.Empty);
            Assert.That(Arr(result, "BisectionSummaries"), Is.Empty);
            Assert.That(Arr(result, "FanSummaries").Length, Is.EqualTo(15));

            var reference = Prop<float>(result, "ReferenceEpsilonMetres");
            Assert.That(reference, Is.GreaterThan(0f));
            // Smallest observed divergent ladder sample becomes the reference epsilon.
            var ladder = Arr(result, "LadderSummaries");
            var minDivergent = float.MaxValue;
            for (var i = 0; i < ladder.Length; i++)
            {
                var sample = ladder.GetValue(i);
                if (Prop<bool>(sample, "HasSignificantDivergence"))
                {
                    minDivergent = Math.Min(minDivergent, Prop<float>(sample, "EpsilonMetres"));
                }
            }

            Assert.That(reference, Is.EqualTo(minDivergent));
        }

        [Test]
        public void DeterministicRequestOrderIsStableAcrossRepeats()
        {
            var first = CaptureRequestOrder();
            var second = CaptureRequestOrder();
            Assert.That(second, Is.EqualTo(first));
            Assert.That(first.Count, Is.GreaterThan(12));
        }

        [Test]
        public void CachingServesExponentialFromLadderHits()
        {
            var search = CreateSearch();
            var physical = 0;
            var result = Drive(search, (phase, epsilon, axis, isBaseline) =>
            {
                physical++;
                if (isBaseline)
                {
                    return BaselineOutcome();
                }

                var diverged = epsilon >= 3e-4f;
                return NeedsFrames(phase) ? Framed(diverged, epsilon, axis) : Compact(diverged);
            });

            Assert.That(Prop<bool>(result, "Succeeded"), Is.True);
            Assert.That(Prop<int>(result, "CacheHitCount"), Is.GreaterThan(0));
            Assert.That(Prop<int>(result, "PhysicalProbeCount"), Is.EqualTo(physical));
            Assert.That(physical, Is.LessThan(1 + 12 + 10 + 7 + 15));
        }

        [Test]
        public void ExactLadderAndFanCounts()
        {
            var search = CreateSearch();
            var result = Drive(search, (phase, epsilon, axis, isBaseline) =>
            {
                if (isBaseline)
                {
                    return BaselineOutcome();
                }

                var diverged = epsilon >= 5e-4f;
                return NeedsFrames(phase) ? Framed(diverged, epsilon, axis) : Compact(diverged);
            });

            Assert.That(Arr(result, "LadderSummaries").Length, Is.EqualTo(12));
            Assert.That(Arr(result, "FanSummaries").Length, Is.EqualTo(15));
            Assert.That(Arr(result, "FanRuns").Length, Is.EqualTo(15));
        }

        [Test]
        public void NoFanClampAndOutsideSearchRangeMarking()
        {
            // Force reference near ceiling so 1.2× exceeds search ceiling.
            var search = CreateSearch();
            var result = Drive(search, (phase, epsilon, axis, isBaseline) =>
            {
                if (isBaseline)
                {
                    return BaselineOutcome();
                }

                // Only the search ceiling diverges → reference = 10 mm so 1.2× exceeds ceiling.
                var diverged = epsilon >= 1e-2f - 1e-12f;
                return NeedsFrames(phase) ? Framed(diverged, epsilon, axis) : Compact(diverged);
            });

            Assert.That(Prop<bool>(result, "Succeeded"), Is.True, Prop<string>(result, "ErrorReason"));
            var fan = Arr(result, "FanSummaries");
            Assert.That(fan.Length, Is.EqualTo(15));

            var ceiling = Prop<float>(result, "SearchRangeCeilingMetres");
            var charCeiling = Prop<float>(result, "CharacterizationCeilingMetres");
            Assert.That(charCeiling, Is.EqualTo(ceiling * 1.2f).Within(1e-8f));

            var sawOutside = false;
            var sawUnclampedAboveCeiling = false;
            for (var i = 0; i < fan.Length; i++)
            {
                var sample = fan.GetValue(i);
                var eps = Prop<float>(sample, "EpsilonMetres");
                var outside = Prop<bool>(sample, "OutsideSearchRange");
                if (eps > ceiling)
                {
                    Assert.That(outside, Is.True, "Fan samples above search ceiling must be marked.");
                    sawOutside = true;
                    sawUnclampedAboveCeiling = true;
                }
                else
                {
                    Assert.That(outside, Is.False);
                }

                Assert.That(eps, Is.LessThanOrEqualTo(charCeiling + 1e-8f));
            }

            Assert.That(sawOutside, Is.True);
            Assert.That(sawUnclampedAboveCeiling, Is.True);
        }

        [Test]
        public void FailurePropagationFromProbe()
        {
            var search = CreateSearch();
            var result = Drive(search, (phase, epsilon, axis, isBaseline) =>
            {
                if (isBaseline)
                {
                    return BaselineOutcome();
                }

                if (phaseName(phase) == "Ladder" && epsilon > 1e-5f)
                {
                    return FailureOutcome("synthetic probe boom");
                }

                return Compact(false);
            });

            Assert.That(Prop<bool>(result, "Succeeded"), Is.False);
            Assert.That(Prop<string>(result, "ErrorReason"), Does.Contain("synthetic probe boom"));
            Assert.That(Prop<string>(result, "Verdict"), Is.EqualTo("FAILED"));
        }

        [Test]
        public void ThresholdBracketSemanticsMatchSpec()
        {
            var search = CreateSearch();
            var result = Drive(search, (phase, epsilon, axis, isBaseline) =>
            {
                if (isBaseline)
                {
                    return BaselineOutcome();
                }

                var diverged = epsilon >= 1e-3f;
                return NeedsFrames(phase) ? Framed(diverged, epsilon, axis) : Compact(diverged);
            });

            Assert.That(Prop<string>(result, "Verdict"), Is.EqualTo("THRESHOLD BRACKET FOUND"));
            Assert.That(Prop<bool>(result, "HasThresholdEstimate"), Is.True);
            Assert.That(Prop<bool>(result, "HasLargestStableEpsilon"), Is.True);
            Assert.That(Prop<bool>(result, "HasSmallestDivergentEpsilon"), Is.True);
            Assert.That(
                Prop<float>(result, "LargestStableEpsilonMetres"),
                Is.LessThan(Prop<float>(result, "SmallestDivergentEpsilonMetres")));
            Assert.That(
                Prop<float>(result, "ThresholdEstimateMetres"),
                Is.EqualTo(Prop<float>(result, "SmallestDivergentEpsilonMetres")));
            Assert.That(Prop<float>(result, "FinalBracketWidthMetres"), Is.GreaterThanOrEqualTo(0f));
            Assert.That(Prop<bool>(result, "ReferenceIsExactThreshold"), Is.False);
            Assert.That(
                Prop<float>(result, "SearchRangeStartMetres"),
                Is.EqualTo(1e-5f));
            Assert.That(
                Prop<float>(result, "SearchRangeCeilingMetres"),
                Is.EqualTo(1e-2f));
            Assert.That(
                Prop<float>(result, "CharacterizationCeilingMetres"),
                Is.EqualTo(1.2e-2f).Within(1e-9f));
        }

        [Test]
        public void DivergenceSettingsValidateSearchSettings()
        {
            var settingsType = Type.GetType("BugCam.Core.DivergenceSettings, BugCam.Core");
            var settings = settingsType.GetMethod("CreateDefault").Invoke(null, null);
            var reason = (string)settingsType.GetMethod("ValidateSearchSettings").Invoke(settings, null);
            Assert.That(reason, Is.Empty);
            UnityEngine.Object.DestroyImmediate((UnityEngine.Object)settings);
        }

        private static List<string> CaptureRequestOrder()
        {
            var search = CreateSearch();
            var order = new List<string>();
            Drive(search, (phase, epsilon, axis, isBaseline) =>
            {
                order.Add(
                    phaseName(phase) + ":" +
                    epsilon.ToString("R") + ":" +
                    AxisLabel(axis) + ":" +
                    isBaseline);
                if (isBaseline)
                {
                    return BaselineOutcome();
                }

                var diverged = epsilon >= 4e-4f;
                return NeedsFrames(phase) ? Framed(diverged, epsilon, axis) : Compact(diverged);
            });
            return order;
        }

        private static object Drive(
            object search,
            Func<object, float, Vector3, bool, object> oracle)
        {
            var searchType = search.GetType();
            var tryGet = searchType.GetMethod("TryGetNextProbe");
            var submit = searchType.GetMethod("SubmitProbeResult");
            var build = searchType.GetMethod("BuildResult");
            var requestType = Type.GetType("BugCam.Core.EpsilonProbeRequest, BugCam.Core");
            var args = new object[] { null };
            var guard = 0;
            while ((bool)tryGet.Invoke(search, args))
            {
                guard++;
                Assert.That(guard, Is.LessThan(500), "Search did not terminate.");
                var request = args[0];
                var phase = Prop<object>(request, "Phase");
                var epsilon = Prop<float>(request, "EpsilonMetres");
                var axis = Prop<Vector3>(request, "Axis");
                var isBaseline = Prop<bool>(request, "IsBaseline");
                var outcome = oracle(phase, epsilon, axis, isBaseline);
                submit.Invoke(search, new[] { request, outcome });
            }

            return build.Invoke(search, null);
        }

        private static object CreateSearch(
            object settings = null,
            int targetBodyId = 49,
            Vector3? axis = null,
            object strategy = null,
            float customStart = 0f)
        {
            var searchType = Type.GetType("BugCam.Core.EpsilonSearch, BugCam.Core");
            var settingsType = Type.GetType("BugCam.Core.EpsilonSearchSettings, BugCam.Core");
            var strategyType = Type.GetType("BugCam.Core.EpsilonSearchStrategy, BugCam.Core");
            Assert.That(searchType, Is.Not.Null);

            if (settings == null)
            {
                settings = settingsType.GetProperty("Default").GetValue(null);
            }

            if (strategy == null)
            {
                strategy = Enum.ToObject(strategyType, 0);
            }

            return Activator.CreateInstance(
                searchType,
                settings,
                targetBodyId,
                axis ?? Vector3.right,
                strategy,
                customStart);
        }

        private static object MakeSearchSettings(
            float start,
            float growth,
            float ceiling,
            int bisection,
            int ladder,
            float[] fan)
        {
            var type = Type.GetType("BugCam.Core.EpsilonSearchSettings, BugCam.Core");
            return Activator.CreateInstance(type, start, growth, ceiling, bisection, ladder, fan);
        }

        private static string Validate(object settings)
        {
            return (string)settings.GetType().GetMethod("Validate").Invoke(settings, null);
        }

        private static float[] DefaultFan()
        {
            return new[] { 0.8f, 0.9f, 1f, 1.1f, 1.2f };
        }

        private static object BaselineOutcome()
        {
            return Framed(false, 0f, Vector3.zero);
        }

        private static object Compact(bool diverged)
        {
            var type = Type.GetType("BugCam.Core.EpsilonProbeOutcome, BugCam.Core");
            var method = type.GetMethod(
                "Success",
                new[] { typeof(bool), typeof(int), typeof(float) });
            return method.Invoke(null, new object[] { diverged, diverged ? 2 : -1, diverged ? 1.5f : 0f });
        }

        private static object Framed(bool diverged, float epsilon, Vector3 axis)
        {
            var run = TinyRun(epsilon, axis);
            var type = Type.GetType("BugCam.Core.EpsilonProbeOutcome, BugCam.Core");
            var runType = Type.GetType("BugCam.Core.RunResult, BugCam.Core");
            var method = type.GetMethod(
                "Success",
                new[] { typeof(bool), typeof(int), typeof(float), runType });
            return method.Invoke(
                null,
                new object[] { diverged, diverged ? 2 : -1, diverged ? 1.5f : 0f, run });
        }

        private static object FailureOutcome(string reason)
        {
            var type = Type.GetType("BugCam.Core.EpsilonProbeOutcome, BugCam.Core");
            return type.GetMethod("Failure").Invoke(null, new object[] { reason });
        }

        private static object TinyRun(float epsilon, Vector3 axis)
        {
            var frames = new float[Stride];
            frames[6] = 1f; // identity quat w
            var runType = Type.GetType("BugCam.Core.RunResult, BugCam.Core");
            var perturbationType = Type.GetType("BugCam.Core.SimulationPerturbation, BugCam.Core");
            object perturbation;
            if (epsilon > 0f && axis != Vector3.zero)
            {
                perturbation = Activator.CreateInstance(
                    perturbationType,
                    49,
                    axis,
                    epsilon);
            }
            else
            {
                perturbation = Activator.CreateInstance(perturbationType);
            }

            var success = runType.GetMethod(
                "Success",
                new[]
                {
                    typeof(float[]),
                    typeof(int[]),
                    typeof(float),
                    perturbationType,
                    typeof(int),
                    typeof(int),
                    typeof(long)
                });
            return success.Invoke(
                null,
                new object[] { frames, new[] { 49 }, epsilon, perturbation, 1, 0, 0L });
        }

        private static bool NeedsFrames(object phase)
        {
            var name = phaseName(phase);
            return name == "Baseline" || name == "Fan";
        }

        private static string phaseName(object phase)
        {
            return phase.ToString();
        }

        private static string AxisLabel(Vector3 axis)
        {
            if (axis == Vector3.right)
            {
                return "X";
            }

            if (axis == Vector3.up)
            {
                return "Y";
            }

            if (axis == Vector3.forward)
            {
                return "Z";
            }

            if (axis == Vector3.zero)
            {
                return "0";
            }

            return axis.ToString();
        }


        private static Array Arr(object target, string name)
        {
            return (Array)target.GetType().GetProperty(name).GetValue(target);
        }

        private static T Prop<T>(object target, string name)
        {
            return (T)target.GetType().GetProperty(name).GetValue(target);
        }
    }
}
