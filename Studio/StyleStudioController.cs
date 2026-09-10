using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ScopeRangefinder
{
    internal sealed class StyleStudioController
    {
        private const string WebHost = "scoperangefinder.studio";
        private const string FontsHost = "scoperangefinder.fonts";
        private const int WindowWidth = 1280;
        private const int WindowHeight = 860;
        private const string WindowTitle = "Scope Rangefinder - Style Studio";
        private const string ChannelState = "state";
        private const string ChannelThumb = "thumb";
        private const string ChannelStatus = "status";
        private const string RequestGetState = "getState";
        private const string RequestExport = "export";
        private const string ChannelSet = "set";
        private const string ChannelApply = "apply";
        private const string ChannelSave = "save";
        private const string ChannelDelete = "delete";
        private const string ChannelImport = "import";
        private const string ChannelFont = "font";
        private const string ChannelScopeAssign = "assignScope";
        private const string ChannelClose = "close";
        private const string ChannelRequestThumbs = "requestThumbs";

        private readonly ScopeRangefinderComponent _owner;
        private object _handle;
        private bool _pageLoaded;
        private bool _failedLatched;
        private string _failureReason;
        private Coroutine _thumbnailJob;
        private readonly Dictionary<string, string> _thumbnailCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private bool _stateDirty;
        private float _stateSendDueAt = float.PositiveInfinity;
        private const float StateDebounceSeconds = 0.1f;

        public StyleStudioController(ScopeRangefinderComponent owner)
        {
            _owner = owner;
        }

        public bool IsOpen => _handle != null && !_failedLatched && StyleStudioGate.IsVisible(_handle);
        public string UnavailableReason
        {
            get
            {
                if (_failedLatched)
                {
                    return _failureReason ?? "The studio window failed; see the log.";
                }

                return StyleStudioGate.UnavailableReason;
            }
        }

        public void Toggle()
        {
            if (_failedLatched)
            {
                Plugin.LogSource?.LogWarning($"Style Studio unavailable: {_failureReason}");
                return;
            }

            if (_handle != null)
            {
                StyleStudioGate.Toggle(_handle);
                return;
            }

            Open();
        }

        public void Open()
        {
            if (_failedLatched)
            {
                Plugin.LogSource?.LogWarning($"Style Studio unavailable: {_failureReason}");
                return;
            }

            if (_handle != null)
            {
                StyleStudioGate.Show(_handle);
                return;
            }

            string reason = StyleStudioGate.UnavailableReason;
            if (reason != null)
            {
                Plugin.LogSource?.LogInfo($"Style Studio not opened: {reason}");
                return;
            }

            string pluginDir = Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? string.Empty;
            if (!File.Exists(Path.Combine(pluginDir, "web", "index.html")))
            {
                _failedLatched = true;
                _failureReason = "The studio's web folder is missing (web/index.html); reinstall the mod.";
                Plugin.LogSource?.LogError($"Style Studio: {_failureReason}");
                return;
            }

            int[] closeKeys = { 0x1B };
            object created = StyleStudioGate.Create(
                WindowTitle,
                Path.Combine(pluginDir, "web"),
                WebHost,
                ScopeDisplayStyle.GetFontsDirectory(),
                FontsHost,
                WindowWidth,
                WindowHeight,
                closeKeys,
                devTools: Plugin.LogScopeKeys.Value);
            if (created == null)
            {
                _failedLatched = true;
                _failureReason = "Anvil-WebOverlay refused to create a window (an earlier start already failed).";
                Plugin.LogSource?.LogWarning($"Style Studio: {_failureReason}");
                return;
            }

            _handle = created;
            StyleStudioGate.Subscribe(
                _handle,
                OnPageLoaded,
                OnChannelMessage,
                OnVisibilityChanged,
                OnFailed,
                OnChannelsFailed,
                OnRequest,
                new[] { RequestGetState, RequestExport });
            StyleStudioGate.Navigate(_handle, $"https://{WebHost}/index.html");
            StyleStudioGate.Show(_handle);
        }

        public void Close()
        {
            if (_handle != null)
            {
                StyleStudioGate.Hide(_handle);
            }
        }

        public void Dispose()
        {
            StopThumbnails();
            if (_handle != null)
            {
                StyleStudioGate.Dispose(_handle);
                _handle = null;
            }
        }
        public void Pump()
        {
            if (_handle == null)
            {
                return;
            }

            StyleStudioGate.PumpEvents(_handle);

            if (_stateDirty && _pageLoaded && Time.realtimeSinceStartup >= _stateSendDueAt)
            {
                _stateDirty = false;
                _stateSendDueAt = float.PositiveInfinity;
                SendState();
            }
        }
        public void MarkStateDirty()
        {
            _stateDirty = true;
            if (float.IsPositiveInfinity(_stateSendDueAt))
            {
                _stateSendDueAt = Time.realtimeSinceStartup + StateDebounceSeconds;
            }
        }

        private void OnPageLoaded()
        {
            _pageLoaded = true;
            _thumbnailCache.Clear();
            SendState();
        }

        private void OnVisibilityChanged(bool visible)
        {
            if (!visible)
            {
                StopThumbnails();
            }
        }

        private void OnFailed(string failure, string message)
        {
            _failedLatched = true;
            _failureReason = FailureSentence(failure, message);
            Plugin.LogSource?.LogWarning($"Style Studio failed ({failure}): {message}");
            StopThumbnails();
            if (_handle != null)
            {
                object dead = _handle;
                _handle = null;
                StyleStudioGate.Dispose(dead);
            }
        }
        private void OnChannelsFailed()
        {
            _failedLatched = true;
            _failureReason = "The studio window could not open its connection to the mod; "
                + "use the F8 editor instead.";
            Plugin.LogSource?.LogWarning($"Style Studio: {_failureReason}");
            StopThumbnails();
            if (_handle != null)
            {
                object dead = _handle;
                _handle = null;
                StyleStudioGate.Dispose(dead);
            }
        }

        private static string FailureSentence(string failure, string message)
        {
            switch (failure)
            {
                case "RuntimeMissing":
                    return "The Microsoft WebView2 runtime is not installed; the built-in editor keeps working.";
                case "LibraryIncomplete":
                    return "Anvil-WebOverlay is installed incompletely (WebView2Loader.dll missing); reinstall it.";
                case "RendererCrashed":
                    return "The studio's browser crashed; reopen it to try again.";
                case "VirtualHostFailed":
                    return "The studio's files could not be served (web/ or fonts/ folder missing, or an old WebView2 runtime).";
                default:
                    return message ?? $"The studio window failed ({failure}).";
            }
        }

        private string OnRequest(string channel, string payload)
        {
            switch (channel)
            {
                case RequestGetState:
                    return payload == "preview" ? BuildLivePreviewJson() : BuildStateJson();
                case RequestExport:
                {
                    string name = payload;
                    if (string.IsNullOrEmpty(name))
                    {
                        return StylePresets.ExportToJson(
                            string.IsNullOrEmpty(Plugin.SelectedStylePreset.Value) ? "Shared Style" : Plugin.SelectedStylePreset.Value,
                            StylePresets.CaptureCurrentValues());
                    }

                    return StylePresets.TryCapturePresetValues(name, out Dictionary<string, string> values)
                        ? StylePresets.ExportToJson(name, values)
                        : null;
                }
                default:
                    return null;
            }
        }

        private void OnChannelMessage(string channel, string payload)
        {
            try
            {
                HandleMessage(channel, payload);
            }
            catch (Exception exception)
            {
                Plugin.LogSource?.LogWarning($"Style Studio: '{channel}' failed: {exception.Message}");
                SendStatus("error", $"{channel} failed: {exception.Message}");
            }
        }

        private void HandleMessage(string channel, string payload)
        {
            switch (channel)
            {
                case ChannelSet:
                    ApplySetting(payload);
                    break;
                case ChannelApply:
                    ApplyPreset(payload);
                    break;
                case ChannelSave:
                    SavePreset(payload);
                    break;
                case ChannelDelete:
                    DeletePreset(payload);
                    break;
                case ChannelImport:
                    ImportPreset(payload);
                    break;
                case ChannelFont:
                    SelectFont(payload);
                    break;
                case ChannelScopeAssign:
                    AssignScope(payload);
                    break;
                case ChannelRequestThumbs:
                    StartThumbnails();
                    break;
                case ChannelClose:
                    Close();
                    break;
                default:
                    Plugin.LogSource?.LogDebug($"Style Studio: unknown channel '{channel}'");
                    break;
            }
        }

        private void ApplySetting(string payload)
        {
            JObject message = JObject.Parse(payload);
            string key = message.Value<string>("key");
            string value = message.Value<string>("value");
            if (string.IsNullOrEmpty(key) || value == null)
            {
                return;
            }

            ConfigEntryBase entry = FindCoveredEntry(key);
            if (entry == null)
            {
                SendStatus("error", $"Unknown setting {key}");
                return;
            }
            object typed = TomlTypeConverter.ConvertToValue(value, entry.SettingType);
            _owner.SetStyleValueFromStudio(entry, typed);
        }

        private static ConfigEntryBase FindCoveredEntry(string sectionDotKey)
        {
            int dot = sectionDotKey.IndexOf('.');
            if (dot <= 0)
            {
                return null;
            }

            var definition = new ConfigDefinition(sectionDotKey.Substring(0, dot), sectionDotKey.Substring(dot + 1));
            ConfigFile config = Plugin.ConfigInstance;
            if (config == null || !StylePresets.IsCoveredDefinition(definition) || !config.ContainsKey(definition))
            {
                return null;
            }

            return config[definition];
        }

        private void ApplyPreset(string payload)
        {
            JObject message = JObject.Parse(payload);
            string name = message.Value<string>("preset");
            string target = message.Value<string>("target") ?? "global";
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            if (target == "scope")
            {
                AssignScope(JsonConvert.SerializeObject(new { preset = name }));
                return;
            }

            _owner.FlushStudioTextEdits();
            if (StylePresets.Apply(name))
            {
                Plugin.SelectedStylePreset.Value = name;
                SendStatus("ok", $"Applied '{name}' to the global style");
            }
            else
            {
                SendStatus("error", $"Could not apply '{name}' (see log)");
            }

            MarkStateDirty();
        }

        private void SavePreset(string payload)
        {
            JObject message = JObject.Parse(payload);
            string name = (message.Value<string>("name") ?? string.Empty).Trim();
            if (name.Length == 0)
            {
                SendStatus("error", "Enter a preset name first");
                return;
            }

            if (StylePresets.IsBuiltin(name))
            {
                SendStatus("error", $"'{name}' is a shipped preset; pick a different name");
                return;
            }

            if (StylePresets.SaveCurrent(name))
            {
                Plugin.SelectedStylePreset.Value = name;
                _thumbnailCache.Remove(name);
                SendStatus("ok", $"Saved '{name}'");
            }
            else
            {
                SendStatus("error", $"Could not save '{name}' (see log)");
            }

            MarkStateDirty();
            StartThumbnails();
        }

        private void DeletePreset(string payload)
        {
            JObject message = JObject.Parse(payload);
            string name = message.Value<string>("name");
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            if (StylePresets.Delete(name))
            {
                if (string.Equals(name, Plugin.SelectedStylePreset.Value, StringComparison.OrdinalIgnoreCase))
                {
                    Plugin.SelectedStylePreset.Value = string.Empty;
                }

                _owner.ClearScopeAssignmentIfPreset(name);
                _thumbnailCache.Remove(name);
                SendStatus("ok", $"Deleted '{name}'");
            }
            else
            {
                SendStatus("error", $"Could not delete '{name}'");
            }

            MarkStateDirty();
        }

        private void ImportPreset(string payload)
        {
            JObject message = JObject.Parse(payload);
            string json = message.Value<string>("json") ?? string.Empty;
            if (StylePresets.TryImportFromJson(json, out string importedName, out string error))
            {
                SendStatus("ok", $"Imported as '{importedName}'");
                MarkStateDirty();
                StartThumbnails();
            }
            else
            {
                SendStatus("error", error ?? "Import failed");
            }
        }

        private void SelectFont(string payload)
        {
            JObject message = JObject.Parse(payload);
            string file = message.Value<string>("file");
            if (string.IsNullOrEmpty(file))
            {
                return;
            }

            _owner.SelectFontFileFromStudio(file);
            MarkStateDirty();
        }

        private void AssignScope(string payload)
        {
            JObject message = JObject.Parse(payload);
            string preset = message.Value<string>("preset");
            if (!_owner.TryAssignScopePresetFromStudio(string.IsNullOrEmpty(preset) ? null : preset, out string status))
            {
                SendStatus("error", status);
            }
            else
            {
                SendStatus("ok", status);
            }

            MarkStateDirty();
        }

        private void SendState()
        {
            if (_handle == null || !_pageLoaded)
            {
                return;
            }
            StyleStudioGate.PostRetained(_handle, ChannelState, BuildStateJson());
        }

        private void SendStatus(string level, string text)
        {
            if (_handle == null)
            {
                return;
            }

            StyleStudioGate.Post(_handle, ChannelStatus, JsonConvert.SerializeObject(new { level, text }));
        }
        private string BuildStateJson()
        {
            var values = new JObject();
            var meta = new JObject();
            foreach (KeyValuePair<string, string> pair in StylePresets.CaptureCurrentValues())
            {
                values[pair.Key] = pair.Value;
                ConfigEntryBase entry = FindCoveredEntry(pair.Key);
                if (entry == null)
                {
                    continue;
                }

                var m = new JObject
                {
                    ["type"] = entry.SettingType.Name,
                    ["default"] = TomlTypeConverter.ConvertToString(entry.DefaultValue, entry.SettingType),
                    ["description"] = entry.Description?.Description ?? string.Empty
                };
                if (entry.Description?.AcceptableValues is AcceptableValueRange<float> range)
                {
                    m["min"] = range.MinValue;
                    m["max"] = range.MaxValue;
                }

                if (entry.SettingType.IsEnum)
                {
                    m["options"] = new JArray(Enum.GetNames(entry.SettingType));
                }

                meta[pair.Key] = m;
            }

            string[] names = StylePresets.ListPresetNames();
            var presets = new JArray();
            foreach (string name in names)
            {
                presets.Add(new JObject
                {
                    ["name"] = name,
                    ["builtin"] = StylePresets.IsBuiltin(name),
                    ["thumb"] = _thumbnailCache.TryGetValue(name, out string png) ? png : null
                });
            }

            string selected = Plugin.SelectedStylePreset.Value ?? string.Empty;
            _owner.GetStudioScopeInfo(out string scopeKey, out string scopeAssigned, out bool aiming);

            var state = new JObject
            {
                ["version"] = Plugin.PluginVersion,
                ["values"] = values,
                ["meta"] = meta,
                ["presets"] = presets,
                ["selected"] = selected,
                ["modified"] = selected.Length > 0 && !StylePresets.MatchesCurrent(selected),
                ["scope"] = new JObject
                {
                    ["key"] = scopeKey,
                    ["assigned"] = scopeAssigned,
                    ["aiming"] = aiming
                },
                ["fonts"] = new JArray(ScopeRangefinderComponent.ListFontFilesForStudio()),
                ["fontsHost"] = $"https://{FontsHost}/",
                ["thumbsAvailable"] = !aiming
            };
            return state.ToString(Formatting.None);
        }

        private string BuildLivePreviewJson()
        {
            byte[] png = _owner.RenderLivePreviewPng();
            var doc = new JObject
            {
                ["preview"] = png != null ? "data:image/png;base64," + Convert.ToBase64String(png) : null
            };
            return doc.ToString(Formatting.None);
        }

        private void StartThumbnails()
        {
            if (_handle == null || _thumbnailJob != null)
            {
                return;
            }

            _thumbnailJob = _owner.StartCoroutine(RenderThumbnails());
        }

        private void StopThumbnails()
        {
            if (_thumbnailJob != null)
            {
                _owner.StopCoroutine(_thumbnailJob);
                _thumbnailJob = null;
            }
        }
        private IEnumerator RenderThumbnails()
        {
            string[] names = StylePresets.ListPresetNames();
            foreach (string name in names)
            {
                if (_handle == null || !_pageLoaded)
                {
                    break;
                }

                while (_owner.IsAimingForStudio())
                {
                    yield return null;
                }

                if (_thumbnailCache.ContainsKey(name))
                {
                    continue;
                }

                if (!StyleSnapshot.TryFromPreset(name, out StyleSnapshot snapshot))
                {
                    continue;
                }

                byte[] png = _owner.RenderStyleThumbnailPng(snapshot);
                if (png != null)
                {
                    string encoded = "data:image/png;base64," + Convert.ToBase64String(png);
                    _thumbnailCache[name] = encoded;
                    StyleStudioGate.Post(_handle, ChannelThumb,
                        JsonConvert.SerializeObject(new { name, png = encoded }));
                }

                yield return null;
            }

            _thumbnailJob = null;
        }
    }
}
