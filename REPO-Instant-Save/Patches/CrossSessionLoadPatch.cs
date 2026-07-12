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
                if (snapshot == null || !snapshot.grid.present)
                {
                    CrossSession.Clear();
                    return;
                }

                // Auto-invalidate a snapshot the native save has already progressed past, so it
                // can never drag the run back to an old level (which would shadow normal saving).
                if (IsSnapshotStale(snapshot, out string reason))
                {
                    Plugin.Log.LogInfo(
                        $"Cross-session: discarding stale Instant Save for '{fileName}' ({reason}); " +
                        "resuming native progression.");
                    CrossSession.Clear();
                    InstantSaveStore.Delete(fileName);
                    return;
                }

                CrossSession.Arm(snapshot);

                // Force the game onto the saved level instead of a fresh random one.
                if (CrossSession.TargetLevel != null && RunManager.instance != null)
                {
                    RunManager.instance.debugLevel = CrossSession.TargetLevel;
                    Plugin.Log.LogInfo($"Cross-session: forcing next level to '{CrossSession.TargetLevel.name}'.");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Cross-session arm failed for '{fileName}': {ex.Message}");
            }
        }

        /// <summary>
        /// A snapshot is stale once the loaded native save no longer sits at the mid-level point
        /// it was taken from. An Instant Save is always an in-level snapshot, so it must not be
        /// force-applied when the save resumes into the shop/lobby, or into a different level than
        /// the one it captured. Both checks read state that <c>LoadGame</c> has just populated.
        /// </summary>
        private static bool IsSnapshotStale(WorldSnapshot snapshot, out string reason)
        {
            var stats = StatsManager.instance;
            if (stats == null)
            {
                reason = "";
                return false;
            }

            // "save level" phase: 0 = in a level, 1 = shop, 2 = lobby (RunManager.ChangeLevel).
            int savePhase = stats.GetRunStatSaveLevel();
            if (savePhase != 0)
            {
                reason = $"native save resumes in phase {savePhase} (shop/lobby), not in a level";
                return true;
            }

            // Progression: levels completed. Null on pre-existing snapshots — skip this check then.
            if (snapshot.runLevel.HasValue)
            {
                int nativeLevel = stats.GetRunStatLevel();
                if (nativeLevel != snapshot.runLevel.Value)
                {
                    reason = $"native run level {nativeLevel} != snapshot run level {snapshot.runLevel.Value}";
                    return true;
                }
            }

            reason = "";
            return false;
        }
    }
}
