using System;
using REPO_Instant_Save.Util;

namespace REPO_Instant_Save.Save
{
    /// <summary>
    /// Mode B — persist the current run's progression to its native .es3 save on demand,
    /// by driving the game's own <c>StatsManager.SaveFileSave()</c>. Host only.
    /// </summary>
    internal static class ProgressionSaver
    {
        /// <returns>true if the save was triggered; false if refused (not host / not ready) or errored.</returns>
        public static bool SaveNow()
        {
            if (!GameState.IsHost)
            {
                Plugin.Log.LogWarning("Save ignored: only the host can save the run.");
                return false;
            }

            if (!GameState.StatsReady)
            {
                Plugin.Log.LogWarning("Save ignored: StatsManager is not ready yet.");
                return false;
            }

            try
            {
                StatsManager.instance.SaveFileSave();
                Plugin.Log.LogInfo($"Run saved to '{StatsManager.instance.saveFileCurrent}'.");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to save run: {ex}");
                return false;
            }
        }
    }
}
