using System;
using UnityEngine;

namespace REPO_Instant_Save.Util
{
    /// <summary>
    /// Thin, null-guarded wrapper over REPO's in-game HUD text so services can surface a short
    /// on-screen confirmation without depending on any menu UI. Routes through
    /// <see cref="SemiFunc.UIFocusText"/> (the mission/focus text line), which needs
    /// <c>MissionUI.instance</c> — present only while a level's HUD is loaded, so we no-op otherwise.
    /// </summary>
    internal static class HudNotify
    {
        private static readonly Color SuccessColor = new Color(0.45f, 1f, 0.45f);
        private static readonly Color FailureColor = new Color(1f, 0.45f, 0.45f);

        public static void Toast(string message, bool success)
        {
            try
            {
                if (MissionUI.instance == null)
                {
                    // No HUD (menu/loading) — nothing to show on; the caller already logs.
                    return;
                }

                SemiFunc.UIFocusText(message, success ? SuccessColor : FailureColor, Color.white, 3f);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"HUD toast failed: {ex.Message}");
            }
        }
    }
}
