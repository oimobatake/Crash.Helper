using System;

namespace Crash.Helper.Memory.Camera
{
    // Keeps the camera at a fixed offset from the player. The offset is captured from the
    // current camera and player positions when following starts, so the two coordinate
    // systems only need parallel axes, not the same origin. Scale converts player units
    // into camera units.
    internal sealed class CameraFollow
    {
        private sealed class State
        {
            internal readonly float Scale;
            internal float[] Offset;
            internal float TrackedZ;
            internal State(float scale) { Scale = scale; }
        }

        private State state = new State(1);
        private volatile float deadzone, speed;

        internal void Reset(float scale) => state = new State(scale);

        // Deadzone is in player units: vertical movement inside it does not move the camera.
        // Speed is the per-second approach rate toward the target; zero moves instantly.
        internal void SetSmoothing(float verticalDeadzone, float followSpeed)
        {
            deadzone = Math.Max(0, verticalDeadzone);
            speed = Math.Max(0, followSpeed);
        }

        // Converts the manual movement delta into the delta that moves toward the follow target.
        // Manual XYZ movement shifts the stored offset instead of fighting the follow.
        internal float?[] Apply(float[] camera, float?[] delta, float[] player, double seconds)
        {
            // Reset can run on another thread, so work on one captured state.
            var current = state;
            var offset = current.Offset;
            if (player == null)
            {
                // Keep manual movement while the player is unavailable, such as during a fade.
                if (offset != null)
                    for (int i = 0; i < 3; i++) if (delta[i].HasValue) offset[i] += delta[i].Value;
                return delta;
            }
            float scale = current.Scale;
            if (offset == null)
            {
                current.Offset = offset = new[] { camera[0] - player[0] * scale, camera[1] - player[1] * scale, camera[2] - player[2] * scale };
                current.TrackedZ = player[2];
            }
            // Drag the tracked height only by the amount the player leaves the deadzone.
            float zone = deadzone, rise = player[2] - current.TrackedZ;
            if (rise > zone) current.TrackedZ = player[2] - zone;
            else if (rise < -zone) current.TrackedZ = player[2] + zone;
            var target = new[] { player[0], player[1], current.TrackedZ };
            float rate = speed;
            // Exponential approach keeps the same feel regardless of the loop interval.
            double blend = rate <= 0 ? 1 : 1 - Math.Exp(-rate * Math.Max(0, Math.Min(seconds, 0.1)));
            var result = (float?[])delta.Clone();
            for (int i = 0; i < 3; i++)
            {
                if (delta[i].HasValue) offset[i] += delta[i].Value;
                result[i] = (float)((target[i] * scale + offset[i] - camera[i]) * blend);
            }
            return result;
        }
    }
}
