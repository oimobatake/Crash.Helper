using System;
using Crash.Helper.Memory.Runtime;

namespace Crash.Helper.Memory.Camera
{
    // Commit after the game's zoom, shake and override passes, before the final view matrix.
    internal static class CameraFrameCode
    {
        internal const int Hook = 0x377B00;
        internal const int ReturnAcknowledgement = 32, ReturnRequest = 168;
        internal static readonly byte[] Original = { 0x49, 0x8B, 0xD7, 0xE8, 8, 0x94, 0x18, 0 };
        internal static byte[] Build(long cave, long data, long module)
        {
            var c = new RuntimeCode(cave); c.Save(); c.Emit(0x53);
            c.Rip(data, 0x48, 0x8B, 0x1D); c.Emit(0x48, 0x85, 0xDB); c.Branch("restore", 0x0F, 0x84);
            c.Emit(0x4C, 0x89, 0xF8); c.Data(16, 0x48, 0x3B, 0x05); c.Branch("restore", 0x0F, 0x85);
            c.Emit(0x31, 0xC0); c.Rip(data + 8, 0xF0, 0x48, 0x0F, 0xB1, 0x1D); c.Branch("restore", 0x0F, 0x85);
            c.Rip(data, 0x48, 0x3B, 0x1D); c.Branch("release", 0x0F, 0x85);
            c.Guards(0, "release", true);
            c.Data(60, 0x8B, 0x05); c.Emit(0x85, 0xC0); c.Branch("capture", 0x0F, 0x84);
            c.DataImmediate(60, 0, 0xC7, 0x05);
            c.Data(ReturnRequest, 0x48, 0x8B, 0x05); c.Emit(0x48, 0x85, 0xC0); c.Branch("return", 0x0F, 0x84);
            c.Rip(data + ReturnAcknowledgement, 0x48, 0x3B, 0x05); c.Branch("capture", 0x0F, 0x84);
            c.Label("return");
            // Ordinary boxes retain their authored reset behavior. For a transition
            // that requires a previous view, seed the gameplay box with the view itself.
            c.Data(232, 0x48, 0x8B, 0x15);
            c.Emit(0x48, 0x85, 0xD2); c.Branch("capture", 0x0F, 0x84);
            c.Data(128, 0x48, 0x8B, 0x0D); c.Emit(0x48, 0x85, 0xC9); c.Branch("resetCamera", 0x0F, 0x84);
            c.Call(module + 0x36DA30);
            c.Data(128, 0x48, 0x8B, 0x15);
            c.Label("resetCamera"); c.Data(136, 0x48, 0x8B, 0x0D); c.Call(module + 0x36B7B0);
            c.Data(ReturnRequest, 0x48, 0x8B, 0x05); c.Rip(data + ReturnAcknowledgement, 0x48, 0x89, 0x05);
            c.Label("capture");
            c.Rip(data + 16, 0xFF, 0x05);
            c.Emit(0x41, 0x0F, 0x10, 7, 0xF2, 0x41, 0x0F, 0x10, 0x4F, 0x10);
            c.Rip(data + 64, 0x0F, 0x11, 0x05); c.Rip(data + 80, 0xF2, 0x0F, 0x11, 0x0D);
            c.Data(0, 0x48, 0x8B, 0x05); c.Rip(data + 24, 0x48, 0x89, 0x05);
            c.Data(8, 0x8B, 0x05); c.Emit(0x85, 0xC0); c.Branch("applied", 0x0F, 0x84);
            c.Data(32, 0x0F, 0x10, 0x05); c.Data(48, 0xF2, 0x0F, 0x10, 0x0D);
            c.Data(96, 0x0F, 0x11, 0x05); c.Data(112, 0xF2, 0x0F, 0x11, 0x0D);
            c.Data(8, 0x8B, 0x05); c.Emit(0xA8, 12); c.Branch("absolute", 0x0F, 0x84);
            c.Data(24, 0x48, 0x8B, 0x0D); c.Data(144, 0x48, 0x8D, 0x15); c.Call(module + 0xC003B0);
            c.Data(8, 0x8B, 0x05); c.Emit(0xA8, 8); c.Branch("nativeFollow", 0x0F, 0x85);
            for (int axis = 0; axis < 3; axis++)
            {
                c.Data(144 + axis * 4, 0xF3, 0x0F, 0x10, 0x05); c.Data(56, 0xF3, 0x0F, 0x59, 0x05);
                c.Data(96 + axis * 4, 0xF3, 0x0F, 0x58, 0x05); c.Emit(0xF3, 0x41, 0x0F, 0x11, 0x47, (byte)(axis * 4));
            }
            c.Branch("rotation", 0xE9);
            c.Label("nativeFollow"); CameraFollowCode.Emit(c, data); c.Branch("rotation", 0xE9);
            c.Label("absolute"); c.Data(8, 0x8B, 0x05); c.Emit(0xA8, 1); c.Branch("rotation", 0x0F, 0x84);
            c.Data(96, 0x48, 0x8B, 0x05); c.Emit(0x49, 0x89, 7); c.Data(104, 0x8B, 0x05); c.Emit(0x41, 0x89, 0x47, 8);
            c.Label("rotation"); c.Data(8, 0x8B, 0x05); c.Emit(0xA8, 2); c.Branch("applied", 0x0F, 0x84);
            c.Data(112, 0x48, 0x8B, 0x05); c.Emit(0x49, 0x89, 0x47, 0x10);
            c.Label("applied");
            c.Emit(0x41, 0x0F, 0x10, 7, 0xF2, 0x41, 0x0F, 0x10, 0x4F, 0x10);
            c.Rip(data + 96, 0x0F, 0x11, 0x05); c.Rip(data + 112, 0xF2, 0x0F, 0x11, 0x0D); c.Rip(data + 16, 0xFF, 0x05);
            c.Label("release"); c.Emit(0x31, 0xC0); c.Rip(data + 8, 0x48, 0x89, 0x05);
            c.Label("restore"); c.Emit(0x5B); c.Restore(); c.Emit(0x49, 0x8B, 0xD7); c.Rip(module + 0x500F10, 0xE8); c.Emit(RuntimeCode.Jump(c.Address, module + Hook + 8));
            var code = c.Finish(); var result = new byte[0xF0A]; code.CopyTo(result, 0);
            RuntimeCode.Jump(cave + 0xF00, module + 0x500F10).CopyTo(result, 0xF00);
            // A CALL already reached at installation must return to the original continuation.
            // The relocation thunk uses CALL rather than JMP to retain the caller's stack shape.
            result[0xF00] = 0xE8;
            RuntimeCode.Jump(cave + 0xF05, module + Hook + 8).CopyTo(result, 0xF05);
            return result;
        }
    }
}
