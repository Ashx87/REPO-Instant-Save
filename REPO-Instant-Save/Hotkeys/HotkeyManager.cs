using REPO_Instant_Save.Save;
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
            if (cfg == null)
            {
                return;
            }

            if (UnityEngine.Input.GetKeyDown(cfg.FullSaveKey.Value))
            {
                InstantSaveService.SaveNow();
            }

            if (UnityEngine.Input.GetKeyDown(cfg.QuickLoadKey.Value))
            {
                RestoreService.RestoreNow();
            }
        }
    }
}
