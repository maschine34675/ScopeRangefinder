using System;
using System.Runtime.CompilerServices;

namespace ScopeRangefinder
{
    internal static class StyleStudioGate
    {
        public const string LibraryGuid = "com.anvil.weboverlay";
        public static readonly Version MinimumVersion = new Version(1, 11, 0);

        private static bool? _loaded;
        private static Version _foundVersion;
        private static bool _versionMismatchLogged;
        public static bool IsLoaded
        {
            get
            {
                if (_loaded == null)
                {
                    _loaded = BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(
                        LibraryGuid, out BepInEx.PluginInfo info);
                    if (_loaded.Value)
                    {
                        _foundVersion = info.Metadata.Version;
                    }
                }

                return _loaded.Value;
            }
        }

        public static Version FoundVersion => IsLoaded ? _foundVersion : null;
        public static bool IsUsable
        {
            get
            {
                if (!IsLoaded)
                {
                    return false;
                }

                if (_foundVersion != null && _foundVersion >= MinimumVersion)
                {
                    return true;
                }

                if (!_versionMismatchLogged)
                {
                    _versionMismatchLogged = true;
                    Plugin.LogSource?.LogInfo(
                        $"Anvil-WebOverlay {_foundVersion} is installed; the Web Style Studio needs " +
                        $"{MinimumVersion} or newer - using the built-in editor only.");
                }

                return false;
            }
        }
        public static string UnavailableReason
        {
            get
            {
                if (!IsLoaded)
                {
                    return "Needs the Anvil-WebOverlay mod (optional dependency).";
                }

                if (!IsUsable)
                {
                    return $"Needs Anvil-WebOverlay {MinimumVersion}+ (installed: {_foundVersion}).";
                }

                if (!IsDisplayModeSupported())
                {
                    return "Not available in exclusive fullscreen; use borderless or windowed.";
                }

                return null;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool IsDisplayModeSupported()
        {
            return WebOverlay.WebOverlayPlugin.IsDisplayModeSupported;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static object Create(
            string title,
            string pageFolder,
            string pageHost,
            string fontsFolder,
            string fontsHost,
            int width,
            int height,
            int[] closeKeys,
            bool devTools)
        {
            var options = new WebOverlay.OverlayOptions
            {
                Frame = true,
                CloseKeys = closeKeys,
                DevTools = devTools,
                Width = width,
                Height = height,
                Dispatch = WebOverlay.EventDispatch.Manual,
                FreeCursorWhileShown = true,
                ClickThroughWhenUnfocused = true,
                InjectTheme = false,
                VirtualHosts = new[]
                {
                    new WebOverlay.VirtualHost(pageHost, pageFolder),
                    new WebOverlay.VirtualHost(fontsHost, fontsFolder)
                    {
                        Access = WebOverlay.HostAccess.Allow
                    }
                }
            };
            return WebOverlay.WebOverlays.Create(title, options);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Navigate(object handle, string url)
        {
            ((WebOverlay.IWebOverlay)handle).Navigate(url);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Show(object handle) => ((WebOverlay.IWebOverlay)handle).Show();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Hide(object handle) => ((WebOverlay.IWebOverlay)handle).Hide();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Toggle(object handle) => ((WebOverlay.IWebOverlay)handle).Toggle();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool IsVisible(object handle) => ((WebOverlay.IWebOverlay)handle).IsVisible;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool IsPageLoaded(object handle) => ((WebOverlay.IWebOverlay)handle).IsPageLoaded;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void PumpEvents(object handle) => ((WebOverlay.IWebOverlay)handle).PumpEvents();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Post(object handle, string channel, string payload)
        {
            ((WebOverlay.IWebOverlay)handle).Post(channel, payload);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void PostRetained(object handle, string channel, string payload)
        {
            ((WebOverlay.IWebOverlay)handle).Post(channel, payload, WebOverlay.PostOptions.Retain);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void OpenDevTools(object handle) => ((WebOverlay.IWebOverlay)handle).OpenDevTools();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Dispose(object handle) => ((WebOverlay.IWebOverlay)handle).Dispose();
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Subscribe(
            object handle,
            Action onPageLoaded,
            Action<string, string> onChannelMessage,
            Action<bool> onVisibilityChanged,
            Action<string, string> onFailed,
            Action onChannelsFailed,
            Func<string, string, string> onRequest,
            string[] requestChannels)
        {
            var overlay = (WebOverlay.IWebOverlay)handle;
            overlay.PageLoaded += onPageLoaded;
            overlay.ChannelMessage += onChannelMessage;
            overlay.VisibilityChanged += onVisibilityChanged;
            overlay.ChannelsFailed += onChannelsFailed;
            object captured = handle;
            overlay.Failed += () =>
            {
                var failed = (WebOverlay.IWebOverlay)captured;
                onFailed(failed.Failure.ToString(), failed.FailureMessage);
            };
            foreach (string channel in requestChannels)
            {
                string channelName = channel;
                overlay.OnRequest(channelName, payload => onRequest(channelName, payload));
            }
        }
    }
}
