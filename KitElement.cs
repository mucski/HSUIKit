using System;
using System.Collections.Generic;
using UnityEngine;

namespace HSUIKit
{
    // Base for every themed control. Builders set everything up, then set Built = true and call
    // ApplyThemeNow() - OnEnable fires during AddComponent, before any fields exist, so it must
    // not style anything until the builder says so. Inactive elements skip live restyles and
    // catch up on their next OnEnable via the version check.
    public abstract class KitElement : MonoBehaviour
    {
        private static readonly List<KitElement> Live = new List<KitElement>();

        protected bool Built;
        private int _appliedVersion = -1;

        protected virtual void OnEnable()
        {
            Live.Add(this);
            if (Built && _appliedVersion != Theme.Version) ApplyThemeNow();
        }

        protected virtual void OnDisable()
        {
            Live.Remove(this);
        }

        public void ApplyThemeNow()
        {
            _appliedVersion = Theme.Version;
            try
            {
                ApplyTheme();
            }
            catch (Exception ex)
            {
                Log.Write(GetType().Name + ".ApplyTheme threw: " + ex);
            }
        }

        protected abstract void ApplyTheme();

        internal static void ApplyAll()
        {
            KitElement[] copy = Live.ToArray();
            for (int i = 0; i < copy.Length; i++)
            {
                if (copy[i] != null && copy[i].Built) copy[i].ApplyThemeNow();
            }
        }

        protected void Finish()
        {
            Built = true;
            ApplyThemeNow();
        }

        protected static void Invoke(VoidHandler h)
        {
            if (h == null) return;
            try { h(); }
            catch (Exception ex) { Log.Write("UI callback threw: " + ex); }
        }
    }
}
