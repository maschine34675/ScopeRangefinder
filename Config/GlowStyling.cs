using UnityEngine;

namespace ScopeRangefinder
{
    internal static class GlowStyling
    {
        public const int LayerCount = 3;
        private static readonly float[] SoftnessBase = { 0.10f, 0.25f, 0.45f };
        private static readonly float[] SoftnessScale = { 0.15f, 0.30f, 0.55f };
        private static readonly float[] AlphaFactor = { 0.50f, 0.30f, 0.15f };
        private static readonly Color FringeOutwardBase = new Color(1f, 0.08f, 0.08f);
        private static readonly Color FringeInwardBase = new Color(0.08f, 0.95f, 1f);
        private const float FringeHueShift = 40f / 360f;
        public static void GetAberrationFringeColors(Color textColor, out Color outward, out Color inward)
        {
            Color.RGBToHSV(textColor, out float hue, out float saturation, out float value);
            Color warm = Color.HSVToRGB(Mathf.Repeat(hue - FringeHueShift, 1f), saturation, value);
            Color cool = Color.HSVToRGB(Mathf.Repeat(hue + FringeHueShift, 1f), saturation, value);
            outward = Color.Lerp(FringeOutwardBase, warm, saturation);
            inward = Color.Lerp(FringeInwardBase, cool, saturation);
        }
        public static float GetAberrationFringeAlpha(float strength)
        {
            return 0.85f * Mathf.Clamp01(strength / 0.15f);
        }

        public static void ConfigureLayer(Material material, int layer, float strength, float thickness, Color textColor)
        {
            material.SetFloat("_FaceDilate", Mathf.Clamp(thickness + 0.02f, -1f, 1f));
            material.SetFloat("_OutlineWidth", 0f);
            material.DisableKeyword("OUTLINE_ON");
            if (material.HasProperty("_OutlineSoftness"))
            {
                material.SetFloat("_OutlineSoftness", SoftnessBase[layer] + SoftnessScale[layer] * strength);
            }
            material.SetColor(
                "_FaceColor",
                new Color(textColor.r, textColor.g, textColor.b, AlphaFactor[layer] * strength));
        }
    }
}
