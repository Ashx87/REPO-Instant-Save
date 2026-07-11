using System;
using REPO_Instant_Save.Util;

namespace REPO_Instant_Save.Save
{
    /// <summary>
    /// Loads the Instant Save snapshot for the current run and applies it. Host-only.
    /// Same-session it repositions live objects; cross-session (after the level was
    /// regenerated) it will also rebuild the saved map — added incrementally.
    /// </summary>
    internal static class RestoreService
    {
        public static bool RestoreNow()
        {
            if (!GameState.IsHost)
            {
                Plugin.Log.LogWarning("Restore ignored: only the host can load.");
                return false;
            }

            if (!GameState.StatsReady)
            {
                Plugin.Log.LogWarning("Restore ignored: StatsManager not ready.");
                return false;
            }

            string saveName = StatsManager.instance.saveFileCurrent;
            WorldSnapshot? snap = InstantSaveStore.Read(saveName);
            if (snap == null)
            {
                Plugin.Log.LogWarning($"No Instant Save snapshot found for '{saveName}'.");
                return false;
            }

            try
            {
                if (Plugin.Instance == null)
                {
                    Plugin.Log.LogWarning("Restore ignored: plugin instance not ready.");
                    return false;
                }

                Plugin.Instance.StartCoroutine(WorldRestore.RestoreRoutine(snap));
                Plugin.Log.LogInfo($"Instant Save restore started from '{saveName}' [{snap.levelName}].");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Instant Save restore failed: {ex}");
                return false;
            }
        }
    }
}
