using REPO_Instant_Save.Save;
using REPO_Instant_Save.Util;
using UnityEngine;

namespace REPO_Instant_Save.Hotkeys
{
    /// <summary>
    /// Polls the configured hotkeys each frame and dispatches actions. Added as a component
    /// to the plugin's (persistent) GameObject at startup. Uses legacy input, which REPO
    /// itself relies on, so it is available at runtime.
    /// </summary>
    internal sealed class HotkeyManager : MonoBehaviour
    {
        private void Update()
        {
            var cfg = Plugin.Instance?.ModConfig;
            if (cfg == null || !cfg.EnableHotkeys.Value)
            {
                return;
            }

            // Only F7 (save) is a hotkey. Restore is never user-triggered — the mod runs it
            // itself via the cross-session patches (WorldRestore.CrossSessionRestore) when a save
            // with a snapshot is loaded.
            if (UnityEngine.Input.GetKeyDown(cfg.FullSaveKey.Value))
            {
                bool ok = InstantSaveService.SaveNow();
                HudNotify.Toast(ok ? "Instant Save complete" : "Instant Save failed", ok);
            }
        }
    }
}
