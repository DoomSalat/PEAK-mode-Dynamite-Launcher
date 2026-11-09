using Peak;
using HarmonyLib;
using UnityEngine;

namespace PeakRedFlareLauncher
{
    // Local-only visual: red camo on the gun body. Other players keep seeing the stock gun.
    [HarmonyPatch(typeof(Action_Antizooka), "OnEnable")]
    public static class CamoPatch
    {
        private const string BodyName = "Anti-Zooka";
        private static readonly Color SilverColor = new Color(0.85f, 0.87f, 0.92f, 1f);

        // The barrel (submesh 1) samples only this window of the atlas UV space; the camo is stretched over exactly it.
        private const float WindowU = 0.043f;
        private const float WindowV = 0.503f;
        private const float WindowWidth = 0.899f;
        private const float WindowHeight = 0.099f;

        private static void ApplyCamo(Material material)
        {
            Vector2 scale = new Vector2(1f / WindowWidth, 1f / WindowHeight);
            Vector2 offset = new Vector2(-WindowU / WindowWidth, -WindowV / WindowHeight);
            material.SetTexture("_BaseMap", Plugin.Camo);
            material.SetTexture("_MainTex", Plugin.Camo);
            material.SetTextureScale("_BaseMap", scale);
            material.SetTextureOffset("_BaseMap", offset);
            material.SetTextureScale("_MainTex", scale);
            material.SetTextureOffset("_MainTex", offset);
            material.SetColor("_BaseColor", Color.white);
            material.SetColor("_Color", Color.white);
            material.SetColor("_EmissionColor", Color.black);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.1f);
        }

        private static void Postfix(Action_Antizooka __instance)
        {
            Apply(__instance.item);
        }

        // Called on spawn and whenever instance data arrives; the marker may come after OnEnable.
        public static void Apply(Item item)
        {
            int variant = Plugin.VariantOf(item);
            if (variant == 0 || item.GetComponent<CamoMarker>() != null)
            {
                return;
            }

            item.gameObject.AddComponent<CamoMarker>();
            foreach (MeshRenderer renderer in item.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.name != BodyName)
                {
                    continue;
                }

                Material[] materials = renderer.materials;
                if (Plugin.CamoEnabled.Value)
                {
                    Material barrel = new Material(materials[0]);
                    ApplyCamo(barrel);
                    materials[1] = barrel;
                }

                if (variant == Plugin.SilverVariant && Plugin.SilverTrim.Value)
                {
                    materials[0].SetTexture("_MainTex", Plugin.SilverAtlas);
                    materials[0].SetTexture("_BaseMap", Plugin.SilverAtlas);
                    materials[0].SetColor("_BaseColor", SilverColor);
                }

                renderer.materials = materials;
            }
        }
    }

    [HarmonyPatch(typeof(Item), nameof(Item.SetItemInstanceDataRPC))]
    public static class VariantDataPatch
    {
        private static void Postfix(Item __instance)
        {
            if (__instance.GetComponent<Action_Antizooka>() != null)
            {
                CamoPatch.Apply(__instance);
            }
        }
    }

    // Modded clients see the launcher name; vanilla clients keep the stock Anti-Zooka name.
    [HarmonyPatch(typeof(Item), nameof(Item.GetItemName))]
    public static class ItemNamePatch
    {
        private static void Postfix(Item __instance, ItemInstanceData data, ref string __result)
        {
            int variant = data != null
                ? (data.TryGetDataEntry<IntItemData>(Plugin.VariantKey, out IntItemData value) ? value.Value : 0)
                : Plugin.VariantOf(__instance);
            if (variant == 0)
            {
                return;
            }

            string key = variant == Plugin.GoldVariant ? "DYNAMITE LAUNCHER GOLD" : "DYNAMITE LAUNCHER";
            __result = LocalizedText.GetText("NAME_" + key);
        }
    }

    // Inventory slots take the icon from the stock Anti-Zooka prefab; swap in the variant icon when the slot data carries the marker.
    [HarmonyPatch(typeof(InventoryItemUI), nameof(InventoryItemUI.SetItem))]
    public static class InventoryIconPatch
    {
        public static Texture IconFor(ItemInstanceData data)
        {
            if (data == null || !data.TryGetDataEntry<IntItemData>(Plugin.VariantKey, out IntItemData value))
            {
                return null;
            }

            return value.Value == Plugin.GoldVariant ? Plugin.GoldIcon : value.Value == Plugin.SilverVariant ? Plugin.SilverIcon : null;
        }

        private static void Postfix(InventoryItemUI __instance, ItemSlot slot)
        {
            if (slot == null || __instance.isBackpack || slot.IsEmpty())
            {
                return;
            }

            Texture icon = IconFor(slot.data);
            if (icon != null)
            {
                __instance.icon.texture = icon;
            }
        }
    }

    [HarmonyPatch(typeof(BackpackWheelSlice), "SetItemIcon")]
    public static class BackpackIconPatch
    {
        private static void Postfix(BackpackWheelSlice __instance, Item iconHolder, ItemInstanceData itemInstanceData)
        {
            Texture icon = iconHolder != null ? InventoryIconPatch.IconFor(itemInstanceData) : null;
            if (icon != null)
            {
                __instance.image.texture = icon;
            }
        }
    }

    public class CamoMarker : MonoBehaviour
    {
    }
}
