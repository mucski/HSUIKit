using UnityEngine;
using UnityEngine.UI;

namespace HSUIKit
{
    // Centered dialog over a dimmed backdrop. onResult gets the clicked button's index, or -1
    // for Escape. The last button is styled Primary (or Danger when `danger` is set).
    public class KitModal : MonoBehaviour
    {
        private const float Width = 400f;

        private CanvasGroup _backdropGroup;
        private CanvasGroup _cardGroup;
        private RectTransform _card;
        private IntHandler _onResult;
        private bool _closing;
        private static int _open;

        private static int _escapeFrame = -1;

        // True while any modal is showing, or in the frame one was dismissed with Escape, so a window's own
        // Escape handling can leave that key press to the modal whatever order the scripts run in.
        public static bool AnyOpen { get { return _open > 0 || _escapeFrame == Time.frameCount; } }

        public static KitModal Show(string title, string message, string[] buttons, IntHandler onResult, bool danger = false)
        {
            Kit.Ensure();
            KitPopup.CloseAll();
            KitTooltip.HideNow();

            RectTransform root = Kit.Rect(Kit.PopupLayer, "Modal");
            Kit.Fill(root);
            KitModal m = root.gameObject.AddComponent<KitModal>();
            m._onResult = onResult;
            _open++;

            Image backdrop = Kit.Image(root, "Backdrop", null, new Color(0f, 0f, 0f, 0.55f), true);
            Kit.Fill(backdrop.rectTransform);
            m._backdropGroup = backdrop.gameObject.AddComponent<CanvasGroup>();
            m._backdropGroup.alpha = 0f;

            m._card = Kit.Rect(root, "Card");
            Kit.Anchor(m._card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(Width, 0f));
            m._cardGroup = m._card.gameObject.AddComponent<CanvasGroup>();
            m._cardGroup.alpha = 0f;
            VerticalLayoutGroup v = m._card.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(24, 24, 22, 20);
            v.spacing = 10f;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            ContentSizeFitter fit = m._card.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            int blur = Mathf.Max(1, Theme.ShadowSize);
            Image shadow = Kit.Image(m._card, "Shadow", Sprites.Shadow(Theme.Radius + 2, blur), new Color(0f, 0f, 0f, Theme.ShadowAlpha));
            Kit.Fill(shadow.rectTransform, -blur, -blur - 6f, -blur, -blur + 6f);
            Kit.IgnoreLayout(shadow.gameObject);
            Image bg = Kit.Image(m._card, "Bg", Sprites.Rounded(Theme.Radius + 2), Kit.Alpha(Theme.Surface, 1f), true);
            Kit.Fill(bg.rectTransform);
            Kit.IgnoreLayout(bg.gameObject);
            Image outline = Kit.Image(m._card, "Outline", Sprites.Outline(Theme.Radius + 2, 1), Theme.Border);
            Kit.Fill(outline.rectTransform);
            Kit.IgnoreLayout(outline.gameObject);

            KitText.Create(m._card, title, TextRole.Header);
            if (!string.IsNullOrEmpty(message)) KitText.Create(m._card, message, TextRole.Dim);
            KitLayout.Space(m._card, 6f);

            RectTransform row = KitLayout.Row(m._card, 8f, TextAnchor.MiddleRight);
            if (buttons == null || buttons.Length == 0) buttons = new[] { "OK" };
            for (int i = 0; i < buttons.Length; i++)
            {
                int index = i;
                bool last = i == buttons.Length - 1;
                ButtonStyle style = last ? (danger ? ButtonStyle.Danger : ButtonStyle.Primary) : ButtonStyle.Ghost;
                KitButton.Create(row, buttons[i], style, delegate { m.Close(index); });
            }

            m._card.anchoredPosition = new Vector2(m._card.anchoredPosition.x, 0f);
            Tween.Fade(m._backdropGroup, 1f, 0.2f, Ease.OutQuad);
            Tween.Fade(m._cardGroup, 1f, 0.18f, Ease.OutQuad);
            Tween.SlideIn(m._card, new Vector2(0f, -14f), 0.26f, Ease.OutCubic);
            return m;
        }

        public void Close(int result)
        {
            if (_closing) return;
            _closing = true;
            _open--;
            _cardGroup.blocksRaycasts = false;
            Tween.Fade(_backdropGroup, 0f, 0.18f, Ease.InQuad);
            Tween.Fade(_cardGroup, 0f, 0.16f, Ease.InQuad, delegate { Destroy(gameObject); });
            if (_onResult != null)
            {
                try { _onResult(result); }
                catch (System.Exception ex) { Log.Write("KitModal result handler threw: " + ex); }
            }
        }

        private void Update()
        {
            if (!_closing && Input.GetKeyDown(KeyCode.Escape))
            {
                _escapeFrame = Time.frameCount;
                Close(-1);
            }
        }

        private void OnDestroy()
        {
            if (!_closing) { _closing = true; _open--; }
        }

        // Asks for a line of text. onResult gets the text on confirm (button or Enter), or null when cancelled.
        public static KitModal Prompt(string title, string message, string initial, string placeholder, string confirm, StringHandler onResult)
        {
            KitInput input = null;
            KitModal m = Show(title, message, new[] { "Cancel", confirm }, delegate(int result)
            {
                if (onResult == null) return;
                onResult(result == 1 && input != null ? input.Text : null);
            });
            // the input goes between the message and the buttons (before the spacer)
            input = KitInput.Create(m._card, placeholder ?? "", initial ?? "", null);
            Kit.Layout(input.gameObject, -1f, 34f);
            input.transform.SetSiblingIndex(m._card.childCount - 3);
            input.OnSubmit = delegate(string text)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) m.Close(1);
            };
            UnityEngine.EventSystems.EventSystem es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null) es.SetSelectedGameObject(input.gameObject);
            input.Field.ActivateInputField();
            return m;
        }
    }

    // Round color chip with a selection ring (accent pickers, palette rows).
    public class KitSwatch : KitElement
    {
        public Color Color;
        public VoidHandler OnClick;

        private Image _ring, _chip;
        private bool _selected;

        public bool Selected
        {
            get { return _selected; }
            set
            {
                _selected = value;
                Tween.Scale(_ring.transform, value ? 1f : 0.6f, 0.3f, value ? Ease.OutBack : Ease.OutCubic);
                Tween.Tint(_ring, value ? Theme.Text : Kit.Alpha(Theme.Text, 0f), 0.2f, Ease.OutQuad);
            }
        }

        public static KitSwatch Create(Transform parent, Color color, float size, VoidHandler onClick)
        {
            RectTransform rt = Kit.Rect(parent, "Swatch");
            Kit.Layout(rt.gameObject, size, size);
            KitSwatch s = rt.gameObject.AddComponent<KitSwatch>();
            s.Color = color;
            s.OnClick = onClick;
            s._ring = Kit.Image(rt, "Ring", Sprites.Circle, Color.clear);
            Kit.Fill(s._ring.rectTransform);
            s._ring.transform.localScale = new Vector3(0.6f, 0.6f, 1f);
            Image gap = Kit.Image(s._ring.rectTransform, "Gap", Sprites.Circle, Color.black);
            Kit.Fill(gap.rectTransform, 2f, 2f, 2f, 2f);
            gap.gameObject.name = "Gap";
            s._chip = Kit.Image(rt, "Chip", Sprites.Circle, color, true);
            Kit.Fill(s._chip.rectTransform, 4f, 4f, 4f, 4f);

            KitHoverClick h = KitHoverClick.On(s._chip.gameObject);
            h.OnEnter = delegate { Tween.Scale(s._chip.transform, 1.12f, 0.2f, Ease.OutCubic); };
            h.OnExit = delegate { Tween.Scale(s._chip.transform, 1f, 0.2f, Ease.OutCubic); };
            h.OnDown = delegate { Tween.Scale(s._chip.transform, 0.9f, 0.08f, Ease.OutQuad); };
            h.OnUp = delegate { Tween.Scale(s._chip.transform, 1.12f, 0.3f, Ease.OutBack); };
            h.OnClick = delegate { Invoke(s.OnClick); };
            s.Finish();
            return s;
        }

        protected override void ApplyTheme()
        {
            _chip.color = Color;
            Transform gap = _ring.transform.Find("Gap");
            if (gap != null) gap.GetComponent<Image>().color = Kit.Alpha(Theme.Background, 1f);
            if (!_selected) _ring.color = Kit.Alpha(Theme.Text, 0f);
            else _ring.color = Theme.Text;
        }
    }
}
