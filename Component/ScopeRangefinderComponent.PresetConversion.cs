using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ScopeRangefinder
{
    internal partial class ScopeRangefinderComponent
    {
        private const float AnchorInferenceDeadband = 0.02f;
        private static ReadoutAnchor InferAnchor(float offsetX, float offsetY)
        {
            int h = offsetX < -AnchorInferenceDeadband ? 0 : offsetX > AnchorInferenceDeadband ? 2 : 1;
            int v = offsetY < -AnchorInferenceDeadband ? 0 : offsetY > AnchorInferenceDeadband ? 2 : 1;
            ReadoutAnchor[,] grid =
            {
                { ReadoutAnchor.BottomLeft, ReadoutAnchor.Bottom, ReadoutAnchor.BottomRight },
                { ReadoutAnchor.Left, ReadoutAnchor.Center, ReadoutAnchor.Right },
                { ReadoutAnchor.TopLeft, ReadoutAnchor.Top, ReadoutAnchor.TopRight }
            };
            return grid[v, h];
        }
        private string ConvertShippedPresetsToInferredAnchors()
        {
            if (_usingMainCameraScope || _activeScopeCamera == null || _reticleReadoutRoot == null)
            {
                return "Aim through a magnified optic first (the conversion measures the live readout)";
            }

            string presetPath = ScopeLayoutConfig.PresetPath;
            if (!File.Exists(presetPath))
            {
                return "Shipped preset file not found";
            }

            JObject document;
            try
            {
                document = JObject.Parse(File.ReadAllText(presetPath));
            }
            catch (Exception exception)
            {
                return "Could not read the preset file: " + exception.Message;
            }

            if (!(document["Scopes"] is JObject scopes))
            {
                return "Preset file has no Scopes section";
            }

            int converted = 0;
            int skipped = 0;
            var report = new List<string>();
            foreach (KeyValuePair<string, JToken> pair in scopes)
            {
                if (!(pair.Value is JObject entry))
                {
                    continue;
                }
                if (pair.Key.StartsWith(OverlayLayoutKeyPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    skipped++;
                    continue;
                }

                float offsetX = entry.Value<float?>("OffsetX") ?? 0f;
                float offsetY = entry.Value<float?>("OffsetY") ?? 0f;
                ReadoutAnchor current = ReadoutAnchor.Center;
                string currentName = entry.Value<string>("Anchor");
                if (!string.IsNullOrEmpty(currentName)
                    && Enum.TryParse(currentName, true, out ReadoutAnchor parsed))
                {
                    current = parsed;
                }

                ReadoutAnchor target = InferAnchor(offsetX, offsetY);
                if (target == current)
                {
                    skipped++;
                    continue;
                }
                float presetScale = entry.Value<float?>("Scale") ?? 0f;
                if (!TryGetAnchorSwitchOffsetDeltaForScale(current, target, presetScale, out Vector2 delta))
                {
                    return "Could not measure the readout envelope";
                }

                entry["OffsetX"] = (float)Math.Round(offsetX + delta.x, 4);
                entry["OffsetY"] = (float)Math.Round(offsetY + delta.y, 4);
                entry["Anchor"] = target.ToString();
                converted++;
                report.Add($"{pair.Key}: {current} -> {target} ({offsetX:0.###},{offsetY:0.###}) -> ({entry["OffsetX"]},{entry["OffsetY"]})");
            }

            string outputPath = Path.Combine(
                Path.GetDirectoryName(presetPath) ?? string.Empty,
                "ScopeRangefinder.presets.anchored.json");
            try
            {
                File.WriteAllText(outputPath, document.ToString(Formatting.Indented));
                File.WriteAllLines(outputPath + ".log", report);
            }
            catch (Exception exception)
            {
                return "Could not write the converted file: " + exception.Message;
            }

            Plugin.LogSource?.LogInfo(
                $"Converted {converted} shipped presets to inferred anchors ({skipped} unchanged) -> {outputPath}");
            return $"Converted {converted} presets -> presets.anchored.json ({skipped} unchanged)";
        }
    }
}
