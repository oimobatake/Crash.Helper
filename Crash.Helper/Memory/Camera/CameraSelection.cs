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
            => MeasurePath(point, path, out _);

        internal static double MeasurePath(float[] point, float[][] path, out double beyondEnd)
            => ProjectPath(point, path, out beyondEnd, out _, out _);

        private static double ProjectPath(float[] point, float[][] path, out double beyondEnd, out int segment, out double fraction)
        {
            double best = double.PositiveInfinity;
            beyondEnd = 0;
            segment = 0;
            fraction = 0;
            bool closed = path.Length > 2 && path[0][0] == path[path.Length - 1][0] &&
                path[0][1] == path[path.Length - 1][1] && path[0][2] == path[path.Length - 1][2];
            for (int i = 1; i < path.Length; i++)
            {
                var a = path[i - 1]; var b = path[i]; double length = 0, projection = 0;
                for (int axis = 0; axis < 3; axis++) { double delta = b[axis] - a[axis]; length += delta * delta; projection += (point[axis] - a[axis]) * delta; }
                double t = length == 0 ? 0 : Math.Max(0, Math.Min(1, projection / length)); double distance = 0;
                for (int axis = 0; axis < 3; axis++) { double delta = point[axis] - a[axis] - t * (b[axis] - a[axis]); distance += delta * delta; }
                if (distance >= best) continue;
                best = distance;
                segment = i;
                fraction = t;
                beyondEnd = 0;
                if (!closed && length > 0)
                {
                    if (i == 1 && projection < 0) beyondEnd = -projection / Math.Sqrt(length);
                    else if (i == path.Length - 1 && projection > length)
                        beyondEnd = (projection - length) / Math.Sqrt(length);
                }
            }
            return best;
        }
        // On a side-facing rail, the authored view points across the rail. A
        // player on the far side of a parallel rail may actually be behind it.
        internal static bool SideRailView(float[] point, float[][] path, float yawDegrees,
            out double depth, out double distance, out double beyondEnd)
        {
            depth = 0;
            distance = ProjectPath(point, path, out beyondEnd, out int segment, out double t);
            if (segment == 0 || float.IsNaN(yawDegrees) || float.IsInfinity(yawDegrees)) return false;
            double yaw = yawDegrees * Math.PI / 180;
            double forwardX = Math.Cos(yaw), forwardY = Math.Sin(yaw);
            var a = path[segment - 1]; var b = path[segment];
            double dx = b[0] - a[0], dy = b[1] - a[1];
            double horizontalLength = Math.Sqrt(dx * dx + dy * dy);
            if (horizontalLength == 0 || Math.Abs(dx * forwardX + dy * forwardY) > horizontalLength * 0.35) return false;
            depth = (point[0] - a[0] - t * dx) * forwardX + (point[1] - a[1] - t * dy) * forwardY;
            return true;
        }

        // Camera volumes store local bounds and Euler angles in radians.
        internal static double DistanceSquared(float[] point, float[] position, float[] rotation, float[] minimum, float[] maximum, float scale)
        {
            double x = point[0] - position[0], y = point[1] - position[1], z = point[2] - position[2];
            double yaw = -rotation[2], pitch = -rotation[1], roll = -rotation[0];
            double a = x * Math.Cos(yaw) - y * Math.Sin(yaw); y = x * Math.Sin(yaw) + y * Math.Cos(yaw); x = a;
            a = x * Math.Cos(pitch) + z * Math.Sin(pitch); z = -x * Math.Sin(pitch) + z * Math.Cos(pitch); x = a;
            a = y * Math.Cos(roll) - z * Math.Sin(roll); z = y * Math.Sin(roll) + z * Math.Cos(roll); y = a;
            var local = new[] { x / scale, y / scale, z / scale }; double distance = 0;
            for (int i = 0; i < 3; i++)
            {
                double delta = local[i] < minimum[i] ? minimum[i] - local[i] : local[i] > maximum[i] ? local[i] - maximum[i] : 0;
                distance += delta * delta;
            }
            return distance * scale * scale;
        }
    }
}
