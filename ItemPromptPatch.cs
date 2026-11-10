using HarmonyLib;

namespace DynamiteLauncher
{
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
            if (SecondaryUsePatch.UsesRightButton(variant))
            {
                __instance.itemPromptSecondary.text = (jump ? LauncherText.FireBackPrompts : LauncherText.DetonatePrompts)[language];
                __instance.itemPromptSecondary.gameObject.SetActive(true);
            }
        }
    }
}
