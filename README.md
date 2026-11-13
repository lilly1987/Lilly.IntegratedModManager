# Lilly Integrated Mod Manager

RimWorld 1.7 mod replacing the vanilla Mods menu page with a category- and tag-based manager.

## Current features
- Four categories: Pre/Core/Normal/After-load.
- Category activation state and per-mod activation state.
- Ctrl-click, Shift-click, and Ctrl+A multi-selection.
- Move selected mods between categories.
- Category-specific folders with create, rename, collapse, move, and delete operations.
- User-defined tags that can be assigned to multiple mods, with tag-specific folders.
- Toggle activation for all eligible mods in a tag, and add or remove tags from selected mods.
- Save, load, and delete named presets containing categories, folders, tags, and activation choices.
- Search the current category or tag by mod name, PackageId, or author.
- Protected Core and official DLC activation.
- Apply changes to the vanilla mod configuration; cancel unapplied changes.
- Persistent category/tag/folder assignments, collapsed state, and activation choices.
- Tag definitions, tag folders, and tag assignments are saved immediately and remain when unapplied manager changes are canceled.

Newly discovered mods are added to the Normal category. Core and official DLC are protected from moving or disabling. Changes apply on the next game restart.

Loading a preset replaces the current edit draft; it does not change the active mod configuration until Apply is selected. Installed mods missing from an older preset are retained in a default category.

Dependency-aware sorting follows the active RimWorld load order, and drag-and-drop reorder now works for multi-selected mod groups within the current folder. Subscription management remains future work.

## Build

Run `build.bat` (or `build.bat /nopause`) with .NET SDK 10 installed. Since `LoadFolders.xml` loads the mod root for RimWorld 1.7, the output is written to `Assemblies\Lilly.IntegratedModManager.dll`.
