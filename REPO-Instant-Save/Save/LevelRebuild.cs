using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using UnityEngine;

namespace REPO_Instant_Save.Save
{
    /// <summary>
    /// Rebuilds a saved level layout during (a cross-session) generation: forces the grid,
    /// the start room and every module back to what the snapshot recorded, instead of the
    /// random generation. Everything downstream (connect/block objects, nav mesh, level
    /// points, valuable volumes) then falls out of the reproduced geometry for free.
    /// </summary>
    internal static class LevelRebuild
    {
        private static Dictionary<string, PrefabRef> _modulePrefabs = new();

        private static void BuildPrefabMap(LevelGenerator lg)
        {
            _modulePrefabs = new Dictionary<string, PrefabRef>();
            var level = lg.Level;
            if (level == null)
            {
                return;
            }

            AddAll(level.StartRooms);
            AddAll(level.ModulesNormal1); AddAll(level.ModulesNormal2); AddAll(level.ModulesNormal3);
            AddAll(level.ModulesPassage1); AddAll(level.ModulesPassage2); AddAll(level.ModulesPassage3);
            AddAll(level.ModulesDeadEnd1); AddAll(level.ModulesDeadEnd2); AddAll(level.ModulesDeadEnd3);
            AddAll(level.ModulesExtraction1); AddAll(level.ModulesExtraction2); AddAll(level.ModulesExtraction3);
            AddAll(level.ModulesSpecial);
        }

        private static void AddAll(List<PrefabRef> list)
        {
            if (list == null)
            {
                return;
            }

            foreach (var pr in list)
            {
                if (pr == null || pr.Prefab == null)
                {
                    continue;
                }

                string key = Clean(pr.Prefab.name);
                if (!_modulePrefabs.ContainsKey(key))
                {
                    _modulePrefabs[key] = pr;
                }
            }
        }

        /// <summary>Replacement for TileGeneration: rebuild LevelGrid straight from the snapshot.</summary>
        public static IEnumerator RebuildTiles(LevelGenerator lg, GridDto grid)
        {
            lg.waitingForSubCoroutine = true;
            BuildPrefabMap(lg);

            lg.LevelWidth = grid.width;
            lg.LevelHeight = grid.height;
            lg.LevelGrid = new LevelGenerator.Tile[grid.width, grid.height];

            for (int x = 0; x < grid.width; x++)
            {
                for (int y = 0; y < grid.height; y++)
                {
                    lg.LevelGrid[x, y] = new LevelGenerator.Tile { x = x, y = y, active = false };
                }
            }

            foreach (var t in grid.tiles)
            {
                if (t.x < 0 || t.y < 0 || t.x >= grid.width || t.y >= grid.height)
                {
                    continue;
                }

                lg.LevelGrid[t.x, t.y] = new LevelGenerator.Tile
                {
                    x = t.x,
                    y = t.y,
                    active = t.active,
                    first = t.first,
                    connections = t.connections,
                    connectedTop = t.cTop,
                    connectedRight = t.cRight,
                    connectedBot = t.cBot,
                    connectedLeft = t.cLeft,
                    type = (Module.Type)t.type,
                };
            }

            // The real TileGeneration recomputes ModuleAmount to the active-tile count; we bypassed
            // it, so set it here. Otherwise Generate() waits `while (ModulesSpawned < ModuleAmount-1)`
            // against the raw (much larger) Level.ModuleAmount and never finishes generating.
            lg.ModuleAmount = grid.tiles.Count;

            Plugin.Log.LogInfo($"Rebuild: grid restored ({grid.tiles.Count} active tiles, ModuleAmount={lg.ModuleAmount}).");
            yield return null;
            lg.waitingForSubCoroutine = false;
        }

        /// <summary>Replacement for StartRoomGeneration: spawn the saved start room prefab.</summary>
        public static IEnumerator RebuildStartRoom(LevelGenerator lg, WorldSnapshot snap)
        {
            lg.waitingForSubCoroutine = true;

            var dto = snap.level.FirstOrDefault(o =>
                o.kind == "module" && o.name.StartsWith("Start Room", StringComparison.Ordinal));

            if (dto != null && _modulePrefabs.TryGetValue(dto.name, out var pr) && pr != null)
            {
                GameObject go = Spawn(pr, Vector3.zero, Quaternion.identity);
                if (go != null)
                {
                    go.transform.parent = lg.LevelParent.transform;
                }
            }
            else
            {
                Plugin.Log.LogWarning($"Rebuild: start room prefab '{dto?.name}' not found.");
            }

            yield return null;
            lg.waitingForSubCoroutine = false;
        }

        /// <summary>Spawn the saved module for grid cell (x,y). Returns false if it should fall back.</summary>
        public static bool SpawnSavedModule(LevelGenerator lg, int x, int y, WorldSnapshot snap)
        {
            // Exclude the start room: it is captured as a "module" but carries Module's default
            // GridX/GridY (0,0) rather than real grid coords, so it would shadow the real module
            // at cell (0,0). RebuildStartRoom spawns it separately.
            var dto = snap.level.FirstOrDefault(o =>
                o.kind == "module" && o.gridX == x && o.gridY == y &&
                !o.name.StartsWith("Start Room", StringComparison.Ordinal));
            if (dto == null)
            {
                return false;
            }

            if (!_modulePrefabs.TryGetValue(dto.name, out var pr) || pr == null)
            {
                Plugin.Log.LogWarning($"Rebuild: module '{dto.name}' at ({x},{y}) prefab not found.");
                return false;
            }

            GameObject go = Spawn(pr, dto.pos.ToVector3(), Quaternion.Euler(dto.euler.ToVector3()));
            if (go == null)
            {
                return false;
            }

            go.transform.parent = lg.LevelParent.transform;

            var module = go.GetComponent<Module>();
            if (module != null)
            {
                module.GridX = x;
                module.GridY = y;
                bool first = lg.LevelGrid[x, y].first;
                bool top = lg.GridCheckActive(x, y + 1);
                bool bottom = lg.GridCheckActive(x, y - 1) || first;
                bool right = lg.GridCheckActive(x + 1, y);
                bool left = lg.GridCheckActive(x - 1, y);
                module.ModuleConnectionSet(top, bottom, right, left, first);
            }

            return true;
        }

        private static GameObject Spawn(PrefabRef pr, Vector3 pos, Quaternion rot)
        {
            return GameManager.instance.gameMode != 0
                ? PhotonNetwork.InstantiateRoomObject(pr.ResourcePath, pos, rot, 0)
                : UnityEngine.Object.Instantiate(pr.Prefab, pos, rot);
        }

        private static string Clean(string name) =>
            name.EndsWith("(Clone)", StringComparison.Ordinal)
                ? name.Substring(0, name.Length - "(Clone)".Length).TrimEnd()
                : name;
    }
}
