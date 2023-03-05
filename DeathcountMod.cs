using BepInEx;
using UnityEngine;

// Mod-specific includes
using Menu;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace Deathcount
{
    [BepInPlugin("kadw.deathcount", "Deathcount", "1.3.1")]
    public class DeathcountMod : BaseUnityPlugin
    {
        private static MenuLabel[] menuDeathLabels = null;
        public static int[] menuDeaths = null;

        public void OnEnable()
        {
            On.Menu.SlugcatSelectMenu.ctor += SlugcatSelectMenu_ctor;

            On.Menu.SlugcatSelectMenu.SlugcatPageContinue.GrafUpdate += SlugcatPageContinue_GrafUpdate;

            On.Menu.SleepAndDeathScreen.GetDataFromGame += SleepAndDeathScreen_GetDataFromGame;

            Debug.Log("Deathcount mod running");
        }


        /*
         * Get the death count for each slugcat, and create the menu label.
         */
        private static void SlugcatSelectMenu_ctor(On.Menu.SlugcatSelectMenu.orig_ctor orig, SlugcatSelectMenu self, ProcessManager manager)
        {
            orig(self, manager);

            int numOfSlugcats = self.slugcatColorOrder.Count;
            menuDeathLabels = new MenuLabel[numOfSlugcats];
            menuDeaths = new int[numOfSlugcats];

            Debug.Log("Deathcount: number of slugcats detected: " + numOfSlugcats);

            for (int i = 0; i < self.slugcatColorOrder.Count; i++)
            {
                // if a save file exists for that slugcat
                if (SlugcatSelectMenu.MineForSaveData(manager, self.slugcatColorOrder[i]) != null)
                {
                    string[] progLines = manager.rainWorld.progression.GetProgLinesFromMemory();
                    for (int j = 0; i < progLines.Length; j++)
                    {
                        string[] array = Regex.Split(progLines[j], "<progDivB>");
                        if (array.Length == 2 && array[0] == "SAVE STATE" && BackwardsCompatibilityRemix.ParseSaveNumber(array[1]) == self.slugcatColorOrder[i])
                        {
                            List<SaveStateMiner.Target> list = new List<SaveStateMiner.Target>();
                            list.Add(new SaveStateMiner.Target(">DEATHS", "<dpB>", "<dpA>", 50));
                            List<SaveStateMiner.Result> list2 = SaveStateMiner.Mine(manager.rainWorld, array[1], list);

                            // try to assign the death number and create the label for the slugcat
                            try
                            {
                                menuDeaths[i] = int.Parse(list2[0].data);
                                Debug.Log("Deathcount: assign: " + self.slugcatColorOrder[i] + " " + i + ", " + int.Parse(list2[0].data));

                                menuDeathLabels[i] = new MenuLabel(self, self.slugcatPages[i], "Deaths: " + menuDeaths[i], new Vector2(-1000f, self.slugcatPages[i].imagePos.y - 405f), new Vector2(), false);
                                menuDeathLabels[i].label.alignment = FLabelAlignment.Left;
                                self.slugcatPages[i].subObjects.Add(menuDeathLabels[i]);
                                // Debug.Log("Deathcount: page " + i + " created");

                                break;
                            }
                            catch
                            {
                                Debug.Log("Deathcount: failed to assign death num. Slugcat/Data: " + self.slugcatColorOrder[i] + " " + i + ", " + list2[0].data);
                            }
                        }
                    }
                }
            }
        }

        /*
         * Update graphics for the deathcount label in each slugcat menu page.
         */
        private static void SlugcatPageContinue_GrafUpdate(On.Menu.SlugcatSelectMenu.SlugcatPageContinue.orig_GrafUpdate orig, SlugcatSelectMenu.SlugcatPageContinue self, float timeStacker)
        {
            orig(self, timeStacker);

            if (menuDeathLabels[self.SlugcatPageIndex] != null)
            {
                float num = self.Scroll(timeStacker);
                float alpha = self.UseAlpha(timeStacker);
                menuDeathLabels[self.SlugcatPageIndex].label.alpha = alpha;
                menuDeathLabels[self.SlugcatPageIndex].label.x = self.MidXpos + num * self.ScrollMagnitude + 121f;
                menuDeathLabels[self.SlugcatPageIndex].label.y = self.imagePos.y - 383f;
                menuDeathLabels[self.SlugcatPageIndex].label.color = Menu.Menu.MenuRGB(Menu.Menu.MenuColors.MediumGrey);
            }
            else
            {
                Debug.Log("Deathcount: menuDeathLabels[self.SlugcatPageIndex] is null. SlugcatPageIndex: " + self.SlugcatPageIndex);

                if (menuDeathLabels[self.SlugcatPageIndex] != null)
                {
                    Debug.Log("Deathcount: menuDeathLabels[self.SlugcatPageIndex]: " + menuDeathLabels[self.SlugcatPageIndex]);
                }
            }
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
