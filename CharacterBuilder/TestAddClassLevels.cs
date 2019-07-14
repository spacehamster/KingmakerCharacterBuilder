using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Kingmaker.Assets.UI.LevelUp;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Prerequisites;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.Validation;
using Kingmaker.Controllers.Rest;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Class.LevelUp;
using Kingmaker.UnitLogic.Class.LevelUp.Actions;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.View;
using Newtonsoft.Json;

namespace CharacterBuilder
{

    public class TestAddClassLevels
    {
        AddClassLevels instance;
        public TestAddClassLevels(AddClassLevels instance)
        {
            this.instance = instance;
        }
        private int GetLevels()
        {
            RuleSummonUnit ruleSummonUnit = Rulebook.CurrentContext.LastEvent<RuleSummonUnit>();
            if (ruleSummonUnit != null && ruleSummonUnit.Level > 0)
            {
                return ruleSummonUnit.Level;
            }
            return instance.Levels;
        }

        public void LevelUp(UnitDescriptor unit, int levels)
        {
            this.LevelUp(unit, levels, false);
        }

        private void LevelUp(UnitDescriptor unit, int levels, bool fromFact)
        {
            using (new IgnorePrerequisites())
            {
                ClassLevelLimit component = unit.Blueprint.GetComponent<ClassLevelLimit>();
                int classLevelLimit = component == null ? int.MaxValue : component.LevelLimit;
                if (ElementsContext.GetData<DefaultBuildData>() != null)
                {
                    classLevelLimit = 0;
                }
                Dictionary<SelectionEntry, HashSet<int>> selectionsHistory = new Dictionary<SelectionEntry, HashSet<int>>();
                HashSet<int> spellHistory = (instance.SelectSpells.Length <= 0) ? null : new HashSet<int>();
                for (int i = 0; i < levels; i++)
                {
                    if (unit.Progression.CharacterLevel < classLevelLimit)
                    {
                        Main.Log($"A: Adding level {i} unitlevel {unit.Progression.CharacterLevel}");
                        this.AddLevel(unit, selectionsHistory, spellHistory);
                    }
                    else
                    {
                        Main.Log($"B: Adding level {i} unitlevel {unit.Progression.CharacterLevel}");
                        LevelUpPlanUnitHolder levelUpPlanUnitHolder = unit.Get<LevelUpPlanUnitHolder>();
                        if(levelUpPlanUnitHolder != null)
                        {
                            UnitDescriptor unitDescriptor = levelUpPlanUnitHolder.RequestPlan();
                            LevelUpController levelUpController = this.AddLevel(unitDescriptor, selectionsHistory, spellHistory);
                            unit.Progression.AddLevelPlan(levelUpController.GetPlan());
                        }
                    }
                }
                this.PrepareSpellbook(unit);
                unit.Progression.ReapplyFeaturesOnLevelUp();
                UnitEntityView view = unit.Unit.View;
                if (view != null)
                {
                    view.UpdateClassEquipment();
                }
                RestController.ApplyRest(unit);
            }
        }

        [NotNull]
        private LevelUpController AddLevel(UnitDescriptor unit, Dictionary<SelectionEntry, HashSet<int>> selectionsHistory, HashSet<int> spellHistory)
        {
            LevelUpController levelUpController = LevelUpController.Start(unit, true, null, null, LevelUpState.CharBuildMode.LevelUp);
            if (levelUpController.State.CanSelectRace)
            {
                BlueprintRace race = unit.Progression.Race;
                DefaultBuildData data = ElementsContext.GetData<DefaultBuildData>();
                if (data != null)
                {
                    race = data.Race;
                }
                if (race != null)
                {
                    levelUpController.SelectRace(race);
                }
            }
            if (levelUpController.State.CanSelectRaceStat)
            {
                levelUpController.SelectRaceStat(instance.RaceStat);
            }
            this.ApplyStatsDistributionPreset(levelUpController);
            if (unit.Progression.GetClassLevel(instance.CharacterClass) <= 0)
            {
                foreach (BlueprintArchetype archetype in instance.Archetypes)
                {
                    levelUpController.AddArchetype(instance.CharacterClass, archetype);
                }
            }
            levelUpController.SelectClass(instance.CharacterClass, false);
            levelUpController.ApplyClassMechanics();
            this.PerformSelections(levelUpController, selectionsHistory, new LevelUpActionPriority?(LevelUpActionPriority.ReplaceSpellbook));
            levelUpController.ApplySpellbook();
            while (levelUpController.State.AttributePoints > 0 && instance.LevelsStat.IsAttribute())
            {
                levelUpController.SpendAttributePoint(instance.LevelsStat);
            }
            this.PerformSelections(levelUpController, selectionsHistory, new LevelUpActionPriority?(LevelUpActionPriority.ApplySkillPoints));
            levelUpController.ApplySkillPoints();
            while (levelUpController.State.SkillPointsRemaining > 0)
            {
                int skillPointsRemaining = levelUpController.State.SkillPointsRemaining;
                foreach (StatType skill in instance.Skills)
                {
                    if (levelUpController.State.SkillPointsRemaining <= 0)
                    {
                        break;
                    }
                    levelUpController.SpendSkillPoint(skill);
                }
                if (skillPointsRemaining == levelUpController.State.SkillPointsRemaining)
                {
                    break;
                }
            }
            this.PerformSelections(levelUpController, selectionsHistory, null);
            this.PerformSpellSelections(levelUpController, spellHistory);
            return levelUpController;
        }

        private void ApplyStatsDistributionPreset(LevelUpController controller)
        {
            if (instance.Fact == null)
            {
                return;
            }
            LevelUpState state = controller.State;
            if (!state.StatsDistribution.Available)
            {
                return;
            }
            StatsDistributionPreset statsDistributionPreset = instance.Fact.Blueprint.GetComponents<StatsDistributionPreset>().FirstOrDefault((StatsDistributionPreset sd) => sd.TargetPoints == state.StatsDistribution.Points);
            if (statsDistributionPreset == null)
            {
                return;
            }
            controller.ApplyStatsDistributionPreset(statsDistributionPreset);
        }

        private void PerformSelections(LevelUpController controller, Dictionary<SelectionEntry, HashSet<int>> selectionsHistory, LevelUpActionPriority? maxPriority = null)
        {
            SelectionEntry[] selections = instance.Selections;
            int i = 0;
            while (i < selections.Length)
            {
                SelectionEntry selectionEntry = selections[i];
                if (maxPriority == null)
                {
                    goto IL_66;
                }
                if (selectionEntry.IsParametrizedFeature)
                {
                    if (SelectFeature.CalculatePriority(selectionEntry.ParametrizedFeature) <= maxPriority.Value)
                    {
                        goto IL_66;
                    }
                }
                else if (SelectFeature.CalculatePriority(selectionEntry.Selection) <= maxPriority.Value)
                {
                    goto IL_66;
                }
            IL_253:
                i++;
                continue;
            IL_66:
                HashSet<int> hashSet;
                if (!selectionsHistory.TryGetValue(selectionEntry, out hashSet))
                {
                    hashSet = new HashSet<int>();
                    selectionsHistory[selectionEntry] = hashSet;
                }
                if (selectionEntry.IsParametrizedFeature)
                {
                    FeatureSelectionState featureSelectionState = controller.State.FindSelection(selectionEntry.ParametrizedFeature, false);
                    if (featureSelectionState != null)
                    {
                        FeatureUIData item;
                        switch (selectionEntry.ParametrizedFeature.ParameterType)
                        {
                            case FeatureParameterType.Custom:
                            case FeatureParameterType.SpellSpecialization:
                                item = new FeatureUIData(selectionEntry.ParametrizedFeature, selectionEntry.ParamObject, string.Empty, string.Empty, null, selectionEntry.ParamObject.ToString());
                                break;
                            case FeatureParameterType.WeaponCategory:
                                item = new FeatureUIData(selectionEntry.ParametrizedFeature, selectionEntry.ParamWeaponCategory, string.Empty, string.Empty, null, selectionEntry.ParamWeaponCategory.ToString());
                                break;
                            case FeatureParameterType.SpellSchool:
                                item = new FeatureUIData(selectionEntry.ParametrizedFeature, selectionEntry.ParamSpellSchool, string.Empty, string.Empty, null, selectionEntry.ParamSpellSchool.ToString());
                                break;
                            case FeatureParameterType.LearnSpell:
                                goto IL_1BD;
                            case FeatureParameterType.Skill:
                                item = new FeatureUIData(selectionEntry.ParametrizedFeature, selectionEntry.Stat, string.Empty, string.Empty, null, selectionEntry.Stat.ToString());
                                break;
                            default:
                                goto IL_1BD;
                        }
                        controller.SelectFeature(featureSelectionState, item);
                        goto IL_1CE;
                    IL_1BD:
                        throw new ArgumentOutOfRangeException();
                    }
                IL_1CE:
                    goto IL_253;
                }
                for (int j = 0; j < selectionEntry.Features.Length; j++)
                {
                    if (!hashSet.Contains(j))
                    {
                        BlueprintFeature blueprintFeature = selectionEntry.Features[j];
                        FeatureSelectionState featureSelectionState2 = controller.State.FindSelection(selectionEntry.Selection, false);
                        if (featureSelectionState2 != null && blueprintFeature != null && controller.SelectFeature(featureSelectionState2, blueprintFeature))
                        {
                            hashSet.Add(j);
                        }
                    }
                }
                goto IL_253;
            }
        }
        private void PerformSpellSelections(LevelUpController controller, HashSet<int> spellHistory)
        {
            for (int i = 0; i < instance.SelectSpells.Length; i++)
            {
                BlueprintAbility spell = instance.SelectSpells[i];
                if (!spellHistory.Contains(i))
                {
                    foreach (SpellSelectionData spellSelectionData in controller.State.SpellSelections)
                    {
                        if (spellSelectionData.HasEmpty())
                        {
                            int level = spellSelectionData.SpellList.GetLevel(spell);
                            if (level >= 0)
                            {
                                SpellSelectionData.SpellSelectionState spellSelectionState = spellSelectionData.LevelCount[level];
                                BlueprintAbility[] array = ((spellSelectionState != null) ? spellSelectionState.SpellSelections : null) ?? ((spellSelectionData.ExtraMaxLevel < level) ? null : spellSelectionData.ExtraSelected);
                                if (array != null)
                                {
                                    int slotIndex = 0;
                                    for (int j = 0; j < array.Length; j++)
                                    {
                                        if (array[j] == null)
                                        {
                                            slotIndex = j;
                                            break;
                                        }
                                    }
                                    if (controller.SelectSpell(spellSelectionData.Spellbook, spellSelectionData.SpellList, level, spell, slotIndex))
                                    {
                                        spellHistory.Add(i);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        private void PrepareSpellbook(UnitDescriptor unit)
        {
            Spellbook spellbook = unit.GetSpellbook(instance.CharacterClass);
            if (spellbook == null)
            {
                return;
            }
            spellbook.UpdateAllSlotsSize(true);
            foreach (BlueprintAbility blueprint in instance.MemorizeSpells)
            {
                AbilityData data = new AbilityData(blueprint, spellbook);
                spellbook.Memorize(data, null);
            }
        }
    }
}
