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
            if (!Plugin.SpawnInLoot.Value || !PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient ||
                Plugin.SilverPrefab == null || Plugin.GoldPrefab == null || Plugin.JumpSilverPrefab == null || Plugin.JumpGoldPrefab == null)
            {
                return;
            }

            var additions = new Dictionary<ushort, int>();
            AddFamily(__0, Plugin.EnableMagicalLoot.Value, Plugin.EnableRareChestLoot.Value, Plugin.ChestLootRarity.Value, Plugin.SilverPrefab, Plugin.GoldPrefab, additions);
            AddFamily(__0, Plugin.JumperEnableMagicalLoot.Value, Plugin.JumperEnableRareChestLoot.Value, Plugin.JumperChestLootRarity.Value, Plugin.JumpSilverPrefab, Plugin.JumpGoldPrefab, additions);
            if (additions.Count == 0)
            {
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

            var withLaunchers = new Dictionary<ushort, int>(weights);
            foreach (var addition in additions)
            {
                withLaunchers[addition.Key] = LootData.RarityWeights.TryGetValue((Rarity)addition.Value, out int configuredWeight)
                    ? configuredWeight
                    : LootData.RarityWeights[Rarity.Rare];
            }

            __state = (pool, weights);
            LootData.AllSpawnWeightData[pool] = withLaunchers;
        }

        // Rarity is stored in the dictionary value until the weight lookup.
        private static void AddFamily(SpawnPool pool, bool magicalEnabled, bool chestEnabled, Rarity chestRarity, GameObject silver, GameObject gold, Dictionary<ushort, int> additions)
        {
            Rarity rarity;
            switch (pool)
            {
                case SpawnPool.LuggageAncient:
                case SpawnPool.RespawnCoffin:
                    if (!magicalEnabled)
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
                    if (!chestEnabled)
                    {
                        return;
                    }
                    rarity = chestRarity;
                    break;
                default:
                    return;
            }

            additions[silver.GetComponent<Item>().itemID] = (int)rarity;
            additions[gold.GetComponent<Item>().itemID] = (int)rarity;
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

            __state = Variants.FromPrefabName(__0);
            if (__state == 0)
            {
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
