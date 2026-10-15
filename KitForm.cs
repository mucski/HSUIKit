using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HSUIKit
{
    // A settings window built from "bind to a getter/setter" rows, for plugins that just want a
    // panel of toggles, sliders, text fields and buttons. Same call shape as the old HSDebugUI
    // DebugPanel (AddToggle/AddSlider/AddTextField/AddButton/AddLabel/AddDynamicLabel/AddHeader,
    // Toggle/Show/Hide/Visible), so migrating a plugin is mostly swapping the type.
    //
    // Layout (1.3.0): each section - the rows after a header, and any rows before the first one - sits in a
    // card, like the showcase. The first time the window opens it shrinks to its content if that is shorter.
    //
    // Controls show the getter's value when the window opens and are re-synced about five times a
    // second while it is visible, so values changed elsewhere (hotkeys, other windows) show up.
    // A control being dragged or typed into is never overwritten.
    public sealed class KitForm
    {
        private readonly KitWindow _window;
        private readonly RectTransform _content;
        private readonly List<Action> _syncs = new List<Action>();
        private readonly Action<string> _log;
        private KitCard _card;      // current section's card; created by the first row after a header
        private bool _fitted;

        public KitWindow Window { get { return _window; } }
        public bool Visible { get { return _window.Visible; } }

        public static KitForm Create(string title, float width, float height, string rememberId)
        {
            return Create(title, width, height, rememberId, null);
        }

        public static KitForm Create(string title, float width, float height, string rememberId, Action<string> log)
        {
            return new KitForm(title, width, height, rememberId, log);
        }

        private KitForm(string title, float width, float height, string rememberId, Action<string> log)
        {
            _log = log;
            _window = KitWindow.Create(title, width, height).Resizable(new Vector2(360f, 240f), new Vector2(1400f, 1400f));
            if (!string.IsNullOrEmpty(rememberId)) _window.Remember(rememberId);
            KitScroll scroll = KitScroll.Create(_window.Content, 18, 14, 10f, true);
            _content = scroll.Content;
            KitFormDriver driver = _window.gameObject.AddComponent<KitFormDriver>();
            driver.Form = this;
        }

        public void Toggle() { _window.Toggle(); }
        public void Show() { _window.Show(); Resync(); }

        // Rows go into the current section's card.
        private RectTransform Target
        {
            get
            {
                if (_card == null) _card = KitCard.Create(_content);
                return _card.Content;
            }
        }

        // First time visible: drop the empty space below short content (never grows the window).
        internal void FitOnce()
        {
            if (_fitted) return;
            _fitted = true;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            float content = LayoutUtility.GetPreferredHeight(_content);
            _window.ShrinkToContent(content);
        }
        public void Hide() { _window.Hide(); }

        public KitText AddHeader(string text)
        {
            _card = null;
            return KitLayout.Header(_content, text);
        }

        public KitText AddLabel(string text)
        {
            return KitLayout.Label(Target, text, TextRole.Dim);
        }

        public KitText AddDynamicLabel(Func<string> text)
        {
            KitText t = KitLayout.Label(Target, SafeString(text), TextRole.Body);
            _syncs.Add(delegate
            {
                string s = SafeString(text);
                if (t != null && t.Value != s) t.Value = s;
            });
            return t;
        }

        public KitSwitch AddToggle(string label, Func<bool> get, Action<bool> set)
        {
            KitSwitch sw = KitSwitch.Create(Target, label, SafeBool(get), delegate(bool v) { Call(set, v); });
            _syncs.Add(delegate
            {
                bool v = SafeBool(get);
                if (sw != null && sw.Value != v) sw.SetValue(v, false, false);
            });
            return sw;
        }

        public KitSlider AddSlider(string label, float min, float max, Func<float> get, Action<float> set)
        {
            return AddSlider(label, min, max, get, set, "F2");
        }

        public KitSlider AddSlider(string label, float min, float max, Func<float> get, Action<float> set, string format)
        {
            KitSlider sl = KitSlider.Create(Target, label, min, max, SafeFloat(get), false, delegate(float v) { Call(set, v); });
            sl.Format = format;
            _syncs.Add(delegate
            {
                if (sl == null || sl.Dragging) return;
                float v = SafeFloat(get);
                if (Mathf.Abs(sl.Value - v) > 1e-5f) sl.Value = v;
            });
            return sl;
        }

        public KitSlider AddIntSlider(string label, int min, int max, Func<int> get, Action<int> set)
        {
            KitSlider sl = KitSlider.Create(Target, label, min, max, SafeInt(get), true, delegate(float v) { Call(set, Mathf.RoundToInt(v)); });
            sl.Format = "0";
            _syncs.Add(delegate
            {
                if (sl == null || sl.Dragging) return;
                int v = SafeInt(get);
                if (Mathf.RoundToInt(sl.Value) != v) sl.Value = v;
            });
            return sl;
        }

        // Commits on Return or when the field loses focus (not on every keystroke).
        public KitInput AddTextField(string label, Func<string> get, Action<string> commit)
        {
            RectTransform target = Target;
            KitLayout.Label(target, label, TextRole.Small);
            KitInput input = KitInput.Create(target, "", SafeString(get), null);
            input.Field.onEndEdit.AddListener(delegate(string s) { Call(commit, s); });
            _syncs.Add(delegate
            {
                if (input == null || input.Field.isFocused) return;
                string s = SafeString(get);
                if (input.Text != s) input.Text = s;
            });
            return input;
        }

        public KitButton AddButton(string label, Action onClick)
        {
            return KitButton.Create(Target, label, ButtonStyle.Secondary, delegate { Call(onClick); });
        }

        internal void Resync()
        {
            for (int i = 0; i < _syncs.Count; i++)
            {
                try { _syncs[i](); }
                catch (Exception ex) { Log(ex); }
            }
        }

        // ---- helpers: a throwing plugin getter/setter must never take the window down ----

        private void Log(Exception ex)
        {
            try { if (_log != null) _log("KitForm: " + ex); }
            catch (Exception) { }
        }

        private void Call(Action a)
        {
            try { if (a != null) a(); }
            catch (Exception ex) { Log(ex); }
        }

        private void Call<T>(Action<T> a, T v)
        {
            try { if (a != null) a(v); }
            catch (Exception ex) { Log(ex); }
        }

        private bool SafeBool(Func<bool> f)
        {
            try { return f != null && f(); }
            catch (Exception ex) { Log(ex); return false; }
        }

        private float SafeFloat(Func<float> f)
        {
            try { return f != null ? f() : 0f; }
            catch (Exception ex) { Log(ex); return 0f; }
        }

        private int SafeInt(Func<int> f)
        {
            try { return f != null ? f() : 0; }
            catch (Exception ex) { Log(ex); return 0; }
        }

        private string SafeString(Func<string> f)
        {
            try { return f != null ? (f() ?? "") : ""; }
            catch (Exception ex) { Log(ex); return ""; }
        }
    }

    internal sealed class KitFormDriver : MonoBehaviour
    {
        public KitForm Form;
        private float _next;

        private void Update()
        {
            if (Form == null || !Form.Visible) return;
            Form.FitOnce();
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.2f;
            Form.Resync();
        }
    }
}
