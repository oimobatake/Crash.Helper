using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Crash.Helper.Memory.Runtime
{
    internal sealed partial class RuntimeMemory
    {
        private readonly Dictionary<long, Dictionary<string, int>> fieldCache = new Dictionary<long, Dictionary<string, int>>();
        private PlayerBinding cachedPlayer;
        internal string PlayerUnavailableReason { get; private set; }

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
                if (physics != 0 && binding.PhysicsActive && binding.Driver == binding.Actor && binding.Parent == 0)
                    binding.PhysicsBody = FindPhysicsBody(physics, binding.Actor, guards);
                long stateRoot = Pointer(guards, Base + 0x1A69A98), state = Pointer(guards, stateRoot + 0x30);
                if (Capture(guards, state + 0x15C0, 1) != 0) { PlayerUnavailableReason = "The player is dead."; return null; }
                if (!Matches(guards)) { PlayerUnavailableReason = "The player changed during discovery."; return null; }
                cachedPlayer = binding; return binding;
            }
            catch (Win32Exception ex) { PlayerUnavailableReason = ex.Message; return null; }
            catch (InvalidOperationException ex) { PlayerUnavailableReason = ex.Message; return null; }
            catch (OverflowException ex) { PlayerUnavailableReason = ex.Message; return null; }
        }
        private long FindPhysicsBody(long physics, long actor, List<RuntimeGuard> guards)
        {
            try
            {
                var bodyGuards = new List<RuntimeGuard>();
                if (Capture(bodyGuards, physics + 0x18) != actor) return 0;
                long id = Capture(bodyGuards, physics + 0x50, 4);
                long world = Pointer(bodyGuards, Base + 0x1A5C160);
                if (Capture(bodyGuards, physics + 0x58) != world) return 0;
                long table = Pointer(bodyGuards, world + 0x18);
                long bodies = Pointer(bodyGuards, table + 8);
                long capacity = Capture(bodyGuards, table + 0x10, 4);
                if (id == 0 || id >= capacity || (bodies & 15) != 0) return 0;
                // Havok stores 128-byte bodies, each with its own ID and a position at offset zero.
                long body = bodies + 128 * id;
                if (Capture(bodyGuards, body + 0x28, 4) != id || !Vector(body).All(Finite) || !Matches(bodyGuards)) return 0;
                guards.AddRange(bodyGuards);
                return body;
            }
            catch (Win32Exception) { return 0; }
            catch (InvalidOperationException) { return 0; }
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
    }
}
