using Peak;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace DynamiteLauncher
{
    // Replaces the anti-gravity sphere spawn with a slow lit flare. Ammo is spent by the item's own Action_ReduceUses.
    [HarmonyPatch(typeof(Action_RaycastSpawnSomething), nameof(Action_RaycastSpawnSomething.RunAction))]
    public static class ShootPatch
    {
        private static bool Prefix(Action_RaycastSpawnSomething __instance)
        {
            if (Plugin.VariantOf(__instance.item) == 0)
            {
                return true;
            }

            Vector3 direction = MainCamera.instance.transform.forward;
            Vector3 position = __instance.rayOrigin.position + direction * Plugin.FlareSpawnOffset.Value;
            GameObject flare = PhotonNetwork.Instantiate("0_Items/Flare", position, FlareProjectile.RotationFor(direction), 0);
            flare.AddComponent<FlareProjectile>().Direction = direction;
            Plugin.Log("Shot variant " + Plugin.VariantOf(__instance.item) + ": flare viewID " + flare.GetComponent<PhotonView>().ViewID + ", dir " + direction);

            return false;
        }
    }

    // Removes the stock range gate: the gun is always shootable.
    [HarmonyPatch(typeof(Action_RaycastSpawnSomething), "FixedUpdate")]
    public static class RangePatch
    {
        private static void Postfix(Action_RaycastSpawnSomething __instance)
        {
            if (Plugin.VariantOf(__instance.item) != 0)
            {
                __instance.item.UIData.isShootable = true;
            }
        }
    }

    // Gold variant never spends ammo (on modded clients).
    [HarmonyPatch(typeof(Action_ReduceUses), nameof(Action_ReduceUses.ReduceUsesRPC))]
    public static class InfiniteAmmoPatch
    {
        private static bool Prefix(Action_ReduceUses __instance)
        {
            bool infinite = Plugin.GoldInfiniteAmmo.Value && Plugin.VariantOf(__instance.item) == Plugin.GoldVariant;
            if (infinite)
            {
                Plugin.Log("Gold launcher: ammo not spent");
            }

            return !infinite;
        }
    }
}
