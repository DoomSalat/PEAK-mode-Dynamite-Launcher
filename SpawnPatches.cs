using HarmonyLib;
using UnityEngine;

namespace PeakRedFlareLauncher
{
    // ItemSpawnerPremium spawns by name through this method: hand out a stock Anti-Zooka with our marker instead.
    [HarmonyPatch(typeof(CharacterItems), "SpawnItemInHand")]
    public static class SpawnInHandPatch
    {
        private static bool Prefix(string objName)
        {
            if (objName == Plugin.SilverPrefabName)
            {
                Plugin.GiveLauncher(Plugin.SilverVariant);
            }
            else if (objName == Plugin.GoldPrefabName)
            {
                Plugin.GiveLauncher(Plugin.GoldVariant);
            }
            else
            {
                return true;
            }

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

            if (path == "0_Items/" + Plugin.SilverPrefabName)
            {
                __result = Plugin.SilverPrefab;
            }
            else if (path == "0_Items/" + Plugin.GoldPrefabName)
            {
                __result = Plugin.GoldPrefab;
            }
        }
    }
}
