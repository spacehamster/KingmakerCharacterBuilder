using Harmony12;
using Kingmaker.Assets.UI.LevelUp;
using Kingmaker.UI;
using Kingmaker.UI.LevelUp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CharacterBuilder
{
    class HideDisableLevelPlanDialogPatches
    {
        [HarmonyPatch(typeof(CharBuildSelectorItem), "ShowDialogWindow")]
        static class CharBuildSelectorItem_ShowDialogWindow_Patch
        {
            static bool Prefix(CharBuildSelectorItem __instance)
            {
                try
                {
                    if (!Main.enabled) return true;
                    if (Main.settings.DisableRemovePlanOnChange)
                    {
                        Traverse.Create(__instance).Method("OnSelectNewItem").GetValue(new object[] { DialogMessageBox.BoxButton.Yes });
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Main.DebugError(ex);
                }
                return true;
            }
        }
        [HarmonyPatch(typeof(CharBAbilityScoresAllocator), "ShowDialogWindow")]
        static class CharBAbilityScoresAllocator_ShowDialogWindow_Patch
        {
            static bool Prefix(CharBAbilityScoresAllocator __instance)
            {
                try
                {
                    if (!Main.enabled) return true;
                    if (Main.settings.DisableRemovePlanOnChange)
                    {
                        Traverse.Create(__instance).Method("OnSelectNewItem").GetValue(new object[] { DialogMessageBox.BoxButton.Yes });
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Main.DebugError(ex);
                }
                return true;
            }
        }
        [HarmonyPatch(typeof(CharBSkillsAllocator), "ShowDialogWindow")]
        static class CharBSkillsAllocator_ShowDialogWindow_Patch
        {
            static bool Prefix(CharBSkillsAllocator __instance)
            {
                try
                {
                    if (!Main.enabled) return true;
                    if (Main.settings.DisableRemovePlanOnChange)
                    {
                        Traverse.Create(__instance).Method("OnSelectNewItem").GetValue(new object[] { DialogMessageBox.BoxButton.Yes });
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Main.DebugError(ex);
                }
                return true;
            }
        }
    }
}
