using System.Collections.Generic;

namespace Crash.Helper.Memory.Runtime
{
    internal sealed class PlayerBinding
    {
        internal long Player, Actor, Driver, ActorId, DriverId;
        internal long SceneFirst, SceneSecond, Parent, DriverParent, PhysicsBody;
        internal bool PhysicsActive, Writable;
        internal readonly List<RuntimeGuard> Guards = new List<RuntimeGuard>();
        internal bool SameActor(PlayerBinding other) => other != null && Actor == other.Actor && ActorId == other.ActorId && Parent == other.Parent && SceneFirst == other.SceneFirst && SceneSecond == other.SceneSecond;
        internal bool Same(PlayerBinding other) => other != null && Actor == other.Actor && Driver == other.Driver && ActorId == other.ActorId && DriverId == other.DriverId && Parent == other.Parent && DriverParent == other.DriverParent && SceneFirst == other.SceneFirst && SceneSecond == other.SceneSecond && PhysicsActive == other.PhysicsActive && Writable == other.Writable;
    }
}
