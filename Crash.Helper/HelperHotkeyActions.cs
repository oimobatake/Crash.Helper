using System.Collections.Generic;
using Crash.Helper.Controls;
using Crash.Helper.Memory;

namespace Crash.Helper
{
    internal static class HelperHotkeyActions
    {
        public static IReadOnlyList<Hotkey> Create(CrashMemory memory, DataControl data, LevelSelectorControl levels, LocationControl location)
        {
            return new[]
            {
                new Hotkey("Set lives to 0", KeyModifiers.None, 0, () => data.SetLives(0)),
                new Hotkey("Set lives to 99", KeyModifiers.None, 0, () => data.SetLives(99)),
                new Hotkey("mask +1", KeyModifiers.None, 0, () => { data.StoredMasks = memory.Masks.Read() + 1; data.Masks = data.StoredMasks; }),
                new Hotkey("mask -1", KeyModifiers.None, 0, () => { data.StoredMasks = memory.Masks.Read() - 1; data.Masks = data.StoredMasks; }),
                new Hotkey("Freeze lives", KeyModifiers.None, 0, data.ToggleFreezeLives),
                new Hotkey("Freeze masks", KeyModifiers.None, 0, data.ToggleFreezeMasks),
                new Hotkey("Freeze current level", KeyModifiers.None, 0, data.ToggleCurrentLevel),
                new Hotkey("Secret level", KeyModifiers.None, 0, data.ToggleSecretLevel),
                new Hotkey("Level Lock / Stop Lock", KeyModifiers.None, 0, levels.ToggleLock),
                new Hotkey("Previous level", KeyModifiers.None, 0, () => levels.MoveSelection(-1)),
                new Hotkey("Next level", KeyModifiers.None, 0, () => levels.MoveSelection(1)),
                new Hotkey("Launch Game", KeyModifiers.None, 0, levels.LaunchSelectedLevel),
                new Hotkey("Freeze X", KeyModifiers.None, 0, () => location.ToggleFreeze(0)),
                new Hotkey("Freeze Y", KeyModifiers.None, 0, () => location.ToggleFreeze(1)),
                new Hotkey("Freeze Z", KeyModifiers.None, 0, () => location.ToggleFreeze(2)),
                new Hotkey("Save location", KeyModifiers.None, 0, location.SaveLocation),
                new Hotkey("Teleport", KeyModifiers.None, 0, location.Teleport)
            };
        }
    }
}
