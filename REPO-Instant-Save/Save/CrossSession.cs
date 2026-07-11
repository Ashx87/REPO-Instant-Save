namespace REPO_Instant_Save.Save
{
    /// <summary>
    /// Cross-session restore state. When the host loads a save that carries an Instant Save
    /// snapshot, we "arm" it here; when that snapshot's level next regenerates we rebuild the
    /// saved map from it (see the level-rebuild patches) and then apply the world restore.
    /// </summary>
    internal static class CrossSession
    {
        /// <summary>The snapshot waiting to be rebuilt on the next matching level generation.</summary>
        public static WorldSnapshot? Pending { get; private set; }

        /// <summary>True once the matching level has started generating and we are driving the rebuild.</summary>
        public static bool RebuildActive { get; set; }

        /// <summary>The Level asset matching the pending snapshot — used to force the game onto it.</summary>
        public static Level? TargetLevel { get; private set; }

        public static void Arm(WorldSnapshot snapshot)
        {
            Pending = snapshot;
            RebuildActive = false;
            TargetLevel = FindLevel(snapshot.levelName);
            Plugin.Log.LogInfo(
                $"Cross-session restore armed for '{snapshot.levelName}' " +
                $"(grid {snapshot.grid.tiles.Count} tiles, {snapshot.level.Count} level objects); " +
                $"target level {(TargetLevel != null ? "found" : "NOT FOUND")}.");
        }

        private static Level? FindLevel(string levelName)
        {
            var rm = RunManager.instance;
            if (rm == null || rm.levels == null)
            {
                return null;
            }

            foreach (var level in rm.levels)
            {
                if (level != null && level.name == levelName)
                {
                    return level;
                }
            }

            return null;
        }

        public static void Clear()
        {
            Pending = null;
            RebuildActive = false;
            TargetLevel = null;
        }

        /// <summary>True when we have a pending snapshot whose level matches the one about to generate.</summary>
        public static bool ShouldRebuildCurrentLevel()
        {
            if (Pending == null || !Pending.grid.present)
            {
                return false;
            }

            var level = RunManager.instance != null ? RunManager.instance.levelCurrent : null;
            return level != null && level.name == Pending.levelName;
        }
    }
}
