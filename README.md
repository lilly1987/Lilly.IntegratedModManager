# Lilly Integrated Mod Manager

RimWorld 1.7 mod replacing the vanilla Mods menu page with a category-based manager.

## Current features
- Four categories: Pre/Core/Normal/After-load.
- Category activation state and per-mod activation state.
- Ctrl-click, Shift-click, and Ctrl+A multi-selection.
- Move selected mods between categories.
- Category-specific folders with create, rename, collapse, move, and delete operations.
- Protected Core and official DLC activation.
- Apply changes to the vanilla mod configuration; cancel unapplied changes.
- Persistent category/folder assignments, collapsed state, and activation choices.

Newly discovered mods are added to the Normal category. Core and official DLC are protected from moving or disabling. Changes apply on the next game restart.

Tags, presets, dependency-aware sorting, search, drag-and-drop ordering, and subscription management are not implemented yet.

## Build

Run `build.bat` (or `build.bat /nopause`) with .NET SDK 10 installed. Since `LoadFolders.xml` loads the mod root for RimWorld 1.7, the output is written to `Assemblies\Lilly.IntegratedModManager.dll`.
