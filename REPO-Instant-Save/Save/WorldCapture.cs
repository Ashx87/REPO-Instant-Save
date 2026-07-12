using System;
using UnityEngine;

namespace REPO_Instant_Save.Save
{
    /// <summary>
    /// Reads the live scene into an immutable <see cref="WorldSnapshot"/>. Host-side only;
    /// never mutates game state. Called at the moment the player triggers an Instant Save.
    /// </summary>
    internal static class WorldCapture
    {
        public static WorldSnapshot Capture()
        {
            var snap = new WorldSnapshot
            {
                levelName = RunManager.instance != null && RunManager.instance.levelCurrent != null
                    ? RunManager.instance.levelCurrent.name
                    : "?",
                takenUtcTicks = DateTime.UtcNow.Ticks,
                runLevel = StatsManager.instance != null ? StatsManager.instance.GetRunStatLevel() : (int?)null,
            };

            CaptureLevelObjects(snap);
            CaptureGrid(snap);
            CaptureGrabbables(snap);
            CaptureExtraction(snap);
            CaptureRound(snap);
            CaptureHinges(snap);
            CapturePlayers(snap);
            return snap;
        }

        private static void CaptureLevelObjects(WorldSnapshot snap)
        {
            var lg = LevelGenerator.Instance;
            if (lg == null || lg.LevelParent == null)
            {
                return;
            }

            foreach (Transform child in lg.LevelParent.transform)
            {
                var module = child.GetComponent<Module>();
                string kind = module != null ? "module"
                    : child.GetComponent<ModuleConnectObject>() != null ? "connect"
                    : "other";

                snap.level.Add(new SceneObjectDto
                {
                    name = Clean(child.name),
                    kind = kind,
                    pos = new Vec3Dto(child.position),
                    euler = new Vec3Dto(child.eulerAngles),
                    gridX = module != null ? module.GridX : -1,
                    gridY = module != null ? module.GridY : -1,
                    moduleType = ModuleTileType(lg, module),
                });
            }
        }

        private static int ModuleTileType(LevelGenerator lg, Module? module)
        {
            if (module == null || lg.LevelGrid == null)
            {
                return -1;
            }

            int x = module.GridX;
            int y = module.GridY;
            if (x < 0 || y < 0 || x >= lg.LevelWidth || y >= lg.LevelHeight)
            {
                return -1;
            }

            var t = lg.LevelGrid[x, y];
            return t != null ? (int)t.type : -1;
        }

        private static void CaptureGrid(WorldSnapshot snap)
        {
            var lg = LevelGenerator.Instance;
            if (lg == null || lg.LevelGrid == null)
            {
                return;
            }

            snap.grid.present = true;
            snap.grid.width = lg.LevelWidth;
            snap.grid.height = lg.LevelHeight;

            for (int x = 0; x < lg.LevelWidth; x++)
            {
                for (int y = 0; y < lg.LevelHeight; y++)
                {
                    var t = lg.LevelGrid[x, y];
                    if (t == null || !t.active)
                    {
                        continue;
                    }

                    snap.grid.tiles.Add(new TileDto
                    {
                        x = x,
                        y = y,
                        active = t.active,
                        first = t.first,
                        connections = t.connections,
                        cTop = t.connectedTop,
                        cRight = t.connectedRight,
                        cBot = t.connectedBot,
                        cLeft = t.connectedLeft,
                        type = (int)t.type,
                    });
                }
            }
        }

        private static void CaptureGrabbables(WorldSnapshot snap)
        {
            // All loose, grabbable world objects: valuables, items (guns/tools) and carts.
            // Structural grabbables (hinges, doors) are skipped — they return with the modules.
            foreach (var pgo in UnityEngine.Object.FindObjectsOfType<PhysGrabObject>())
            {
                if (pgo == null)
                {
                    continue;
                }

                var valuable = pgo.GetComponent<ValuableObject>();
                string? kind = pgo.GetComponent<PhysGrabCart>() != null ? "cart"
                    : valuable != null ? "valuable"
                    : pgo.GetComponent<ItemAttributes>() != null ? "item"
                    : null;

                if (kind == null)
                {
                    continue;
                }

                snap.valuables.Add(new ValuableDto
                {
                    name = Clean(pgo.name),
                    kind = kind,
                    pos = new Vec3Dto(pgo.transform.position),
                    euler = new Vec3Dto(pgo.transform.eulerAngles),
                    value = valuable != null ? valuable.dollarValueCurrent : 0f,
                });
            }
        }

        private static void CaptureExtraction(WorldSnapshot snap)
        {
            foreach (var e in UnityEngine.Object.FindObjectsOfType<ExtractionPoint>())
            {
                if (e == null)
                {
                    continue;
                }

                snap.extraction.Add(new ExtractionDto
                {
                    name = Clean(e.name),
                    pos = new Vec3Dto(e.transform.position),
                    haulCurrent = e.haulCurrent,
                    haulGoal = e.haulGoal,
                    state = (int)e.currentState,
                });
            }
        }

        private static void CaptureRound(WorldSnapshot snap)
        {
            var r = RoundDirector.instance;
            if (r == null)
            {
                return;
            }

            snap.round = new RoundDto
            {
                present = true,
                currentHaul = r.currentHaul,
                totalHaul = r.totalHaul,
                currentHaulMax = r.currentHaulMax,
                haulGoal = r.haulGoal,
                haulGoalMax = r.haulGoalMax,
                extractionPoints = r.extractionPoints,
                extractionPointSurplus = r.extractionPointSurplus,
                extractionPointsCompleted = r.extractionPointsCompleted,
                extractionHaulGoal = r.extractionHaulGoal,
                extractionPointActive = r.extractionPointActive,
                allExtractionPointsCompleted = r.allExtractionPointsCompleted,
            };
        }

        private static void CaptureHinges(WorldSnapshot snap)
        {
            foreach (var h in UnityEngine.Object.FindObjectsOfType<PhysGrabHinge>())
            {
                if (h == null)
                {
                    continue;
                }

                Vector3 pivot = h.hingePoint != null ? h.hingePoint.position : h.transform.position;
                snap.hinges.Add(new HingeDto
                {
                    name = Clean(h.name),
                    pivot = new Vec3Dto(pivot),
                    pos = new Vec3Dto(h.transform.position),
                    euler = new Vec3Dto(h.transform.eulerAngles),
                    closed = h.closed,
                    broken = h.broken,
                    dead = h.dead,
                });
            }
        }

        private static void CapturePlayers(WorldSnapshot snap)
        {
            if (GameDirector.instance == null || GameDirector.instance.PlayerList == null)
            {
                return;
            }

            foreach (var p in GameDirector.instance.PlayerList)
            {
                if (p == null)
                {
                    continue;
                }

                snap.players.Add(new PlayerDto
                {
                    steamId = p.steamID ?? "",
                    name = p.playerName ?? "",
                    pos = new Vec3Dto(p.transform.position),
                    euler = new Vec3Dto(p.transform.eulerAngles),
                    health = p.playerHealth != null ? p.playerHealth.health : 0,
                });
            }
        }

        /// <summary>Strips Unity's "(Clone)" suffix so names match their source prefab.</summary>
        private static string Clean(string name) =>
            name.EndsWith("(Clone)", StringComparison.Ordinal)
                ? name.Substring(0, name.Length - "(Clone)".Length).TrimEnd()
                : name;
    }
}
