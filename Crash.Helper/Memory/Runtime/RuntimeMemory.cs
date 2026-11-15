using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Crash.Helper.Memory.LevelLock;

namespace Crash.Helper.Memory.Runtime
{
    internal sealed class RuntimeGuard
    {
        internal long Address, Value;
        internal int Size;
        internal uint Mask;
        internal RuntimeGuard(long address, long value, int size = 8, uint mask = uint.MaxValue)
        { Address = address; Value = value; Size = size; Mask = mask; }
    }

    internal sealed class PlayerBinding
    {
        internal long Player, Actor, Driver, ActorId, DriverId;
        internal long SceneFirst, SceneSecond, Parent, DriverParent;
        internal bool PhysicsActive, Writable;
        internal readonly List<RuntimeGuard> Guards = new List<RuntimeGuard>();
        internal bool SameActor(PlayerBinding other) => other != null && Actor == other.Actor && ActorId == other.ActorId && Parent == other.Parent && SceneFirst == other.SceneFirst && SceneSecond == other.SceneSecond;
        internal bool Same(PlayerBinding other) => other != null && Actor == other.Actor && Driver == other.Driver && ActorId == other.ActorId && DriverId == other.DriverId && Parent == other.Parent && DriverParent == other.DriverParent && SceneFirst == other.SceneFirst && SceneSecond == other.SceneSecond && PhysicsActive == other.PhysicsActive && Writable == other.Writable;
    }

    // Resolve engine-owned objects afresh. Never retain a physics capsule or a copied camera model.
    internal sealed class RuntimeMemory : IDisposable
    {
        internal readonly Process Process;
        internal readonly IntPtr Handle;
        internal readonly long Base;
        private readonly Dictionary<long, Dictionary<string, int>> fieldCache = new Dictionary<long, Dictionary<string, int>>();
        private PlayerBinding cachedPlayer;
        internal string PlayerUnavailableReason { get; private set; }

        internal RuntimeMemory(Process target)
        {
            Process = System.Diagnostics.Process.GetProcessById(target.Id);
            if (Process.MainModule.ModuleMemorySize != GameMemoryProfile.Steam.ModuleSize)
            { Process.Dispose(); throw new NotSupportedException("Engine transforms require the inspected Steam build."); }
            Base = Process.MainModule.BaseAddress.ToInt64();
            Handle = LevelLockNative.OpenProcess(0xC38, false, Process.Id);
            if (Handle == IntPtr.Zero) { Process.Dispose(); LevelLockNative.Check(false, "Open engine transform memory"); }
        }
        internal byte[] Read(long address, int size)
        {
            var bytes = new byte[size]; UIntPtr count;
            LevelLockNative.Check(LevelLockNative.ReadProcessMemory(Handle, new IntPtr(address), bytes, (UIntPtr)size, out count) && count.ToUInt64() == (ulong)size, "Read engine transform");
            return bytes;
        }
        internal long Number(long address, int size = 8)
        {
            var bytes = Read(address, size);
            return size == 8 ? BitConverter.ToInt64(bytes, 0) : size == 4 ? BitConverter.ToUInt32(bytes, 0) : bytes[0];
        }
        internal void Write(long address, byte[] bytes)
        {
            UIntPtr count;
            LevelLockNative.Check(LevelLockNative.WriteProcessMemory(Handle, new IntPtr(address), bytes, (UIntPtr)bytes.Length, out count) && count.ToUInt64() == (ulong)bytes.Length, "Write engine transform command");
        }
        internal void Require(long address, byte[] bytes)
        { if (!Read(address, bytes.Length).SequenceEqual(bytes)) throw new InvalidOperationException("Engine transform instructions do not match this game build."); }
        internal void WhilePaused(Action action) => LevelLockNative.WhilePaused(Handle, Process, action);
        internal void WriteCode(long address, byte[] bytes)
        {
            uint previous;
            LevelLockNative.Check(LevelLockNative.VirtualProtectEx(Handle, new IntPtr(address), (UIntPtr)bytes.Length, 0x40, out previous), "Protect transform instructions");
            try { Write(address, bytes); LevelLockNative.Check(LevelLockNative.FlushInstructionCache(Handle, new IntPtr(address), (UIntPtr)bytes.Length), "Flush transform instructions"); }
            finally { uint ignored; LevelLockNative.Check(LevelLockNative.VirtualProtectEx(Handle, new IntPtr(address), (UIntPtr)bytes.Length, previous, out ignored), "Restore transform protection"); }
        }
        internal long Allocate(long anchor, int size = 8192)
        {
            anchor &= ~65535L;
            for (long distance = 65536; distance < 0x10000000; distance += 65536)
                foreach (long hint in new[] { anchor + distance, anchor - distance })
                {
                    long result = LevelLockNative.VirtualAllocEx(Handle, new IntPtr(hint), (UIntPtr)size, 0x3000, 4).ToInt64();
                    if (result != 0) return result;
                }
            throw new InvalidOperationException("Could not allocate the engine transform dispatcher.");
        }
        internal void Protect(long address)
        {
            uint previous;
            LevelLockNative.Check(LevelLockNative.VirtualProtectEx(Handle, new IntPtr(address), (UIntPtr)4096, 0x20, out previous), "Protect transform dispatcher");
            LevelLockNative.Check(LevelLockNative.FlushInstructionCache(Handle, new IntPtr(address), (UIntPtr)4096), "Flush transform dispatcher");
        }
        internal bool Matches(IEnumerable<RuntimeGuard> guards)
        {
            try
            {
                var entries = guards as IList<RuntimeGuard> ?? guards.ToArray();
                for (int i = 0; i < entries.Count; i++)
                {
                    var guard = entries[i]; int count = 1;
                    if (guard.Size == 8)
                    {
                        while (i + count < entries.Count && entries[i + count].Size == 8 && entries[i + count].Address == guard.Address + count * 8L) count++;
                        var bytes = Read(guard.Address, count * 8);
                        for (int j = 0; j < count; j++) if (BitConverter.ToInt64(bytes, j * 8) != entries[i + j].Value) return false;
                        i += count - 1;
                    }
                    else if ((Number(guard.Address, guard.Size) & guard.Mask) != guard.Value) return false;
                }
                return true;
            }
            catch (Win32Exception) { return false; }
        }
        internal long Capture(List<RuntimeGuard> guards, long address, int size = 8, uint mask = uint.MaxValue)
        {
            long value = Number(address, size);
            if (size != 8) value &= mask;
            guards.Add(new RuntimeGuard(address, value, size, mask)); return value;
        }
        internal long Pointer(List<RuntimeGuard> guards, long address)
        { long value = Capture(guards, address); if (value < 0x10000) throw new InvalidOperationException("The engine object is unavailable."); return value; }
        private string Text(long address) => Encoding.UTF8.GetString(Read(address, 180)).Split('\0')[0];
        private long Meta(long address)
        {
            long getter = Number(Number(address)); var code = Read(getter, 16);
            if (code[0] == 0x48 && code[1] == 0x8B && code[2] == 0x05) return Number(getter + 7 + BitConverter.ToInt32(code, 3));
            if (code[0] == 0x48 && code[1] == 0x8B && code[2] == 0x41 && code[4] == 0xC3) return Number(address + code[3]);
            throw new InvalidOperationException("The engine object type is unavailable.");
        }
        internal string TypeName(long address) => Text(Number(Meta(address) + 0x10));
        private void Type(long address, string expected)
        { if (TypeName(address) != expected) throw new InvalidOperationException("The engine object has been replaced."); }
        private Dictionary<string, int> Fields(long address)
        {
            long meta = Meta(address); Dictionary<string, int> result;
            if (fieldCache.TryGetValue(meta, out result)) return result;
            int count = checked((int)Number(meta + 0x18, 4));
            if (count < 0 || count > 1024) throw new InvalidOperationException("Invalid engine field list.");
            long array = Number(meta + 0x28); result = new Dictionary<string, int>();
            for (int i = 0; i < count; i++)
            { long field = Number(array + i * 8); result[Text(Number(field + 0x30))] = checked((int)Number(field + 0x18, 4)); }
            fieldCache[meta] = result; return result;
        }
        internal long[] List(long list, List<RuntimeGuard> guards, int limit, bool captureEntries = true)
        {
            int count = checked((int)Capture(guards, list + 0x10, 4));
            long capacity = Number(list + 0x14, 4), bytes = Number(list + 0x18, 4);
            if (count < 0 || count > limit || capacity < count || bytes < count * 8L) throw new InvalidOperationException("The engine list is unavailable.");
            if (count == 0) return new long[0];
            long array = Pointer(guards, list + 0x20);
            if (!captureEntries)
            {
                var entries = Read(array, count * 8);
                return Enumerable.Range(0, count).Select(i => BitConverter.ToInt64(entries, i * 8)).ToArray();
            }
            return Enumerable.Range(0, count).Select(i => Capture(guards, array + i * 8)).ToArray();
        }
        internal long LocalPlayer(List<RuntimeGuard> guards)
        {
            long manager = Pointer(guards, Base + 0x1A665E8); Type(manager, "CPlayerManager");
            long list = Pointer(guards, manager + 0x10); Type(list, "CPlayerList");
            long selected = 0;
            foreach (long player in List(list, guards, 16))
            {
                if (player == 0) continue; Type(player, "CPlayer");
                if (Number(player + 0x94, 4) != 0) continue;
                if (selected != 0) throw new InvalidOperationException("The local player is ambiguous.");
                Capture(guards, player + 0x94, 4); selected = player;
            }
            if (selected == 0) throw new InvalidOperationException("The local player is unavailable.");
            return selected;
        }
        private long Actor(long actor, List<RuntimeGuard> guards)
        {
            Type(actor, "CActor"); Capture(guards, actor); long id = Capture(guards, actor + 0x88, 4);
            if (Capture(guards, actor + 0x38, 4, 0x105) != 0x105) throw new InvalidOperationException("The player actor is not spawned.");
            if (Number(Number(actor) + 0x200) != Base + 0x508EA0) throw new InvalidOperationException("The actor teleport method does not match.");
            return id;
        }
        internal PlayerBinding Player()
        {
            PlayerUnavailableReason = null;
            try
            {
                if (cachedPlayer != null && Matches(cachedPlayer.Guards)) return cachedPlayer;
                cachedPlayer = null; fieldCache.Clear();
                var binding = new PlayerBinding(); var guards = binding.Guards;
                binding.SceneFirst = Capture(guards, Base + 0x1A5C6E0); binding.SceneSecond = Capture(guards, Base + 0x1A5C6E8);
                binding.Player = LocalPlayer(guards);
                long handle = Pointer(guards, binding.Player + 0x10);
                binding.Actor = Pointer(guards, handle + 0x28); binding.ActorId = Actor(binding.Actor, guards);
                binding.Parent = Capture(guards, binding.Actor + 0x10);
                binding.Driver = binding.Actor;
                long components = Pointer(guards, binding.Actor + 0x18);
                var componentList = List(components, guards, 128);
                long physics = componentList.FirstOrDefault(component => component != 0 && TypeName(component) == "CBehaviorPhysicsGenericShapeComponent");
                binding.PhysicsActive = physics == 0 || Capture(guards, physics + 0x20, 4, 0x59) == 0x41;
                long vehicle = Capture(guards, binding.Player + 0x18);
                if (vehicle != 0)
                {
                    long driver = Capture(guards, vehicle + 0x28);
                    if (driver != 0 && Capture(guards, driver + 0x38, 4, 0x105) == 0x105) binding.Driver = driver;
                }
                if (binding.Driver == binding.Actor && !binding.PhysicsActive)
                {
                    foreach (long component in componentList)
                    {
                        if (component == 0 || TypeName(component) != "CVehicleRiderComponent") continue;
                        if (Capture(guards, component + 0x20, 4, 0x59) != 0x41 || Capture(guards, component + 0x64, 1) == 0) continue;
                        long riderHandle = Capture(guards, component + 0x30);
                        if (riderHandle == 0) continue;
                        long driver = Capture(guards, riderHandle + 0x28);
                        if (driver != 0 && Capture(guards, driver + 0x38, 4, 0x105) == 0x105) binding.Driver = driver;
                    }
                }
                if (binding.Driver == binding.Actor && !binding.PhysicsActive)
                {
                    foreach (long component in componentList)
                    {
                        if (component == 0) continue;
                        string name = TypeName(component);
                        if (!name.StartsWith("Scripts.Graph.", StringComparison.Ordinal) || name.IndexOf("_Ride_", StringComparison.Ordinal) < 0) continue;
                        var fields = Fields(component);
                        var ride = fields.FirstOrDefault(f => f.Key.StartsWith("RideEntity", StringComparison.Ordinal) || (f.Key.StartsWith("Ride", StringComparison.Ordinal) && f.Key.EndsWith("Entity", StringComparison.Ordinal)));
                        if (ride.Key == null) continue;
                        bool updating = false;
                        foreach (var updater in fields.Where(f => f.Key.IndexOf("updateNodeUpdater", StringComparison.Ordinal) >= 0))
                        {
                            long update = Capture(guards, component + updater.Value);
                            if (update == 0) continue;
                            Type(update, "CUpdateNodeUpdater");
                            if (Capture(guards, update + 0x20) != 0) updating = true;
                        }
                        if (!updating) continue;
                        long rideHandle = Capture(guards, component + ride.Value);
                        if (rideHandle == 0) continue;
                        long driver = Capture(guards, rideHandle + 0x28);
                        // A ride script can keep its updater and an empty weak handle after dismount.
                        if (driver == 0) continue;
                        if (Capture(guards, driver + 0x38, 4, 0x105) != 0x105) continue;
                        if (binding.Driver != binding.Actor && binding.Driver != driver) throw new InvalidOperationException("The controlled actor is ambiguous.");
                        binding.Driver = driver;
                    }
                }
                binding.DriverId = binding.Driver == binding.Actor ? binding.ActorId : Actor(binding.Driver, guards);
                binding.DriverParent = binding.Driver == binding.Actor ? binding.Parent : Capture(guards, binding.Driver + 0x10);
                binding.Writable = binding.PhysicsActive || binding.Driver != binding.Actor;
                long stateRoot = Pointer(guards, Base + 0x1A69A98), state = Pointer(guards, stateRoot + 0x30);
                if (Capture(guards, state + 0x15C0, 1) != 0) { PlayerUnavailableReason = "The player is dead."; return null; }
                if (!Matches(guards)) { PlayerUnavailableReason = "The player changed during discovery."; return null; }
                cachedPlayer = binding; return binding;
            }
            catch (Win32Exception ex) { PlayerUnavailableReason = ex.Message; return null; }
            catch (InvalidOperationException ex) { PlayerUnavailableReason = ex.Message; return null; }
            catch (OverflowException ex) { PlayerUnavailableReason = ex.Message; return null; }
        }
        internal float[] UnattachedPosition(PlayerBinding binding)
        {
            try
            {
                if (binding == null || !Matches(binding.Guards) || Number(binding.Actor + 0x10) != 0) return null;
                var data = Read(binding.Actor + 0x20, 12);
                var result = Enumerable.Range(0, 3).Select(i => BitConverter.ToSingle(data, i * 4)).ToArray();
                return result.All(Finite) && Number(binding.Actor + 0x10) == 0 && Matches(binding.Guards) ? result : null;
            }
            catch (Win32Exception) { return null; }
        }
        internal PlayerBinding PositionSubject()
        {
            try
            {
                var subject = new PlayerBinding(); var guards = subject.Guards;
                subject.SceneFirst = Capture(guards, Base + 0x1A5C6E0); subject.SceneSecond = Capture(guards, Base + 0x1A5C6E8);
                subject.Actor = CameraSubject(guards); if (subject.Actor == 0) return null;
                subject.ActorId = Number(subject.Actor + 0x88, 4); subject.Parent = Capture(guards, subject.Actor + 0x10);
                subject.Driver = subject.Actor; subject.DriverId = subject.ActorId;
                return Matches(guards) ? subject : null;
            }
            catch (Win32Exception) { return null; }
            catch (InvalidOperationException) { return null; }
        }
        internal long Camera(List<RuntimeGuard> guards)
        {
            Capture(guards, Base + 0x1A5C6E0); Capture(guards, Base + 0x1A5C6E8);
            long player = LocalPlayer(guards), system = Pointer(guards, Base + 0x1A890E0); Type(system, "CCameraSystem");
            long cameras = Pointer(guards, system + 0x20), selected = 0, unowned = 0;
            int unownedCount = 0;
            foreach (long instance in List(cameras, guards, 16))
            {
                if (instance == 0) continue; Type(instance, "CCameraSystemInstance");
                long owner = Capture(guards, instance + 0x70);
                if (owner != 0 && Capture(guards, owner + 0x28) != player) continue;
                if (Number(instance + 0x178, 1) == 0 || Number(instance + 0x179, 1) == 0 || Number(instance + 0x2D0, 1) == 0) continue;
                Capture(guards, instance + 0x178, 1); Capture(guards, instance + 0x179, 1); Capture(guards, instance + 0x2D0, 1);
                if (owner == 0) { unowned = instance; unownedCount++; }
                else { if (selected != 0) return 0; selected = instance; }
            }
            // Single-player levels can create their only gameplay camera without an owner handle.
            // Never guess between several unowned cameras or select another player's camera.
            if (selected == 0 && unownedCount == 1) selected = unowned;
            return selected != 0 && Matches(guards) ? selected + 0x80 : 0;
        }
        internal long CameraSubject(List<RuntimeGuard> guards)
        {
            try
            {
                long player = LocalPlayer(guards), handle = Pointer(guards, player + 0x10), actor = Pointer(guards, handle + 0x28);
                Type(actor, "CActor"); Capture(guards, actor); Capture(guards, actor + 0x88, 4);
                if (Capture(guards, actor + 0x38, 4, 0x105) != 0x105) return 0;
                long root = Pointer(guards, Base + 0x1A69A98), state = Pointer(guards, root + 0x30);
                return Capture(guards, state + 0x15C0, 1) == 0 ? actor : 0;
            }
            catch (Win32Exception) { return 0; }
            catch (InvalidOperationException) { return 0; }
        }
        internal float[] Vector(long address)
        {
            var bytes = Read(address, 12);
            return Enumerable.Range(0, 3).Select(i => BitConverter.ToSingle(bytes, i * 4)).ToArray();
        }
        internal long NearestCamera(long instance, long actor, List<RuntimeGuard> guards, out long selectedCamera)
        {
            selectedCamera = 0;
            long database = Pointer(guards, instance + 0x2B0); Type(database, "CCameraBoxDatabase");
            long list = Pointer(guards, database + 0x10); Type(list, "CCameraBoxList");
            var boxes = List(list, guards, 2048, false); long array = Number(list + 0x20);
            long defaultBox = Capture(guards, instance + 0x150);
            float[] point = Number(actor + 0x10) == 0 ? Vector(actor + 0x20) : null;
            if (point == null)
            {
                long targeting = Pointer(guards, instance + 0x48), primary = Pointer(guards, targeting + 0x50);
                point = Vector(primary + 0x18);
            }
            if (!point.All(Finite)) return 0;
            long nearest = 0; int index = -1, bestRank = int.MaxValue;
            double best = double.PositiveInfinity, entranceBest = double.PositiveInfinity;
            List<RuntimeGuard> selectedGuards = null, currentGuards = null;
            long current = CurrentCamera(instance), currentBox = 0;
            int currentIndex = -1, currentRank = int.MaxValue; double currentScore = double.PositiveInfinity;
            var paths = new Crash.Helper.Memory.Camera.CameraPathReader(this);
            for (int i = 0; i < boxes.Length; i++)
            {
                long box = boxes[i]; if (box == 0 || box == defaultBox || Number(box + 0x191, 1) == 0 || Number(box + 0x158) == 0) continue;
                var position = Vector(box + 0x10); var rotation = Vector(box + 0x1C); var min = Vector(box + 0x120); var max = Vector(box + 0x12C);
                if (!position.Concat(rotation).Concat(min).Concat(max).All(Finite) || Enumerable.Range(0, 3).Any(axis => min[axis] > max[axis])) continue;
                double distance = Crash.Helper.Memory.Camera.CameraSelection.DistanceSquared(point, position, rotation, min, max);
                Tuple<long, List<RuntimeGuard>>[] choices;
                try { choices = CameraChoices(box, new List<RuntimeGuard>(), new HashSet<long>(), 0).ToArray(); }
                catch (Win32Exception) { continue; }
                catch (InvalidOperationException) { continue; }
                foreach (var candidate in choices)
                {
                    var candidateGuards = candidate.Item2; float[][] path = null;
                    try { path = paths.Read(candidate.Item1, candidateGuards); }
                    catch (Win32Exception) { continue; }
                    catch (InvalidOperationException) { continue; }
                    int rank = distance < 0.0001 ? 0 : path != null ? 1 : 2;
                    double score = path != null ? Crash.Helper.Memory.Camera.CameraSelection.PathDistanceSquared(point, path) : distance;
                    if (path != null && candidate.Item1 == current && (rank < currentRank || (rank == currentRank && score < currentScore)))
                    {
                        currentBox = box; currentIndex = i; currentRank = rank; currentScore = score; currentGuards = candidateGuards;
                    }
                    if (rank < bestRank || (rank == bestRank && (score < best - 0.01 || (Math.Abs(score - best) < 0.01 && distance < entranceBest))))
                    {
                        bestRank = rank; best = score; entranceBest = distance; nearest = box; index = i;
                        selectedCamera = candidate.Item1; selectedGuards = candidateGuards;
                    }
                }
            }
            // Adjacent rails can overlap, and our control-point segments approximate curved paths.
            // Keep the engine's active view for a near tie instead of changing its authored offset.
            if (currentBox != 0 && currentRank == bestRank && Crash.Helper.Memory.Camera.CameraSelection.WithinPathTie(currentScore, best))
            { nearest = currentBox; index = currentIndex; selectedCamera = current; selectedGuards = currentGuards; }
            if (nearest == 0) return 0;
            guards.AddRange(selectedGuards);
            Type(nearest, "CCameraBox"); Capture(guards, array + index * 8L); Capture(guards, nearest); Capture(guards, nearest + 0x158);
            if (Capture(guards, nearest + 0x191, 1) == 0) return 0;
            long active = Pointer(guards, instance + 0x58); Type(active, "CCameraBoxList");
            return Matches(guards) ? nearest : 0;
        }
        private long CurrentCamera(long instance)
        {
            try
            {
                var guards = new List<RuntimeGuard>();
                long list = Pointer(guards, instance + 0x58); var boxes = List(list, guards, 128, false);
                if (boxes.Length == 0) return 0;
                long camera = boxes[0]; var visited = new HashSet<long>();
                while (camera != 0 && visited.Add(camera) && visited.Count <= 8)
                {
                    Capture(guards, camera);
                    if (TypeName(camera) != "CCameraBox") return Matches(guards) ? camera : 0;
                    long handle = Capture(guards, camera + 0x160), active = handle == 0 ? 0 : Capture(guards, handle + 0x28);
                    camera = active != 0 ? active : Capture(guards, camera + 0x158);
                }
            }
            catch (Win32Exception) { }
            catch (InvalidOperationException) { }
            return 0;
        }
        private IEnumerable<Tuple<long, List<RuntimeGuard>>> CameraChoices(long box, List<RuntimeGuard> prefix, HashSet<long> visited, int depth)
        {
            if (depth > 8 || !visited.Add(box)) yield break;
            var references = new List<Tuple<long, List<RuntimeGuard>>>();
            var primary = new List<RuntimeGuard>(prefix); long camera = Capture(primary, box + 0x158);
            if (camera != 0) references.Add(Tuple.Create(camera, primary));
            var active = new List<RuntimeGuard>(prefix); long handle = Capture(active, box + 0x160);
            if (handle != 0) { camera = Capture(active, handle + 0x28); if (camera != 0) references.Add(Tuple.Create(camera, active)); }
            long blends = Number(box + 0x170);
            if (blends != 0)
            {
                int count = checked((int)Number(blends + 0x10, 4));
                if (count < 0 || count > 128) throw new InvalidOperationException("The camera blend list is unavailable.");
                long array = Number(blends + 0x20);
                for (int i = 0; i < count; i++)
                {
                    var blend = new List<RuntimeGuard>(prefix); Capture(blend, box + 0x170); Capture(blend, blends + 0x10, 4); Capture(blend, blends + 0x20);
                    camera = Capture(blend, array + i * 32L); if (camera != 0) references.Add(Tuple.Create(camera, blend));
                }
            }
            foreach (var reference in references)
            {
                Capture(reference.Item2, reference.Item1);
                if (TypeName(reference.Item1) == "CCameraBox")
                {
                    foreach (var nested in CameraChoices(reference.Item1, reference.Item2, visited, depth + 1)) yield return nested;
                }
                else if (visited.Add(reference.Item1)) yield return reference;
            }
        }
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public void Dispose() { LevelLockNative.CloseHandle(Handle); Process.Dispose(); }
    }
}
