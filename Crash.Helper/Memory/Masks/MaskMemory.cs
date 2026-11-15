using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Crash.Helper.Memory.LevelLock;

namespace Crash.Helper.Memory.Masks
{
    // A normal loading/spawn transition, not a failure of the mask implementation.
    internal sealed class MaskNotReadyException : InvalidOperationException
    {
        internal MaskNotReadyException(string message) : base(message) { }
    }

    internal sealed class MaskGuard
    {
        internal readonly long Address, Value;
        internal readonly int Size;
        internal MaskGuard(long address, long value, int size = 8) { Address = address; Value = value; Size = size; }
    }

    internal sealed class MaskBinding
    {
        internal long Actor, Owner, AddMethod, AddMeta, RemoveMethod, RemoveMeta, RefreshMethod, RefreshMeta, EndMethod, EndMeta;
        internal long SpawnMethod, SpawnMeta, ModelMethod, ModelMeta;
        internal long TintOnMethod, TintOnMeta, TintOffMethod, TintOffMeta, MinimumAddress;
        internal long InvincibilityTimer;
        internal long RuntimeStateAddress;
        internal string Level;
        internal readonly List<MaskGuard> Guards = new List<MaskGuard>();
        internal bool SameScene(MaskBinding other) => other != null && Actor == other.Actor && Owner == other.Owner && Level == other.Level;
    }

    internal sealed class MaskSnapshot
    {
        internal int Count;
        internal int RuntimeState;
        internal int Minimum;
        internal bool Invincible, DamagedInvulnerable, Dead, Ready, Paused;
        internal MaskBinding Binding;
        internal long CountAddress, InvincibleAddress, DamagedInvulnerableAddress;
        internal bool CanConfigure => Binding != null && !Dead && Count >= 0 && Count <= 3 && (!Invincible || Binding.InvincibilityTimer != 0);
        internal static readonly MaskSnapshot Unavailable = new MaskSnapshot();
    }

    // Engine-owned lists identify the current actor. No heap scans or saved heap addresses are used.
    internal sealed class MaskMemory : IDisposable
    {
        internal readonly Process Process;
        internal readonly IntPtr Handle;
        internal readonly long Base;
        private MaskBinding binding;
        private long countAddress, invincibleAddress, deadAddress, damagedInvulnerableAddress;
        private readonly List<MaskGuard> variableGuards = new List<MaskGuard>();
        internal const string ManagerType = "Scripts.Graph.Crash_AkuAkuManager";
        internal const string AddEvent = "Crash_AkuAkuManager_c.GraphEvent_id_80tnqnrs_Definition";
        internal const string RemoveEvent = "Crash_AkuAkuManager_c.New_Graph_Event_1_id_ibjgf1k9";
        internal const string RefreshEvent = "Crash_AkuAkuManager_c.GraphEvent_id_9becl2sc_Definition";
        internal const string EndEvent = "Crash_AkuAkuManager_c.GraphEvent_id_xjwze7ot_Definition";
        internal const string SpawnEvent = "Crash_AkuAkuManager_c.GraphEvent_id_a9nnfx8i_Definition";
        internal const string ModelEvent = "Crash_AkuAkuManager_c.New_Graph_Event_1_id_9nj5heau";
        internal const string TintOnEvent = "Crash_AkuAkuManager_c.GraphEvent_id_5sryuo67_Definition";
        internal const string TintOffEvent = "Crash_AkuAkuManager_c.GraphEvent_id_202aweq4_Definition";

        internal MaskMemory(Process target)
        {
            Process = System.Diagnostics.Process.GetProcessById(target.Id);
            if (Process.MainModule.ModuleMemorySize != GameMemoryProfile.Steam.ModuleSize)
            { Process.Dispose(); throw new NotSupportedException("Mask events are supported by the inspected Steam build only."); }
            Base = Process.MainModule.BaseAddress.ToInt64();
            Handle = LevelLockNative.OpenProcess(0xC38, false, Process.Id);
            if (Handle == IntPtr.Zero) { Process.Dispose(); LevelLockNative.Check(false, "Open mask memory"); }
        }

        internal byte[] Read(long address, int size)
        {
            var data = new byte[size]; UIntPtr count;
            LevelLockNative.Check(LevelLockNative.ReadProcessMemory(Handle, new IntPtr(address), data, (UIntPtr)size, out count) && count.ToUInt64() == (ulong)size, "Read mask memory");
            return data;
        }
        internal long Number(long address, int size = 8)
        {
            var data = Read(address, size);
            return size == 8 ? BitConverter.ToInt64(data, 0) : size == 4 ? (long)BitConverter.ToUInt32(data, 0) : data[0];
        }
        internal string Text(long address, int size = 180) => Encoding.UTF8.GetString(Read(address, size)).Split('\0')[0];
        internal bool Matches(IEnumerable<MaskGuard> guards) => guards.All(g => g.Size == -4
            ? Number(g.Address, 4) >= g.Value : Number(g.Address, g.Size) == g.Value);
        internal void Require(long address, byte[] expected)
        {
            if (!Read(address, expected.Length).SequenceEqual(expected)) throw new InvalidOperationException("Mask runtime instructions changed; no event was sent.");
        }
        internal string TypeName(long address, out long meta)
        {
            meta = 0;
            if (address == 0) return null;
            long getter = Number(Number(address));
            var code = Read(getter, 16);
            if (code[0] == 0x48 && code[1] == 0x8B && code[2] == 0x05)
                meta = Number(getter + 7 + BitConverter.ToInt32(code, 3));
            else if (code[0] == 0x48 && code[1] == 0x8B && code[2] == 0x41 && code[4] == 0xC3)
                meta = Number(address + code[3]);
            else return null;
            return Text(Number(meta + 0x10));
        }
        private void RequireType(long address, string type)
        {
            long meta;
            if (TypeName(address, out meta) != type) throw new InvalidOperationException("The mask player is not ready.");
        }
        private long Capture(List<MaskGuard> guards, long address, int size = 8)
        {
            long value = Number(address, size); guards.Add(new MaskGuard(address, value, size)); return value;
        }
        private long Pointer(List<MaskGuard> guards, long address)
        {
            long value = Capture(guards, address);
            if (value == 0) throw new MaskNotReadyException("The mask player has not spawned.");
            return value;
        }
        private long[] List(long address, string type, int limit, List<MaskGuard> guards, out long array, bool fixedCount = true)
        {
            RequireType(address, type);
            int count = checked((int)(fixedCount ? Capture(guards, address + 0x10, 4) : Number(address + 0x10, 4)));
            long capacity = Number(address + 0x14, 4), bytes = Number(address + 0x18, 4);
            if (count == 0) throw new MaskNotReadyException("The mask list is loading.");
            if (count > limit || capacity < count || capacity > limit || bytes < count * 8L)
                throw new InvalidOperationException("The mask list is loading.");
            array = Pointer(guards, address + 0x20);
            var data = Read(array, count * 8);
            return Enumerable.Range(0, count).Select(i => BitConverter.ToInt64(data, i * 8)).ToArray();
        }

        private void LocateVariables()
        {
            variableGuards.Clear();
            long root = Pointer(variableGuards, Base + 0x1A69A98);
            long state = Pointer(variableGuards, root + 0x20);
            long store = Pointer(variableGuards, state + 0x21F8);
            long table = Pointer(variableGuards, store + 0x10);
            int size = checked((int)Number(table + 0x10, 4));
            if (size < 8 || size > 0x100000 || size % 8 != 0) throw new InvalidOperationException("Invalid mask variable table.");
            long valuesAddress = Pointer(variableGuards, table + 0x18), handlesAddress = Pointer(variableGuards, table + 0x28);
            var values = Read(valuesAddress, size); var handles = Read(handlesAddress, size);
            var found = new Dictionary<string, long>();
            for (int i = 0; i < size; i += 8)
            {
                long handle = BitConverter.ToInt64(handles, i), value = BitConverter.ToInt64(values, i);
                if (handle == 0 || value == 0) continue;
                string name = Text(Number(handle + 8), 64);
                if (name != "AkuAkuCount" && name != "AkuAkuInvincible" && name != "PlayerDied" && name != "DamagedInvulnerable") continue;
                RequireType(value, "CGameVariableValue");
                RequireType(Number(handle + 0x28), name == "AkuAkuCount" ? "CGameIntVariable" : "CGameBoolVariable");
                if (found.ContainsKey(name)) throw new InvalidOperationException("Mask variable is not unique.");
                found.Add(name, value + 0x10);
                Capture(variableGuards, valuesAddress + i); Capture(variableGuards, handlesAddress + i);
            }
            countAddress = found["AkuAkuCount"]; invincibleAddress = found["AkuAkuInvincible"]; deadAddress = found["PlayerDied"];
            damagedInvulnerableAddress = found["DamagedInvulnerable"];
        }

        private MaskBinding Discover()
        {
            var result = new MaskBinding(); var guards = result.Guards;
            long loading = Pointer(guards, Base + 0x1A8FEB8), scene = Pointer(guards, loading + 0xA0);
            if (Capture(guards, scene + 0x22C, 1) != 0) throw new MaskNotReadyException("The mask scene is loading.");
            long pause = Pointer(guards, Base + 0x1A5B010);
            guards.Add(new MaskGuard(pause + 0x100, 0, 1));
            result.Level = Text(Base + 0x1A5C6E0, 128);
            if (result.Level.Length == 0) throw new MaskNotReadyException("No active mask level.");
            for (int i = 0; i <= Encoding.UTF8.GetByteCount(result.Level); i += 8) Capture(guards, Base + 0x1A5C6E0 + i);
            long manager = Pointer(guards, Base + 0x1A665E8); RequireType(manager, "CPlayerManager");
            long array;
            var players = List(Pointer(guards, manager + 0x10), "CPlayerList", 64, guards, out array);
            var local = players.Select((p, i) => new { Address = p, Index = i }).Where(p => p.Address != 0 && Number(p.Address + 0x94, 4) == 0).ToArray();
            if (local.Length == 0) throw new MaskNotReadyException("The local mask player is loading.");
            if (local.Length != 1) throw new InvalidOperationException("No unique local mask player.");
            long player = local[0].Address; RequireType(player, "CPlayer"); Capture(guards, array + local[0].Index * 8);
            long hero = Pointer(guards, player + 0x10);
            result.Actor = Pointer(guards, hero + 0x28); RequireType(result.Actor, "CActor");
            long componentList = Pointer(guards, result.Actor + 0x18);
            var components = List(componentList, "igComponentList", 4096, guards, out array, false);
            long ownerMeta = 0;
            for (int i = 0; i < components.Length; i++)
            {
                long meta;
                if (components[i] == 0 || TypeName(components[i], out meta) != ManagerType) continue;
                if (result.Owner != 0) throw new InvalidOperationException("Mask manager is not unique.");
                result.Owner = components[i]; ownerMeta = meta; Capture(guards, array + i * 8);
                // Mask visuals append/remove components. Guard the manager's slot, not the changing tail.
                guards.Add(new MaskGuard(componentList + 0x10, i + 1, -4));
            }
            if (result.Owner == 0) throw new MaskNotReadyException("The current character has no mask manager.");
            if (Capture(guards, result.Owner + 0x18) != result.Actor || Capture(guards, result.Owner + 0x38) != ownerMeta)
                throw new InvalidOperationException("The current character has no mask manager.");
            var delegators = List(Pointer(guards, ownerMeta + 0xA0), "igVscDelegatorList", 4096, guards, out array);
            foreach (var entry in delegators)
            {
                long meta;
                if (entry == 0 || TypeName(entry, out meta) != "CVscComponentEntityMessageDelegator") continue;
                long message = Number(entry + 0x18); string name = Text(Number(message + 0x10));
                if (name != AddEvent && name != RemoveEvent && name != RefreshEvent && name != EndEvent && name != SpawnEvent && name != ModelEvent && name != TintOnEvent && name != TintOffEvent) continue;
                Capture(guards, entry + 0x18); long method = Pointer(guards, entry + 0x10); RequireType(method, "igVscMethod");
                if (name == AddEvent)
                {
                    if (result.AddMethod != 0) throw new InvalidOperationException("Mask collection event is not unique.");
                    result.AddMethod = method; result.AddMeta = message;
                }
                else if (name == RemoveEvent)
                {
                    if (result.RemoveMethod != 0) throw new InvalidOperationException("Mask loss event is not unique.");
                    result.RemoveMethod = method; result.RemoveMeta = message;
                }
                else if (name == RefreshEvent)
                {
                    if (result.RefreshMethod != 0) throw new InvalidOperationException("Mask refresh event is not unique.");
                    result.RefreshMethod = method; result.RefreshMeta = message;
                }
                else if (name == EndEvent)
                {
                    if (result.EndMethod != 0) throw new InvalidOperationException("Mask invincibility end event is not unique.");
                    result.EndMethod = method; result.EndMeta = message;
                }
                else if (name == SpawnEvent)
                {
                    if (result.SpawnMethod != 0) throw new InvalidOperationException("Mask spawn event is not unique.");
                    result.SpawnMethod = method; result.SpawnMeta = message;
                }
                else if (name == ModelEvent)
                {
                    if (result.ModelMethod != 0) throw new InvalidOperationException("Mask model event is not unique.");
                    result.ModelMethod = method; result.ModelMeta = message;
                }
                else if (name == TintOnEvent)
                {
                    if (result.TintOnMethod != 0) throw new InvalidOperationException("Mask gold-tint event is not unique.");
                    result.TintOnMethod = method; result.TintOnMeta = message;
                }
                else
                {
                    if (result.TintOffMethod != 0) throw new InvalidOperationException("Mask tint-removal event is not unique.");
                    result.TintOffMethod = method; result.TintOffMeta = message;
                }
            }
            if (result.AddMethod == 0 || result.RemoveMethod == 0 || result.RefreshMethod == 0 || result.EndMethod == 0 || result.SpawnMethod == 0 || result.ModelMethod == 0) throw new InvalidOperationException("Mask events are unavailable.");
            if (result.TintOnMethod == 0 || result.TintOffMethod == 0) throw new InvalidOperationException("Mask tint events are unavailable.");
            int fieldCount = checked((int)Number(ownerMeta + 0x18, 4));
            if (fieldCount < 1 || fieldCount > 1024) throw new InvalidOperationException("Invalid mask manager fields.");
            long fields = Number(ownerMeta + 0x28);
            bool timerFieldFound = false;
            for (int i = 0; i < fieldCount; i++)
            {
                long field = Number(fields + i * 8);
                string name = Text(Number(field + 0x30));
                if (name == "currentState" || name == "Int_id_tod2nk3r")
                {
                    RequireType(field, "igIntMetaField");
                    long stateOffset = Capture(guards, field + 0x18, 4);
                    if (stateOffset < 0x40 || stateOffset > 0x2000) throw new InvalidOperationException("Invalid mask runtime state field.");
                    if (name == "currentState") result.RuntimeStateAddress = result.Owner + stateOffset;
                    else result.MinimumAddress = result.Owner + stateOffset;
                    continue;
                }
                if (name != "_Internal_TimerAction_id_wwl6fvoo_timer") continue;
                RequireType(field, "igObjectRefMetaField");
                long offset = Capture(guards, field + 0x18, 4);
                if (offset < 0x40 || offset > 0x2000) throw new InvalidOperationException("Invalid mask timer field.");
                timerFieldFound = true;
                // The engine creates this timer lazily on the first third-mask pickup in each scene.
                result.InvincibilityTimer = Capture(guards, result.Owner + offset);
                if (result.InvincibilityTimer != 0)
                {
                    RequireType(result.InvincibilityTimer, "igVscTimer");
                    if (Capture(guards, result.InvincibilityTimer + 0x30) != 0) throw new InvalidOperationException("Unexpected mask timer abort callback.");
                }
            }
            if (!timerFieldFound) throw new InvalidOperationException("Mask invincibility timer field is unavailable.");
            if (result.RuntimeStateAddress == 0) throw new InvalidOperationException("Mask runtime state field is unavailable.");
            if (result.MinimumAddress == 0) throw new InvalidOperationException("Mask assist minimum field is unavailable.");
            return result;
        }

        internal MaskSnapshot Snapshot()
        {
            try { return ReadSnapshot(); }
            catch (MaskNotReadyException) { binding = null; return MaskSnapshot.Unavailable; }
            catch { binding = null; countAddress = 0; throw; }
        }

        private MaskSnapshot ReadSnapshot()
        {
            if (countAddress == 0 || !Matches(variableGuards)) { countAddress = 0; LocateVariables(); }
            int count = checked((int)Number(countAddress, 4));
            bool invincible = Number(invincibleAddress, 1) != 0, dead = Number(deadAddress, 1) != 0;
            bool damagedInvulnerable = Number(damagedInvulnerableAddress, 1) != 0;
            long loading = Number(Base + 0x1A8FEB8), pause = Number(Base + 0x1A5B010);
            bool paused = pause == 0 || Number(pause + 0x100, 1) != 0;
            if (loading == 0 || Number(loading + 0xA0) == 0 || Number(Number(loading + 0xA0) + 0x22C, 1) != 0)
            { binding = null; return new MaskSnapshot { Count = count, Invincible = invincible, DamagedInvulnerable = damagedInvulnerable, Dead = dead, Paused = paused }; }
            // Pause blocks execution, not discovery/readback. Ignore only the pause-byte guard here.
            if (binding == null || !Matches(binding.Guards.Where(g => g.Address != pause + 0x100))) binding = Discover();
            return new MaskSnapshot { Count = count, Invincible = invincible, DamagedInvulnerable = damagedInvulnerable, Dead = dead, Paused = paused,
                RuntimeState = unchecked((int)Number(binding.RuntimeStateAddress, 4)),
                Minimum = checked((int)Number(binding.MinimumAddress, 4)),
                Ready = count >= 0 && count <= 3 && !dead && !paused && (!invincible || binding.InvincibilityTimer != 0), Binding = binding,
                CountAddress = countAddress, InvincibleAddress = invincibleAddress, DamagedInvulnerableAddress = damagedInvulnerableAddress };
        }

        internal IEnumerable<MaskGuard> CommandGuards(MaskSnapshot state) => state.Binding.Guards.Concat(variableGuards)
            .Concat(new[] { new MaskGuard(state.CountAddress, state.Count, 4), new MaskGuard(deadAddress, 0, 1),
                new MaskGuard(state.DamagedInvulnerableAddress, state.DamagedInvulnerable ? 1 : 0, 1) });
        public void Dispose() { LevelLockNative.CloseHandle(Handle); Process.Dispose(); }
    }
}
