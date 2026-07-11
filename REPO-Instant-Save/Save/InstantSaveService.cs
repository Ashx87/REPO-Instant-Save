using System;
using REPO_Instant_Save.Util;

namespace REPO_Instant_Save.Save
{
    /// <summary>
    /// Orchestrates an Instant Save: capture the full world snapshot, trigger the game's
    /// own progression save (so the .es3 / save folder exists), then persist the snapshot
    /// beside it. Host-only, and only while a playable level is loaded.
    /// </summary>
    internal static class InstantSaveService
    {
        public static bool SaveNow()
        {
            if (!GameState.IsHost)
            {
                Plugin.Log.LogWarning("Instant Save ignored: only the host can save.");
                return false;
            }

            if (!GameState.InRunLevel)
            {
                Plugin.Log.LogWarning("Instant Save ignored: you must be inside a level.");
                return false;
            }

            if (!GameState.StatsReady)
            {
                Plugin.Log.LogWarning("Instant Save ignored: StatsManager not ready.");
                return false;
            }

            try
            {
                WorldSnapshot snapshot = WorldCapture.Capture();

                // Persist native progression first so the save folder/.es3 exist.
                StatsManager.instance.SaveFileSave();

                string saveName = StatsManager.instance.saveFileCurrent;
                InstantSaveStore.Write(saveName, snapshot);

                Plugin.Log.LogInfo(
                    $"Instant Save captured for '{saveName}' [{snapshot.levelName}]: " +
                    $"{snapshot.level.Count} level objects, {snapshot.valuables.Count} valuables, " +
                    $"{snapshot.extraction.Count} extraction points, {snapshot.players.Count} players.");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Instant Save failed: {ex}");
                return false;
            }
        }
    }
}
