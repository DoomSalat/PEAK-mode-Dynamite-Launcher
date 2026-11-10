using Peak;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace DynamiteLauncher
{
    // Local-only visual: red camo on the gun body. Other players keep seeing the stock gun.
    [HarmonyPatch(typeof(Action_Antizooka), "OnEnable")]
    public static class CamoPatch
    {
        private const string BodyName = "Anti-Zooka";
        private const string BaseMap = "_BaseMap";
        private const string MainTexture = "_MainTex";
        private const string BaseColor = "_BaseColor";
        private const string LegacyColor = "_Color";
        private const string EmissionColor = "_EmissionColor";
        private const string Metallic = "_Metallic";
        private const string Smoothness = "_Smoothness";
        private const float CamoSmoothness = 0.1f;
        private const int FrameMaterial = 0;
        private const int BarrelMaterial = 1;

        // The barrel (submesh 1) samples only this window of the atlas UV space; the camo is stretched over exactly it.
        private const float WindowU = 0.043f;
        private const float WindowV = 0.503f;
        private const float WindowWidth = 0.899f;
        private const float WindowHeight = 0.099f;

        private static readonly Color SilverColor = new Color(0.85f, 0.87f, 0.92f, 1f);

        private static void ApplyCamo(Material material, Texture texture)
        {
            Vector2 scale = new Vector2(1f / WindowWidth, 1f / WindowHeight);
            Vector2 offset = new Vector2(-WindowU / WindowWidth, -WindowV / WindowHeight);
            material.SetTexture(BaseMap, texture);
            material.SetTexture(MainTexture, texture);
            material.SetTextureScale(BaseMap, scale);
            material.SetTextureOffset(BaseMap, offset);
            material.SetTextureScale(MainTexture, scale);
            material.SetTextureOffset(MainTexture, offset);
            material.SetColor(BaseColor, Color.white);
            material.SetColor(LegacyColor, Color.white);
            material.SetColor(EmissionColor, Color.black);
            material.SetFloat(Metallic, 0f);
            material.SetFloat(Smoothness, CamoSmoothness);
        }

        private static void Postfix(Item ___item)
        {
            Apply(___item);
        }

        // Called on spawn and whenever instance data arrives; the marker may come after OnEnable.
        public static void Apply(Item item)
        {
            int variant = Plugin.VariantOf(item);
            if (variant == 0 || item.GetComponent<CamoMarker>() != null)
            {
                return;
            }

            foreach (MeshRenderer renderer in item.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.name != BodyName)
                {
                    continue;
                }

                CamoMarker marker = item.gameObject.AddComponent<CamoMarker>();
                marker.Variant = variant;
                marker.Renderer = renderer;
                marker.Original = renderer.materials;
                Refresh(marker);
                return;
            }
        }

        // Rebuilds the materials from the originals, so config changes apply to launchers that already exist.
        public static void Refresh(CamoMarker marker)
        {
            if (marker.Applied != null)
            {
                foreach (Material material in marker.Applied)
                {
                    Object.Destroy(material);
                }
            }

            Material[] materials = new Material[marker.Original.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = new Material(marker.Original[i]);
            }

            if (Plugin.CamoEnabled.Value)
            {
                // The barrel starts from a copy of the frame material: the stock barrel material carries the space shader.
                materials[BarrelMaterial] = new Material(marker.Original[FrameMaterial]);
                ApplyCamo(materials[BarrelMaterial], Variants.Camo(marker.Variant));
            }

            if (Variants.IsSilver(marker.Variant) && Plugin.SilverTrim.Value)
            {
                materials[FrameMaterial].SetTexture(MainTexture, Plugin.SilverAtlas);
                materials[FrameMaterial].SetTexture(BaseMap, Plugin.SilverAtlas);
                materials[FrameMaterial].SetColor(BaseColor, SilverColor);
            }

            marker.Applied = materials;
            marker.Renderer.materials = materials;
        }

        public static void RefreshAll()
        {
            foreach (CamoMarker marker in CamoMarker.All)
            {
                Refresh(marker);
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
            int variant = data != null ? Variants.FromData(data) : Plugin.VariantOf(__instance);
            if (variant == 0)
            {
                return;
            }

            __result = LocalizedText.GetText("NAME_" + Variants.TextKey(variant));
        }
    }

    // Inventory slots take the icon from the stock Anti-Zooka prefab; swap in the variant icon when the slot data carries the marker.
    [HarmonyPatch(typeof(InventoryItemUI), nameof(InventoryItemUI.SetItem))]
    public static class InventoryIconPatch
    {
        public static Texture IconFor(ItemInstanceData data)
        {
            return Variants.Icon(Variants.FromData(data));
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
        public static readonly List<CamoMarker> All = new List<CamoMarker>();

        public int Variant { get; set; } // Set: filled by CamoPatch.Apply right after AddComponent
        public MeshRenderer Renderer { get; set; } // Set: filled by CamoPatch.Apply right after AddComponent
        public Material[] Original { get; set; } // Set: filled by CamoPatch.Apply right after AddComponent
        public Material[] Applied { get; set; } // Set: replaced on every CamoPatch.Refresh

        private void OnEnable()
        {
            All.Add(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
        }
    }
}
