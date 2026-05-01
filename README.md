# Chess Lane Clash

A small Unity 2D lane-push prototype built around chess-themed units.

This is not a chess rules game. Pieces are treated as unit classes:

- `Pawn`: cheap melee
- `Rook`: tank
- `Knight`: all-rounder with splash damage
- `Bishop`: ranged
- `Queen`: healer
- `King`: melee leader with aura buffs

## Project

- Engine: Unity 2D
- Main scene: `Assets/Scene/GamePlay.unity`
- Main gameplay scripts:
  - `Assets/Scripts/ChessGameManager.cs`
  - `Assets/Scripts/ChessUnit.cs`
  - `Assets/Scripts/ChessBase.cs`
  - `Assets/Scripts/ChessProjectile.cs`
  - `Assets/Scripts/ChessCombatFx.cs`

## How to run

1. Open the project in Unity.
2. Open `Assets/Scene/GamePlay.unity`.
3. Press Play.

## Controls

- Click the unit cards at the bottom to spawn units.
- Hotkeys:
  - `1`: Pawn
  - `2`: Rook
  - `3`: Knight
  - `4`: Bishop
  - `5`: Queen
  - `6`: King

## Game flow

- Your base starts on the left.
- Enemy bases are on the right.
- Units walk automatically and attack whatever is directly ahead of them.
- After an enemy base is destroyed, it becomes the new player base.
- A new enemy base is generated further to the right.

## Art and resources

- Base sprites:
  - `Assets/Image/PlayerBase.png`
  - `Assets/Image/EnemyBase.png`
- Runtime-loaded WebGL-safe copies:
  - `Assets/Resources/PlayerBase.png`
  - `Assets/Resources/EnemyBase.png`
- Background:
  - `Assets/Resources/BattleBackground.png`

## WebGL

The project includes a custom responsive WebGL template:

- `Assets/WebGLTemplates/Responsive`

If you build for WebGL, make sure Unity has reimported the files under `Assets/Resources`, otherwise runtime-loaded sprites may not update correctly.

## Notes

- This project is still in active iteration.
- Balance values are in code, mainly inside `ChessUnit.cs` and `ChessGameManager.cs`.
- A lot of scene UI is authored directly in `GamePlay.unity`, so visual edits are usually easier to do in the Unity editor than in code.
