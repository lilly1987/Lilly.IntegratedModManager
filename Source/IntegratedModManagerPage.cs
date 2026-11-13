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
        private readonly List<ManagerTagData> tags;
        private readonly List<ManagerModData> modStates;
        private readonly List<ManagerPresetData> presets;
        private readonly HashSet<string> selectedPackageIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ModMetaData> modsByPackageId =
            new Dictionary<string, ModMetaData>(StringComparer.OrdinalIgnoreCase);
        private readonly List<ModMetaData> visibleMods = new List<ModMetaData>();

        private string currentCategoryId;
        private string currentTagId;
        private string primarySelectedPackageId;
        private int lastSelectedIndex = -1;
        private string currentFolderId;
        private bool tagMode;
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
            tags = settings.Tags.Select(tag => tag.Copy()).ToList();
            modStates = settings.Mods.Select(mod => mod.Copy()).ToList();
            presets = settings.Presets.Select(preset => preset.Copy()).ToList();

            foreach (ModMetaData mod in ModLister.AllInstalledMods)
            {
                if (mod != null && !mod.PackageId.NullOrEmpty())
                    modsByPackageId[mod.PackageId] = mod;
            }

            currentCategoryId = categories[0].Id;
            currentTagId = tags.FirstOrDefault()?.Id;
            currentFolderId = categories[0].Folders[0].Id;
            RefreshVisibleMods();
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(20f, 10f, inRect.width - 470f, 36f),
                "Lilly - Integrated Mod Manager");
            Text.Font = GameFont.Small;
            if (Widgets.ButtonText(new Rect(inRect.width - 440f, 10f, 115f, 32f),
                "프리셋 저장"))
                OpenSavePresetDialog();
            if (Widgets.ButtonText(new Rect(inRect.width - 315f, 10f, 115f, 32f),
                "프리셋 불러오기"))
                OpenPresetMenu();
            if (Widgets.ButtonText(new Rect(inRect.width - 190f, 10f, 170f, 32f),
                tagMode ? "카테고리 관리" : "태그 관리"))
            {
                tagMode = !tagMode;
                selectedPackageIds.Clear();
                primarySelectedPackageId = null;
                lastSelectedIndex = -1;
                currentFolderId = tagMode
                    ? (FindCurrentTag()?.Folders.FirstOrDefault()?.Id)
                    : (FindCurrentCategory()?.Folders.FirstOrDefault()?.Id);
                RefreshVisibleMods();
            }

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
            if (tagMode)
            {
                DrawTagTabs(rect);
                return;
            }

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

        private void DrawTagTabs(Rect rect)
        {
                float addWidth = 105f;
                float tabWidth = tags.Count == 0
                    ? 0f
                    : Mathf.Max(80f, (rect.width - addWidth - 8f) / tags.Count);

                for (int i = 0; i < tags.Count; i++)
                {
                    ManagerTagData tag = tags[i];
                    Rect tabRect = new Rect(rect.x + i * tabWidth, rect.y,
                        Mathf.Min(tabWidth - 5f, rect.width - addWidth - i * tabWidth), rect.height);
                    if (tag.Id == currentTagId)
                        Widgets.DrawHighlight(tabRect);

                    Rect stateRect = new Rect(tabRect.x + 2f, tabRect.y + 7f, 30f, 30f);
                    if (Widgets.ButtonText(stateRect, GetTagStateLabel(tag)))
                        ToggleTagEnabled(tag);

                    Rect labelRect = new Rect(stateRect.xMax + 3f, tabRect.y,
                        Mathf.Max(20f, tabRect.width - 125f), tabRect.height);
                    if (Widgets.ButtonText(labelRect,
                        tag.Name + " (" + GetInstalledCount(tag) + ")"))
                        SelectTag(tag);

                    Rect renameRect = new Rect(tabRect.xMax - 84f, tabRect.y + 7f, 42f, 30f);
                    if (Widgets.ButtonText(renameRect, "이름"))
                        OpenRenameTagDialog(tag);
                    Rect deleteRect = new Rect(tabRect.xMax - 38f, tabRect.y + 7f, 34f, 30f);
                    if (Widgets.ButtonText(deleteRect, "×"))
                        ConfirmDeleteTag(tag);
                }

                Rect addRect = new Rect(rect.xMax - addWidth, rect.y, addWidth, rect.height);
                if (Widgets.ButtonText(addRect, "+ 태그 추가"))
                    OpenCreateTagDialog();
        }

        private void SelectTag(ManagerTagData tag)
        {
                if (tag == null)
                    return;

                currentTagId = tag.Id;
                currentFolderId = tag.Folders.FirstOrDefault()?.Id;
                selectedPackageIds.Clear();
                primarySelectedPackageId = null;
                lastSelectedIndex = -1;
                RefreshVisibleMods();
        }

        private void DrawModList(Rect rect)
        {
            if (tagMode)
            {
                DrawTagModList(rect);
                return;
            }

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

                    DrawModRow(mod, visibleIndex, viewRect.width, y);

                    y += RowHeight;
                    visibleIndex++;
                }
            }

            Widgets.EndScrollView();
        }

        private void DrawTagModList(Rect rect)
        {
            ManagerTagData tag = FindCurrentTag();
            if (tag == null)
            {
                Widgets.DrawBox(rect);
                Widgets.Label(rect.ContractedBy(12f),
                    "태그가 없습니다. 상단의 '+ 태그 추가' 버튼으로 태그를 만드세요.");
                return;
            }

            Widgets.DrawBox(rect);
            Rect innerRect = rect.ContractedBy(8f);
            float viewHeight = Mathf.Max(innerRect.height, GetFolderListHeight(tag));
            Rect viewRect = new Rect(0f, 0f, innerRect.width - 16f, viewHeight);
            Widgets.BeginScrollView(innerRect, ref modListScroll, viewRect);

            float y = 0f;
            int visibleIndex = 0;
            for (int folderIndex = 0; folderIndex < tag.Folders.Count; folderIndex++)
            {
                ManagerFolderData folder = tag.Folders[folderIndex];
                Rect headerRect = new Rect(0f, y, viewRect.width, RowHeight);
                Widgets.DrawLightHighlight(headerRect);
                if (Widgets.ButtonText(new Rect(headerRect.x + 2f, y + 2f, 28f, 26f),
                    folder.Collapsed ? "▶" : "▼"))
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
                    OpenCreateFolderDialog(tag, folder);
                if (Widgets.ButtonText(new Rect(actionX + 46f, y + 2f, 62f, 26f), "이름"))
                    OpenRenameFolderDialog(tag, folder);
                GUI.enabled = folder.Id != "uncategorized";
                if (Widgets.ButtonText(new Rect(actionX + 112f, y + 2f, 62f, 26f), "삭제"))
                    ConfirmDeleteFolder(tag, folder);
                GUI.enabled = true;
                y += RowHeight;

                if (folder.Collapsed)
                    continue;

                for (int modIndex = 0; modIndex < folder.ModIds.Count; modIndex++)
                {
                    ModMetaData mod = FindMod(folder.ModIds[modIndex]);
                    if (mod == null)
                        continue;

                    DrawModRow(mod, visibleIndex, viewRect.width, y);
                    y += RowHeight;
                    visibleIndex++;
                }
            }

            Widgets.EndScrollView();
        }

        private void DrawModRow(ModMetaData mod, int visibleIndex, float width, float y)
        {
            Rect rowRect = new Rect(0f, y, width, RowHeight);
            if (selectedPackageIds.Contains(mod.PackageId))
                Widgets.DrawHighlight(rowRect);
            else
                Widgets.DrawHighlightIfMouseover(rowRect);

            Rect checkRect = new Rect(rowRect.x + 3f, rowRect.y + 3f, 24f, 24f);
            ManagerModData state = FindModState(mod.PackageId);
            bool checkedValue = state != null && state.Enabled;
            bool protectedMod = IsProtected(mod);
            Widgets.Checkbox(checkRect.position, ref checkedValue, checkRect.width, protectedMod);
            if (!protectedMod && state != null && checkedValue != state.Enabled)
                state.Enabled = checkedValue;

            Rect nameRect = new Rect(checkRect.xMax + 5f, rowRect.y,
                rowRect.width - checkRect.width - 12f, rowRect.height);
            if (Widgets.ButtonInvisible(nameRect))
                SelectMod(visibleIndex, Event.current.control, Event.current.shift);

            string name = mod.Name.NullOrEmpty() ? mod.FolderName : mod.Name;
            Widgets.Label(nameRect, protectedMod ? name + " (필수)" : name);
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

            ManagerModData modData = FindModState(mod.PackageId);
            List<string> tagNames = new List<string>();
            if (modData != null && modData.TagIds != null)
            {
                for (int i = 0; i < modData.TagIds.Count; i++)
                {
                    ManagerTagData tag = FindTag(modData.TagIds[i]);
                    if (tag != null)
                        tagNames.Add(tag.Name);
                }
            }
            Widgets.Label(new Rect(inner.x, y, inner.width - 115f, 26f),
                "태그: " + (tagNames.Count == 0 ? "없음" : string.Join(", ", tagNames)));
            if (Widgets.ButtonText(new Rect(inner.xMax - 110f, y, 110f, 26f), "태그 편집"))
                OpenSelectedTagMenu(new[] { mod.PackageId });
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
            {
                if (tagMode)
                    OpenTagFolderMoveMenu();
                else
                    OpenMoveMenu();
            }
            x += buttonWidth + 8f;

            if (Widgets.ButtonText(new Rect(x, rect.y, buttonWidth, rect.height), "선택 태그 편집"))
                OpenSelectedTagMenu(selectedPackageIds.ToArray());

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

        private void OpenTagFolderMoveMenu()
        {
            ManagerTagData tag = FindCurrentTag();
            if (tag == null || selectedPackageIds.Count == 0)
                return;

            List<FloatMenuOption> options = new List<FloatMenuOption>();
            for (int i = 0; i < tag.Folders.Count; i++)
            {
                ManagerFolderData folder = tag.Folders[i];
                if (folder.Id == currentFolderId)
                    continue;

                ManagerFolderData target = folder;
                options.Add(new FloatMenuOption(target.Name,
                    () => MoveSelectionToTagFolder(tag, target)));
            }
            if (options.Count > 0)
                Find.WindowStack.Add(new FloatMenu(options));
        }

        private void OpenSelectedTagMenu(IEnumerable<string> packageIds)
        {
            List<string> selectedIds = packageIds == null
                ? new List<string>()
                : packageIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (selectedIds.Count == 0)
                return;

            List<FloatMenuOption> options = new List<FloatMenuOption>();
            for (int i = 0; i < tags.Count; i++)
            {
                ManagerTagData tag = tags[i];
                ManagerTagData target = tag;
                bool allHaveTag = selectedIds.All(id => HasTag(id, target.Id));
                string action = allHaveTag ? "태그 제거: " : "태그 추가: ";
                options.Add(new FloatMenuOption(action + target.Name,
                    () => SetTagForSelection(selectedIds, target, !allHaveTag)));
            }
            options.Add(new FloatMenuOption("새 태그 만들기",
                () => OpenCreateTagDialog(selectedIds)));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void SetTagForSelection(
            List<string> packageIds,
            ManagerTagData tag,
            bool add)
        {
            ManagerFolderData unfiled = FindFolder(tag, "uncategorized");
            if (unfiled == null)
            {
                unfiled = new ManagerFolderData("uncategorized", "미분류");
                tag.Folders.Insert(0, unfiled);
            }

            for (int i = 0; i < packageIds.Count; i++)
            {
                ManagerModData modData = FindModState(packageIds[i]);
                if (modData == null)
                    continue;
                if (modData.TagIds == null)
                    modData.TagIds = new List<string>();

                if (add)
                {
                    if (!modData.TagIds.Contains(tag.Id))
                    {
                        modData.TagIds.Add(tag.Id);
                        unfiled.ModIds.Add(packageIds[i]);
                    }
                }
                else
                {
                    modData.TagIds.RemoveAll(id =>
                        string.Equals(id, tag.Id, StringComparison.OrdinalIgnoreCase));
                    for (int folderIndex = 0; folderIndex < tag.Folders.Count; folderIndex++)
                        tag.Folders[folderIndex].ModIds.RemoveAll(id =>
                            string.Equals(id, packageIds[i], StringComparison.OrdinalIgnoreCase));
                }
            }

            if (tagMode && tag.Id == currentTagId)
                RefreshVisibleMods();
        }

        private void OpenCreateTagDialog(IEnumerable<string> packageIds = null)
        {
            List<string> selectedIds = packageIds == null
                ? new List<string>()
                : packageIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            Find.WindowStack.Add(new ManagerFolderNameDialog(
                "새 태그",
                "",
                name =>
                {
                    if (tags.Any(tag => string.Equals(tag.Name, name.Trim(),
                        StringComparison.OrdinalIgnoreCase)))
                    {
                        Messages.Message("같은 이름의 태그가 이미 있습니다.",
                            MessageTypeDefOf.RejectInput);
                        return false;
                    }

                    ManagerTagData tag = new ManagerTagData(
                        Guid.NewGuid().ToString("N"), name.Trim());
                    tags.Add(tag);
                    if (selectedIds.Count > 0)
                        SetTagForSelection(selectedIds, tag, true);

                    tagMode = true;
                    SelectTag(tag);
                    return true;
                }));
        }

        private void OpenRenameTagDialog(ManagerTagData tag)
        {
            Find.WindowStack.Add(new ManagerFolderNameDialog(
                "태그 이름 변경",
                tag.Name,
                name =>
                {
                    if (tags.Any(existing => existing != tag &&
                        string.Equals(existing.Name, name.Trim(),
                            StringComparison.OrdinalIgnoreCase)))
                    {
                        Messages.Message("같은 이름의 태그가 이미 있습니다.",
                            MessageTypeDefOf.RejectInput);
                        return false;
                    }

                    tag.Name = name.Trim();
                    return true;
                }));
        }

        private void ConfirmDeleteTag(ManagerTagData tag)
        {
            if (tag == null)
                return;

            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "태그를 삭제하고 모든 모드에서 연결을 해제할까요?",
                () =>
                {
                    for (int i = 0; i < modStates.Count; i++)
                    {
                        if (modStates[i].TagIds != null)
                            modStates[i].TagIds.RemoveAll(id =>
                                string.Equals(id, tag.Id, StringComparison.OrdinalIgnoreCase));
                    }
                    tags.Remove(tag);
                    if (currentTagId == tag.Id)
                    {
                        currentTagId = tags.FirstOrDefault()?.Id;
                        currentFolderId = FindCurrentTag()?.Folders.FirstOrDefault()?.Id;
                        selectedPackageIds.Clear();
                        primarySelectedPackageId = null;
                        RefreshVisibleMods();
                    }
                },
                destructive: true));
        }

        private void ToggleTagEnabled(ManagerTagData tag)
        {
            List<ManagerModData> members = modStates.Where(mod =>
                mod.TagIds != null && mod.TagIds.Contains(tag.Id) &&
                FindMod(mod.PackageId) != null && !IsProtected(FindMod(mod.PackageId))).ToList();
            if (members.Count == 0)
                return;

            bool enable = members.Any(mod => !mod.Enabled);
            for (int i = 0; i < members.Count; i++)
                members[i].Enabled = enable;
        }

        private string GetTagStateLabel(ManagerTagData tag)
        {
            int enabled = 0;
            int total = 0;
            for (int i = 0; i < modStates.Count; i++)
            {
                ManagerModData state = modStates[i];
                ModMetaData mod = FindMod(state.PackageId);
                if (mod == null || IsProtected(mod) || state.TagIds == null ||
                    !state.TagIds.Contains(tag.Id))
                    continue;
                total++;
                if (state.Enabled)
                    enabled++;
            }
            if (total == 0 || enabled == 0)
                return "○";
            return enabled == total ? "●" : "◐";
        }

        private int GetInstalledCount(ManagerTagData tag)
        {
            int count = 0;
            for (int i = 0; i < modStates.Count; i++)
            {
                if (modStates[i].TagIds != null && modStates[i].TagIds.Contains(tag.Id) &&
                    FindMod(modStates[i].PackageId) != null)
                    count++;
            }
            return count;
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

        private void MoveSelectionToTagFolder(
            ManagerTagData tag,
            ManagerFolderData folder)
        {
            if (tag == null || folder == null)
                return;

            folder.Collapsed = false;
            List<string> movingIds = selectedPackageIds.ToList();
            for (int i = 0; i < movingIds.Count; i++)
            {
                string packageId = movingIds[i];
                if (!HasTag(packageId, tag.Id))
                    continue;

                for (int folderIndex = 0; folderIndex < tag.Folders.Count; folderIndex++)
                    tag.Folders[folderIndex].ModIds.RemoveAll(id =>
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

        private void OpenCreateFolderDialog(
            ManagerTagData tag,
            ManagerFolderData insertAfter)
        {
            Find.WindowStack.Add(new ManagerFolderNameDialog(
                "새 폴더",
                "",
                name =>
                {
                    if (HasFolderName(tag, name, null))
                    {
                        Messages.Message("같은 이름의 폴더가 이미 있습니다.",
                            MessageTypeDefOf.RejectInput);
                        return false;
                    }

                    ManagerFolderData folder = new ManagerFolderData(
                        Guid.NewGuid().ToString("N"), name.Trim());
                    int index = tag.Folders.IndexOf(insertAfter);
                    tag.Folders.Insert(index < 0 ? tag.Folders.Count : index + 1, folder);
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

        private void OpenRenameFolderDialog(ManagerTagData tag, ManagerFolderData folder)
        {
            Find.WindowStack.Add(new ManagerFolderNameDialog(
                "폴더 이름 변경",
                folder.Name,
                name =>
                {
                    if (HasFolderName(tag, name, folder))
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

        private void ConfirmDeleteFolder(ManagerTagData tag, ManagerFolderData folder)
        {
            if (folder == null || folder.Id == "uncategorized")
                return;

            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "폴더를 삭제할까요? 폴더 안의 모드는 이 태그의 미분류 폴더로 이동합니다.",
                () =>
                {
                    ManagerFolderData unfiled = FindFolder(tag, "uncategorized");
                    if (unfiled == null)
                    {
                        unfiled = new ManagerFolderData("uncategorized", "미분류");
                        tag.Folders.Insert(0, unfiled);
                    }
                    for (int i = 0; i < folder.ModIds.Count; i++)
                    {
                        if (!unfiled.ModIds.Contains(folder.ModIds[i]))
                            unfiled.ModIds.Add(folder.ModIds[i]);
                    }
                    tag.Folders.Remove(folder);
                    currentFolderId = unfiled.Id;
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

        private static bool HasFolderName(
            ManagerTagData tag,
            string name,
            ManagerFolderData except)
        {
            if (name.NullOrEmpty())
                return true;
            for (int i = 0; i < tag.Folders.Count; i++)
            {
                ManagerFolderData folder = tag.Folders[i];
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
            settings.Tags = tags.Select(tag => tag.Copy()).ToList();
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

        private void OpenSavePresetDialog()
        {
            Find.WindowStack.Add(new ManagerFolderNameDialog(
                "현재 구성 프리셋 저장",
                "",
                name =>
                {
                    if (presets.Any(preset => string.Equals(preset.Name, name.Trim(),
                        StringComparison.OrdinalIgnoreCase)))
                    {
                        Messages.Message("같은 이름의 프리셋이 이미 있습니다.",
                            MessageTypeDefOf.RejectInput);
                        return false;
                    }

                    ManagerPresetData preset = new ManagerPresetData(
                        Guid.NewGuid().ToString("N"),
                        name.Trim(),
                        categories,
                        tags,
                        modStates);
                    presets.Add(preset);
                    SavePresetsToSettings();
                    Messages.Message("프리셋을 저장했습니다: " + preset.Name,
                        MessageTypeDefOf.PositiveEvent);
                    return true;
                }));
        }

        private void OpenPresetMenu()
        {
            if (presets.Count == 0)
            {
                Messages.Message("저장된 프리셋이 없습니다.", MessageTypeDefOf.RejectInput);
                return;
            }

            List<FloatMenuOption> options = new List<FloatMenuOption>();
            for (int i = 0; i < presets.Count; i++)
            {
                ManagerPresetData preset = presets[i];
                ManagerPresetData target = preset;
                options.Add(new FloatMenuOption(
                    "불러오기: " + target.Name,
                    () => LoadPreset(target)));
                options.Add(new FloatMenuOption(
                    "삭제: " + target.Name,
                    () => ConfirmDeletePreset(target)));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void LoadPreset(ManagerPresetData preset)
        {
            if (preset == null)
                return;

            categories.Clear();
            categories.AddRange(preset.Categories.Select(category => category.Copy()));
            tags.Clear();
            tags.AddRange(preset.Tags.Select(tag => tag.Copy()));
            modStates.Clear();
            modStates.AddRange(preset.Mods.Select(mod => mod.Copy()));
            AddInstalledModsMissingFromPreset();

            currentCategoryId = categories.FirstOrDefault()?.Id;
            currentTagId = tags.FirstOrDefault()?.Id;
            currentFolderId = tagMode
                ? FindCurrentTag()?.Folders.FirstOrDefault()?.Id
                : FindCurrentCategory()?.Folders.FirstOrDefault()?.Id;
            selectedPackageIds.Clear();
            primarySelectedPackageId = null;
            lastSelectedIndex = -1;
            modListScroll = Vector2.zero;
            RefreshVisibleMods();
            Messages.Message(
                "프리셋을 편집 화면에 불러왔습니다. 적용을 눌러 설정을 저장하세요: " + preset.Name,
                MessageTypeDefOf.PositiveEvent);
        }

        private void AddInstalledModsMissingFromPreset()
        {
            ManagerCategoryData normalCategory = categories.FirstOrDefault(category =>
                string.Equals(category.Id, "normal", StringComparison.OrdinalIgnoreCase));
            if (normalCategory == null)
            {
                normalCategory = new ManagerCategoryData("normal", "일반 모드");
                categories.Add(normalCategory);
            }

            ManagerFolderData unfiled = FindFolder(normalCategory, "uncategorized");
            if (unfiled == null)
            {
                unfiled = new ManagerFolderData("uncategorized", "미분류");
                normalCategory.Folders.Insert(0, unfiled);
            }

            foreach (ModMetaData mod in modsByPackageId.Values)
            {
                if (FindModState(mod.PackageId) == null)
                    modStates.Add(new ManagerModData(mod.PackageId, mod.Active));

                if (categories.Any(category => category.ModIds != null &&
                    category.ModIds.Any(packageId => string.Equals(
                        packageId, mod.PackageId, StringComparison.OrdinalIgnoreCase))))
                    continue;

                ManagerCategoryData destination = mod.IsCoreMod || mod.Official
                    ? categories.FirstOrDefault(category =>
                        string.Equals(category.Id, "core", StringComparison.OrdinalIgnoreCase))
                    : normalCategory;
                if (destination == null)
                    destination = normalCategory;
                ManagerFolderData destinationFolder =
                    FindFolder(destination, "uncategorized");
                if (destinationFolder == null)
                {
                    destinationFolder = new ManagerFolderData("uncategorized", "미분류");
                    destination.Folders.Insert(0, destinationFolder);
                }
                destination.ModIds.Add(mod.PackageId);
                destinationFolder.ModIds.Add(mod.PackageId);
            }
        }

        private void ConfirmDeletePreset(ManagerPresetData preset)
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "프리셋 '" + preset.Name + "'을(를) 삭제할까요?",
                () =>
                {
                    presets.Remove(preset);
                    SavePresetsToSettings();
                    Messages.Message("프리셋을 삭제했습니다: " + preset.Name,
                        MessageTypeDefOf.PositiveEvent);
                },
                destructive: true));
        }

        private void SavePresetsToSettings()
        {
            settings.Presets = presets.Select(preset => preset.Copy()).ToList();
            IntegratedModManagerMod.Instance.WriteSettings();
        }

        private void RefreshVisibleMods()
        {
            visibleMods.Clear();
            if (tagMode)
            {
                ManagerTagData tag = FindCurrentTag();
                if (tag == null)
                    return;
                for (int folderIndex = 0; folderIndex < tag.Folders.Count; folderIndex++)
                {
                    ManagerFolderData folder = tag.Folders[folderIndex];
                    if (folder.Collapsed)
                        continue;
                    for (int modIndex = 0; modIndex < folder.ModIds.Count; modIndex++)
                    {
                        ModMetaData mod = FindMod(folder.ModIds[modIndex]);
                        if (mod != null)
                            visibleMods.Add(mod);
                    }
                }
                return;
            }

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

        private float GetFolderListHeight(ManagerTagData tag)
        {
            float height = 0f;
            for (int i = 0; i < tag.Folders.Count; i++)
            {
                ManagerFolderData folder = tag.Folders[i];
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

        private static ManagerFolderData FindFolder(ManagerTagData tag, string id)
        {
            if (tag == null || tag.Folders == null)
                return null;
            for (int i = 0; i < tag.Folders.Count; i++)
            {
                if (tag.Folders[i] != null &&
                    string.Equals(tag.Folders[i].Id, id,
                        StringComparison.OrdinalIgnoreCase))
                    return tag.Folders[i];
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

        private ManagerTagData FindCurrentTag()
        {
            return FindTag(currentTagId);
        }

        private ManagerTagData FindTag(string tagId)
        {
            if (tagId.NullOrEmpty())
                return null;
            for (int i = 0; i < tags.Count; i++)
            {
                if (string.Equals(tags[i].Id, tagId, StringComparison.OrdinalIgnoreCase))
                    return tags[i];
            }
            return null;
        }

        private bool HasTag(string packageId, string tagId)
        {
            ManagerModData modData = FindModState(packageId);
            return modData != null && modData.TagIds != null &&
                modData.TagIds.Contains(tagId);
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
