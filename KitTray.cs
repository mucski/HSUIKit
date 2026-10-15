using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NeoUIManager.Integration;
using UnityEngine;

namespace HSUIKit
{
    // One shared place for every HSUIKit-based plugin to put its main window in NeoUIManager's
    // pet tray (window API v1, see NeoUIManagerIntegration.pdf). The vendor helper is compiled
    // once into HSUIKit instead of into every plugin. Safe without NeoUIManager installed:
    // registration just keeps retrying quietly (5,10,20 then every 30 s) and the plugin's own
    // hotkey stays in charge - callers should only skip their own hotkey when IsRegistered is true.
    public static class KitTray
    {
        private static readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();
        private static GameObject _host;

        private sealed class Entry
        {
            public object Owner = new object();
            public string Id, Name, Version, Shortcut;
            public Texture2D Icon;
            public Action Open, Toggle;
            public Func<bool> CanOpen;
            public NeoUiPluginWindowClient Client;
            public float NextTry, Delay = 5f;
            public string LastError;
        }

        /// <summary>Register a KitWindow as a plugin's main window. Plugin id: lowercase, e.g. "mucski.hsfocusdof".</summary>
        public static void Register(string pluginId, string displayName, string version, Texture2D icon,
            string defaultShortcut, KitWindow window)
        {
            Register(pluginId, displayName, version, icon, defaultShortcut,
                delegate { if (window != null && !window.Visible) window.Show(); },
                delegate { if (window != null) window.Toggle(); },
                delegate { return window != null; });
        }

        /// <summary>Register with explicit callbacks (all run on the main thread). canOpen may be null.</summary>
        public static void Register(string pluginId, string displayName, string version, Texture2D icon,
            string defaultShortcut, Action open, Action toggle, Func<bool> canOpen)
        {
            Entry e;
            if (!_entries.TryGetValue(pluginId, out e))
            {
                e = new Entry();
                _entries[pluginId] = e;
            }
            e.Id = pluginId; e.Name = displayName; e.Version = version; e.Icon = icon;
            e.Shortcut = defaultShortcut ?? "None"; e.Open = open; e.Toggle = toggle; e.CanOpen = canOpen;
            e.NextTry = 0f; e.Delay = 5f;
            EnsureHost();
            TryNow(e);
        }

        // Dev (KitInspect): open every registered plugin window, so windows built on first open exist too.
        internal static void OpenAllForInspection()
        {
            foreach (Entry e in _entries.Values)
            {
                try { if (e.Open != null && (e.CanOpen == null || e.CanOpen())) e.Open(); }
                catch (Exception ex) { Log.Write("inspect: opening " + e.Id + " threw " + ex.Message); }
            }
        }

        /// <summary>True once NeoUIManager accepted this plugin. While false, keep your own hotkey active.</summary>
        public static bool IsRegistered(string pluginId)
        {
            Entry e;
            return _entries.TryGetValue(pluginId, out e) && e.Client != null && e.Client.IsActive;
        }

        /// <summary>The shortcut currently in effect (the user's choice), or null when not registered.</summary>
        public static string GetBinding(string pluginId)
        {
            Entry e;
            string binding, error;
            if (_entries.TryGetValue(pluginId, out e) && e.Client != null && e.Client.TryGetBinding(out binding, out error))
                return binding;
            return null;
        }

        public static void Unregister(string pluginId)
        {
            Entry e;
            if (!_entries.TryGetValue(pluginId, out e)) return;
            _entries.Remove(pluginId);
            if (e.Client != null)
            {
                try { e.Client.Dispose(); }
                catch (Exception ex) { Debug.LogWarning("[HSUIKit] Tray unregister failed for " + pluginId + ": " + ex.Message); }
            }
        }

        /// <summary>Loads a 64x64 PNG embedded in the caller's assembly. Keep the result in a static field.</summary>
        public static Texture2D LoadIcon(Assembly assembly, string resourceName)
        {
            Texture2D texture = null;
            try
            {
                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null || stream.Length <= 0 || stream.Length > 65536) return null;
                    byte[] bytes = new byte[(int)stream.Length];
                    int read = 0;
                    while (read < bytes.Length)
                    {
                        int count = stream.Read(bytes, read, bytes.Length - read);
                        if (count <= 0) return null;
                        read += count;
                    }
                    texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                    if (!texture.LoadImage(bytes) || texture.width != 64 || texture.height != 64) return null;
                    texture.name = resourceName;
                    texture.wrapMode = TextureWrapMode.Clamp;
                    texture.filterMode = FilterMode.Bilinear;
                    texture.hideFlags = HideFlags.HideAndDontSave;
                    Texture2D result = texture;
                    texture = null;
                    return result;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[HSUIKit] Tray icon '" + resourceName + "' unavailable, using initials: " + ex.Message);
                return null;
            }
            finally
            {
                if (texture != null) UnityEngine.Object.Destroy(texture);
            }
        }

        private static void EnsureHost()
        {
            if (_host != null) return;
            _host = new GameObject("HSUIKit Tray");
            UnityEngine.Object.DontDestroyOnLoad(_host);
            _host.AddComponent<TrayRetry>();
        }

        private static void TryNow(Entry e)
        {
            if (e.Client != null && e.Client.IsActive) return;
            NeoUiPluginWindowClient client;
            string error;
            if (NeoUiPluginWindowClient.TryRegister(e.Owner, e.Id, e.Name, e.Version, e.Icon, e.Shortcut,
                e.Open, e.Toggle, e.CanOpen, out client, out error))
            {
                e.Client = client;
                e.LastError = null;
                return;
            }
            if (error != e.LastError)
            {
                e.LastError = error;
                Debug.LogWarning("[HSUIKit] NeoUIManager registration for " + e.Id + ": " + error + " (retrying)");
            }
            e.NextTry = Time.realtimeSinceStartup + e.Delay;
            e.Delay = Mathf.Min(e.Delay * 2f, 30f);
        }

        private sealed class TrayRetry : MonoBehaviour
        {
            private float _nextScan;
            private readonly List<Entry> _scratch = new List<Entry>();

            // Retries are 5 s apart at the shortest, so looking once a second is plenty, and it must not
            // allocate: this runs for the whole session.
            private void Update()
            {
                float now = Time.realtimeSinceStartup;
                if (now < _nextScan) return;
                _nextScan = now + 1f;
                _scratch.Clear();
                _scratch.AddRange(_entries.Values);
                for (int i = 0; i < _scratch.Count; i++)
                {
                    Entry e = _scratch[i];
                    if (e.Client != null && e.Client.IsActive) continue;
                    if (now >= e.NextTry) TryNow(e);
                }
            }
        }
    }
}
