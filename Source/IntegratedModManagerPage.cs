using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Lilly.IntegratedModManager
{
    internal sealed class IntegratedModManagerPage : Page
    {
        private const float RowHeight = 30f;
        private const float BottomHeight = 42f;

        private readonly IntegratedModManagerSettings settings;
        private readonly List<ManagerCategoryData> categories;
        private readonly List<ManagerModData> modStates;
        private readonly HashSet<string> selectedPackageIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ModMetaData> modsByPackageId =
            new Dictionary<string, ModMetaData>(StringComparer.OrdinalIgnoreCase);
        private readonly List<ModMetaData> visibleMods = new List<ModMetaData>();

        private string currentCategoryId;
        private string primarySelectedPackageId;
        private int lastSelectedIndex = -1;
        private string currentFolderId;
        private Vector2 modListScroll;
        private Vector2 descriptionScroll;

        public override Vector2 InitialSize
        {
            get { return new Vector2(UI.screenWidth, UI.screenHeight); }
        }

        protected override float Margin
        {
            get { return 0f; }
        }

        public IntegratedModManagerPage()
        {
            doWindowBackground = false;
            doCloseX = false;
            absorbInputAroundWindow = true;

            if (IntegratedModManagerMod.Instance == null)
                throw new InvalidOperationException("Integrated Mod Manager did not initialize.");

            settings = IntegratedModManagerMod.Instance.Settings;
            settings.EnsureInitialized();
            categories = settings.Categories.Select(category => category.Copy()).ToList();
            modStates = settings.Mods.Select(mod => mod.Copy()).ToList();

            foreach (ModMetaData mod in ModLister.AllInstalledMods)
            {
                if (mod != null && !mod.PackageId.NullOrEmpty())
                    modsByPackageId[mod.PackageId] = mod;
            }

            currentCategoryId = categories[0].Id;
            currentFolderId = categories[0].Folders[0].Id;
            RefreshVisibleMods();
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(20f, 10f, inRect.width - 40f, 36f),
                "Lilly - Integrated Mod Manager");
            Text.Font = GameFont.Small;

            DrawCategoryTabs(new Rect(20f, 48f, inRect.width - 40f, 44f));

            float contentTop = 100f;
            float contentBottom = inRect.height - BottomHeight - 20f;
            float contentHeight = Mathf.Max(100f, contentBottom - contentTop);
            float leftWidth = inRect.width * 0.66f;
            Rect listRect = new Rect(20f, contentTop, leftWidth - 30f, contentHeight);
            Rect detailsRect = new Rect(leftWidth, contentTop, inRect.width - leftWidth - 20f,
                contentHeight);

            DrawModList(listRect);
            DrawDetails(detailsRect);
            DrawBottomButtons(new Rect(20f, inRect.height - BottomHeight - 8f,
                inRect.width - 40f, BottomHeight));
            HandleSelectAll();
        }

        private void DrawCategoryTabs(Rect rect)
        {
            float tabWidth = Mathf.Max(150f, rect.width / categories.Count);
            for (int i = 0; i < categories.Count; i++)
            {
                ManagerCategoryData category = categories[i];
                Rect tabRect = new Rect(rect.x + i * tabWidth, rect.y,
                    Mathf.Min(tabWidth - 6f, rect.xMax - (rect.x + i * tabWidth)), rect.height);
                if (category.Id == currentCategoryId)
                    Widgets.DrawHighlight(tabRect);

                Rect enabledRect = new Rect(tabRect.x + 4f, tabRect.y + 8f, 24f, 24f);
                bool enabled = category.Enabled;
                Widgets.Checkbox(enabledRect.position, ref enabled);
                if (enabled != category.Enabled)
                    category.Enabled = enabled;

                Rect labelRect = new Rect(enabledRect.xMax + 4f, tabRect.y,
                    tabRect.width - enabledRect.width - 8f, tabRect.height);
                string label = category.Name + " (" + GetInstalledCount(category) + ")";
                if (Widgets.ButtonText(labelRect, label))
                {
                    currentCategoryId = category.Id;
                    currentFolderId = category.Folders.Count > 0
                        ? category.Folders[0].Id
                        : null;
                    selectedPackageIds.Clear();
                    primarySelectedPackageId = null;
                    lastSelectedIndex = -1;
                    RefreshVisibleMods();
                }
            }
        }

        private void DrawModList(Rect rect)
        {
            ManagerCategoryData category = FindCurrentCategory();
            if (category == null)
                return;

            Widgets.DrawBox(rect);
            Rect innerRect = rect.ContractedBy(8f);
            float viewHeight = Mathf.Max(innerRect.height, GetFolderListHeight(category));
            Rect viewRect = new Rect(0f, 0f, innerRect.width - 16f, viewHeight);
            Widgets.BeginScrollView(innerRect, ref modListScroll, viewRect);

            float y = 0f;
            int visibleIndex = 0;
            for (int folderIndex = 0; folderIndex < category.Folders.Count; folderIndex++)
            {
                ManagerFolderData folder = category.Folders[folderIndex];
                Rect headerRect = new Rect(0f, y, viewRect.width, RowHeight);
                Widgets.DrawLightHighlight(headerRect);
                string foldLabel = folder.Collapsed ? "▶" : "▼";
                if (Widgets.ButtonText(new Rect(headerRect.x + 2f, y + 2f, 28f, 26f), foldLabel))
                {
                    folder.Collapsed = !folder.Collapsed;
                    RefreshVisibleMods();
                }

                Rect titleRect = new Rect(headerRect.x + 32f, y + 2f,
                    Mathf.Max(30f, headerRect.width - 225f), 26f);
                if (folder.Id == currentFolderId)
                    Widgets.DrawHighlight(titleRect);
                if (Widgets.ButtonText(titleRect,
                    folder.Name + " (" + GetInstalledCount(folder) + ")"))
                    currentFolderId = folder.Id;

                float actionX = headerRect.xMax - 186f;
                if (Widgets.ButtonText(new Rect(actionX, y + 2f, 42f, 26f), "+"))
                    OpenCreateFolderDialog(category, folder);
                if (Widgets.ButtonText(new Rect(actionX + 46f, y + 2f, 62f, 26f), "이름"))
                    OpenRenameFolderDialog(category, folder);
                bool isDefaultFolder = folder.Id == "uncategorized";
                GUI.enabled = !isDefaultFolder;
                if (Widgets.ButtonText(new Rect(actionX + 112f, y + 2f, 62f, 26f), "삭제"))
                    ConfirmDeleteFolder(category, folder);
                GUI.enabled = true;
                y += RowHeight;

                if (folder.Collapsed)
                    continue;

                for (int folderModIndex = 0; folderModIndex < folder.ModIds.Count; folderModIndex++)
                {
                    ModMetaData mod = FindMod(folder.ModIds[folderModIndex]);
                    if (mod == null)
                        continue;

                    Rect rowRect = new Rect(0f, y, viewRect.width, RowHeight);
                if (selectedPackageIds.Contains(mod.PackageId))
                    Widgets.DrawHighlight(rowRect);
                else
                    Widgets.DrawHighlightIfMouseover(rowRect);

                Rect checkRect = new Rect(rowRect.x + 3f, rowRect.y + 3f, 24f, 24f);
                ManagerModData state = FindModState(mod.PackageId);
                bool checkedValue = state != null && state.Enabled;
                bool protectedMod = IsProtected(mod);
                GUI.enabled = !protectedMod;
                Widgets.Checkbox(checkRect.position, ref checkedValue, checkRect.width, protectedMod);
                GUI.enabled = true;
                if (!protectedMod && state != null && checkedValue != state.Enabled)
                    state.Enabled = checkedValue;

                string name = mod.Name.NullOrEmpty() ? mod.FolderName : mod.Name;
                if (protectedMod)
                    name = name + " (필수)";
                Rect nameRect = new Rect(checkRect.xMax + 5f, rowRect.y,
                    rowRect.width - checkRect.width - 12f, rowRect.height);
                bool control = Event.current.control;
                bool shift = Event.current.shift;
                if (Widgets.ButtonInvisible(nameRect))
                    SelectMod(visibleIndex, control, shift);
                Widgets.Label(nameRect, name);

                    y += RowHeight;
                    visibleIndex++;
                }
            }

            Widgets.EndScrollView();
        }

        private void DrawDetails(Rect rect)
        {
            Widgets.DrawBox(rect);
            Rect inner = rect.ContractedBy(12f);
            ModMetaData mod = FindMod(primarySelectedPackageId);
            if (mod == null)
            {
                Widgets.Label(inner, "모드를 선택하면 상세 정보가 표시됩니다.");
                return;
            }

            float y = inner.y;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inner.x, y, inner.width, 36f), mod.Name);
            Text.Font = GameFont.Small;
            y += 38f;

            Widgets.Label(new Rect(inner.x, y, inner.width, 24f),
                "PackageId: " + mod.PackageIdPlayerFacing);
            y += 28f;
            Widgets.Label(new Rect(inner.x, y, inner.width, 24f),
                "제작자: " + mod.AuthorsString);
            y += 32f;

            Rect descriptionRect = new Rect(inner.x, y, inner.width,
                Mathf.Max(80f, inner.height - (y - inner.y) - 4f));
            string description = mod.Description.NullOrEmpty()
                ? "설명이 없습니다."
                : mod.Description;
            Widgets.LabelScrollable(descriptionRect, description, ref descriptionScroll);
        }

        private void DrawBottomButtons(Rect rect)
        {
            float buttonWidth = 142f;
            float x = rect.x;
            if (Widgets.ButtonText(new Rect(x, rect.y, buttonWidth, rect.height), "전체 선택 (Ctrl+A)"))
                SelectAllVisible();
            x += buttonWidth + 8f;

            if (Widgets.ButtonText(new Rect(x, rect.y, buttonWidth, rect.height), "선택 활성화"))
                SetSelectedEnabled(true);
            x += buttonWidth + 8f;

            if (Widgets.ButtonText(new Rect(x, rect.y, buttonWidth, rect.height), "선택 비활성화"))
                SetSelectedEnabled(false);
            x += buttonWidth + 8f;

            if (Widgets.ButtonText(new Rect(x, rect.y, buttonWidth, rect.height), "선택 모드 이동"))
                OpenMoveMenu();

            float rightX = rect.xMax - buttonWidth * 2f - 8f;
            if (Widgets.ButtonText(new Rect(rightX, rect.y, buttonWidth, rect.height), "취소"))
                Close();
            if (Widgets.ButtonText(new Rect(rightX + buttonWidth + 8f, rect.y,
                buttonWidth, rect.height), "적용"))
                ApplyChanges();
        }

        private void HandleSelectAll()
        {
            Event current = Event.current;
            if (current.type != EventType.KeyDown || current.keyCode != KeyCode.A ||
                !current.control)
                return;

            SelectAllVisible();
            current.Use();
        }

        private void SelectAllVisible()
        {
            selectedPackageIds.Clear();
            for (int i = 0; i < visibleMods.Count; i++)
                selectedPackageIds.Add(visibleMods[i].PackageId);
            if (visibleMods.Count > 0)
            {
                primarySelectedPackageId = visibleMods[0].PackageId;
                lastSelectedIndex = 0;
            }
        }

        private void SelectMod(int index, bool control, bool shift)
        {
            if (index < 0 || index >= visibleMods.Count)
                return;

            string packageId = visibleMods[index].PackageId;
            if (shift && lastSelectedIndex >= 0)
            {
                if (!control)
                    selectedPackageIds.Clear();
                int start = Mathf.Min(lastSelectedIndex, index);
                int end = Mathf.Max(lastSelectedIndex, index);
                for (int i = start; i <= end; i++)
                    selectedPackageIds.Add(visibleMods[i].PackageId);
            }
            else if (control)
            {
                if (!selectedPackageIds.Add(packageId))
                    selectedPackageIds.Remove(packageId);
                lastSelectedIndex = index;
            }
            else
            {
                selectedPackageIds.Clear();
                selectedPackageIds.Add(packageId);
                lastSelectedIndex = index;
            }

            primarySelectedPackageId = packageId;
        }

        private void SetSelectedEnabled(bool enabled)
        {
            for (int i = 0; i < selectedPackageIds.Count; i++)
            {
                string packageId = selectedPackageIds.ElementAt(i);
                ModMetaData mod = FindMod(packageId);
                ManagerModData state = FindModState(packageId);
                if (mod != null && state != null && !IsProtected(mod))
                    state.Enabled = enabled;
            }
        }

        private void OpenMoveMenu()
        {
            if (selectedPackageIds.Count == 0)
                return;

            List<FloatMenuOption> options = new List<FloatMenuOption>();
            for (int i = 0; i < categories.Count; i++)
            {
                ManagerCategoryData category = categories[i];
                ManagerCategoryData target = category;
                if (target.Id != currentCategoryId)
                {
                    options.Add(new FloatMenuOption(target.Name + " / 미분류",
                        () => MoveSelectionTo(target)));
                    continue;
                }

                for (int folderIndex = 0; folderIndex < target.Folders.Count; folderIndex++)
                {
                    ManagerFolderData folder = target.Folders[folderIndex];
                    if (folder.Id == currentFolderId)
                        continue;

                    ManagerFolderData targetFolder = folder;
                    options.Add(new FloatMenuOption(
                        target.Name + " / " + targetFolder.Name,
                        () => MoveSelectionToFolder(target, targetFolder)));
                }
            }
            if (options.Count > 0)
                Find.WindowStack.Add(new FloatMenu(options));
        }

        private void MoveSelectionTo(ManagerCategoryData target)
        {
            if (target == null)
                return;

            List<string> movingIds = selectedPackageIds.ToList();
            ManagerFolderData targetFolder = FindFolder(target, "uncategorized");
            if (targetFolder == null)
            {
                targetFolder = new ManagerFolderData("uncategorized", "미분류");
                target.Folders.Insert(0, targetFolder);
            }
            for (int i = 0; i < movingIds.Count; i++)
            {
                string packageId = movingIds[i];
                ModMetaData mod = FindMod(packageId);
                if (mod == null || IsProtected(mod))
                    continue;

                for (int j = 0; j < categories.Count; j++)
                {
                    categories[j].ModIds.RemoveAll(id =>
                        string.Equals(id, packageId, StringComparison.OrdinalIgnoreCase));
                    for (int folderIndex = 0; folderIndex < categories[j].Folders.Count; folderIndex++)
                        categories[j].Folders[folderIndex].ModIds.RemoveAll(id =>
                            string.Equals(id, packageId, StringComparison.OrdinalIgnoreCase));
                }
                target.ModIds.Add(packageId);
                targetFolder.ModIds.Add(packageId);
            }

            selectedPackageIds.Clear();
            primarySelectedPackageId = null;
            RefreshVisibleMods();
        }

        private void MoveSelectionToFolder(
            ManagerCategoryData category,
            ManagerFolderData folder)
        {
            if (category == null || folder == null)
                return;

            folder.Collapsed = false;
            List<string> movingIds = selectedPackageIds.ToList();
            for (int i = 0; i < movingIds.Count; i++)
            {
                string packageId = movingIds[i];
                ModMetaData mod = FindMod(packageId);
                if (mod == null || IsProtected(mod))
                    continue;

                for (int folderIndex = 0; folderIndex < category.Folders.Count; folderIndex++)
                    category.Folders[folderIndex].ModIds.RemoveAll(id =>
                        string.Equals(id, packageId, StringComparison.OrdinalIgnoreCase));
                folder.ModIds.Add(packageId);
            }

            currentFolderId = folder.Id;
            selectedPackageIds.Clear();
            primarySelectedPackageId = null;
            RefreshVisibleMods();
        }

        private void OpenCreateFolderDialog(
            ManagerCategoryData category,
            ManagerFolderData insertAfter)
        {
            Find.WindowStack.Add(new ManagerFolderNameDialog(
                "새 폴더",
                "",
                name =>
                {
                    if (HasFolderName(category, name, null))
                    {
                        Messages.Message("같은 이름의 폴더가 이미 있습니다.",
                            MessageTypeDefOf.RejectInput);
                        return false;
                    }

                    ManagerFolderData folder = new ManagerFolderData(
                        Guid.NewGuid().ToString("N"), name.Trim());
                    int index = category.Folders.IndexOf(insertAfter);
                    category.Folders.Insert(index < 0 ? category.Folders.Count : index + 1,
                        folder);
                    currentFolderId = folder.Id;
                    return true;
                }));
        }

        private void OpenRenameFolderDialog(
            ManagerCategoryData category,
            ManagerFolderData folder)
        {
            Find.WindowStack.Add(new ManagerFolderNameDialog(
                "폴더 이름 변경",
                folder.Name,
                name =>
                {
                    if (HasFolderName(category, name, folder))
                    {
                        Messages.Message("같은 이름의 폴더가 이미 있습니다.",
                            MessageTypeDefOf.RejectInput);
                        return false;
                    }

                    folder.Name = name.Trim();
                    return true;
                }));
        }

        private void ConfirmDeleteFolder(
            ManagerCategoryData category,
            ManagerFolderData folder)
        {
            if (folder == null || folder.Id == "uncategorized")
                return;

            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "폴더를 삭제할까요? 폴더 안의 모드는 미분류 폴더로 이동합니다.",
                () =>
                {
                    ManagerFolderData uncategorized = FindFolder(category, "uncategorized");
                    if (uncategorized == null)
                    {
                        uncategorized = new ManagerFolderData("uncategorized", "미분류");
                        category.Folders.Insert(0, uncategorized);
                    }

                    for (int i = 0; i < folder.ModIds.Count; i++)
                    {
                        string packageId = folder.ModIds[i];
                        if (!uncategorized.ModIds.Contains(packageId))
                            uncategorized.ModIds.Add(packageId);
                    }
                    category.Folders.Remove(folder);
                    currentFolderId = uncategorized.Id;
                    RefreshVisibleMods();
                },
                destructive: true));
        }

        private static bool HasFolderName(
            ManagerCategoryData category,
            string name,
            ManagerFolderData except)
        {
            if (name.NullOrEmpty())
                return true;
            for (int i = 0; i < category.Folders.Count; i++)
            {
                ManagerFolderData folder = category.Folders[i];
                if (folder != except && string.Equals(folder.Name, name.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private void ApplyChanges()
        {
            List<string> activePackageIds = new List<string>();
            for (int categoryIndex = 0; categoryIndex < categories.Count; categoryIndex++)
            {
                ManagerCategoryData category = categories[categoryIndex];
                for (int modIndex = 0; modIndex < category.ModIds.Count; modIndex++)
                {
                    string packageId = category.ModIds[modIndex];
                    ModMetaData mod = FindMod(packageId);
                    if (mod == null)
                        continue;

                    ManagerModData state = FindModState(packageId);
                    if (IsProtected(mod) || (category.Enabled && state != null && state.Enabled))
                        activePackageIds.Add(mod.PackageId);
                }
            }

            ModsConfig.SetActiveToList(activePackageIds);
            ModsConfig.Save();
            settings.Categories = categories.Select(category => category.Copy()).ToList();
            settings.Mods = modStates.Select(mod => mod.Copy()).ToList();
            IntegratedModManagerMod.Instance.WriteSettings();
            Close();

            if (ModsConfig.ActiveModsInLoadOrder.Any(mod =>
                ModsConfig.ModHasAnyOrderingIssues(mod)))
                Find.WindowStack.Add(new Dialog_MessageBox(
                    "모드 목록을 적용했습니다. 일부 모드의 선행/후순위 문제가 감지되었습니다. " +
                    "모드 순서를 확인한 뒤 게임을 재시작하세요."));
            else
                Find.WindowStack.Add(new Dialog_MessageBox(
                    "모드 목록을 적용했습니다. 변경사항은 게임을 재시작한 뒤 적용됩니다."));
        }

        private void RefreshVisibleMods()
        {
            visibleMods.Clear();
            ManagerCategoryData category = FindCurrentCategory();
            if (category == null)
                return;

            for (int folderIndex = 0; folderIndex < category.Folders.Count; folderIndex++)
            {
                ManagerFolderData folder = category.Folders[folderIndex];
                if (folder.Collapsed)
                    continue;
                for (int modIndex = 0; modIndex < folder.ModIds.Count; modIndex++)
                {
                    ModMetaData mod = FindMod(folder.ModIds[modIndex]);
                    if (mod != null)
                        visibleMods.Add(mod);
                }
            }
        }

        private int GetInstalledCount(ManagerCategoryData category)
        {
            int count = 0;
            for (int i = 0; i < category.ModIds.Count; i++)
            {
                if (modsByPackageId.ContainsKey(category.ModIds[i]))
                    count++;
            }
            return count;
        }

        private int GetInstalledCount(ManagerFolderData folder)
        {
            int count = 0;
            for (int i = 0; i < folder.ModIds.Count; i++)
            {
                if (modsByPackageId.ContainsKey(folder.ModIds[i]))
                    count++;
            }
            return count;
        }

        private float GetFolderListHeight(ManagerCategoryData category)
        {
            float height = 0f;
            for (int i = 0; i < category.Folders.Count; i++)
            {
                ManagerFolderData folder = category.Folders[i];
                height += RowHeight;
                if (folder.Collapsed)
                    continue;
                for (int modIndex = 0; modIndex < folder.ModIds.Count; modIndex++)
                {
                    if (FindMod(folder.ModIds[modIndex]) != null)
                        height += RowHeight;
                }
            }
            return height;
        }

        private static ManagerFolderData FindFolder(ManagerCategoryData category, string id)
        {
            if (category == null || category.Folders == null)
                return null;
            for (int i = 0; i < category.Folders.Count; i++)
            {
                if (category.Folders[i] != null &&
                    string.Equals(category.Folders[i].Id, id,
                        StringComparison.OrdinalIgnoreCase))
                    return category.Folders[i];
            }
            return null;
        }

        private ManagerCategoryData FindCurrentCategory()
        {
            for (int i = 0; i < categories.Count; i++)
            {
                if (categories[i].Id == currentCategoryId)
                    return categories[i];
            }
            return null;
        }

        private ManagerModData FindModState(string packageId)
        {
            for (int i = 0; i < modStates.Count; i++)
            {
                if (string.Equals(modStates[i].PackageId, packageId,
                    StringComparison.OrdinalIgnoreCase))
                    return modStates[i];
            }
            return null;
        }

        private ModMetaData FindMod(string packageId)
        {
            if (packageId.NullOrEmpty())
                return null;
            ModMetaData mod;
            return modsByPackageId.TryGetValue(packageId, out mod) ? mod : null;
        }

        private static bool IsProtected(ModMetaData mod)
        {
            return mod.IsCoreMod || mod.Official;
        }
    }
}
