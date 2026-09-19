namespace Crash.Helper.Memory.Camera
{
    internal sealed class CameraMemoryProfile
    {
        internal byte?[] CapturePattern { get; }
        internal int PositionCodeOffset { get; }
        internal int RotationCodeOffset { get; }
        internal int[] ValueOffsets { get; }
        internal int[] PauseMenu { get; }
        internal int[] Loading { get; }

        internal CameraMemoryProfile(byte?[] capturePattern, int positionCodeOffset, int rotationCodeOffset, int[] valueOffsets)
            : this(capturePattern, positionCodeOffset, rotationCodeOffset, valueOffsets, null) { }

        internal CameraMemoryProfile(byte?[] capturePattern, int positionCodeOffset, int rotationCodeOffset, int[] valueOffsets, int[] pauseMenu, int[] loading = null)
        {
            CapturePattern = (byte?[])capturePattern.Clone();
            PositionCodeOffset = positionCodeOffset;
            RotationCodeOffset = rotationCodeOffset;
            ValueOffsets = (int[])valueOffsets.Clone();
            PauseMenu = pauseMenu == null ? null : (int[])pauseMenu.Clone();
            Loading = loading == null ? null : (int[])loading.Clone();
        }

        internal static readonly CameraMemoryProfile Steam = new CameraMemoryProfile(
            new byte?[] { 0x89, 0x41, 0x18, 0x0F, 0x10, 0x42, 0x1C, 0x0F, 0x11, 0x41, 0x1C, 0xF2, 0x0F, 0x10, 0x4A, 0x2C },
            0x3370D3, 0x3370DB, new[] { 0, 4, 8, 0x14, 0x10 }, new[] { 0x01A5B010, 0x100 }, new[] { 0x01A8FEB8, 0xA0, 0x22C });

        internal static readonly CameraMemoryProfile Xbox = new CameraMemoryProfile(
            Steam.CapturePattern, 0x31FFD3, 0x31FFDB, Steam.ValueOffsets, new[] { 0x01A81A08, 0x100 }, new[] { 0x01AB7AE0, 0xA0, 0x22C });
    }
}
