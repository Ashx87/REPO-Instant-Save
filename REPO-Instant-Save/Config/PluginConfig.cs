using BepInEx.Configuration;
using UnityEngine;

namespace REPO_Instant_Save.Config
{
    /// <summary>
    /// Strongly-typed wrapper over the BepInEx config file. All tunables live here so no
    /// behaviour is hard-coded. Bound once from <see cref="Plugin"/> at startup.
    /// </summary>
    internal sealed class PluginConfig
    {
        /// <summary>Capture an Instant Save of the whole level. Host only.</summary>
        public ConfigEntry<KeyCode> FullSaveKey { get; }

        /// <summary>Whether the save hotkey is active.</summary>
        public ConfigEntry<bool> EnableHotkeys { get; }

        /// <summary>Stop REPO from auto-deleting the save on team wipe / early leave.</summary>
        public ConfigEntry<bool> PreventDeathDelete { get; }

        public PluginConfig(ConfigFile file)
        {
            FullSaveKey = file.Bind(
                "Hotkeys", "FullSaveNow", KeyCode.F7,
                "Capture an Instant Save of the whole level right now (host only).");

            EnableHotkeys = file.Bind(
                "Hotkeys", "EnableHotkeys", true,
                "Enable the Instant Save hotkey (F7 by default).");

            PreventDeathDelete = file.Bind(
                "Safety", "PreventDeathDelete", true,
                "Prevent REPO from deleting your save when the whole team dies or you leave a run early.");
        }
    }
}
