# HS Doors

You build automatic sliding doors and revolving doors. The mod captures the moving blocks and slides or rotates them. Drum glass, leaves, the operator header, and the 3-wing rotor are extra building blocks.

Same idea as [HS Lift](https://github.com/Crumb3D/HS_Lift) and [HS Escalator](https://github.com/Crumb3D/HS_Escalator): you place the parts, the setup tool marks them, a wired panel runs them.

## Sliding door

1. Build the leaves (our leaf blocks, vanilla glass sheets, or anything else).
2. Place an **HS Door Panel** beside the opening and wire it.
3. Door Setup Tool, hold E: **New Sliding Door**, **Set Corner 1** and **Set Corner 2** on the leaf area, **Register Panel**.
4. **Cycle Mode** for bi-parting, single left, single right, or telescopic.

## Revolving door

1. Build the fixed drum. **Glass** (3 m / 5 m drum sides, or 1 m stackable drum walls) or **solid paint-able** (`Revolving Wall Solid` — steel, paintbrush works; R cycles plate / thick / glass). Those drum walls stay in the world; only the wings/rotor move.
2. Build the wings around a hub, or place the **3-Wing Revolving Rotor**.
3. **New Revolving Door**, **Set Hub** on the centre post, **Set Radius / Top** on the outer edge / top, **Register Panel**.
4. Mode: continuous, on-demand (runs while the sensor sees someone, then stops on a wing), or manual.

`hsdoors exclude` keeps an extra cell still if the capture grabbed a drum leftover.

## Panel (hold E)

Enable / Disable, Reverse, Safety reverse, Trap mode, Cycle lock, Cycle sensor, Cycle unpowered (fail open / closed / stay).

Defaults are safe. Host caps live in `HSDoorsSettings.json` (mod folder, then `%AppData%/7DaysToDie/HSDoors/`, then the world save — last wins).

Admin: `giveself hsdoorsTool` / `giveself hsdoorsPanel` and `hsdoors`.

## Rebuild the models

Needs **Blender** (any recent) and **Unity 2022.3.62f2** — the game's version, not Unity 6.

```
powershell -File build_assets.ps1
```

That writes `Resources/HSDoors.unity3d`. `_blender/` and `_unity/` are source; they do not need to ship in a release zip.
