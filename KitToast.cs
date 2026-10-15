using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HSUIKit
{
    public enum ToastKind { Info, Success, Warning, Error }

    // Top-right notification stack. Newest on top; slides in from the right, the others glide
    // down to make room. Hovering pauses its timer, clicking dismisses it.
    public class KitToast : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private const float Width = 320f, Gap = 10f, Margin = 18f, OffX = Width + 60f;
        private const int MaxToasts = 5;
        private static readonly List<KitToast> Stack = new List<KitToast>();

        private RectTransform _rt;
        private CanvasGroup _group;
        private RectTransform _timerBar;
        private float _height;
        private float _duration, _remaining;
        private bool _hover, _closing;

        public static KitToast Show(string title, string message, ToastKind kind = ToastKind.Info, float seconds = 4f)
        {
            return Show(title, message, kind, seconds, null, null);
        }

        // Same, with an action button at the right edge (e.g. "Undo"): clicking it dismisses the toast, then runs
        // the action. A separate overload so plugins built against the 4-argument Show keep binding.
        public static KitToast Show(string title, string message, ToastKind kind, float seconds, string actionLabel, VoidHandler action)
        {
            Kit.Ensure();
            bool hasAction = !string.IsNullOrEmpty(actionLabel);
            float actionW = hasAction ? 76f : 0f;
            Color kc = KindColor(kind);

            RectTransform rt = Kit.Rect(Kit.ToastLayer, "Toast");
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            KitToast t = rt.gameObject.AddComponent<KitToast>();
            t._rt = rt;
            t._group = rt.gameObject.AddComponent<CanvasGroup>();
            t._duration = t._remaining = Mathf.Max(0.5f, seconds);

            int blur = Mathf.Max(1, Theme.ShadowSize / 2);
            Image shadow = Kit.Image(rt, "Shadow", Sprites.Shadow(Theme.Radius, blur), new Color(0f, 0f, 0f, Theme.ShadowAlpha * 0.9f));
            Kit.Fill(shadow.rectTransform, -blur, -blur - 4f, -blur, -blur + 4f);
            Image bg = Kit.Image(rt, "Bg", Sprites.Rounded(Theme.Radius), Kit.Alpha(Theme.Surface, 0.98f), true);
            Kit.Fill(bg.rectTransform);

            Image iconBg = Kit.Image(rt, "IconBg", Sprites.Circle, Kit.Alpha(kc, 0.18f));
            Kit.Anchor(iconBg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -14f), new Vector2(28f, 28f));
            if (kind == ToastKind.Success || kind == ToastKind.Error)
            {
                Image icon = Kit.Image(iconBg.rectTransform, "Icon", kind == ToastKind.Success ? Sprites.Check : Sprites.Cross, kc);
                Kit.Fill(icon.rectTransform, 5f, 5f, 5f, 5f);
            }
            else
            {
                Text glyph = Kit.MakeText(iconBg.rectTransform, "Glyph", kind == ToastKind.Warning ? "!" : "i", Theme.FontSize + 1, kc, TextAnchor.MiddleCenter);
                glyph.fontStyle = FontStyle.Bold;
                Kit.Fill(glyph.rectTransform);
            }

            float textW = Width - 56f - 16f - (hasAction ? actionW + 8f : 0f);
            Text titleText = Kit.MakeText(rt, "Title", title, Theme.FontSize, Theme.Text, TextAnchor.UpperLeft);
            titleText.fontStyle = FontStyle.Bold;
            Kit.Anchor(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(56f, -13f), new Vector2(textW, 20f));

            Text msg = Kit.MakeText(rt, "Message", message ?? "", Theme.SmallSize + 1, Theme.TextDim, TextAnchor.UpperLeft);
            msg.horizontalOverflow = HorizontalWrapMode.Wrap;
            Kit.Anchor(msg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(56f, -35f), new Vector2(textW, 20f));
            float msgH = string.IsNullOrEmpty(message) ? 0f : msg.preferredHeight;
            msg.rectTransform.sizeDelta = new Vector2(textW, msgH);

            t._height = Mathf.Max(56f, 35f + msgH + 16f);
            rt.sizeDelta = new Vector2(Width, t._height);

            Image timer = Kit.Image(rt, "Timer", null, Kit.Alpha(kc, 0.8f));
            t._timerBar = timer.rectTransform;
            t._timerBar.anchorMin = new Vector2(0f, 0f);
            t._timerBar.anchorMax = new Vector2(1f, 0f);
            t._timerBar.pivot = new Vector2(0f, 0f);
            t._timerBar.sizeDelta = new Vector2(-2f * Theme.Radius, 2f);
            t._timerBar.anchoredPosition = new Vector2(Theme.Radius, 1f);

            Image outline = Kit.Image(rt, "Outline", Sprites.Outline(Theme.Radius, 1), Theme.Border);
            Kit.Fill(outline.rectTransform);

            if (hasAction)
            {
                // the button claims its own press + click, so the toast's click-to-dismiss doesn't fire first
                KitButton b = KitButton.Create(rt, actionLabel, ButtonStyle.Secondary, delegate
                {
                    t.Dismiss();
                    if (action == null) return;
                    try { action(); }
                    catch (System.Exception ex) { Log.Write("KitToast action threw: " + ex); }
                });
                Kit.Anchor(b.Rect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(actionW, 30f));
            }

            Stack.Insert(0, t);
            rt.anchoredPosition = new Vector2(OffX, -Margin);
            t._group.alpha = 0f;
            Tween.MoveX(rt, -Margin, 0.45f, Ease.OutBack);
            Tween.Fade(t._group, 1f, 0.2f, Ease.OutQuad);
            Relayout(t);

            while (Stack.Count > MaxToasts) Stack[Stack.Count - 1].Dismiss();
            return t;
        }

        public static Color KindColor(ToastKind kind)
        {
            switch (kind)
            {
                case ToastKind.Success: return Theme.Success;
                case ToastKind.Warning: return Theme.Warning;
                case ToastKind.Error: return Theme.Error;
                default: return Theme.Accent;
            }
        }

        private static void Relayout(KitToast fresh)
        {
            float y = -Margin;
            for (int i = 0; i < Stack.Count; i++)
            {
                KitToast t = Stack[i];
                if (t == fresh)
                {
                    Vector2 p = t._rt.anchoredPosition; p.y = y; t._rt.anchoredPosition = p;
                }
                else
                {
                    Tween.MoveY(t._rt, y, 0.35f, Ease.OutCubic);
                }
                y -= t._height + Gap;
            }
        }

        public void Dismiss()
        {
            if (_closing) return;
            _closing = true;
            Stack.Remove(this);
            _group.blocksRaycasts = false;
            Tween.MoveX(_rt, OffX, 0.28f, Ease.InCubic);
            Tween.Fade(_group, 0f, 0.28f, Ease.InQuad, delegate { Destroy(gameObject); });
            Relayout(null);
        }

        private void Update()
        {
            if (_closing) return;
            if (!_hover) _remaining -= Time.unscaledDeltaTime;
            _timerBar.localScale = new Vector3(Mathf.Clamp01(_remaining / _duration), 1f, 1f);
            if (_remaining <= 0f) Dismiss();
        }

        private void OnDestroy()
        {
            Stack.Remove(this);
        }

        public void OnPointerEnter(PointerEventData e) { _hover = true; }

        public void OnPointerExit(PointerEventData e) { _hover = false; }

        public void OnPointerClick(PointerEventData e) { Dismiss(); }
    }
}
