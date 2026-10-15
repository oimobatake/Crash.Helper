using System;

namespace Crash.Helper.Memory.Camera
{
    // Keeps the camera at a fixed offset from the player. The offset is captured from the
    // current camera and player positions when following starts, so the two coordinate
    // systems only need parallel axes, not the same origin. Scale converts player units
    // into camera units.
    internal sealed class CameraFollow
    {
        private const double TwoPi = Math.PI * 2;

        private sealed class State
        {
            internal readonly float Scale;
            internal float[] Offset;
            internal float TrackedZ;
            // Orbit angles use the camera's yaw and pitch convention, in radians.
            internal double OrbitYaw, OrbitPitch, Distance = -1;
            internal State(float scale) { Scale = scale; }
        }

        private State state = new State(1);
        private volatile float deadzone, speed;
        private volatile bool orbit;
        private volatile double[] requestedAngles, orbitAngles;
        private volatile float orbitDistance = -1;
        private readonly object stepSync = new object();
        private double stepYaw, stepPitch, stepDistance, requestedDistance = double.NaN;

        internal void Reset(float scale) => state = new State(scale);

        // Deadzone is in player units: vertical movement inside it does not move the camera.
        // Speed is the per-second approach rate toward the target; zero moves instantly.
        internal void SetSmoothing(float verticalDeadzone, float followSpeed)
        {
            deadzone = Math.Max(0, verticalDeadzone);
            speed = Math.Max(0, followSpeed);
        }

        internal bool Orbiting => orbit;

        internal void SetOrbit(bool enabled)
        {
            if (orbit == enabled) return;
            orbit = enabled;
            requestedAngles = null;
            orbitAngles = null;
            orbitDistance = -1;
            lock (stepSync) { stepYaw = stepPitch = stepDistance = 0; requestedDistance = double.NaN; }
            // Recapture the follow offset or orbit distance from the current camera.
            state = new State(state.Scale);
        }

        // Degrees in [0, 360). NaN keeps that angle unchanged.
        internal void RequestOrbitAngles(double yawDegrees, double pitchDegrees) =>
            requestedAngles = new[] { yawDegrees * Math.PI / 180, pitchDegrees * Math.PI / 180 };

        // Adds degrees to the orbit angles on the next update. Repeated presses accumulate.
        internal void StepOrbitAngles(double yawDegrees, double pitchDegrees)
        {
            lock (stepSync)
            {
                stepYaw += yawDegrees * Math.PI / 180;
                stepPitch += pitchDegrees * Math.PI / 180;
            }
        }

        // Distances are in player units, so they stay readable whatever Follow scale is.
        internal void RequestOrbitDistance(double distance)
        {
            lock (stepSync) { requestedDistance = distance; stepDistance = 0; }
        }

        internal void StepOrbitDistance(double distance)
        {
            lock (stepSync) stepDistance += distance;
        }

        // Current distance from the player in player units, or a negative value before the first orbit update.
        internal float OrbitDistance => orbitDistance;

        // Current orbit yaw and pitch in degrees, or null before the first orbit update.
        internal double[] OrbitDegrees
        {
            get
            {
                var angles = orbitAngles;
                return angles == null ? null : new[] { angles[0] * 180 / Math.PI, angles[1] * 180 / Math.PI };
            }
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
            var target = Target(current, player);
            double blend = Blend(seconds);
            var result = (float?[])delta.Clone();
            for (int i = 0; i < 3; i++)
            {
                if (delta[i].HasValue) offset[i] += delta[i].Value;
                result[i] = (float)((target[i] + offset[i] - camera[i]) * blend);
            }
            return result;
        }

        // Circles the camera around the player while looking at it. Movement and rotation input
        // drives the orbit: Left/Right and Yaw change the horizontal angle, Up/Down and Pitch the
        // vertical angle, and Forward/Back the distance.
        internal float?[] Orbit(float[] camera, int[] directions, float xyzSpeed, float rotationSpeed, double seconds,
            double mouseYaw, double mousePitch, float[] player)
        {
            var result = new float?[5];
            if (player == null) return result;
            var current = state;
            if (current.Offset == null)
            {
                // The player and camera share an origin, so the player in camera units is the orbit center.
                current.Offset = new float[3];
                current.TrackedZ = player[2];
            }
            var target = Target(current, player);
            if (current.Distance < 0)
            {
                double dx = target[0] - camera[0], dy = target[1] - camera[1], dz = target[2] - camera[2];
                double flat = Math.Sqrt(dx * dx + dy * dy);
                current.Distance = Math.Max(1, Math.Sqrt(flat * flat + dz * dz));
                current.OrbitYaw = Math.Atan2(dy, dx);
                current.OrbitPitch = Math.Atan2(-dz, flat);
            }
            var requested = requestedAngles;
            if (requested != null)
            {
                requestedAngles = null;
                if (!double.IsNaN(requested[0])) current.OrbitYaw = requested[0];
                if (!double.IsNaN(requested[1])) current.OrbitPitch = SignedAngle(requested[1]);
            }
            lock (stepSync)
            {
                current.OrbitYaw += stepYaw;
                current.OrbitPitch += stepPitch;
                stepYaw = stepPitch = 0;
                // Keep at least one camera unit so the look-at direction stays defined.
                if (!double.IsNaN(requestedDistance)) current.Distance = Math.Max(1, requestedDistance * current.Scale);
                current.Distance = Math.Max(1, current.Distance + stepDistance * current.Scale);
                requestedDistance = double.NaN;
                stepDistance = 0;
            }
            // Match CameraMovement: speeds are per 30 ms, and mouse deltas are per event.
            double step = Math.Max(0, Math.Min(seconds, 0.05)) / 0.03;
            current.OrbitYaw += (directions[0] + directions[3]) * rotationSpeed * step + mouseYaw;
            current.OrbitPitch += (directions[2] + directions[4]) * rotationSpeed * step + mousePitch;
            current.Distance = Math.Max(1, current.Distance - directions[1] * xyzSpeed * step);
            current.OrbitYaw = Wrap(current.OrbitYaw);
            // Clamp instead of wrapping: crossing straight up or down flips the yaw and reverses the controls.
            current.OrbitPitch = Math.Max(-CameraMovement.PitchLimit, Math.Min(CameraMovement.PitchLimit, current.OrbitPitch));
            orbitAngles = new[] { current.OrbitYaw, Wrap(current.OrbitPitch) };
            orbitDistance = (float)(current.Distance / Math.Max(current.Scale, 0.0001f));

            // The view points along (cos pitch cos yaw, cos pitch sin yaw, -sin pitch); positive pitch looks down.
            double cp = Math.Cos(current.OrbitPitch);
            var desired = new[]
            {
                target[0] - current.Distance * cp * Math.Cos(current.OrbitYaw),
                target[1] - current.Distance * cp * Math.Sin(current.OrbitYaw),
                target[2] + current.Distance * Math.Sin(current.OrbitPitch)
            };
            double blend = Blend(seconds);
            var position = new double[3];
            for (int i = 0; i < 3; i++)
            {
                position[i] = camera[i] + (desired[i] - camera[i]) * blend;
                result[i] = (float)(position[i] - camera[i]);
            }
            // Look at the player from where the camera actually is, which keeps the view upright
            // even when the vertical angle passes over the top.
            double lx = target[0] - position[0], ly = target[1] - position[1], lz = target[2] - position[2];
            double horizontal = Math.Sqrt(lx * lx + ly * ly);
            if (horizontal > 0.001)
            {
                double yaw = Math.Atan2(ly, lx);
                // Stay near the current value so the yaw does not jump by a full turn.
                yaw += Math.Round((camera[3] - yaw) / TwoPi) * TwoPi;
                result[3] = (float)(yaw - camera[3]);
            }
            result[4] = (float)(Math.Atan2(-lz, horizontal) - camera[4]);
            return result;
        }

        private double[] Target(State current, float[] player)
        {
            // Drag the tracked height only by the amount the player leaves the deadzone.
            float zone = deadzone, rise = player[2] - current.TrackedZ;
            if (rise > zone) current.TrackedZ = player[2] - zone;
            else if (rise < -zone) current.TrackedZ = player[2] + zone;
            return new double[] { player[0] * current.Scale, player[1] * current.Scale, current.TrackedZ * current.Scale };
        }

        // Exponential approach keeps the same feel regardless of the loop interval.
        private double Blend(double seconds)
        {
            float rate = speed;
            return rate <= 0 ? 1 : 1 - Math.Exp(-rate * Math.Max(0, Math.Min(seconds, 0.1)));
        }

        // Maps an angle to (-pi, pi], so 350 degrees means 10 degrees upward.
        private static double SignedAngle(double angle)
        {
            angle = Wrap(angle);
            return angle > Math.PI ? angle - TwoPi : angle;
        }

        private static double Wrap(double angle)
        {
            angle %= TwoPi;
            return angle < 0 ? angle + TwoPi : angle;
        }
    }
}
