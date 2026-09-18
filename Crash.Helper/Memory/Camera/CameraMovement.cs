using System;

namespace Crash.Helper.Memory.Camera
{
    internal static class CameraMovement
    {
        // Preserve the previous XYZ speed scale (units per 30 ms) while using elapsed time.
        // Normalize XYZ together so two- and three-axis movement has the same total speed.
        internal static float?[] Delta(int[] directions, float xyzSpeed, float rotationSpeed, double seconds)
        {
            var result = new float?[5];
            double scale = Math.Max(0, Math.Min(seconds, 0.05)) / 0.03;
            double length = Math.Sqrt(directions[0] * directions[0] + directions[1] * directions[1] + directions[2] * directions[2]);
            for (int i = 0; i < 5; i++)
            {
                if (directions[i] == 0) continue;
                result[i] = (float)(directions[i] * scale * (i < 3 ? xyzSpeed / length : rotationSpeed));
            }
            return result;
        }
    }
}
