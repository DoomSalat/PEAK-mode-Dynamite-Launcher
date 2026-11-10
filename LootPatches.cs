using System.Collections.Generic;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace DynamiteLauncher
{
    [HarmonyPatch(typeof(LootData), nameof(LootData.GetRandomItems))]
    internal static class LauncherLootPoolPatch
    {
        private static void Prefix(SpawnPool __0, out (SpawnPool Pool, Dictionary<ushort, int> Original) __state)
        {
            __state = default;
            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient ||
                Plugin.SilverPrefab == null || Plugin.GoldPrefab == null)
            {
                return;
            }

            Rarity rarity;
            switch (__0)
            {
                case SpawnPool.LuggageAncient:
                case SpawnPool.RespawnCoffin:
                    if (!Plugin.EnableMagicalLoot.Value)
                    {
                        return;
                    }
                    rarity = Rarity.Mythic;
                    break;
                case SpawnPool.LuggageBeach:
                case SpawnPool.LuggageJungle:
                case SpawnPool.LuggageTundra:
                case SpawnPool.LuggageCaldera:
                case SpawnPool.LuggageClimber:
                case SpawnPool.LuggageMesa:
                case SpawnPool.LuggageRoots:
                case SpawnPool.LuggageGloom:
                case SpawnPool.LuggageCitadel:
                    if (!Plugin.EnableRareChestLoot.Value)
                    {
                        return;
                    }
                    rarity = Plugin.ChestLootRarity.Value;
                    break;
                default:
                    return;
            }

            if (LootData.AllSpawnWeightData == null)
            {
                LootData.PopulateLootData();
            }

            SpawnPool pool = EffectivePool(__0);
            if (!LootData.AllSpawnWeightData.TryGetValue(pool, out var weights))
            {
                return;
            }

            int weight = LootData.RarityWeights.TryGetValue(rarity, out int configuredWeight)
                ? configuredWeight
                : LootData.RarityWeights[Rarity.Rare];
            var withLaunchers = new Dictionary<ushort, int>(weights)
            {
                [Plugin.SilverPrefab.GetComponent<Item>().itemID] = weight,
                [Plugin.GoldPrefab.GetComponent<Item>().itemID] = weight
            };
            __state = (pool, weights);
            LootData.AllSpawnWeightData[pool] = withLaunchers;
        }

        private static void Finalizer((SpawnPool Pool, Dictionary<ushort, int> Original) __state)
        {
            if (__state.Original != null)
            {
                LootData.AllSpawnWeightData[__state.Pool] = __state.Original;
            }
        }

        private static SpawnPool EffectivePool(SpawnPool pool)
        {
            return RunSettings.GetValue(RunSettings.SETTINGTYPE.ChaosItems, false) == 1
                ? SpawnPool.All
                : pool;
        }
    }

    [HarmonyPatch(typeof(PhotonNetwork), nameof(PhotonNetwork.InstantiateItemRoom))]
    internal static class LauncherLootSpawnPatch
    {
        private static void Prefix(ref string __0, out int __state)
        {
            __state = 0;
            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
            {
                return;
            }

            switch (__0)
            {
                case Plugin.SilverPrefabName:
                    __state = Plugin.SilverVariant;
                    break;
                case Plugin.GoldPrefabName:
                    __state = Plugin.GoldVariant;
                    break;
                default:
                    return;
            }

            __0 = Plugin.SourcePrefabName;
        }

        private static void Postfix(GameObject __result, int __state)
        {
            if (__state != 0 && __result != null)
            {
                Plugin.InitializeLauncher(__result.GetComponent<Item>(), __state);
            }
        }
    }
}
