using UnityEngine;
using Harmony12;
using UnityModManagerNet;
using System.Reflection;
using System;
using Kingmaker.UI.LevelUp;
using Kingmaker;
using Kingmaker.Blueprints.Root;
using System.IO;
using System.Collections.Generic;
using System.Linq;
namespace CharacterBuilder
{
#if DEBUG
    [EnableReloading]
#endif
    public class Main
    {
        public static UnityModManager.ModEntry.ModLogger logger;
        [System.Diagnostics.Conditional("DEBUG")]
        public static void Log(string msg)
        {
            if(logger != null) logger.Log(msg);
        }
        public static void Error(Exception ex)
        {
            if (logger != null) logger.Log(ex.ToString() + "\n" + ex.StackTrace);
        }
        public static void Error(string msg)
        {
            if (logger != null) logger.Error(msg);
        }
        public static bool enabled;
        public static Settings settings;
        static bool Load(UnityModManager.ModEntry modEntry)
        {
            try
            {
                logger = modEntry.Logger;
                settings = UnityModManager.ModSettings.Load<Settings>(modEntry);
                var harmony = HarmonyInstance.Create(modEntry.Info.Id);
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                //FixPatches(harmony);
                modEntry.OnToggle = OnToggle;
                modEntry.OnGUI = OnGUI;
                modEntry.OnSaveGUI = OnSaveGUI;
#if DEBUG
                modEntry.OnUnload = Unload;
#endif
            }
            catch (Exception ex)
            {
                Error(ex);
                throw;
            }
            return true;
        }

        static bool Unload(UnityModManager.ModEntry modEntry)
        {
            HarmonyInstance.Create(modEntry.Info.Id).UnpatchAll(modEntry.Info.Id);
            return true;
        }

        static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            settings.Save(modEntry);
        }
        // Called when the mod is turned to on/off.
        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value /* active or inactive */)
        {
            enabled = value;
            return true; // Permit or not.
        }
        /*
        * Refer 
        * CharacterBuildController
        * LevelUpController
        * LevelUpState
        * CharBPhaseSkills
        * NewGameWinPhasePregen
        */
        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            try
            {
                if (!enabled) return;
                if(GUILayout.Button("Test Default Level Plan"))
                {
                    Test.TestDefaultLevelPlan();
                }
                if (GUILayout.Button("Test Default Level Plan2"))
                {
                    Test.TestDefaultLevelPlan2();
                }
                if (GUILayout.Button("Test Default Level Plan3"))
                {
                    Test.TestDefaultLevelPlan3();
                }
                if (GUILayout.Button("Test Leveling Up"))
                {
                    Test.TestLevelUp();
                }
                CharacterBuilderGUI.OnGUI();
            } catch(Exception ex)
            {
                Error(ex);
                GUILayout.Label("Error rendering UI");
            }
        }
        static void FixPatches(HarmonyInstance harmony)
        {
            var original = typeof(CharacterBuildController).GetMethod("OnShow", BindingFlags.Instance | BindingFlags.NonPublic);
            if (original == null)
            {
                Log("Can't find method CharacterBuildController.OnShow");
                return;
            }
            var info = harmony.GetPatchInfo(original);
            if (info == null)
            {
                Log("CharacterBuildController.OnShow is not patched!!!!");
                return;
            }
            harmony.Unpatch(original, HarmonyPatchType.Prefix, "Respec");
        }
    }
}
