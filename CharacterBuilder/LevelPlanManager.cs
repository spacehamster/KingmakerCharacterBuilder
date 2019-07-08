using Harmony12;
using Kingmaker;
using Kingmaker.Assets.UI.LevelUp;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Root;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Persistence.JsonUtility;
using Kingmaker.UI;
using Kingmaker.UI.LevelUp;
using Kingmaker.UI.LevelUp.Phase;
using Kingmaker.UI.ServiceWindow;
using Kingmaker.UnitLogic.Class.LevelUp;
using Kingmaker.View;
using Kingmaker.Visual.CharacterSystem;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace CharacterBuilder
{
    class LevelPlanManager
    {
        /*
         * Intercept CharacterBuildController and prevent the default 
         * LevelUpComplete from been called, also finalize any level plans
         */
        [HarmonyPatch(typeof(CharacterBuildController), "Commit")]
        static class CharacterBuildController_Commit_Patch
        {
            static bool Prefix(CharacterBuildController __instance)
            {
                try
                {
                    if (!Main.enabled) return true;
                    if (Main.settings.DisableRemovePlanOnChange)
                    {
                        Traverse.Create(__instance.LevelUpController).Field("m_PlanChanged").SetValue(false);
                    }
                    if (__instance.LevelUpController != CurrentLevelUpController)
                    {
                        Main.Log("CharacterBuildController.Commit, not creating level up plan");
                        return true;
                    }
                    /*
                     * as __instance.Unit does not have a proper view attached to it,
                     * we prevent the method from running as it will fail on 
                     * __instance.Unit.View.UpdateClassEquipment();
                     */
                    var planResult = CurrentLevelUpController.GetPlan();
                    CurrentLevelPlan.AddLevelPlan(planResult);
                    CurrentLevelUpController = null;

                    //LevelUpController.Commit
                    __instance.LevelUpController.Preview.Unit.Dispose();
                    LevelUpPreviewThread.Stop();
                    //CharacterBuildController.Commit
                    Traverse.Create(__instance).Property<LevelUpController>("LevelUpController").Value = null;
                    foreach (CharBPhase charBPhase in __instance.CharacterBuildPhaseStates)
                    {
                        charBPhase.Dispose();
                    }
                    if(Game.Instance.UI.ServiceWindow != null)
                    {
                        Game.Instance.UI.ServiceWindow.WindowTabs.Show(false);
                    }
                    Traverse.Create(__instance).Field("m_IsChargen").SetValue(false);
                    __instance.Show(false);
                    __instance.Unit = null;
                    Main.Log("LevelUpController.Commit, creating level up plan");
                }
                catch (Exception ex)
                {
                    Main.Error(ex);
                    return false;
                }
                return false;
            }
        }
        [HarmonyPatch(typeof(CharacterBuildController), "OnHide")]
        static class CharacterBuildController_OnHide_Patch
        {
            static void Postfix(CharacterBuildController __instance)
            {
                CurrentLevelUpController = null;
            }
        }
        [HarmonyPatch(typeof(CharacterBuildController), "OnShow")]
        static class CharacterBuildController_Onshow_Patch
        {
            static bool Prefix(CharacterBuildController __instance)
            {
                return true;
            }
        }
        public static LevelPlanHolder CurrentLevelPlan;
        public static LevelUpController CurrentLevelUpController = null;
        static void ShowDollRoom(UnitEntityData unit)
        {
            var dollRoom = Game.Instance.UI.Common.DollRoom;
            if (dollRoom != null)
            {
                CharGenDollRoom component = Game.Instance.UI.Common.DollRoom.GetComponent<CharGenDollRoom>();
                if (component)
                {
                    component.CreateDolls();
                }
                dollRoom.SetUnit(unit);
                dollRoom.Show(true);
            }
        }
        /*
         * Uses the CharacterBuildController to edit level plans
         * Refer MainMenu.StartChargen which sets up the state and then calls CharacterBuildController.HandleLevelUpStart
         * 
         */
        public static void EditLevelPlan(LevelPlanHolder levelPlanHolder, int level)
        {
            var unit = levelPlanHolder.CreateUnit(level);
            if(Main.settings.ShowDollRoom) ShowDollRoom(unit);
            CharacterBuildController characterBuildController = Game.Instance.UI.CharacterBuildController;
            var mode = level == 1 ? LevelUpState.CharBuildMode.CharGen : LevelUpState.CharBuildMode.LevelUp;
            CurrentLevelUpController = LevelUpController.Start(
                unit: unit.Descriptor, 
                instantCommit: false, 
                unitJson:null, 
                onSuccess: null, 
                mode: mode);
            CurrentLevelUpController.SelectPortrait(Game.Instance.BlueprintRoot.CharGen.Portraits[0]);
            CurrentLevelUpController.SelectGender(Gender.Male);
            CurrentLevelUpController.SelectRace(Game.Instance.BlueprintRoot.Progression.CharacterRaces[0]);
            CurrentLevelUpController.SelectAlignment(Kingmaker.Enums.Alignment.TrueNeutral);
            CurrentLevelUpController.SelectVoice(Game.Instance.BlueprintRoot.CharGen.MaleVoices[0]);
            CurrentLevelUpController.SelectName("LevelPlan");
            Traverse.Create(characterBuildController).Property<LevelUpController>("LevelUpController").Value = CurrentLevelUpController;
            Traverse.Create(characterBuildController).Field("Mode").SetValue(CurrentLevelUpController.State.Mode);
            Traverse.Create(characterBuildController).Field("Unit").SetValue(unit.Descriptor);
            characterBuildController.Show(true);
        }
        static void CreateLevelPlan()
        {
            var unit = Game.Instance.Player.MainCharacter.Value.Descriptor;
            var unitJson = UnitSerialization.Serialize(unit);
            CharacterBuildController characterBuildController = Game.Instance.UI.CharacterBuildController;

            CurrentLevelUpController = LevelUpController.Start(
                unit: unit,
                instantCommit: false,
                unitJson: unitJson,
                onSuccess: null,
                mode: LevelUpState.CharBuildMode.PreGen);
            Traverse.Create(characterBuildController).Property<LevelUpController>("LevelUpController").Value = CurrentLevelUpController;
            Traverse.Create(characterBuildController).Field("Mode").SetValue(CurrentLevelUpController.State.Mode);
            Traverse.Create(characterBuildController).Field("Unit").SetValue(unit);
            characterBuildController.Show(true);
        }
    }
}
