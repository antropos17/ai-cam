using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
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
            Assert.That(
                Field<string>(type, "FirstDivergencePngFileName"),
                Is.EqualTo("first-sustained-divergence.png"));
            Assert.That(
                Field<string>(type, "MaxSpreadPngFileName"),
                Is.EqualTo("maximum-spread.png"));
            Assert.That(
                Field<string>(type, "FinalPngFileName"),
                Is.EqualTo("final-state.png"));
            Assert.That(Field<string>(type, "OverviewPngFileName"), Is.EqualTo("overview.png"));
            Assert.That(Field<string>(type, "RunsDirectoryName"), Is.EqualTo("runs"));
            Assert.That(Field<string>(type, "BaselineRunFileName"), Is.EqualTo("baseline.json"));
            Assert.That(
                Field<string>(type, "ConsoleReportFileName"),
                Is.EqualTo("console-report.txt"));
            var fanName = type.GetMethod("FanRunFileName").Invoke(null, new object[] { 0 });
            Assert.That(fanName, Is.EqualTo("fan-00.json"));
        }

        [Test]
        public void RankingOrdersByMaxErrorDescThenBodyIdAscAndOmitsZero()
        {
            var rankingType = Type.GetType("BugCam.Evidence.GhostBodyRanking, BugCam.Evidence");
            Assert.That(rankingType, Is.Not.Null);

            var compare = rankingType.GetMethod(
                "Compare",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(compare, Is.Not.Null);

            var a = (bodyId: 2, error: 1f, index: 0);
            var b = (bodyId: 1, error: 2f, index: 1);
            var c = (bodyId: 3, error: 2f, index: 2);
            Assert.That(compare.Invoke(null, new object[] { a, b }), Is.GreaterThan(0));
            Assert.That(compare.Invoke(null, new object[] { b, c }), Is.LessThan(0));

            // Oracle Rank with known ids/errors — exact order, omit zero, Length > 0.
            var rankOverload = rankingType.GetMethod(
                "Rank",
                new[] { typeof(int[]), typeof(float[]), typeof(int) });
            Assert.That(rankOverload, Is.Not.Null, "Rank(bodyIds, errors, limit) overload required.");

            var bodyIds = new[] { 10, 20, 30, 40 };
            var errors = new[] { 0f, 2f, 2f, 1f };
            var ranked = (Array)rankOverload.Invoke(null, new object[] { bodyIds, errors, 10 });
            Assert.That(ranked.Length, Is.GreaterThan(0));
            Assert.That(ranked.Length, Is.EqualTo(3));
            Assert.That(Prop<int>(ranked.GetValue(0), "BodyId"), Is.EqualTo(20));
            Assert.That(Prop<float>(ranked.GetValue(0), "MaxPositionErrorMetres"), Is.EqualTo(2f));
            Assert.That(Prop<int>(ranked.GetValue(1), "BodyId"), Is.EqualTo(30));
            Assert.That(Prop<float>(ranked.GetValue(1), "MaxPositionErrorMetres"), Is.EqualTo(2f));
            Assert.That(Prop<int>(ranked.GetValue(2), "BodyId"), Is.EqualTo(40));
            Assert.That(Prop<float>(ranked.GetValue(2), "MaxPositionErrorMetres"), Is.EqualTo(1f));

            for (var i = 0; i < ranked.Length; i++)
            {
                Assert.That(
                    Prop<float>(ranked.GetValue(i), "MaxPositionErrorMetres"),
                    Is.GreaterThan(0f),
                    "Zero-error bodies must be omitted.");
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
            Assert.That(Prop<bool>(document, "Success"), Is.True);

            var json = BuildMetricsJson(document);
            Assert.That(json, Does.Contain("\"retainedFanCount\":0"));
            Assert.That(json, Does.Contain("\"hasThresholdEstimate\":false"));
            Assert.That(json, Does.Contain("\"thresholdEstimateMetres\":null"));
            Assert.That(json, Does.Contain("\"hasReferenceEpsilon\":false"));
            Assert.That(json, Does.Contain("\"referenceEpsilonMetres\":null"));
            Assert.That(json, Does.Contain("\"hasFinalBracketWidth\":false"));
            Assert.That(json, Does.Contain("\"finalBracketWidthMetres\":null"));
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
            Assert.That(json, Does.Contain("\"success\":true"));
            Assert.That(json, Does.Contain("\"hasThresholdEstimate\":false"));
            Assert.That(json, Does.Contain("\"thresholdEstimateMetres\":null"));
            Assert.That(json, Does.Contain("\"referenceEpsilonMetres\":null"));
            Assert.That(json, Does.Contain("\"finalBracketWidthMetres\":null"));
            Assert.That(json, Does.Contain("\"referenceIsExactThreshold\":false"));
            Assert.That(json, Does.Contain("\"amplificationDefined\":false"));
            Assert.That(json, Does.Contain("\"amplification\":null"));
            Assert.That(json, Does.Contain("\"maxSpreadMetres\":null"));
            Assert.That(json, Does.Contain("\"firstDivergenceFrame\":null"));
            Assert.That(json, Does.Contain("\"firstDivergenceBodyId\":null"));
            Assert.That(json, Does.Contain("\"hasSignificantDivergence\":false"));
            Assert.That(json, Does.Contain("\"unityVersion\":"));
            Assert.That(json, Does.Contain("\"gitCommitSha\":"));
            Assert.That(json, Does.Contain("\"gitBranch\":"));
            Assert.That(json, Does.Contain("\"scenePath\":"));
            Assert.That(json, Does.Not.Contain("NaN"));
            Assert.That(json, Does.Not.Contain("Infinity"));
        }

        [Test]
        public void FailedSearchWritesHonestFailureBundle()
        {
            var failureType = Type.GetType("BugCam.Core.EpsilonSearchResult, BugCam.Core");
            var failure = failureType.GetMethod("Failure", new[] { typeof(string) })
                .Invoke(null, new object[] { "Temporary scene cleanup timed out after 120 frames." });

            var document = BuildDocument(failure, Vector3.right);
            Assert.That(Prop<bool>(document, "Success"), Is.False);
            Assert.That(Prop<string>(document, "ErrorCode"), Is.EqualTo("CLEANUP_TIMEOUT"));
            Assert.That(Arr(document, "Fans").Length, Is.EqualTo(0));

            var json = BuildMetricsJson(document);
            Assert.That(json, Does.Contain("\"success\":false"));
            Assert.That(json, Does.Contain("\"errorCode\":\"CLEANUP_TIMEOUT\""));
            Assert.That(json, Does.Contain("\"retainedFanCount\":0"));
            Assert.That(json, Does.Contain("\"thresholdEstimateMetres\":null"));
            Assert.That(json, Does.Contain("\"referenceEpsilonMetres\":null"));
            Assert.That(json, Does.Contain("\"finalBracketWidthMetres\":null"));
            Assert.That(json, Does.Contain("\"maxSpreadMetres\":null"));
            Assert.That(json, Does.Contain("\"firstDivergenceFrame\":null"));
            Assert.That(json, Does.Contain("\"physicalProbeCount\":null"));

            var console = FormatHonestConsole(document);
            Assert.That(console, Does.Contain("thresholdEstimateMetres=null"));
            Assert.That(console, Does.Contain("succeeded=False"));
            Assert.That(console, Does.Not.Match("thresholdEstimateMetres=0(\r|\n|$)"));
            Assert.That(console, Does.Contain("maxSpreadMetres=null"));
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
                Assert.That(
                    File.Exists(Path.Combine(runDir, "runs", "baseline.json")),
                    Is.True);
                for (var i = 0; i < 15; i++)
                {
                    var fanFile = Path.Combine(runDir, "runs", "fan-" + i.ToString("00") + ".json");
                    Assert.That(File.Exists(fanFile), Is.True, fanFile);
                    var fanJson = File.ReadAllText(fanFile);
                    Assert.That(fanJson, Does.Contain("\"fanIndex\":" + i));
                    Assert.That(fanJson, Does.Contain("\"stateFrames\":["));
                    Assert.That(fanJson, Does.Contain("\"stableBodyIds\":["));
                }

                var baselineJson = File.ReadAllText(Path.Combine(runDir, "runs", "baseline.json"));
                Assert.That(baselineJson, Does.Contain("\"kind\":\"baseline\""));
                Assert.That(baselineJson, Does.Contain("\"stateFrames\":["));

                var manifest = File.ReadAllText(Path.Combine(runDir, "manifest.json"));
                Assert.That(manifest, Does.Contain("\"artifacts\":["));
                Assert.That(manifest, Does.Contain("runs/baseline.json"));
                Assert.That(manifest, Does.Contain("runs/fan-00.json"));
                Assert.That(manifest, Does.Contain("runs/fan-14.json"));
                Assert.That(manifest, Does.Contain("first-sustained-divergence.png"));
                Assert.That(manifest, Does.Contain("maximum-spread.png"));
                Assert.That(manifest, Does.Contain("final-state.png"));

                var checkpoint = Path.Combine(root, "Library", "BugCamEvidence", "Block1.5");
                Assert.That(File.Exists(Path.Combine(checkpoint, "last-run.txt")), Is.True);
                Assert.That(File.Exists(Path.Combine(checkpoint, "summary.md")), Is.True);

                var console = File.ReadAllText(Path.Combine(runDir, "report", "console-report.txt"));
                Assert.That(console, Does.Contain("BUGCAM_BLOCK_1_4_EPSILON_SEARCH"));
                Assert.That(console, Does.Contain("BUGCAM_BLOCK_1_5_GHOST_EVIDENCE"));
                Assert.That(console, Does.Not.Match("thresholdEstimateMetres=0(\r|\n|$)"));
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
        public void StableAndFailureBundlesDoNotFabricateFanRunJson()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "BugCamGhostNoFans-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var writerType = Type.GetType("BugCam.Evidence.GhostEvidenceWriter, BugCam.Evidence");
                var writeMethod = writerType.GetMethod(
                    "Write",
                    new[]
                    {
                        Type.GetType("BugCam.Evidence.GhostEvidenceDocument, BugCam.Evidence"),
                        typeof(string)
                    });

                var stableDoc = BuildDocument(DriveStableSearch(), Vector3.right, "stable-run");
                var stableWrite = writeMethod.Invoke(null, new object[] { stableDoc, root });
                Assert.That(Prop<bool>(stableWrite, "Succeeded"), Is.True);
                var stableDir = Prop<string>(stableWrite, "RunDirectory");
                Assert.That(File.Exists(Path.Combine(stableDir, "runs", "baseline.json")), Is.True);
                Assert.That(
                    Directory.GetFiles(Path.Combine(stableDir, "runs"), "fan-*.json").Length,
                    Is.EqualTo(0));

                var failureType = Type.GetType("BugCam.Core.EpsilonSearchResult, BugCam.Core");
                var failure = failureType.GetMethod("Failure", new[] { typeof(string) })
                    .Invoke(null, new object[] { "search exploded"});
                var failDoc = BuildDocument(failure, Vector3.right, "fail-run");
                Assert.That(Prop<bool>(failDoc, "Success"), Is.False);
                var failWrite = writeMethod.Invoke(null, new object[] { failDoc, root });
                Assert.That(Prop<bool>(failWrite, "Succeeded"), Is.True);
                var failDir = Prop<string>(failWrite, "RunDirectory");
                Assert.That(failDir, Is.Not.EqualTo(stableDir));
                Assert.That(File.Exists(Path.Combine(failDir, "runs", "baseline.json")), Is.False);
                Assert.That(
                    Directory.GetFiles(Path.Combine(failDir, "runs"), "*.json").Length,
                    Is.EqualTo(0));
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
        public void StrictPrimaryMetricsNullForFailedCleanupBuildStableAndPresentForBracket()
        {
            // Failed search
            var failureType = Type.GetType("BugCam.Core.EpsilonSearchResult, BugCam.Core");
            var failed = failureType.GetMethod("Failure", new[] { typeof(string) })
                .Invoke(null, new object[] { "probe failed"});
            AssertPrimaryUnavailable(BuildMetricsJson(BuildDocument(failed, Vector3.right)));

            // Cleanup timeout
            var cleanup = failureType.GetMethod("Failure", new[] { typeof(string) })
                .Invoke(null, new object[] { "Temporary scene cleanup timed out after 120 frames." });
            var cleanupDoc = BuildDocument(cleanup, Vector3.right);
            Assert.That(Prop<string>(cleanupDoc, "ErrorCode"), Is.EqualTo("CLEANUP_TIMEOUT"));
            AssertPrimaryUnavailable(BuildMetricsJson(cleanupDoc));

            // STABLE
            AssertPrimaryUnavailable(BuildMetricsJson(BuildDocument(DriveStableSearch(), Vector3.right)));

            // Bracket / characterization with divergence — primary object must be populated.
            var bracketJson = BuildMetricsJson(BuildDocument(DriveBracketSearch(), Vector3.right));
            Assert.That(bracketJson, Does.Contain("\"hasSignificantDivergence\":true"));
            Assert.That(bracketJson, Does.Contain("\"hasMaxSpread\":true"));
            Assert.That(bracketJson, Does.Contain("\"hasFirstDivergenceBodyId\":true"));
            Assert.That(
                bracketJson,
                Does.Match("\"primary\":\\{[^}]*\"maxSpreadMetres\":[0-9]"),
                "Primary maxSpreadMetres must be a number, not null.");
            Assert.That(
                bracketJson,
                Does.Match("\"primary\":\\{[^}]*\"firstDivergenceBodyId\":[0-9]"),
                "Primary firstDivergenceBodyId must be a number, not null.");
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
        public void ConsoleReportAgreesWithMetricsNullThreshold()
        {
            var searchResult = DriveStableSearch();
            var document = BuildDocument(searchResult, Vector3.right);
            var json = BuildMetricsJson(document);
            var console = FormatHonestConsole(document);

            Assert.That(json, Does.Contain("\"thresholdEstimateMetres\":null"));
            Assert.That(console, Does.Contain("hasThresholdEstimate=False"));
            Assert.That(console, Does.Contain("thresholdEstimateMetres=null"));
            Assert.That(console, Does.Not.Match("thresholdEstimateMetres=0(\r|\n|$)"));
            Assert.That(console, Does.Contain("referenceEpsilonMetres=null"));
            Assert.That(console, Does.Contain("finalBracketWidthMetres=null"));
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
        public void FirstDivergenceMarkerUsesFirstDivergenceBodyIdNotMaxSpread()
        {
            // Fixture: FirstDivergenceBodyId=2 ≠ MaxSpreadBodyId=49 ≠ AffectedBodyIds[0]=1.
            var searchResult = DriveBracketSearch();
            var document = BuildDocument(searchResult, Vector3.right);
            Assert.That(Prop<bool>(document, "HasPrimaryFan"), Is.True);

            var primary = document.GetType().GetProperty("PrimaryDivergence").GetValue(document);
            var firstDivBodyId = Prop<int>(primary, "FirstDivergenceBodyId");
            var maxSpreadBodyId = Prop<int>(primary, "MaxSpreadBodyId");
            var affected = Prop<int[]>(primary, "AffectedBodyIds");
            var firstDivFrame = Prop<int>(primary, "FirstDivergenceFrame");
            var maxSpreadStep = Prop<int>(primary, "MaxSpreadStep");
            Assert.That(firstDivBodyId, Is.EqualTo(2));
            Assert.That(maxSpreadBodyId, Is.EqualTo(49));
            Assert.That(affected, Is.Not.Null.And.Not.Empty);
            Assert.That(affected[0], Is.EqualTo(1));
            Assert.That(firstDivBodyId, Is.Not.EqualTo(maxSpreadBodyId));
            Assert.That(firstDivBodyId, Is.Not.EqualTo(affected[0]));
            Assert.That(maxSpreadBodyId, Is.Not.EqualTo(affected[0]));
            Assert.That(firstDivFrame, Is.GreaterThanOrEqualTo(0));
            Assert.That(maxSpreadStep, Is.GreaterThanOrEqualTo(0));

            var fans = Arr(document, "Fans");
            var primaryFan = fans.GetValue(Prop<int>(document, "PrimaryFanIndex"));
            var primaryRun = Prop<object>(primaryFan, "Run");

            var samplerType = Type.GetType("BugCam.Evidence.GhostTrajectorySampler, BugCam.Evidence");
            var findBodyIndex = samplerType.GetMethod(
                "FindBodyIndex",
                BindingFlags.Public | BindingFlags.Static);
            var tryGetBodyPosition = samplerType.GetMethod(
                "TryGetBodyPosition",
                BindingFlags.Public | BindingFlags.Static);

            var firstBodyIndex = (int)findBodyIndex.Invoke(null, new object[] { primaryRun, firstDivBodyId });
            var firstArgs = new object[] { primaryRun, firstBodyIndex, firstDivFrame, null };
            Assert.That((bool)tryGetBodyPosition.Invoke(null, firstArgs), Is.True);
            var expectedFirstWorld = (Vector3)firstArgs[3];

            var maxBodyIndex = (int)findBodyIndex.Invoke(null, new object[] { primaryRun, maxSpreadBodyId });
            var maxArgs = new object[] { primaryRun, maxBodyIndex, maxSpreadStep, null };
            Assert.That((bool)tryGetBodyPosition.Invoke(null, maxArgs), Is.True);
            var expectedMaxWorld = (Vector3)maxArgs[3];

            var affectedIndex = (int)findBodyIndex.Invoke(null, new object[] { primaryRun, affected[0] });
            var affectedArgs = new object[] { primaryRun, affectedIndex, firstDivFrame, null };
            Assert.That((bool)tryGetBodyPosition.Invoke(null, affectedArgs), Is.True);
            var affectedWorld = (Vector3)affectedArgs[3];

            Assert.That(expectedFirstWorld, Is.Not.EqualTo(expectedMaxWorld));
            Assert.That(expectedFirstWorld, Is.Not.EqualTo(affectedWorld));

            var drawSet = document.GetType().GetProperty("DrawSet").GetValue(document);
            Assert.That(Prop<bool>(drawSet, "HasFirstDivergence"), Is.True);
            Assert.That(Prop<bool>(drawSet, "HasMaxSpread"), Is.True);
            Assert.That(Prop<int>(drawSet, "FirstDivergenceBodyId"), Is.EqualTo(2));
            Assert.That(Prop<int>(drawSet, "MaxSpreadBodyId"), Is.EqualTo(49));
            Assert.That(
                Prop<Vector3>(drawSet, "FirstDivergenceWorld"),
                Is.EqualTo(expectedFirstWorld),
                "First-divergence marker must sample FirstDivergenceBodyId at FirstDivergenceFrame.");
            Assert.That(
                Prop<Vector3>(drawSet, "MaxSpreadWorld"),
                Is.EqualTo(expectedMaxWorld),
                "Max-spread marker must sample MaxSpreadBodyId at MaxSpreadStep.");
            Assert.That(
                Prop<Vector3>(drawSet, "FirstDivergenceWorld"),
                Is.Not.EqualTo(affectedWorld),
                "First-divergence marker must not use AffectedBodyIds[0].");
            Assert.That(
                Prop<Vector3>(drawSet, "FirstDivergenceWorld"),
                Is.Not.EqualTo(expectedMaxWorld),
                "First-divergence marker must not proxy MaxSpreadBodyId.");
        }

        [Test]
        public void WindowAndHostPinNestedCoroutineSearchContracts()
        {
            var windowType = Type.GetType("BugCam.Editor.GhostVisualizationWindow, BugCam.Editor");
            var hostType = Type.GetType("BugCam.Editor.GhostEvidencePlayModeHost, BugCam.Editor");
            Assert.That(windowType, Is.Not.Null);
            Assert.That(hostType, Is.Not.Null);
            Assert.That(
                hostType.GetMethod(
                    "TryStartTowerSearch",
                    BindingFlags.Public | BindingFlags.Static),
                Is.Not.Null,
                "Host must expose the single shared search entry.");
            Assert.That(
                hostType.GetMethod(
                    "StartTowerSearch",
                    BindingFlags.Public | BindingFlags.Static),
                Is.Null,
                "StartTowerSearch must be private; public callers use TryStartTowerSearch.");
            Assert.That(
                hostType.GetMethod(
                    "StartTowerSearch",
                    BindingFlags.NonPublic | BindingFlags.Static),
                Is.Not.Null);
            Assert.That(
                Type.GetType("BugCam.Editor.EditorCoroutineUtility, BugCam.Editor"),
                Is.Null,
                "Broken EditorCoroutineUtility must be removed; Window uses Host MonoBehaviour.");
            Assert.That(
                windowType.GetMethod(
                    "RunSearchCoroutine",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public),
                Is.Null,
                "Window must not own a nested-IEnumerator search path.");

            // Source contract: Window call site + Host MonoBehaviour yield return runner.Run.
            // In-memory enumerator demos alone are insufficient Host proof.
            var windowCs = File.ReadAllText(
                Path.Combine(Application.dataPath, "BugCam", "Editor", "GhostVisualizationWindow.cs"));
            var hostCs = File.ReadAllText(
                Path.Combine(Application.dataPath, "BugCam", "Editor", "GhostEvidencePlayModeHost.cs"));
            Assert.That(
                windowCs,
                Does.Contain("GhostEvidencePlayModeHost.TryStartTowerSearch("),
                "Window must call Host.TryStartTowerSearch.");
            // Avoid substring false-positive on TryStartTowerSearch → StartTowerSearch.
            Assert.That(
                windowCs,
                Does.Not.Contain(".StartTowerSearch("),
                "Window must not call ungated Host.StartTowerSearch.");
            var menuIdx = hostCs.IndexOf("MenuRunTowerStep32()", StringComparison.Ordinal);
            Assert.That(menuIdx, Is.GreaterThanOrEqualTo(0));
            var menuBody = hostCs.Substring(menuIdx, Math.Min(400, hostCs.Length - menuIdx));
            Assert.That(
                menuBody,
                Does.Contain("TryStartTowerSearch("),
                "MenuRunTowerStep32 must route through TryStartTowerSearch.");
            Assert.That(
                menuBody,
                Does.Not.Contain("StartTowerSearch(32"),
                "Menu must not call ungated StartTowerSearch.");
            Assert.That(
                hostCs,
                Does.Contain("StartCoroutine(Run("),
                "Host must start its search via MonoBehaviour.StartCoroutine.");
            Assert.That(
                hostCs,
                Does.Contain("yield return runner.Run("),
                "Host Run must yield return EpsilonSearchRunner.Run (Unity nested pump).");

            // Semantic footnote: outer-only MoveNext skips nested IEnumerator bodies.
            var innerRanBroken = false;
            IEnumerator InnerBroken()
            {
                innerRanBroken = true;
                yield return null;
            }

            IEnumerator OuterBroken()
            {
                yield return InnerBroken();
            }

            var outer = OuterBroken();
            Assert.That(outer.MoveNext(), Is.True);
            Assert.That(outer.Current, Is.InstanceOf<IEnumerator>());
            Assert.That(outer.MoveNext(), Is.False);
            Assert.That(
                innerRanBroken,
                Is.False,
                "Outer-only MoveNext must leave nested body unrun (EditorCoroutineUtility bug).");
        }

        [Test]
        public void ConcurrentTryStartTowerSearchRejectsWhileBusy()
        {
            const string busyKey = "BugCam.GhostSearch.Busy";
            const string pendingKey = "BugCam.GhostHost.Pending";
            SessionState.SetBool(busyKey, false);
            SessionState.SetBool(pendingKey, false);

            var hostType = Type.GetType("BugCam.Editor.GhostEvidencePlayModeHost, BugCam.Editor");
            Assert.That(hostType, Is.Not.Null);
            var tryStart = hostType.GetMethod(
                "TryStartTowerSearch",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(tryStart, Is.Not.Null);

            SessionState.SetBool(busyKey, true);
            try
            {
                var strategyType = Type.GetType("BugCam.Core.EpsilonSearchStrategy, BugCam.Core");
                var strategy = Enum.ToObject(strategyType, 0);
                var args = new object[]
                {
                    32,
                    strategy,
                    Vector3.right,
                    "test",
                    null
                };
                var accepted = (bool)tryStart.Invoke(null, args);
                Assert.That(accepted, Is.False, "Busy Host must reject concurrent TryStart.");
                Assert.That(args[4], Is.InstanceOf<string>());
                Assert.That((string)args[4], Does.Contain("already pending or running"));
                Assert.That(SessionState.GetBool(busyKey, false), Is.True);
            }
            finally
            {
                SessionState.SetBool(busyKey, false);
                SessionState.SetBool(pendingKey, false);
            }
        }

        [Test]
        public void PlayModeInterruptCleanupDestroysTempRunnerAndAllowsRestart()
        {
            const string busyKey = "BugCam.GhostSearch.Busy";
            const string pendingKey = "BugCam.GhostHost.Pending";
            const string goName = "BugCamGhostEvidenceRunner_TEMP";

            var hostType = Type.GetType("BugCam.Editor.GhostEvidencePlayModeHost, BugCam.Editor");
            Assert.That(hostType, Is.Not.Null);

            var cleanup = hostType.GetMethod(
                "CleanupInterruptedSearchForTests",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(
                cleanup,
                Is.Not.Null,
                "Host must expose CleanupInterruptedSearchForTests seam.");

            var allowPlayModeEntry = hostType.GetField(
                "AllowPlayModeEntry",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(allowPlayModeEntry, Is.Not.Null);

            var tryStart = hostType.GetMethod(
                "TryStartTowerSearch",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(tryStart, Is.Not.Null);

            var searchCompleted = hostType.GetEvent("SearchCompleted");
            Assert.That(searchCompleted, Is.Not.Null);

            var completionType = Type.GetType("BugCam.Editor.GhostSearchCompletion, BugCam.Editor");
            Assert.That(completionType, Is.Not.Null);

            var previousAllow = (bool)allowPlayModeEntry.GetValue(null);
            var holder = new InterruptCompletionHolder();
            var handler = BuildSearchCompletedHandler(completionType, holder);

            SessionState.SetBool(busyKey, false);
            SessionState.SetBool(pendingKey, false);
            cleanup.Invoke(null, null);

            try
            {
                searchCompleted.AddEventHandler(null, handler);

                var runner = new GameObject(goName);
                runner.hideFlags = HideFlags.DontSave;
                Assert.That(
                    GameObject.Find(goName),
                    Is.Not.Null,
                    "Host runner must exist while search is active.");

                SessionState.SetBool(busyKey, true);
                SessionState.SetBool(pendingKey, true);
                Assert.That(SessionState.GetBool(busyKey, false), Is.True);

                cleanup.Invoke(null, null);

                Assert.That(
                    GameObject.Find(goName),
                    Is.Null,
                    "Interrupt cleanup must destroy BugCamGhostEvidenceRunner_TEMP.");
                var leftovers = Resources.FindObjectsOfTypeAll<GameObject>()
                    .Where(go => go != null && go.name == goName)
                    .ToArray();
                Assert.That(
                    leftovers,
                    Is.Empty,
                    "No Host TEMP runner may remain after interrupt cleanup.");
                Assert.That(SessionState.GetBool(busyKey, false), Is.False, "Busy must clear.");
                Assert.That(SessionState.GetBool(pendingKey, false), Is.False, "Pending must clear.");
                Assert.That(holder.Count, Is.EqualTo(1), "Interrupt must notify once.");
                Assert.That(holder.Last, Is.Not.Null);
                Assert.That(
                    Prop<bool>(holder.Last, "WriteSucceeded"),
                    Is.False,
                    "Interrupt must not emit SearchCompleted as success.");
                Assert.That(
                    Prop<string>(holder.Last, "Status"),
                    Does.Contain("Interrupted"));

                cleanup.Invoke(null, null);
                Assert.That(GameObject.Find(goName), Is.Null);
                Assert.That(SessionState.GetBool(busyKey, false), Is.False);
                Assert.That(SessionState.GetBool(pendingKey, false), Is.False);
                Assert.That(
                    holder.Count,
                    Is.EqualTo(1),
                    "Repeated cleanup must not emit another SearchCompleted.");

                allowPlayModeEntry.SetValue(null, false);
                var strategyType = Type.GetType("BugCam.Core.EpsilonSearchStrategy, BugCam.Core");
                var strategy = Enum.ToObject(strategyType, 0);
                var args = new object[]
                {
                    32,
                    strategy,
                    Vector3.right,
                    "interrupt-regression",
                    null
                };
                var accepted = (bool)tryStart.Invoke(null, args);
                Assert.That(
                    accepted,
                    Is.True,
                    "After interrupt cleanup, TryStartTowerSearch must accept.");
                Assert.That(args[4], Is.Null);
                Assert.That(
                    SessionState.GetBool(busyKey, false),
                    Is.True,
                    "Accepted restart must mark Busy.");
            }
            finally
            {
                searchCompleted.RemoveEventHandler(null, handler);
                allowPlayModeEntry.SetValue(null, previousAllow);
                cleanup.Invoke(null, null);
                SessionState.SetBool(busyKey, false);
                SessionState.SetBool(pendingKey, false);
            }
        }

        private sealed class InterruptCompletionHolder
        {
            public int Count;
            public object Last;
        }

        private static Delegate BuildSearchCompletedHandler(Type completionType, InterruptCompletionHolder holder)
        {
            var param = Expression.Parameter(completionType, "completion");
            var holderConst = Expression.Constant(holder);
            var countField = Expression.Field(holderConst, nameof(InterruptCompletionHolder.Count));
            var lastField = Expression.Field(holderConst, nameof(InterruptCompletionHolder.Last));
            var body = Expression.Block(
                Expression.Assign(countField, Expression.Add(countField, Expression.Constant(1))),
                Expression.Assign(lastField, Expression.Convert(param, typeof(object))));
            return Expression.Lambda(typeof(Action<>).MakeGenericType(completionType), body, param)
                .Compile();
        }

        [Test]
        public void BuildFailedOverBracketNullsThresholdInConsoleAndMetrics()
        {
            var searchResult = DriveBracketSearch();
            Assert.That(Prop<bool>(searchResult, "Succeeded"), Is.True);
            Assert.That(Prop<bool>(searchResult, "HasThresholdEstimate"), Is.True);

            var document = CreateFailureDocument(
                searchResult,
                "BUILD_FAILED",
                "forced build failure over bracket search");
            Assert.That(Prop<bool>(document, "Success"), Is.False);
            Assert.That(Prop<string>(document, "ErrorCode"), Is.EqualTo("BUILD_FAILED"));

            var json = BuildMetricsJson(document);
            var console = FormatHonestConsole(document);
            Assert.That(json, Does.Contain("\"hasThresholdEstimate\":false"));
            Assert.That(json, Does.Contain("\"thresholdEstimateMetres\":null"));
            Assert.That(console, Does.Contain("hasThresholdEstimate=False"));
            Assert.That(console, Does.Contain("thresholdEstimateMetres=null"));
            Assert.That(console, Does.Not.Match("thresholdEstimateMetres=0(\r|\n|$)"));
            Assert.That(
                console,
                Does.Contain("BUGCAM_BLOCK_1_5_GHOST_EVIDENCE"),
                "GhostEvidenceReport section must agree with metrics.");
        }

        [Test]
        public void FloorDivergentSuccessRetainsFansWithoutThresholdEstimate()
        {
            // Matches PlayMode FastStepCount=40 reality on this tower:
            // DIVERGENT AT SEARCH FLOOR, no threshold, fans retained.
            var searchResult = DriveFloorDivergentSearch();
            Assert.That(Prop<bool>(searchResult, "Succeeded"), Is.True, Prop<string>(searchResult, "ErrorReason"));
            Assert.That(Prop<string>(searchResult, "Verdict"), Is.EqualTo("DIVERGENT AT SEARCH FLOOR"));
            Assert.That(Prop<bool>(searchResult, "HasThresholdEstimate"), Is.False);
            Assert.That(Arr(searchResult, "FanRuns").Length, Is.EqualTo(15));

            var document = BuildDocument(searchResult, Vector3.right);
            Assert.That(Prop<bool>(document, "Success"), Is.True);
            Assert.That(Arr(document, "Fans").Length, Is.EqualTo(15));

            var json = BuildMetricsJson(document);
            var console = FormatHonestConsole(document);
            Assert.That(json, Does.Contain("\"success\":true"));
            Assert.That(json, Does.Contain("\"verdict\":\"DIVERGENT AT SEARCH FLOOR\""));
            Assert.That(json, Does.Contain("\"hasThresholdEstimate\":false"));
            Assert.That(json, Does.Contain("\"thresholdEstimateMetres\":null"));
            Assert.That(json, Does.Contain("\"retainedFanCount\":15"));
            Assert.That(console, Does.Contain("hasThresholdEstimate=False"));
            Assert.That(console, Does.Contain("thresholdEstimateMetres=null"));
            Assert.That(console, Does.Contain("retainedFanCount=15"));
            Assert.That(console, Does.Contain("verdict=DIVERGENT AT SEARCH FLOOR"));
        }

        [Test]
        public void ScreenshotCaptureFailsClosedOnBlankOrWritesDistinctPngs()
        {
            var searchResult = DriveBracketSearch();
            var document = BuildDocument(searchResult, Vector3.right);
            Assert.That(Prop<bool>(document, "HasPrimaryFan"), Is.True);

            var root = Path.Combine(
                Path.GetTempPath(),
                "BugCamGhostShot-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                var captureType = Type.GetType(
                    "BugCam.Editor.GhostScreenshotCapture, BugCam.Editor");
                Assert.That(captureType, Is.Not.Null, "GhostScreenshotCapture must compile.");
                var capture = captureType.GetMethod(
                    "Capture",
                    new[] { document.GetType(), typeof(string) });
                Assert.That(capture, Is.Not.Null);

                var result = capture.Invoke(null, new object[] { document, root });
                var overview = Prop<bool>(result, "OverviewWritten");
                var first = Prop<bool>(result, "FirstDivergenceWritten");
                var max = Prop<bool>(result, "MaxSpreadWritten");
                var finalShot = Prop<bool>(result, "FinalWritten");
                var visuals = Prop<string>(result, "VisualsDirectory");
                var pngs = Directory.Exists(visuals)
                    ? Directory.GetFiles(visuals, "*.png")
                    : Array.Empty<string>();

                var anyWritten = overview || first || max || finalShot;
                if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    Assert.That(anyWritten, Is.False, "Null graphics must omit named PNG visuals.");
                    Assert.That(pngs.Length, Is.EqualTo(0));
                    return;
                }

                Assert.That(anyWritten, Is.True, "GPU capture should retain named PNG visuals.");
                Assert.That(overview, Is.True);
                Assert.That(first, Is.True);
                Assert.That(max, Is.True);
                Assert.That(finalShot, Is.True);
                Assert.That(pngs.Length, Is.EqualTo(4));

                var hashes = new string[pngs.Length];
                for (var i = 0; i < pngs.Length; i++)
                {
                    var bytes = File.ReadAllBytes(pngs[i]);
                    using (var sha = SHA256.Create())
                    {
                        hashes[i] = Convert.ToBase64String(sha.ComputeHash(bytes));
                    }
                }

                Assert.That(
                    hashes.Distinct().Count(),
                    Is.GreaterThan(1),
                    "When visuals are retained they must not be byte-identical copies.");
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
            // Force reference near ceiling so 1.2× exceeds search ceiling (same oracle as Core).
            var searchResult = DriveHighFanOutsideSearch();
            var document = BuildDocument(searchResult, Vector3.right);
            var fans = Arr(document, "Fans");
            var ceiling = Prop<float>(searchResult, "SearchRangeCeilingMetres");
            Assert.That(fans.Length, Is.EqualTo(15));
            Assert.That(ceiling, Is.GreaterThan(0f));

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

            Assert.That(sawOutside, Is.True, "Fixture must include at least one fan ε > ceiling.");
        }

        [Test]
        public void GhostBodyLimitDefaultIsTen()
        {
            var settingsType = Type.GetType("BugCam.Core.DivergenceSettings, BugCam.Core");
            var settings = settingsType.GetMethod("CreateDefault").Invoke(null, null);
            Assert.That(Prop<int>(settings, "GhostBodyLimit"), Is.EqualTo(10));
            UnityEngine.Object.DestroyImmediate((UnityEngine.Object)settings);
        }

        private static object BuildDocument(object searchResult, Vector3 axis, string runId = "test-run")
        {
            var builderType = Type.GetType("BugCam.Evidence.GhostEvidenceBuilder, BugCam.Evidence");
            var identityType = Type.GetType("BugCam.Evidence.GhostSearchIdentity, BugCam.Evidence");
            var settingsType = Type.GetType("BugCam.Core.DivergenceSettings, BugCam.Core");
            var strategyType = Type.GetType("BugCam.Core.EpsilonSearchStrategy, BugCam.Core");
            var envType = Type.GetType("BugCam.Evidence.GhostRunEnvironment, BugCam.Evidence");
            Assert.That(builderType, Is.Not.Null);

            var strategy = Enum.ToObject(strategyType, 0);
            var identity = Activator.CreateInstance(identityType, 49, axis, strategy);
            var settings = settingsType.GetMethod("CreateDefault").Invoke(null, null);
            var environment = Activator.CreateInstance(
                envType,
                "test-unity",
                "test-sha",
                "test-branch",
                "Assets/TestScene.unity");

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
                        typeof(string),
                        envType
                    }).Invoke(
                    null,
                    new object[] { searchResult, identity, settings, null, runId, environment });

                Assert.That(Prop<bool>(build, "Succeeded"), Is.True, Prop<string>(build, "ErrorReason"));
                return Prop<object>(build, "Document");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate((UnityEngine.Object)settings);
            }
        }

        private static object CreateFailureDocument(
            object searchResult,
            string errorCode,
            string errorReason)
        {
            var builderType = Type.GetType("BugCam.Evidence.GhostEvidenceBuilder, BugCam.Evidence");
            var identityType = Type.GetType("BugCam.Evidence.GhostSearchIdentity, BugCam.Evidence");
            var strategyType = Type.GetType("BugCam.Core.EpsilonSearchStrategy, BugCam.Core");
            var envType = Type.GetType("BugCam.Evidence.GhostRunEnvironment, BugCam.Evidence");
            var strategy = Enum.ToObject(strategyType, 0);
            var identity = Activator.CreateInstance(identityType, 49, Vector3.right, strategy);
            var environment = Activator.CreateInstance(
                envType,
                "test-unity",
                "test-sha",
                "test-branch",
                "Assets/TestScene.unity");

            return builderType.GetMethod(
                    "CreateFailureDocument",
                    BindingFlags.Public | BindingFlags.Static)
                .Invoke(
                    null,
                    new object[]
                    {
                        searchResult,
                        identity,
                        errorCode,
                        errorReason,
                        10,
                        "build-failed-run",
                        environment
                    });
        }

        private static void AssertPrimaryUnavailable(string json)
        {
            Assert.That(json, Does.Contain("\"maxSpreadMetres\":null"));
            Assert.That(json, Does.Contain("\"firstDivergenceFrame\":null"));
            Assert.That(json, Does.Contain("\"firstDivergenceBodyId\":null"));
            Assert.That(json, Does.Contain("\"amplification\":null"));
            Assert.That(json, Does.Contain("\"hasSignificantDivergence\":false"));
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

        private static string FormatHonestConsole(object document)
        {
            var writerType = Type.GetType("BugCam.Evidence.GhostEvidenceWriter, BugCam.Evidence");
            return (string)writerType.GetMethod("FormatHonestConsoleReport")
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

        private static object DriveHighFanOutsideSearch()
        {
            var search = CreateSearch();
            return Drive(search, (phase, epsilon, axis, isBaseline) =>
            {
                if (isBaseline)
                {
                    return BaselineOutcome();
                }

                // Only the search ceiling diverges → reference ≈ ceiling so 1.2× exceeds it.
                var diverged = epsilon >= 1e-2f - 1e-12f;
                return NeedsFrames(phase) ? Framed(diverged, epsilon, axis) : Compact(diverged);
            });
        }

        private static object DriveFloorDivergentSearch()
        {
            var search = CreateSearch();
            return Drive(search, (phase, epsilon, axis, isBaseline) =>
            {
                if (isBaseline)
                {
                    return BaselineOutcome();
                }

                // Every perturbed sample diverges — no stable lower bound / no threshold.
                return NeedsFrames(phase) ? Framed(true, epsilon, axis) : Compact(true);
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
            // Marker split (when diverged): FirstDivergenceBodyId=2 (argmax |Δpos| at frame 2),
            // MaxSpreadBodyId=49 (global max later), AffectedBodyIds[0]=1 (ID-sorted).
            const int steps = 12;
            const int bodies = 3;
            var frames = new float[steps * bodies * Stride];
            for (var step = 0; step < steps; step++)
            {
                for (var body = 0; body < bodies; body++)
                {
                    var offset = ((step * bodies) + body) * Stride;
                    frames[offset] = body * 0.1f;
                    frames[offset + 1] = 0f;
                    frames[offset + 2] = 0f;
                    frames[offset + 6] = 1f;
                }
            }

            if (diverged)
            {
                for (var step = 2; step < steps; step++)
                {
                    // body0 id=1, body1 id=2, body2 id=49
                    SetY(frames, step, 0, bodies, 0.5f);
                    SetY(frames, step, 1, bodies, step < 7 ? 1.2f : 1.2f);
                    SetY(frames, step, 2, bodies, step < 7 ? 0.4f : 5.0f);
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

        private static void SetY(float[] frames, int step, int body, int bodies, float y)
        {
            frames[(((step * bodies) + body) * Stride) + 1] = y;
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
