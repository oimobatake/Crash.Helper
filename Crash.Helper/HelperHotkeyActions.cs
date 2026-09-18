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
                new Hotkey("Position Save", KeyModifiers.None, 0, position.SavePosition),
                new Hotkey("Position TP", KeyModifiers.None, 0, position.Teleport),
                new Hotkey("Freeze XYZ", KeyModifiers.None, 0, () => camera.ToggleFreeze(false)),
                new Hotkey("Freeze YawPitch", KeyModifiers.None, 0, () => camera.ToggleFreeze(true)),
                new Hotkey("Camera Save", KeyModifiers.None, 0, camera.SaveCamera),
                new Hotkey("Camera TP", KeyModifiers.None, 0, camera.Teleport)
            };
            for (int i = 0; i < hotkeys.Count; i++)
                hotkeys[i].Group = i < 6 ? "Data" : i < 12 ? "Level" : i < 17 ? "Position" : "Camera";
            string[] axes = { "X", "Y", "Z", "Yaw", "Pitch" };
            for (int i = 0; i < axes.Length; i++)
            {
                int axis = i;
                hotkeys.Add(new Hotkey("Camera " + axes[i] + "+", KeyModifiers.None, 0, () => { }) { Group = "Camera", RepeatWhileHeld = true, CameraAxis = axis, CameraDirection = 1 });
                hotkeys.Add(new Hotkey("Camera " + axes[i] + "-", KeyModifiers.None, 0, () => { }) { Group = "Camera", RepeatWhileHeld = true, CameraAxis = axis, CameraDirection = -1 });
            }
            return hotkeys;
        }
    }
}
