using System;

namespace Crash.Helper.Memory.Camera
{
    internal static class CameraMovement
    {
        // Preserve the previous XYZ speed scale (units per 30 ms) while using elapsed time.
        // Normalize XYZ together so two- and three-axis movement has the same total speed.
        internal static float?[] Delta(int[] directions, float xyzSpeed, float rotationSpeed, double seconds, float yaw,
            float pitch = 0, bool followPitch = false, double mouseYaw = 0, double mousePitch = 0)
        {
            var result = new float?[5];
            double scale = Math.Max(0, Math.Min(seconds, 0.05)) / 0.03;
            double sin = Math.Sin(yaw), cos = Math.Cos(yaw);
            double forward = directions[1] * (followPitch ? Math.Cos(pitch) : 1);
            double x = directions[0] * sin + forward * cos;
            double y = -directions[0] * cos + forward * sin;
            // Positive pitch looks down. Up/Down remain aligned with world Z.
            double z = directions[2] - (followPitch ? directions[1] * Math.Sin(pitch) : 0);
            double length = Math.Sqrt(x * x + y * y + z * z);
            if (length > 0.000001)
            {
                double distance = xyzSpeed * scale / length;
                // The game's view points along +X at zero yaw: forward=(cos,sin), right=(sin,-cos).
                if (directions[0] != 0 || directions[1] != 0)
                {
                    result[0] = (float)(x * distance);
                    result[1] = (float)(y * distance);
                }
                if (directions[2] != 0 || (followPitch && directions[1] != 0)) result[2] = (float)(z * distance);
            }
            for (int i = 3; i < 5; i++)
            {
                if (directions[i] == 0) continue;
                result[i] = (float)(directions[i] * scale * rotationSpeed);
            }
            // Mouse deltas are per event, not a speed, and must not be scaled by frame time.
            if (mouseYaw != 0) result[3] = (result[3] ?? 0) + (float)mouseYaw;
            if (mousePitch != 0) result[4] = (result[4] ?? 0) + (float)mousePitch;
            return result;
        }
    }
}
