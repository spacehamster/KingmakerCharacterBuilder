using Harmony12;
using Kingmaker;
using Kingmaker.Assets.UI.LevelUp;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Prerequisites;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Root;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Persistence.JsonUtility;
using Kingmaker.UI.LevelUp;
using Kingmaker.UI.ServiceWindow;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Class.LevelUp;
using Kingmaker.UnitLogic.Class.LevelUp.Actions;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.View;
using Kingmaker.Visual.CharacterSystem;
using Kingmaker.Visual.Sound;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CharacterBuilder
{
    public class Test
    {
        public object SelectSpells { get; private set; }

        static void ShowDollRoom(UnitEntityData unit, bool pregenMode = true)
        {
            if (unit == null) throw new Exception("ShowDollRoom received null unit");
            var dollRoom = Game.Instance.UI.Common.DollRoom;
            if (dollRoom != null)
            {
                CharGenDollRoom component = Game.Instance.UI.Common.DollRoom.GetComponent<CharGenDollRoom>();
                if (component)
                {
                    component.CreateDolls();
                } else
                {
                    Main.Error("CharGenDollRoom is null");
                }
                dollRoom.SetUnit(unit);
                if (pregenMode)
                {
                    BlueprintUnit blueprint = unit.Blueprint;
                    UnitEntityView unitEntityView = blueprint.Prefab.Load(false);
                    if (unitEntityView != null)
                    {
                        Character characterComponent = unitEntityView.GetComponent<Character>();
                        Character character = dollRoom.CreateAvatar(characterComponent, blueprint.name);
                        character.AnimationManager.IsInCombat = false;
                        dollRoom.SetAvatar(character);
                    } else
                    {
                        Main.Error("ShowDollRoom.unitEntityView is null");
                    }
                }
                //dollRoom.Show(true);
                if (dollRoom.Unit == null) Main.Error("Failed to set DollRoom.Unit");
                if (dollRoom.GetAvatar() == null) Main.Error("Failed to set DollRoom.Avatar");
            } else
            {
                Main.Error("Game.Instance.UI.Common.DollRoom is null");
            }
        }
        static UnitEntityData CreateTestUnit()
        {
            var chargen = Traverse.Create(Game.Instance.UI.MainMenu).Field("m_ChargetUnit").GetValue<ChargenUnit>();
            return chargen.Unit;
        }
        static UnitEntityData CreateUnit()
        {
            var unit = Game.Instance.CreateUnitVacuum(BlueprintRoot.Instance.DefaultPlayerCharacter);
            return unit;
        }
/*
* Test creating a levelplan from a class's DefaultBuild. Refer AddClassLevels.LevelUp and LevelUpController.SelectDefaultClassBuild
*/
        internal static void TestDefaultLevelPlan()
        {
            var defaultClass = ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>("0937bec61c0dabc468428f496580c721"); //Alchemist
            var defaultBuild = defaultClass.DefaultBuild;
            var addClassLevels = defaultBuild.GetComponent<AddClassLevels>();
            var targetPoints = Main.settings.DefaultPointBuy25 ? 25 : 20;
            var stats = defaultBuild.GetComponents<StatsDistributionPreset>().FirstOrDefault(sd => sd.TargetPoints == targetPoints);
            UnitEntityData unitData = Main.settings.DefaultPointBuy25 ?
                Game.Instance.CreateUnitVacuum(BlueprintRoot.Instance.DefaultPlayerCharacter) :
                Game.Instance.CreateUnitVacuum(BlueprintRoot.Instance.CustomCompanion);
            var unit = unitData.Descriptor;
            new TestAddClassLevels(addClassLevels).LevelUp(unit, 5);
            Main.Log($"unit level {unit.Progression.CharacterLevel}");
            var levelPlanHolder = new LevelPlanHolder();
            for (int i = 0; i < 20; i++)
            {
                var plan = unit.Progression.GetLevelPlan(i);
                levelPlanHolder.LevelPlanData[i] = plan;
            }
            LevelPlanManager.CurrentLevelPlan = levelPlanHolder;
        }
        internal static void TestDefaultLevelPlan2()
        {
            var defaultClass = ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>("0937bec61c0dabc468428f496580c721"); //Alchemist
            var defaultBuild = defaultClass.DefaultBuild;
            var addClassLevels = defaultBuild.GetComponent<AddClassLevels>();
            var targetPoints = Main.settings.DefaultPointBuy25 ? 25 : 20;
            var stats = defaultBuild.GetComponents<StatsDistributionPreset>().FirstOrDefault(sd => sd.TargetPoints == targetPoints);
            UnitEntityData unitData = Main.settings.DefaultPointBuy25 ?
                Game.Instance.CreateUnitVacuum(BlueprintRoot.Instance.DefaultPlayerCharacter) :
                Game.Instance.CreateUnitVacuum(BlueprintRoot.Instance.CustomCompanion);
            var unit = unitData.Descriptor;
            LogUnit("BlankUnit.txt", unit);
            var levelUpController = TestLevelUpController.Start(unit: unit, instantCommit: true, unitJson: null, onSuccess: null, mode: LevelUpState.CharBuildMode.CharGen);
            bool success = true;
            success = levelUpController.SelectPortrait(ResourcesLibrary.GetBlueprints<BlueprintPortrait>().First());
            if (!success) Main.Log("Error selecting portrait");
            success = levelUpController.SelectGender(Gender.Male);
            if (!success) Main.Log("Error selecting gender");
            var race = ResourcesLibrary.TryGetBlueprint<BlueprintRace>("0a5d473ead98b0646b94495af250fdc4"); //Human
            race = ResourcesLibrary.TryGetBlueprint<BlueprintRace>("5c4e42124dc2b4647af6e36cf2590500"); //Tiefling
            success = levelUpController.SelectRace(race);
            if (!success) Main.Log("Error selecting race");
            levelUpController.State.SelectedClass = ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>("0937bec61c0dabc468428f496580c721");
            ApplyDefaultBuild(levelUpController);
            //success = levelUpController.SelectClass(ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>("0937bec61c0dabc468428f496580c721"));
            //if (!success) Main.Log("Error selecting class");
            //levelUpController.ApplyClassMechanics();
            //levelUpController.ApplySpellbook();
            //levelUpController.SelectDefaultClassBuild();

            var levelPlanHolder = new LevelPlanHolder();
            for (int i = 0; i < 20; i++)
            {
                var plan = unit.Progression.GetLevelPlan(i+1);
                levelPlanHolder.LevelPlanData[i] = plan;
            }
            LevelPlanManager.CurrentLevelPlan = levelPlanHolder;

            var token = UnitSerialization.Serialize(unit);
            File.WriteAllText("TestDefaultAlch.json", token.ToString());
            LogUnit("TestDefaultAlch.txt", unit);
        }
        internal static void TestDefaultLevelPlan3()
        {
            var defaultClass = ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>("0937bec61c0dabc468428f496580c721"); //Alchemist
            var defaultBuild = defaultClass.DefaultBuild;
            //Note: DefaultPlayerCharacter is StartGame_Player_Unit 4391e8b9afbb0cf43aeba700c089f56d
            //CustomCompanion is CustomCompanion baaff53a675a84f4983f1e2113b24552
            UnitEntityData unitData = Main.settings.DefaultPointBuy25 ?
                Game.Instance.CreateUnitVacuum(BlueprintRoot.Instance.DefaultPlayerCharacter) :
                Game.Instance.CreateUnitVacuum(BlueprintRoot.Instance.CustomCompanion);
            var unit = unitData.Descriptor;
            var race = ResourcesLibrary.TryGetBlueprint<BlueprintRace>("0a5d473ead98b0646b94495af250fdc4"); //Human
            race = ResourcesLibrary.TryGetBlueprint<BlueprintRace>("5c4e42124dc2b4647af6e36cf2590500"); //Tiefling
            unit.Progression.SetRace(race);
            //DefaultBuildData sets a global variable DefaultBuildData that is used by AddClassLevels to determine the race to use
            //If race is not set, it defaults to  unit.Progression.Race;
            using (new DefaultBuildData(race))
            {
                DefaultBuildData data = ElementsContext.GetData<DefaultBuildData>();
                Main.Log($"Default Build Data {data.Race.name}");
                unit.Ensure<LevelUpPlanUnitHolder>();
                unit.Progression.DropLevelPlans();
                unit.AddFact(defaultBuild, null, null);
                var levelPlanHolder = new LevelPlanHolder();
                for (int i = 0; i < 20; i++)
                {
                    var plan = unit.Progression.GetLevelPlan(i + 1);
                    levelPlanHolder.LevelPlanData[i] = plan;
                }
                var levelPlan = unit.Progression.GetLevelPlan(1);
                var stats = levelPlan.Actions.Select(action =>
                {
                    if (action is AddStatPoint add) return 1;
                    if (action is RemoveStatPoint) return -1;
                    return 0;
                }).Sum();
                Main.Log($"Added {stats} points");
                LevelPlanManager.CurrentLevelPlan = levelPlanHolder;
            }


        }
        internal static void AddFeatures(UnitDescriptor unit, IList<BlueprintFeatureBase> features, BlueprintScriptableObject source, int level)
        {

        }
        static void ApplyDefaultBuild(TestLevelUpController controller)
        {
            var defaultBuild = controller.State.SelectedClass.DefaultBuild;
            BlueprintRace race = controller.Preview.Progression.Race;

            controller.Unit.Ensure<LevelUpPlanUnitHolder>();
            controller.Unit.Progression.DropLevelPlans();
            controller.Unit.AddFact(defaultBuild, null, null);
            LevelPlanData levelPlan = controller.Unit.Progression.GetLevelPlan(controller.State.NextLevel);


        }
        internal static void TestLevelUp()
        {
            var defaultClass = ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>("0937bec61c0dabc468428f496580c721"); //Alchemist
            var defaultBuild = defaultClass.DefaultBuild;
            var addClassLevels = defaultBuild.GetComponent<AddClassLevels>();
            var targetPoints = Main.settings.DefaultPointBuy25 ? 25 : 20;
            var stats = defaultBuild.GetComponents<StatsDistributionPreset>().FirstOrDefault(sd => sd.TargetPoints == targetPoints);
            UnitEntityData unitData = Main.settings.DefaultPointBuy25 ?
                Game.Instance.CreateUnitVacuum(BlueprintRoot.Instance.DefaultPlayerCharacter) :
                Game.Instance.CreateUnitVacuum(BlueprintRoot.Instance.CustomCompanion);
            var unit = unitData.Descriptor;
            bool success = false;
            var levelUpController = TestLevelUpController.Start(unit: unit, instantCommit: true, unitJson: null, onSuccess: null, mode: LevelUpState.CharBuildMode.CharGen);
            success = levelUpController.SelectPortrait(ResourcesLibrary.GetBlueprints<BlueprintPortrait>().First());
            if (!success) Main.Log("Error selecting portrait");
            success = levelUpController.SelectGender(Gender.Male);
            if (!success) Main.Log("Error selecting gender");
            success = levelUpController.SelectRace(ResourcesLibrary.TryGetBlueprint<BlueprintRace>("0a5d473ead98b0646b94495af250fdc4"));
            if (!success) Main.Log("Error selecting race");
            success = levelUpController.SelectRaceStat(Kingmaker.EntitySystem.Stats.StatType.Intelligence);
            if (!success) Main.Log("Error selecting race stat");
            success = levelUpController.SelectClass(ResourcesLibrary.TryGetBlueprint<BlueprintCharacterClass>("0937bec61c0dabc468428f496580c721"));
            if (!success) Main.Log("Error selecting class");
            levelUpController.ApplyClassMechanics();
            levelUpController.ApplySpellbook();
            success = levelUpController.RemoveStatPoint(Kingmaker.EntitySystem.Stats.StatType.Charisma);
            success &= levelUpController.RemoveStatPoint(Kingmaker.EntitySystem.Stats.StatType.Charisma);
            success &= levelUpController.RemoveStatPoint(Kingmaker.EntitySystem.Stats.StatType.Charisma);
            success &= levelUpController.AddStatPoint(Kingmaker.EntitySystem.Stats.StatType.Constitution);
            success &= levelUpController.AddStatPoint(Kingmaker.EntitySystem.Stats.StatType.Constitution);
            success &= levelUpController.AddStatPoint(Kingmaker.EntitySystem.Stats.StatType.Dexterity);
            success &= levelUpController.AddStatPoint(Kingmaker.EntitySystem.Stats.StatType.Dexterity);
            success &= levelUpController.AddStatPoint(Kingmaker.EntitySystem.Stats.StatType.Dexterity);
            success &= levelUpController.AddStatPoint(Kingmaker.EntitySystem.Stats.StatType.Dexterity);
            success &= levelUpController.AddStatPoint(Kingmaker.EntitySystem.Stats.StatType.Dexterity);
            success &= levelUpController.AddStatPoint(Kingmaker.EntitySystem.Stats.StatType.Dexterity);
            success &= levelUpController.AddStatPoint(Kingmaker.EntitySystem.Stats.StatType.Intelligence);
            success &= levelUpController.AddStatPoint(Kingmaker.EntitySystem.Stats.StatType.Intelligence);
            success &= levelUpController.AddStatPoint(Kingmaker.EntitySystem.Stats.StatType.Intelligence);
            success &= levelUpController.AddStatPoint(Kingmaker.EntitySystem.Stats.StatType.Intelligence);
            success &= levelUpController.AddStatPoint(Kingmaker.EntitySystem.Stats.StatType.Intelligence);
            success &= levelUpController.AddStatPoint(Kingmaker.EntitySystem.Stats.StatType.Intelligence);
            success &= levelUpController.AddStatPoint(Kingmaker.EntitySystem.Stats.StatType.Intelligence);
            success &= levelUpController.AddStatPoint(Kingmaker.EntitySystem.Stats.StatType.Intelligence);
            success &= levelUpController.AddStatPoint(Kingmaker.EntitySystem.Stats.StatType.Intelligence);
            if (!success) Main.Log("Error selecting stats");
            success = levelUpController.SpendSkillPoint(Kingmaker.EntitySystem.Stats.StatType.SkillUseMagicDevice);
            success &= levelUpController.SpendSkillPoint(Kingmaker.EntitySystem.Stats.StatType.SkillKnowledgeWorld);
            success &= levelUpController.SpendSkillPoint(Kingmaker.EntitySystem.Stats.StatType.SkillPerception);
            success &= levelUpController.SpendSkillPoint(Kingmaker.EntitySystem.Stats.StatType.SkillThievery);
            success &= levelUpController.SpendSkillPoint(Kingmaker.EntitySystem.Stats.StatType.SkillStealth);
            success &= levelUpController.SpendSkillPoint(Kingmaker.EntitySystem.Stats.StatType.SkillLoreNature);
            success &= levelUpController.SpendSkillPoint(Kingmaker.EntitySystem.Stats.StatType.SkillKnowledgeArcana);
            if (!success) Main.Log("Error selecting skills");
            var selection = ResourcesLibrary.TryGetBlueprint<BlueprintFeatureSelection>("247a4068296e8be42890143f451b4b45"); //BasicFeatSelection
            var featurePointBlack = ResourcesLibrary.TryGetBlueprint<BlueprintFeature>("0da0c194d6e1d43419eb8d990b28e0ab"); //PointBlankFeature
            success = levelUpController.SelectFeature(new FeatureSelectionState(null, null, selection, 0, 0), featurePointBlack);
            if (!success) Main.Log("Error selecting point blank");
            var featurePreciseShot = ResourcesLibrary.TryGetBlueprint<BlueprintFeature>("8f3d1e6b4be006f4d896081f2f889665"); //PreciseShotFeature
            success = levelUpController.SelectFeature(new FeatureSelectionState(null, null, selection, 1, 0), featurePreciseShot);
            if (!success) Main.Log("Error selecting precise shot");
            var spells = new List<string>()
            {
                "4f8181e7a7f1d904fbaea64220e83379",
                "5590652e1c2225c4ca30c4a699ab3649",
                "4e0e9aba6447d514f88eff1464cc4763",
                "ef768022b0785eb43a18969903c537c4",
                "2c38da66e5a599347ac95b3294acbe00",
                "9d504f8dff6e93b4ab6afc938ed6a23d",
                "24afb2c948c731440a3aaf5411904c89",
                "c60969e7f264e6d4b84a1499fdcf9039",
            };
            var alchemistSpellBook = ResourcesLibrary.TryGetBlueprint<BlueprintSpellbook>("fcbf2a5447624528a3f333bced844d06");
            var alchemistSpellList = ResourcesLibrary.TryGetBlueprint<BlueprintSpellList>("f60d0cd93edc65c42ad31e34a905fb2f");
            success = true;
            for (var i = 0; i < spells.Count; i++)
            {
                var spell = ResourcesLibrary.TryGetBlueprint<BlueprintAbility>(spells[i]);
                success &= levelUpController.SelectSpell(alchemistSpellBook, alchemistSpellList, 1, spell, i);
            }
            if (!success) Main.Log("Error selecting spells");
            success = levelUpController.SelectVoice(ResourcesLibrary.TryGetBlueprint<BlueprintUnitAsksList>("e7b22776ba8e2b84eaaff98e439639a7"));
            if (!success) Main.Log("Error selecting voice");
            success = levelUpController.SelectName("ABC");
            if (!success) Main.Log("Error selecting name");
            var token = UnitSerialization.Serialize(unit);
            File.WriteAllText("TestUnit.json", token.ToString());
            LogUnit("TestUnit.txt", unit);
        }
        static void LogUnit(string path, UnitDescriptor unit)
        {
            using (var sw = new StreamWriter(path))
            {
                sw.WriteLine($"Unit: {unit.CharacterName}({unit.Blueprint.name})");
                sw.WriteLine($"Stats: {unit.Stats.Strength}, {unit.Stats.Dexterity}, {unit.Stats.Constitution}, {unit.Stats.Intelligence}, {unit.Stats.Wisdom}, {unit.Stats.Charisma}");
                sw.WriteLine($"Level: {unit.Progression.CharacterLevel}");
                sw.WriteLine($"Race: {unit.Progression.Race}");
                sw.WriteLine($"Gender: {unit.Gender} CustomGender: {unit.CustomGender}");
                foreach (var _class in unit.Progression.Classes)
                {
                    var archetypes = string.Join(", ", _class.Archetypes.Select(a => a.name));
                    sw.WriteLine($"Class: {_class.CharacterClass.name} level {_class.Level} Archetypes {archetypes}");
                }
                foreach (var feature in unit.Progression.Features)
                {
                    sw.WriteLine($"Feature: {feature.Blueprint?.name ?? "Null"} source {feature.Source?.name ?? "Null"}");
                }
                foreach (var spellbook in unit.Spellbooks)
                {
                    foreach (var ability in spellbook.GetAllKnownSpells())
                    {
                        sw.WriteLine($"Spell: {ability.Blueprint.name} spellbook {spellbook.Blueprint.name} level {ability.SpellLevel}");
                    }
                }
                foreach (var feature in unit.Abilities)
                {
                    sw.WriteLine($"Ability: {feature.Blueprint.name} spellbook {feature.Spellbook?.Blueprint.name ?? "Null"}");
                }
                var armorProficiencies = string.Join(", ", unit.Proficiencies.ArmorProficiencies);
                var weaponProficiencies = string.Join(", ", unit.Proficiencies.WeaponProficiencies);
                sw.WriteLine($"ArmorProficiencies: {armorProficiencies}");
                sw.WriteLine($"WeaponProficiencies: {weaponProficiencies}");
                for (int i = 1; i <= 20; i++)
                {
                    var plan = unit.Progression.GetLevelPlan(i);
                    var planText = plan == null ? "null" : $"level {plan.Level} entries {plan.Actions.Count()}";
                    sw.WriteLine($"LevelPlan: index {i} {planText}");
                }
            }
        }
        /*
        * Refer MainMenu.StartChargen which also calls CharacterBuildController.HandleLevelUpStart
        * 
        */
        internal static void TestLevelup()
        {
            var unit = CreateUnit();
            var descriptorJson = UnitSerialization.Serialize(unit.Descriptor); ;
            if (Main.settings.ShowDollRoom) ShowDollRoom(unit);
            CharacterBuildController characterBuildController = Game.Instance.UI.CharacterBuildController;
            LevelPlanManager.CurrentLevelUpController = LevelUpController.Start(
                unit: unit.Descriptor,
                instantCommit: false,
                unitJson: descriptorJson,
                onSuccess: null,
                mode: LevelUpState.CharBuildMode.CharGen);
            /*CurrentLevelUpController.SelectPortrait(Game.Instance.BlueprintRoot.CharGen.Portraits[0]);
            CurrentLevelUpController.SelectGender(Gender.Male);
            CurrentLevelUpController.SelectRace(Game.Instance.BlueprintRoot.Progression.CharacterRaces[0]);
            CurrentLevelUpController.SelectAlignment(Kingmaker.Enums.Alignment.TrueNeutral);
            CurrentLevelUpController.SelectVoice(Game.Instance.BlueprintRoot.CharGen.MaleVoices[0]);
            CurrentLevelUpController.SelectName("LevelPlan");*/
            Traverse.Create(characterBuildController).Property<LevelUpController>("LevelUpController").Value = LevelPlanManager.CurrentLevelUpController;
            characterBuildController.Unit = unit.Descriptor;
            characterBuildController.Show(true);
        }
        [HarmonyPatch(typeof(UberLogger.Logger), "Log")]
        static class Logger_Log_Patch
        {
            static void Log(UberLogger.LogInfo logInfo)
            {
                var IncludeCallStacks = true;
                using (var sw = new StringWriter())
                {
                    sw.Write("[");
                    sw.Write(logInfo.GetTimeStampAsString());
                    if (!string.IsNullOrEmpty(logInfo.Channel))
                    {
                        sw.Write(" - ");
                        sw.Write(logInfo.Channel);
                    }
                    sw.Write("]: ");
                    sw.WriteLine(logInfo.Message);
                    if (IncludeCallStacks && logInfo.Callstack.Count > 0)
                    {
                        foreach (var logStackFrame in logInfo.Callstack)
                        {
                            sw.WriteLine(logStackFrame.GetFormattedMethodName());
                        }
                        sw.WriteLine();
                    }
                    Main.Log(sw.ToString());
                }
            }
            static void Postfix(string channel, UnityEngine.Object source, UberLogger.LogSeverity severity, Exception ex, object message, params object[] par)
            {
                return;
                if (channel == "UI") return;
                var callstack = new List<UberLogger.LogStackFrame>();
                if (severity != UberLogger.LogSeverity.Message)
                {
                    if (ex != null)
                    {
                        callstack = ex.StackTrace.Split(new string[] { "\n" }, StringSplitOptions.None)
                            .Select(l => new UberLogger.LogStackFrame(l, string.Empty, 0))
                            .ToList<UberLogger.LogStackFrame>();
                    }
                    else
                    {
                        callstack = new List<UberLogger.LogStackFrame>();
                    }
                }
                Log(new UberLogger.LogInfo(source, channel, severity, callstack, message, par));
            }
        }
    }
}
