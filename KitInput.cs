using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HSUIKit
{
    // Wraps the stock InputField (caret/selection/IME are not worth reimplementing) with a
    // themed body, hover state and an accent underline that grows from the center on focus.
    public class KitInput : KitElement, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public InputField Field;
        public StringHandler OnChanged;
        public StringHandler OnSubmit;

        private Image _bg, _outline, _underline;
        private Text _text, _placeholder;
        private bool _focused, _hover;

        public string Text
        {
            get { return Field.text; }
            set { Field.text = value; }
        }

        public static KitInput Create(Transform parent, string placeholder, string value, StringHandler onChanged)
        {
            RectTransform rt = Kit.Rect(parent, "Input");
            rt.gameObject.SetActive(false); // InputField must not OnEnable before textComponent is wired
            Kit.Layout(rt.gameObject, -1f, 34f);

            Image bg = rt.gameObject.AddComponent<Image>();
            bg.raycastTarget = true;

            Text text = Kit.MakeText(rt, "Text", "", Theme.FontSize, Theme.Text, TextAnchor.MiddleLeft);
            Kit.Fill(text.rectTransform, 12f, 0f, 12f, 0f);
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;

            Text ph = Kit.MakeText(rt, "Placeholder", placeholder, Theme.FontSize, Theme.TextDim, TextAnchor.MiddleLeft);
            Kit.Fill(ph.rectTransform, 12f, 0f, 12f, 0f);
            ph.fontStyle = FontStyle.Italic;
            // Clip a long placeholder at the box edge instead of drawing past it (typed text is
            // already kept inside by InputField). The rect is a full line tall, so Truncate is safe.
            ph.horizontalOverflow = HorizontalWrapMode.Wrap;
            ph.verticalOverflow = VerticalWrapMode.Truncate;

            Image outline = Kit.Image(rt, "Outline", null, Color.clear);
            Kit.Fill(outline.rectTransform);

            Image underline = Kit.Image(rt, "Underline", Sprites.Pill(2f), Color.clear);
            RectTransform u = underline.rectTransform;
            u.anchorMin = new Vector2(0f, 0f);
            u.anchorMax = new Vector2(1f, 0f);
            u.pivot = new Vector2(0.5f, 0f);
            u.sizeDelta = new Vector2(-12f, 2f);
            u.anchoredPosition = new Vector2(0f, 1f);
            u.localScale = new Vector3(0f, 1f, 1f);

            InputField f = rt.gameObject.AddComponent<InputField>();
            f.textComponent = text;
            f.placeholder = ph;
            f.targetGraphic = bg;
            f.transition = Selectable.Transition.None;
            f.lineType = InputField.LineType.SingleLine;
            Navigation nav = f.navigation;
            nav.mode = Navigation.Mode.None;
            f.navigation = nav;
            f.text = value ?? "";

            KitInput k = rt.gameObject.AddComponent<KitInput>();
            k.Field = f;
            k._bg = bg;
            k._text = text;
            k._placeholder = ph;
            k._outline = outline;
            k._underline = underline;
            k.OnChanged = onChanged;
            f.onValueChanged.AddListener(delegate(string s)
            {
                if (k.OnChanged != null) k.OnChanged(s);
            });
            f.onEndEdit.AddListener(delegate(string s)
            {
                if (k.OnSubmit != null) k.OnSubmit(s);
            });

            k.Finish();
            rt.gameObject.SetActive(true);
            return k;
        }

        protected override void ApplyTheme()
        {
            Kit.SetSprite(_bg, Sprites.Rounded(Theme.RadiusSmall));
            _bg.color = BgColor();
            Kit.SetSprite(_outline, Sprites.Outline(Theme.RadiusSmall, 1));
            _outline.color = OutlineColor();
            _underline.color = Theme.Accent;
            _text.font = Kit.Font; _text.fontSize = Theme.FontSize; _text.color = Theme.Text;
            _placeholder.font = Kit.Font; _placeholder.fontSize = Theme.FontSize; _placeholder.color = Kit.Alpha(Theme.TextDim, 0.8f);
            Field.selectionColor = Kit.Alpha(Theme.Accent, 0.35f);
        }

        private Color BgColor()
        {
            if (_focused) return Kit.Shade(Theme.Surface, -0.15f);
            return _hover ? Theme.SurfaceHover : Theme.Surface;
        }

        private Color OutlineColor()
        {
            return _focused ? Kit.Alpha(Theme.Accent, 0.55f) : Theme.Border;
        }

        private void Refresh()
        {
            Tween.Tint(_bg, BgColor(), 0.15f, Ease.OutQuad);
            Tween.Tint(_outline, OutlineColor(), 0.15f, Ease.OutQuad);
        }

        public void OnSelect(BaseEventData e)
        {
            _focused = true;
            Refresh();
            Tween.Scale(_underline.transform, Vector3.one, 0.3f, Ease.OutCubic);
        }

        public void OnDeselect(BaseEventData e)
        {
            _focused = false;
            Refresh();
            Tween.Scale(_underline.transform, new Vector3(0f, 1f, 1f), 0.2f, Ease.InQuad);
        }

        public void OnPointerEnter(PointerEventData e) { _hover = true; Refresh(); }

        public void OnPointerExit(PointerEventData e) { _hover = false; Refresh(); }
    }
}
