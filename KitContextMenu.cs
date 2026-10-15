using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HSUIKit
{
    // Fluent menu description: new KitMenu().Item("Copy", onCopy, "Ctrl+C").Separator().Item(...)
    public class KitMenu
    {
        internal class Entry
        {
            public string Label;
            public string Shortcut;
            public VoidHandler OnClick;
            public bool Enabled = true;
            public bool Separator;
            public bool Danger;
        }

        internal readonly List<Entry> Entries = new List<Entry>();

        public KitMenu Item(string label, VoidHandler onClick, string shortcut = null, bool enabled = true)
        {
            Entries.Add(new Entry { Label = label, OnClick = onClick, Shortcut = shortcut, Enabled = enabled });
            return this;
        }

        public KitMenu Danger(string label, VoidHandler onClick, string shortcut = null)
        {
            Entries.Add(new Entry { Label = label, OnClick = onClick, Shortcut = shortcut, Danger = true });
            return this;
        }

        // Runs the first enabled item whose label starts with `label` (self-tests drive menus this way).
        public bool Invoke(string label)
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                Entry e = Entries[i];
                if (e.Separator || !e.Enabled || e.OnClick == null || !e.Label.StartsWith(label)) continue;
                e.OnClick();
                return true;
            }
            return false;
        }

        public KitMenu Separator()
        {
            Entries.Add(new Entry { Separator = true });
            return this;
        }
    }

    public static class KitContextMenu
    {
        private const float ItemH = 30f, SepH = 9f, Pad = 5f;

        private static RectTransform _panel;
        private static RectTransform _blocker;
        private static readonly VoidHandler CloseHandler = Close;

        // Right-click anywhere on `go` opens the menu built by `build` at the cursor.
        public static KitContextTrigger Attach(GameObject go, MenuBuilder build)
        {
            Kit.HitArea(go); // also turns on raycasts for an existing non-raycast background
            KitContextTrigger t = go.GetComponent<KitContextTrigger>();
            if (t == null) t = go.AddComponent<KitContextTrigger>();
            t.Build = build;
            return t;
        }

        public static void Show(KitMenu menu, Vector2 screen)
        {
            Kit.Ensure();
            KitTooltip.HideNow();
            KitPopup.Open(CloseHandler);
            _blocker = KitPopup.CreateBlocker(Close);

            RectTransform panel = Kit.Rect(Kit.PopupLayer, "ContextMenu");
            _panel = panel;
            CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            int blur = Mathf.Max(1, Theme.ShadowSize / 2);
            Image shadow = Kit.Image(panel, "Shadow", Sprites.Shadow(Theme.RadiusSmall, blur), new Color(0f, 0f, 0f, Theme.ShadowAlpha));
            Kit.Fill(shadow.rectTransform, -blur, -blur - 3f, -blur, -blur + 3f);
            Image bg = Kit.Image(panel, "Bg", Sprites.Rounded(Theme.RadiusSmall), Kit.Alpha(Theme.Surface, 1f), true);
            Kit.Fill(bg.rectTransform);

            // Build rows, measuring text to size the panel.
            float y = Pad, labelW = 0f, shortW = 0f;
            List<RectTransform> rows = new List<RectTransform>();
            for (int i = 0; i < menu.Entries.Count; i++)
            {
                KitMenu.Entry e = menu.Entries[i];
                if (e.Separator)
                {
                    Image line = Kit.Image(panel, "Separator", null, Theme.Border);
                    RectTransform lr = line.rectTransform;
                    lr.anchorMin = new Vector2(0f, 1f);
                    lr.anchorMax = new Vector2(1f, 1f);
                    lr.pivot = new Vector2(0.5f, 0.5f);
                    lr.sizeDelta = new Vector2(-16f, 1f);
                    lr.anchoredPosition = new Vector2(0f, -(y + SepH * 0.5f));
                    y += SepH;
                    continue;
                }
                RectTransform row = BuildRow(panel, e, y, rows.Count);
                rows.Add(row);
                Text lt = row.Find("Inner/Label").GetComponent<Text>();
                labelW = Mathf.Max(labelW, lt.preferredWidth);
                Transform st = row.Find("Inner/Shortcut");
                if (st != null) shortW = Mathf.Max(shortW, st.GetComponent<Text>().preferredWidth);
                y += ItemH;
            }
            y += Pad;
            float width = Mathf.Max(170f, labelW + (shortW > 0f ? shortW + 28f : 0f) + 40f);

            Image outline = Kit.Image(panel, "Outline", Sprites.Outline(Theme.RadiusSmall, 1), Theme.Border);
            Kit.Fill(outline.rectTransform);

            KitPopup.PlaceAtPoint(panel, screen, new Vector2(width, y));
            Tween.Fade(group, 1f, 0.12f, Ease.OutQuad);
            Tween.SlideIn(panel, new Vector2(0f, 6f), 0.16f, Ease.OutCubic);
        }

        private static RectTransform BuildRow(RectTransform panel, KitMenu.Entry e, float y, int index)
        {
            RectTransform row = Kit.Rect(panel, "Item " + e.Label);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.sizeDelta = new Vector2(-2f * Pad, ItemH);
            row.anchoredPosition = new Vector2(0f, -y);
            Image hover = row.gameObject.AddComponent<Image>();
            Kit.SetSprite(hover, Sprites.Rounded(Mathf.Max(0, Theme.RadiusSmall - 2)));
            Color hoverColor = e.Danger ? Theme.Error : Theme.Accent;
            hover.color = Kit.Alpha(hoverColor, 0f);
            hover.raycastTarget = true;

            RectTransform inner = Kit.Rect(row, "Inner");
            Kit.Fill(inner);
            CanvasGroup ig = inner.gameObject.AddComponent<CanvasGroup>();
            ig.blocksRaycasts = false;
            ig.alpha = 0f;
            Color textColor = e.Danger ? Theme.Error : Theme.Text;
            Text label = Kit.MakeText(inner, "Label", e.Label, Theme.FontSize, textColor, TextAnchor.MiddleLeft);
            Kit.Fill(label.rectTransform, 12f, 0f, 12f, 0f);
            Text shortcut = null;
            if (!string.IsNullOrEmpty(e.Shortcut))
            {
                shortcut = Kit.MakeText(inner, "Shortcut", e.Shortcut, Theme.SmallSize, Theme.TextDim, TextAnchor.MiddleRight);
                Kit.Fill(shortcut.rectTransform, 12f, 0f, 12f, 0f);
            }

            float delay = Mathf.Min(index, 10) * 0.018f;
            Tween.Fade(ig, e.Enabled ? 1f : 0.4f, 0.16f, Ease.OutQuad, null, delay);
            inner.anchoredPosition = new Vector2(-6f, 0f);
            Tween.MoveX(inner, 0f, 0.22f, Ease.OutCubic, null, delay);

            if (!e.Enabled) return row;
            KitHoverClick h = KitHoverClick.On(row.gameObject);
            h.OnEnter = delegate
            {
                Tween.Tint(hover, hoverColor, 0.08f, Ease.OutQuad);
                label.color = Color.white;
                if (shortcut != null) shortcut.color = Kit.Alpha(Color.white, 0.75f);
            };
            h.OnExit = delegate
            {
                Tween.Tint(hover, Kit.Alpha(hoverColor, 0f), 0.15f, Ease.OutQuad);
                label.color = textColor;
                if (shortcut != null) shortcut.color = Theme.TextDim;
            };
            VoidHandler click = e.OnClick;
            h.OnClick = delegate
            {
                Close();
                if (click == null) return;
                try { click(); }
                catch (System.Exception ex) { Log.Write("KitContextMenu item threw: " + ex); }
            };
            return row;
        }

        public static bool IsOpen { get { return _panel != null; } }

        public static void Close()
        {
            KitPopup.Forget(CloseHandler);
            if (_blocker != null) Object.Destroy(_blocker.gameObject);
            _blocker = null;
            if (_panel == null) return;
            RectTransform panel = _panel;
            _panel = null;
            CanvasGroup g = panel.GetComponent<CanvasGroup>();
            g.blocksRaycasts = false;
            Tween.Fade(g, 0f, 0.1f, Ease.InQuad, delegate { if (panel != null) Object.Destroy(panel.gameObject); });
        }
    }

    public class KitContextTrigger : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
    {
        public MenuBuilder Build;

        public void OnPointerDown(PointerEventData e) { } // claim the press so the click routes here

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Right || Build == null) return;
            KitMenu menu = null;
            try { menu = Build(); }
            catch (System.Exception ex) { Log.Write("KitContextTrigger build threw: " + ex); }
            if (menu != null) KitContextMenu.Show(menu, e.position);
        }
    }
}
