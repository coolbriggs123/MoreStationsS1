using MelonLoader;
using UnityEngine;
using Il2CppScheduleOne.Employees;
using System.IO;
using Il2CppSystem.Collections.Generic;
using Il2CppScheduleOne.Management;
using HarmonyLib;
using System.Reflection;

[assembly: MelonInfo(typeof(BotanistEnhanced.Core), "BotanistEnhanced", "1.0.0", "rbawe", null)]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace BotanistEnhanced
{
    public class Core : MelonMod
    {
        private Il2CppSystem.Collections.Generic.Dictionary<int, bool> processedBotanists;
        private HarmonyLib.Harmony harmony;

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg($"BotanistEnhanced Starting...");
            processedBotanists = new Il2CppSystem.Collections.Generic.Dictionary<int, bool>();
            harmony = new HarmonyLib.Harmony("com.rbawe.botanistenhanced");

            LoggerInstance.Msg("If you need any help join https://discord.gg/PCawAVnhMH");
            LoggerInstance.Msg("Happy Selling!");
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            if (sceneName != "Main") return;

            try
            {
                var botanistType = typeof(Botanist);
                var methods = botanistType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | 
                    BindingFlags.Instance | BindingFlags.Static);
                
                var initializeMethod = methods.FirstOrDefault(m => m.Name.Contains("Initialize"));

                if (initializeMethod == null)
                {
                    MelonLogger.Error("Could not find Initialize method");
                    return;
                }

                var postfix = typeof(Core).GetMethod(nameof(BotanistInitializePostfix),
                    BindingFlags.NonPublic | BindingFlags.Static);

                harmony.Patch(initializeMethod,
                    postfix: new HarmonyMethod(postfix));

                MelonLogger.Msg("Successfully patched Botanist Initialize method");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Failed to patch: {ex.Message}");
            }
        }

        private static void BotanistInitializePostfix(Botanist __instance)
        {
            try
            {
                if (__instance == null) return;

                __instance.MaxAssignedPots = 20;
                MelonLogger.Msg($"Enhanced Botanist: MaxAssignedPots set to {__instance.MaxAssignedPots}");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Error in postfix: {ex.Message}");
            }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (sceneName != "Main") return;
            processedBotanists.Clear();
        }
    }
}
