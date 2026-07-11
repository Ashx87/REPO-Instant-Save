using System.Collections;
using HarmonyLib;
using REPO_Instant_Save.Save;

namespace REPO_Instant_Save.Patches
{
    /// <summary>
    /// During a cross-session restore, replace the random level generation with the saved layout.
    /// Only active while a matching snapshot is pending; otherwise the originals run untouched.
    /// </summary>
    [HarmonyPatch(typeof(LevelGenerator), "TileGeneration")]
    internal static class TileGenerationRebuildPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(LevelGenerator __instance, ref IEnumerator __result)
        {
            if (CrossSession.Pending == null || !CrossSession.ShouldRebuildCurrentLevel())
            {
                return true;
            }

            CrossSession.RebuildActive = true;
            Plugin.Log.LogInfo($"Cross-session: rebuilding saved layout for '{CrossSession.Pending.levelName}'.");
            __result = LevelRebuild.RebuildTiles(__instance, CrossSession.Pending.grid);
            return false;
        }
    }

    [HarmonyPatch(typeof(LevelGenerator), "StartRoomGeneration")]
    internal static class StartRoomRebuildPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(LevelGenerator __instance, ref IEnumerator __result)
        {
            if (!CrossSession.RebuildActive || CrossSession.Pending == null)
            {
                return true;
            }

            __result = LevelRebuild.RebuildStartRoom(__instance, CrossSession.Pending);
            return false;
        }
    }

    [HarmonyPatch(typeof(LevelGenerator), "SpawnModule")]
    internal static class SpawnModuleRebuildPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(LevelGenerator __instance, int x, int y)
        {
            if (!CrossSession.RebuildActive || CrossSession.Pending == null)
            {
                return true;
            }

            // Skip the original (random pick) only when we successfully placed the saved module.
            return !LevelRebuild.SpawnSavedModule(__instance, x, y, CrossSession.Pending);
        }
    }
}
