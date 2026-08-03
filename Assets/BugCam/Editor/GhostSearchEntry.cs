#if UNITY_EDITOR
using System.Globalization;
using BugCam.Core;
using UnityEditor;
using UnityEngine;

namespace BugCam.Editor
{
    /// <summary>
    /// Block 2.2.1 A1 search-entry parameters (docs/CONTRACT-2.2.1.md). Carried from the
    /// window / menu into <see cref="GhostEvidencePlayModeHost"/> and persisted through
    /// SessionState across the Play Mode domain reload. The settings asset travels as a
    /// GUID; epsilon overrides are canonical metres (millimetres exist only at the
    /// display layer).
    /// </summary>
    public readonly struct GhostSearchEntry
    {
        public GhostSearchEntry(
            int stepCount,
            EpsilonSearchStrategy strategy,
            Vector3 searchAxis,
            int targetBodyId,
            string settingsAssetGuid,
            bool hasFloorOverride,
            float floorOverrideMetres,
            bool hasCeilingOverride,
            float ceilingOverrideMetres)
        {
            StepCount = stepCount;
            Strategy = strategy;
            SearchAxis = searchAxis;
            TargetBodyId = targetBodyId;
            SettingsAssetGuid = settingsAssetGuid ?? string.Empty;
            HasFloorOverride = hasFloorOverride;
            FloorOverrideMetres = floorOverrideMetres;
            HasCeilingOverride = hasCeilingOverride;
            CeilingOverrideMetres = ceilingOverrideMetres;
        }

        public int StepCount { get; }

        public EpsilonSearchStrategy Strategy { get; }

        public Vector3 SearchAxis { get; }

        public int TargetBodyId { get; }

        /// <summary>Empty = no asset assigned (defaults source).</summary>
        public string SettingsAssetGuid { get; }

        public bool HasFloorOverride { get; }

        /// <summary>Metres, full float precision.</summary>
        public float FloorOverrideMetres { get; }

        public bool HasCeilingOverride { get; }

        /// <summary>Metres, full float precision.</summary>
        public float CeilingOverrideMetres { get; }

        /// <summary>
        /// Default tower entry: projectile target, no asset, no overrides — the exact
        /// configuration whose numbers are pinned to the Block 2.2 gate.
        /// </summary>
        public static GhostSearchEntry Tower(
            int stepCount,
            EpsilonSearchStrategy strategy,
            Vector3 searchAxis)
        {
            return new GhostSearchEntry(
                stepCount,
                strategy,
                searchAxis,
                GhostSearchTargetCatalog.TowerDefaultTargetBodyId,
                string.Empty,
                false,
                0f,
                false,
                0f);
        }
    }

    /// <summary>One selectable perturbation target.</summary>
    public readonly struct GhostSearchTargetOption
    {
        public GhostSearchTargetOption(int bodyId, string displayName)
        {
            BodyId = bodyId;
            DisplayName = displayName ?? string.Empty;
        }

        public int BodyId { get; }

        public string DisplayName { get; }
    }

    /// <summary>
    /// Display-name provider for the target dropdown (contract amendment 2026-08-03):
    /// the window consumes options, never hardcoded tower strings — after A2 a
    /// scene-capture provider feeds the same dropdown without rework.
    /// </summary>
    public static class GhostSearchTargetCatalog
    {
        /// <summary>The tower projectile — the Block 2.2 gate target.</summary>
        public const int TowerDefaultTargetBodyId = 49;

        /// <summary>
        /// Options derived from the procedural tower definition (stable IDs 1…48 bricks,
        /// 49 projectile) — the id set comes from the factory, not from literals here.
        /// </summary>
        public static GhostSearchTargetOption[] TowerOptions()
        {
            var bodies = TowerProbeRequestFactory.CreateBaseline(1).Bodies;
            var options = new GhostSearchTargetOption[bodies.Length];
            for (var i = 0; i < bodies.Length; i++)
            {
                var id = bodies[i].StableId;
                options[i] = new GhostSearchTargetOption(
                    id,
                    id == TowerDefaultTargetBodyId
                        ? "снаряд — body " + id.ToString(CultureInfo.InvariantCulture)
                        : "кирпич — body " + id.ToString(CultureInfo.InvariantCulture));
            }

            return options;
        }

        public static bool Contains(GhostSearchTargetOption[] options, int bodyId)
        {
            if (options == null)
            {
                return false;
            }

            for (var i = 0; i < options.Length; i++)
            {
                if (options[i].BodyId == bodyId)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Resolution of a <see cref="GhostSearchEntry"/> against the ratified validation
    /// table and source precedence (window override &gt; asset &gt; defaults). Reason
    /// strings are the verbatim UI literals from docs/CONTRACT-2.2.1.md — empty when the
    /// field is valid. Fail-closed: an assigned-but-missing asset never falls back to
    /// defaults silently.
    /// </summary>
    public sealed class GhostSearchEntryResolution
    {
        internal GhostSearchEntryResolution(
            bool isValid,
            string floorReason,
            string ceilingReason,
            string ratioReason,
            string stepsReason,
            string targetReason,
            string assetReason,
            string firstReason,
            float effectiveFloorMetres,
            float effectiveCeilingMetres,
            string sourceKind,
            string sourceDescription,
            string assetName,
            DivergenceSettings asset)
        {
            IsValid = isValid;
            FloorReason = floorReason ?? string.Empty;
            CeilingReason = ceilingReason ?? string.Empty;
            RatioReason = ratioReason ?? string.Empty;
            StepsReason = stepsReason ?? string.Empty;
            TargetReason = targetReason ?? string.Empty;
            AssetReason = assetReason ?? string.Empty;
            FirstReason = firstReason ?? string.Empty;
            EffectiveFloorMetres = effectiveFloorMetres;
            EffectiveCeilingMetres = effectiveCeilingMetres;
            SourceKind = sourceKind ?? string.Empty;
            SourceDescription = sourceDescription ?? string.Empty;
            AssetName = assetName ?? string.Empty;
            Asset = asset;
        }

        public bool IsValid { get; }

        public string FloorReason { get; }

        public string CeilingReason { get; }

        public string RatioReason { get; }

        public string StepsReason { get; }

        public string TargetReason { get; }

        public string AssetReason { get; }

        /// <summary>First violated reason in table order — the disabled-button row.</summary>
        public string FirstReason { get; }

        public float EffectiveFloorMetres { get; }

        public float EffectiveCeilingMetres { get; }

        /// <summary>"defaults" | "asset" | "defaults+window" | "asset+window".</summary>
        public string SourceKind { get; }

        /// <summary>Human source line, e.g. «ассет "X" + правка окна (потолок)».</summary>
        public string SourceDescription { get; }

        public string AssetName { get; }

        /// <summary>Loaded asset (read-only use), null when none assigned or missing.</summary>
        public DivergenceSettings Asset { get; }
    }

    /// <summary>
    /// The single settings-construction path for the search pipeline (contract: the
    /// default-settings factory is never called by the runner or window — a source scan
    /// pins host/window at zero call sites and this file at exactly one).
    /// </summary>
    public static class GhostSearchEntryResolver
    {
        // Verbatim ratified reason strings (docs/CONTRACT-2.2.1.md validation table).
        public const string ReasonFloor =
            "ниже гейта воспроизводимости 1e-6 м эффект неотличим от шума измерения";
        public const string ReasonCeiling =
            "возмущение крупнее 1 м не является малым";
        public const string ReasonRatio =
            "диапазон вырожден для 12-точечной лестницы";
        public const string ReasonSteps =
            "число шагов должно быть положительным";

        public static GhostSearchEntryResolution Resolve(in GhostSearchEntry entry)
        {
            DivergenceSettings asset = null;
            var assetName = string.Empty;
            var assetReason = string.Empty;
            var assetAssigned = !string.IsNullOrEmpty(entry.SettingsAssetGuid);
            if (assetAssigned)
            {
                var path = AssetDatabase.GUIDToAssetPath(entry.SettingsAssetGuid);
                asset = string.IsNullOrEmpty(path)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<DivergenceSettings>(path);
                if (asset == null)
                {
                    assetReason =
                        "ассет настроек (GUID " + entry.SettingsAssetGuid +
                        ") не найден — тихий откат к дефолтам запрещён; назначьте ассет " +
                        "заново или очистите поле";
                }
                else
                {
                    assetName = asset.name;
                }
            }

            var floor = entry.HasFloorOverride
                ? entry.FloorOverrideMetres
                : (asset != null ? asset.EpsilonStart : DivergenceSettings.DefaultEpsilonStart);
            var ceiling = entry.HasCeilingOverride
                ? entry.CeilingOverrideMetres
                : (asset != null ? asset.EpsilonCeiling : DivergenceSettings.DefaultEpsilonCeiling);

            var floorReason = IsFinite(floor) &&
                              floor >= DivergenceSettings.MinSearchEpsilonFloorMetres
                ? string.Empty
                : AttributeToAsset(ReasonFloor, "epsilonStart", assetName,
                    asset != null && !entry.HasFloorOverride);
            var ceilingReason = IsFinite(ceiling) &&
                                ceiling > floor &&
                                ceiling <= DivergenceSettings.MaxSearchEpsilonCeilingMetres
                ? string.Empty
                : AttributeToAsset(ReasonCeiling, "epsilonCeiling", assetName,
                    asset != null && !entry.HasCeilingOverride);
            // Ratio row is meaningful only once both bounds individually pass.
            var ratioReason = string.Empty;
            if (floorReason.Length == 0 && ceilingReason.Length == 0 &&
                !(ceiling / floor >= DivergenceSettings.MinSearchCeilingToFloorRatio))
            {
                ratioReason = ReasonRatio;
            }

            var stepsReason = entry.StepCount >= 1 ? string.Empty : ReasonSteps;

            var targetReason = GhostSearchTargetCatalog.Contains(
                GhostSearchTargetCatalog.TowerOptions(), entry.TargetBodyId)
                ? string.Empty
                : "цель body " + entry.TargetBodyId.ToString(CultureInfo.InvariantCulture) +
                  " отсутствует в наборе тел сцены";

            // Non-epsilon search fields always come from the asset (or defaults); their
            // invalidity is attributed to the asset with the field named by Validate().
            if (assetReason.Length == 0 && asset != null)
            {
                var structError = asset.ValidateSearchSettings();
                if (structError.Length != 0 && IsNonEpsilonStructError(structError))
                {
                    assetReason = "ассет \"" + assetName + "\": " + structError;
                }
            }

            var isValid =
                floorReason.Length == 0 &&
                ceilingReason.Length == 0 &&
                ratioReason.Length == 0 &&
                stepsReason.Length == 0 &&
                targetReason.Length == 0 &&
                assetReason.Length == 0;

            var firstReason = floorReason.Length != 0 ? floorReason
                : ceilingReason.Length != 0 ? ceilingReason
                : ratioReason.Length != 0 ? ratioReason
                : stepsReason.Length != 0 ? stepsReason
                : targetReason.Length != 0 ? targetReason
                : assetReason;

            BuildSource(
                assetAssigned,
                asset != null,
                assetName,
                entry.HasFloorOverride,
                entry.HasCeilingOverride,
                out var sourceKind,
                out var sourceDescription);

            return new GhostSearchEntryResolution(
                isValid,
                floorReason,
                ceilingReason,
                ratioReason,
                stepsReason,
                targetReason,
                assetReason,
                firstReason,
                floor,
                ceiling,
                sourceKind,
                sourceDescription,
                assetName,
                asset);
        }

        /// <summary>
        /// Runner-side settings construction — fail-closed on an invalid resolution, never
        /// a silent fall-back. The returned base settings instance is the loaded asset
        /// (read-only) or a fresh defaults instance; effective epsilon bounds live in the
        /// returned struct only, so the project asset is never mutated.
        /// </summary>
        public static bool TryCreateRuntimeSettings(
            GhostSearchEntryResolution resolution,
            out DivergenceSettings baseSettings,
            out EpsilonSearchSettings effectiveSearchSettings,
            out string failReason)
        {
            baseSettings = null;
            effectiveSearchSettings = default;
            if (resolution == null || !resolution.IsValid)
            {
                failReason = resolution == null
                    ? "Search entry resolution is required."
                    : resolution.FirstReason;
                return false;
            }

            baseSettings = resolution.Asset != null
                ? resolution.Asset
                : DivergenceSettings.CreateDefault();
            effectiveSearchSettings = new EpsilonSearchSettings(
                resolution.EffectiveFloorMetres,
                baseSettings.EpsilonGrowthFactor,
                resolution.EffectiveCeilingMetres,
                baseSettings.BisectionIterations,
                baseSettings.LadderPointCount,
                baseSettings.FanMultipliers);
            failReason = string.Empty;
            return true;
        }

        /// <summary>
        /// Millimetre display text for a canonical metre value: the SHORTEST plain decimal
        /// string that parses back (via <see cref="TryParseMillimetresToMetres"/>) to the
        /// bit-identical stored metres — so what the user typed is what they see
        /// ("0.0001", never "0.000100000005"). Display layer only; storage, manifest and
        /// evidence keep full precision (review decision 2026-08-03, A1).
        /// </summary>
        public static string MillimetresTextFromMetres(float metres)
        {
            var mm = (double)metres * 1000d;
            for (var digits = 1; digits <= 17; digits++)
            {
                var text = mm.ToString("G" + digits.ToString(CultureInfo.InvariantCulture),
                    CultureInfo.InvariantCulture);
                if (text.IndexOf('E') >= 0 || text.IndexOf('e') >= 0)
                {
                    // Prefer plain notation; a longer digit count renders it plain.
                    continue;
                }

                if (TryParseMillimetresToMetres(text, out var back) && back == metres)
                {
                    return text;
                }
            }

            return mm.ToString("R", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Invariant-culture millimetre input → canonical metres. Round-trips the code
        /// defaults bit-exactly (pinned by test). False on non-numeric input — the caller
        /// keeps the raw text and reports the field's verbatim table reason (fail-closed,
        /// no silent revert).
        /// </summary>
        public static bool TryParseMillimetresToMetres(string text, out float metres)
        {
            metres = 0f;
            double mm;
            if (!double.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out mm))
            {
                return false;
            }

            if (double.IsNaN(mm) || double.IsInfinity(mm))
            {
                return false;
            }

            metres = (float)(mm / 1000d);
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        /// <summary>
        /// The contract asset row: when the offending value comes from the asset (not a
        /// window override), the reason names the asset and the concrete field.
        /// </summary>
        private static string AttributeToAsset(
            string tableReason,
            string fieldName,
            string assetName,
            bool valueFromAsset)
        {
            return valueFromAsset
                ? "ассет \"" + assetName + "\", поле " + fieldName + ": " + tableReason
                : tableReason;
        }

        /// <summary>
        /// Epsilon bound errors are covered by the table rows above; only the remaining
        /// struct invariants (growth factor, ladder, bisection, fan multipliers) get
        /// attributed here to avoid double-reporting the same violation.
        /// </summary>
        private static bool IsNonEpsilonStructError(string structError)
        {
            return !structError.StartsWith("Epsilon", System.StringComparison.Ordinal) ||
                   structError.StartsWith("EpsilonGrowthFactor", System.StringComparison.Ordinal);
        }

        private static void BuildSource(
            bool assetAssigned,
            bool assetFound,
            string assetName,
            bool floorOverridden,
            bool ceilingOverridden,
            out string sourceKind,
            out string sourceDescription)
        {
            var overridden = floorOverridden || ceilingOverridden;
            var baseKind = assetAssigned ? "asset" : "defaults";
            sourceKind = overridden ? baseKind + "+window" : baseKind;

            string baseText;
            if (!assetAssigned)
            {
                baseText = "дефолты";
            }
            else if (assetFound)
            {
                baseText = "ассет \"" + assetName + "\"";
            }
            else
            {
                baseText = "ассет (не найден)";
            }

            if (!overridden)
            {
                sourceDescription = baseText;
                return;
            }

            string overrideText;
            if (floorOverridden && ceilingOverridden)
            {
                overrideText = "обе границы";
            }
            else if (floorOverridden)
            {
                overrideText = "нижняя граница";
            }
            else
            {
                overrideText = "потолок";
            }

            sourceDescription = baseText + " + правка окна (" + overrideText + ")";
        }
    }
}
#endif
