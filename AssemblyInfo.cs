using System.Reflection;

// The file version is what shows in the DLL's Properties > Details, so players can tell which HSUIKit.dll is newer.
// The assembly version itself stays unset on purpose: plugins built against any earlier HSUIKit.dll keep loading.
// Kit changes must only add things (new types, new members, new overloads) so older plugins keep working with a
// newer kit; bump the version whenever something is added.
[assembly: AssemblyTitle("HSUIKit")]
[assembly: AssemblyProduct("HSUIKit")]
[assembly: AssemblyFileVersion(HSUIKit.KitVersion.Text + ".0")]
[assembly: AssemblyInformationalVersion(HSUIKit.KitVersion.Text)]

namespace HSUIKit
{
    public static class KitVersion
    {
        internal const string Text = "1.3.0";

        /// <summary>The installed kit's version, read at runtime (a plugin can compare it with the one it needs).</summary>
        public static readonly System.Version Current = new System.Version(Text);
    }
}
