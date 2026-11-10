using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Peak;
using HarmonyLib;
using ExitGames.Client.Photon;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace DynamiteLauncher
{
    // Replaces the anti-gravity sphere spawn with a slow lit flare. Ammo is spent by the item's own Action_ReduceUses.
    [HarmonyPatch(typeof(Action_RaycastSpawnSomething), nameof(Action_RaycastSpawnSomething.RunAction))]
    public static class ShootPatch
    {
        internal static void Launch(Vector3 muzzle, Vector3 direction)
        {
            GameObject flare = PhotonNetwork.Instantiate("0_Items/Flare", muzzle + direction * Plugin.FlareSpawnOffset.Value, FlareProjectile.RotationFor(direction), 0);
            flare.AddComponent<FlareProjectile>().Direction = direction;
        }

        private static bool Prefix(Item ___item, Transform ___rayOrigin)
        {
            if (Plugin.VariantOf(___item) == 0)
            {
                return true;
            }

            Launch(___rayOrigin.position, MainCamera.instance.transform.forward);

            return false;
        }
    }

    // Optional: the client that created a launcher swaps the stock sphere a player without the mod fires from it for our flare.
    [HarmonyPatch(typeof(AntiSphere), "FixedUpdate")]
    public static class RemoteShotPatch
    {
        private const byte DestroyEventCode = 204;
        private const int DestroyResends = 4;
        private const float DestroyResendDelay = 0.4f;

        private class Checked : MonoBehaviour
        {
        }

        private static void Prefix(AntiSphere __instance)
        {
            if (!Plugin.ReplaceOthersShots.Value || __instance.GetComponent<Checked>() != null)
            {
                return;
            }

            __instance.gameObject.AddComponent<Checked>();
            PhotonView view = __instance.GetComponent<PhotonView>();
            if (view == null || view.IsMine)
            {
                return;
            }

            foreach (Character character in Character.AllCharacters)
            {
                Item item = character.data.currentItem;
                if (character.photonView.Owner != view.Owner || item == null || Plugin.VariantOf(item) == 0)
                {
                    continue;
                }

                if (!IsCreator(item))
                {
                    return;
                }

                Action_RaycastSpawnSomething action = item.GetComponent<Action_RaycastSpawnSomething>();
                Vector3 muzzle = action.rayOrigin.position;
                Vector3 direction = __instance.transform.position - muzzle;
                ShootPatch.Launch(muzzle, direction.sqrMagnitude > 0.01f ? direction.normalized : character.transform.forward);
                DestroyRemote(view);
                return;
            }
        }

        private static bool IsCreator(Item item)
        {
            return item.data.TryGetDataEntry<IntItemData>(Plugin.CreatorKey, out IntItemData value) && value.Value == PhotonNetwork.LocalPlayer.ActorNumber;
        }

        // Events from different senders are unordered: a third client may get our destroy before the shooter's instantiate, so resend.
        private static IEnumerator SendDestroyRepeatedly(int viewId)
        {
            Hashtable data = new Hashtable { [(byte)0] = viewId };
            for (int i = 0; i < DestroyResends; i++)
            {
                PhotonNetwork.NetworkingClient.OpRaiseEvent(DestroyEventCode, data, new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
                yield return new WaitForSeconds(DestroyResendDelay);
            }
        }

        // Photon's own Destroy refuses foreign objects, so send the raw destroy event (code 204) like PhotonNetwork.Destroy does.
        private static void DestroyRemote(PhotonView view)
        {
            if (view.IsMine || PhotonNetwork.IsMasterClient)
            {
                PhotonNetwork.Destroy(view.gameObject);
                return;
            }

            Plugin.Instance.StartCoroutine(SendDestroyRepeatedly(view.ViewID));
            Object.Destroy(view.gameObject);
        }
    }

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

    // Gold variant never spends ammo (on modded clients).
    [HarmonyPatch(typeof(Action_ReduceUses), nameof(Action_ReduceUses.ReduceUsesRPC))]
    public static class InfiniteAmmoPatch
    {
        private static bool Prefix(Item ___item)
        {
            return !(Plugin.GoldInfiniteAmmo.Value && Plugin.VariantOf(___item) == Plugin.GoldVariant);
        }
    }
}
