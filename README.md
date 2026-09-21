# Crash.Helper

CrashHelper is a speedrun practice tool for Crash Bandicoot N. Sane Trilogy.  
It lets you adjust lives and masks, repeat levels, return to saved positions, and control the camera while the game is running.  
Use it to practice difficult sections, explore levels, or set up camera angles.

## Compatibility

- Supports recognized builds of the Steam and Xbox PC versions of the game.
- Requires Windows and .NET Framework 4.8.
- The Process panel shows the detected version. If it shows "Unknown", game-editing controls stay disabled because the build is not recognized.
- `Launch Game` uses Steam. For the Xbox PC version, start the game yourself, then let CrashHelper connect.

## Getting started

1. Open `Crash.Helper.exe` and start the game.
2. Keep `Helper enabled` checked. CrashHelper automatically looks for the game and shows "Process attached." when connected.
3. Use the Data and Level panels for lives, masks, and level selection.
4. Open `Settings` to assign keyboard shortcuts, change the Steam application path, or edit game flags.
5. Turn on `Advanced Controls` to show the Position, Camera, and Others panels.

To start the Steam game at a particular level, select a level and click `Launch Game` before starting the game. If Steam is installed in a different location, choose "steam.exe" under [Settings] -> [CrashHelper] -> "Steam application".

## What you can do

### Adjust lives and masks

| Control | What it does |
| --- | --- |
| Lives + / - | Changes lives one at a time, from 0 to 999. |
| Freeze lives | Keeps lives at the stored value. You can still change that value with the helper. |
| Masks + / - | Changes the stored mask count between 0 and 2. |
| Freeze masks | Holds the selected mask count. |
| Maskform when hit | Available while masks are frozen. Maintains the mask state used for mask form on damage, while allowing the game's hit transition. It does not give you a mask when the current count is zero. |

Keyboard actions can also set lives directly to 0 or 99.

### Select and repeat levels

The level list covers all three games. Optional preview images help you identify the selected level.

| Control | What it does |
| --- | --- |
| Level Lock | keeps the selected level as the destination for subsequent level loads. |
| Freeze current level | locks the level you are currently playing. |
| Stop Lock | releases the lock so the game can choose its normal destination again. |
| Launch Game | starts the Steam game with the selected level as its starting map. |
| Secret level | Can only be used when the level is locked in one of Air Crash, Snow Go, Road to Ruin, Hang'em High, or Future Frenzy, allowing you to start from the secret entrance. |

Level Lock changes the load destination; it does not instantly teleport you into another level. Trigger a level load in the game to use the locked destination.

### Save and change the player's position

Turn on `Advanced Controls` to use the Position panel.

- View or edit X, Y, and Z coordinates. Press Enter to apply an edited value.
- Freeze each coordinate separately to hold the player on that axis.
- Click `Save` to remember the current position, then `TP` (teleport) to return to it.
- Assign movement shortcuts for forward, back, left, right, up, and down. Enable the Freeze axes you want to control.
- Adjust movement speed or hold the assigned speed-boost shortcut to move at twice that speed.

Position Save stores coordinates, not a full game state. It does not restore enemies, items, or level progress.

### Control the camera

The Camera panel lets you arrange a view independently of the game's normal camera movement.

- `Control XYZ` gives you control over the camera's position.
- `Control YawPitch` gives you control over its horizontal and vertical viewing angles.
- Edit values and press Enter, or use assigned movement and rotation shortcuts.
- Enable `Mouse control` in Settings to look around with the mouse. Separate X/Y sensitivity and inverted Y options are available.
- Choose whether camera movement follows its pitch, and adjust movement and rotation speeds.
- Click `Save` to remember a view and `TP` to restore it. Only the enabled Control groups are restored.

Position freezes and camera Control checkboxes reset when loading begins. Enable them again after the level loads. Advanced input is also paused during the game's pause menu.

### Change gems and powers

Open [Settings] -> [GameFlag] to toggle the game's stored unlock flags:

- Color gems: blue, red, green, yellow, purple, and orange.
- Crash 2: Speed Shoes.
- Crash 3: Super Charged Body Slam, Double Jump, Death Tornado Spin, Fruit Bazooka, and Speed Shoes.

These checkboxes apply changes to the running game immediately.

### Control fade behavior

Under [Advanced Controls] -> [Others], `Disable fade write` stops the game from updating its fade value. While this option is enabled, player-position editing and movement are disabled and position freezes are cleared.

## Keyboard shortcuts and settings

Open [Settings] -> [CrashHelper] to assign shortcuts. Select a shortcut field and press a key or key combination, then click `Save`. Shortcuts start unassigned; `Reset` clears an individual binding.

Available actions include lives and masks, level selection and locking, player movement and teleporting, camera movement and saved views, input toggles, and fade control.

Shortcuts require the helper to be enabled and connected to a recognized game build. They work while the game or helper is active and pause while you edit input fields. Mouse camera control requires the game to be active.

Preferences and shortcut bindings are stored in `CrashHelper_Settings.json` beside the executable. This file stores helper settings, not a game save or a saved position/camera snapshot.
