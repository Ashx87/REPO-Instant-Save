using HarmonyLib;
using REPO_Instant_Save.Save;

namespace REPO_Instant_Save.Patches
{
    /// <summary>
    /// When the forced (saved) level finishes generating, clear the level override so the run
    /// continues normally afterwards. The map rebuild + world restore hook in here next.
    /// </summary>
    [HarmonyPatch(typeof(LevelGenerator), "GenerateDone")]
    internal static class CrossSessionGenerateDonePatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            if (CrossSession.Pending == null || !CrossSession.RebuildActive)
            {
                return;
            }

            WorldSnapshot snap = CrossSession.Pending;
            Plugin.Log.LogInfo($"Cross-session: saved level '{snap.levelName}' rebuilt — applying world restore.");

            // Stop forcing the level so the truck/shop/next levels are normal again.
            if (RunManager.instance != null)
            {
                RunManager.instance.debugLevel = null;
            }

            if (Plugin.Instance != null)
            {
                Plugin.Instance.StartCoroutine(WorldRestore.CrossSessionRestore(snap));
            }

            CrossSession.Clear();
        }
    }
}
