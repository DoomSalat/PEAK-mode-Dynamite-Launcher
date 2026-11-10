using HarmonyLib;
using UnityEngine;

namespace DynamiteLauncher
{
    // ItemSpawnerPremium spawns by name through this method: hand out a stock Anti-Zooka with our marker instead.
    [HarmonyPatch(typeof(CharacterItems), "SpawnItemInHand")]
    public static class SpawnInHandPatch
    {
        private static bool Prefix(string objName)
        {
            int variant = Variants.FromPrefabName(objName);
            if (variant == 0)
            {
                return true;
            }

            Plugin.GiveLauncher(variant);
            return false;
        }
    }

    // Mods like ItemSpawnerPremium check Resources.Load("0_Items/<name>") before spawning; answer it for our cloned prefabs.
    [HarmonyPatch(typeof(Resources), nameof(Resources.Load), new[] { typeof(string), typeof(System.Type) })]
    public static class ResourcesLoadPatch
    {
        private static void Postfix(string path, ref Object __result)
        {
            if (__result != null)
            {
                return;
            }

            if (path != null && path.StartsWith(Plugin.ItemsFolder))
            {
                __result = Variants.Prefab(Variants.FromPrefabName(path.Substring(Plugin.ItemsFolder.Length)));
            }
        }
    }
}
