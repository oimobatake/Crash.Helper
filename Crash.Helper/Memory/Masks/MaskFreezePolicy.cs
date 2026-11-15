namespace Crash.Helper.Memory.Masks
{
    // State only: effects are always produced by the game's collection/loss events.
    internal sealed class MaskFreezePolicy
    {
        private bool armed, charging;
        internal void Reset() { armed = charging = false; }
        internal void ManualChange() { armed = false; }
        internal int DesiredCount(int count, bool invincible, bool ready, bool freeze, int stored, bool formOnHit)
        {
            if (!ready || !freeze) { Reset(); return count; }
            // A natural third pickup keeps its normal invincibility, regardless of the frozen target.
            if (invincible || count == 3) { Reset(); return count; }
            if (!formOnHit) { Reset(); return stored; }
            if (armed && count == 1) charging = true;
            if (charging) return 3;
            if (count == 2) armed = true;
            return 2;
        }
    }
}
