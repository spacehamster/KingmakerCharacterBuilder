using Harmony12;
using Kingmaker;
using Kingmaker.UI;
using Kingmaker.UI.LevelUp;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Class.LevelUp;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace CharacterBuilder
{
    class CharacterBuilderGUI
    {
        enum UIState
        {
            Default,
            CreatingPlan,
            ManagingFiles,
            ManagingSettings,
            Debug
        };
        const float DefaultLabelWidth = 200f;
        const float DefaultSliderWidth = 300f;
        static UIState m_UIState = UIState.Default;
        public const string LevelPlanFolder = "mods/CharacterBuilder/LevelPlans";
        public static bool IsSelectingUnit = false;
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
        private static LevelPlanHolder CurrentLevelPlan
        {
            get => LevelPlanManager.CurrentLevelPlan;
            set { LevelPlanManager.CurrentLevelPlan = value;  }
        }
        private static LevelUpController CurrentLevelUpController
        {
            get => LevelPlanManager.CurrentLevelUpController;
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
                foreach (var unitRefrence in Game.Instance.Player.PartyCharacters.Concat(Game.Instance.Player.RemoteCompanions))
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
            if (GUILayout.Toggle(Main.settings.DefaultPointBuy25, " 25", GUILayout.ExpandWidth(false))) Main.settings.DefaultPointBuy25 = true;
            if (GUILayout.Toggle(!Main.settings.DefaultPointBuy25, " 20", GUILayout.ExpandWidth(false))) Main.settings.DefaultPointBuy25 = false;
            GUILayout.EndHorizontal();
            Main.settings.DisableRemovePlanOnChange = GUILayout.Toggle(Main.settings.DisableRemovePlanOnChange, " Disable Removing level plan on change");
            Main.settings.AutoSelectSkills = GUILayout.Toggle(Main.settings.AutoSelectSkills, " Auto select skills");
            Main.settings.ShowDollRoom = GUILayout.Toggle(Main.settings.ShowDollRoom, " Show DollRoom");
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
                    foreach (var unit in Game.Instance.Player.PartyCharacters.Concat(Game.Instance.Player.RemoteCompanions))
                    {
                        if (GUILayout.Button(unit.Value.CharacterName))
                        {
                            CurrentLevelPlan.unit = unit.Value.Descriptor;
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
        static void OnDebug()
        {
            var previewThread = Traverse.Create(typeof(LevelUpPreviewThread)).Field("s_Thread").GetValue<Thread>();
            var previewSource = Traverse.Create(typeof(LevelUpPreviewThread)).Field("s_Source").GetValue<JToken>();
            void DisplayUnit(UnitDescriptor unit)
            {
                GUILayout.Label($"UniqueId: {unit.Unit.UniqueId} InstanceId: {unit.Unit.View?.GetInstanceID().ToString() ?? "Null"}");
                GUILayout.Label($"Blueprint: {unit.Blueprint.name} {unit.Blueprint.AssetGuid}");
                GUILayout.Label($"CharacterLevel: {unit.Progression.CharacterLevel}");
                GUILayout.Label($"Race: {unit.Progression.Race?.Name ?? "Null"}");
                var levelPlan = Traverse.Create(unit.Progression).Field("m_LevelPlans").GetValue<List<LevelPlanData>>();
                if (levelPlan == null) GUILayout.Label($"LevelPlan: null");
                else
                {
                    var levels = string.Join(", ", levelPlan.Select(p => p.Level));
                    GUILayout.Label($"LevelPlan: [{levels}]");
                }
            }
            var controller = Game.Instance.UI.CharacterBuildController?.LevelUpController;
            GUILayout.Label(string.Format("Controller State {0}, CBC.LUC {1}, AreEqual {2}, PreviewThread {3}, PreviewSource {4}",
                CurrentLevelUpController != null,
                Game.Instance.UI.CharacterBuildController?.LevelUpController != null,
                Game.Instance.UI.CharacterBuildController?.LevelUpController == CurrentLevelUpController,
                previewThread != null,
                previewSource != null));
            if (controller != null)
            {
                //Controller
                GUILayout.Label("Controller", Util.BoldLabelStyle);
                GUILayout.Label($"Autocommit: {controller.AutoCommit}");
                GUILayout.Label($"Doll: {controller.Doll != null}");
                GUILayout.Label($"HasNextLevelPlan: {controller.HasNextLevelPlan}");
                GUILayout.Label($"IsAutoLevelup: {controller.IsAutoLevelup}");

                //LevelUpState
                GUILayout.Label("LevelUpState", Util.BoldLabelStyle);
                var state = controller.State;
                GUILayout.Label($"AlignmentRestriction: {state.AlignmentRestriction.GetRestriction()}");
                GUILayout.Label($"AttributePoints: {state.AttributePoints}");
                GUILayout.Label($"ExtraSkillPoints: {state.ExtraSkillPoints}");
                GUILayout.Label($"IntelligenceSkillPoints: {state.IntelligenceSkillPoints}");
                GUILayout.Label($"IsEmployee: {state.IsEmployee}");
                GUILayout.Label($"IsFirstLevel: {state.IsFirstLevel}");
                GUILayout.Label($"IsLoreCompanion: {state.IsLoreCompanion}");
                GUILayout.Label($"Mode: {state.Mode}");
                GUILayout.Label($"NextClassLevel: {state.NextClassLevel}");
                GUILayout.Label($"NextLevel: {state.NextLevel}");
                GUILayout.Label($"StatsDistribution: {state.StatsDistribution.TotalPoints} Total {state.StatsDistribution.Points} Current");
                GUILayout.Label($"Does state.Unit == controller.Unit {state.Unit == controller.Unit}"); //False
                GUILayout.Label($"Does state.Unit == controller.Preview {state.Unit == controller.Preview}"); //True
                                                                                                              //Controller Actions
                GUILayout.Label("ControllerActions", Util.BoldLabelStyle);
                foreach (var action in controller.LevelUpActions)
                {
                    GUILayout.Label(Util.MakeActionReadable(action));
                }
                //Unit. Charget = StartGame_Player_Unit
                GUILayout.Label("Unit", Util.BoldLabelStyle);
                DisplayUnit(controller.Unit);
                //Preview
                GUILayout.Label("Preview", Util.BoldLabelStyle);
                DisplayUnit(controller.Preview);

            }
            GUILayout.Label("DollRoom", Util.BoldLabelStyle);
            var dollRoom = Game.Instance.UI.Common.DollRoom;
            if (dollRoom == null)
            {
                GUILayout.Label("DollRoom null");
            } else
            {
                var dollroomUnit = dollRoom.Unit;
                GUILayout.Label($"IsVisible: {dollRoom.IsVisible}");
                var dollroomAvatar = dollRoom.GetAvatar();
                GUILayout.Label($"Avatar {dollroomAvatar}");
                if (dollroomUnit != null) DisplayUnit(dollroomUnit.Descriptor);
                else GUILayout.Label("DollRoom Unit is null");
            }
            GUILayout.Label("MainMenu.CurrentChargen", Util.BoldLabelStyle);
            var mainMenu = Game.Instance.UI.MainMenu;
            var chargen = Traverse.Create(mainMenu).Field("m_ChargenUnit").GetValue<ChargenUnit>();
            var defaultChargen = Traverse.Create(mainMenu).Field("m_DefaultChargenUnit").GetValue<ChargenUnit>();
            if (chargen != null) DisplayUnit(chargen.Unit.Descriptor);
            else GUILayout.Label("CurrentChargen is null");
            GUILayout.Label("MainMenu.DefaultChargen", Util.BoldLabelStyle);
            DisplayUnit(defaultChargen.Unit.Descriptor);
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
            if (GUILayout.Button("Debug"))
            {
                m_UIState = m_UIState == UIState.ManagingSettings ? UIState.Default : UIState.Debug;
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
            if (m_UIState == UIState.Debug)
            {
                OnDebug();
                return;
            }
            OnDefaultGUI();
        }
    }
}
