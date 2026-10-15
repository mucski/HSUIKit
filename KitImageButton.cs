using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HSUIKit
{
    // Picture button / thumbnail card. The image is cover-cropped (fills the frame, keeps its
    // aspect) via RawImage.uvRect and zooms slightly on hover. Image, placeholder and hover tint
    // are KitRoundedImage meshes, so the rounding is real geometry and correct on any background
    // (the earlier corner-paint overlay showed as squares wherever the background differed).
    // Shows a pulsing placeholder until SetImage() is called, then fades the image in.
    public class KitImageButton : KitElement, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public RectTransform Rect;
        public VoidHandler OnClick;
        public VoidHandler OnDoubleClick;
        public object Tag; // caller data (file path, index...)

        private bool _card;
        private RectTransform _frame;
        private KitRoundedImage _image, _placeholder, _hoverTint;
        private Image _ring, _cardBg, _cardOutline;
        private Text _title, _subtitle;
        private CanvasGroup _imageGroup;
        private Rect _baseUv = new Rect(0f, 0f, 1f, 1f);
        private float _zoom = 1f;
        private bool _hover, _down, _selected, _hasImage;
        private float _captionH;
        private string _fullTitle;

        public bool Selected
        {
            get { return _selected; }
            set
            {
                if (_selected == value) return;
                _selected = value;
                Tween.Tint(_ring, value ? Theme.Accent : Kit.Alpha(Theme.Accent, 0f), 0.18f, Ease.OutQuad);
                if (_title != null) Tween.Tint(_title, value ? Theme.Accent : Theme.Text, 0.18f, Ease.OutQuad);
            }
        }

        public string Title
        {
            get { return _fullTitle; }
            set { _fullTitle = value; FitTitle(); }
        }

        public string Subtitle
        {
            get { return _subtitle != null ? _subtitle.text : null; }
            set { if (_subtitle != null) _subtitle.text = value; }
        }

        // Square (or any size) image-only button.
        public static KitImageButton Create(Transform parent, Texture image, Vector2 size, VoidHandler onClick)
        {
            KitImageButton b = Build(parent, size, 0f, false, onClick);
            if (image != null) b.SetImage(image, false);
            return b;
        }

        public static KitImageButton Create(Transform parent, Sprite image, Vector2 size, VoidHandler onClick)
        {
            KitImageButton b = Build(parent, size, 0f, false, onClick);
            if (image != null) b.SetImage(image, false);
            return b;
        }

        // Thumbnail card: image on top, title (+ optional subtitle) underneath.
        public static KitImageButton CreateCard(Transform parent, Texture image, Vector2 imageSize, string title, string subtitle, VoidHandler onClick)
        {
            float captionH = string.IsNullOrEmpty(subtitle) ? 30f : 46f;
            KitImageButton b = Build(parent, imageSize, captionH, true, onClick);
            b._captionH = captionH;
            b._fullTitle = title ?? "";
            // One line, shortened with an ellipsis when it doesn't fit (wrapping silently cut the 2nd line).
            b._title = Kit.MakeText(b.Rect, "Title", b._fullTitle, Theme.FontSize, Theme.Text, TextAnchor.UpperLeft);
            Kit.Anchor(b._title.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(10f, captionH - 8f), new Vector2(imageSize.x - 20f, 20f));
            b._title.rectTransform.pivot = new Vector2(0f, 1f);
            if (!string.IsNullOrEmpty(subtitle))
            {
                b._subtitle = Kit.MakeText(b.Rect, "Subtitle", subtitle, Theme.SmallSize, Theme.TextDim, TextAnchor.UpperLeft);
                b._subtitle.horizontalOverflow = HorizontalWrapMode.Wrap;
                b._subtitle.verticalOverflow = VerticalWrapMode.Truncate;
                Kit.Anchor(b._subtitle.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(10f, captionH - 27f), new Vector2(imageSize.x - 20f, 18f));
                b._subtitle.rectTransform.pivot = new Vector2(0f, 1f);
            }
            b._ring.transform.SetAsLastSibling();
            b.ApplyThemeNow();
            if (image != null) b.SetImage(image, false);
            return b;
        }

        private static KitImageButton Build(Transform parent, Vector2 imageSize, float captionH, bool card, VoidHandler onClick)
        {
            Vector2 total = new Vector2(imageSize.x, imageSize.y + captionH);
            RectTransform rt = Kit.Rect(parent, card ? "ThumbnailCard" : "ImageButton");
            rt.sizeDelta = total;
            Kit.Layout(rt.gameObject, total.x, total.y);
            KitImageButton b = rt.gameObject.AddComponent<KitImageButton>();
            b.Rect = rt;
            b._card = card;
            b.OnClick = onClick;

            if (card)
            {
                b._cardBg = Kit.Image(rt, "CardBg", null, Color.clear, true);
                Kit.Fill(b._cardBg.rectTransform);
            }
            else
            {
                Kit.HitArea(rt.gameObject);
            }

            // Frame = the image area (whole button, or the top part of a card).
            b._frame = Kit.Rect(rt, "Frame");
            b._frame.anchorMin = new Vector2(0f, 1f);
            b._frame.anchorMax = new Vector2(1f, 1f);
            b._frame.pivot = new Vector2(0.5f, 1f);
            b._frame.sizeDelta = new Vector2(0f, imageSize.y);
            b._frame.anchoredPosition = Vector2.zero;

            b._placeholder = Rounded(b._frame, "Placeholder", Color.clear);

            b._image = Rounded(b._frame, "Image", Color.white);
            b._image.enabled = false;
            b._imageGroup = b._image.gameObject.AddComponent<CanvasGroup>();
            b._imageGroup.blocksRaycasts = false;

            b._hoverTint = Rounded(b._frame, "HoverTint", new Color(1f, 1f, 1f, 0f));

            if (card)
            {
                b._cardOutline = Kit.Image(rt, "CardOutline", null, Color.clear);
                Kit.Fill(b._cardOutline.rectTransform);
            }
            b._ring = Kit.Image(rt, "SelectRing", null, Color.clear);
            Kit.Fill(b._ring.rectTransform, -3f, -3f, -3f, -3f);

            b.Finish();
            return b;
        }

        private static KitRoundedImage Rounded(RectTransform parent, string name, Color color)
        {
            RectTransform rt = Kit.Rect(parent, name);
            Kit.Fill(rt);
            KitRoundedImage img = rt.gameObject.AddComponent<KitRoundedImage>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        protected override void ApplyTheme()
        {
            int r = _card ? Theme.Radius : Theme.RadiusSmall;
            // A card's image sits in its top edge: round the top (matching the card), keep the
            // bottom square where it meets the caption.
            KitRoundedImage[] layers = { _placeholder, _image, _hoverTint };
            for (int i = 0; i < layers.Length; i++)
            {
                layers[i].Radius = r;
                layers[i].SetCorners(true, !_card);
            }
            if (_card)
            {
                Kit.SetSprite(_cardBg, Sprites.Rounded(r));
                _cardBg.color = Theme.Surface;
                Kit.SetSprite(_cardOutline, Sprites.Outline(r, 1));
                _cardOutline.color = Theme.Border;
            }
            Kit.SetSprite(_ring, Sprites.Outline(r + 3, 2));
            _ring.color = _selected ? Theme.Accent : Kit.Alpha(Theme.Accent, 0f);
            if (_title != null)
            {
                _title.font = Kit.Font; _title.fontSize = Theme.FontSize; _title.color = _selected ? Theme.Accent : Theme.Text;
            }
            if (_subtitle != null)
            {
                _subtitle.font = Kit.Font; _subtitle.fontSize = Theme.SmallSize; _subtitle.color = Theme.TextDim;
            }
            FitTitle();
        }

        // New image-area size (KitGridFit stretches cells to fill a row). The caption keeps its height.
        public void Resize(Vector2 imageSize)
        {
            Vector2 total = new Vector2(imageSize.x, imageSize.y + _captionH);
            if (Rect.sizeDelta == total && _frame.sizeDelta.y == imageSize.y) return;
            Rect.sizeDelta = total;
            LayoutElement le = GetComponent<LayoutElement>();
            if (le != null) { le.preferredWidth = total.x; le.preferredHeight = total.y; }
            _frame.sizeDelta = new Vector2(0f, imageSize.y);
            if (_title != null) _title.rectTransform.sizeDelta = new Vector2(imageSize.x - 20f, _title.rectTransform.sizeDelta.y);
            if (_subtitle != null) _subtitle.rectTransform.sizeDelta = new Vector2(imageSize.x - 20f, _subtitle.rectTransform.sizeDelta.y);
            FitTitle();
        }

        private void FitTitle()
        {
            if (_title == null || _fullTitle == null) return;
            string s = _fullTitle;
            float max = _title.rectTransform.sizeDelta.x;
            TextGenerator gen = _title.cachedTextGeneratorForLayout;
            TextGenerationSettings set = _title.GetGenerationSettings(Vector2.zero);
            float ppu = _title.pixelsPerUnit;
            if (max > 0f && gen.GetPreferredWidth(s, set) / ppu > max)
            {
                while (s.Length > 1 && gen.GetPreferredWidth(s.TrimEnd() + "\u2026", set) / ppu > max) s = s.Substring(0, s.Length - 1);
                s = s.TrimEnd() + "\u2026";
            }
            if (_title.text != s) _title.text = s;
        }

        // "Nothing to show": stops the loading shimmer and leaves a plain placeholder.
        public void SetNone()
        {
            _hasImage = true;
            _image.enabled = false;
            _image.texture = null;
            _placeholder.color = Kit.Alpha(Theme.SurfaceActive, 0.35f);
        }

        public Texture CurrentTexture { get { return _image.enabled ? _image.texture : null; } }

        public void SetImage(Sprite sprite, bool fadeIn)
        {
            if (sprite == null) { SetImage((Texture)null, fadeIn); return; }
            Rect tr = sprite.textureRect;
            Texture tex = sprite.texture;
            _baseUv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
            ApplyImage(tex, tr.width, tr.height, fadeIn);
        }

        public void SetImage(Texture tex, bool fadeIn)
        {
            _baseUv = new Rect(0f, 0f, 1f, 1f);
            if (tex == null)
            {
                _hasImage = false;
                _image.enabled = false;
                _image.texture = null;
                return;
            }
            ApplyImage(tex, tex.width, tex.height, fadeIn);
        }

        private void ApplyImage(Texture tex, float texW, float texH, bool fadeIn)
        {
            // Cover-crop: shrink the UV window along the axis the texture has "extra" of.
            float frameAspect = _frame.rect.width > 0f ? _frame.rect.width / _frame.rect.height : Rect.sizeDelta.x / Mathf.Max(1f, _frame.sizeDelta.y);
            float texAspect = texW / Mathf.Max(1f, texH);
            Rect uv = _baseUv;
            if (texAspect > frameAspect)
            {
                float w = uv.width * (frameAspect / texAspect);
                uv.x += (uv.width - w) * 0.5f;
                uv.width = w;
            }
            else
            {
                float h = uv.height * (texAspect / frameAspect);
                uv.y += (uv.height - h) * 0.5f;
                uv.height = h;
            }
            _baseUv = uv;
            _image.texture = tex;
            _image.enabled = true;
            _hasImage = true;
            _placeholder.color = Kit.Alpha(Theme.SurfaceActive, 0f);
            ApplyZoom();
            if (fadeIn)
            {
                _imageGroup.alpha = 0f;
                Tween.Fade(_imageGroup, 1f, 0.35f, Ease.OutQuad);
                _image.transform.localScale = new Vector3(1.06f, 1.06f, 1f);
                Tween.Scale(_image.transform, 1f, 0.5f, Ease.OutCubic);
            }
            else
            {
                _imageGroup.alpha = 1f;
            }
        }

        private void ApplyZoom()
        {
            Rect u = _baseUv;
            float w = u.width * _zoom, h = u.height * _zoom;
            _image.uvRect = new Rect(u.x + (u.width - w) * 0.5f, u.y + (u.height - h) * 0.5f, w, h);
        }

        private void ZoomTo(float z, float duration, Ease ease)
        {
            float from = _zoom;
            Tween.Run(this, "zoom", duration, ease, delegate(float t) { _zoom = Tween.Lerp(from, z, t); ApplyZoom(); });
        }

        private void Update()
        {
            // Loading shimmer until an image arrives.
            if (_hasImage) return;
            float a = 0.55f + 0.25f * Mathf.Sin(Time.unscaledTime * 3.5f + Rect.anchoredPosition.x * 0.02f);
            _placeholder.color = Kit.Alpha(Theme.SurfaceActive, a);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            _hover = true;
            Tween.Tint(_hoverTint, new Color(1f, 1f, 1f, 0.07f), 0.15f, Ease.OutQuad);
            if (!_down) Tween.Scale(transform, 1.035f, 0.25f, Ease.OutCubic);
            ZoomTo(0.92f, 0.5f, Ease.OutCubic);
        }

        public void OnPointerExit(PointerEventData e)
        {
            _hover = false;
            Tween.Tint(_hoverTint, new Color(1f, 1f, 1f, 0f), 0.2f, Ease.OutQuad);
            if (!_down) Tween.Scale(transform, 1f, 0.3f, Ease.OutCubic);
            ZoomTo(1f, 0.45f, Ease.OutCubic);
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            _down = true;
            Tween.Scale(transform, 0.97f, 0.09f, Ease.OutQuad);
            int r = _card ? Theme.Radius : Theme.RadiusSmall;
            KitRipple.Play(_frame, _hoverTint.transform.GetSiblingIndex() + 1, e.position, r, true, !_card,
                new Color(1f, 1f, 1f, 0.25f), 0.6f);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!_down) return;
            _down = false;
            Tween.Scale(transform, _hover ? 1.035f : 1f, 0.35f, Ease.OutBack);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            Invoke(OnClick);
            if (e.clickCount == 2) Invoke(OnDoubleClick);
        }
    }
}
