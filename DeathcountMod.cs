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
    [BepInPlugin("kadw.deathcount", "Deathcount", "1.0.0")]
    public class DeathcountMod : BaseUnityPlugin
    {
        private static MenuLabel[] menuDeathLabels = null;
        public static int[] menuDeaths = null;
        private static bool isSlugBaseEnabled = false;

        public void OnEnable()
        {
            On.Menu.SlugcatSelectMenu.ctor += SlugcatSelectMenu_ctor;
            On.Menu.SlugcatSelectMenu.MineForSaveData += SlugcatSelectMenu_MineForSaveData;
            On.Menu.SlugcatSelectMenu.SlugcatPageContinue.ctor += SlugcatPageContinue_ctor;
            On.Menu.SlugcatSelectMenu.SlugcatPageContinue.GrafUpdate += SlugcatPageContinue_GrafUpdate;

            On.Menu.SleepAndDeathScreen.GetDataFromGame += SleepAndDeathScreen_GetDataFromGame;

            // for slugbase compatibility
            On.RainWorld.Start += RainWorld_Start;
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
                    isSlugBaseEnabled = true;
                    // Debug.Log("Deathcount: SlugBase found.");
                    return;
                }
            }

            // Debug.Log("Deathcount: SlugBase not found.");
        }

        /*
         * Initialize menuDeaths and menuDeathLabels with the number of existing slugcats.
         */
        private static void SlugcatSelectMenu_ctor(On.Menu.SlugcatSelectMenu.orig_ctor orig, SlugcatSelectMenu self, ProcessManager manager)
        {
            if (isSlugBaseEnabled)
            {
                MenuCtor_SlugBaseVersion();
            }
            else
            {
                menuDeathLabels = new MenuLabel[4];
                menuDeaths = new int[4];
            }

            orig(self, manager);

            /*
            Debug.Log("DEATHCOUNT 00: " + self.saveGameData.Length); - number of slugcats
            Debug.Log("DEATHCOUNT 01: " + self.slugcatColorOrder.Length); - number of slugcats
            Debug.Log("DEATHCOUNT 02: " + self.pages.Count); - number of slugcats + 1
            Debug.Log("DEATHCOUNT 03: " + self.slugcatPages.Length); - number of slugcats
            */
        }

        /*
         * Used in above method when SlugBase exists.
         * Has to be split into another method or game crashes if SlugBase isn't there.
         */
        private static void MenuCtor_SlugBaseVersion()
        {
            int slugcats = 4 + SlugBase.PlayerManager.GetCustomPlayers().Count; // 3 vanilla slugs + number of custom slugs + 1 (for length)
            menuDeathLabels = new MenuLabel[slugcats];
            menuDeaths = new int[slugcats];
        }

        /*
         * Get the death count for each slugcat, including SlugBase custom slugcats if they exist.
         */
        private static SlugcatSelectMenu.SaveGameData SlugcatSelectMenu_MineForSaveData(On.Menu.SlugcatSelectMenu.orig_MineForSaveData orig, ProcessManager manager, int slugcat)
        {
            Debug.Log("Deathcount Slugcat Select Menu MineForSaveData");
            SlugcatSelectMenu.SaveGameData returnData = orig(manager, slugcat);

            if (returnData != null) {
                if (isSlugBaseEnabled)
                {
                    if (MineForSaveData_SlugBaseVersion(manager, slugcat))
                    {
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
                            Debug.Log("Deathcount vanilla: failed to assign death num. Slugcat/Data: " + slugcat + ", " + list2[0].data);
                            menuDeaths[slugcat] = -1;
                        }
                    }
                }
            }

            return returnData;
        }

        /*
         * Used in above method when SlugBase exists.
         * Has to be split into another method or game crashes if SlugBase isn't there.
         * Returns true if there's a valid custom slugcat save, false if there isn't and mod should check vanilla saves.
         */
        private static bool MineForSaveData_SlugBaseVersion(ProcessManager manager, int slugcat)
        {
            SlugBase.SlugBaseCharacter ply = SlugBase.PlayerManager.GetCustomPlayer(slugcat);
            if (ply != null)
            {
                SaveState save = manager.rainWorld.progression.currentSaveState;
                if (save != null && save.saveStateNumber == slugcat)
                {
                    menuDeaths[slugcat] = save.deathPersistentSaveData.deaths;
                    return true;
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
                    return true;
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
                    Debug.Log("Deathcount custom: failed to assign death num. Slugcat/Data: " + slugcat + ", " + list2[0].data);
                    DeathcountMod.menuDeaths[slugcat] = -1;
                }

                return true;
            }

            return false;
        }

        /*
         * Create deathcount label for each slugcat menu page, and update it with the other labels.
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
