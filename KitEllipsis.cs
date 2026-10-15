using UnityEngine;
using UnityEngine.UI;

namespace HSUIKit
{
    // One-line text that ends in "…" when it doesn't fit its box, re-fitted whenever the box changes width.
    // For names in lists/cards: wrapping into a one-line box silently cut the second line off.
    //   KitEllipsis.Set(text, fullString);
    public class KitEllipsis : MonoBehaviour
    {
        private Text _text;
        private string _full = "";
        private float _fittedWidth = -1f;
        private bool _pending;

        public string Full { get { return _full; } }

        public static KitEllipsis Set(Text text, string full)
        {
            if (text == null) return null;
            KitEllipsis e = text.GetComponent<KitEllipsis>();
            if (e == null) e = text.gameObject.AddComponent<KitEllipsis>();
            e._text = text;
            e._full = full ?? "";
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            e._fittedWidth = -1f;
            e.Fit();
            return e;
        }

        // Fitting inside a layout pass would dirty the layout again: note it and fit after the pass.
        private void OnRectTransformDimensionsChange() { _pending = true; }

        private void LateUpdate()
        {
            if (!_pending) return;
            _pending = false;
            if (_text != null && _text.rectTransform.rect.width != _fittedWidth) Fit();
        }

        private void Fit()
        {
            if (_text == null) return;
            float max = _text.rectTransform.rect.width;
            _fittedWidth = max;
            string s = _full;
            if (max > 0f)
            {
                TextGenerator gen = _text.cachedTextGeneratorForLayout;
                TextGenerationSettings set = _text.GetGenerationSettings(Vector2.zero);
                float ppu = _text.pixelsPerUnit;
                if (gen.GetPreferredWidth(s, set) / ppu > max)
                {
                    while (s.Length > 1 && gen.GetPreferredWidth(s.TrimEnd() + "…", set) / ppu > max) s = s.Substring(0, s.Length - 1);
                    s = s.TrimEnd() + "…";
                }
            }
            if (_text.text != s) _text.text = s;
        }
    }
}
