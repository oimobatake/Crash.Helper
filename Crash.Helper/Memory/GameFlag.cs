using System.Collections.Generic;

namespace Crash.Helper.Memory
{
    public sealed class GameFlag
    {
        public string Group { get; }
        public string Name { get; }
        public GamePointer<bool> Value { get; }

        internal GameFlag(string group, string name, int offset)
        {
            Group = group;
            Name = name;
            Value = new GamePointer<bool>(0x01A69A98, 0x30, offset);
        }

        internal static IReadOnlyList<GameFlag> CreateAll()
        {
            return new[]
            {
                new GameFlag("Color Gems", "Blue Gem", 0x1D20),
                new GameFlag("Color Gems", "Red Gem", 0x1EA0),
                new GameFlag("Color Gems", "Green Gem", 0x1D80),
                new GameFlag("Color Gems", "Yellow Gem", 0x1F00),
                new GameFlag("Color Gems", "Purple Gem", 0x1E40),
                new GameFlag("Color Gems", "Orange Gem", 0x1DE0),
                new GameFlag("Super Powers", "Crash 2 - Speed Shoes", 0x3220),
                new GameFlag("Super Powers", "Crash 3 - Super Charged Body Slam", 0x3280),
                new GameFlag("Super Powers", "Crash 3 - Double Jump", 0x3100),
                new GameFlag("Super Powers", "Crash 3 - Death Tornado Spin", 0x30A0),
                new GameFlag("Super Powers", "Crash 3 - Fruit Bazooka", 0x3160),
                new GameFlag("Super Powers", "Crash 3 - Speed Shoes", 0x31C0)
            };
        }
    }
}
