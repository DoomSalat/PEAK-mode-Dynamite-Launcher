using System.Reflection;
using HarmonyLib;
using Peak;
using UnityEngine;

namespace DynamiteLauncher
{
    // Right mouse button (secondary use): a launcher detonates its flares in flight, a Jumper fires from the rear end of the barrel after the button has been held as long as the primary charge takes (scaled by ChargeSpeedMultiplier). The button is local-only input, so it works for the holder only.
    [HarmonyPatch(typeof(Item), nameof(Item.StartUseSecondary))]
    public static class SecondaryUsePatch
    {
        // Jumpers fire backwards with it, guided launchers always detonate with it, plain launchers only if DetonateOnRightClick is on.
        public static bool UsesRightButton(int variant)
        {
            return variant != 0 && (Plugin.DetonateOnRightClick.Value || Variants.IsJump(variant) || Variants.IsGuided(variant));
        }

        private static void Postfix(Item __instance, bool __state)
        {
            int variant = Plugin.VariantOf(__instance);
            if (variant == 0 || !__instance.photonView.IsMine || !__state)
            {
                return;
            }

            if (Variants.IsJump(variant))
            {
                JumpHold.Begin(__instance);
            }
            else if (UsesRightButton(variant))
            {
                FlareProjectile.DetonateAll(__instance);
            }
        }

        // StartUseSecondary does nothing when a use is already running, so __state tells whether this call began a new press.
        private static void Prefix(Item __instance, out bool __state)
        {
            __state = !__instance.isUsingPrimary && !__instance.isUsingSecondary;
        }

        internal static void FireRear(Item item)
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

    // Hold-to-fire timer of the Jumper's rear shot; only the local holder uses the right button, so a single slot is enough.
    public static class JumpHold // Static: one local right-button press at a time, no scene object needed
    {
        private static readonly MethodInfo PlayChargeEffect = AccessTools.Method(typeof(Action_Antizooka), "PlayChargeFX"); // Static: reflection lookup done once
        private static readonly MethodInfo StopChargeEffect = AccessTools.Method(typeof(Action_Antizooka), "StopChargeFX"); // Static: reflection lookup done once

        private static Item _item;
        private static float _startTime;
        private static bool _fired;

        public static void Begin(Item item)
        {
            _item = item;
            _startTime = Time.time;
            _fired = false;
            PlayChargeEffect.Invoke(item.GetComponent<Action_Antizooka>(), null);
        }

        public static void Cancel(Item item)
        {
            if (_item == item)
            {
                _item = null;
                StopChargeEffect.Invoke(item.GetComponent<Action_Antizooka>(), null);
            }
        }

        public static bool IsCharging(Item item)
        {
            return _item == item && !_fired && item.isUsingSecondary;
        }

        // Same time as the primary charge, so the global ChargeSpeedMultiplier setting speeds it up too.
        public static float Fraction(Item item)
        {
            float duration = item.usingTimePrimary / Plugin.ChargeSpeedMultiplier.Value;
            return duration <= 0f ? 1f : Mathf.Clamp01((Time.time - _startTime) / duration);
        }

        public static void Tick(Item item)
        {
            if (IsCharging(item) && Fraction(item) >= 1f)
            {
                _fired = true;
                SecondaryUsePatch.FireRear(item);
            }
        }
    }

    [HarmonyPatch(typeof(Item), nameof(Item.ContinueUseSecondary))]
    public static class JumpHoldTickPatch
    {
        private static void Postfix(Item __instance)
        {
            JumpHold.Tick(__instance);
        }
    }

    [HarmonyPatch(typeof(Item), nameof(Item.CancelUseSecondary))]
    public static class JumpHoldCancelPatch
    {
        private static void Postfix(Item __instance)
        {
            JumpHold.Cancel(__instance);
        }
    }

    // Shows the game's filling circle while the Jumper's right button is held.
    [HarmonyPatch(typeof(Item), nameof(Item.progress), MethodType.Getter)]
    public static class JumpHoldProgressPatch
    {
        private static void Postfix(Item __instance, ref float __result)
        {
            if (JumpHold.IsCharging(__instance))
            {
                __result = JumpHold.Fraction(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(Item), nameof(Item.shouldShowCastProgress), MethodType.Getter)]
    public static class JumpHoldShowPatch
    {
        private static void Postfix(Item __instance, ref bool __result)
        {
            if (JumpHold.IsCharging(__instance))
            {
                __result = true;
            }
        }
    }

    // Hides the filling circle the game shows while the right button is held.
    [HarmonyPatch(typeof(Item), nameof(Item.shouldShowCastProgress), MethodType.Getter)]
    public static class DetonateProgressPatch
    {
        private static void Postfix(Item __instance, ref bool __result)
        {
            int variant = Plugin.VariantOf(__instance);
            if (__result && __instance.isUsingSecondary && SecondaryUsePatch.UsesRightButton(variant) && !Variants.IsJump(variant))
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
            if (SecondaryUsePatch.UsesRightButton(Plugin.VariantOf(__instance)))
            {
                __result = true;
            }
        }
    }
}
