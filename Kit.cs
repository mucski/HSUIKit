using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HSUIKit
{
    // Root of the kit: one persistent overlay canvas with four stacked layers (windows,
    // popups, toasts, tooltips), the tween runner, font resolution and small builder helpers.
    public static class Kit
    {
        public const int UILayer = 5;

        public static RectTransform WindowLayer;
        public static RectTransform PopupLayer;
        public static RectTransform ToastLayer;
        public static RectTransform TooltipLayer;

        private static GameObject _root;
        private static Canvas _canvas;
        private static CanvasScaler _scaler;
        private static Font _font;
        private static string _fontFor;
        private static string _fontStatus = "not loaded yet";

        public static float ScaleFactor { get { return _canvas != null ? _canvas.scaleFactor : 1f; } }
        public static Canvas Canvas { get { Ensure(); return _canvas; } }

        public static void Ensure()
        {
            if (_root != null) return;

            Theme.EnsureLoaded();

            _root = new GameObject("HSUIKit");
            _root.layer = UILayer;
            UnityEngine.Object.DontDestroyOnLoad(_root);

            _canvas = _root.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 30100; // just above HSDebugUI's 30000
            _scaler = _root.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            _scaler.scaleFactor = Theme.UIScale;
            _root.AddComponent<GraphicRaycaster>();
            _root.AddComponent<KitRunner>();
            _root.AddComponent<KitCameraGuard>();

            KitStats.Install();
            KitInspect.InstallIfRequested(_root);   // dev: inspect.txt -> audit + screenshot every window

            WindowLayer = Layer("Windows");
            PopupLayer = Layer("Popups");
            ToastLayer = Layer("Toasts");
            TooltipLayer = Layer("Tooltips");

            if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
            {
                GameObject es = new GameObject("HSUIKit EventSystem");
                UnityEngine.Object.DontDestroyOnLoad(es);
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
                Log.Write("Kit.Ensure: no EventSystem found, created one.");
            }
            Log.Write("Kit.Ensure: root created.");
        }

        private static RectTransform Layer(string name)
        {
            RectTransform rt = Rect(_root.transform, name);
            Fill(rt);
            return rt;
        }

        internal static void OnThemeChanged()
        {
            if (_scaler != null) _scaler.scaleFactor = Theme.UIScale;
        }

        // ---- font ----

        public static Font Font
        {
            get
            {
                if (_font == null || _fontFor != Theme.FontName) ResolveFont();
                return _font;
            }
        }

        public static string FontStatus { get { Font f = Font; return _fontStatus; } }

        private static void ResolveFont()
        {
            _fontFor = Theme.FontName;
            _font = null;
            bool installed = IsOsFontInstalled(Theme.FontName);
            if (installed)
            {
                try
                {
                    _font = Font.CreateDynamicFontFromOSFont(Theme.FontName, Theme.FontSize);
                }
                catch (Exception ex)
                {
                    Log.Write("Kit.ResolveFont: CreateDynamicFontFromOSFont threw: " + ex.Message);
                }
            }
            if (_font != null)
            {
                _fontStatus = "OS font '" + Theme.FontName + "'";
            }
            else
            {
                _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                _fontStatus = "built-in Arial ('" + Theme.FontName + "' " + (installed ? "failed to load" : "not installed") + ")";
            }
            Log.Write("Kit.ResolveFont: " + _fontStatus);
        }

        // Font.GetOSInstalledFontNames may not exist on every Unity build - reflect for it and
        // assume "installed" (let CreateDynamicFontFromOSFont decide) when it's missing.
        private static bool IsOsFontInstalled(string name)
        {
            try
            {
                MethodInfo m = typeof(Font).GetMethod("GetOSInstalledFontNames", BindingFlags.Public | BindingFlags.Static);
                if (object.ReferenceEquals(m, null)) return true;
                string[] names = m.Invoke(null, null) as string[];
                if (names == null) return true;
                for (int i = 0; i < names.Length; i++)
                {
                    if (string.Equals(names[i], name, StringComparison.OrdinalIgnoreCase)) return true;
                }
                return false;
            }
            catch
            {
                return true;
            }
        }

        // ---- builder helpers ----

        public static RectTransform Rect(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = UILayer;
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static void Fill(RectTransform rt)
        {
            Fill(rt, 0f, 0f, 0f, 0f);
        }

        // Stretch to parent with insets (left, bottom, right, top).
        public static void Fill(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        public static Image Image(Transform parent, string name, Sprite sprite, Color color, bool raycast = false)
        {
            RectTransform rt = Rect(parent, name);
            Image img = rt.gameObject.AddComponent<Image>();
            SetSprite(img, sprite);
            img.color = color;
            img.raycastTarget = raycast;
            KitThemeColor.AutoBind(img, color);   // a theme colour keeps following the theme
            return img;
        }

        public static void SetSprite(Image img, Sprite sprite)
        {
            img.sprite = sprite;
            img.type = sprite != null && sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            img.fillCenter = true;
        }

        // Transparent full-rect raycast target, so an otherwise graphic-less object gets events.
        // Invisible pointer target. Reuses an existing Graphic (a GameObject can only hold one)
        // and just enables raycasts; otherwise adds a KitRaycastTarget, which generates no
        // geometry (a transparent Image was still batched, drawn and clipped every frame).
        public static Graphic HitArea(GameObject go)
        {
            Graphic existing = go.GetComponent<Graphic>();
            if (existing != null)
            {
                existing.raycastTarget = true;
                return existing;
            }
            KitRaycastTarget g = go.AddComponent<KitRaycastTarget>();
            g.raycastTarget = true;
            return g;
        }

        public static Text MakeText(Transform parent, string name, string text, int size, Color color, TextAnchor anchor)
        {
            RectTransform rt = Rect(parent, name);
            Text t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.text = text;
            t.raycastTarget = false;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            rt.gameObject.AddComponent<KitTextSnap>();   // keeps glyphs on whole pixels (crisp text)
            KitThemeColor.AutoBind(t, color);             // a theme colour keeps following the theme
            return t;
        }

        public static LayoutElement Layout(GameObject go, float preferredWidth, float preferredHeight)
        {
            LayoutElement le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.preferredWidth = preferredWidth;
            le.preferredHeight = preferredHeight;
            if (preferredWidth >= 0f) le.minWidth = preferredWidth;
            if (preferredHeight >= 0f) le.minHeight = preferredHeight;
            // A fixed size must not also be flexible. Without this, a control whose own layout
            // group force-expands its children (KitSegmented) reports flexibleHeight = 1 upward,
            // and in a parent with spare room (a window body) it balloons. Explicit flexible values set by callers are kept.
            if (preferredWidth >= 0f && le.flexibleWidth < 0f) le.flexibleWidth = 0f;
            if (preferredHeight >= 0f && le.flexibleHeight < 0f) le.flexibleHeight = 0f;
            return le;
        }

        public static void IgnoreLayout(GameObject go)
        {
            LayoutElement le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
        }

        // ---- color helpers ----

        public static Color Alpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        // amount > 0 lightens toward white, < 0 darkens toward black. Keeps alpha.
        public static Color Shade(Color c, float amount)
        {
            Color target = amount >= 0f ? Color.white : Color.black;
            float t = Mathf.Abs(amount);
            return new Color(c.r + (target.r - c.r) * t, c.g + (target.g - c.g) * t, c.b + (target.b - c.b) * t, c.a);
        }

        public static Vector2 ScreenToLocal(RectTransform space, Vector2 screen)
        {
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(space, screen, null, out local);
            return local;
        }

        public static Vector2 WorldToLocal(RectTransform space, Vector3 world)
        {
            return ScreenToLocal(space, RectTransformUtility.WorldToScreenPoint(null, world));
        }
    }

    internal sealed class KitRunner : MonoBehaviour
    {
        private void Update()
        {
            Tween.Tick(Mathf.Min(Time.unscaledDeltaTime, 0.1f));
            if (KitWindow.VisibleCount > 0) Theme.Poll();
        }
    }
}
