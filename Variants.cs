using UnityEngine;

namespace DynamiteLauncher
{
    // Lookup helpers for the six launcher variants (see Plugin.*Variant).
    public static class Variants
    {
        public static bool IsJump(int variant)
        {
            return variant == Plugin.JumpSilverVariant || variant == Plugin.JumpGoldVariant;
        }

        public static int FromData(ItemInstanceData data)
        {
            return data != null && data.TryGetDataEntry<IntItemData>(Plugin.VariantKey, out IntItemData value) ? value.Value : 0;
        }

        public static Texture2D Camo(int variant)
        {
            if (IsJump(variant))
            {
                return Plugin.CamoGreen;
            }

            return IsGuided(variant) ? Plugin.CamoBlue : Plugin.Camo;
        }

        public static bool IsGuided(int variant)
        {
            return variant == Plugin.GuidedSilverVariant || variant == Plugin.GuidedGoldVariant;
        }

        public static bool IsGold(int variant)
        {
            return variant == Plugin.GoldVariant || variant == Plugin.JumpGoldVariant || variant == Plugin.GuidedGoldVariant;
        }

        public static bool IsSilver(int variant)
        {
            return variant == Plugin.SilverVariant || variant == Plugin.JumpSilverVariant || variant == Plugin.GuidedSilverVariant;
        }

        public static string PrefabName(int variant)
        {
            switch (variant)
            {
                case Plugin.SilverVariant: return Plugin.SilverPrefabName;
                case Plugin.GoldVariant: return Plugin.GoldPrefabName;
                case Plugin.JumpSilverVariant: return Plugin.JumpSilverPrefabName;
                case Plugin.JumpGoldVariant: return Plugin.JumpGoldPrefabName;
                case Plugin.GuidedSilverVariant: return Plugin.GuidedSilverPrefabName;
                case Plugin.GuidedGoldVariant: return Plugin.GuidedGoldPrefabName;
                default: return null;
            }
        }

        public static int FromPrefabName(string name)
        {
            for (int variant = Plugin.SilverVariant; variant <= Plugin.GuidedGoldVariant; variant++)
            {
                if (PrefabName(variant) == name)
                {
                    return variant;
                }
            }

            return 0;
        }

        public static string TextKey(int variant)
        {
            switch (variant)
            {
                case Plugin.SilverVariant: return "DYNAMITE LAUNCHER";
                case Plugin.GoldVariant: return "DYNAMITE LAUNCHER GOLD";
                case Plugin.JumpSilverVariant: return "DYNAMITE JUMPER";
                case Plugin.JumpGoldVariant: return "DYNAMITE JUMPER GOLD";
                case Plugin.GuidedSilverVariant: return "GUIDED DYNAMITE LAUNCHER";
                default: return "GUIDED DYNAMITE LAUNCHER GOLD";
            }
        }

        public static Texture2D Icon(int variant)
        {
            switch (variant)
            {
                case Plugin.SilverVariant: return Plugin.SilverIcon;
                case Plugin.GoldVariant: return Plugin.GoldIcon;
                case Plugin.JumpSilverVariant: return Plugin.JumpSilverIcon;
                case Plugin.JumpGoldVariant: return Plugin.JumpGoldIcon;
                case Plugin.GuidedSilverVariant: return Plugin.GuidedSilverIcon;
                case Plugin.GuidedGoldVariant: return Plugin.GuidedGoldIcon;
                default: return null;
            }
        }

        public static GameObject Prefab(int variant)
        {
            switch (variant)
            {
                case Plugin.SilverVariant: return Plugin.SilverPrefab;
                case Plugin.GoldVariant: return Plugin.GoldPrefab;
                case Plugin.JumpSilverVariant: return Plugin.JumpSilverPrefab;
                case Plugin.JumpGoldVariant: return Plugin.JumpGoldPrefab;
                case Plugin.GuidedSilverVariant: return Plugin.GuidedSilverPrefab;
                case Plugin.GuidedGoldVariant: return Plugin.GuidedGoldPrefab;
                default: return null;
            }
        }

        public static int Ammo(int variant)
        {
            if (IsJump(variant))
            {
                return Plugin.JumpSilverAmmo.Value;
            }

            return IsGuided(variant) ? Plugin.GuidedSilverAmmo.Value : Plugin.SilverAmmo.Value;
        }

        public static bool InfiniteAmmo(int variant)
        {
            switch (variant)
            {
                case Plugin.GoldVariant: return Plugin.GoldInfiniteAmmo.Value;
                case Plugin.JumpGoldVariant: return Plugin.JumpGoldInfiniteAmmo.Value;
                case Plugin.GuidedGoldVariant: return Plugin.GuidedGoldInfiniteAmmo.Value;
                default: return false;
            }
        }
    }
}
