using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ScopeRangefinder
{
    internal static class ScopeDisplayStyle
    {
        public const float DefaultOffsetX = -165f;
        public const float DefaultOffsetY = 50f;

        private static Sprite _whiteSprite;
        private static TMP_FontAsset _cachedFont;

        private static readonly string[] PreferredFontNameParts =
        {
            "DS-Digital",
            "Digital",
            "LCD",
            "7segment",
            "7-segment",
            "Segment",
            "Mono",
            "Monospace",
            "Timer",
            "Clock",
            "BN SDF",
            "BM SDF",
            "RobotoMono",
        };

        public static TMP_FontAsset LoadRangefinderFont()
        {
            if (_cachedFont != null)
            {
                return _cachedFont;
            }

            TMP_FontAsset[] fonts = Resources.LoadAll<TMP_FontAsset>("UI/Fonts");
            if (fonts != null)
            {
                for (int p = 0; p < PreferredFontNameParts.Length; p++)
                {
                    string part = PreferredFontNameParts[p];
                    for (int i = 0; i < fonts.Length; i++)
                    {
                        TMP_FontAsset font = fonts[i];
                        if (font != null && font.name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            _cachedFont = font;
                            return _cachedFont;
                        }
                    }
                }

                for (int i = 0; i < fonts.Length; i++)
                {
                    if (fonts[i] != null)
                    {
                        _cachedFont = fonts[i];
                        return _cachedFont;
                    }
                }
            }

            _cachedFont = TMP_Settings.defaultFontAsset;
            return _cachedFont;
        }

        public static void ApplyReadoutStyle(TextMeshProUGUI text)
        {
            text.font = LoadRangefinderFont();
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 32f;
            text.fontStyle = FontStyles.Normal;
            text.characterSpacing = 6f;
            text.color = new Color(0.18f, 0.98f, 0.22f, 0.96f);
            text.outlineWidth = 0.12f;
            text.outlineColor = new Color(0f, 0.12f, 0f, 0.9f);
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

        public static TextMeshProUGUI CreateReadoutText(RectTransform panelRect)
        {
            var textObject = new GameObject("DistanceText");
            textObject.transform.SetParent(panelRect, false);

            var rectTransform = textObject.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = new Vector2(6f, 3f);
            rectTransform.offsetMax = new Vector2(-6f, -3f);

            var text = textObject.AddComponent<TextMeshProUGUI>();
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

            Sprite builtin = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            if (builtin != null)
            {
                _whiteSprite = builtin;
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
