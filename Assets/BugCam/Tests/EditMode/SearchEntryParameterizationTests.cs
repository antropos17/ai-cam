#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BugCam.Tests
{
    /// <summary>
    /// Block 2.2.1 A1 pins (docs/CONTRACT-2.2.1.md): the ratified validation table with
    /// its verbatim reason strings, source precedence window &gt; asset &gt; defaults,
    /// bit-exact mm↔m round-trip of the code defaults, the single settings-construction
    /// path (no CreateDefault in host/window, exactly one in the resolver), no silent
    /// step-count reset, and SessionState persistence of the full entry.
    /// </summary>
    public sealed class SearchEntryParameterizationTests
    {
        // Verbatim ratified UI literals — changing them is a contract change, not a rename.
        private const string ReasonFloor =
            "ниже гейта воспроизводимости 1e-6 м эффект неотличим от шума измерения";
        private const string ReasonCeiling =
            "возмущение крупнее 1 м не является малым";
        private const string ReasonRatio =
            "диапазон вырожден для 12-точечной лестницы";
        private const string ReasonSteps =
            "число шагов должно быть положительным";

        private const string TempAssetPath =
            "Assets/BugCam/Tests/EditMode/TempA1SettingsAsset.asset";

        private static Type EntryType()
        {
            var type = Type.GetType("BugCam.Editor.GhostSearchEntry, BugCam.Editor");
            Assert.That(type, Is.Not.Null);
            return type;
        }

        private static Type ResolverType()
        {
            var type = Type.GetType("BugCam.Editor.GhostSearchEntryResolver, BugCam.Editor");
            Assert.That(type, Is.Not.Null);
            return type;
        }

        private static Type SettingsType()
        {
            var type = Type.GetType("BugCam.Core.DivergenceSettings, BugCam.Core");
            Assert.That(type, Is.Not.Null);
            return type;
        }

        private static object MakeEntry(
            int stepCount,
            int targetBodyId,
            string assetGuid,
            bool hasFloor,
            float floorMetres,
            bool hasCeiling,
            float ceilingMetres)
        {
            var strategyType = Type.GetType("BugCam.Core.EpsilonSearchStrategy, BugCam.Core");
            var strategy = Enum.ToObject(strategyType, 0);
            return Activator.CreateInstance(
                EntryType(),
                stepCount,
                strategy,
                Vector3.right,
                targetBodyId,
                assetGuid,
                hasFloor,
                floorMetres,
                hasCeiling,
                ceilingMetres);
        }

        private static object Resolve(object entry)
        {
            var resolve = ResolverType().GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static);
            Assert.That(resolve, Is.Not.Null);
            return resolve.Invoke(null, new[] { entry });
        }

        private static T Prop<T>(object instance, string name)
        {
            var prop = instance.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(prop, Is.Not.Null, name + " must exist.");
            return (T)prop.GetValue(instance);
        }

        private static float Const(string name)
        {
            var field = SettingsType().GetField(name, BindingFlags.Public | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, name + " must exist.");
            return (float)field.GetRawConstantValue();
        }

        [Test]
        public void ValidationTableBoundsAndVerbatimReasons()
        {
            // Floor below the 1e-6 m gate — verbatim reason, invalid.
            var low = Resolve(MakeEntry(32, 49, "", true, 0.9e-6f, false, 0f));
            Assert.That(Prop<bool>(low, "IsValid"), Is.False);
            Assert.That(Prop<string>(low, "FloorReason"), Is.EqualTo(ReasonFloor));

            // Floor exactly at the boundary is valid (contract: ≥ 1e-6 м).
            var floorEdge = Resolve(MakeEntry(32, 49, "", true, 1e-6f, false, 0f));
            Assert.That(Prop<string>(floorEdge, "FloorReason"), Is.Empty);
            Assert.That(Prop<bool>(floorEdge, "IsValid"), Is.True);

            // Ceiling above 1 m — verbatim reason, invalid.
            var big = Resolve(MakeEntry(32, 49, "", false, 0f, true, 1.5f));
            Assert.That(Prop<bool>(big, "IsValid"), Is.False);
            Assert.That(Prop<string>(big, "CeilingReason"), Is.EqualTo(ReasonCeiling));

            // Ceiling exactly at 1 m is valid (contract: ≤ 1 м).
            var ceilingEdge = Resolve(MakeEntry(32, 49, "", false, 0f, true, 1f));
            Assert.That(Prop<string>(ceilingEdge, "CeilingReason"), Is.Empty);
            Assert.That(Prop<bool>(ceilingEdge, "IsValid"), Is.True);

            // Degenerate ratio (< 1.001) — verbatim ladder reason.
            var degenerate = Resolve(MakeEntry(32, 49, "", true, 1e-3f, true, 1.0005e-3f));
            Assert.That(Prop<bool>(degenerate, "IsValid"), Is.False);
            Assert.That(Prop<string>(degenerate, "RatioReason"), Is.EqualTo(ReasonRatio));

            var okRatio = Resolve(MakeEntry(32, 49, "", true, 1e-3f, true, 1.002e-3f));
            Assert.That(Prop<string>(okRatio, "RatioReason"), Is.Empty);
            Assert.That(Prop<bool>(okRatio, "IsValid"), Is.True);

            // Steps must be a positive integer — verbatim reason; no silent reset anywhere.
            var zeroSteps = Resolve(MakeEntry(0, 49, "", false, 0f, false, 0f));
            Assert.That(Prop<bool>(zeroSteps, "IsValid"), Is.False);
            Assert.That(Prop<string>(zeroSteps, "StepsReason"), Is.EqualTo(ReasonSteps));

            var oneStep = Resolve(MakeEntry(1, 49, "", false, 0f, false, 0f));
            Assert.That(Prop<string>(oneStep, "StepsReason"), Is.Empty);
            Assert.That(Prop<bool>(oneStep, "IsValid"), Is.True);

            // Stale target id fails closed (the dropdown never offers an invalid one).
            var badTarget = Resolve(MakeEntry(32, 999, "", false, 0f, false, 0f));
            Assert.That(Prop<bool>(badTarget, "IsValid"), Is.False);
            Assert.That(Prop<string>(badTarget, "TargetReason"), Is.Not.Empty);
        }

        [Test]
        public void SourcePrecedenceWindowOverAssetOverDefaults()
        {
            var defaultFloor = Const("DefaultEpsilonStart");
            var defaultCeiling = Const("DefaultEpsilonCeiling");

            // Defaults: no asset, no overrides.
            var defaults = Resolve(MakeEntry(32, 49, "", false, 0f, false, 0f));
            Assert.That(Prop<bool>(defaults, "IsValid"), Is.True);
            Assert.That(Prop<float>(defaults, "EffectiveFloorMetres"), Is.EqualTo(defaultFloor));
            Assert.That(Prop<float>(defaults, "EffectiveCeilingMetres"), Is.EqualTo(defaultCeiling));
            Assert.That(Prop<string>(defaults, "SourceKind"), Is.EqualTo("defaults"));

            // Window override beats defaults.
            var window = Resolve(MakeEntry(32, 49, "", true, 2e-5f, false, 0f));
            Assert.That(Prop<float>(window, "EffectiveFloorMetres"), Is.EqualTo(2e-5f));
            Assert.That(Prop<float>(window, "EffectiveCeilingMetres"), Is.EqualTo(defaultCeiling));
            Assert.That(Prop<string>(window, "SourceKind"), Is.EqualTo("defaults+window"));

            // Asset beats defaults; window override beats the asset.
            var settings = ScriptableObject.CreateInstance(SettingsType()) as ScriptableObject;
            try
            {
                AssetDatabase.CreateAsset(settings, TempAssetPath);
                var so = new SerializedObject(settings);
                so.FindProperty("epsilonStart").floatValue = 5e-5f;
                so.FindProperty("epsilonCeiling").floatValue = 5e-3f;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                var guid = AssetDatabase.AssetPathToGUID(TempAssetPath);
                Assert.That(guid, Is.Not.Empty);

                var fromAsset = Resolve(MakeEntry(32, 49, guid, false, 0f, false, 0f));
                Assert.That(Prop<bool>(fromAsset, "IsValid"), Is.True);
                Assert.That(Prop<float>(fromAsset, "EffectiveFloorMetres"), Is.EqualTo(5e-5f));
                Assert.That(Prop<float>(fromAsset, "EffectiveCeilingMetres"), Is.EqualTo(5e-3f));
                Assert.That(Prop<string>(fromAsset, "SourceKind"), Is.EqualTo("asset"));

                var overridden = Resolve(MakeEntry(32, 49, guid, true, 7e-5f, false, 0f));
                Assert.That(Prop<float>(overridden, "EffectiveFloorMetres"), Is.EqualTo(7e-5f));
                Assert.That(Prop<float>(overridden, "EffectiveCeilingMetres"), Is.EqualTo(5e-3f));
                Assert.That(Prop<string>(overridden, "SourceKind"), Is.EqualTo("asset+window"));
            }
            finally
            {
                AssetDatabase.DeleteAsset(TempAssetPath);
            }
        }

        [Test]
        public void MissingAssetFailsClosedWithoutSilentDefaults()
        {
            var missing = Resolve(MakeEntry(
                32, 49, "00000000000000000000000000000000", false, 0f, false, 0f));
            Assert.That(Prop<bool>(missing, "IsValid"), Is.False);
            Assert.That(Prop<string>(missing, "AssetReason"), Does.Contain("не найден"));

            // The runner-side constructor refuses too — never a silent defaults fallback.
            var tryCreate = ResolverType().GetMethod(
                "TryCreateRuntimeSettings",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(tryCreate, Is.Not.Null);
            var args = new object[] { missing, null, null, null };
            var created = (bool)tryCreate.Invoke(null, args);
            Assert.That(created, Is.False, "Invalid resolution must not create settings.");
            Assert.That((string)args[3], Is.Not.Empty);
        }

        [Test]
        public void DefaultEntryRuntimeSettingsMatchCreateDefault()
        {
            // Precondition of the bit-identity gate: the resolver's default path yields the
            // exact search settings CreateDefault produced before parameterization.
            var resolution = Resolve(MakeEntry(32, 49, "", false, 0f, false, 0f));
            var tryCreate = ResolverType().GetMethod(
                "TryCreateRuntimeSettings",
                BindingFlags.Public | BindingFlags.Static);
            var args = new object[] { resolution, null, null, null };
            Assert.That((bool)tryCreate.Invoke(null, args), Is.True);
            var effective = args[2];

            var createDefault = SettingsType().GetMethod(
                "CreateDefault", BindingFlags.Public | BindingFlags.Static);
            var reference = createDefault.Invoke(null, null);
            var toSearch = SettingsType().GetMethod("ToSearchSettings");
            var expected = toSearch.Invoke(reference, null);

            foreach (var name in new[]
                     {
                         "EpsilonStartMetres",
                         "EpsilonGrowthFactor",
                         "EpsilonCeilingMetres"
                     })
            {
                Assert.That(
                    Prop<float>(effective, name),
                    Is.EqualTo(Prop<float>(expected, name)),
                    name + " must match the pre-A1 default path bit-for-bit.");
            }

            foreach (var name in new[] { "BisectionIterations", "LadderPointCount" })
            {
                Assert.That(
                    Prop<int>(effective, name),
                    Is.EqualTo(Prop<int>(expected, name)),
                    name + " must match the pre-A1 default path.");
            }

            Assert.That(
                Prop<float[]>(effective, "FanMultipliers"),
                Is.EqualTo(Prop<float[]>(expected, "FanMultipliers")),
                "FanMultipliers must match the pre-A1 default path.");
        }

        [Test]
        public void MillimetreRoundTripIsBitExactForDefaultsAndInvariant()
        {
            var toText = ResolverType().GetMethod(
                "MillimetresTextFromMetres", BindingFlags.Public | BindingFlags.Static);
            var tryParse = ResolverType().GetMethod(
                "TryParseMillimetresToMetres", BindingFlags.Public | BindingFlags.Static);
            Assert.That(toText, Is.Not.Null);
            Assert.That(tryParse, Is.Not.Null);

            foreach (var metres in new[]
                     {
                         Const("DefaultEpsilonStart"),
                         Const("DefaultEpsilonCeiling"),
                         1e-6f,
                         1f,
                         3.79e-3f,
                         1.98919879e-05f
                     })
            {
                var text = (string)toText.Invoke(null, new object[] { metres });
                Assert.That(text, Does.Not.Contain(","), "Invariant culture: no comma.");
                var args = new object[] { text, null };
                Assert.That((bool)tryParse.Invoke(null, args), Is.True, "parse " + text);
                Assert.That(
                    (float)args[1],
                    Is.EqualTo(metres),
                    "mm round-trip must be bit-exact for " + text);
            }

            foreach (var bad in new[] { "abc", "0,5", "NaN", "Infinity", "" })
            {
                var args = new object[] { bad, null };
                Assert.That(
                    (bool)tryParse.Invoke(null, args),
                    Is.False,
                    "'" + bad + "' must be rejected (fail-closed input).");
            }
        }

        [Test]
        public void DisplayShowsShortestRoundTripTextForNonExactFloats()
        {
            // Review decision 2026-08-03: the input field displays the shortest plain
            // round-trip representation of the stored metres — what the user typed is what
            // they see — while storage stays bit-exact. Values chosen for non-exact float
            // representation (the old full-"R" display showed "0.000100000005" etc.).
            var toText = ResolverType().GetMethod(
                "MillimetresTextFromMetres", BindingFlags.Public | BindingFlags.Static);
            var tryParse = ResolverType().GetMethod(
                "TryParseMillimetresToMetres", BindingFlags.Public | BindingFlags.Static);

            foreach (var pair in new[]
                     {
                         new[] { "0.0001", "0.0001" },
                         new[] { "0.002", "0.002" },
                         new[] { "0.1234", "0.1234" },
                         new[] { "7.3", "7.3" },
                         new[] { "10", "10" },
                         new[] { "0.01", "0.01" }
                     })
            {
                var parseArgs = new object[] { pair[0], null };
                Assert.That((bool)tryParse.Invoke(null, parseArgs), Is.True, pair[0]);
                var metres = (float)parseArgs[1];

                var display = (string)toText.Invoke(null, new object[] { metres });
                Assert.That(
                    display,
                    Is.EqualTo(pair[1]),
                    "typed '" + pair[0] + "' must display as '" + pair[1] + "'");

                // Display → parse must still recover the stored metres bit-exactly.
                var backArgs = new object[] { display, null };
                Assert.That((bool)tryParse.Invoke(null, backArgs), Is.True);
                Assert.That((float)backArgs[1], Is.EqualTo(metres),
                    "display text must round-trip to the stored value");
            }
        }

        [Test]
        public void SingleSettingsConstructionPathAndNoSilentStepReset()
        {
            var hostCs = File.ReadAllText(Path.Combine(
                Application.dataPath, "BugCam", "Editor", "GhostEvidencePlayModeHost.cs"));
            var windowCs = File.ReadAllText(Path.Combine(
                Application.dataPath, "BugCam", "Editor", "GhostVisualizationWindow.cs"));
            var resolverCs = File.ReadAllText(Path.Combine(
                Application.dataPath, "BugCam", "Editor", "GhostSearchEntry.cs"));

            Assert.That(
                CountOf(hostCs, "CreateDefault("),
                Is.EqualTo(0),
                "CreateDefault() inside the runner is dead (contract A1) — the resolver is the single path.");
            Assert.That(
                CountOf(windowCs, "CreateDefault("),
                Is.EqualTo(0),
                "The window must read effective values through the resolver, not CreateDefault().");
            Assert.That(
                CountOf(resolverCs, "CreateDefault("),
                Is.EqualTo(1),
                "Exactly one settings-construction call site lives in the resolver.");

            Assert.That(
                CountOf(windowCs, "_stepCount = DefaultRunStepCount"),
                Is.EqualTo(1),
                "Only the field initializer may mention the default step count — " +
                "no silent reset to 32 on invalid input (contract table).");
        }

        [Test]
        public void HostPersistsFullEntryInSessionState()
        {
            var hostType = Type.GetType("BugCam.Editor.GhostEvidencePlayModeHost, BugCam.Editor");
            Assert.That(hostType, Is.Not.Null);
            var allow = hostType.GetField(
                "AllowPlayModeEntry", BindingFlags.NonPublic | BindingFlags.Static);
            var tryStart = hostType.GetMethod(
                "TryStartTowerSearch", BindingFlags.Public | BindingFlags.Static);
            Assert.That(allow, Is.Not.Null);
            Assert.That(tryStart, Is.Not.Null);

            var previousAllow = (bool)allow.GetValue(null);
            SessionState.SetBool("BugCam.GhostSearch.Busy", false);
            SessionState.SetBool("BugCam.GhostHost.Pending", false);
            try
            {
                allow.SetValue(null, false);
                var entry = MakeEntry(17, 7, "", true, 2e-5f, true, 5e-3f);
                var args = new object[] { entry, "a1-persistence-test", null };
                Assert.That((bool)tryStart.Invoke(null, args), Is.True, (string)args[2]);

                Assert.That(SessionState.GetInt("BugCam.GhostHost.StepCount", -1), Is.EqualTo(17));
                Assert.That(SessionState.GetInt("BugCam.GhostHost.TargetBodyId", -1), Is.EqualTo(7));
                Assert.That(
                    SessionState.GetString("BugCam.GhostHost.SettingsAssetGuid", "x"),
                    Is.Empty);
                Assert.That(
                    SessionState.GetBool("BugCam.GhostHost.HasFloorOverride", false),
                    Is.True);
                Assert.That(
                    SessionState.GetFloat("BugCam.GhostHost.FloorOverrideMetres", 0f),
                    Is.EqualTo(2e-5f));
                Assert.That(
                    SessionState.GetBool("BugCam.GhostHost.HasCeilingOverride", false),
                    Is.True);
                Assert.That(
                    SessionState.GetFloat("BugCam.GhostHost.CeilingOverrideMetres", 0f),
                    Is.EqualTo(5e-3f));
            }
            finally
            {
                allow.SetValue(null, previousAllow);
                SessionState.SetBool("BugCam.GhostSearch.Busy", false);
                SessionState.SetBool("BugCam.GhostHost.Pending", false);
                SessionState.SetBool("BugCam.GhostHost.HasFloorOverride", false);
                SessionState.SetBool("BugCam.GhostHost.HasCeilingOverride", false);
                SessionState.SetInt(
                    "BugCam.GhostHost.TargetBodyId", 49);
                SessionState.SetInt("BugCam.GhostHost.StepCount", 32);
            }
        }

        private static int CountOf(string haystack, string needle)
        {
            var count = 0;
            var index = 0;
            while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += needle.Length;
            }

            return count;
        }
    }
}
#endif
