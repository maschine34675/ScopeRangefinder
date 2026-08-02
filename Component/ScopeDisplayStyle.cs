using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ScopeRangefinder
{
    internal static class ScopeDisplayStyle
    {
        public const float DefaultOffsetX = -165f;
        public const float DefaultOffsetY = 50f;
        public const string GameBenderFontName = "Jovanny Lemonad - Bender";
        public const string GameBenderTmpFontName = "Jovanny Lemonad - Bender Normal SDF";
        private const float GameFontRetryIntervalSeconds = 5f;

        private static Sprite _whiteSprite;
        private static Font _cachedFont;
        private static string _cachedFontKey;
        private static float _nextGameFontRetryTime;
        private static TMP_FontAsset _cachedTmpFont;
        private static string _cachedTmpFontKey;
        private static float _nextTmpFontRetryTime;
        private static readonly Dictionary<string, TMP_FontAsset> _systemTmpFonts =
            new Dictionary<string, TMP_FontAsset>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> _failedSystemFontNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, TMP_FontAsset> _customTmpFonts =
            new Dictionary<string, TMP_FontAsset>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> _failedCustomFontSpecs =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static bool _tmpShaderSeeded;
        public static void InvalidateFontCaches()
        {
            _cachedTmpFontKey = null;
            _cachedFontKey = null;
            _failedSystemFontNames.Clear();
            _failedCustomFontSpecs.Clear();
        }

        private static readonly string[] FallbackOsFontNames =
        {
            "Consolas",
            "Bahnschrift",
            "Segoe UI",
            "Arial"
        };

        public static Font LoadRangefinderFont()
        {
            ScopeFontSource source = Plugin.ScopeFontSource?.Value ?? ScopeFontSource.SystemFont;
            string fontKey = source == ScopeFontSource.SystemFont
                ? "system:" + (Plugin.ScopeFontName?.Value ?? "Consolas")
                : source.ToString();

            if (_cachedFont != null && string.Equals(_cachedFontKey, fontKey, StringComparison.OrdinalIgnoreCase))
            {
                return _cachedFont;
            }

            if (source != ScopeFontSource.SystemFont)
            {
                if (_cachedFont == null || Time.unscaledTime >= _nextGameFontRetryTime)
                {
                    _nextGameFontRetryTime = Time.unscaledTime + GameFontRetryIntervalSeconds;
                    Font gameFont = FindLoadedGameFont(GameBenderFontName);
                    if (gameFont != null)
                    {
                        _cachedFont = gameFont;
                        _cachedFontKey = fontKey;
                        return _cachedFont;
                    }

                    _cachedFont = CreateSystemFont();
                    _cachedFontKey = null;
                }

                return _cachedFont;
            }

            _cachedFont = CreateSystemFont();
            _cachedFontKey = fontKey;
            return _cachedFont;
        }
        public static TMP_FontAsset LoadRangefinderTmpFont()
        {
            ScopeFontSource source = Plugin.ScopeFontSource?.Value ?? ScopeFontSource.SystemFont;
            string fontKey;
            switch (source)
            {
                case ScopeFontSource.SystemFont:
                    fontKey = "system:" + (Plugin.ScopeFontName?.Value ?? "Consolas");
                    break;
                case ScopeFontSource.CustomFont:
                    fontKey = "custom:" + (Plugin.CustomFontFile?.Value ?? string.Empty);
                    break;
                default:
                    fontKey = "game";
                    break;
            }

            if (_cachedTmpFont != null && string.Equals(_cachedTmpFontKey, fontKey, StringComparison.OrdinalIgnoreCase))
            {
                return _cachedTmpFont;
            }

            if (source == ScopeFontSource.GameBender)
            {
                if (_cachedTmpFont == null || Time.unscaledTime >= _nextTmpFontRetryTime)
                {
                    _nextTmpFontRetryTime = Time.unscaledTime + GameFontRetryIntervalSeconds;
                    TMP_FontAsset gameFont = FindLoadedTmpFont(GameBenderTmpFontName);
                    if (gameFont != null)
                    {
                        _cachedTmpFont = gameFont;
                        _cachedTmpFontKey = fontKey;
                        return _cachedTmpFont;
                    }

                    _cachedTmpFont = CreateSystemTmpFont();
                    _cachedTmpFontKey = null;
                }

                return _cachedTmpFont;
            }

            TMP_FontAsset resolved = source == ScopeFontSource.CustomFont
                ? LoadCustomTmpFont()
                : CreateSystemTmpFont();
            _cachedTmpFont = resolved != null ? resolved : FindLoadedTmpFont(GameBenderTmpFontName);
            _cachedTmpFontKey = _cachedTmpFont != null ? fontKey : null;
            return _cachedTmpFont;
        }

        private static TMP_FontAsset FindLoadedTmpFont(string fontName)
        {
            TMP_FontAsset[] loadedFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            for (int i = 0; i < loadedFonts.Length; i++)
            {
                if (loadedFonts[i].name == fontName)
                {
                    return loadedFonts[i];
                }
            }

            return null;
        }

        private static TMP_FontAsset CreateSystemTmpFont()
        {
            string sourceName = Plugin.ScopeFontName?.Value ?? "Consolas";
            if (_systemTmpFonts.TryGetValue(sourceName, out TMP_FontAsset cachedFont) && cachedFont != null)
            {
                return cachedFont;
            }

            if (_failedSystemFontNames.Contains(sourceName))
            {
                return null;
            }
            string fontPath = FindOsFontPath(sourceName);
            TMP_FontAsset tmpFont = fontPath != null
                ? CreateTmpFontFromFont(new Font(fontPath))
                : null;

            if (tmpFont == null)
            {
                tmpFont = CreateTmpFontFromFont(CreateSystemFont());
            }

            if (tmpFont == null)
            {
                _failedSystemFontNames.Add(sourceName);
                Plugin.LogSource?.LogWarning(
                    $"Could not build a TMP font asset for system font '{sourceName}' " +
                    $"(font file: {fontPath ?? "not found"}); falling back to the game font.");
                return null;
            }

            tmpFont.name = "ScopeRangefinder Dynamic " + sourceName;
            _systemTmpFonts[sourceName] = tmpFont;
            return tmpFont;
        }
        private static TMP_FontAsset LoadCustomTmpFont()
        {
            string fontSpec = Plugin.CustomFontFile?.Value?.Trim();
            if (string.IsNullOrEmpty(fontSpec))
            {
                return null;
            }

            if (_customTmpFonts.TryGetValue(fontSpec, out TMP_FontAsset cachedFont) && cachedFont != null)
            {
                return cachedFont;
            }

            if (_failedCustomFontSpecs.Contains(fontSpec))
            {
                return null;
            }
            string fileName = fontSpec;
            string assetName = null;
            int separatorIndex = fontSpec.IndexOf(':');
            if (separatorIndex > 0)
            {
                fileName = fontSpec.Substring(0, separatorIndex).Trim();
                assetName = fontSpec.Substring(separatorIndex + 1).Trim();
            }

            string path = System.IO.Path.Combine(GetFontsDirectory(), fileName);
            if (!System.IO.File.Exists(path))
            {
                _failedCustomFontSpecs.Add(fontSpec);
                Plugin.LogSource?.LogWarning(
                    $"Custom font file not found: {path}; falling back to the game font.");
                return null;
            }

            string extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
            TMP_FontAsset tmpFont = extension == ".ttf" || extension == ".otf"
                ? CreateTmpFontFromFont(new Font(path))
                : LoadTmpFontFromBundle(path, assetName);

            if (tmpFont == null)
            {
                _failedCustomFontSpecs.Add(fontSpec);
                Plugin.LogSource?.LogWarning(
                    $"Could not load a usable font from '{path}'; falling back to the game font.");
                return null;
            }

            _customTmpFonts[fontSpec] = tmpFont;
            return tmpFont;
        }

        private static TMP_FontAsset LoadTmpFontFromBundle(string path, string assetName)
        {
            AssetBundle bundle;
            try
            {
                bundle = AssetBundle.LoadFromFile(path);
            }
            catch (Exception exception)
            {
                Plugin.LogSource?.LogWarning($"Failed to load asset bundle '{path}': {exception.Message}");
                return null;
            }

            if (bundle == null)
            {
                return null;
            }

            TMP_FontAsset[] tmpFonts = bundle.LoadAllAssets<TMP_FontAsset>();
            TMP_FontAsset result = null;

            if (!string.IsNullOrEmpty(assetName))
            {
                foreach (TMP_FontAsset candidate in tmpFonts)
                {
                    if (string.Equals(candidate.name, assetName, StringComparison.OrdinalIgnoreCase))
                    {
                        result = candidate;
                        break;
                    }
                }

                if (result == null)
                {
                    Plugin.LogSource?.LogWarning(
                        $"Font asset '{assetName}' not found in bundle. Available: " +
                        string.Join(", ", System.Linq.Enumerable.Select(tmpFonts, f => f.name)));
                }
            }
            else if (tmpFonts.Length > 0)
            {
                result = tmpFonts[0];
                if (tmpFonts.Length > 1)
                {
                    Plugin.LogSource?.LogInfo(
                        $"Bundle contains {tmpFonts.Length} font assets, using '{result.name}'. " +
                        "Select another via 'bundlefile:FontAssetName'. Available: " +
                        string.Join(", ", System.Linq.Enumerable.Select(tmpFonts, f => f.name)));
                }
            }

            if (result == null && string.IsNullOrEmpty(assetName))
            {
                Font[] fonts = bundle.LoadAllAssets<Font>();
                result = fonts.Length > 0 ? CreateTmpFontFromFont(fonts[0]) : null;
            }
            bundle.Unload(false);
            return result;
        }
        private static TMP_FontAsset CreateTmpFontFromFont(Font font)
        {
            if (font == null)
            {
                return null;
            }

            try
            {
                if (!_tmpShaderSeeded)
                {
                    System.Reflection.FieldInfo shaderField = typeof(ShaderUtilities).GetField(
                        "k_ShaderRef_MobileSDF",
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                    if (shaderField != null && shaderField.GetValue(null) as Shader == null)
                    {
                        Shader sdfShader = Shader.Find("TextMeshPro/Mobile/Distance Field");
                        if (sdfShader == null)
                        {
                            sdfShader = Shader.Find("TextMeshPro/Distance Field");
                        }

                        if (sdfShader == null)
                        {
                            sdfShader = FindLoadedTmpFont(GameBenderTmpFontName)?.material?.shader;
                        }

                        if (sdfShader != null)
                        {
                            shaderField.SetValue(null, sdfShader);
                        }
                    }

                    _tmpShaderSeeded = true;
                }

                return TMP_FontAsset.CreateFontAsset(font);
            }
            catch (Exception exception)
            {
                Plugin.LogSource?.LogWarning($"TMP font asset creation failed for '{font.name}': {exception.Message}");
                return null;
            }
        }

        internal static string GetFontsDirectory()
        {
            string directory = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? string.Empty,
                "fonts");
            System.IO.Directory.CreateDirectory(directory);
            return directory;
        }
        private static string FindOsFontPath(string familyName)
        {
            string target = NormalizeFontToken(familyName);
            if (target.Length < 3)
            {
                return null;
            }

            string registryPath = TryFindFontPathViaRegistry(target);
            if (registryPath != null)
            {
                return registryPath;
            }

            string bestPath = null;
            int bestScore = 0;
            foreach (string path in EnumerateOsFontFiles())
            {
                string candidate = NormalizeFontToken(System.IO.Path.GetFileNameWithoutExtension(path));
                int score;
                if (candidate == target)
                {
                    score = 100;
                }
                else if (candidate == target + "regular")
                {
                    score = 90;
                }
                else if (candidate.StartsWith(target, StringComparison.Ordinal))
                {
                    score = 80 - (candidate.Length - target.Length);
                }
                else if (candidate.Length >= 4 && target.StartsWith(candidate, StringComparison.Ordinal))
                {
                    score = 60 - (target.Length - candidate.Length);
                }
                else
                {
                    continue;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestPath = path;
                }
            }

            return bestPath;
        }
        private static string TryFindFontPathViaRegistry(string normalizedTarget)
        {
            try
            {
                Type registryType = Type.GetType("Microsoft.Win32.Registry, mscorlib");
                if (registryType == null)
                {
                    return null;
                }

                string bestPath = null;
                int bestScore = 0;
                foreach (string rootName in new[] { "LocalMachine", "CurrentUser" })
                {
                    object rootKey = registryType.GetField(rootName)?.GetValue(null);
                    if (rootKey == null)
                    {
                        continue;
                    }

                    var openSubKey = rootKey.GetType().GetMethod("OpenSubKey", new[] { typeof(string) });
                    object fontsKey = openSubKey?.Invoke(
                        rootKey,
                        new object[] { @"Software\Microsoft\Windows NT\CurrentVersion\Fonts" });
                    if (fontsKey == null)
                    {
                        continue;
                    }

                    try
                    {
                        var getValueNames = fontsKey.GetType().GetMethod("GetValueNames");
                        var getValue = fontsKey.GetType().GetMethod("GetValue", new[] { typeof(string) });
                        foreach (string valueName in (string[])getValueNames.Invoke(fontsKey, null))
                        {
                            string displayName = valueName;
                            int suffixIndex = displayName.LastIndexOf(" (", StringComparison.Ordinal);
                            if (suffixIndex > 0)
                            {
                                displayName = displayName.Substring(0, suffixIndex);
                            }

                            string token = NormalizeFontToken(displayName);
                            int score;
                            if (token == normalizedTarget)
                            {
                                score = 100;
                            }
                            else if (token.StartsWith(normalizedTarget, StringComparison.Ordinal))
                            {
                                score = 70 - (token.Length - normalizedTarget.Length);
                            }
                            else
                            {
                                continue;
                            }

                            if (score <= bestScore)
                            {
                                continue;
                            }
                            string file = getValue.Invoke(fontsKey, new object[] { valueName }) as string;
                            if (string.IsNullOrEmpty(file))
                            {
                                continue;
                            }

                            string path = System.IO.Path.IsPathRooted(file)
                                ? file
                                : System.IO.Path.Combine(
                                    Environment.GetFolderPath(Environment.SpecialFolder.Fonts), file);
                            if (!System.IO.File.Exists(path))
                            {
                                continue;
                            }

                            bestScore = score;
                            bestPath = path;
                        }
                    }
                    finally
                    {
                        (fontsKey as IDisposable)?.Dispose();
                    }
                }

                return bestPath;
            }
            catch
            {
                return null;
            }
        }
        private static System.Collections.Generic.IEnumerable<string> EnumerateOsFontFiles()
        {
            foreach (string path in Font.GetPathsToOSFonts())
            {
                yield return path;
            }

            string userFontsDirectory = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "Windows", "Fonts");
            if (!System.IO.Directory.Exists(userFontsDirectory))
            {
                yield break;
            }

            foreach (string path in System.IO.Directory.GetFiles(userFontsDirectory))
            {
                string extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
                if (extension == ".ttf" || extension == ".otf" || extension == ".ttc")
                {
                    yield return path;
                }
            }
        }

        private static string NormalizeFontToken(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var builder = new System.Text.StringBuilder(value.Length);
            foreach (char character in value)
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToLowerInvariant(character));
                }
            }

            return builder.ToString();
        }

        private static Font FindLoadedGameFont(string fontName)
        {
            Font[] loadedFonts = Resources.FindObjectsOfTypeAll<Font>();
            for (int i = 0; i < loadedFonts.Length; i++)
            {
                if (loadedFonts[i].name == fontName)
                {
                    return loadedFonts[i];
                }
            }

            return null;
        }

        private static Font CreateSystemFont()
        {
            Font font = Font.CreateDynamicFontFromOSFont(GetPreferredOsFontNames(), 96);
            if (font != null)
            {
                return font;
            }

            return Font.CreateDynamicFontFromOSFont("Arial", 96);
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
            text.resizeTextForBestFit = false;
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
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);
            _whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            return _whiteSprite;
        }
    }
}
