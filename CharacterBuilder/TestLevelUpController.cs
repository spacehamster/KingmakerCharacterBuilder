using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Harmony12;
using JetBrains.Annotations;
using Kingmaker;
using Kingmaker.Assets.UI.LevelUp;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.Root;
using Kingmaker.Controllers.Rest;
using Kingmaker.Dungeon.Units;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Persistence.JsonUtility;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.Items;
using Kingmaker.UI.SettingsUI;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Class.LevelUp;
using Kingmaker.UnitLogic.Class.LevelUp.Actions;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.Visual.Sound;
using Newtonsoft.Json.Linq;

namespace CharacterBuilder
{
	public class TestLevelUpController
    {
		public const int MaxLevel = 20;

		[NotNull]
		public readonly UnitDescriptor Unit;

		public readonly bool AutoCommit;

		[NotNull]
		public UnitDescriptor Preview;

		[NotNull]
		public List<ILevelUpAction> LevelUpActions;

		[NotNull]
		public LevelUpState State;

		[CanBeNull]
		public DollState Doll;

		public int BirthDay;

		public int BirthMonth;

		private bool m_RecalculatePreview;

		[CanBeNull]
		private Action m_OnSuccess;

		private bool m_ApplyingPlan;

		private bool m_HasPlan;

		private bool m_PlanChanged;

		private TestLevelUpController([NotNull] UnitDescriptor unit, bool autoCommit, [CanBeNull] JToken unitJson, LevelUpState.CharBuildMode mode)
		{
			this.LevelUpActions = new List<ILevelUpAction>();
			this.Unit = unit;
			this.AutoCommit = autoCommit;
			if (this.AutoCommit)
			{
				this.Preview = unit;
			}
			else
			{
				this.StartPreviewThread(unitJson);
				this.Preview = this.RequestPreview();
			}
			this.State = new LevelUpState(this.Preview, mode);
			if ((this.State.Mode == LevelUpState.CharBuildMode.CharGen && !this.AutoCommit) || this.State.Mode == LevelUpState.CharBuildMode.Respec)
			{
				this.Doll = new DollState();
			}
			this.ApplyLevelUpPlan(false);
		}

		public bool IsAutoLevelup
		{
			[CompilerGenerated]
			get
			{
				return this.m_HasPlan && !this.m_PlanChanged;
			}
		}

		public bool HasNextLevelPlan
		{
			get
			{
				bool flag = this.State.IsFirstLevel && this.State.SelectedClass != null;
				return flag || this.Unit.Progression.GetLevelPlan(this.State.NextLevel) != null;
			}
		}
		object GetUnitPart(Type type, UnitDescriptor unit)
        {
            var partsManager = AccessTools.Field(typeof(UnitDescriptor), "m_Parts").GetValue(unit);
            var parts = (Dictionary<Type, UnitPart>)AccessTools.Field(typeof(UnitPartsManager), "m_Parts").GetValue(partsManager);
            parts.TryGetValue(type, out var result);
            return result;
        }
		private bool ApplyLevelUpPlan(bool ignoreSettings = false)
		{
			this.m_HasPlan = false;
			bool flag2;
			if (this.State.Mode != LevelUpState.CharBuildMode.PreGen && !ignoreSettings)
			{
                var UnitPartImportableCompanionType = Type.GetType("Kingmaker.Dungeon.Units.UnitPartImportableCompanion, Assembly-CSharp");
                var unitPartImportableCompanion = GetUnitPart(UnitPartImportableCompanionType, this.Unit);
                flag2 = unitPartImportableCompanion != null ?
                    (bool)AccessTools.Field(UnitPartImportableCompanionType, "IsOnImportedPlan").GetValue(unitPartImportableCompanion) :
                    false;
			}
			else
			{
				flag2 = true;
			}
			bool flag3 = flag2;
			if (!flag3)
			{
				switch (Game.Instance.Player.Difficulty.AutoLevelup)
				{
				case AutolevelupState.Off:
					flag3 = false;
					break;
				case AutolevelupState.Companions:
					flag3 = (this.Unit.Unit != Game.Instance.Player.MainCharacter);
					break;
				case AutolevelupState.AllPossible:
					flag3 = true;
					break;
				default:
					flag3 = false;
					break;
				}
			}
			if (!flag3)
			{
				return false;
			}
			LevelPlanData levelPlan = this.Unit.Progression.GetLevelPlan(this.State.NextLevel);
			if (levelPlan == null)
			{
				return false;
			}
			this.m_ApplyingPlan = true;
			try
			{
				foreach (ILevelUpAction action in levelPlan.Actions)
				{
					this.m_HasPlan = true;
					this.AddAction(action, true);
				}
			}
			finally
			{
				this.m_ApplyingPlan = false;
			}
			return this.m_HasPlan;
		}

		[NotNull]
		public static TestLevelUpController Start([NotNull] UnitDescriptor unit, bool instantCommit = false, [CanBeNull] JToken unitJson = null, Action onSuccess = null, LevelUpState.CharBuildMode mode = LevelUpState.CharBuildMode.LevelUp)
		{
			return new TestLevelUpController(unit, instantCommit, unitJson, mode)
			{
				m_OnSuccess = onSuccess
			};
		}

		public static int GetEffectiveLevel(UnitEntityData unit)
		{
			unit = (unit ?? Game.Instance.Player.MainCharacter.Value);
			int? num = (unit != null) ? new int?(unit.Descriptor.Progression.CharacterLevel) : null;
			int i = (num == null) ? 1 : num.Value;
			int? num2 = (unit != null) ? new int?(unit.Descriptor.Progression.Experience) : null;
			int num3 = (num2 == null) ? 1 : num2.Value;
			while (i < 20)
			{
				if (Game.Instance.BlueprintRoot.Progression.XPTable.GetBonus(i + 1) > num3)
				{
					break;
				}
				i++;
			}
			return i;
		}

		public static bool CanLevelUp([NotNull] UnitDescriptor unit)
		{
			if (Game.Instance.Player.IsInCombat)
			{
				return false;
			}
			if (unit.State.IsDead)
			{
				return false;
			}
			int characterLevel = unit.Progression.CharacterLevel;
			if (characterLevel >= 20)
			{
				return false;
			}
			int bonus = Game.Instance.BlueprintRoot.Progression.XPTable.GetBonus(characterLevel + 1);
			return bonus <= unit.Progression.Experience;
		}

		private void UpdatePreview()
		{
			if (!this.m_RecalculatePreview)
			{
				return;
			}
			if (this.AutoCommit)
			{
				throw new Exception("Trying to recalc preview in AutoCommit mode.");
			}
			this.m_RecalculatePreview = false;
			try
			{
				this.Preview.Unit.Dispose();
				this.Preview = this.RequestPreview();
				List<ILevelUpAction> collection = this.ApplyLevelup(this.Preview);
				this.LevelUpActions.AddRange(collection);
				if (this.Doll != null)
				{
					this.Doll.UpdateMechanicsEntities(this.Preview);
				}
			}
			finally
			{
			}
		}

		private List<ILevelUpAction> ApplyLevelup(UnitDescriptor unitDescriptor)
		{
			this.State = new LevelUpState(unitDescriptor, this.State.Mode);
			List<ILevelUpAction> list = new List<ILevelUpAction>();
			foreach (ILevelUpAction levelUpAction in this.LevelUpActions)
			{
				if (!levelUpAction.Check(this.State, unitDescriptor))
				{
					UberDebug.Log("Invalid action: " + levelUpAction, Array.Empty<object>());
				}
				else
				{
					list.Add(levelUpAction);
					levelUpAction.Apply(this.State, unitDescriptor);
					this.State.OnApplyAction();
				}
			}
			unitDescriptor.Progression.ReapplyFeaturesOnLevelUp();
			this.LevelUpActions.Clear();
			return list;
		}

		private void StartPreviewThread([CanBeNull] JToken unitJson)
		{
			if (unitJson == null)
			{
				unitJson = UnitSerialization.Serialize(this.Unit);
			}
			LevelUpPreviewThread.Start(unitJson);
		}

		[NotNull]
		private UnitDescriptor RequestPreview()
		{
			if (this.AutoCommit)
			{
				throw new Exception("Trying to request preview in AutoCommit mode.");
			}
			UnitDescriptor result;
			try
			{
				UnitDescriptor unitDescriptor = LevelUpPreviewThread.RequestPreview();
				unitDescriptor.Buffs.SetupPreview(unitDescriptor);
				unitDescriptor.Unit.PostLoad();
				unitDescriptor.Unit.TurnOn();
				result = unitDescriptor;
			}
			finally
			{
			}
			return result;
		}

		private bool AddAction([NotNull] ILevelUpAction action, bool ignoreOrder = false)
		{
			LevelUpActionPriority levelUpActionPriority = (this.LevelUpActions.Count <= 0) ? LevelUpActionPriority.Visual : this.LevelUpActions[this.LevelUpActions.Count - 1].Priority;
			if (ignoreOrder || this.AutoCommit || action.Priority >= levelUpActionPriority)
			{
				if (!this.m_RecalculatePreview)
				{
					if (!action.Check(this.State, this.Preview))
					{
						return false;
					}
					this.LevelUpActions.Add(action);
					action.Apply(this.State, this.Preview);
					this.State.OnApplyAction();
					if (this.Doll != null)
					{
						this.Doll.UpdateMechanicsEntities(this.Preview);
					}
					if (!this.m_ApplyingPlan)
					{
						this.m_PlanChanged = true;
					}
					return true;
				}
				else
				{
					this.LevelUpActions.Add(action);
				}
			}
			else
			{
				for (int i = 0; i < this.LevelUpActions.Count; i++)
				{
					if (this.LevelUpActions[i].Priority > action.Priority)
					{
						this.LevelUpActions.Insert(i, action);
						this.m_RecalculatePreview = true;
						break;
					}
				}
			}
			this.UpdatePreview();
			bool flag = this.LevelUpActions.Contains(action);
			if (flag && !this.m_ApplyingPlan)
			{
				this.m_PlanChanged = true;
			}
			return flag;
		}

		private bool RemoveAction<T>([CanBeNull] Predicate<T> predicate = null) where T : ILevelUpAction
		{
			if (this.AutoCommit)
			{
				return false;
			}
			for (int i = 0; i < this.LevelUpActions.Count; i++)
			{
				ILevelUpAction levelUpAction = this.LevelUpActions[i];
				LevelUpActionPriority priority = levelUpAction.Priority;
				if (!(levelUpAction.GetType() != typeof(T)))
				{
					if (predicate == null || predicate((T)((object)levelUpAction)))
					{
						this.LevelUpActions.RemoveAt(i);
						this.m_RecalculatePreview = true;
						if (!this.m_ApplyingPlan && priority > LevelUpActionPriority.Visual && priority < LevelUpActionPriority.Alignment)
						{
							this.m_PlanChanged = true;
						}
						return true;
					}
				}
			}
			return false;
		}

		public bool SelectClass([NotNull] BlueprintCharacterClass characterClass, bool applyMechanics = true)
		{
			this.RemoveAction<SelectClass>(null);
			if (!this.AddAction(new SelectClass(characterClass), false))
			{
				return false;
			}
			if (applyMechanics)
			{
				this.ApplyClassMechanics();
				this.ApplySpellbook();
				this.ApplySkillPoints();
			}
			if (this.Doll != null)
			{
				this.Doll.SetClass(characterClass);
			}
			if (!this.State.CanSelectAlignment && this.State.IsAlignmentRestricted(this.Preview.Alignment.Value))
			{
				this.RemoveAction<SelectAlignment>(null);
				this.UpdatePreview();
			}
			return true;
		}

		public bool SelectDefaultClassBuild()
		{
			if (!this.HasNextLevelPlan)
			{
				return false;
			}
			if (!this.State.IsFirstLevel)
			{
				this.RemoveAction<SelectClass>(null);
				bool flag = this.ApplyLevelUpPlan(true);
				if (flag)
				{
					this.m_PlanChanged = false;
					this.UpdateDifficulty();
					return true;
				}
				return false;
			}
			else
			{
				if (this.State.SelectedClass == null)
				{
					return false;
				}
				BlueprintUnitFact defaultBuild = this.State.SelectedClass.DefaultBuild;
				if (defaultBuild == null)
				{
					return false;
				}
				BlueprintRace race = this.Preview.Progression.Race;
				if (race == null || this.State.CanSelectRace)
				{
					return false;
				}
				bool result;
				try
				{
					using (new DefaultBuildData(race))
					{
						this.Unit.Ensure<LevelUpPlanUnitHolder>();
						this.Unit.Progression.DropLevelPlans();
						this.Unit.AddFact(defaultBuild, null, null);
						LevelPlanData levelPlan = this.Unit.Progression.GetLevelPlan(this.State.NextLevel);
						if (levelPlan == null)
						{
							result = false;
						}
						else
						{
							this.m_RecalculatePreview = true;
							this.LevelUpActions.RemoveAll((ILevelUpAction a) => TestLevelUpController.IsDefaultBuildPriority(a.Priority));
							this.LevelUpActions.AddRange(from a in levelPlan.Actions
							where TestLevelUpController.IsDefaultBuildPriority(a.Priority)
							select a);
							this.LevelUpActions = (from a in this.LevelUpActions
							orderby a.Priority
							select a).ToList<ILevelUpAction>();
							this.UpdatePreview();
							this.m_HasPlan = true;
							this.m_PlanChanged = false;
							this.UpdateDifficulty();
							result = true;
						}
					}
				}
				finally
				{
					this.Unit.RemoveFact(defaultBuild);
					this.Unit.Remove<LevelUpPlanUnitHolder>();
				}
				return result;
			}
		}

		public void ApplyPlanAsFarAsPossible()
		{
			int effectiveLevel = LevelUpController.GetEffectiveLevel(this.Unit.Unit);
			while (this.Preview.Progression.CharacterLevel < effectiveLevel && this.HasNextLevelPlan)
			{
				this.ApplyLevelUpPlan(true);
				this.Commit();
			}
		}

		private void UpdateDifficulty()
		{
			AutolevelupState currentValue = SettingsRoot.Instance.AutoLevelup.CurrentValue;
			SettingsRoot.Instance.AutoLevelup.CurrentValue = ((this.Unit.IsMainCharacter || currentValue == AutolevelupState.AllPossible) ? AutolevelupState.AllPossible : AutolevelupState.Companions);
			BlueprintRoot.Instance.DifficultyList.CustomDifficulty.Settings.AutoLevelup = SettingsRoot.Instance.AutoLevelup.CurrentValue;
			Game.Instance.SettingsManager.DifficultySettingsController.SetGameDifficulty(BlueprintRoot.Instance.DifficultyList.CustomDifficulty);
		}

		public void ApplyStatsDistributionPreset(StatsDistributionPreset preset)
		{
			foreach (ILevelUpAction action in preset.GetActions())
			{
				this.AddAction(action, false);
			}
		}

		private static bool IsDefaultBuildPriority(LevelUpActionPriority priority)
		{
			return priority != LevelUpActionPriority.Visual && priority != LevelUpActionPriority.Race && priority != LevelUpActionPriority.Class && priority != LevelUpActionPriority.ApplyClass && priority != LevelUpActionPriority.ApplySpellbook && priority != LevelUpActionPriority.ApplySkillPoints && priority != LevelUpActionPriority.Alignment;
		}

		public void ApplyClassMechanics()
		{
			if (!this.LevelUpActions.OfType<ApplyClassMechanics>().Any<ApplyClassMechanics>())
			{
				this.AddAction(new ApplyClassMechanics(), false);
			}
		}

		public void ApplySpellbook()
		{
			if (!this.LevelUpActions.OfType<ApplySpellbook>().Any<ApplySpellbook>())
			{
				this.AddAction(new ApplySpellbook(), false);
			}
		}

		public void ApplySkillPoints()
		{
			if (!this.LevelUpActions.OfType<ApplySkillPoints>().Any<ApplySkillPoints>())
			{
				this.AddAction(new ApplySkillPoints(), false);
			}
		}

		public bool AddArchetype([NotNull] BlueprintArchetype archetype)
		{
			return !(this.State.SelectedClass == null) && this.AddAction(new AddArchetype(this.State.SelectedClass, archetype), false);
		}

		public bool AddArchetype([NotNull] BlueprintCharacterClass characterClass, [NotNull] BlueprintArchetype archetype)
		{
			return this.AddAction(new AddArchetype(characterClass, archetype), false);
		}

		public void RemoveArchetype([NotNull] BlueprintArchetype archetype)
		{
			this.RemoveAction<AddArchetype>((AddArchetype a) => a.Archetype == archetype);
			this.UpdatePreview();
		}

		public bool SpendAttributePoint(StatType attribute)
		{
			return this.AddAction(new SpendAttributePoint(attribute), false);
		}

		public void UnspendAttributePoint(StatType attribute)
		{
			this.RemoveAction<SpendAttributePoint>((SpendAttributePoint a) => a.Attribute == attribute);
			this.UpdatePreview();
		}

		public bool SpendSkillPoint(StatType skill)
		{
			return this.AddAction(new SpendSkillPoint(skill), false);
		}

		public void UnspendSkillPoint(StatType skill)
		{
			this.RemoveAction<SpendSkillPoint>((SpendSkillPoint a) => a.Skill == skill);
			this.UpdatePreview();
		}

		public bool SelectFeature([NotNull] FeatureSelectionState selection, [NotNull] IFeatureSelectionItem item)
		{
			return this.AddAction(new SelectFeature(selection, item), false);
		}

		public void UnselectFeature([NotNull] FeatureSelectionState selectionState)
		{
			this.RemoveAction<SelectFeature>((SelectFeature a) => a.Selection == selectionState.Selection && a.SelectionIndex == selectionState.Index);
			this.UpdatePreview();
		}

		public bool SelectSpell([NotNull] BlueprintSpellbook spellbook, [NotNull] BlueprintSpellList spellList, int spellLevel, [NotNull] BlueprintAbility spell, int slotIndex)
		{
			return this.AddAction(new SelectSpell(spellbook, spellList, spellLevel, spell, slotIndex), false);
		}

		public void UnselectSpell([NotNull] BlueprintSpellbook spellbook, [NotNull] BlueprintSpellList spellList, int slotIndex, int level)
		{
			if (level >= 0)
			{
				this.RemoveAction<SelectSpell>((SelectSpell a) => a.SlotIndex == slotIndex && a.SpellLevel == level && a.SpellList == spellList && a.Spellbook == spellbook);
			}
			else
			{
				this.RemoveAction<SelectSpell>((SelectSpell a) => a.SlotIndex == slotIndex && a.SpellList == spellList && a.Spellbook == spellbook);
			}
			this.UpdatePreview();
		}

		public bool AddStatPoint(StatType attribute)
		{
			if (this.RemoveAction<RemoveStatPoint>((RemoveStatPoint a) => a.Attribute == attribute))
			{
				this.UpdatePreview();
				return true;
			}
			return this.AddAction(new AddStatPoint(attribute), false);
		}

		public bool RemoveStatPoint(StatType attribute)
		{
			if (this.RemoveAction<AddStatPoint>((AddStatPoint a) => a.Attribute == attribute))
			{
				this.UpdatePreview();
				return true;
			}
			return this.AddAction(new RemoveStatPoint(attribute), false);
		}

		public bool SelectAlignment(Alignment alignment)
		{
			this.RemoveAction<SelectAlignment>(null);
			return this.AddAction(new SelectAlignment(alignment), false);
		}

		public bool SelectRace(BlueprintRace race)
		{
			this.RemoveAction<SelectRace>(null);
			if (!this.AddAction(new SelectRace(race), false))
			{
				return false;
			}
			if (this.Doll != null)
			{
				this.Doll.SetRace(race);
			}
			return true;
		}

		public bool SelectRaceStat(StatType attribute)
		{
			this.RemoveAction<SelectRaceStat>(null);
			return this.AddAction(new SelectRaceStat(attribute), false);
		}

		public bool SelectName(string name)
		{
			this.RemoveAction<SelectName>(null);
			return this.AddAction(new SelectName(name), false);
		}

		public bool SelectPortrait([NotNull] BlueprintPortrait portrait)
		{
			this.RemoveAction<SelectPortrait>(null);
			if (!this.AddAction(new SelectPortrait(portrait), false))
			{
				return false;
			}
			if (this.Doll != null)
			{
				this.Doll.SetPortrait(portrait);
			}
			return true;
		}

		public bool SelectGender(Gender gender)
		{
			this.RemoveAction<SelectGender>(null);
			if (!this.AddAction(new SelectGender(gender), false))
			{
				return false;
			}
			if (this.Doll != null)
			{
				this.Doll.SetGender(gender);
			}
			return true;
		}

		public bool SelectVoice(BlueprintUnitAsksList voice)
		{
			this.RemoveAction<SelectVoice>(null);
			return this.AddAction(new SelectVoice(voice), false);
		}

		public void SetBirthDay(int day, int month)
		{
			this.BirthDay = day;
			this.BirthMonth = month;
		}

		public void Commit()
		{
			if (this.AutoCommit)
			{
				UberDebug.LogWarning("Trying to commit LevelUp with AutoCommit", Array.Empty<object>());
				return;
			}
			this.Preview.Unit.Dispose();

            var UnitPartImportableCompanionType = Type.GetType("Kingmaker.Dungeon.Units.UnitPartImportableCompanion, Assembly-CSharp");
            var unitPartImportableCompanion = GetUnitPart(UnitPartImportableCompanionType, this.Unit);
			if (unitPartImportableCompanion != null)
			{
                var levels = (List<LevelPlanData>)AccessTools.Field(UnitPartImportableCompanionType, "Levels").GetValue(unitPartImportableCompanion);
                levels.Add(this.GetPlan());
			}
			this.ApplyLevelup(this.Unit);
			this.Unit.Unit.View.UpdateClassEquipment();
			LevelUpPreviewThread.Stop();
			if (this.State.IsFirstLevel)
			{
				this.SetupNewCharacher();
			}
			if (this.m_PlanChanged && !this.State.IsFirstLevel)
			{
				this.Unit.Progression.DropLevelPlans();
			}
			if (this.m_OnSuccess != null)
			{
				this.m_OnSuccess();
			}
		}

		public void Cancel()
		{
			if (!this.AutoCommit)
			{
				this.Preview.Unit.Dispose();
			}
			LevelUpPreviewThread.Stop();
		}

		[NotNull]
		public LevelPlanData GetPlan()
		{
			return new LevelPlanData(this.State.NextLevel, this.LevelUpActions.ToArray());
		}

		public LevelUpTotalStats BuildTotal()
		{
			return new LevelUpTotalStats(this.Unit, this.Preview);
		}

		private void SetupNewCharacher()
		{
			this.Unit.Unit.View.UpdateAsks();
			this.Unit.BirthDay = this.BirthDay;
			this.Unit.BirthMonth = this.BirthMonth;
			if (this.Doll != null)
			{
				this.Unit.Doll = this.Doll.CreateData();
				this.Unit.LeftHandedOverride = new bool?(this.Doll.LeftHanded);
			}
			if (this.State.Mode != LevelUpState.CharBuildMode.PreGen)
			{
				ItemsCollection.DoWithoutEvents(delegate
				{
					LevelUpHelper.AddStartingItems(this.Unit);
				});
			}
			else
			{
				this.Unit.Body.Initialize();
			}
			this.Unit.AddStartingInventory();
			foreach (Spellbook spellbook in this.Unit.Spellbooks)
			{
				spellbook.UpdateAllSlotsSize(true);
				int num = spellbook.GetTotalFreeSlotsCount();
				for (int i = 0; i < 100; i++)
				{
					if (num <= 0)
					{
						break;
					}
					foreach (BlueprintAbility blueprint in BlueprintRoot.Instance.Progression.CharGenMemorizeSpells)
					{
						AbilityData data = new AbilityData(blueprint, spellbook);
						spellbook.Memorize(data, null);
					}
					int totalFreeSlotsCount = spellbook.GetTotalFreeSlotsCount();
					if (num <= totalFreeSlotsCount)
					{
						break;
					}
					num = totalFreeSlotsCount;
				}
			}
			RestController.ApplyRest(this.Unit);
			if (this.Unit.IsCustomCompanion() && this.State.Mode != LevelUpState.CharBuildMode.Respec)
			{
				Game.Instance.EntityCreator.AddEntity(this.Unit.Unit, Game.Instance.Player.CrossSceneState);
				Game.Instance.Player.RemoteCompanions.Add(this.Unit.Unit);
				Game.Instance.Player.InvalidateCharacterLists();
				this.Unit.Unit.IsInGame = false;
				this.Unit.Unit.AttachToViewOnLoad(null);
				if (this.Unit.Unit.View != null)
				{
					this.Unit.Unit.View.transform.SetParent(Game.Instance.DynamicRoot, true);
				}
			}
		}
	}
}
