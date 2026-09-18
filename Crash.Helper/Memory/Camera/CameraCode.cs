using System;
using System.Collections.Generic;
using System.Text;

namespace Crash.Helper.Memory.Camera
{
    internal static class CameraCode
    {
        internal static readonly byte[] CaptureOriginal = { 0x89, 0x41, 0x18, 0x0F, 0x10, 0x42, 0x1C };
        internal static readonly byte[] PositionOriginal = { 0x0F, 0x11, 0x01 };
        internal static readonly byte[] RotationOriginal = { 0xF2, 0x0F, 0x11, 0x49, 0x10 };
        internal static readonly byte[] OwnerMarker = Encoding.ASCII.GetBytes("CrashHelperCamera1");

        internal static byte[] Jump(long from, long to, int length)
        {
            var bytes = new byte[length];
            bytes[0] = 0xE9;
            BitConverter.GetBytes(checked((int)(to - from - 5))).CopyTo(bytes, 1);
            for (int i = 5; i < length; i++) bytes[i] = 0x90;
            return bytes;
        }

        internal static byte[] Build(long cave, long pointerStorage, long returnAddress)
        {
            var bytes = new List<byte> { 0x89, 0x41, 0x18 }; // mov [rcx+18],eax
            bytes.AddRange(new byte[] { 0x48, 0x89, 0x0D }); // mov [rip+disp32],rcx
            bytes.AddRange(BitConverter.GetBytes(checked((int)(pointerStorage - (cave + 10)))));
            bytes.AddRange(new byte[] { 0x0F, 0x10, 0x42, 0x1C }); // movups xmm0,[rdx+1C]
            bytes.AddRange(Jump(cave + bytes.Count, returnAddress, 5));
            return bytes.ToArray();
        }
    }
}
