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
        private const float RearMuzzleDistance = 2f;

        // Every shot goes through here: if something solid sits where the flare would appear (a wall right in front or behind the shooter), blast there instead of spawning a flare inside the mesh.
        internal static void Launch(Item source, Vector3 muzzle, Vector3 direction, bool rear = false)
        {
            float reach = Plugin.FlareSpawnOffset.Value + Plugin.FlareRadius.Value;
            Vector3 shot = direction;
            Vector3 spawn = muzzle;
            if (rear)
            {
                shot = -direction;
                reach += RearMuzzleDistance;
                spawn = muzzle + shot * RearMuzzleDistance;
            }

            if (SpawnBlocked(muzzle, shot, reach, out Vector3 blastPoint))
            {
                FlareProjectile.Detonate(source, blastPoint);
                return;
            }

            GameObject flare = PhotonNetwork.Instantiate("0_Items/Flare", spawn + shot * Plugin.FlareSpawnOffset.Value, FlareProjectile.RotationFor(shot), 0);
            FlareProjectile projectile = flare.AddComponent<FlareProjectile>();
            projectile.Direction = shot;
            projectile.Source = source;
        }

        private static bool IsOwnCollider(Collider collider)
        {
            return CharacterRagdoll.TryGetCharacterFromCollider(collider, out Character character) && character == Character.localCharacter;
        }

        private static bool SpawnBlocked(Vector3 start, Vector3 direction, float reach, out Vector3 point)
        {
            float nearest = float.MaxValue;
            point = Vector3.zero;
            foreach (RaycastHit hit in Physics.RaycastAll(start, direction, reach, HelperFunctions.AllPhysical, QueryTriggerInteraction.Ignore))
            {
                if (IsOwnCollider(hit.collider) || hit.distance <= 0f || hit.distance >= nearest)
                {
                    continue;
                }

                nearest = hit.distance;
                point = hit.point + hit.normal * Plugin.ExplosionOffset.Value;
            }

            if (nearest < float.MaxValue)
            {
                return true;
            }

            Vector3 end = start + direction * reach;
            foreach (Collider collider in Physics.OverlapSphere(end, Plugin.FlareRadius.Value, HelperFunctions.AllPhysical, QueryTriggerInteraction.Ignore))
            {
                if (!IsOwnCollider(collider))
                {
                    point = end;
                    return true;
                }
            }

            return false;
        }

        private static bool Prefix(Item ___item, Transform ___rayOrigin)
        {
            if (Plugin.VariantOf(___item) == 0)
            {
                return true;
            }

            Launch(___item, ___rayOrigin.position, MainCamera.instance.transform.forward);

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
                ShootPatch.Launch(item, muzzle, direction.sqrMagnitude > 0.01f ? direction.normalized : character.transform.forward);
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

    // Right mouse button (secondary use): a launcher detonates its flares in flight, a Jumper fires from the rear end of the barrel. The button is local-only input, so it works for the holder only.
    [HarmonyPatch(typeof(Item), nameof(Item.StartUseSecondary))]
    public static class DetonatePatch
    {
        private static void Postfix(Item __instance, bool __state)
        {
            int variant = Plugin.VariantOf(__instance);
            if (variant == 0 || !__instance.photonView.IsMine || !__state)
            {
                return;
            }

            if (Variants.IsJump(variant))
            {
                FireRear(__instance);
            }
            else if (Plugin.DetonateOnRightClick.Value)
            {
                FlareProjectile.DetonateAll(__instance);
            }
        }

        // StartUseSecondary does nothing when a use is already running, so __state tells whether this call began a new press.
        private static void Prefix(Item __instance, out bool __state)
        {
            __state = !__instance.isUsingPrimary && !__instance.isUsingSecondary;
        }

        private static void FireRear(Item item)
        {
            OptionableIntItemData uses = item.GetData<OptionableIntItemData>(DataEntryKey.ItemUses);
            if (uses.HasData && uses.Value == 0)
            {
                return;
            }

            ShootPatch.Launch(item, item.GetComponent<Action_RaycastSpawnSomething>().rayOrigin.position, MainCamera.instance.transform.forward, true);
            item.GetComponent<Action_ReduceUses>().RunAction();
        }
    }

    // Hides the filling circle the game shows while the right button is held.
    [HarmonyPatch(typeof(Item), nameof(Item.shouldShowCastProgress), MethodType.Getter)]
    public static class DetonateProgressPatch
    {
        private static void Postfix(Item __instance, ref bool __result)
        {
            if (__result && __instance.isUsingSecondary && Plugin.VariantOf(__instance) != 0 && (Plugin.DetonateOnRightClick.Value || Variants.IsJump(Plugin.VariantOf(__instance))))
            {
                __result = false;
            }
        }
    }

    // Lets the right button work even with an empty magazine.
    [HarmonyPatch(typeof(Item), nameof(Item.CanUseSecondary))]
    public static class DetonateAllowedPatch
    {
        private static void Postfix(Item __instance, ref bool __result)
        {
            if (Plugin.VariantOf(__instance) != 0 && (Plugin.DetonateOnRightClick.Value || Variants.IsJump(Plugin.VariantOf(__instance))))
            {
                __result = true;
            }
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

    // Gold variants never spend ammo (on modded clients).
    [HarmonyPatch(typeof(Action_ReduceUses), nameof(Action_ReduceUses.ReduceUsesRPC))]
    public static class InfiniteAmmoPatch
    {
        private static bool Prefix(Item ___item)
        {
            return !Variants.InfiniteAmmo(Plugin.VariantOf(___item));
        }
    }

    // Adds the mouse button hints to the vanilla item prompts on the right of the screen.
    [HarmonyPatch(typeof(GUIManager), nameof(GUIManager.UpdateItemPrompts))]
    public static class ItemPromptPatch
    {
        private static void Postfix(GUIManager __instance)
        {
            Item item = Character.localCharacter != null ? Character.localCharacter.data.currentItem : null;
            if (item == null || Character.localCharacter != Character.observedCharacter)
            {
                return;
            }

            int variant = Plugin.VariantOf(item);
            if (variant == 0)
            {
                return;
            }

            int language = (int)LocalizedText.CURRENT_LANGUAGE;
            bool jump = Variants.IsJump(variant);
            __instance.itemPromptMain.text = (jump ? LauncherText.FireForwardPrompts : LauncherText.FirePrompts)[language];
            __instance.itemPromptMain.gameObject.SetActive(true);
            if (jump || Plugin.DetonateOnRightClick.Value)
            {
                __instance.itemPromptSecondary.text = (jump ? LauncherText.FireBackPrompts : LauncherText.DetonatePrompts)[language];
                __instance.itemPromptSecondary.gameObject.SetActive(true);
            }
        }
    }
}
