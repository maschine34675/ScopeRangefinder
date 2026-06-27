using System;
using UnityEngine;
using UnityEngine.UI;

namespace ScopeRangefinder
{
    internal static class ScopeDisplayStyle
    {
        public const float DefaultOffsetX = -165f;
        public const float DefaultOffsetY = 50f;

        private static Sprite _whiteSprite;
        private static Font _cachedFont;
        private static string _cachedFontName;

        private static readonly string[] FallbackOsFontNames =
        {
            "Consolas",
            "Roboto",
            "Bahnschrift Light Condensed",
            "Bahnschrift Condensed",
            "Arial Narrow",
            "Arial"
        };

        public static Font LoadRangefinderFont()
        {
            string requestedFontName = Plugin.ScopeFontName?.Value ?? "Consolas";
            if (_cachedFont != null && string.Equals(_cachedFontName, requestedFontName, StringComparison.OrdinalIgnoreCase))
            {
                return _cachedFont;
            }

            _cachedFontName = requestedFontName;
            _cachedFont = Font.CreateDynamicFontFromOSFont(GetPreferredOsFontNames(), 96);
            if (_cachedFont != null)
            {
                return _cachedFont;
            }

            _cachedFont = Font.CreateDynamicFontFromOSFont("Arial", 96);
            return _cachedFont;
        }

        public static string[] GetPreferredOsFontNames()
        {
            string configuredFontName = Plugin.ScopeFontName?.Value;
            if (string.IsNullOrWhiteSpace(configuredFontName))
            {
                return FallbackOsFontNames;
            }

            string[] fontNames = new string[FallbackOsFontNames.Length + 1];
            fontNames[0] = configuredFontName.Trim();
            Array.Copy(FallbackOsFontNames, 0, fontNames, 1, FallbackOsFontNames.Length);
            return fontNames;
        }

        public static void ApplyReadoutStyle(Text text)
        {
            text.font = LoadRangefinderFont();
            text.raycastTarget = false;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 32;
            text.fontStyle = FontStyle.Normal;
            text.color = new Color(0.18f, 0.98f, 0.22f, 0.96f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
        }

        public static RectTransform CreateDisplayPanel(Transform parent)
        {
            var panelObject = new GameObject("DisplayPanel");
            panelObject.transform.SetParent(parent, false);

            var panelRect = panelObject.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.30f);
            panelRect.anchorMax = new Vector2(0.5f, 0.30f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = new Vector2(DefaultOffsetX, DefaultOffsetY);
            panelRect.sizeDelta = new Vector2(142f, 46f);

            CreateBackground(panelObject.transform, new Color(0.01f, 0.035f, 0.01f, 0.82f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            CreateBackground(panelObject.transform, new Color(0.08f, 0.22f, 0.08f, 0.55f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 1);

            return panelRect;
        }

        public static Text CreateReadoutText(RectTransform panelRect)
        {
            var textObject = new GameObject("DistanceText");
            textObject.transform.SetParent(panelRect, false);

            var rectTransform = textObject.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = new Vector2(6f, 3f);
            rectTransform.offsetMax = new Vector2(-6f, -3f);

            var text = textObject.AddComponent<Text>();
            ApplyReadoutStyle(text);
            return text;
        }

        private static void CreateBackground(
            Transform parent,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax,
            int siblingIndex = 0)
        {
            var backgroundObject = new GameObject("Background");
            backgroundObject.transform.SetParent(parent, false);
            backgroundObject.transform.SetSiblingIndex(siblingIndex);

            var rectTransform = backgroundObject.AddComponent<RectTransform>();
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = offsetMin;
            rectTransform.offsetMax = offsetMax;

            var image = backgroundObject.AddComponent<Image>();
            image.sprite = GetWhiteSprite();
            image.type = Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
        }

        private static Sprite GetWhiteSprite()
        {
            if (_whiteSprite != null)
            {
                return _whiteSprite;
            }

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);
            _whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            return _whiteSprite;
        }
    }
}
