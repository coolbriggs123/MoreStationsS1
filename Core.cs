
using MelonLoader;
using UnityEngine;
using Il2CppScheduleOne.Employees;
using System.IO;
using Il2CppSystem.Collections.Generic;
using Il2CppScheduleOne.Management;
using static Il2CppVLB.Consts;
using HarmonyLib;
using System.Reflection;

[assembly: MelonInfo(typeof(ChemistEnhanced.Core), "ChemistEnhanced", "1.0.0", "Coolbriggs", null)]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace ChemistEnhanced
{
    public class Core : MelonMod
    {
        private Il2CppSystem.Collections.Generic.Dictionary<int, bool> processedChemists;
        private HarmonyLib.Harmony harmony;

        public override void OnInitializeMelon()
        {
            MelonLogger.Msg($"ChemistEnhanced Starting...");
            processedChemists = new Il2CppSystem.Collections.Generic.Dictionary<int, bool>();
            harmony = new HarmonyLib.Harmony("com.coolbriggs.chemistenhanced");
            
            MelonLogger.Msg("If you need any help join https://discord.gg/PCawAVnhMH");
            MelonLogger.Msg("Happy Selling!");
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            if (sceneName != "Main") return;
            
            try
            {
                var chemistType = typeof(Chemist);
                var initializeMethod = chemistType.GetMethod("Initialize", 
                    BindingFlags.Public | BindingFlags.NonPublic | 
                    BindingFlags.Instance | BindingFlags.Static);

                if (initializeMethod == null) return;

                var postfix = typeof(Core).GetMethod(nameof(ChemistInitializePostfix), 
                    BindingFlags.NonPublic | BindingFlags.Static);
                
                harmony.Patch(initializeMethod, 
                    postfix: new HarmonyMethod(postfix));
            }
            catch (System.Exception)
            {
                // Silent catch
            }
        }

        private static void ChemistInitializePostfix(Chemist __instance)
        {
            if (__instance == null) return;
            if (__instance._configuration_k__BackingField == null) return;
            if (__instance._configuration_k__BackingField.Stations == null) return;

            __instance._configuration_k__BackingField.Stations.MaxItems = 20;
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (sceneName != "Main") return;
            processedChemists.Clear();
        }
    }
}
