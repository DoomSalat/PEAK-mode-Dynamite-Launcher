using Peak;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace DynamiteLauncher
{
    [HarmonyPatch]
    internal static class ChargeSpeedPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Item), nameof(Item.StartUsePrimary));
            yield return AccessTools.Method(typeof(Item), nameof(Item.ContinueUsePrimary));
        }

        private static void Prefix(Item __instance, out float? __state)
        {
            __state = null;
            float multiplier = Plugin.ChargeSpeedMultiplier.Value;
            if (multiplier == 1f || Plugin.VariantOf(__instance) == 0)
            {
                return;
            }

            __state = __instance.usingTimePrimary;
            __instance.usingTimePrimary /= multiplier;
        }

        private static void Finalizer(Item __instance, float? __state)
        {
            if (__state.HasValue)
            {
                __instance.usingTimePrimary = __state.Value;
            }
        }
    }

    // Removes the stock range gate: the gun is always shootable.
    [HarmonyPatch(typeof(Action_RaycastSpawnSomething), "FixedUpdate")]
    public static class RangePatch
    {
        private static void Postfix(Item ___item)
        {
            if (Plugin.VariantOf(___item) != 0)
            {
                ___item.UIData.isShootable = true;
            }
        }
    }

    // Gold variants never spend ammo (on modded clients).
    [HarmonyPatch(typeof(Action_ReduceUses), nameof(Action_ReduceUses.ReduceUsesRPC))]
    public static class InfiniteAmmoPatch
    {
        private static bool Prefix(Item ___item)
        {
            return !Variants.InfiniteAmmo(Plugin.VariantOf(___item));
        }
    }
}
