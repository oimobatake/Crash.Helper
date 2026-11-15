using System;

namespace Crash.Helper.Memory.Camera
{
    internal static class CameraSelection
    {
        internal static float[] TransformPoint(float[] point, float[] origin, float[] orientation, float[] scale)
        {
            // igEntityTransform stores the conjugate of the world rotation used for points.
            double x = point[0] * scale[0], y = point[1] * scale[1], z = point[2] * scale[2];
            double qx = -orientation[0], qy = -orientation[1], qz = -orientation[2], w = orientation[3];
            double a = qy * z - qz * y, b = qz * x - qx * z, c = qx * y - qy * x;
            return new[] { (float)(origin[0] + x + 2 * (w * a + qy * c - qz * b)),
                (float)(origin[1] + y + 2 * (w * b + qz * a - qx * c)),
                (float)(origin[2] + z + 2 * (w * c + qx * b - qy * a)) };
        }
        // A five-percent distance tie preserves the game's choice between overlapping rails.
        // Compare squared distances so a clearly nearer bonus or alternate path still wins.
        internal static bool WithinPathTie(double current, double best)
            => !double.IsInfinity(current) && !double.IsNaN(current) && current <= best * 1.1025 + 0.01;
        internal static double PathDistanceSquared(float[] point, float[][] path)
        {
            double best = double.PositiveInfinity;
            for (int i = 1; i < path.Length; i++)
            {
                var a = path[i - 1]; var b = path[i]; double length = 0, projection = 0;
                for (int axis = 0; axis < 3; axis++) { double delta = b[axis] - a[axis]; length += delta * delta; projection += (point[axis] - a[axis]) * delta; }
                double t = length == 0 ? 0 : Math.Max(0, Math.Min(1, projection / length)); double distance = 0;
                for (int axis = 0; axis < 3; axis++) { double delta = point[axis] - a[axis] - t * (b[axis] - a[axis]); distance += delta * delta; }
                best = Math.Min(best, distance);
            }
            return best;
        }
        // Camera volumes store local bounds and Euler angles in degrees.
        internal static double DistanceSquared(float[] point, float[] position, float[] rotation, float[] minimum, float[] maximum)
        {
            double x = point[0] - position[0], y = point[1] - position[1], z = point[2] - position[2];
            double yaw = -rotation[2] * Math.PI / 180, pitch = -rotation[1] * Math.PI / 180, roll = -rotation[0] * Math.PI / 180;
            double a = x * Math.Cos(yaw) - y * Math.Sin(yaw); y = x * Math.Sin(yaw) + y * Math.Cos(yaw); x = a;
            a = x * Math.Cos(pitch) + z * Math.Sin(pitch); z = -x * Math.Sin(pitch) + z * Math.Cos(pitch); x = a;
            a = y * Math.Cos(roll) - z * Math.Sin(roll); z = y * Math.Sin(roll) + z * Math.Cos(roll); y = a;
            var local = new[] { x, y, z }; double distance = 0;
            for (int i = 0; i < 3; i++)
            {
                double delta = local[i] < minimum[i] ? minimum[i] - local[i] : local[i] > maximum[i] ? local[i] - maximum[i] : 0;
                distance += delta * delta;
            }
            return distance;
        }
    }
}
