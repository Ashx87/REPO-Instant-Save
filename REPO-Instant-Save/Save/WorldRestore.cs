using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using UnityEngine;

namespace REPO_Instant_Save.Save
{
    /// <summary>
    /// Applies a <see cref="WorldSnapshot"/> back onto the live scene. Host-authoritative.
    /// Runs as a coroutine so the player teleport can be held for a few physics frames
    /// (defeating movement-controller drift) and objects get an impact-grace window so
    /// they don't break or lose value while settling. Valuables also have their value
    /// restored, and ones that were destroyed (broken) since the save are re-created.
    /// </summary>
    internal static class WorldRestore
    {
        private const float ImpactGraceSeconds = 3f;
        private const int PlayerHoldFrames = 12;

        // Hinges (doors/drawers) matched during restore, re-asserted each hold frame so the
        // joint's spring can't jitter them off the saved angle.
        private static readonly List<(PhysGrabObject pgo, Vector3 pos, Quaternion rot)> HeldHinges = new();

        public static IEnumerator RestoreRoutine(WorldSnapshot snap)
        {
            RestoreGrabbables(snap);
            RefreshEnemies();
            RestoreExtraction(snap);
            RestoreHinges(snap);
            RestoreHealth(snap);
            RestorePlayers(snap);

            for (int i = 0; i < PlayerHoldFrames; i++)
            {
                HoldLocalPlayer(snap);
                HoldHinges();
                yield return new WaitForFixedUpdate();
            }

            Plugin.Log.LogInfo("Restore: complete.");
        }

        /// <summary>
        /// Cross-session entry: the map was just rebuilt, but the generator also spawned random
        /// valuables. Clear those first, then run the normal restore (which re-creates the saved
        /// valuables, teleports players, etc.).
        /// </summary>
        public static IEnumerator CrossSessionRestore(WorldSnapshot snap)
        {
            ClearSpawnedValuables();
            yield return null;
            yield return RestoreRoutine(snap);
        }

        /// <summary>Destroy every valuable the generator spawned so restore can re-create the saved set.</summary>
        private static void ClearSpawnedValuables()
        {
            int n = 0;
            foreach (var v in UnityEngine.Object.FindObjectsOfType<ValuableObject>())
            {
                if (v == null)
                {
                    continue;
                }

                if (SemiFunc.IsMultiplayer())
                {
                    if (PhotonNetwork.IsMasterClient)
                    {
                        PhotonNetwork.Destroy(v.gameObject);
                        n++;
                    }
                }
                else
                {
                    UnityEngine.Object.Destroy(v.gameObject);
                    n++;
                }
            }

            Plugin.Log.LogInfo($"Cross-session: cleared {n} generator-spawned valuable(s).");
        }

        // ---- Players ----------------------------------------------------------------

        public static void RestorePlayers(WorldSnapshot snap)
        {
            if (GameDirector.instance == null || GameDirector.instance.PlayerList == null)
            {
                return;
            }

            int moved = 0;
            foreach (var player in GameDirector.instance.PlayerList)
            {
                if (player == null)
                {
                    continue;
                }

                PlayerDto? dto = snap.players.Find(x => x.steamId == player.steamID);
                if (dto == null)
                {
                    continue;
                }

                player.Spawn(dto.pos.ToVector3(), Quaternion.Euler(dto.euler.ToVector3()));
                ZeroVelocity(player.GetComponent<Rigidbody>());
                moved++;
            }

            Plugin.Log.LogInfo($"Restore: teleported {moved} player(s) to saved positions.");
        }

        private static void HoldLocalPlayer(WorldSnapshot snap)
        {
            if (GameDirector.instance == null || GameDirector.instance.PlayerList == null)
            {
                return;
            }

            foreach (var player in GameDirector.instance.PlayerList)
            {
                if (player == null || !player.isLocal)
                {
                    continue;
                }

                PlayerDto? dto = snap.players.Find(x => x.steamId == player.steamID);
                if (dto == null)
                {
                    continue;
                }

                Vector3 pos = dto.pos.ToVector3();
                Quaternion rot = Quaternion.Euler(dto.euler.ToVector3());

                if (PlayerController.instance != null)
                {
                    PlayerController.instance.transform.position = pos;
                    PlayerController.instance.transform.rotation = rot;
                }

                Rigidbody rb = player.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.position = pos;
                    ZeroVelocity(rb);
                }

                player.transform.position = pos;
            }
        }

        public static void RestoreHealth(WorldSnapshot snap)
        {
            if (GameDirector.instance == null || GameDirector.instance.PlayerList == null)
            {
                return;
            }

            int done = 0;
            foreach (var player in GameDirector.instance.PlayerList)
            {
                if (player == null || player.playerHealth == null)
                {
                    continue;
                }

                PlayerDto? dto = snap.players.Find(x => x.steamId == player.steamID);
                if (dto == null)
                {
                    continue;
                }

                int current = player.playerHealth.health;
                int target = dto.health;
                if (target > current)
                {
                    player.playerHealth.Heal(target - current, false);
                }
                else if (target < current && target > 0)
                {
                    player.playerHealth.Hurt(current - target, savingGrace: false, enemyIndex: -1, hurtByHeal: true);
                }

                done++;
            }

            Plugin.Log.LogInfo($"Restore: set health on {done} player(s).");
        }

        // ---- Enemies ----------------------------------------------------------------

        /// <summary>Despawn all currently-spawned enemies so the director re-spawns them fresh.</summary>
        public static void RefreshEnemies()
        {
            var dir = EnemyDirector.instance;
            if (dir == null || dir.enemiesSpawned == null)
            {
                return;
            }

            int n = 0;
            foreach (var enemy in dir.enemiesSpawned.ToList())
            {
                if (enemy == null)
                {
                    continue;
                }

                try
                {
                    enemy.Despawn();
                    n++;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"Restore: enemy despawn failed: {ex.Message}");
                }
            }

            Plugin.Log.LogInfo($"Restore: refreshed (despawned) {n} enemy group(s).");
        }

        // ---- Extraction / round accounting ------------------------------------------

        /// <summary>Restore round haul accounting and reset extraction-point states (un-complete).</summary>
        public static void RestoreExtraction(WorldSnapshot snap)
        {
            var r = RoundDirector.instance;
            if (r != null && snap.round != null && snap.round.present)
            {
                r.currentHaul = snap.round.currentHaul;
                r.totalHaul = snap.round.totalHaul;
                r.currentHaulMax = snap.round.currentHaulMax;
                r.haulGoal = snap.round.haulGoal;
                r.haulGoalMax = snap.round.haulGoalMax;
                r.extractionPoints = snap.round.extractionPoints;
                r.extractionPointSurplus = snap.round.extractionPointSurplus;
                r.extractionPointsCompleted = snap.round.extractionPointsCompleted;
                r.extractionHaulGoal = snap.round.extractionHaulGoal;
                r.extractionPointActive = snap.round.extractionPointActive;
                r.allExtractionPointsCompleted = snap.round.allExtractionPointsCompleted;
                if (!snap.round.extractionPointActive)
                {
                    r.extractionPointCurrent = null;
                }
            }

            var live = UnityEngine.Object.FindObjectsOfType<ExtractionPoint>().ToList();
            var used = new HashSet<ExtractionPoint>();
            int n = 0;

            foreach (var dto in snap.extraction)
            {
                Vector3 target = dto.pos.ToVector3();
                ExtractionPoint? best = null;
                float bestDist = float.MaxValue;

                foreach (var ep in live)
                {
                    if (ep == null || used.Contains(ep))
                    {
                        continue;
                    }

                    float d = (ep.transform.position - target).sqrMagnitude;
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = ep;
                    }
                }

                if (best != null)
                {
                    best.UpdateLock(false);
                    best.haulGoal = dto.haulGoal;

                    if (dto.state == (int)ExtractionPoint.State.Complete)
                    {
                        // This point was already extracted at save time — keep it completed so it
                        // matches the restored RoundDirector count (and can't be re-extracted).
                        MarkExtractionComplete(best);
                    }
                    else
                    {
                        // Not-yet-completed points reset to Idle (waiting for the player to
                        // activate). Restoring a saved Active/Extracting state would let the point
                        // auto-complete when haul >= goal, bypassing manual-confirm mods.
                        DriveExtractionState(best, ExtractionPoint.State.Idle);
                    }

                    used.Add(best);
                    n++;
                }
            }

            // Make sure no extraction point is left locked/unusable.
            foreach (var ep in live)
            {
                if (ep != null)
                {
                    ep.UpdateLock(false);
                }
            }

            Plugin.Log.LogInfo($"Restore: reset {n} extraction point(s) + round accounting.");
        }

        /// <summary>
        /// Drive an extraction point into its resting Complete state without re-running the
        /// currency award or completed-count increment — those are restored from RoundDto, so
        /// re-running them here would double-count. Setting <c>taxReturn</c> first makes Complete's
        /// stateStart skip that accounting; the platform-hide is gated behind it too, so we hide
        /// the platform ourselves. (Host-authoritative: in multiplayer the state RPC reaches
        /// clients, whose run stats are corrected by the host's normal sync.)
        /// </summary>
        private static void MarkExtractionComplete(ExtractionPoint ep)
        {
            ep.taxReturn = true;
            DriveExtractionState(ep, ExtractionPoint.State.Complete);

            if (ep.platform != null)
            {
                ep.platform.gameObject.SetActive(false);
            }
        }

        private static void DriveExtractionState(ExtractionPoint ep, ExtractionPoint.State state)
        {
            try
            {
                var pv = ep.GetComponent<PhotonView>();
                if (pv != null && SemiFunc.IsMultiplayer())
                {
                    pv.RPC("StateSetRPC", RpcTarget.All, state);
                }
                else
                {
                    ep.StateSetRPC(state);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Restore: extraction StateSet failed: {ex.Message}");
            }
        }

        // ---- Doors / drawers (hinges) -----------------------------------------------

        /// <summary>Restore door/drawer hinge leaf positions and open/broken flags.</summary>
        public static void RestoreHinges(WorldSnapshot snap)
        {
            var live = UnityEngine.Object.FindObjectsOfType<PhysGrabHinge>().ToList();
            var used = new HashSet<PhysGrabHinge>();
            int n = 0;
            HeldHinges.Clear();

            foreach (var dto in snap.hinges)
            {
                Vector3 pivot = dto.pivot.ToVector3();
                PhysGrabHinge? best = null;
                float bestDist = float.MaxValue;

                foreach (var h in live)
                {
                    if (h == null || used.Contains(h) || Clean(h.name) != dto.name)
                    {
                        continue;
                    }

                    Vector3 hp = h.hingePoint != null ? h.hingePoint.position : h.transform.position;
                    float d = (hp - pivot).sqrMagnitude;
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = h;
                    }
                }

                if (best != null)
                {
                    var pgo = best.GetComponent<PhysGrabObject>();
                    if (pgo != null)
                    {
                        Vector3 p = dto.pos.ToVector3();
                        Quaternion q = Quaternion.Euler(dto.euler.ToVector3());
                        pgo.Teleport(p, q);
                        ZeroVelocity(pgo.GetComponent<Rigidbody>());
                        HeldHinges.Add((pgo, p, q));
                    }

                    best.closed = dto.closed;
                    best.broken = dto.broken;
                    best.dead = dto.dead;
                    used.Add(best);
                    n++;
                }
            }

            Plugin.Log.LogInfo($"Restore: reset {n}/{snap.hinges.Count} hinge(s) (doors/drawers).");
        }

        // ---- Grabbables (valuables / items / carts) --------------------------------

        public static void RestoreGrabbables(WorldSnapshot snap)
        {
            var live = UnityEngine.Object.FindObjectsOfType<PhysGrabObject>().ToList();
            var used = new HashSet<PhysGrabObject>();
            Dictionary<string, ValuableSource> valuablePrefabs = BuildValuablePrefabMap();

            int moved = 0, revalued = 0, recreated = 0;

            foreach (var dto in snap.valuables)
            {
                Vector3 target = dto.pos.ToVector3();
                Quaternion rot = Quaternion.Euler(dto.euler.ToVector3());

                PhysGrabObject? best = FindNearestUnused(live, used, dto.name, target);
                if (best != null)
                {
                    best.Teleport(target, rot);
                    ZeroVelocity(best.GetComponent<Rigidbody>());
                    GraceImpact(best);

                    if (dto.kind == "valuable")
                    {
                        var vo = best.GetComponent<ValuableObject>();
                        if (vo != null)
                        {
                            SetValuableValue(vo, dto.value);
                            revalued++;
                        }
                    }

                    used.Add(best);
                    moved++;
                }
                else if (dto.kind == "valuable")
                {
                    // Object no longer exists (it was broken/destroyed) — re-create it.
                    PhysGrabObject? recreatedObj = RecreateValuable(dto, valuablePrefabs);
                    if (recreatedObj != null)
                    {
                        ZeroVelocity(recreatedObj.GetComponent<Rigidbody>());
                        GraceImpact(recreatedObj);
                        recreated++;
                    }
                }
            }

            Plugin.Log.LogInfo(
                $"Restore: moved {moved}, revalued {revalued}, recreated {recreated} / {snap.valuables.Count} object(s).");
        }

        private static PhysGrabObject? FindNearestUnused(
            List<PhysGrabObject> live, HashSet<PhysGrabObject> used, string name, Vector3 target)
        {
            PhysGrabObject? best = null;
            float bestDist = float.MaxValue;

            foreach (var pgo in live)
            {
                if (pgo == null || used.Contains(pgo) || Clean(pgo.name) != name)
                {
                    continue;
                }

                float d = (pgo.transform.position - target).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = pgo;
                }
            }

            return best;
        }

        private static void SetValuableValue(ValuableObject vo, float value)
        {
            try
            {
                var pv = vo.GetComponent<PhotonView>();
                if (pv != null && SemiFunc.IsMultiplayer())
                {
                    pv.RPC("DollarValueSetRPC", RpcTarget.All, value);
                    return;
                }

                vo.DollarValueSetRPC(value);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Restore: could not set value on '{vo.name}': {ex.Message}");
            }
        }

        private static PhysGrabObject? RecreateValuable(ValuableDto dto, Dictionary<string, ValuableSource> map)
        {
            if (!map.TryGetValue(dto.name, out ValuableSource src) || src.Prefab == null)
            {
                Plugin.Log.LogWarning($"Restore: cannot re-create '{dto.name}' — prefab not found in level presets.");
                return null;
            }

            Vector3 pos = dto.pos.ToVector3();
            Quaternion rot = Quaternion.Euler(dto.euler.ToVector3());

            GameObject go = SemiFunc.IsMultiplayer()
                ? PhotonNetwork.InstantiateRoomObject(src.ResourcePath, pos, rot, 0)
                : UnityEngine.Object.Instantiate(src.Prefab, pos, rot);

            if (go == null)
            {
                return null;
            }

            // Force the saved value so its setup doesn't roll a fresh random one.
            var vo = go.GetComponent<ValuableObject>();
            if (vo != null)
            {
                vo.dollarValueOverride = Mathf.RoundToInt(dto.value);
            }

            return go.GetComponent<PhysGrabObject>();
        }

        private static Dictionary<string, ValuableSource> BuildValuablePrefabMap()
        {
            var map = new Dictionary<string, ValuableSource>();

            var level = LevelGenerator.Instance != null ? LevelGenerator.Instance.Level : null;
            if (level != null && level.ValuablePresets != null)
            {
                foreach (var preset in level.ValuablePresets)
                {
                    if (preset == null)
                    {
                        continue;
                    }

                    AddAll(map, preset.tiny);
                    AddAll(map, preset.small);
                    AddAll(map, preset.medium);
                    AddAll(map, preset.big);
                    AddAll(map, preset.wide);
                    AddAll(map, preset.tall);
                    AddAll(map, preset.veryTall);
                }
            }

            // Surplus "money bag" valuables are spawned by extraction points on tax-return, not by
            // the level generator, so they never appear in ValuablePresets. Register them from the
            // AssetManager so a bag left on the cart survives a cross-session restore.
            var am = AssetManager.instance;
            if (am != null)
            {
                AddSurplus(map, am.surplusValuableSmall);
                AddSurplus(map, am.surplusValuableMedium);
                AddSurplus(map, am.surplusValuableBig);
            }

            return map;
        }

        private static void AddAll(Dictionary<string, ValuableSource> map, List<PrefabRef> list)
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
                if (!map.ContainsKey(key))
                {
                    map[key] = new ValuableSource(pr.Prefab, pr.ResourcePath);
                }
            }
        }

        private static void AddSurplus(Dictionary<string, ValuableSource> map, GameObject? prefab)
        {
            if (prefab == null)
            {
                return;
            }

            string key = Clean(prefab.name);
            if (!map.ContainsKey(key))
            {
                // The game network-instantiates these from "Valuables/<name>" (ExtractionPoint.SpawnTaxReturn).
                map[key] = new ValuableSource(prefab, "Valuables/" + prefab.name);
            }
        }

        /// <summary>A spawnable valuable prefab plus the Resources path used to network-instantiate it.</summary>
        private readonly struct ValuableSource
        {
            public readonly GameObject Prefab;
            public readonly string ResourcePath;

            public ValuableSource(GameObject prefab, string resourcePath)
            {
                Prefab = prefab;
                ResourcePath = resourcePath;
            }
        }

        // ---- Shared helpers ---------------------------------------------------------

        private static void HoldHinges()
        {
            foreach (var (pgo, pos, rot) in HeldHinges)
            {
                if (pgo == null)
                {
                    continue;
                }

                var rb = pgo.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.position = pos;
                    rb.rotation = rot;
                    ZeroVelocity(rb);
                }

                pgo.transform.position = pos;
                pgo.transform.rotation = rot;
            }
        }

        private static void GraceImpact(PhysGrabObject pgo)
        {
            var impact = pgo.GetComponent<PhysGrabObjectImpactDetector>();
            if (impact != null)
            {
                impact.ImpactDisable(ImpactGraceSeconds);
            }
        }

        private static void ZeroVelocity(Rigidbody? rb)
        {
            if (rb == null || rb.isKinematic)
            {
                return;
            }

            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        private static string Clean(string name) =>
            name.EndsWith("(Clone)", StringComparison.Ordinal)
                ? name.Substring(0, name.Length - "(Clone)".Length).TrimEnd()
                : name;
    }
}
