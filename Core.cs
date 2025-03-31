
using MelonLoader;
using UnityEngine;
using Il2CppScheduleOne.Employees;
using System.IO;
using Il2CppSystem.Collections.Generic;
using Il2CppScheduleOne.Management;
using static Il2CppVLB.Consts;

[assembly: MelonInfo(typeof(ChemistEnhanced.Core), "ChemistEnhanced", "1.0.0", "Coolbriggs", null)]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace ChemistEnhanced
{


    public class Core : MelonMod
    {
        private Chemist chemistComponent;

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (sceneName != "Main")
                return;

            MelonCoroutines.Start(DelayedInit());
        }

        private System.Collections.IEnumerator DelayedInit()
        {
            yield return new WaitForSeconds(2f);  // Wait for 2 seconds

            var chemistObject = GameObject.Find("Chemist(Clone)");
            if (chemistObject == null)
            {
                LoggerInstance.Error("Could not find Chemist object");
                yield break;
            }

            chemistComponent = chemistObject.GetComponent<Chemist>();
            if (chemistComponent == null)
            {
                LoggerInstance.Error("Could not find Chemist component");
                yield break;
            }

            MaxItems();
        }

        private void MaxItems()
        {
            if (chemistComponent == null || chemistComponent._configuration_k__BackingField == null) 
                return;

            var stations = chemistComponent._configuration_k__BackingField.Stations;
            stations.MaxItems = 20;
            LoggerInstance.Msg($"Max Items Patched");
            LoggerInstance.Msg($"If you need any help join https://discord.gg/PCawAVnhMH");
            LoggerInstance.Msg($"Happy Selling!");
        }

        public override void OnPreferencesSaved()
        {
            MaxItems();
        }
    }
}
