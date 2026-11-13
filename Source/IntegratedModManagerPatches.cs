using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Lilly.IntegratedModManager
{
    [StaticConstructorOnStartup]
    internal static class IntegratedModManagerStartup
    {
        static IntegratedModManagerStartup()
        {
            new Harmony("Lilly.IntegratedModManager")
                .PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.Add), new[] { typeof(Window) })]
    internal static class WindowStack_Add_ModManager_Patch
    {
        private static void Prefix(ref Window __0)
        {
            if (__0 is Page_ModsConfig)
                __0 = new IntegratedModManagerPage();
        }
    }
}
