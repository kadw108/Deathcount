using System;
using BepInEx;
using UnityEngine;

using Menu;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Reflection;
using System.IO;

namespace Deathcount
{
    [BepInPlugin("kadw.deathcount", "Deathcount", "0.1.0")]
    public class DeathcountMod : BaseUnityPlugin
    {
        private static MenuLabel[] menuDeathLabels = new MenuLabel[10];
        public static int[] menuDeaths = new int[10];
        private static bool isSlugBaseEnabled;

        public void OnEnable()
        {
            // On.Menu.SlugcatSelectMenu.ctor += SlugcatSelectMenu_ctor;
            On.Menu.SlugcatSelectMenu.MineForSaveData += SlugcatSelectMenu_MineForSaveData;
            On.Menu.SlugcatSelectMenu.SlugcatPageContinue.ctor += SlugcatPageContinue_ctor;
            On.Menu.SlugcatSelectMenu.SlugcatPageContinue.GrafUpdate += SlugcatPageContinue_GrafUpdate;

            On.Menu.SleepAndDeathScreen.GetDataFromGame += SleepAndDeathScreen_GetDataFromGame;

            // for slugbase compatibility
            On.RainWorld.Start += RainWorld_Start;
        }

        /*
         * Check SlugBase enabled.
         */
        private static void RainWorld_Start(On.RainWorld.orig_Start orig, RainWorld rainWorld)
        {
            orig(rainWorld);

            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == "SlugBase")
                {
                    isSlugBaseEnabled = true;
                    // Debug.Log("Deathcount: SlugBase found.");
                    break;
                }
            }

            /*
            if (!isSlugBaseEnabled)
            {
                Debug.Log("Deathcount: SlugBase not found.");
            }
            */
        }

        private static void SlugcatSelectMenu_ctor(On.Menu.SlugcatSelectMenu.orig_ctor orig, SlugcatSelectMenu self, ProcessManager manager)
        {
            orig(self, manager);
            Debug.Log("DEATHCOUNT 00: " + self.saveGameData.Length);
            Debug.Log("DEATHCOUNT 01: " + self.slugcatColorOrder.Length);
            Debug.Log("DEATHCOUNT 02: " + self.pages.Count);
            Debug.Log("DEATHCOUNT 03: " + self.slugcatPages.Length);

            menuDeathLabels = new MenuLabel[self.pages.Count];
            menuDeaths = new int[self.pages.Count];
        }

        /*
         * Get the death count for each slugcat, including SlugBase custom slugcats if they exist.
         */
        private static SlugcatSelectMenu.SaveGameData SlugcatSelectMenu_MineForSaveData(On.Menu.SlugcatSelectMenu.orig_MineForSaveData orig, ProcessManager manager, int slugcat)
        {
            SlugcatSelectMenu.SaveGameData returnData = orig(manager, slugcat);
            // Debug.Log("Deathcount MFSD " + slugcat + " " + SlugBase.PlayerManager.GetCustomPlayer(slugcat));

            if (returnData != null) {
                if (isSlugBaseEnabled)
                {
                    SlugBase.SlugBaseCharacter ply = SlugBase.PlayerManager.GetCustomPlayer(slugcat);
                    if (ply != null)
                    {
                        SaveState save = manager.rainWorld.progression.currentSaveState;
                        if (save != null && save.saveStateNumber == slugcat)
                        {
                            menuDeaths[slugcat] = save.deathPersistentSaveData.deaths;
                            return returnData;
                        }

                        int slot = manager.rainWorld.options.saveSlot;
                        string progLinesCustomSlug;
                        try
                        {
                            progLinesCustomSlug = File.ReadAllText(SlugBase.SaveManager.GetSaveFilePath(ply.Name, slot));
                        }
                        catch
                        {
                            // most likely there is no save file for this slugcat
                            // Debug.Log("Deathcount GDH save read error, name: " + ply.Name + " slot: " + slot);
                            return returnData;
                        }

                        List<SaveStateMiner.Target> list = new List<SaveStateMiner.Target>();
                        list.Add(new SaveStateMiner.Target(">DEATHS", "<dpB>", "<dpA>", 50));
                        List<SaveStateMiner.Result> list2 = SaveStateMiner.Mine(manager.rainWorld, progLinesCustomSlug, list);

                        try
                        {
                            DeathcountMod.menuDeaths[slugcat] = int.Parse(list2[0].data);
                            // Debug.Log("DEATHCOUNT ASSIGN: " + slugcat + " " + int.Parse(list2[0].data));
                        }
                        catch
                        {
                            Debug.Log("Deathcount custom slug: failed to assign death num. Data: " + list2[0].data);
                            DeathcountMod.menuDeaths[slugcat] = -1;
                        }

                        return returnData;
                    }
                }

                string[] progLines = manager.rainWorld.progression.GetProgLines();
                for (int i = 0; i < progLines.Length; i++)
                {
                    string[] array = Regex.Split(progLines[i], "<progDivB>");
                    if (array.Length == 2 && array[0] == "SAVE STATE" && int.Parse(array[1][21].ToString()) == slugcat)
                    {
                        List<SaveStateMiner.Target> list = new List<SaveStateMiner.Target>();
                        list.Add(new SaveStateMiner.Target(">DEATHS", "<dpB>", "<dpA>", 50));
                        List<SaveStateMiner.Result> list2 = SaveStateMiner.Mine(manager.rainWorld, array[1], list);

                        try
                        {
                            menuDeaths[slugcat] = int.Parse(list2[0].data);
                            // Debug.Log("DEATHCOUNT ASSIGN: " + slugcat + " " + int.Parse(list2[0].data));
                            break;
                        }
                        catch
                        {
                            Debug.Log("Deathcount vanilla slug: failed to assign death num. Data: " + list2[0].data);
                            menuDeaths[slugcat] = -1;
                        }
                    }
                }
            }

            return returnData;
        }

        /*
         * Create deathcount label for each slugcat menu page, and update it with the other lables.
         */
        private static void SlugcatPageContinue_ctor(On.Menu.SlugcatSelectMenu.SlugcatPageContinue.orig_ctor orig, SlugcatSelectMenu.SlugcatPageContinue self, Menu.Menu menu, MenuObject owner, int pageIndex, int slugcatNumber)
        {
            orig(self, menu, owner, pageIndex, slugcatNumber);

            //Debug.Log("DEATHCOUNT MOD: " + self.SlugcatPageIndex + " " + self.slugcatNumber);
            menuDeathLabels[self.slugcatNumber] = new MenuLabel(menu, self, "Deaths: " + menuDeaths[self.slugcatNumber], new Vector2(-1000f, self.imagePos.y - 405f), new Vector2(), false);
            menuDeathLabels[self.slugcatNumber].label.alignment = FLabelAlignment.Left;
            self.subObjects.Add(menuDeathLabels[self.slugcatNumber]);
        }
        private static void SlugcatPageContinue_GrafUpdate(On.Menu.SlugcatSelectMenu.SlugcatPageContinue.orig_GrafUpdate orig, SlugcatSelectMenu.SlugcatPageContinue self, float timeStacker)
        {
            orig(self, timeStacker);

            float num = self.Scroll(timeStacker);
            float alpha = self.UseAlpha(timeStacker);
            menuDeathLabels[self.slugcatNumber].label.alpha = alpha;
            menuDeathLabels[self.slugcatNumber].label.x = self.MidXpos + num * self.ScrollMagnitude + 115f;
            menuDeathLabels[self.slugcatNumber].label.color = Menu.Menu.MenuRGB(Menu.Menu.MenuColors.MediumGrey);
        } 

        /*
         * Add death count to sleep/death screen.
         */
        private static void SleepAndDeathScreen_GetDataFromGame(On.Menu.SleepAndDeathScreen.orig_GetDataFromGame orig, SleepAndDeathScreen self, KarmaLadderScreen.SleepDeathScreenDataPackage package)
        {
            orig(self, package);

            int deaths = package.saveState.deathPersistentSaveData.deaths;

            MenuLabel deathcounter = new MenuLabel(self, self.pages[0], "Deaths: " + deaths, new Vector2(75f, 75f), new Vector2(), false);
            deathcounter.label.alignment = FLabelAlignment.Left;
            deathcounter.label.color = Menu.Menu.MenuRGB(Menu.Menu.MenuColors.MediumGrey);
            self.pages[0].subObjects.Add(deathcounter);
        }
      }
}
