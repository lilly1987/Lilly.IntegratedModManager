using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Lilly.IntegratedModManager
{
    public sealed class IntegratedModManagerMod : Mod
    {
        internal static IntegratedModManagerMod Instance { get; private set; }

        internal IntegratedModManagerSettings Settings { get; private set; }

        public IntegratedModManagerMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<IntegratedModManagerSettings>();
        }

        public override string SettingsCategory()
        {
            return "Lilly - Integrated Mod Manager";
        }
    }

    public sealed class IntegratedModManagerSettings : ModSettings
    {
        public List<ManagerCategoryData> Categories = new List<ManagerCategoryData>();
        public List<ManagerTagData> Tags = new List<ManagerTagData>();
        public List<ManagerModData> Mods = new List<ManagerModData>();
        public List<ManagerPresetData> Presets = new List<ManagerPresetData>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref Categories, "categories", LookMode.Deep);
            Scribe_Collections.Look(ref Tags, "tags", LookMode.Deep);
            Scribe_Collections.Look(ref Mods, "mods", LookMode.Deep);
            Scribe_Collections.Look(ref Presets, "presets", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                EnsureInitialized();
        }

        internal void EnsureInitialized()
        {
            if (Categories == null)
                Categories = new List<ManagerCategoryData>();
            if (Mods == null)
                Mods = new List<ManagerModData>();
            if (Tags == null)
                Tags = new List<ManagerTagData>();
            if (Presets == null)
                Presets = new List<ManagerPresetData>();
            for (int i = Presets.Count - 1; i >= 0; i--)
            {
                if (Presets[i] == null || Presets[i].Id.NullOrEmpty() ||
                    Presets[i].Name.NullOrEmpty())
                    Presets.RemoveAt(i);
            }

            if (Categories.Count == 0)
            {
                Categories.Add(new ManagerCategoryData("before", "선행 모드"));
                Categories.Add(new ManagerCategoryData("core", "핵심 모드"));
                Categories.Add(new ManagerCategoryData("normal", "일반 모드"));
                Categories.Add(new ManagerCategoryData("after", "후순위 모드"));
            }

            ManagerCategoryData coreCategory = FindCategory("core") ?? Categories[0];
            ManagerCategoryData normalCategory = FindCategory("normal") ?? Categories[Categories.Count - 1];
            HashSet<string> assignedPackageIds =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < Categories.Count; i++)
            {
                ManagerCategoryData category = Categories[i];
                if (category == null)
                {
                    Categories.RemoveAt(i--);
                    continue;
                }

                if (category.ModIds == null)
                    category.ModIds = new List<string>();
                if (category.Folders == null)
                    category.Folders = new List<ManagerFolderData>();
                if (category.Folders.Count == 0)
                    category.Folders.Add(new ManagerFolderData(
                        "uncategorized", "미분류"));

                HashSet<string> folderPackageIds =
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int folderIndex = category.Folders.Count - 1; folderIndex >= 0; folderIndex--)
                {
                    ManagerFolderData folder = category.Folders[folderIndex];
                    if (folder == null)
                    {
                        category.Folders.RemoveAt(folderIndex);
                        continue;
                    }

                    if (folder.ModIds == null)
                        folder.ModIds = new List<string>();
                    for (int modIndex = folder.ModIds.Count - 1; modIndex >= 0; modIndex--)
                    {
                        string packageId = folder.ModIds[modIndex];
                        if (packageId.NullOrEmpty() ||
                            !category.ModIds.Contains(packageId) ||
                            !folderPackageIds.Add(packageId))
                            folder.ModIds.RemoveAt(modIndex);
                    }
                }

                ManagerFolderData uncategorized = FindFolder(category, "uncategorized");
                if (uncategorized == null)
                {
                    uncategorized = new ManagerFolderData("uncategorized", "미분류");
                    category.Folders.Insert(0, uncategorized);
                }

                for (int modIndex = 0; modIndex < category.ModIds.Count; modIndex++)
                {
                    string packageId = category.ModIds[modIndex];
                    if (folderPackageIds.Add(packageId))
                        uncategorized.ModIds.Add(packageId);
                }

                for (int j = category.ModIds.Count - 1; j >= 0; j--)
                {
                    string packageId = category.ModIds[j];
                    if (packageId.NullOrEmpty() || !assignedPackageIds.Add(packageId))
                        category.ModIds.RemoveAt(j);
                }
            }

            foreach (ModMetaData mod in ModLister.AllInstalledMods)
            {
                if (mod == null || mod.PackageId.NullOrEmpty())
                    continue;

                string packageId = mod.PackageId;
                GetOrCreateModData(mod);

                if (assignedPackageIds.Add(packageId))
                {
                    ManagerCategoryData category =
                        mod.IsCoreMod || mod.Official ? coreCategory : normalCategory;
                    category.ModIds.Add(packageId);
                    ManagerFolderData uncategorized = FindFolder(category, "uncategorized");
                    if (uncategorized == null)
                    {
                        uncategorized = new ManagerFolderData("uncategorized", "미분류");
                        category.Folders.Insert(0, uncategorized);
                    }
                    uncategorized.ModIds.Add(packageId);
                }
            }

            for (int tagIndex = Tags.Count - 1; tagIndex >= 0; tagIndex--)
            {
                ManagerTagData tag = Tags[tagIndex];
                if (tag == null || tag.Id.NullOrEmpty())
                {
                    Tags.RemoveAt(tagIndex);
                    continue;
                }

                if (tag.Folders == null)
                    tag.Folders = new List<ManagerFolderData>();
                if (tag.Folders.Count == 0)
                    tag.Folders.Add(new ManagerFolderData("uncategorized", "미분류"));

                HashSet<string> linkedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int folderIndex = tag.Folders.Count - 1; folderIndex >= 0; folderIndex--)
                {
                    ManagerFolderData folder = tag.Folders[folderIndex];
                    if (folder == null)
                    {
                        tag.Folders.RemoveAt(folderIndex);
                        continue;
                    }

                    if (folder.ModIds == null)
                        folder.ModIds = new List<string>();
                    for (int modIndex = folder.ModIds.Count - 1; modIndex >= 0; modIndex--)
                    {
                        string packageId = folder.ModIds[modIndex];
                        ManagerModData modData = FindModData(packageId);
                        if (packageId.NullOrEmpty() || modData == null ||
                            modData.TagIds == null || !modData.TagIds.Contains(tag.Id) ||
                            !linkedIds.Add(packageId))
                            folder.ModIds.RemoveAt(modIndex);
                    }
                }

                ManagerFolderData unfiled = FindTagFolder(tag, "uncategorized");
                if (unfiled == null)
                {
                    unfiled = new ManagerFolderData("uncategorized", "미분류");
                    tag.Folders.Insert(0, unfiled);
                }

                for (int modIndex = 0; modIndex < Mods.Count; modIndex++)
                {
                    ManagerModData modData = Mods[modIndex];
                    if (modData != null && modData.TagIds != null &&
                        modData.TagIds.Contains(tag.Id) && linkedIds.Add(modData.PackageId))
                        unfiled.ModIds.Add(modData.PackageId);
                }
            }
        }

        private static ManagerFolderData FindFolder(ManagerCategoryData category, string id)
        {
            if (category.Folders == null)
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

        private static ManagerFolderData FindTagFolder(ManagerTagData tag, string id)
        {
            if (tag.Folders == null)
                return null;
            for (int i = 0; i < tag.Folders.Count; i++)
            {
                if (tag.Folders[i] != null &&
                    string.Equals(tag.Folders[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    return tag.Folders[i];
            }
            return null;
        }

        internal ManagerCategoryData FindCategory(string id)
        {
            if (Categories == null)
                return null;

            for (int i = 0; i < Categories.Count; i++)
            {
                if (Categories[i] != null &&
                    string.Equals(Categories[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    return Categories[i];
            }
            return null;
        }

        internal ManagerModData FindModData(string packageId)
        {
            if (Mods == null)
                return null;

            for (int i = 0; i < Mods.Count; i++)
            {
                if (Mods[i] != null &&
                    string.Equals(Mods[i].PackageId, packageId, StringComparison.OrdinalIgnoreCase))
                    return Mods[i];
            }
            return null;
        }

        internal ManagerTagData FindTag(string id)
        {
            if (Tags == null)
                return null;
            for (int i = 0; i < Tags.Count; i++)
            {
                if (Tags[i] != null &&
                    string.Equals(Tags[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    return Tags[i];
            }
            return null;
        }

        internal ManagerModData GetOrCreateModData(ModMetaData mod)
        {
            ManagerModData data = FindModData(mod.PackageId);
            if (data != null)
                return data;

            data = new ManagerModData(mod.PackageId, mod.Active);
            Mods.Add(data);
            return data;
        }
    }

    public sealed class ManagerCategoryData : IExposable
    {
        public string Id;
        public string Name;
        public bool Enabled = true;
        public List<string> ModIds = new List<string>();
        public List<ManagerFolderData> Folders = new List<ManagerFolderData>();

        public ManagerCategoryData()
        {
        }

        public ManagerCategoryData(string id, string name)
        {
            Id = id;
            Name = name;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Id, "id");
            Scribe_Values.Look(ref Name, "name");
            Scribe_Values.Look(ref Enabled, "enabled", true);
            Scribe_Collections.Look(ref ModIds, "mods", LookMode.Value);
            Scribe_Collections.Look(ref Folders, "folders", LookMode.Deep);
        }

        internal ManagerCategoryData Copy()
        {
            ManagerCategoryData copy = new ManagerCategoryData(Id, Name);
            copy.Enabled = Enabled;
            copy.ModIds = ModIds == null
                ? new List<string>()
                : new List<string>(ModIds);
            copy.Folders = Folders == null
                ? new List<ManagerFolderData>()
                : Folders.Select(folder => folder.Copy()).ToList();
            return copy;
        }
    }

    public sealed class ManagerFolderData : IExposable
    {
        public string Id;
        public string Name;
        public bool Collapsed;
        public List<string> ModIds = new List<string>();

        public ManagerFolderData()
        {
        }

        public ManagerFolderData(string id, string name)
        {
            Id = id;
            Name = name;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Id, "id");
            Scribe_Values.Look(ref Name, "name");
            Scribe_Values.Look(ref Collapsed, "collapsed", false);
            Scribe_Collections.Look(ref ModIds, "mods", LookMode.Value);
        }

        internal ManagerFolderData Copy()
        {
            ManagerFolderData copy = new ManagerFolderData(Id, Name);
            copy.Collapsed = Collapsed;
            copy.ModIds = ModIds == null
                ? new List<string>()
                : new List<string>(ModIds);
            return copy;
        }
    }

    public sealed class ManagerTagData : IExposable
    {
        public string Id;
        public string Name;
        public List<ManagerFolderData> Folders = new List<ManagerFolderData>();

        public ManagerTagData()
        {
        }

        public ManagerTagData(string id, string name)
        {
            Id = id;
            Name = name;
            Folders.Add(new ManagerFolderData("uncategorized", "미분류"));
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Id, "id");
            Scribe_Values.Look(ref Name, "name");
            Scribe_Collections.Look(ref Folders, "folders", LookMode.Deep);
        }

        internal ManagerTagData Copy()
        {
            ManagerTagData copy = new ManagerTagData(Id, Name);
            copy.Folders = Folders == null
                ? new List<ManagerFolderData>()
                : Folders.Select(folder => folder.Copy()).ToList();
            return copy;
        }
    }

    public sealed class ManagerModData : IExposable
    {
        public string PackageId;
        public bool Enabled = true;
        public List<string> TagIds = new List<string>();

        public ManagerModData()
        {
        }

        public ManagerModData(string packageId, bool enabled)
        {
            PackageId = packageId;
            Enabled = enabled;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref PackageId, "packageId");
            Scribe_Values.Look(ref Enabled, "enabled", true);
            Scribe_Collections.Look(ref TagIds, "tags", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && TagIds == null)
                TagIds = new List<string>();
        }

        internal ManagerModData Copy()
        {
            ManagerModData copy = new ManagerModData(PackageId, Enabled);
            copy.TagIds = TagIds == null ? new List<string>() : new List<string>(TagIds);
            return copy;
        }
    }

    public sealed class ManagerPresetData : IExposable
    {
        public string Id;
        public string Name;
        public List<ManagerCategoryData> Categories = new List<ManagerCategoryData>();
        public List<ManagerTagData> Tags = new List<ManagerTagData>();
        public List<ManagerModData> Mods = new List<ManagerModData>();

        public ManagerPresetData()
        {
        }

        public ManagerPresetData(
            string id,
            string name,
            IEnumerable<ManagerCategoryData> categories,
            IEnumerable<ManagerTagData> tags,
            IEnumerable<ManagerModData> mods)
        {
            Id = id;
            Name = name;
            Categories = categories.Select(category => category.Copy()).ToList();
            Tags = tags.Select(tag => tag.Copy()).ToList();
            Mods = mods.Select(mod => mod.Copy()).ToList();
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Id, "id");
            Scribe_Values.Look(ref Name, "name");
            Scribe_Collections.Look(ref Categories, "categories", LookMode.Deep);
            Scribe_Collections.Look(ref Tags, "tags", LookMode.Deep);
            Scribe_Collections.Look(ref Mods, "mods", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (Categories == null)
                    Categories = new List<ManagerCategoryData>();
                if (Tags == null)
                    Tags = new List<ManagerTagData>();
                if (Mods == null)
                    Mods = new List<ManagerModData>();
            }
        }

        internal ManagerPresetData Copy()
        {
            return new ManagerPresetData(Id, Name, Categories, Tags, Mods);
        }
    }
}
