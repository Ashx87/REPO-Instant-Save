using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using REPO_Instant_Save.Config;
using REPO_Instant_Save.Hotkeys;

namespace REPO_Instant_Save
{
    /// <summary>
    /// BepInEx entry point. Wires up a shared logger and applies Harmony patches.
    /// All feature logic lives in dedicated services/patches, not here.
    /// </summary>
    [BepInPlugin(ModInfo.Guid, ModInfo.Name, ModInfo.Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static Plugin? Instance { get; private set; }

        /// <summary>Shared logger so non-MonoBehaviour classes can log without a reference.</summary>
        internal static ManualLogSource Log { get; private set; } = null!;

        /// <summary>Strongly-typed config, available to services and patches.</summary>
        internal PluginConfig ModConfig { get; private set; } = null!;

        private readonly Harmony _harmony = new(ModInfo.Guid);

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            Log.LogInfo($"{ModInfo.Name} v{ModInfo.Version} initialising…");

            try
            {
                ModConfig = new PluginConfig(Config);
                _harmony.PatchAll();
                gameObject.AddComponent<HotkeyManager>();
                Log.LogInfo($"{ModInfo.Name} loaded successfully. Full-save key: {ModConfig.FullSaveKey.Value}.");
            }
            catch (Exception ex)
            {
                Log.LogError($"{ModInfo.Name} failed to initialise: {ex}");
            }
        }
    }
}
