using System;
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
         * LevelUpController.SelectDefaultClassBuild
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
            var levelUpController = LevelUpController.Start(unit.Descriptor, true);
            levelUpController.SelectPortrait(Game.Instance.BlueprintRoot.CharGen.Portraits[0]);
            levelUpController.SelectGender(Gender.Male);
            levelUpController.SelectRace(Game.Instance.BlueprintRoot.Progression.CharacterRaces[0]);
            if (levelUpController.State.CanSelectRaceStat)
            {
                levelUpController.SelectRaceStat(addClassLevels.RaceStat);
            }
            levelUpController.ApplyStatsDistributionPreset(stats);
            levelUpController.SelectClass(defaultClass);
            levelUpController.SelectDefaultClassBuild();
            for (int i = 0; i < 20; i++)
            {
                var plan = unit.Descriptor.Progression.GetLevelPlan(i + 1);
                LevelPlanData[i] = plan;
            }
            LevelPlanData[0] = levelUpController.GetPlan();
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
                    foreach (var data in plan.Actions)
                    {
                        GUILayout.Label($"  {Util.MakeActionReadable(data)}");
                    }
                }
            }
        }
        /*
        * Refer LevelUpController, LevelUpState.IsComplete CharacterBuildController.Next
        * CharBPhase.IsUnlocked
        */
        void VerifyLevelPlan()
        {
            var unitEntityData = new UnitEntityData(GameConsts.DefaultUnitUniqueId, false, Game.Instance.BlueprintRoot.SystemMechanics.DefaultUnit);
            ApplyLevelPlan(unitEntityData.Descriptor);
            for (int i = 0; i < 20; i++)
            {
                var levelUpController = LevelUpController.Start(unitEntityData.Descriptor, true);
                ValidLevels[i] = levelUpController.State.IsComplete();
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