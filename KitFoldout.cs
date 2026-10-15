using UnityEngine;
using UnityEngine.UI;

namespace HSUIKit
{
    // Collapsible card: header with rotating chevron, body whose height animates between 0 and
    // its content's preferred height (LayoutElement.preferredHeight on a RectMask2D clip).
    public class KitFoldout : KitElement
    {
        private const float HeaderH = 40f;

        public RectTransform Content;
        public BoolHandler OnToggled;

        private Image _bg, _outline, _hover, _chevron;
        private Text _title;
        private LayoutElement _bodyLayout;
        private CanvasGroup _contentGroup;
        private bool _open;
        private bool _animating;

        public bool IsOpen { get { return _open; } }

        public static KitFoldout Create(Transform parent, string title, bool open)
        {
            RectTransform rt = Kit.Rect(parent, "Foldout " + title);
            KitFoldout f = rt.gameObject.AddComponent<KitFoldout>();
            f._open = open;
            f._bg = rt.gameObject.AddComponent<Image>();
            f._bg.raycastTarget = false;
            VerticalLayoutGroup v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            RectTransform header = Kit.Rect(rt, "Header");
            Kit.Layout(header.gameObject, -1f, HeaderH);
            f._hover = Kit.Image(header, "Hover", null, Color.clear, true);
            Kit.Fill(f._hover.rectTransform, 3f, 3f, 3f, 3f);
            f._title = Kit.MakeText(header, "Title", title, Theme.FontSize, Theme.Text, TextAnchor.MiddleLeft);
            Kit.Fill(f._title.rectTransform, 38f, 0f, 12f, 0f);
            f._chevron = Kit.Image(header, "Chevron", Sprites.Chevron, Color.white);
            Kit.Anchor(f._chevron.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(20f, 0f), new Vector2(18f, 18f));
            f._chevron.transform.localEulerAngles = new Vector3(0f, 0f, open ? 0f : 90f);

            KitHoverClick hc = KitHoverClick.On(f._hover.gameObject);
            hc.OnEnter = delegate { Tween.Tint(f._hover, Kit.Alpha(Theme.SurfaceHover, 1f), 0.12f, Ease.OutQuad); };
            hc.OnExit = delegate { Tween.Tint(f._hover, Kit.Alpha(Theme.SurfaceHover, 0f), 0.2f, Ease.OutQuad); };
            hc.OnClick = delegate { f.SetOpen(!f._open, true); };

            RectTransform body = Kit.Rect(rt, "Body");
            body.gameObject.AddComponent<RectMask2D>();
            f._bodyLayout = body.gameObject.AddComponent<LayoutElement>();

            RectTransform content = Kit.Rect(body, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            content.anchoredPosition = Vector2.zero;
            VerticalLayoutGroup cv = content.gameObject.AddComponent<VerticalLayoutGroup>();
            cv.padding = new RectOffset(14, 14, 2, 14);
            cv.spacing = 10f;
            cv.childForceExpandWidth = true;
            cv.childForceExpandHeight = false;
            ContentSizeFitter fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            f._contentGroup = content.gameObject.AddComponent<CanvasGroup>();
            f._contentGroup.alpha = open ? 1f : 0f;
            f.Content = content;

            f._bodyLayout.preferredHeight = 0f;
            f._bodyLayout.minHeight = 0f;

            f._outline = Kit.Image(rt, "Outline", null, Color.clear);
            Kit.Fill(f._outline.rectTransform);
            Kit.IgnoreLayout(f._outline.gameObject);

            f.Finish();
            return f;
        }

        protected override void ApplyTheme()
        {
            Kit.SetSprite(_bg, Sprites.Rounded(Theme.Radius));
            _bg.color = Theme.Surface;
            Kit.SetSprite(_outline, Sprites.Outline(Theme.Radius, 1));
            _outline.color = Theme.Border;
            Kit.SetSprite(_hover, Sprites.Rounded(Mathf.Max(0, Theme.Radius - 3)));
            _chevron.color = Theme.TextDim;
            _title.font = Kit.Font;
            _title.fontSize = Theme.FontSize;
            _title.fontStyle = FontStyle.Bold;
            _title.color = Theme.Text;
        }

        public void SetOpen(bool open, bool animate)
        {
            _open = open;
            float from = _bodyLayout.preferredHeight;
            float to = open ? LayoutUtility.GetPreferredHeight(Content) : 0f;
            Tween.Rotate(_chevron.transform, open ? 0f : 90f, animate ? 0.3f : 0f, Ease.OutBack);
            if (!animate)
            {
                _bodyLayout.preferredHeight = to;
                _contentGroup.alpha = open ? 1f : 0f;
                return;
            }
            _animating = true;
            Tween.Run(this, "height", open ? 0.34f : 0.26f, open ? Ease.OutCubic : Ease.InOutCubic, delegate(float t)
            {
                _bodyLayout.preferredHeight = Mathf.Max(0f, Tween.Lerp(from, to, t));
            }, delegate { _animating = false; });
            Tween.Fade(_contentGroup, open ? 1f : 0f, open ? 0.3f : 0.15f, Ease.OutQuad, null, open ? 0.06f : 0f);
            if (OnToggled != null)
            {
                try { OnToggled(open); }
                catch (System.Exception ex) { Log.Write("KitFoldout.OnToggled threw: " + ex); }
            }
        }

        // While open and idle, follow the content's real height (children added later,
        // wrapped text reflowing on resize, nested foldouts opening).
        private void LateUpdate()
        {
            if (!_open || _animating) return;
            float h = Content.rect.height;
            if (Mathf.Abs(_bodyLayout.preferredHeight - h) > 0.5f) _bodyLayout.preferredHeight = h;
        }
    }
}
