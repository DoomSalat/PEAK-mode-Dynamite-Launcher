using UnityEngine;

namespace DynamiteLauncher
{
    // Lookup helpers for the four launcher variants (see Plugin.*Variant).
    public static class Variants
    {
        public static bool IsJump(int variant)
        {
            return variant == Plugin.JumpSilverVariant || variant == Plugin.JumpGoldVariant;
        }

        public static bool IsGold(int variant)
        {
            return variant == Plugin.GoldVariant || variant == Plugin.JumpGoldVariant;
        }

        public static bool IsSilver(int variant)
        {
            return variant == Plugin.SilverVariant || variant == Plugin.JumpSilverVariant;
        }

        public static string PrefabName(int variant)
        {
            switch (variant)
            {
                case Plugin.SilverVariant: return Plugin.SilverPrefabName;
                case Plugin.GoldVariant: return Plugin.GoldPrefabName;
                case Plugin.JumpSilverVariant: return Plugin.JumpSilverPrefabName;
                case Plugin.JumpGoldVariant: return Plugin.JumpGoldPrefabName;
                default: return null;
            }
        }

        public static int FromPrefabName(string name)
        {
            for (int variant = Plugin.SilverVariant; variant <= Plugin.JumpGoldVariant; variant++)
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
                default: return "DYNAMITE JUMPER GOLD";
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
                default: return null;
            }
        }

        public static int Ammo(int variant)
        {
            return IsJump(variant) ? Plugin.JumpSilverAmmo.Value : Plugin.SilverAmmo.Value;
        }

        public static bool InfiniteAmmo(int variant)
        {
            return variant == Plugin.GoldVariant ? Plugin.GoldInfiniteAmmo.Value : variant == Plugin.JumpGoldVariant && Plugin.JumpGoldInfiniteAmmo.Value;
        }
    }
}
