using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HSUIKit
{
    // Dev tool for checking every plugin's UI at once. When UserData/HSUIKit/inspect.txt exists (a comma list of
    // theme presets, e.g. "candy-light,glass-dark"), it waits for the plugins to build their windows, then opens
    // every kit window in turn, steps through the pages of its top-level tabs, runs KitLayoutAudit on each and
    // saves a screenshot to UserData/HSUIKit/inspect/. Ends with a per-window summary line in the log.
    // Changes theme.txt (presets are applied for real) - back it up first.
    internal sealed class KitInspect : MonoBehaviour
    {
        private const float StartDelay = 30f;   // plugins build their windows when the Studio scene starts

        public static void InstallIfRequested(GameObject host)
        {
            string file = Path.Combine(Path.Combine(Directory.GetCurrentDirectory(), "UserData"), "HSUIKit/inspect.txt");
            if (!File.Exists(file)) return;
            KitInspect k = host.AddComponent<KitInspect>();
            // "presets;;key=value;;..." - keys: dump=Title|path, prep=female, click=UI path, call=Type.Method
            string[] parts = File.ReadAllText(file).Trim().Split(new[] { ";;" }, System.StringSplitOptions.None);
            k._presets = parts[0].Split(',');
            for (int i = 1; i < parts.Length; i++)
            {
                int eq = parts[i].IndexOf('=');
                if (eq < 0) continue;
                string key = parts[i].Substring(0, eq).Trim(), val = parts[i].Substring(eq + 1).Trim();
                if (key == "dump") k._dump = val;
                else if (key == "colors") k._colorsFile = val;
                else k._steps.Add(new KeyValuePair<string, string>(key, val));
            }
            Log.Write("inspect: requested for presets " + File.ReadAllText(file).Trim());
        }

        private string[] _presets;
        private string _dump;
        private string _colorsFile;   // colors=<file>: every graphic's colour per window, to diff a fresh vs switched theme
        private readonly System.Text.StringBuilder _colors = new System.Text.StringBuilder();
        private readonly List<KeyValuePair<string, string>> _steps = new List<KeyValuePair<string, string>>();   // "Window title|path part": rect dump of matching objects (see KitLayoutAudit.DumpRects)

        private IEnumerator Start()
        {
            while (Time.realtimeSinceStartup < StartDelay) yield return null;
            string dir = Path.Combine(Path.Combine(Directory.GetCurrentDirectory(), "UserData"), "HSUIKit/inspect");
            Directory.CreateDirectory(dir);

            var wasShown = new Dictionary<KitWindow, bool>();
            foreach (KitWindow w in Kit.WindowLayer.GetComponentsInChildren<KitWindow>(true)) wasShown[w] = w.Visible;
            foreach (KeyValuePair<string, string> st in _steps)
            {
                try { RunStep(st.Key, st.Value); }
                catch (System.Exception ex) { Log.Write("inspect: step " + st.Key + "=" + st.Value + " threw " + ex.GetBaseException().Message); }
                yield return new WaitForSeconds(st.Key == "prep" ? 4f : 1f);
            }
            KitTray.OpenAllForInspection();   // windows created on first open
            yield return new WaitForSeconds(1f);
            KitWindow[] windows = Kit.WindowLayer.GetComponentsInChildren<KitWindow>(true);
            Log.Write("inspect: " + windows.Length + " kit windows");
            var summary = new List<string>();

            foreach (string raw in _presets)
            {
                string preset = raw.Trim();
                if (System.Array.IndexOf(Theme.Presets, preset) >= 0) Theme.SetPreset(preset);
                yield return null;
                for (int i = 0; i < windows.Length; i++)
                {
                    KitWindow w = windows[i];
                    if (w == null) continue;
                    foreach (KitWindow other in windows) if (other != null && other != w && other.Visible) other.Hide();
                    if (!w.Visible) w.Show();
                    if (w.Collapsed) w.SetCollapsed(false, false);
                    yield return new WaitForSeconds(0.6f);

                    string title = Safe(w.Title);
                    KitTabs tabs = TopTabs(w);
                    int issues = 0, pages = tabs != null ? tabs.Pages.Length : 1;
                    for (int p = 0; p < pages; p++)
                    {
                        string page = "";
                        if (tabs != null)
                        {
                            tabs.Select(p, false);
                            page = Safe(tabs.Pages[p].name.Replace("Page ", ""));
                            yield return new WaitForSeconds(0.35f);
                        }
                        string label = title + (page.Length > 0 ? " / " + page : "");
                        issues += KitLayoutAudit.Run(w.Content, label);
                        if (_dump != null && _dump.Split('|')[0] == w.Title) KitLayoutAudit.DumpRects(w.Content, _dump.Split('|')[1]);
                        if (_colorsFile != null) DumpColors(w, label);
                        string shot = Path.Combine(dir, preset + "_" + i.ToString("00") + "_" + title + (page.Length > 0 ? "_" + page : "") + ".png");
                        Application.CaptureScreenshot(shot);
                        Vector3[] c = new Vector3[4];
                        w.Rect.GetWorldCorners(c);
                        Log.Write("inspect-rect: " + Path.GetFileName(shot) + " " + Mathf.RoundToInt(c[0].x) + " " + Mathf.RoundToInt(Screen.height - c[1].y) + " " + Mathf.RoundToInt(c[2].x - c[0].x) + " " + Mathf.RoundToInt(c[1].y - c[0].y));
                        yield return null;
                        yield return null;
                    }
                    // Dead space: body taller than what its layout wants (only meaningful when Content is laid out
                    // by a group; scroll views report their content separately so they're skipped).
                    string slack = "";
                    UnityEngine.UI.LayoutGroup lg = w.Content.GetComponent<UnityEngine.UI.LayoutGroup>();
                    if (lg != null)
                    {
                        // Real gap: from the lowest visible child down to the content's bottom padding.
                        Vector3[] cc = new Vector3[4];
                        w.Content.GetWorldCorners(cc);
                        float bottom = cc[0].y, lowest = float.MaxValue;
                        for (int ci = 0; ci < w.Content.childCount; ci++)
                        {
                            RectTransform ch = w.Content.GetChild(ci) as RectTransform;
                            if (ch == null || !ch.gameObject.activeSelf) continue;
                            UnityEngine.UI.ILayoutIgnorer ig = ch.GetComponent(typeof(UnityEngine.UI.ILayoutIgnorer)) as UnityEngine.UI.ILayoutIgnorer;
                            if (ig != null && ig.ignoreLayout) continue;
                            ch.GetWorldCorners(cc);
                            lowest = Mathf.Min(lowest, cc[0].y);
                        }
                        float spare = lowest - bottom - lg.padding.bottom;
                        if (lowest < float.MaxValue && spare > 24f) slack = " | " + Mathf.RoundToInt(spare) + " px empty below the content";
                    }
                    summary.Add(preset + " | " + title + " | " + pages + " page(s) | " + issues + " issue(s)" + slack);
                }
            }

            foreach (KitWindow w in windows)
            {
                if (w == null) continue;
                bool shown;
                if (wasShown.TryGetValue(w, out shown) && !shown && w.Visible) w.Hide();
            }
            if (_colorsFile != null) File.WriteAllText(Path.Combine(dir, _colorsFile), _colors.ToString());
            foreach (string s in summary) Log.Write("inspect-summary: " + s);
            Log.Write("inspect: done");
        }

        // One line per drawn graphic: window label, path, colour. Paths are made unique per window by index.
        private void DumpColors(KitWindow w, string label)
        {
            var seen = new Dictionary<string, int>();
            foreach (UnityEngine.UI.Graphic g in w.GetComponentsInChildren<UnityEngine.UI.Graphic>(false))
            {
                if (!g.enabled) continue;
                string path = g.name;
                for (Transform t = g.transform.parent; t != null && t != w.transform; t = t.parent) path = t.name + "/" + path;
                int n;
                seen.TryGetValue(path, out n);
                seen[path] = n + 1;
                _colors.Append(label).Append('\t').Append(path).Append('#').Append(n).Append('\t').Append(Theme.ToHex(g.color)).Append('\n');
            }
        }

        // Test-run setup steps (reflection only: the kit doesn't reference the game's assemblies).
        private static void RunStep(string key, string val)
        {
            if (key == "prep" && val == "female")
            {
                string dir = Path.Combine(Path.Combine(Directory.GetCurrentDirectory(), "UserData"), Path.Combine("chara", "female"));
                string[] cards = Directory.Exists(dir) ? Directory.GetFiles(dir, "*.png") : new string[0];
                if (cards.Length == 0) { Log.Write("inspect: prep female - no cards"); return; }
                System.Array.Sort(cards);
                System.Type studio = FindType("Studio.Studio");
                object inst = studio.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy).GetValue(null, null);
                studio.GetMethod("AddFemale", new[] { typeof(string) }).Invoke(inst, new object[] { cards[0] });
                Log.Write("inspect: prep added female " + Path.GetFileName(cards[0]));
            }
            else if (key == "click")
            {
                int slash = val.IndexOf('/');
                GameObject root = GameObject.Find(slash < 0 ? val : val.Substring(0, slash));
                Transform t = root != null && slash >= 0 ? root.transform.Find(val.Substring(slash + 1)) : (root != null ? root.transform : null);
                UnityEngine.UI.Button b = t != null ? t.GetComponent<UnityEngine.UI.Button>() : null;
                if (b == null)
                {
                    // Not at that path: the last path segment as a button name anywhere (plugins move their buttons).
                    string name = val.Substring(val.LastIndexOf('/') + 1);
                    foreach (UnityEngine.UI.Button any in Object.FindObjectsOfType<UnityEngine.UI.Button>())
                        if (any.name == name) { b = any; break; }
                }
                if (b == null) { Log.Write("inspect: click - no button at " + val); return; }
                b.onClick.Invoke();
                Log.Write("inspect: clicked " + val);
            }
            else if (key == "call")
            {
                // Type.Method (static) or Type.Instance.Method (singleton instance method).
                int dot = val.LastIndexOf('.');
                string typeName = val.Substring(0, dot), method = val.Substring(dot + 1);
                object target = null;
                if (typeName.EndsWith(".Instance"))
                {
                    typeName = typeName.Substring(0, typeName.Length - 9);
                    System.Type owner = FindType(typeName);
                    const System.Reflection.BindingFlags sf = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy;
                    System.Reflection.PropertyInfo pi = owner.GetProperty("Instance", sf);
                    System.Reflection.FieldInfo fi = owner.GetField("Instance", sf);
                    target = pi != null ? pi.GetValue(null, null) : fi.GetValue(null);
                }
                System.Type type = FindType(typeName);
                System.Reflection.MethodInfo m = type.GetMethod(method, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | (target != null ? System.Reflection.BindingFlags.Instance : System.Reflection.BindingFlags.Static), null, System.Type.EmptyTypes, null);
                m.Invoke(target, null);
                Log.Write("inspect: called " + val);
            }
        }

        private static System.Type FindType(string fullName)
        {
            foreach (System.Reflection.Assembly a in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                System.Type t = a.GetType(fullName, false);
                if (t != null) return t;
            }
            throw new System.Exception("type not found: " + fullName);
        }

        // The outermost KitTabs in the window (nested tab sets are reached through their page).
        private static KitTabs TopTabs(KitWindow w)
        {
            KitTabs best = null;
            int bestDepth = int.MaxValue;
            foreach (KitTabs t in w.GetComponentsInChildren<KitTabs>(true))
            {
                int d = 0;
                for (Transform x = t.transform; x != null && x != w.transform; x = x.parent) d++;
                if (d < bestDepth) { best = t; bestDepth = d; }
            }
            return best;
        }

        private static string Safe(string s)
        {
            if (string.IsNullOrEmpty(s)) return "untitled";
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace(' ', '_');
        }
    }
}
