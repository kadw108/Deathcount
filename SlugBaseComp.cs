using System;
using UnityEngine;

using System.Reflection;
using MonoMod.RuntimeDetour;

using System.Collections.Generic;
using System.IO;

namespace Deathcount
{
    // Compatibility with SlugBase + custom slugcats
     class SlugBaseComp
    {
        internal static void OnEnable()
        {
            // this doesn't work, must import MonoMod.RuntimeDetour and use below code
            // On.SlugBase.SaveManager.GetCustomSaveData += SBSave_GetCustomSaveData;

            new Hook(Type.GetType("SlugBase.SaveManager, SlugBase").GetMethod("GetCustomSaveData",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
                typeof(SlugBaseComp).GetMethod("SBSave_GetDataHook",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
        }

        // reference to the original method (required)
        public delegate Menu.SlugcatSelectMenu.SaveGameData GetCustomSaveData(RainWorld rainWorld, string name, int slot);

        protected static Menu.SlugcatSelectMenu.SaveGameData SBSave_GetDataHook(SlugBaseComp.GetCustomSaveData orig, RainWorld rainWorld, string name, int slot)
        {
            Menu.SlugcatSelectMenu.SaveGameData return_data = orig(rainWorld, name, slot);
            Debug.Log("Deathcount GDH - " + name + " " + slot);

            string saveData;
            try
            {
                saveData = File.ReadAllText(SlugBase.SaveManager.GetSaveFilePath(name, slot));
            }
            catch 
            {
                Debug.Log("Deathcount GDH save read error, name: " + name + " slot: " + slot);
                return return_data;
            }

            // Mine death data
            List<SaveStateMiner.Target> list = new List<SaveStateMiner.Target>();
            list.Add(new SaveStateMiner.Target(">DEATHS", "<dpB>", "<dpA>", 50));
            List<SaveStateMiner.Result> list2 = SaveStateMiner.Mine(rainWorld, saveData, list);

            int slugcatNum = SlugBase.PlayerManager.GetCustomPlayer(name).SlugcatIndex;

            try
            {
                DeathcountMod.menuDeaths[slugcatNum] = int.Parse(list2[0].data);
                //Debug.Log("DEATHCOUNT ASSIGN: " + slugcat + " " + int.Parse(list2[0].data));
            }
            catch
            {
                Debug.Log("Deathcount GDH: failed to assign death num. Data: " + list2[0].data + " Name: " + name);
                DeathcountMod.menuDeaths[slugcatNum] = -1;
            }

            return return_data;
        }
    }
}
