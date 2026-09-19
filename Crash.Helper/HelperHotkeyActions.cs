using System.Collections.Generic;
using Crash.Helper.Controls;
using Crash.Helper.Memory;

namespace Crash.Helper
{
    internal static class HelperHotkeyActions
    {
        public static IReadOnlyList<Hotkey> Create(CrashMemory memory, DataControl data, LevelSelectorControl levels, PositionControl position, CameraControl camera)
        {
            var hotkeys = new List<Hotkey>
            {
                new Hotkey("Set lives to 0", KeyModifiers.None, 0, () => data.SetLives(0)),
                new Hotkey("Set lives to 99", KeyModifiers.None, 0, () => data.SetLives(99)),
                new Hotkey("Mask +1", KeyModifiers.None, 0, () => { data.StoredMasks = memory.Masks.Read() + 1; data.Masks = data.StoredMasks; }),
                new Hotkey("Mask -1", KeyModifiers.None, 0, () => { data.StoredMasks = memory.Masks.Read() - 1; data.Masks = data.StoredMasks; }),
                new Hotkey("Freeze lives", KeyModifiers.None, 0, data.ToggleFreezeLives),
                new Hotkey("Freeze masks", KeyModifiers.None, 0, data.ToggleFreezeMasks),
                new Hotkey("Freeze current level", KeyModifiers.None, 0, data.ToggleCurrentLevel),
                new Hotkey("Secret level", KeyModifiers.None, 0, data.ToggleSecretLevel),
                new Hotkey("Level Lock / Stop Lock", KeyModifiers.None, 0, levels.ToggleLock),
                new Hotkey("Previous level", KeyModifiers.None, 0, () => levels.MoveSelection(-1)),
                new Hotkey("Next level", KeyModifiers.None, 0, () => levels.MoveSelection(1)),
                new Hotkey("Launch Game", KeyModifiers.None, 0, levels.LaunchSelectedLevel),
                new Hotkey("Freeze X", KeyModifiers.None, 0, () => position.ToggleFreeze(0)),
                new Hotkey("Freeze Y", KeyModifiers.None, 0, () => position.ToggleFreeze(1)),
                new Hotkey("Freeze Z", KeyModifiers.None, 0, () => position.ToggleFreeze(2)),
                PositionMovement("Forward", 1, 1),
                PositionMovement("Back", 1, -1),
                PositionMovement("Left", 0, -1),
                PositionMovement("Right", 0, 1),
                PositionMovement("Up", 2, 1),
                PositionMovement("Down", 2, -1),
                new Hotkey("Position XYZ Speed x2", KeyModifiers.None, 0, () => { }) { RepeatWhileHeld = true, PositionSpeedBoost = true },
                new Hotkey("Position Save", KeyModifiers.None, 0, position.SavePosition),
                new Hotkey("Position TP", KeyModifiers.None, 0, position.Teleport),
                new Hotkey("Control XYZ", KeyModifiers.None, 0, () => camera.ToggleFreeze(false)),
                new Hotkey("Control YawPitch", KeyModifiers.None, 0, () => camera.ToggleFreeze(true)),
                CameraMovement("Forward", 1, 1),
                CameraMovement("Back", 1, -1),
                CameraMovement("Left", 0, -1),
                CameraMovement("Right", 0, 1),
                CameraMovement("Up", 2, 1),
                CameraMovement("Down", 2, -1),
                CameraMovement("Yaw (Left)", 3, 1),
                CameraMovement("Yaw (Right)", 3, -1),
                CameraMovement("Pitch (Up)", 4, -1),
                CameraMovement("Pitch (Down)", 4, 1),
                new Hotkey("XYZ Speed x2", KeyModifiers.None, 0, () => { }) { RepeatWhileHeld = true, CameraSpeedBoost = true },
                new Hotkey("Camera Save", KeyModifiers.None, 0, camera.SaveCamera),
                new Hotkey("Camera TP", KeyModifiers.None, 0, camera.Teleport)
            };
            for (int i = 0; i < hotkeys.Count; i++)
                hotkeys[i].Group = i < 6 ? "Data" : i < 12 ? "Level" : i < 24 ? "Position" : "Camera";
            return hotkeys;
        }

        private static Hotkey CameraMovement(string name, int axis, int direction)
        {
            // Keep movement axes independent of the display order.
            return new Hotkey("Camera " + name, KeyModifiers.None, 0, () => { })
            {
                RepeatWhileHeld = true, CameraAxis = axis, CameraDirection = direction
            };
        }

        private static Hotkey PositionMovement(string name, int axis, int direction) =>
            new Hotkey("Position " + name, KeyModifiers.None, 0, () => { })
            {
                RepeatWhileHeld = true, PositionAxis = axis, PositionDirection = direction
            };
    }
}
