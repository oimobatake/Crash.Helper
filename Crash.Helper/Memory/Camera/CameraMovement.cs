using System;

namespace Crash.Helper.Memory.Camera
{
    internal static class CameraMovement
    {
        // Preserve the previous XYZ speed scale (units per 30 ms) while using elapsed time.
        // Normalize XYZ together so two- and three-axis movement has the same total speed.
        internal static float?[] Delta(int[] directions, float xyzSpeed, float rotationSpeed, double seconds, float yaw)
        {
            var result = new float?[5];
            double scale = Math.Max(0, Math.Min(seconds, 0.05)) / 0.03;
            double length = Math.Sqrt(directions[0] * directions[0] + directions[1] * directions[1] + directions[2] * directions[2]);
            if (length > 0)
            {
                double distance = xyzSpeed * scale / length;
                // The game's view points along +X at zero yaw: forward=(cos,sin), right=(sin,-cos).
                // Pitch does not change altitude; Up/Down control world Z separately.
                double sin = Math.Sin(yaw), cos = Math.Cos(yaw);
                if (directions[0] != 0 || directions[1] != 0)
                {
                    result[0] = (float)((directions[0] * sin + directions[1] * cos) * distance);
                    result[1] = (float)((-directions[0] * cos + directions[1] * sin) * distance);
                }
                if (directions[2] != 0) result[2] = (float)(directions[2] * distance);
            }
            for (int i = 3; i < 5; i++)
            {
                if (directions[i] == 0) continue;
                result[i] = (float)(directions[i] * scale * rotationSpeed);
            }
            return result;
        }
    }
}
