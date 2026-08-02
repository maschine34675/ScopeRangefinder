using System;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace ScopeRangefinder
{
    internal partial class ScopeRangefinderComponent
    {
        private const int PreviewTextureWidth = 256;
        private const int PreviewTextureHeight = 64;
        private const float PreviewOrthoHalfWidth = 0.5f;
        private const float PreviewOrthoHalfHeight = 0.125f;
        private const float PreviewFontSize = 1.2f;
        private const float PreviewRenderInterval = 0.1f;
        private const float PreviewRequestTimeout = 0.5f;

        private static float _previewRequestedUntil;
        private struct PreviewSignature : IEquatable<PreviewSignature>
        {
            public TMP_FontAsset Font;
            public Texture Atlas;
            public Color TextColor;
            public float Spacing;
            public bool ZeroLine;
            public float Thickness;
            public float Outline;
            public float Glow;
            public float Aberration;
            public string SampleText;

            public bool Equals(PreviewSignature other)
            {
                return Font == other.Font
                    && Atlas == other.Atlas
                    && TextColor == other.TextColor
                    && Spacing == other.Spacing
                    && ZeroLine == other.ZeroLine
                    && Thickness == other.Thickness
                    && Outline == other.Outline
                    && Glow == other.Glow
                    && Aberration == other.Aberration
                    && SampleText == other.SampleText;
            }
        }

        private PreviewSignature _renderedPreviewSignature;
        private bool _previewRendered;

        private RenderTexture _previewTexture;
        private Camera _previewCamera;
        private CommandBuffer _previewBuffer;
        private TextMeshPro _previewText;
        private MeshRenderer _previewRenderer;
        private Material _previewTextMaterial;
        private readonly Material[] _previewGlowMaterials = new Material[GlowStyling.LayerCount];
        private readonly Material[] _previewFringeMaterials = new Material[2];
        private TMP_FontAsset _previewFont;
        private float _nextPreviewRenderTime;
        internal static void DrawFontPreview(ConfigEntryBase entry)
        {
            _previewRequestedUntil = Time.realtimeSinceStartup + PreviewRequestTimeout;

            Texture texture = _activeInstance?._previewTexture;
            if (texture == null)
            {
                GUILayout.Label("Preview initializing...");
                return;
            }

            GUILayout.Box(
                texture,
                GUILayout.Width(PreviewTextureWidth),
                GUILayout.Height(PreviewTextureHeight));
        }
        private void UpdateFontPreview()
        {
            if (Time.realtimeSinceStartup > _previewRequestedUntil
                || Time.realtimeSinceStartup < _nextPreviewRenderTime)
            {
                return;
            }
            _nextPreviewRenderTime = Time.realtimeSinceStartup + PreviewRenderInterval;

            TMP_FontAsset font = ScopeDisplayStyle.LoadRangefinderTmpFont();
            if (font == null)
            {
                return;
            }

            var signature = new PreviewSignature
            {
                Font = font,
                Atlas = font.material != null ? font.material.mainTexture : null,
                TextColor = Plugin.ScopeWorldTextColor.Value,
                Spacing = Plugin.ScopeTextSpacing.Value,
                ZeroLine = Plugin.ShowZeroLine.Value,
                Thickness = Plugin.ScopeTextThickness.Value,
                Outline = Plugin.ScopeTextOutline.Value,
                Glow = Plugin.ScopeTextGlow.Value,
                Aberration = Plugin.ScopeTextAberration.Value,
                SampleText = BuildSampleDistanceText()
            };
            if (_previewRendered && signature.Equals(_renderedPreviewSignature))
            {
                return;
            }

            EnsurePreviewRig();
            ApplyPreviewAppearance(font);
            RenderPreview();
            _renderedPreviewSignature = signature;
            _previewRendered = true;
        }

        private void EnsurePreviewRig()
        {
            if (_previewCamera != null)
            {
                return;
            }

            _previewTexture = new RenderTexture(PreviewTextureWidth, PreviewTextureHeight, 16);
            _previewTexture.name = "ScopeRangefinderFontPreview";

            var cameraObject = new GameObject("ScopeRangefinderPreviewCamera");
            cameraObject.transform.SetParent(transform, false);
            _previewCamera = cameraObject.AddComponent<Camera>();
            _previewCamera.enabled = false;
            _previewCamera.targetTexture = _previewTexture;
            _previewCamera.clearFlags = CameraClearFlags.SolidColor;
            _previewCamera.backgroundColor = new Color(0.08f, 0.08f, 0.08f, 1f);
            _previewCamera.cullingMask = 0;
            _previewCamera.useOcclusionCulling = false;
            _previewCamera.allowMSAA = false;

            _previewBuffer = new CommandBuffer { name = "Scope Readout Preview" };
            _previewCamera.AddCommandBuffer(CameraEvent.AfterEverything, _previewBuffer);
            var textObject = new GameObject("PreviewText");
            textObject.transform.SetParent(transform, false);
            _previewText = textObject.AddComponent<TextMeshPro>();
            _previewText.alignment = TextAlignmentOptions.Center;
            _previewText.enableWordWrapping = false;
            _previewText.overflowMode = TextOverflowModes.Overflow;
            _previewText.fontSize = PreviewFontSize;
            _previewText.rectTransform.sizeDelta = new Vector2(4f, 1f);
            _previewRenderer = textObject.GetComponent<MeshRenderer>();
            _previewRenderer.enabled = false;
        }
        private void ApplyPreviewAppearance(TMP_FontAsset font)
        {
            if (_previewFont != font)
            {
                _previewFont = font;
                _previewText.font = font;

                if (_previewTextMaterial != null)
                {
                    Destroy(_previewTextMaterial);
                }

                for (int i = 0; i < _previewGlowMaterials.Length; i++)
                {
                    if (_previewGlowMaterials[i] != null)
                    {
                        Destroy(_previewGlowMaterials[i]);
                        _previewGlowMaterials[i] = null;
                    }
                }

                for (int i = 0; i < _previewFringeMaterials.Length; i++)
                {
                    if (_previewFringeMaterials[i] != null)
                    {
                        Destroy(_previewFringeMaterials[i]);
                        _previewFringeMaterials[i] = null;
                    }
                }

                _previewTextMaterial = new Material(font.material);
                ConfigureReticleDrawMaterial(_previewTextMaterial, 5000);
            }
            Texture atlas = font.material != null ? font.material.mainTexture : null;
            if (atlas != null && _previewTextMaterial.mainTexture != atlas)
            {
                _previewTextMaterial.mainTexture = atlas;
            }
            Color textColor = Plugin.ScopeWorldTextColor.Value;
            if (_previewTextMaterial.HasProperty("_FaceColor"))
            {
                _previewText.color = new Color(1f, 1f, 1f, textColor.a);
                _previewTextMaterial.SetColor(
                    "_FaceColor", new Color(textColor.r, textColor.g, textColor.b, 1f));
            }
            else
            {
                _previewText.color = textColor;
            }

            _previewText.characterSpacing = Plugin.ScopeTextSpacing.Value;
            _previewText.fontSize = Plugin.ShowZeroLine.Value ? 0.7f : PreviewFontSize;
            _previewText.alignment = Plugin.ShowZeroLine.Value
                ? TextAlignmentOptions.Left
                : TextAlignmentOptions.Center;

            if (_previewTextMaterial.HasProperty("_FaceDilate"))
            {
                _previewTextMaterial.SetFloat("_FaceDilate", Plugin.ScopeTextThickness.Value);
            }

            if (_previewTextMaterial.HasProperty("_OutlineWidth"))
            {
                float outlineWidth = Mathf.Clamp01(Plugin.ScopeTextOutline.Value);
                _previewTextMaterial.SetFloat("_OutlineWidth", outlineWidth);
                _previewTextMaterial.SetColor("_OutlineColor", Color.black);
                if (outlineWidth > 0f)
                {
                    _previewTextMaterial.EnableKeyword("OUTLINE_ON");
                }
                else
                {
                    _previewTextMaterial.DisableKeyword("OUTLINE_ON");
                }
            }

            float glowStrength = Mathf.Clamp01(Plugin.ScopeTextGlow.Value);
            bool glowActive = glowStrength > 0.001f && _previewTextMaterial.HasProperty("_FaceDilate");
            if (glowActive)
            {
                for (int i = 0; i < _previewGlowMaterials.Length; i++)
                {
                    if (_previewGlowMaterials[i] == null)
                    {
                        _previewGlowMaterials[i] = new Material(_previewTextMaterial);
                    }

                    if (atlas != null && _previewGlowMaterials[i].mainTexture != atlas)
                    {
                        _previewGlowMaterials[i].mainTexture = atlas;
                    }

                    GlowStyling.ConfigureLayer(
                        _previewGlowMaterials[i],
                        i,
                        glowStrength,
                        Plugin.ScopeTextThickness.Value,
                        Plugin.ScopeWorldTextColor.Value);
                }
            }

            float aberration = Mathf.Clamp01(Plugin.ScopeTextAberration.Value);
            if (aberration > 0.001f && _previewTextMaterial.HasProperty("_FaceDilate"))
            {
                GlowStyling.GetAberrationFringeColors(textColor, out Color outwardColor, out Color inwardColor);
                for (int i = 0; i < _previewFringeMaterials.Length; i++)
                {
                    if (_previewFringeMaterials[i] == null)
                    {
                        _previewFringeMaterials[i] = new Material(_previewTextMaterial);
                    }

                    Material fringe = _previewFringeMaterials[i];
                    if (atlas != null && fringe.mainTexture != atlas)
                    {
                        fringe.mainTexture = atlas;
                    }

                    fringe.SetFloat("_FaceDilate", Plugin.ScopeTextThickness.Value);
                    fringe.SetFloat("_OutlineWidth", 0f);
                    fringe.DisableKeyword("OUTLINE_ON");
                    Color fringeColor = i == 0 ? outwardColor : inwardColor;
                    fringeColor.a = GlowStyling.GetAberrationFringeAlpha(aberration);
                    fringe.SetColor("_FaceColor", fringeColor);
                }
            }
            Material sharedMaterial = glowActive
                ? _previewGlowMaterials[GlowStyling.LayerCount - 1]
                : _previewTextMaterial;
            if (_previewText.fontSharedMaterial != sharedMaterial)
            {
                _previewText.fontSharedMaterial = sharedMaterial;
            }
            _previewText.UpdateMeshPadding();
            _previewText.SetMonospaceText(BuildSampleDistanceText(), false);
            _previewText.ForceMeshUpdate();
        }

        private void RenderPreview()
        {
            float offsetX = Plugin.ShowZeroLine.Value ? -_previewText.textBounds.center.x : 0f;
            _previewText.transform.position = new Vector3(offsetX, 0f, -1f);
            _previewText.transform.rotation = Quaternion.identity;

            _previewBuffer.Clear();
            _previewBuffer.SetViewProjectionMatrices(
                Matrix4x4.identity,
                Matrix4x4.Ortho(
                    -PreviewOrthoHalfWidth, PreviewOrthoHalfWidth,
                    -PreviewOrthoHalfHeight, PreviewOrthoHalfHeight,
                    0.01f, 10f));

            if (Plugin.ScopeTextGlow.Value > 0.001f)
            {
                for (int i = _previewGlowMaterials.Length - 1; i >= 0; i--)
                {
                    if (_previewGlowMaterials[i] != null)
                    {
                        _previewBuffer.DrawRenderer(_previewRenderer, _previewGlowMaterials[i], 0, 0);
                    }
                }
            }
            float aberration = Mathf.Clamp01(Plugin.ScopeTextAberration.Value);
            Mesh previewMesh = aberration > 0.001f ? _previewText.mesh : null;
            if (previewMesh != null)
            {
                float shift = aberration * AberrationMaxShift * (_previewText.fontSize / ReadoutTmpFontSize);
                Vector3 offset = new Vector3(shift, 0f, 0f);
                Matrix4x4 textMatrix = _previewRenderer.localToWorldMatrix;
                if (_previewFringeMaterials[0] != null)
                {
                    _previewBuffer.DrawMesh(previewMesh, Matrix4x4.Translate(offset) * textMatrix, _previewFringeMaterials[0], 0, 0);
                }

                if (_previewFringeMaterials[1] != null)
                {
                    _previewBuffer.DrawMesh(previewMesh, Matrix4x4.Translate(-offset) * textMatrix, _previewFringeMaterials[1], 0, 0);
                }
            }

            _previewBuffer.DrawRenderer(_previewRenderer, _previewTextMaterial, 0, 0);
            _previewCamera.Render();
        }

        private void DestroyFontPreview()
        {
            if (_previewCamera != null)
            {
                Destroy(_previewCamera.gameObject);
                _previewCamera = null;
            }

            if (_previewText != null)
            {
                Destroy(_previewText.gameObject);
                _previewText = null;
            }

            if (_previewTexture != null)
            {
                _previewTexture.Release();
                Destroy(_previewTexture);
                _previewTexture = null;
            }

            if (_previewTextMaterial != null)
            {
                Destroy(_previewTextMaterial);
                _previewTextMaterial = null;
            }

            for (int i = 0; i < _previewGlowMaterials.Length; i++)
            {
                if (_previewGlowMaterials[i] != null)
                {
                    Destroy(_previewGlowMaterials[i]);
                    _previewGlowMaterials[i] = null;
                }
            }

            for (int i = 0; i < _previewFringeMaterials.Length; i++)
            {
                if (_previewFringeMaterials[i] != null)
                {
                    Destroy(_previewFringeMaterials[i]);
                    _previewFringeMaterials[i] = null;
                }
            }

            _previewBuffer?.Release();
            _previewBuffer = null;
            _previewRenderer = null;
            _previewFont = null;
            _previewRendered = false;
        }
    }
}
