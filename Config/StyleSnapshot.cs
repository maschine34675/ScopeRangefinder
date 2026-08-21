using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace ScopeRangefinder
{
    internal sealed class StyleSnapshot
    {
        public DistanceUnit DistanceUnit;
        public bool ShowUnitSuffix;
        public bool UseDecimalFormat;
        public bool ShowZeroLine;
        public BallisticsLineMode BallisticsLine;
        public HoldUnit BallisticsHoldUnit;
        public string RangeLinePrefix;
        public string ZeroLinePrefix;
        public string NoDistanceText;

        public Color TextColor;
        public ScopeFontSource FontSource;
        public string FontName;
        public string CustomFontFile;
        public float TextThickness;
        public float TextSpacing;
        public float TextGlow;
        public float TextOutline;
        public float TextAberration;
        public float TextOffsetY;

        public bool BackgroundVisible;
        public float BackgroundWidth;
        public float BackgroundHeight;
        public Color BackgroundColor;
        public static StyleSnapshot FromDefaults()
        {
            return new StyleSnapshot
            {
                DistanceUnit = Default(Plugin.DistanceUnit),
                ShowUnitSuffix = Default(Plugin.ShowUnitSuffix),
                UseDecimalFormat = Default(Plugin.UseDecimalFormat),
                ShowZeroLine = Default(Plugin.ShowZeroLine),
                BallisticsLine = Default(Plugin.BallisticsLine),
                BallisticsHoldUnit = Default(Plugin.BallisticsHoldUnit),
                RangeLinePrefix = Default(Plugin.RangeLinePrefix),
                ZeroLinePrefix = Default(Plugin.ZeroLinePrefix),
                NoDistanceText = Default(Plugin.NoDistanceText),
                TextColor = Default(Plugin.ScopeWorldTextColor),
                FontSource = Default(Plugin.ScopeFontSource),
                FontName = Default(Plugin.ScopeFontName),
                CustomFontFile = Default(Plugin.CustomFontFile),
                TextThickness = Default(Plugin.ScopeTextThickness),
                TextSpacing = Default(Plugin.ScopeTextSpacing),
                TextGlow = Default(Plugin.ScopeTextGlow),
                TextOutline = Default(Plugin.ScopeTextOutline),
                TextAberration = Default(Plugin.ScopeTextAberration),
                TextOffsetY = Default(Plugin.ScopeWorldTextOffsetY),
                BackgroundVisible = Default(Plugin.ScopeWorldBackground),
                BackgroundWidth = Default(Plugin.ScopeWorldBackgroundWidth),
                BackgroundHeight = Default(Plugin.ScopeWorldBackgroundHeight),
                BackgroundColor = Default(Plugin.ScopeWorldBackgroundColor)
            };
        }
        public static bool TryFromPreset(string presetName, out StyleSnapshot snapshot)
        {
            snapshot = null;
            if (!StylePresets.TryGetPresetValues(presetName, out Dictionary<string, string> values))
            {
                return false;
            }

            if (values == null)
            {
                Plugin.LogSource?.LogWarning($"Style preset '{presetName}' is empty or not a JSON object.");
                return false;
            }

            snapshot = FromDefaults();
            foreach (KeyValuePair<string, string> pair in values)
            {
                try
                {
                    ApplyValue(snapshot, pair.Key, pair.Value);
                }
                catch (Exception exception)
                {
                    Plugin.LogSource?.LogWarning(
                        $"Style preset '{presetName}': invalid value '{pair.Value}' for {pair.Key}: {exception.Message}");
                }
            }

            return true;
        }
        private static readonly Dictionary<string, Action<StyleSnapshot, string>> Setters =
            new Dictionary<string, Action<StyleSnapshot, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Readout.DistanceUnit"] = (s, v) => s.DistanceUnit = ConvertLikeConfig(Plugin.DistanceUnit, v),
                ["Readout.ShowUnitSuffix"] = (s, v) => s.ShowUnitSuffix = ConvertLikeConfig(Plugin.ShowUnitSuffix, v),
                ["Readout.UseDecimalFormat"] = (s, v) => s.UseDecimalFormat = ConvertLikeConfig(Plugin.UseDecimalFormat, v),
                ["Readout.ShowZeroLine"] = (s, v) => s.ShowZeroLine = ConvertLikeConfig(Plugin.ShowZeroLine, v),
                ["Readout.BallisticsLine"] = (s, v) => s.BallisticsLine = ConvertLikeConfig(Plugin.BallisticsLine, v),
                ["Readout.BallisticsHoldUnit"] = (s, v) => s.BallisticsHoldUnit = ConvertLikeConfig(Plugin.BallisticsHoldUnit, v),
                ["Readout.RangeLinePrefix"] = (s, v) => s.RangeLinePrefix = ConvertLikeConfig(Plugin.RangeLinePrefix, v),
                ["Readout.ZeroLinePrefix"] = (s, v) => s.ZeroLinePrefix = ConvertLikeConfig(Plugin.ZeroLinePrefix, v),
                ["Readout.NoDistanceText"] = (s, v) => s.NoDistanceText = ConvertLikeConfig(Plugin.NoDistanceText, v),
                ["Scope Text.ScopeWorldTextColor"] = (s, v) => s.TextColor = ConvertLikeConfig(Plugin.ScopeWorldTextColor, v),
                ["Scope Text.ScopeFontSource"] = (s, v) => s.FontSource = ConvertLikeConfig(Plugin.ScopeFontSource, v),
                ["Scope Text.ScopeFontName"] = (s, v) => s.FontName = ConvertLikeConfig(Plugin.ScopeFontName, v),
                ["Scope Text.CustomFontFile"] = (s, v) => s.CustomFontFile = ConvertLikeConfig(Plugin.CustomFontFile, v),
                ["Scope Text.ScopeTextThickness"] = (s, v) => s.TextThickness = ConvertLikeConfig(Plugin.ScopeTextThickness, v),
                ["Scope Text.ScopeTextSpacing"] = (s, v) => s.TextSpacing = ConvertLikeConfig(Plugin.ScopeTextSpacing, v),
                ["Scope Text.ScopeTextGlow"] = (s, v) => s.TextGlow = ConvertLikeConfig(Plugin.ScopeTextGlow, v),
                ["Scope Text.ScopeTextOutline"] = (s, v) => s.TextOutline = ConvertLikeConfig(Plugin.ScopeTextOutline, v),
                ["Scope Text.ScopeTextAberration"] = (s, v) => s.TextAberration = ConvertLikeConfig(Plugin.ScopeTextAberration, v),
                ["Scope Text.ScopeWorldTextOffsetY"] = (s, v) => s.TextOffsetY = ConvertLikeConfig(Plugin.ScopeWorldTextOffsetY, v),
                ["Scope Background.ScopeWorldBackground"] = (s, v) => s.BackgroundVisible = ConvertLikeConfig(Plugin.ScopeWorldBackground, v),
                ["Scope Background.ScopeWorldBackgroundWidth"] = (s, v) => s.BackgroundWidth = ConvertLikeConfig(Plugin.ScopeWorldBackgroundWidth, v),
                ["Scope Background.ScopeWorldBackgroundHeight"] = (s, v) => s.BackgroundHeight = ConvertLikeConfig(Plugin.ScopeWorldBackgroundHeight, v),
                ["Scope Background.ScopeWorldBackgroundColor"] = (s, v) => s.BackgroundColor = ConvertLikeConfig(Plugin.ScopeWorldBackgroundColor, v)
            };

        private static void ApplyValue(StyleSnapshot snapshot, string key, string serialized)
        {
            if (Setters.TryGetValue(key, out Action<StyleSnapshot, string> setter))
            {
                setter(snapshot, serialized);
            }
        }
        private static T ConvertLikeConfig<T>(ConfigEntry<T> entry, string serialized)
        {
            object value = TomlTypeConverter.ConvertToValue(serialized, typeof(T));
            AcceptableValueBase acceptableValues = entry?.Description?.AcceptableValues;
            if (acceptableValues != null)
            {
                value = acceptableValues.Clamp(value);
            }

            return (T)value;
        }

        private static T Default<T>(ConfigEntry<T> entry)
        {
            return entry != null ? (T)entry.DefaultValue : default;
        }
    }
    internal static class ActiveStyle
    {
        private static StyleSnapshot _override;

        public static bool HasOverride => _override != null;

        public static void SetOverride(StyleSnapshot snapshot)
        {
            _override = snapshot;
        }

        public static void ClearOverride()
        {
            _override = null;
        }

        public static DistanceUnit DistanceUnit => _override?.DistanceUnit ?? Plugin.DistanceUnit.Value;
        public static bool ShowUnitSuffix => _override?.ShowUnitSuffix ?? Plugin.ShowUnitSuffix.Value;
        public static bool UseDecimalFormat => _override?.UseDecimalFormat ?? Plugin.UseDecimalFormat.Value;
        public static bool ShowZeroLine => _override?.ShowZeroLine ?? Plugin.ShowZeroLine.Value;
        public static BallisticsLineMode BallisticsLine => _override?.BallisticsLine ?? Plugin.BallisticsLine.Value;
        public static HoldUnit BallisticsHoldUnit => _override?.BallisticsHoldUnit ?? Plugin.BallisticsHoldUnit.Value;
        public static string RangeLinePrefix => _override != null ? _override.RangeLinePrefix : Plugin.RangeLinePrefix.Value;
        public static string ZeroLinePrefix => _override != null ? _override.ZeroLinePrefix : Plugin.ZeroLinePrefix.Value;
        public static string NoDistanceText => _override != null ? _override.NoDistanceText : Plugin.NoDistanceText.Value;

        public static Color TextColor => _override?.TextColor ?? Plugin.ScopeWorldTextColor.Value;
        public static ScopeFontSource FontSource =>
            _override?.FontSource ?? Plugin.ScopeFontSource?.Value ?? ScopeFontSource.SystemFont;
        public static string FontName =>
            _override != null ? _override.FontName : Plugin.ScopeFontName?.Value ?? "Consolas";
        public static string CustomFontFile =>
            _override != null ? _override.CustomFontFile : Plugin.CustomFontFile?.Value;

        public static float TextThickness => _override?.TextThickness ?? Plugin.ScopeTextThickness.Value;
        public static float TextSpacing => _override?.TextSpacing ?? Plugin.ScopeTextSpacing.Value;
        public static float TextGlow => _override?.TextGlow ?? Plugin.ScopeTextGlow.Value;
        public static float TextOutline => _override?.TextOutline ?? Plugin.ScopeTextOutline.Value;
        public static float TextAberration => _override?.TextAberration ?? Plugin.ScopeTextAberration.Value;
        public static float TextOffsetY => _override?.TextOffsetY ?? Plugin.ScopeWorldTextOffsetY.Value;

        public static bool BackgroundVisible => _override?.BackgroundVisible ?? Plugin.ScopeWorldBackground.Value;
        public static float BackgroundWidth => _override?.BackgroundWidth ?? Plugin.ScopeWorldBackgroundWidth.Value;
        public static float BackgroundHeight => _override?.BackgroundHeight ?? Plugin.ScopeWorldBackgroundHeight.Value;
        public static Color BackgroundColor => _override?.BackgroundColor ?? Plugin.ScopeWorldBackgroundColor.Value;
    }
}
