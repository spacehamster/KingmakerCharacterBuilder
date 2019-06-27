using UnityEngine;
using Harmony12;
using UnityModManagerNet;
using System.Reflection;
using System;
using Kingmaker.UI.LevelUp;

namespace CharacterBuilder
{

    public class Main
    {
        public static UnityModManager.ModEntry.ModLogger logger;
        [System.Diagnostics.Conditional("DEBUG")]
        public static void DebugLog(string msg)
        {
            if(logger != null) logger.Log(msg);
        }
        public static void DebugError(Exception ex)
        {
            if (logger != null) logger.Log(ex.ToString() + "\n" + ex.StackTrace);
        }
        public static bool enabled;
        public static Settings settings;
        static void FixPatches(HarmonyInstance harmony)
        {
            var original = typeof(CharacterBuildController).GetMethod("OnShow", BindingFlags.Instance | BindingFlags.NonPublic);
            if(original == null)
            {
                DebugLog("Can't find method CharacterBuildController.OnShow");
                return;
            }
            var info = harmony.GetPatchInfo(original);
            if (info == null) {
                DebugLog("CharacterBuildController.OnShow is not patched!!!!");
                return;
            }
            harmony.Unpatch(original, HarmonyPatchType.Prefix, "Respec");
        }
        static HarmonyInstance harmony;
        static bool Load(UnityModManager.ModEntry modEntry)
        {
            try
            {
                logger = modEntry.Logger;
                settings = UnityModManager.ModSettings.Load<Settings>(modEntry);
                harmony = HarmonyInstance.Create(modEntry.Info.Id);
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                FixPatches(harmony);
                modEntry.OnToggle = OnToggle;
                modEntry.OnGUI = OnGUI;
                modEntry.OnSaveGUI = OnSaveGUI;
            }
            catch (Exception ex)
            {
                DebugError(ex);
                throw;
            }
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
        * 
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
                LevelPlanManager.OnGUI();
            } catch(Exception ex)
            {
                DebugError(ex);
                throw;
            }
        }
    }
    
}
