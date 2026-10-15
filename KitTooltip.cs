using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HSUIKit
{
    // One shared tooltip bubble in Kit.TooltipLayer. Attach(go, text) to any element.
    public class KitTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
    {
        private const float Delay = 0.45f, MaxWidth = 260f;

        public string Text;

        private static RectTransform _view;
        private static CanvasGroup _group;
        private static Image _bg;
        private static Text _text;
        private static KitTooltip _owner;

        public static KitTooltip Attach(GameObject go, string text)
        {
            KitTooltip t = go.GetComponent<KitTooltip>();
            if (t == null) t = go.AddComponent<KitTooltip>();
            t.Text = text;
            return t;
        }

        public void OnPointerEnter(PointerEventData e)
        {
            KitTooltip self = this;
            Tween.Run(this, "tip", 0f, Ease.Linear, delegate(float t) { }, delegate { ShowFor(self); }, Delay);
        }

        public void OnPointerExit(PointerEventData e)
        {
            Tween.Kill(this, "tip");
            if (_owner == this) Hide();
        }

        public void OnPointerDown(PointerEventData e)
        {
            Tween.Kill(this, "tip");
            if (_owner == this) Hide();
        }

        private void OnDisable()
        {
            Tween.Kill(this, "tip");
            if (_owner == this) HideNow();
        }

        private static void EnsureView()
        {
            if (_view != null) return;
            Kit.Ensure();
            _view = Kit.Rect(Kit.TooltipLayer, "Tooltip");
            _view.anchorMin = _view.anchorMax = new Vector2(0.5f, 0.5f);
            _view.pivot = new Vector2(0.5f, 0f);
            _group = _view.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.alpha = 0f;
            _bg = Kit.Image(_view, "Bg", null, Color.clear);
            Kit.Fill(_bg.rectTransform);
            _text = Kit.MakeText(_view, "Text", "", Theme.SmallSize, Color.white, TextAnchor.MiddleCenter);
            Kit.Fill(_text.rectTransform, 10f, 6f, 10f, 6f);
            _view.gameObject.SetActive(false);
        }

        private static void ShowFor(KitTooltip owner)
        {
            if (owner == null || !owner.isActiveAndEnabled || string.IsNullOrEmpty(owner.Text)) return;
            EnsureView();
            _owner = owner;
            _view.gameObject.SetActive(true);

            Kit.SetSprite(_bg, Sprites.Rounded(Mathf.Min(Theme.RadiusSmall, 8)));
            _bg.color = Kit.Alpha(Kit.Shade(Theme.SurfaceActive, 0.08f), 0.98f);
            _text.font = Kit.Font;
            _text.fontSize = Theme.SmallSize + 1;
            _text.color = Theme.Text;
            _text.text = owner.Text;

            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            float w = Mathf.Min(_text.preferredWidth, MaxWidth);
            _text.horizontalOverflow = HorizontalWrapMode.Wrap;
            _view.sizeDelta = new Vector2(w + 20f, 100f);
            _text.rectTransform.sizeDelta = new Vector2(-20f, -12f);
            float h = _text.preferredHeight;
            _view.sizeDelta = new Vector2(w + 20f, h + 12f);

            RectTransform target = (RectTransform)owner.transform;
            Vector3[] c = new Vector3[4];
            target.GetWorldCorners(c);
            RectTransform layer = Kit.TooltipLayer;
            Vector2 top = Kit.WorldToLocal(layer, (c[1] + c[2]) * 0.5f);
            Rect lr = layer.rect;
            float halfW = _view.sizeDelta.x * 0.5f;
            top.x = Mathf.Clamp(top.x, lr.xMin + halfW + 4f, lr.xMax - halfW - 4f);
            Vector2 pos = top + new Vector2(0f, 8f);
            if (pos.y + _view.sizeDelta.y > lr.yMax - 4f)
            {
                // No room above: flip below the target.
                Vector2 bottom = Kit.WorldToLocal(layer, (c[0] + c[3]) * 0.5f);
                pos = new Vector2(top.x, bottom.y - 8f - _view.sizeDelta.y);
            }

            _view.anchoredPosition = pos - new Vector2(0f, 5f);
            Tween.Fade(_group, 1f, 0.14f, Ease.OutQuad);
            Tween.Move(_view, pos, 0.22f, Ease.OutCubic);
        }

        private static void Hide()
        {
            if (_view == null) return;
            _owner = null;
            RectTransform v = _view;
            Tween.Fade(_group, 0f, 0.1f, Ease.InQuad, delegate { v.gameObject.SetActive(false); });
        }

        public static void HideNow()
        {
            if (_view == null) return;
            _owner = null;
            Tween.Kill(_group, "alpha");
            _group.alpha = 0f;
            _view.gameObject.SetActive(false);
        }
    }
}
