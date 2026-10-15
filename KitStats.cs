using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace HSUIKit
{
    // Measures UGUI's own per-frame work - CanvasUpdateRegistry.PerformUpdate: layout rebuilds,
    // graphic (mesh) rebuilds, RectMask2D clipping/culling - by swapping its willRenderCanvases
    // handler for a timed wrapper. This is ALL UGUI in the game (Studio's own UI too), not just
    // the kit. Native canvas batching after it can't be timed from here.
    //
    // Safety: the wrapper is only installed after confirming the original handler really was
    // removed, so PerformUpdate can never run twice per frame.
    public static class KitStats
    {
        private static Canvas.WillRenderCanvases _inner;
        private static bool _tried;
        private static long _windowTicks, _windowWorst;
        private static int _windowFrames;
        private static float _windowStart;
        private static readonly List<Graphic> GraphicScratch = new List<Graphic>();

        public static bool Installed { get { return _inner != null; } }
        public static double UiUpdateAvgMs;   // last full second
        public static double UiUpdateWorstMs; // worst single frame in the last second

        internal static void Install()
        {
            if (_tried) return;
            _tried = true;
            try
            {
                FieldInfo evField = typeof(Canvas).GetField("willRenderCanvases", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                MethodInfo perform = typeof(CanvasUpdateRegistry).GetMethod("PerformUpdate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (object.ReferenceEquals(evField, null) || object.ReferenceEquals(perform, null)) { Log.Write("KitStats: hooks not found - UI timing off."); return; }
                CanvasUpdateRegistry registry = CanvasUpdateRegistry.instance; // makes sure it has subscribed
                Canvas.WillRenderCanvases original = (Canvas.WillRenderCanvases)Delegate.CreateDelegate(typeof(Canvas.WillRenderCanvases), registry, perform);

                if (!Contains(evField, perform)) { Log.Write("KitStats: PerformUpdate not subscribed - UI timing off."); return; }
                Canvas.willRenderCanvases -= original;
                if (Contains(evField, perform))
                {
                    Log.Write("KitStats: could not unhook PerformUpdate - UI timing off.");
                    return;
                }
                _inner = original;
                Canvas.willRenderCanvases += Timed;
                _windowStart = Time.unscaledTime;
                Log.Write("KitStats: UI update timing installed.");
            }
            catch (Exception ex)
            {
                Log.Write("KitStats: install failed: " + ex.Message);
            }
        }

        private static bool Contains(FieldInfo evField, MethodInfo method)
        {
            Delegate d = evField.GetValue(null) as Delegate;
            if (d == null) return false;
            Delegate[] list = d.GetInvocationList();
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i].Method == method) return true;
            }
            return false;
        }

        private static void Timed()
        {
            long t0 = Stopwatch.GetTimestamp();
            _inner();
            long dt = Stopwatch.GetTimestamp() - t0;
            _windowTicks += dt;
            if (dt > _windowWorst) _windowWorst = dt;
            _windowFrames++;
            float now = Time.unscaledTime;
            if (now - _windowStart >= 1f && _windowFrames > 0)
            {
                double toMs = 1000.0 / Stopwatch.Frequency;
                UiUpdateAvgMs = _windowTicks * toMs / _windowFrames;
                UiUpdateWorstMs = _windowWorst * toMs;
                _windowTicks = 0;
                _windowWorst = 0;
                _windowFrames = 0;
                _windowStart = now;
            }
        }

        // Active graphics on the kit canvas (drawn or raycast). Allocation-free after the first
        // call; call it at most about once a second.
        public static int KitGraphicCount()
        {
            if (Kit.WindowLayer == null) return 0;
            GraphicScratch.Clear();
            Kit.WindowLayer.root.GetComponentsInChildren(false, GraphicScratch);
            int n = GraphicScratch.Count;
            GraphicScratch.Clear();
            return n;
        }
    }

    // Raycast-only graphic: receives pointer events for its rect but generates no geometry, so
    // it costs nothing to batch, draw or clip. Used for every invisible hit area in the kit.
    public class KitRaycastTarget : Graphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
        }

        public override void SetMaterialDirty() { }
        public override void SetVerticesDirty() { }
    }
}
