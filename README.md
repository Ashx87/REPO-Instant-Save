# REPO Instant Save

A BepInEx 5 / Harmony plugin for **R.E.P.O.** that adds an instant quicksave on top of the
game's own progression save.

## Features

- **F7 — Instant Save.** Captures the current world state (players, valuables, carts, hinges,
  extraction points, haul/round accounting) and triggers the game's native save, then writes a
  snapshot alongside it. A HUD toast confirms the save completed.
- **Automatic cross-session restore.** If you quit and reload a save that carries a snapshot, the
  mod rebuilds the exact same level layout and restores the captured state on top of it — no
  manual restore hotkey, it's handled for you.
- **No-permadelete safety toggle.** Optionally prevents the game's automatic save-delete on team
  wipe / early leave, so a bad run doesn't erase your progress.

## Requirements

- [BepInEx 5.4.x](https://github.com/BepInEx/BepInEx) for R.E.P.O.
- Host / singleplayer only — saving and restoring are host-authoritative.

## Usage

Press **F7** in a level to save. That's it — restore fires automatically the next time you load
that save.

## Configuration

Settings are exposed via the standard BepInEx config file (`BepInEx/config/Ashx87.REPOInstantSave.cfg`):

- Instant Save hotkey (default: F7) and enable toggle
- No-permadelete toggle

## Notes

There is no reproducible level seed in R.E.P.O., so cross-session restore works by forcing the
game to regenerate the identical tile layout from the snapshot before reapplying world state.
See the project repository for full technical details.
