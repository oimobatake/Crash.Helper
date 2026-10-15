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
            internal State(float scale) { Scale = scale; }
        }

        private State state = new State(1);

        internal void Reset(float scale) => state = new State(scale);

        // Converts the manual movement delta into the delta that reaches the follow target.
        // Manual XYZ movement shifts the stored offset instead of fighting the follow.
        internal float?[] Apply(float[] camera, float?[] delta, float[] player)
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
                current.Offset = offset = new[] { camera[0] - player[0] * scale, camera[1] - player[1] * scale, camera[2] - player[2] * scale };
            var result = (float?[])delta.Clone();
            for (int i = 0; i < 3; i++)
            {
                if (delta[i].HasValue) offset[i] += delta[i].Value;
                result[i] = player[i] * scale + offset[i] - camera[i];
            }
            return result;
        }
    }
}
