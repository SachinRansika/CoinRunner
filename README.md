# Coin Runner — Unity 2D Platformer (Lab 02)

Run, jump, collect coins, dodge spikes, reach the flag.
Controls: **← → / A D** = run, **Space** = jump (only from the ground).

## Open the project
1. Unzip the folder.
2. **Unity Hub > Add > Add project from disk** and pick the `CoinRunner` folder.
3. Choose any **Unity 6 (6000.x)** editor when asked. The first import takes a few minutes.

## First launch (automatic)
When the project finishes importing, the script `Assets/Editor/CoinRunnerBuilder.cs`
builds everything for you and saves it as `Assets/Scenes/CoinRunner.unity`:
Tilemap level on the `Ground` layer, Player + GroundCheck, camera follow,
Coin and Spike prefabs, Goal flag, coin counter, and the LEVEL COMPLETE panel.

If a dialog asks for TextMeshPro resources:
1. **Window > TextMeshPro > Import TMP Essential Resources**
2. **Coin Runner > Build Level (rebuild everything)** (top menu bar)

If it asks to switch Active Input Handling to **Both**, say OK and restart Unity.

Then open `Assets/Scenes/CoinRunner.unity` (if not already open) and press **Play**.

## What is inside
| Path | Content |
|---|---|
| `Assets/Scripts` | PlayerMovement, PlayerInteractions, CameraFollow, GameManager, MovingPlatform (optional) |
| `Assets/Editor/CoinRunnerBuilder.cs` | Builds the scene automatically (menu: Coin Runner) |
| `Assets/Prefabs`, `Tiles`, `Sprites`, `Scenes` | Created by the builder |
| `Docs/` | The step-by-step Word guide (to build it by hand) |

## Notes
- The builder paints the level with code (`Tilemap.SetTile`) so no Tile Palette is needed. To paint by hand,
  follow Part 2 of the guide (Window > 2D > Tile Palette).
- Spikes sit at Y = -2.5 so they rest on top of the ground.
- The player uses a no-friction physics material so it does not stick to walls (Challenge 2).
- Tags used: `Coin`, `Hazard`, `Goal`. Layer used: `Ground`.
