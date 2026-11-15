using System;
using System.Collections.Generic;

namespace Crash.Helper.Memory.Masks
{
    // One immutable dispatcher per process. Only its data mailbox changes between events.
    internal static class MaskCommandCode
    {
        internal const int GuardOffset = 0x100, GuardLimit = 72, MessageOffset = 0x800, VariantOffset = 0x820;
        internal static readonly byte[] Original = { 0x48, 0x89, 0x5C, 0x24, 0x18 };
        internal const int HookOffset = 0x4E3830;
        internal static byte[] Jump(long from, long to) { var result = new List<byte> { 0xE9 }; result.AddRange(BitConverter.GetBytes(checked((int)(to - from - 5)))); return result.ToArray(); }

        internal static byte[] Build(long cave, long data, long module, int thread)
        {
            var code = new List<byte>(); var labels = new Dictionary<string, int>(); var branches = new List<Tuple<int, string>>();
            Action<byte[]> emit = bytes => code.AddRange(bytes);
            Action<string> label = name => labels.Add(name, code.Count);
            Action<byte[], string> branch = (op, name) => { emit(op); branches.Add(Tuple.Create(code.Count, name)); emit(new byte[4]); };
            Action<byte[], long> rip = (op, target) => { emit(op); emit(BitConverter.GetBytes(checked((int)(target - (cave + code.Count + 4))))); };
            Action<long> call = address => { emit(new byte[] { 0x48, 0xB8 }); emit(BitConverter.GetBytes(address)); emit(new byte[] { 0xFF, 0xD0 }); };
            Action<long, int> status = (address, value) => { emit(new byte[] { 0x48, 0xB8 }); emit(BitConverter.GetBytes(address)); emit(new byte[] { 0xC7, 0x00 }); emit(BitConverter.GetBytes(value)); };
            emit(new byte[] { 0x9C, 0x50, 0x51, 0x52, 0x41, 0x50, 0x41, 0x51, 0x41, 0x52, 0x41, 0x53 });
            emit(new byte[] { 0x48, 0x81, 0xEC, 0xA8, 0, 0, 0 });
            for (int i = 0; i < 6; i++) emit(new byte[] { 0xF3, 0x0F, 0x7F, (byte)(0x44 + i * 8), 0x24, (byte)(0x20 + i * 16) });
            emit(new byte[] { 0x65, 0x81, 0x3C, 0x25, 0x48, 0, 0, 0 }); emit(BitConverter.GetBytes(thread));
            branch(new byte[] { 0x0F, 0x85 }, "restore");
            emit(new byte[] { 0xB8, 1, 0, 0, 0, 0x31, 0xC9 });
            rip(new byte[] { 0xF0, 0x0F, 0xB1, 0x0D }, data);
            branch(new byte[] { 0x0F, 0x85 }, "restore");
            rip(new byte[] { 0x44, 0x8B, 0x15 }, data + 12); // r10d: guard count
            rip(new byte[] { 0x4C, 0x8D, 0x0D }, data + GuardOffset); // r9: guard cursor
            label("guard"); emit(new byte[] { 0x45, 0x85, 0xD2 }); branch(new byte[] { 0x0F, 0x84 }, "run");
            emit(new byte[] { 0x49, 0x8B, 0x01, 0x4D, 0x8B, 0x59, 8, 0x41, 0x8B, 0x49, 16 });
            emit(new byte[] { 0x83, 0xF9, 8 }); branch(new byte[] { 0x0F, 0x84 }, "guard64");
            emit(new byte[] { 0x83, 0xF9, 4 }); branch(new byte[] { 0x0F, 0x84 }, "guard32");
            emit(new byte[] { 0x83, 0xF9, 0xFC }); branch(new byte[] { 0x0F, 0x84 }, "guardMinimum");
            emit(new byte[] { 0x83, 0xF9, 1 }); branch(new byte[] { 0x0F, 0x85 }, "changed");
            emit(new byte[] { 0x44, 0x38, 0x18 }); branch(new byte[] { 0xE9 }, "compare");
            label("guard64"); emit(new byte[] { 0x4C, 0x39, 0x18 }); branch(new byte[] { 0xE9 }, "compare");
            label("guardMinimum"); emit(new byte[] { 0x44, 0x39, 0x18 }); branch(new byte[] { 0x0F, 0x82 }, "changed"); branch(new byte[] { 0xE9 }, "nextGuard");
            label("guard32"); emit(new byte[] { 0x44, 0x39, 0x18 });
            label("compare"); branch(new byte[] { 0x0F, 0x85 }, "changed");
            label("nextGuard");
            emit(new byte[] { 0x49, 0x83, 0xC1, 24, 0x41, 0xFF, 0xCA }); branch(new byte[] { 0xE9 }, "guard");
            Action<int, int, bool> invoke = (metaOffset, methodOffset, signedDelta) =>
            {
                rip(new byte[] { 0x48, 0x8B, 0x0D }, data + metaOffset);
                rip(new byte[] { 0x48, 0x8D, 0x15 }, data + MessageOffset); call(module + 0xDDC490);
                rip(new byte[] { 0x48, 0x8B, 0x15 }, data + MessageOffset);
                emit(new byte[] { 0x48, 0x85, 0xD2 }); branch(new byte[] { 0x0F, 0x84 }, "allocationFailed");
                if (signedDelta) { rip(new byte[] { 0x8B, 0x05 }, data + 40); emit(new byte[] { 0x89, 0x42, 0x38 }); }
                rip(new byte[] { 0x48, 0x8D, 0x0D }, data + VariantOffset); call(module + 0xCBC2A0);
                rip(new byte[] { 0x48, 0x8B, 0x0D }, data + methodOffset); rip(new byte[] { 0x48, 0x8B, 0x15 }, data + 16);
                emit(new byte[] { 0x41, 0xB8, 1, 0, 0, 0 }); rip(new byte[] { 0x4C, 0x8D, 0x0D }, data + VariantOffset); call(module + 0xC988C0);
                rip(new byte[] { 0x48, 0x8D, 0x0D }, data + VariantOffset); call(module + 0xC5EF20);
                rip(new byte[] { 0x48, 0x8B, 0x0D }, data + MessageOffset); call(module + 0xDF0E10);
            };
            label("run");
            // Assist masks are a respawn floor, not a lower bound for explicit helper commands.
            // Restore this field before publishing completion, including allocation failures.
            rip(new byte[] { 0x48, 0x8B, 0x05 }, data + 144); emit(new byte[] { 0xC7, 0, 0, 0, 0, 0 });
            rip(new byte[] { 0x8B, 0x05 }, data + 8);
            emit(new byte[] { 0x83, 0xF8, 2 }); branch(new byte[] { 0x0F, 0x84 }, "restoreMask");
            emit(new byte[] { 0x85, 0xC0 }); branch(new byte[] { 0x0F, 0x84 }, "collect");
            rip(new byte[] { 0x8B, 0x05 }, data + 80); emit(new byte[] { 0x85, 0xC0 }); branch(new byte[] { 0x0F, 0x84 }, "decrement");
            rip(new byte[] { 0x48, 0x8B, 0x0D }, data + 88); call(module + 0xCBC740); // Stop the old expiry callback before ending invincibility.
            invoke(72, 64, false); // End invincibility using the game's own cleanup event.
            label("decrement");
            invoke(32, 24, true); // Signed delta: no damage animation or damage-immunity gate.
            invoke(56, 48, false); // Update the mask model and state after the decrement.
            branch(new byte[] { 0xE9 }, "success");
            label("restoreMask");
            invoke(32, 24, true);
            invoke(104, 96, false); // Ensure the mask exists without stopping the damage-immunity timer.
            invoke(120, 112, false); // Update its appearance without the full state cleanup.
            rip(new byte[] { 0x48, 0x8B, 0x05 }, data + 136); emit(new byte[] { 0x83, 0x38, 2 });
            branch(new byte[] { 0x0F, 0x82 }, "singleTint");
            invoke(168, 160, false); branch(new byte[] { 0xE9 }, "syncState");
            label("singleTint"); invoke(184, 176, false);
            label("syncState");
            // Mirror the refresh graph's currentState bookkeeping, without its timer-abort branch.
            // The count itself has already been changed by the signed-delta graph.
            rip(new byte[] { 0x48, 0x8B, 0x05 }, data + 136); emit(new byte[] { 0x8B, 0x10 });
            rip(new byte[] { 0x48, 0x8B, 0x05 }, data + 128); emit(new byte[] { 0x89, 0x10 });
            branch(new byte[] { 0xE9 }, "success");
            label("collect"); invoke(32, 24, false);
            Action restoreMinimum = () =>
            {
                rip(new byte[] { 0x8B, 0x15 }, data + 152);
                rip(new byte[] { 0x48, 0x8B, 0x05 }, data + 144); emit(new byte[] { 0x89, 0x10 });
            };
            label("success");
            restoreMinimum();
            status(data + 4, 1); branch(new byte[] { 0xE9 }, "restore");
            label("changed"); status(data + 4, 2); branch(new byte[] { 0xE9 }, "restore");
            label("allocationFailed"); restoreMinimum(); status(data + 4, 3);
            label("restore");
            for (int i = 0; i < 6; i++) emit(new byte[] { 0xF3, 0x0F, 0x6F, (byte)(0x44 + i * 8), 0x24, (byte)(0x20 + i * 16) });
            emit(new byte[] { 0x48, 0x81, 0xC4, 0xA8, 0, 0, 0, 0x41, 0x5B, 0x41, 0x5A, 0x41, 0x59, 0x41, 0x58, 0x5A, 0x59, 0x58, 0x9D });
            emit(Original); emit(Jump(cave + code.Count, module + HookOffset + 5));
            var result = code.ToArray();
            foreach (var item in branches) BitConverter.GetBytes(labels[item.Item2] - item.Item1 - 4).CopyTo(result, item.Item1);
            return result;
        }
    }
}
