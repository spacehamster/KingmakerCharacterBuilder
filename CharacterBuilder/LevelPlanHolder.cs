using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Root;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Class.LevelUp;
using Kingmaker.Utility;
using UnityEngine;

namespace CharacterBuilder
{
    public class LevelPlanHolder
    {
        public LevelPlanData[] LevelPlanData = new LevelPlanData[20];
        bool[] open = new bool[20];
        bool[] ValidLevels = new bool[20];
        string[] Message = new string[20];
        public bool IsDirty = false;
        public bool IsApplied = false;
        public string Name = "LevelPlan";
        public UnitDescriptor unit;
        public LevelPlanHolder()
        {

        }
        /*
         * Create a level plan from BlueprintCharacterClass' defaultBuild
         * Refer AddClassLevels.LevelUp and LevelController.ApplyStatsDistributionPreset
         * LevelUpController.SelectDefaultClassBuild and CharacterBuildController.LoadDefaultProgression
         * 
         * AddClassLevels and StatsDistributionPreset are both components of 
         * BlueprintCharacterClass.DefaultBuild
         */
        public LevelPlanHolder(BlueprintCharacterClass defaultClass)
        {
            var defaultBuild = defaultClass.DefaultBuild;
            var addClassLevels = defaultBuild.GetComponent<AddClassLevels>();
            var targetPoints = Main.settings.DefaultPointBuy25 ? 25 : 20;
            var stats = defaultBuild.GetComponents<StatsDistributionPreset>().FirstOrDefault(sd => sd.TargetPoints == targetPoints);


            var unit = Main.settings.DefaultPointBuy25 ?
                Game.Instance.CreateUnitVacuum(BlueprintRoot.Instance.DefaultPlayerCharacter) :
                Game.Instance.CreateUnitVacuum(BlueprintRoot.Instance.CustomCompanion);
            var levelUpController = LevelUpController.Start(unit.Descriptor, instantCommit:true, mode: LevelUpState.CharBuildMode.CharGen);
            levelUpController.SelectPortrait(Game.Instance.BlueprintRoot.CharGen.Portraits[0]);
            levelUpController.SelectGender(Gender.Male);
            levelUpController.SelectRace(Game.Instance.BlueprintRoot.Progression.CharacterRaces[0]);
            if (levelUpController.State.CanSelectRaceStat)
            {
                levelUpController.SelectRaceStat(addClassLevels.RaceStat);
            }
            levelUpController.ApplyStatsDistributionPreset(stats);
            levelUpController.SelectClass(defaultClass);
            {
                var race = levelUpController.Preview.Progression.Race;
                if (race == null) Main.Error($"Leveup race is null");
                else Main.Log($"Levelup race is {race.name}");
            }
            levelUpController.SelectDefaultClassBuild();
            levelUpController.SelectAlignment(Kingmaker.Enums.Alignment.TrueNeutral);
            levelUpController.SelectName($"Default {defaultClass.Name} Build");
            LevelPlanData[0] = new LevelPlanData(1, levelUpController.LevelUpActions.ToArray());
            for (int i = 1; i < 20; i++)
            {
                var plan = unit.Descriptor.Progression.GetLevelPlan(i + 1);
                LevelPlanData[i] = plan;
            }
            //LevelPlanData[0] = levelUpController.GetPlan();
            VerifyLevelPlan();
        }
        public LevelPlanHolder(UnitDescriptor descriptor)
        {
            for(int i = 0; i < LevelPlanData.Length; i++)
            {
                LevelPlanData[i] = descriptor.Progression.GetLevelPlan(i + 1);
            }
            VerifyLevelPlan();
        }
        public void ShowLevelPlan()
        {
            for (int i = 0; i < 20; i++)
            {
                var plan = LevelPlanData[i];
                GUILayout.BeginHorizontal();
                var statusText = plan == null ? "None" : $"Actions {plan.Actions.Length}";
                if(ValidLevels[i])
                {
                    statusText += " Valid";
                } else
                {
                    statusText += " Not Valid";
                }
                GUILayout.Label($"Level {i + 1} {statusText}");
                var openText = open[i] ? "Less" : "More";
                if (GUILayout.Button(openText, GUILayout.ExpandWidth(false)))
                {
                    open[i] = !open[i];
                }
                if (GUILayout.Button("Edit", GUILayout.ExpandWidth(false)))
                {
                    LevelPlanManager.EditLevelPlan(this, i);
                }
                GUILayout.EndHorizontal();
                if (plan != null && open[i])
                {
                    GUILayout.Label($"Levelplan state: {Message[i]}");
                    foreach (var data in plan.Actions)
                    {
                        GUILayout.Label($"  {Util.MakeActionReadable(data)}");
                    }
                }
            }
        }
        /*
        * Verify that a level plan is valid. We do this by creating a test unit and try to level it up with the leveling plan
        * Refer LevelUpController, LevelUpState.IsComplete CharacterBuildController.Next
        * CharBPhase.IsUnlocked
        */
        void VerifyLevelPlan()
        {
            var unitEntityData = new UnitEntityData(GameConsts.DefaultUnitUniqueId, false, Game.Instance.BlueprintRoot.SystemMechanics.DefaultUnit);
            ApplyLevelPlan(unitEntityData.Descriptor);
            for (int i = 0; i < 20; i++)
            {
                var levelUpLog = new List<string>();
                var levelUpController = LevelUpController.Start(unitEntityData.Descriptor, true);
                if (LevelPlanData[i] == null) throw new Exception($"Level plan is null for index {i}");
                if (LevelPlanData[i].Actions == null) throw new Exception($"Actions not set for level plan {i}, level {LevelPlanData[i].Level}");
                foreach (var action in LevelPlanData[i].Actions)
                {
                    if(!action.Check(levelUpController.State, unitEntityData.Descriptor))
                    {
                        levelUpLog.Add($"Invalid action: {action}");
                    }
                    else
                    {
                        action.Apply(levelUpController.State, unitEntityData.Descriptor);
                        levelUpController.State.OnApplyAction();
                    }
                }
                unitEntityData.Descriptor.Progression.ReapplyFeaturesOnLevelUp();
                ValidLevels[i] = levelUpController.State.IsComplete() && levelUpLog.Count == 0;
                if (!levelUpController.State.IsComplete())
                {
                    if (!levelUpController.State.StatsDistribution.IsComplete())
                    {
                        levelUpLog.Add("Stat Distribution is incomplete");
                    }
                    if (levelUpController.State.CanSelectAlignment)
                    {
                        levelUpLog.Add("Alignment not selected");
                    }
                    if (levelUpController.State.CanSelectRace)
                    {
                        levelUpLog.Add("Race not selected");
                    }
                    if (levelUpController.State.CanSelectRaceStat)
                    {
                        levelUpLog.Add("CanSelectRaceStat");
                    }
                    if (levelUpController.State.CanSelectName)
                    {
                        levelUpLog.Add("CanSelectName");
                    }
                    if (levelUpController.State.CanSelectPortrait)
                    {
                        levelUpLog.Add("CanSelectPortrait");
                    }
                    if (levelUpController.State.CanSelectGender)
                    {
                        levelUpLog.Add("CanSelectGender");
                    }
                    if (!levelUpController.State.CanSelectVoice)
                    {
                        levelUpLog.Add("CanSelectVoice");
                    }
                    if (levelUpController.State.AttributePoints > 0)
                    {
                        levelUpLog.Add("Unassigned Attribute Points");
                    }
                    if (levelUpController.State.SelectedClass == null)
                    {
                        levelUpLog.Add("Class is null");
                    }
                    if (!levelUpController.State.IsSkillPointsComplete())
                    {
                        levelUpLog.Add("Unassigned Skill Points");
                    }
                    if (levelUpController.State.Selections.Any((FeatureSelectionState s) => !s.Selected && s.CanSelectAnything(levelUpController.State, unitEntityData.Descriptor)))
                    {
                        levelUpLog.Add("Unassinged Features");
                    }
                    if (levelUpController.State.SpellSelections.Any((SpellSelectionData data) => data.CanSelectAnything(unitEntityData.Descriptor)))
                    {
                        levelUpLog.Add("Unassigned Spells");
                    }
                }
                if (ValidLevels[i])
                {
                    Message[i] = "Valid level plan";
                } else
                {
                    Message[i] = $"Invalid level plan:\n{string.Join("\n", levelUpLog)}";
                }
            }
        }
        public void AddLevelPlan(LevelPlanData plan)
        {
            IsDirty = true;
            IsApplied = false;
            LevelPlanData[plan.Level - 1] = plan;
            VerifyLevelPlan();
        }
        public void ApplyLevelPlan(UnitDescriptor unit)
        {
            for(int i = 0; i < LevelPlanData.Length; i++)
            {
                if (LevelPlanData[i] == null) continue;
                unit.Progression.AddLevelPlan(LevelPlanData[i]);
            }
        }
        /*
         * Refer AddClassLevels.LevelUp and Player.CreateCustomCompanion
         */
        public UnitEntityData CreateUnit(int level)
        {

            //var playerUnit = ResourcesLibrary.TryGetBlueprint<BlueprintUnit>("4391e8b9afbb0cf43aeba700c089f56d");
            //var unit = new UnitDescriptor(playerUnit, null);
            var unit = Main.settings.DefaultPointBuy25 ?
                Game.Instance.CreateUnitVacuum(BlueprintRoot.Instance.DefaultPlayerCharacter) :
                Game.Instance.CreateUnitVacuum(BlueprintRoot.Instance.CustomCompanion);
            ApplyLevelPlan(unit.Descriptor);
            for (int i = 0; i < level; i++)
            {
                var levelUpController = LevelUpController.Start(unit.Descriptor, true);
            }
            return unit;
        }
    }
}