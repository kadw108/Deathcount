using System;
using BepInEx;
using UnityEngine;

// Mod-specific includes
using Menu;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace Deathcount
{
    [BepInPlugin("kadw.deathcount", "Deathcount", "1.0")]
    public class DeathcountMod : BaseUnityPlugin
    {
        private static MenuLabel[] menuDeathLabels = null;
        public static int[] menuDeaths = null;
        // public static bool isSlugBaseEnabled = false; // SlugBase doesn't exist for Remix/Downpour yet, compatability is disabled

        public void OnEnable()
        {
            On.Menu.SlugcatSelectMenu.SetSlugcatColorOrder += SlugcatSelectMenu_SetSlugcatColorOrder;
            On.Menu.SlugcatSelectMenu.MineForSaveData += SlugcatSelectMenu_MineForSaveData;
            On.Menu.SlugcatSelectMenu.SlugcatPageContinue.ctor += SlugcatPageContinue_ctor;
            On.Menu.SlugcatSelectMenu.SlugcatPageContinue.GrafUpdate += SlugcatPageContinue_GrafUpdate;

            On.Menu.SleepAndDeathScreen.GetDataFromGame += SleepAndDeathScreen_GetDataFromGame;

            // for slugbase compatibility
            // On.RainWorld.Start += RainWorld_Start;
            Debug.Log("Deathcount mod running");
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
                    // isSlugBaseEnabled = true;
                    // Debug.Log("Deathcount: SlugBase found.");
                    return;
                }
            }

            // Debug.Log("Deathcount: SlugBase not found.");
        }

        /*
         * Initialize menuDeaths and menuDeathLabels with the number of existing slugcats.
         */
        private static void SlugcatSelectMenu_SetSlugcatColorOrder(On.Menu.SlugcatSelectMenu.orig_SetSlugcatColorOrder orig, SlugcatSelectMenu self)
        {
            orig(self);

            // Each slugcat gets own death number + label, corresponding to number of pages in menu
            // SetSlugcatColorOrder is called before MineForSaveData, so menuDeathLabels and menuDeaths are never null during the MineForSaveData hook
            int numOfSlugcats = self.slugcatColorOrder.Count;
            menuDeathLabels = new MenuLabel[numOfSlugcats];
            menuDeaths = new int[numOfSlugcats];
        }

        /*
         * Get the death count for each slugcat, including SlugBase custom slugcats if they exist.
         */
        private static SlugcatSelectMenu.SaveGameData SlugcatSelectMenu_MineForSaveData(On.Menu.SlugcatSelectMenu.orig_MineForSaveData orig, ProcessManager manager, SlugcatStats.Name slugcat)
        {
            // Debug.Log("Deathcount Slugcat Select Menu MineForSaveData");
            SlugcatSelectMenu.SaveGameData returnData = orig(manager, slugcat);

            // Debug.Log("slugcat num " + slugcat + (int) slugcat);

            if (returnData != null) {

                string[] progLines = manager.rainWorld.progression.GetProgLinesFromMemory();
                for (int i = 0; i < progLines.Length; i++)
                {
                    string[] array = Regex.Split(progLines[i], "<progDivB>");
                    if (array.Length == 2 && array[0] == "SAVE STATE" && BackwardsCompatibilityRemix.ParseSaveNumber(array[1]) == slugcat)
                    {
                        List<SaveStateMiner.Target> list = new List<SaveStateMiner.Target>();
                        list.Add(new SaveStateMiner.Target(">DEATHS", "<dpB>", "<dpA>", 50));
                        List<SaveStateMiner.Result> list2 = SaveStateMiner.Mine(manager.rainWorld, array[1], list);

                        try
                        {
                            menuDeaths[(int) slugcat] = int.Parse(list2[0].data);
                            // Debug.Log("DEATHCOUNT ASSIGN: " + slugcat + " " + int.Parse(list2[0].data));
                            break;
                        }
                        catch
                        {
                            Debug.Log("Deathcount vanilla: failed to assign death num. Slugcat/Data: " + slugcat + " " + (int) slugcat + ", " + list2[0].data);
                            menuDeaths[(int) slugcat] = -1;
                        }
                    }
                }
            }

            return returnData;
        }

        /*
         * Create deathcount label for each slugcat menu page, and update it with the other labels.
         */
        private static void SlugcatPageContinue_ctor(On.Menu.SlugcatSelectMenu.SlugcatPageContinue.orig_ctor orig, SlugcatSelectMenu.SlugcatPageContinue self, Menu.Menu menu, MenuObject owner, int pageIndex, SlugcatStats.Name slugcatNumber)
        {
            orig(self, menu, owner, pageIndex, slugcatNumber);

            // Debug.Log("DEATHCOUNT MOD: " + self.SlugcatPageIndex + " " + self.slugcatNumber + " " + (int) self.slugcatNumber);
            menuDeathLabels[(int) self.slugcatNumber] = new MenuLabel(menu, self, "Deaths: " + menuDeaths[(int) self.slugcatNumber], new Vector2(-1000f, self.imagePos.y - 405f), new Vector2(), false);
            menuDeathLabels[(int) self.slugcatNumber].label.alignment = FLabelAlignment.Left;
            self.subObjects.Add(menuDeathLabels[(int) self.slugcatNumber]);
        }
        private static void SlugcatPageContinue_GrafUpdate(On.Menu.SlugcatSelectMenu.SlugcatPageContinue.orig_GrafUpdate orig, SlugcatSelectMenu.SlugcatPageContinue self, float timeStacker)
        {
            orig(self, timeStacker);

            float num = self.Scroll(timeStacker);
            float alpha = self.UseAlpha(timeStacker);
            menuDeathLabels[(int) self.slugcatNumber].label.alpha = alpha;
            menuDeathLabels[(int) self.slugcatNumber].label.x = self.MidXpos + num * self.ScrollMagnitude + 121f;
            menuDeathLabels[(int)self.slugcatNumber].label.y = self.imagePos.y - 383f;
            menuDeathLabels[(int) self.slugcatNumber].label.color = Menu.Menu.MenuRGB(Menu.Menu.MenuColors.MediumGrey);
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
