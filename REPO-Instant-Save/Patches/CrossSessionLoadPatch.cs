using System;
using System.Collections.Generic;
using HarmonyLib;
using REPO_Instant_Save.Save;

namespace REPO_Instant_Save.Patches
{
    /// <summary>
    /// When a save is loaded, arm a cross-session rebuild if it has an Instant Save snapshot.
    /// The actual map rebuild happens later, when the saved level regenerates.
    /// </summary>
    [HarmonyPatch(typeof(StatsManager), nameof(StatsManager.LoadGame))]
    internal static class CrossSessionLoadPatch
    {
        [HarmonyPostfix]
        private static void Postfix(string fileName)
        {
            try
            {
                WorldSnapshot? snapshot = InstantSaveStore.Read(fileName);
                if (snapshot != null && snapshot.grid.present)
                {
                    CrossSession.Arm(snapshot);

                    // Force the game onto the saved level instead of a fresh random one.
                    if (CrossSession.TargetLevel != null && RunManager.instance != null)
                    {
                        RunManager.instance.debugLevel = CrossSession.TargetLevel;
                        Plugin.Log.LogInfo($"Cross-session: forcing next level to '{CrossSession.TargetLevel.name}'.");
                    }
                }
                else
                {
                    CrossSession.Clear();
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Cross-session arm failed for '{fileName}': {ex.Message}");
            }
        }
    }
}
