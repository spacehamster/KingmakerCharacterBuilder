using Harmony12;
using Kingmaker;
using Kingmaker.Assets.UI.LevelUp;
using Kingmaker.Blueprints;
using Kingmaker.EntitySystem.Persistence.JsonUtility;
using Kingmaker.UI;
using Kingmaker.UI.LevelUp;
using Kingmaker.UI.LevelUp.Phase;
using Kingmaker.UnitLogic.Class.LevelUp;
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
                        Main.DebugLog("CharacterBuildController.Commit, not creating level up plan");
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
                    Main.DebugLog("LevelUpController.Commit, creating level up plan");
                }
                catch (Exception ex)
                {
                    Main.DebugError(ex);
                    return true;
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
        enum UIState
        {
            Default,
            CreatingPlan,
            ManagingFiles,
            ManagingSettings
        };
        const float DefaultLabelWidth = 200f;
        const float DefaultSliderWidth = 300f;
        public const string LevelPlanFolder = "mods/CharacterBuilder/LevelPlans";
        public static LevelPlanHolder CurrentLevelPlan;
        static UIState m_UIState = UIState.Default;
        public static bool IsSelectingUnit = false;
        public static LevelUpController CurrentLevelUpController = null;
        private static string[] m_LevelPlanFiles;
        public static string[] LevelPlanFiles
        {
            get
            {
                if (m_LevelPlanFiles == null)
                {
                    Directory.CreateDirectory(LevelPlanFolder);
                    m_LevelPlanFiles = Directory.GetFiles(LevelPlanFolder)
                        .Where(f => f.EndsWith(".json"))
                        .ToArray();
                }
                return m_LevelPlanFiles;
            }
            set
            {
                m_LevelPlanFiles = value;
            }
        }
        public static void EditLevelPlan(LevelPlanHolder levelPlanHolder, int level)
        {
            var unit = levelPlanHolder.CreateUnit(level);
            CharacterBuildController characterBuildController = Game.Instance.UI.CharacterBuildController;
            CurrentLevelUpController = LevelUpController.Start(unit, false, null, false, null);
            CurrentLevelUpController.SelectPortrait(Game.Instance.BlueprintRoot.CharGen.Portraits[0]);
            CurrentLevelUpController.SelectGender(Gender.Male);
            CurrentLevelUpController.SelectRace(Game.Instance.BlueprintRoot.Progression.CharacterRaces[0]);
            CurrentLevelUpController.SelectAlignment(Kingmaker.Enums.Alignment.TrueNeutral);
            CurrentLevelUpController.SelectVoice(Game.Instance.BlueprintRoot.CharGen.MaleVoices[0]);
            CurrentLevelUpController.SelectName("LevelPlan");
            Traverse.Create(characterBuildController).Property<LevelUpController>("LevelUpController").Value = CurrentLevelUpController;
            Traverse.Create(characterBuildController).Field("m_IsChargen").SetValue(CurrentLevelUpController.State.IsCharGen);
            Traverse.Create(characterBuildController).Field("Unit").SetValue(unit);
            characterBuildController.Show(true);
        }
        static void CreateLevelPlan()
        {
            var unit = Game.Instance.Player.MainCharacter.Value.Descriptor;
            var unitJson = UnitSerialization.Serialize(unit);
            CharacterBuildController characterBuildController = Game.Instance.UI.CharacterBuildController;

            CurrentLevelUpController = LevelUpController.Start(unit, false, unitJson, false, null);
            Traverse.Create(characterBuildController).Property<LevelUpController>("LevelUpController").Value = CurrentLevelUpController;
            Traverse.Create(characterBuildController).Field("m_IsChargen").SetValue(CurrentLevelUpController.State.IsCharGen);
            Traverse.Create(characterBuildController).Field("Unit").SetValue(unit);
            characterBuildController.Show(true);
        }
        static void OnCreatingPlan()
        {
            GUILayout.Label("Create New Plan");
            if (GUILayout.Button("Blank", GUILayout.Width(DefaultLabelWidth)))
            {
                CurrentLevelPlan = new LevelPlanHolder();
                m_UIState = UIState.Default;
            }
            if (Game.Instance.Player.MainCharacter.Value != null)
            {
                GUILayout.Label("Copy From Unit");
                foreach (var unitRefrence in Game.Instance.Player.PartyCharacters.Concat(Game.Instance.Player.PartyCharacters))
                {
                    var unit = unitRefrence.Value;
                    if (GUILayout.Button(unit.CharacterName, GUILayout.Width(DefaultLabelWidth)))
                    {
                        CurrentLevelPlan = new LevelPlanHolder(unit.Descriptor);
                        CurrentLevelPlan.Name = unit.CharacterName;
                        m_UIState = UIState.Default;
                    }
                }
            }
            GUILayout.Label("Copy From Class");
            foreach (var characterClass in Game.Instance.BlueprintRoot.Progression.CharacterClasses)
            {
                if (characterClass.DefaultBuild == null) continue;
                if (GUILayout.Button(characterClass.Name, GUILayout.Width(DefaultLabelWidth)))
                {
                    CurrentLevelPlan = new LevelPlanHolder(characterClass);
                    CurrentLevelPlan.Name = characterClass.Name;
                    m_UIState = UIState.Default;
                }
            }
        }
        static void OnManagingFiles()
        {
            if (LevelPlanFiles.Length == 0)
            {
                GUILayout.Label("No level plans found");
            }
            foreach (var filepath in LevelPlanFiles)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(Path.GetFileNameWithoutExtension(filepath));
                if (GUILayout.Button("Open", GUILayout.ExpandWidth(false)))
                {
                    CurrentLevelPlan = Util.LoadLevelingPlan(filepath);
                    m_UIState = UIState.Default;
                }
                GUILayout.EndHorizontal();
            }
        }
        static void OnManagingSettings()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Default Point Buy ", GUILayout.ExpandWidth(false));
            if(GUILayout.Toggle(Main.settings.DefaultPointBuy25, " 25", GUILayout.ExpandWidth(false))) Main.settings.DefaultPointBuy25 = true;
            if(GUILayout.Toggle(!Main.settings.DefaultPointBuy25, " 20", GUILayout.ExpandWidth(false))) Main.settings.DefaultPointBuy25 = false;
            GUILayout.EndHorizontal();
            Main.settings.DisableRemovePlanOnChange = GUILayout.Toggle(Main.settings.DisableRemovePlanOnChange, " Disable Removing level plan on change");
            Main.settings.AutoSelectSkills = GUILayout.Toggle(Main.settings.AutoSelectSkills, " Auto select skills");
        }
        static void OnDefaultGUI()
        {
            if (CurrentLevelPlan == null)
            {
                GUILayout.Label("No plan open");
            }
            else
            {
                GUILayout.BeginHorizontal();
                if (CurrentLevelPlan != null)
                {
                    CurrentLevelPlan.Name = GUILayout.TextField(CurrentLevelPlan.Name);
                    if (GUILayout.Button("Select Unit"))
                    {
                        IsSelectingUnit = !IsSelectingUnit;
                    }
                    var applyButtonStyle = CurrentLevelPlan.unit == null ? Util.DisabledButtonStyle : GUI.skin.button;
                    if (GUILayout.Button("Apply Plan", applyButtonStyle))
                    {
                        if (CurrentLevelPlan.unit != null)
                        {
                            CurrentLevelPlan.ApplyLevelPlan(CurrentLevelPlan.unit);
                            CurrentLevelPlan.IsApplied = true;
                        }
                    }
                    if (GUILayout.Button("Save Plan"))
                    {
                        Util.SaveLevelingPlan(CurrentLevelPlan);
                        CurrentLevelPlan.IsDirty = false;
                    }
                }
                GUILayout.EndHorizontal();
                if (IsSelectingUnit)
                {
                    foreach (var unit in Game.Instance.Player.ControllableCharacters)
                    {
                        if (unit.Descriptor.IsPet) continue;
                        if (GUILayout.Button(unit.CharacterName))
                        {
                            CurrentLevelPlan.unit = unit.Descriptor;
                            CurrentLevelPlan.IsApplied = false;
                            IsSelectingUnit = false;
                        }
                    }
                }
                GUILayout.BeginHorizontal();
                GUILayout.Label(CurrentLevelPlan.unit == null ? "No unit selected" : $"Selected Unit: {CurrentLevelPlan.unit.CharacterName}");
                if (CurrentLevelPlan.unit != null && !CurrentLevelPlan.IsApplied) GUILayout.Label("Level Plan has not been applied to unit");
                if (CurrentLevelPlan.IsDirty) GUILayout.Label("Level Plan has not been saved");
                GUILayout.EndHorizontal();
                CurrentLevelPlan.ShowLevelPlan();
            }
        }
        public static void OnGUI()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("New Level Plan"))
            {
                m_UIState = m_UIState == UIState.CreatingPlan ? UIState.Default : UIState.CreatingPlan;
            }
            if (GUILayout.Button("Manage Level Plans"))
            {
                LevelPlanFiles = null;
                m_UIState = m_UIState == UIState.ManagingFiles ? UIState.Default : UIState.ManagingFiles;
            }
            if (GUILayout.Button("Settings"))
            {
                m_UIState = m_UIState == UIState.ManagingSettings ? UIState.Default : UIState.ManagingSettings;
            }
            GUILayout.EndHorizontal();
            if (m_UIState == UIState.CreatingPlan)
            {
                OnCreatingPlan();
                return;
            }
            if (m_UIState == UIState.ManagingFiles)
            {
                OnManagingFiles();
                return;
            }
            if (m_UIState == UIState.ManagingSettings)
            {
                OnManagingSettings();
                return;
            }
            var previewThread = Traverse.Create(typeof(LevelUpPreviewThread)).Field("s_Thread").GetValue<Thread>();
            var previewSource = Traverse.Create(typeof(LevelUpPreviewThread)).Field("s_Source").GetValue<JToken>();
            GUILayout.Label(string.Format("Controller State {0}, CBC.LUC {1}, AreEqual {2}, PreviewThread {3}, PreviewSource {4}",
                CurrentLevelUpController != null,
                Game.Instance.UI.CharacterBuildController.LevelUpController != null,
                Game.Instance.UI.CharacterBuildController.LevelUpController == CurrentLevelUpController,
                previewThread != null,
                previewSource != null));
            OnDefaultGUI();
        }
    }
}
