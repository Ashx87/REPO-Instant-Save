namespace REPO_Instant_Save.Util
{
    /// <summary>
    /// Thin, null-safe guards around REPO's global singletons so callers stay
    /// host-aware and don't touch the game before it is ready.
    /// </summary>
    internal static class GameState
    {
        /// <summary>True on the host or in single-player — the only place saving is authoritative.</summary>
        public static bool IsHost => SemiFunc.IsMasterClientOrSingleplayer();

        /// <summary>True while a playable run level is active (not menus, lobby or shop).</summary>
        public static bool InRunLevel => SemiFunc.RunIsLevel();

        /// <summary>True once the stats/save subsystem exists.</summary>
        public static bool StatsReady => StatsManager.instance != null;
    }
}
