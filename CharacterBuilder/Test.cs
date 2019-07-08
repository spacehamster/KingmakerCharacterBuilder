using Harmony12;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Root;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Persistence.JsonUtility;
using Kingmaker.UI.LevelUp;
using Kingmaker.UI.ServiceWindow;
using Kingmaker.UnitLogic.Class.LevelUp;
using Kingmaker.View;
using Kingmaker.Visual.CharacterSystem;
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
