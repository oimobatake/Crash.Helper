namespace Crash.Helper.Memory.Camera
{
    internal sealed class CameraMemoryProfile
    {
        internal byte?[] CapturePattern { get; }
        internal int PositionCodeOffset { get; }
        internal int RotationCodeOffset { get; }
        internal int[] ValueOffsets { get; }

        internal CameraMemoryProfile(byte?[] capturePattern, int positionCodeOffset, int rotationCodeOffset, int[] valueOffsets)
        {
            CapturePattern = (byte?[])capturePattern.Clone();
            PositionCodeOffset = positionCodeOffset;
            RotationCodeOffset = rotationCodeOffset;
            ValueOffsets = (int[])valueOffsets.Clone();
        }

        internal static readonly CameraMemoryProfile Steam = new CameraMemoryProfile(
            new byte?[] { 0x89, 0x41, 0x18, 0x0F, 0x10, 0x42, 0x1C, 0x0F, 0x11, 0x41, 0x1C, 0xF2, 0x0F, 0x10, 0x4A, 0x2C },
            0x3370D3, 0x3370DB, new[] { 0, 4, 8, 0x14, 0x10 });
    }
}
