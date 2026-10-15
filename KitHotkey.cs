using System;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HSUIKit
{
    // A key + modifier combo, serializable as "Ctrl+Shift+G" for config files.
    public struct KitHotkey
    {
        public KeyCode Key;
        public bool Ctrl, Shift, Alt;

        public KitHotkey(KeyCode key, bool ctrl, bool shift, bool alt)
        {
            Key = key; Ctrl = ctrl; Shift = shift; Alt = alt;
        }

        public bool IsNone { get { return Key == KeyCode.None; } }

        private static bool CtrlHeld { get { return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl); } }
        private static bool ShiftHeld { get { return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift); } }
        private static bool AltHeld { get { return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt); } }

        private bool ModsMatch { get { return CtrlHeld == Ctrl && ShiftHeld == Shift && AltHeld == Alt; } }

        // Pressed this frame with exactly these modifiers (and not while a kit field is capturing).
        public bool IsDown()
        {
            return !IsNone && !KitHotkeyField.AnyCapturing && Input.GetKeyDown(Key) && ModsMatch;
        }

        public bool IsHeld()
        {
            return !IsNone && !KitHotkeyField.AnyCapturing && Input.GetKey(Key) && ModsMatch;
        }

        public string Serialize()
        {
            if (IsNone) return "None";
            StringBuilder sb = new StringBuilder();
            if (Ctrl) sb.Append("Ctrl+");
            if (Shift) sb.Append("Shift+");
            if (Alt) sb.Append("Alt+");
            sb.Append(Key.ToString());
            return sb.ToString();
        }

        public static KitHotkey Parse(string s)
        {
            KitHotkey k = new KitHotkey();
            if (string.IsNullOrEmpty(s)) return k;
            string[] parts = s.Split('+');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) k.Ctrl = true;
                else if (p.Equals("Shift", StringComparison.OrdinalIgnoreCase)) k.Shift = true;
                else if (p.Equals("Alt", StringComparison.OrdinalIgnoreCase)) k.Alt = true;
                else if (p.Length > 0 && !p.Equals("None", StringComparison.OrdinalIgnoreCase))
                {
                    try { k.Key = (KeyCode)Enum.Parse(typeof(KeyCode), p, true); }
                    catch { k.Key = KeyCode.None; }
                }
            }
            return k;
        }

        public override string ToString()
        {
            if (IsNone) return "None";
            StringBuilder sb = new StringBuilder();
            if (Ctrl) sb.Append("Ctrl + ");
            if (Shift) sb.Append("Shift + ");
            if (Alt) sb.Append("Alt + ");
            sb.Append(KeyName(Key));
            return sb.ToString();
        }

        public static string KeyName(KeyCode k)
        {
            string n = k.ToString();
            if (n.StartsWith("Alpha") && n.Length == 6) return n.Substring(5);
            if (n.StartsWith("Keypad")) return "Num " + n.Substring(6);
            switch (k)
            {
                case KeyCode.BackQuote: return "`";
                case KeyCode.Minus: return "-";
                case KeyCode.Equals: return "=";
                case KeyCode.LeftBracket: return "[";
                case KeyCode.RightBracket: return "]";
                case KeyCode.Semicolon: return ";";
                case KeyCode.Quote: return "'";
                case KeyCode.Comma: return ",";
                case KeyCode.Period: return ".";
                case KeyCode.Slash: return "/";
                case KeyCode.Backslash: return "\\";
                case KeyCode.Return: return "Enter";
                case KeyCode.PageUp: return "PgUp";
                case KeyCode.PageDown: return "PgDn";
                case KeyCode.UpArrow: return "Up";
                case KeyCode.DownArrow: return "Down";
                case KeyCode.LeftArrow: return "Left";
                case KeyCode.RightArrow: return "Right";
                case KeyCode.Mouse2: return "Middle Mouse";
                case KeyCode.Mouse3: return "Mouse 4";
                case KeyCode.Mouse4: return "Mouse 5";
            }
            return n;
        }
    }

    // Click, press a combo, done. Esc cancels, Backspace/Delete clears, clicking elsewhere cancels.
    public class KitHotkeyField : KitElement, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerClickHandler
    {
        private static int _capturing;
        private static KeyCode[] _allKeys;

        public static bool AnyCapturing { get { return _capturing > 0; } }

        public HotkeyHandler OnChanged;

        private KitHotkey _value;
        private Text _label, _keyText;
        private Image _box, _outline;
        private RectTransform _boxRt;
        private KitIconButton _clear;
        private bool _listening, _hover;
        private int _startFrame;

        public KitHotkey Value
        {
            get { return _value; }
            set { _value = value; Redraw(); }
        }

        public static KitHotkeyField Create(Transform parent, string label, KitHotkey value, HotkeyHandler onChanged)
        {
            RectTransform row = KitLayout.Row(parent, 8f);
            row.name = "Hotkey " + label;
            Kit.Layout(row.gameObject, -1f, 34f);

            Text lbl = Kit.MakeText(row, "Label", label, Theme.FontSize, Theme.Text, TextAnchor.MiddleLeft);
            lbl.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            RectTransform box = Kit.Rect(row, "Box");
            Kit.Layout(box.gameObject, 170f, 34f);
            KitHotkeyField f = box.gameObject.AddComponent<KitHotkeyField>();
            f._boxRt = box;
            f._label = lbl;
            f._value = value;
            f.OnChanged = onChanged;
            f._box = box.gameObject.AddComponent<Image>();
            f._box.raycastTarget = true;
            f._keyText = Kit.MakeText(box, "Keys", "", Theme.FontSize, Theme.Text, TextAnchor.MiddleCenter);
            Kit.Fill(f._keyText.rectTransform, 8f, 0f, 8f, 0f);
            f._outline = Kit.Image(box, "Outline", null, Color.clear);
            Kit.Fill(f._outline.rectTransform);

            f._clear = KitIconButton.Create(row, Sprites.Cross, 26f, false, delegate { f.Commit(new KitHotkey()); });
            KitTooltip.Attach(f._clear.gameObject, "Clear hotkey");

            f.Finish();
            f.Redraw();
            return f;
        }

        protected override void ApplyTheme()
        {
            _label.font = Kit.Font; _label.fontSize = Theme.FontSize; _label.color = Theme.Text;
            _keyText.font = Kit.Font; _keyText.fontSize = Theme.FontSize;
            Kit.SetSprite(_box, Sprites.Rounded(Theme.RadiusSmall));
            Kit.SetSprite(_outline, Sprites.Outline(Theme.RadiusSmall, 1));
            Redraw();
        }

        private void Redraw()
        {
            if (_keyText == null) return;
            if (_listening)
            {
                string mods = "";
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) mods += "Ctrl + ";
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) mods += "Shift + ";
                if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) mods += "Alt + ";
                _keyText.text = mods.Length > 0 ? mods + "…" : "Press a key…";
                _keyText.color = Theme.Accent;
                _keyText.fontStyle = FontStyle.Italic;
            }
            else
            {
                _keyText.text = _value.ToString();
                _keyText.color = _value.IsNone ? Theme.TextDim : Theme.Text;
                _keyText.fontStyle = _value.IsNone ? FontStyle.Italic : FontStyle.Bold;
            }
            _box.color = _listening ? Kit.Shade(Theme.Surface, -0.15f) : (_hover ? Theme.SurfaceHover : Theme.Surface);
            if (!_listening) _outline.color = Theme.Border;
            _clear.gameObject.SetActive(!_value.IsNone && !_listening);
        }

        public void OnPointerEnter(PointerEventData e) { _hover = true; Redraw(); }

        public void OnPointerExit(PointerEventData e) { _hover = false; Redraw(); }

        public void OnPointerDown(PointerEventData e) { }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            if (!_listening) StartListening();
        }

        private void StartListening()
        {
            if (_allKeys == null) _allKeys = (KeyCode[])Enum.GetValues(typeof(KeyCode));
            _listening = true;
            _capturing++;
            _startFrame = Time.frameCount;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            Tween.Scale(_boxRt, 1.04f, 0.2f, Ease.OutBack);
            Redraw();
        }

        private void StopListening()
        {
            if (!_listening) return;
            _listening = false;
            _capturing--;
            Tween.Scale(_boxRt, 1f, 0.25f, Ease.OutBack);
            Redraw();
        }

        private void Commit(KitHotkey k)
        {
            StopListening();
            _value = k;
            Redraw();
            Tween.Tint(_outline, Theme.Success, 0.1f, Ease.OutQuad, delegate { Tween.Tint(_outline, Theme.Border, 0.5f, Ease.InQuad); });
            if (OnChanged != null)
            {
                try { OnChanged(k); }
                catch (Exception ex) { Log.Write("KitHotkeyField.OnChanged threw: " + ex); }
            }
        }

        private void Update()
        {
            if (!_listening) return;

            // Pulsing accent outline while waiting.
            float pulse = 0.45f + 0.4f * Mathf.Sin(Time.unscaledTime * 6f);
            _outline.color = Kit.Alpha(Theme.Accent, pulse);
            Redraw();

            if (Time.frameCount == _startFrame) return; // ignore the click that started listening

            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(_boxRt, Input.mousePosition, null)) { StopListening(); return; }
            }
            if (!Input.anyKeyDown) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { StopListening(); return; }
            if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Delete)) { Commit(new KitHotkey()); return; }

            for (int i = 0; i < _allKeys.Length; i++)
            {
                KeyCode k = _allKeys[i];
                if (IsModifier(k) || k == KeyCode.Mouse0 || k == KeyCode.Mouse1 || k >= KeyCode.JoystickButton0) continue;
                if (!Input.GetKeyDown(k)) continue;
                Commit(new KitHotkey(k,
                    Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl),
                    Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift),
                    Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)));
                return;
            }
        }

        private static bool IsModifier(KeyCode k)
        {
            return k == KeyCode.LeftControl || k == KeyCode.RightControl || k == KeyCode.LeftShift || k == KeyCode.RightShift ||
                   k == KeyCode.LeftAlt || k == KeyCode.RightAlt || k == KeyCode.LeftCommand || k == KeyCode.RightCommand ||
                   k == KeyCode.LeftWindows || k == KeyCode.RightWindows || k == KeyCode.AltGr;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            StopListening();
        }
    }
}
