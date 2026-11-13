using System;
using System.Collections.Generic;
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
        public List<ManagerModData> Mods = new List<ManagerModData>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref Categories, "categories", LookMode.Deep);
            Scribe_Collections.Look(ref Mods, "mods", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                EnsureInitialized();
        }

        internal void EnsureInitialized()
        {
            if (Categories == null)
                Categories = new List<ManagerCategoryData>();
            if (Mods == null)
                Mods = new List<ManagerModData>();

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
                }
            }
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
        }

        internal ManagerCategoryData Copy()
        {
            ManagerCategoryData copy = new ManagerCategoryData(Id, Name);
            copy.Enabled = Enabled;
            copy.ModIds = ModIds == null
                ? new List<string>()
                : new List<string>(ModIds);
            return copy;
        }
    }

    public sealed class ManagerModData : IExposable
    {
        public string PackageId;
        public bool Enabled = true;

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
        }

        internal ManagerModData Copy()
        {
            return new ManagerModData(PackageId, Enabled);
        }
    }
}
