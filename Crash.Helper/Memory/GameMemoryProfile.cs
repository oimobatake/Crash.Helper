using System.Linq;

namespace Crash.Helper.Memory
{
    internal sealed class GameMemoryProfile
    {
        internal string Name { get; private set; }
        internal int ModuleSize { get; private set; }
        internal int[] Lives { get; private set; }
        internal int[] Masks { get; private set; }
        internal int[] CurrentLevel { get; private set; }
        internal int[] LoadMap { get; private set; }
        internal int[] SecretLevel { get; private set; }
        internal int[] Fade { get; private set; }
        internal int[] Loading { get; private set; }
        internal int[][] Position { get; private set; }
        internal int PositionCodeOffset { get; private set; }
        internal int FadeCodeOffset { get; private set; }
        internal byte[] FadeOriginal { get; private set; }
        internal int[][] Flags { get; private set; }
        internal byte?[] LevelLockPattern { get; private set; }
        internal Camera.CameraMemoryProfile Camera { get; private set; }

        internal static readonly GameMemoryProfile Steam = new GameMemoryProfile
        {
            Name = "Steam", ModuleSize = 30883840,
            Fade = new[] { 0x01A8FEB8, 0x374 },
            Loading = new[] { 0x01A8FEB8, 0xA0, 0x22C },
            Camera = Memory.Camera.CameraMemoryProfile.Steam,
            PositionCodeOffset = 0x12C9578,
            FadeCodeOffset = 0x11D6EF, FadeOriginal = new byte[] { 0x0F, 0x29, 0x41, 0x10 },
            Lives = new[] { 0x01AA27C8, 0x10 },
            Masks = new[] { 0x01A69A98, 0x30, 0x1E0 },
            CurrentLevel = new[] { 0x01A5C6E0 },
            LoadMap = new[] { 0x01A5C6D8, 0x18 },
            SecretLevel = new[] { 0x01A69A98, 0x30, 0x1740 },
            Position = new[]
            {
                new[] { 0x01A5C160, 0x18, 0x8, 0x80 },
                new[] { 0x01A5C160, 0x18, 0x8, 0x84 },
                new[] { 0x01A5C160, 0x18, 0x8, 0x88 }
            },
            Flags = BuildFlags(new[] { 0x01A69A98, 0x30 },
                new[] { 0x1D20, 0x1EA0, 0x1D80, 0x1F00, 0x1E40, 0x1DE0, 0x3220, 0x3280, 0x3100, 0x30A0, 0x3160, 0x31C0 }),
            LevelLockPattern = new byte?[] { 0x41, 0x88, 0x00, 0x48, 0x85, 0xD2, 0x74, null, 0x49, 0x83, 0xC8, 0xFF }
        };

        internal static readonly GameMemoryProfile Xbox = new GameMemoryProfile
        {
            Name = "Xbox(PC)", ModuleSize = 30633984,
            Fade = new[] { 0x01AB9C20, 0x374 },
            Loading = new[] { 0x01AB7AE0, 0xA0, 0x22C },
            Camera = Memory.Camera.CameraMemoryProfile.Xbox,
            PositionCodeOffset = 0x12E2818,
            FadeCodeOffset = 0x10C47A, FadeOriginal = new byte[] { 0x0F, 0x11, 0x41, 0x10 },
            Lives = new[] { 0x01AC9C00, 0x10 },
            Masks = new[] { 0x01A93118, 0x30, 0x1B8 },
            CurrentLevel = new[] { 0x01A84000 },
            LoadMap = new[] { 0x01A83FF8, 0x18 },
            SecretLevel = new[] { 0x01A93118, 0x30, 0x1718 },
            Position = new[]
            {
                new[] { 0x01A83B50, 0xE0, 0x80 },
                new[] { 0x01A83B50, 0xE0, 0x84 },
                new[] { 0x01A83B50, 0xE0, 0x88 }
            },
            Flags = BuildFlags(new[] { 0x01A8E348, 0x320, 0x20, 0xC8, 0x70 },
                new[] { 0x3F8, 0x578, 0x458, 0x5D8, 0x518, 0x4B8 })
                .Concat(BuildFlags(new[] { 0x01AD1288, 0x28, 0x18, 0x88, 0x40, 0x28 },
                    new[] { 0xA28, 0xA88, 0x908, 0x8A8, 0x968, 0x9C8 })).ToArray(),
            LevelLockPattern = new byte?[] { 0x41, 0x88, 0x00, 0x48, 0x85, 0xD2, 0x0F, 0x84, null, null, null, null, 0x48, 0x89, 0x5C, 0x24, 0x30 }
        };

        internal static GameMemoryProfile FromModuleSize(int size)
        {
            if (size == Steam.ModuleSize) return Steam;
            if (size == Xbox.ModuleSize) return Xbox;
            return null;
        }

        private static int[][] BuildFlags(int[] root, int[] offsets)
        {
            var result = new int[offsets.Length][];
            for (int i = 0; i < offsets.Length; i++)
            {
                result[i] = new int[root.Length + 1];
                root.CopyTo(result[i], 0);
                result[i][root.Length] = offsets[i];
            }
            return result;
        }
    }
}
