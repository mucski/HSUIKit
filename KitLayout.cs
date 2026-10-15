using UnityEngine;
using UnityEngine.UI;

namespace HSUIKit
{
    public enum TextRole { Body, Dim, Header, Small, Accent }

    // Themed plain label.
    public class KitText : KitElement
    {
        public Text Text;
        public TextRole Role;

        public string Value
        {
            get { return Text.text; }
            set { Text.text = value; }
        }

        public static KitText Create(Transform parent, string text, TextRole role = TextRole.Body, bool wrap = true)
        {
            Text t = Kit.MakeText(parent, "Text", text, Theme.FontSize, Theme.Text, TextAnchor.UpperLeft);
            if (wrap) t.horizontalOverflow = HorizontalWrapMode.Wrap;
            KitText k = t.gameObject.AddComponent<KitText>();
            k.Text = t;
            k.Role = role;
            k.Finish();
            return k;
        }

        protected override void ApplyTheme()
        {
            Text.font = Kit.Font;
            Text.fontStyle = Role == TextRole.Header ? FontStyle.Bold : FontStyle.Normal;
            switch (Role)
            {
                case TextRole.Header: Text.fontSize = Theme.TitleSize; Text.color = Theme.Text; break;
                case TextRole.Dim: Text.fontSize = Theme.FontSize; Text.color = Theme.TextDim; break;
                case TextRole.Small: Text.fontSize = Theme.SmallSize; Text.color = Theme.TextDim; break;
                case TextRole.Accent: Text.fontSize = Theme.FontSize; Text.color = Theme.Accent; break;
                default: Text.fontSize = Theme.FontSize; Text.color = Theme.Text; break;
            }
        }
    }

    // Rounded surface panel with a vertical stack inside.
    public class KitCard : KitElement
    {
        private Image _bg;
        private Image _outline;
        public RectTransform Content;

        public static KitCard Create(Transform parent, int padding = 14, float spacing = 10f)
        {
            RectTransform rt = Kit.Rect(parent, "Card");
            KitCard c = rt.gameObject.AddComponent<KitCard>();
            c._bg = rt.gameObject.AddComponent<Image>();
            c._bg.raycastTarget = false;
            VerticalLayoutGroup v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(padding, padding, padding, padding);
            v.spacing = spacing;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            c._outline = Kit.Image(rt, "Outline", null, Color.clear);
            Kit.Fill(c._outline.rectTransform);
            Kit.IgnoreLayout(c._outline.gameObject);

            c.Content = rt;
            c.Finish();
            return c;
        }

        protected override void ApplyTheme()
        {
            Kit.SetSprite(_bg, Sprites.Rounded(Theme.Radius));
            _bg.color = Theme.Card;
            Kit.SetSprite(_outline, Sprites.Outline(Theme.Radius, 1));
            _outline.color = Theme.Border;
            _outline.transform.SetAsLastSibling();
        }
    }

    public static class KitLayout
    {
        // Section header. Sections get more room above their header than between the header and its
        // own content (22 vs 10 px with the usual 10 px stack spacing) so they read as separate groups.
        public static KitText Header(Transform parent, string text)
        {
            if (parent.childCount > 0) Space(parent, SectionGap);
            return KitText.Create(parent, text, TextRole.Header);
        }

        public const float SectionGap = 2f;   // + the stack's own spacing on both sides

        public static KitText Label(Transform parent, string text, TextRole role = TextRole.Body)
        {
            return KitText.Create(parent, text, role);
        }

        public static RectTransform Row(Transform parent, float spacing = 8f, TextAnchor align = TextAnchor.MiddleLeft)
        {
            RectTransform rt = Kit.Rect(parent, "Row");
            HorizontalLayoutGroup h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = align;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            return rt;
        }

        public static RectTransform Space(Transform parent, float height)
        {
            RectTransform rt = Kit.Rect(parent, "Space");
            Kit.Layout(rt.gameObject, -1f, height);
            return rt;
        }

        // Wrapping grid of fixed-size cells (thumbnails, image buttons). Reports its own
        // preferred height from the available width, so it sits fine inside a scroll page.
        public static RectTransform Grid(Transform parent, Vector2 cellSize, float spacing = 10f)
        {
            RectTransform rt = Kit.Rect(parent, "Grid");
            GridLayoutGroup g = rt.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = cellSize;
            g.spacing = new Vector2(spacing, spacing);
            g.startCorner = GridLayoutGroup.Corner.UpperLeft;
            g.startAxis = GridLayoutGroup.Axis.Horizontal;
            g.childAlignment = TextAnchor.UpperLeft;
            g.constraint = GridLayoutGroup.Constraint.Flexible;
            g.padding = new RectOffset(4, 4, 4, 4); // room for hover-grow + selection ring
            return rt;
        }

        // Responsive grid: as many columns of at least minImage as fit, then the cells stretch to fill
        // the row exactly (image aspect kept; a short single row grows up to maxGrow). Children are
        // KitImageButtons (plain, or cards with a caption of captionH).
        public static RectTransform FlexGrid(Transform parent, Vector2 minImage, float captionH = 0f, float spacing = 10f, float maxGrow = 1.5f)
        {
            RectTransform rt = Grid(parent, new Vector2(minImage.x, minImage.y + captionH), spacing);
            // Flush with the text column; the 3 px selection ring may poke into the page margin.
            rt.GetComponent<GridLayoutGroup>().padding = new RectOffset(0, 0, 0, 0);
            KitGridFit fit = rt.gameObject.AddComponent<KitGridFit>();
            fit.MinImage = minImage;
            fit.Caption = captionH;
            fit.MaxGrow = maxGrow;
            return rt;
        }

        // Transparent flexible filler for rows (pushes following items to the right).
        public static RectTransform Flexible(Transform parent)
        {
            RectTransform rt = Kit.Rect(parent, "Flex");
            LayoutElement le = rt.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            return rt;
        }
    }
}
