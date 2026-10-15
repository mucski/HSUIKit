# HSUIKit

A small UI library for Honey Select (Unity 5.3.5) plugins: dark, rounded windows and controls built from code with
UGUI - windows (movable, resizable, remembered), buttons, switches, sliders, number fields you can drag, dropdowns
with search, color picker, context menus, tooltips, toasts, modal dialogs, virtualized lists, tabs, foldouts, progress
bars, forms (`KitForm`), a window tray that cooperates with NeoUIManager (`KitTray`) and a small tween engine.
The look is controlled by an optional `Plugins\HSUIKit\theme.txt`.

Used by:
- [HSMaterialEditor](https://github.com/mucski/HSMaterialEditor)
- [HSPE](https://github.com/mucski/HSPE)
- [HSTimeline](https://github.com/mucski/HSTimeline)
- [HSLRE](https://github.com/mucski/HSLRE)

## Install

Players get `HSUIKit.dll` inside the zip of a plugin that needs it; it goes into the game's `Plugins` folder. When
several plugins ship it, keep the newest: right-click the file > Properties > Details > File version (a file with no
version there is older than 1.0.0).

## Versions

The version is in `AssemblyInfo.cs` and readable at runtime as `HSUIKit.KitVersion.Current`. Changes only ever add
things (new controls, new members, new overloads), so a plugin built against an older HSUIKit keeps working with a
newer one. The assembly version itself is left unset on purpose, so older plugins keep loading.

## Building

Visual Studio 2019+ / MSBuild, .NET Framework 3.5 target, only UnityEngine and UnityEngine.UI are referenced:

```
msbuild HSUIKit.csproj /p:Configuration=Release /p:HSDir="C:\path\to\HoneySelect"
```

`HSDir` must point to a Honey Select install (references come from `StudioNEO_64_Data\Managed`). The plugins that use
the kit expect this repository cloned next to theirs (`..\HSUIKit\bin\Release\HSUIKit.dll`).

## License

GNU General Public License v3.0 - see `LICENSE`.
