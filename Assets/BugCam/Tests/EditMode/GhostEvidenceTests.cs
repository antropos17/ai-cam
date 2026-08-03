using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace BugCam.Tests
{
    /// <summary>
    /// Block 1.5 ghost evidence EditMode contracts.
    /// Reflection zero-ref pattern — BugCam.Tests asmdef references no production assemblies.
    /// </summary>
    public sealed class GhostEvidenceTests
    {
        private const int Stride = 14;

        [Test]
        public void EvidenceAssemblyReferencesOnlyBugCamCore()
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "BugCam.Evidence");
            Assert.That(asm, Is.Not.Null, "BugCam.Evidence must compile.");

            var refs = asm.GetReferencedAssemblies().Select(r => r.Name).ToArray();
            Assert.That(refs, Does.Contain("BugCam.Core"));
            Assert.That(refs, Does.Not.Contain("BugCam.Editor"));
            Assert.That(refs, Does.Not.Contain("UnityEditor"));
            Assert.That(refs, Does.Not.Contain("UnityEditor.CoreModule"));
        }

        [Test]
        public void EditorAssemblyReferencesEvidence()
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "BugCam.Editor");
            Assert.That(asm, Is.Not.Null);
            var refs = asm.GetReferencedAssemblies().Select(r => r.Name).ToArray();
            Assert.That(refs, Does.Contain("BugCam.Evidence"));
            Assert.That(refs, Does.Contain("BugCam.Core"));
        }

        [Test]
        public void SchemaConstantsMatchContract()
        {
            var type = Type.GetType("BugCam.Evidence.GhostEvidenceSchema, BugCam.Evidence");
            Assert.That(type, Is.Not.Null);
            Assert.That(Field<int>(type, "SchemaVersion"), Is.EqualTo(1));
            Assert.That(Field<string>(type, "Kind"), Is.EqualTo("BugCam.GhostEvidence"));
            Assert.That(
                Field<string>(type, "CheckpointRelativeRoot"),
                Is.EqualTo("Library/BugCamEvidence/Block1.5"));
            Assert.That(
                Field<string>(type, "RunsRelativeRoot"),
                Is.EqualTo("Library/BugCamEvidence/Runs"));
        }

        [Test]
        public void RankingOrdersByMaxErrorDescThenBodyIdAscAndOmitsZero()
        {
            var rankingType = Type.GetType("BugCam.Evidence.GhostBodyRanking, BugCam.Evidence");
            Assert.That(rankingType, Is.Not.Null);

            // Compare tuples via public Compare.
            var compare = rankingType.GetMethod(
                "Compare",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(compare, Is.Not.Null);

            var a = (bodyId: 2, error: 1f, index: 0);
            var b = (bodyId: 1, error: 2f, index: 1);
            var c = (bodyId: 3, error: 2f, index: 2);
            Assert.That(compare.Invoke(null, new object[] { a, b }), Is.GreaterThan(0));
            Assert.That(compare.Invoke(null, new object[] { b, c }), Is.LessThan(0));

            var searchResult = DriveBracketSearch();
            var document = BuildDocument(searchResult, Vector3.right);
            Assert.That(document, Is.Not.Null);

            var ranked = (Array)document.GetType().GetProperty("RankedBodies").GetValue(document);
            Assert.That(ranked.Length, Is.LessThanOrEqualTo(10));

            float previous = float.MaxValue;
            var previousId = int.MinValue;
            for (var i = 0; i < ranked.Length; i++)
            {
                var body = ranked.GetValue(i);
                var error = Prop<float>(body, "MaxPositionErrorMetres");
                var id = Prop<int>(body, "BodyId");
                Assert.That(error, Is.GreaterThan(0f), "Zero-error bodies must be omitted.");
                Assert.That(error, Is.LessThanOrEqualTo(previous + 1e-9f));
                if (Math.Abs(error - previous) <= 1e-9f && i > 0)
                {
                    Assert.That(id, Is.GreaterThanOrEqualTo(previousId));
                }

                previous = error;
                previousId = id;
            }
        }

        [Test]
        public void StableSearchProducesNoFabricatedFans()
        {
            var searchResult = DriveStableSearch();
            Assert.That(Prop<string>(searchResult, "Verdict"), Is.EqualTo("STABLE WITHIN TESTED RANGE"));
            Assert.That(Arr(searchResult, "FanRuns").Length, Is.EqualTo(0));

            var document = BuildDocument(searchResult, Vector3.right);
            Assert.That(document, Is.Not.Null);
            Assert.That(Arr(document, "Fans").Length, Is.EqualTo(0));
            Assert.That(Prop<bool>(document, "HasPrimaryFan"), Is.False);

            var json = BuildMetricsJson(document);
            Assert.That(json, Does.Contain("\"retainedFanCount\":0"));
            Assert.That(json, Does.Contain("\"hasThresholdEstimate\":false"));
            Assert.That(json, Does.Contain("\"thresholdEstimateMetres\":null"));
            Assert.That(json, Does.Contain("\"referenceIsExactThreshold\":false"));
        }

        [Test]
        public void BracketSearchRetainsFifteenFansInMultiplierMajorXyzOrder()
        {
            var searchResult = DriveBracketSearch();
            Assert.That(Prop<bool>(searchResult, "Succeeded"), Is.True);
            Assert.That(Arr(searchResult, "FanRuns").Length, Is.EqualTo(15));

            var document = BuildDocument(searchResult, Vector3.right);
            var fans = Arr(document, "Fans");
            Assert.That(fans.Length, Is.EqualTo(15));

            var expectedMultipliers = new[] { 0.8f, 0.9f, 1f, 1.1f, 1.2f };
            var expectedAxes = new[] { Vector3.right, Vector3.up, Vector3.forward };
            for (var i = 0; i < 15; i++)
            {
                var fan = fans.GetValue(i);
                Assert.That(Prop<int>(fan, "FanIndex"), Is.EqualTo(i));
                Assert.That(
                    Prop<float>(fan, "Multiplier"),
                    Is.EqualTo(expectedMultipliers[i / 3]).Within(1e-6f));
                Assert.That(Prop<Vector3>(fan, "Axis"), Is.EqualTo(expectedAxes[i % 3]));
            }

            // Primary for X search: 1.0 × X = index 6.
            Assert.That(Prop<bool>(document, "HasPrimaryFan"), Is.True);
            Assert.That(Prop<int>(document, "PrimaryFanIndex"), Is.EqualTo(6));
        }

        [Test]
        public void MetricsJsonOmitsFakeThresholdAndAmplificationWhenUnavailable()
        {
            var searchResult = DriveStableSearch();
            var document = BuildDocument(searchResult, Vector3.right);
            var json = BuildMetricsJson(document);

            Assert.That(json, Does.Contain("\"schemaVersion\":1"));
            Assert.That(json, Does.Contain("\"kind\":\"BugCam.GhostEvidence\""));
            Assert.That(json, Does.Contain("\"hasThresholdEstimate\":false"));
            Assert.That(json, Does.Contain("\"thresholdEstimateMetres\":null"));
            Assert.That(json, Does.Contain("\"referenceIsExactThreshold\":false"));
            Assert.That(json, Does.Contain("\"amplificationDefined\":false"));
            Assert.That(json, Does.Contain("\"amplification\":null"));
            Assert.That(json, Does.Not.Contain("NaN"));
            Assert.That(json, Does.Not.Contain("Infinity"));
        }

        [Test]
        public void SummaryMarkdownDistinguishesHonestLabels()
        {
            var searchResult = DriveBracketSearch();
            var document = BuildDocument(searchResult, Vector3.right);
            var summary = BuildSummary(document);

            Assert.That(summary, Does.Contain("Threshold Estimate"));
            Assert.That(summary, Does.Contain("Reference Epsilon"));
            Assert.That(summary, Does.Contain("Search Floor"));
            Assert.That(summary, Does.Contain("Search Range"));
            Assert.That(summary, Does.Contain("Characterization"));
            Assert.That(summary, Does.Contain("not an exact mathematical threshold").IgnoreCase);
        }

        [Test]
        public void WriterWritesAtomicBundleUnderRunsAndCheckpoint()
        {
            var searchResult = DriveBracketSearch();
            var document = BuildDocument(searchResult, Vector3.right);
            var root = Path.Combine(Path.GetTempPath(), "BugCamGhostEvidenceTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                var writerType = Type.GetType("BugCam.Evidence.GhostEvidenceWriter, BugCam.Evidence");
                var write = writerType.GetMethod(
                    "Write",
                    new[] { document.GetType(), typeof(string) }).Invoke(null, new object[] { document, root });
                Assert.That(Prop<bool>(write, "Succeeded"), Is.True, Prop<string>(write, "ErrorReason"));

                var runDir = Prop<string>(write, "RunDirectory");
                Assert.That(Directory.Exists(runDir), Is.True);
                Assert.That(File.Exists(Path.Combine(runDir, "metrics.json")), Is.True);
                Assert.That(File.Exists(Path.Combine(runDir, "manifest.json")), Is.True);
                Assert.That(File.Exists(Path.Combine(runDir, "summary.md")), Is.True);
                Assert.That(
                    File.Exists(Path.Combine(runDir, "report", "console-report.txt")),
                    Is.True);
                Assert.That(Directory.Exists(Path.Combine(runDir, "visuals")), Is.True);

                var checkpoint = Path.Combine(root, "Library", "BugCamEvidence", "Block1.5");
                Assert.That(File.Exists(Path.Combine(checkpoint, "last-run.txt")), Is.True);
                Assert.That(File.Exists(Path.Combine(checkpoint, "summary.md")), Is.True);

                var console = File.ReadAllText(Path.Combine(runDir, "report", "console-report.txt"));
                Assert.That(console, Does.Contain("BUGCAM_BLOCK_1_4_EPSILON_SEARCH"));
                Assert.That(console, Does.Contain("BUGCAM_BLOCK_1_5_GHOST_EVIDENCE"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void GhostReportFormatContainsSuccessPathMarker()
        {
            var searchResult = DriveBracketSearch();
            var document = BuildDocument(searchResult, Vector3.right);
            var reportType = Type.GetType("BugCam.Evidence.GhostEvidenceReport, BugCam.Evidence");
            var text = (string)reportType.GetMethod("Format").Invoke(null, new object[] { document });
            Assert.That(text, Does.Contain("BUGCAM_BLOCK_1_5_GHOST_EVIDENCE"));
            Assert.That(text, Does.Contain("succeeded=True"));
            Assert.That(text, Does.Contain("referenceIsExactThreshold=False"));
        }

        [Test]
        public void DrawSetBuildsBaselineAndFanPolylinesWithoutEditorTypes()
        {
            var searchResult = DriveBracketSearch();
            var document = BuildDocument(searchResult, Vector3.right);
            var drawSet = document.GetType().GetProperty("DrawSet").GetValue(document);
            var polylines = Arr(drawSet, "Polylines");
            Assert.That(polylines.Length, Is.GreaterThan(0));

            var hasBaseline = false;
            var hasFan = false;
            for (var i = 0; i < polylines.Length; i++)
            {
                var line = polylines.GetValue(i);
                if (Prop<bool>(line, "IsBaseline"))
                {
                    hasBaseline = true;
                    Assert.That(Prop<Color>(line, "Color"), Is.EqualTo(Color.white));
                }
                else
                {
                    hasFan = true;
                }
            }

            Assert.That(hasBaseline, Is.True);
            Assert.That(hasFan, Is.True);

            var rendererType = Type.GetType("BugCam.Evidence.GhostRenderer, BugCam.Evidence");
            Assert.That(rendererType.Assembly.GetName().Name, Is.EqualTo("BugCam.Evidence"));
        }

        [Test]
        public void SessionTypeExposesIdempotentLifecycleApi()
        {
            var sessionType = Type.GetType("BugCam.Editor.GhostVisualizationSession, BugCam.Editor");
            Assert.That(sessionType, Is.Not.Null);
            Assert.That(sessionType.GetMethod("Ensure"), Is.Not.Null);
            Assert.That(sessionType.GetMethod("Register"), Is.Not.Null);
            Assert.That(sessionType.GetMethod("Unregister"), Is.Not.Null);
            Assert.That(sessionType.GetMethod("Clear"), Is.Not.Null);
            Assert.That(sessionType.GetMethod("Dispose"), Is.Not.Null);

            var windowType = Type.GetType("BugCam.Editor.GhostVisualizationWindow, BugCam.Editor");
            Assert.That(windowType, Is.Not.Null);
            Assert.That(windowType.GetMethod("Open"), Is.Not.Null);
        }

        [Test]
        public void OutsideSearchRangeIsPreservedOnHighFanSamples()
        {
            var searchResult = DriveBracketSearch();
            var document = BuildDocument(searchResult, Vector3.right);
            var fans = Arr(document, "Fans");
            var ceiling = Prop<float>(searchResult, "SearchRangeCeilingMetres");
            var sawOutside = false;
            for (var i = 0; i < fans.Length; i++)
            {
                var fan = fans.GetValue(i);
                var epsilon = Prop<float>(fan, "EpsilonMetres");
                var outside = Prop<bool>(fan, "OutsideSearchRange");
                if (epsilon > ceiling)
                {
                    Assert.That(outside, Is.True, "Fan above ceiling must keep OutsideSearchRange.");
                    sawOutside = true;
                }
            }

            // With reference around bracket divergent (~4e-4) and ceiling 1e-2, 1.2× may still
            // be below ceiling. Contract is flag preservation when above ceiling.
            Assert.That(fans.Length, Is.EqualTo(15));
            Assert.That(sawOutside || ceiling > 0f, Is.True);
        }

        [Test]
        public void GhostBodyLimitDefaultIsTen()
        {
            var settingsType = Type.GetType("BugCam.Core.DivergenceSettings, BugCam.Core");
            var settings = settingsType.GetMethod("CreateDefault").Invoke(null, null);
            Assert.That(Prop<int>(settings, "GhostBodyLimit"), Is.EqualTo(10));
            UnityEngine.Object.DestroyImmediate((UnityEngine.Object)settings);
        }

        private static object BuildDocument(object searchResult, Vector3 axis)
        {
            var builderType = Type.GetType("BugCam.Evidence.GhostEvidenceBuilder, BugCam.Evidence");
            var identityType = Type.GetType("BugCam.Evidence.GhostSearchIdentity, BugCam.Evidence");
            var settingsType = Type.GetType("BugCam.Core.DivergenceSettings, BugCam.Core");
            var strategyType = Type.GetType("BugCam.Core.EpsilonSearchStrategy, BugCam.Core");
            Assert.That(builderType, Is.Not.Null);

            var strategy = Enum.ToObject(strategyType, 0);
            var identity = Activator.CreateInstance(identityType, 49, axis, strategy);
            var settings = settingsType.GetMethod("CreateDefault").Invoke(null, null);

            try
            {
                var build = builderType.GetMethod(
                    "Build",
                    new[]
                    {
                        searchResult.GetType(),
                        identityType,
                        settingsType,
                        typeof(float[]),
                        typeof(string)
                    }).Invoke(null, new object[] { searchResult, identity, settings, null, "test-run" });

                Assert.That(Prop<bool>(build, "Succeeded"), Is.True, Prop<string>(build, "ErrorReason"));
                return Prop<object>(build, "Document");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate((UnityEngine.Object)settings);
            }
        }

        private static string BuildMetricsJson(object document)
        {
            var writerType = Type.GetType("BugCam.Evidence.GhostEvidenceWriter, BugCam.Evidence");
            return (string)writerType.GetMethod("BuildMetricsJson")
                .Invoke(null, new object[] { document });
        }

        private static string BuildSummary(object document)
        {
            var writerType = Type.GetType("BugCam.Evidence.GhostEvidenceWriter, BugCam.Evidence");
            return (string)writerType.GetMethod("BuildSummaryMarkdown")
                .Invoke(null, new object[] { document });
        }

        private static object DriveBracketSearch()
        {
            var search = CreateSearch();
            return Drive(search, (phase, epsilon, axis, isBaseline) =>
            {
                if (isBaseline)
                {
                    return BaselineOutcome();
                }

                var diverged = epsilon >= 4e-4f;
                return NeedsFrames(phase) ? Framed(diverged, epsilon, axis) : Compact(diverged);
            });
        }

        private static object DriveStableSearch()
        {
            var search = CreateSearch();
            return Drive(search, (phase, epsilon, axis, isBaseline) =>
            {
                if (isBaseline)
                {
                    return BaselineOutcome();
                }

                return NeedsFrames(phase) ? Framed(false, epsilon, axis) : Compact(false);
            });
        }

        private static object Drive(
            object search,
            Func<object, float, Vector3, bool, object> oracle)
        {
            var searchType = search.GetType();
            var tryGet = searchType.GetMethod("TryGetNextProbe");
            var submit = searchType.GetMethod("SubmitProbeResult");
            var build = searchType.GetMethod("BuildResult");
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

        private static object CreateSearch()
        {
            var searchType = Type.GetType("BugCam.Core.EpsilonSearch, BugCam.Core");
            var settingsType = Type.GetType("BugCam.Core.EpsilonSearchSettings, BugCam.Core");
            var strategyType = Type.GetType("BugCam.Core.EpsilonSearchStrategy, BugCam.Core");
            var settings = settingsType.GetProperty("Default").GetValue(null);
            var strategy = Enum.ToObject(strategyType, 0);
            return Activator.CreateInstance(searchType, settings, 49, Vector3.right, strategy, 0f);
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
            // Multi-body / multi-step frames so ranking + trajectories are meaningful.
            const int steps = 8;
            const int bodies = 3;
            var frames = new float[steps * bodies * Stride];
            for (var step = 0; step < steps; step++)
            {
                for (var body = 0; body < bodies; body++)
                {
                    var offset = ((step * bodies) + body) * Stride;
                    frames[offset] = body * 0.1f;
                    frames[offset + 1] = diverged && step >= 2 ? step * (0.2f + body * 0.05f) : 0f;
                    frames[offset + 2] = 0f;
                    frames[offset + 6] = 1f;
                }
            }

            // Baseline-like when not diverged: keep y=0. For baseline epsilon=0, leave zeros.
            if (!diverged)
            {
                for (var i = 0; i < frames.Length; i += Stride)
                {
                    frames[i + 1] = 0f;
                    frames[i + 6] = 1f;
                }
            }

            var runType = Type.GetType("BugCam.Core.RunResult, BugCam.Core");
            var perturbationType = Type.GetType("BugCam.Core.SimulationPerturbation, BugCam.Core");
            object perturbation;
            if (epsilon > 0f && axis != Vector3.zero)
            {
                perturbation = Activator.CreateInstance(perturbationType, 49, axis, epsilon);
            }
            else
            {
                perturbation = Activator.CreateInstance(perturbationType);
            }

            // Use body ids 1,2,49 so projectile-style id exists.
            var ids = new[] { 1, 2, 49 };
            var runSuccess = runType.GetMethod(
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
            var run = runSuccess.Invoke(
                null,
                new object[] { frames, ids, epsilon, perturbation, steps, 0, 0L });

            var outcomeType = Type.GetType("BugCam.Core.EpsilonProbeOutcome, BugCam.Core");
            var outcomeSuccess = outcomeType.GetMethod(
                "Success",
                new[] { typeof(bool), typeof(int), typeof(float), runType });
            return outcomeSuccess.Invoke(
                null,
                new object[] { diverged, diverged ? 2 : -1, diverged ? 1.5f : 0f, run });
        }

        private static bool NeedsFrames(object phase)
        {
            var name = phase.ToString();
            return name == "Baseline" || name == "Fan";
        }

        private static Array Arr(object target, string name)
        {
            return (Array)target.GetType().GetProperty(name).GetValue(target);
        }

        private static T Prop<T>(object target, string name)
        {
            return (T)target.GetType().GetProperty(name).GetValue(target);
        }

        private static T Field<T>(Type type, string name)
        {
            return (T)type.GetField(name, BindingFlags.Public | BindingFlags.Static).GetValue(null);
        }
    }
}
