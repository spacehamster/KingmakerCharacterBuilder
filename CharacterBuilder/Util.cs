using Harmony12;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.EntitySystem.Persistence.JsonUtility;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Class.LevelUp.Actions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters;
using UnityEngine;

namespace CharacterBuilder
{
    class Util
    {
        public static GUIStyle DisabledButtonStyle;
        public static GUIStyle BoldLabelStyle;
        static Util()
        {
            DisabledButtonStyle = new GUIStyle(GUI.skin.button);
            DisabledButtonStyle.active.textColor = Color.gray;
            DisabledButtonStyle.focused = DisabledButtonStyle.active;
            DisabledButtonStyle.normal = DisabledButtonStyle.active;
            DisabledButtonStyle.hover = DisabledButtonStyle.active;

            BoldLabelStyle = new GUIStyle(GUI.skin.label);
            BoldLabelStyle.fontStyle = FontStyle.Bold;
        }
        private static List<BlueprintUnit> m_Companions;
        public static IList<BlueprintUnit> GetCompanionUnits()
        {
            if(m_Companions == null)
            {
                m_Companions = new List<BlueprintUnit>();
                foreach(var unit in ResourcesLibrary.GetBlueprints<BlueprintUnit>())
                {
                    if (unit.IsCompanion) m_Companions.Add(unit);
                }
            }
            return m_Companions;
        }
        public static void Dump(object obj, string path)
        {
            var JsonSettings = new JsonSerializerSettings
            {
                TypeNameAssemblyFormat = FormatterAssemblyStyle.Simple,
                Formatting = Formatting.Indented,
                Converters = DefaultJsonSettings.CommonConverters.ToList<JsonConverter>(),
                ContractResolver = new OptInContractResolver(),
                TypeNameHandling = TypeNameHandling.Auto,
                PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                DefaultValueHandling = DefaultValueHandling.Include,
                ReferenceLoopHandling = ReferenceLoopHandling.Serialize
            };
            var serializer = JsonSerializer.Create(JsonSettings);
            using (var file = new StreamWriter(path))
            using (JsonWriter writer = new JsonTextWriter(file))
            {
                serializer.Serialize(writer, obj);
            }
        }
        public static void SaveLevelingPlan(LevelPlanHolder levelPlan)
        {
            string invalid = new string(Path.GetInvalidFileNameChars());
            foreach (char c in invalid)
            {
                levelPlan.Name = levelPlan.Name.Replace(c.ToString(), "");
            }
            if (string.IsNullOrEmpty(levelPlan.Name))
            {
                levelPlan.Name = "LevelPlan";
            }
            Dump(levelPlan.LevelPlanData, $"Mods/CharacterBuilder/LevelPlans/{levelPlan.Name}.json");
        }
        public static LevelPlanHolder LoadLevelingPlan(string filepath)
        {
            var JsonSettings = new JsonSerializerSettings
            {
                TypeNameAssemblyFormat = FormatterAssemblyStyle.Simple,
                Formatting = Formatting.Indented,
                Converters = DefaultJsonSettings.CommonConverters.ToList<JsonConverter>(),
                ContractResolver = new OptInContractResolver(),
                TypeNameHandling = TypeNameHandling.Auto,
                PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                DefaultValueHandling = DefaultValueHandling.Include,
                ReferenceLoopHandling = ReferenceLoopHandling.Serialize
            };
            var serializer = JsonSerializer.Create(JsonSettings);
            using (StreamReader sr = new StreamReader(filepath))
            using (JsonTextReader reader = new JsonTextReader(sr))
            {
                LevelPlanData[] data = serializer.Deserialize<LevelPlanData[]>(reader);
                var levelPlanHolder = new LevelPlanHolder
                {
                    LevelPlanData = data,
                    Name = Path.GetFileNameWithoutExtension(filepath)
                };
                return levelPlanHolder;
            }
        }
        public static string MakeActionReadable(ILevelUpAction action)
        {
            var result = "";
            result += action.GetType().Name;
            if (action is AddArchetype addArchtype)
            {
                result += $"({addArchtype.Archetype.Name})";
            }
            if (action is AddStatPoint addStatPoint)
            {
                result += $"({addStatPoint.Attribute})";
            }
            if (action is ApplyClassMechanics applyClassMechanics)
            {
                result += $"()";
            }
            if (action is ApplySkillPoints applySkillPoints)
            {
                result += $"()";
            }
            if (action is ApplySpellbook applySpellbook)
            {
                result += $"()";
            }
            if (action is RemoveStatPoint removeStatPoint)
            {
                result += $"({removeStatPoint.Attribute})";
            }
            if (action is SelectAlignment selectAlignment)
            {
                result += $"({selectAlignment.Alignment})";
            }
            if (action is SelectClass selectClass)
            {
                var m_CharacterClass = Traverse.Create(selectClass).Field("m_CharacterClass").GetValue<BlueprintCharacterClass>();
                result += $"({m_CharacterClass.Name})";
            }
            if (action is SelectFeature selectFeature)
            {
                var selection = selectFeature.Selection;
                var item = selectFeature.Item;
                result += $"({selection}, {item})";
            }
            if (action is SelectGender selectGender)
            {
                result += $"({selectGender.Gender})";
            }
            if (action is SelectName selectName)
            {
                result += $"({selectName.Name})";
            }
            if (action is SelectPortrait selectPortrait)
            {
                result += $"({selectPortrait.Portrait.name})";
            }
            if (action is SelectRace selectRace)
            {
                result += $"({selectRace.Race.Name})";
            }
            if (action is SelectRaceStat selectRaceStat)
            {
                result += $"({selectRaceStat.Attribute})";
            }
            if (action is SelectSpell selectSpell)
            {
                result += $"({selectSpell.Spellbook.Name}, {selectSpell.SpellList.name}, {selectSpell.SpellLevel}, {selectSpell.Spell.Name}, {selectSpell.SlotIndex})";
            }
            if (action is SelectVoice selectVoice)
            {
                result += $"({selectVoice.Voice.DisplayName})";
            }
            if (action is SpendAttributePoint spendAttributePoint)
            {
                result += $"({spendAttributePoint.Attribute})";
            }
            if (action is SpendSkillPoint spendSkillPoint)
            {
                result += $"({spendSkillPoint.Skill})";
            }
            result += ", " + action.Priority;
            return result;
        }
    }
}
