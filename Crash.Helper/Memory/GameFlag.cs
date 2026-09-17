using System.Collections.Generic;

namespace Crash.Helper.Memory
{
    public sealed class GameFlag
    {
        public string Group { get; }
        public string Name { get; }
        public GamePointer<bool> Value { get; }

        internal GameFlag(string group, string name, int index)
        {
            Group = group;
            Name = name;
            Value = new GamePointer<bool>(GameMemoryProfile.Steam.Flags[index]);
        }

        internal static IReadOnlyList<GameFlag> CreateAll()
        {
            return new[]
            {
                new GameFlag("Color Gems", "Blue Gem", 0),
                new GameFlag("Color Gems", "Red Gem", 1),
                new GameFlag("Color Gems", "Green Gem", 2),
                new GameFlag("Color Gems", "Yellow Gem", 3),
                new GameFlag("Color Gems", "Purple Gem", 4),
                new GameFlag("Color Gems", "Orange Gem", 5),
                new GameFlag("Super Powers", "Crash 2 - Speed Shoes", 6),
                new GameFlag("Super Powers", "Crash 3 - Super Charged Body Slam", 7),
                new GameFlag("Super Powers", "Crash 3 - Double Jump", 8),
                new GameFlag("Super Powers", "Crash 3 - Death Tornado Spin", 9),
                new GameFlag("Super Powers", "Crash 3 - Fruit Bazooka", 10),
                new GameFlag("Super Powers", "Crash 3 - Speed Shoes", 11)
            };
        }
    }
}
