using HarmonyLib;

namespace REPO_Instant_Save.Patches
{
    /// <summary>
    /// Stops REPO from auto-deleting the save file on team wipe / arena death / early leave.
    /// The vanilla <c>DataDirector.SaveDeleteCheck</c> does nothing but decide whether to call
    /// <c>SaveFileDelete</c>, so skipping the whole method is safe. Intentional deletes (the
    /// saves menu, our own manager) go through a different path and are unaffected.
    /// </summary>
    [HarmonyPatch(typeof(DataDirector), nameof(DataDirector.SaveDeleteCheck))]
    internal static class SaveDeletePatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            if (Plugin.Instance?.ModConfig?.PreventDeathDelete.Value == true)
            {
                Plugin.Log.LogInfo("Blocked REPO auto-delete of the save file (PreventDeathDelete).");
                return false; // skip original — do not delete
            }

            return true; // run vanilla behaviour
        }
    }
}
