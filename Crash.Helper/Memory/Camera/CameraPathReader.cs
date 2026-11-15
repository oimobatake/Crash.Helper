using System;
using System.Collections.Generic;
using System.Linq;
using Crash.Helper.Memory.Runtime;

namespace Crash.Helper.Memory.Camera
{
    // Activation boxes describe entrances. A spline describes the area actually viewed by a camera.
    internal sealed class CameraPathReader
    {
        private readonly RuntimeMemory memory;
        internal CameraPathReader(RuntimeMemory memory) { this.memory = memory; }

        internal float[][] Read(long camera, List<RuntimeGuard> guards)
        {
            if (memory.TypeName(camera) != "CSplineCamera") return null;
            long handle = memory.Pointer(guards, camera + 0x1B0), entity = memory.Pointer(guards, handle + 0x28);
            memory.Capture(guards, entity);
            long components = memory.Pointer(guards, entity + 0x18);
            var entries = memory.List(components, guards, 128, false); long component = 0;
            for (int i = 0; i < entries.Length; i++)
                if (entries[i] != 0 && memory.TypeName(entries[i]) == "CSplineComponent")
                { component = entries[i]; memory.Capture(guards, memory.Number(components + 0x20) + i * 8L); break; }
            if (component == 0) return null;
            memory.Capture(guards, component);
            long spline = memory.Capture(guards, component + 0x40);
            bool world = spline != 0 && memory.Number(component + 0x38, 1) == 0;
            if (!world)
            {
                long data = memory.Pointer(guards, component + 0x10); spline = memory.Pointer(guards, data + 0x18);
            }
            if (memory.TypeName(spline) != "igSpline2") return null;
            long controls = memory.Pointer(guards, spline + 0x10);
            var points = memory.List(controls, guards, 4096, false);
            if (points.Length < 2) return null;
            var result = points.Select(point => memory.Vector(point + 0x10)).ToArray();
            if (!result.SelectMany(point => point).All(RuntimeMemory.Finite)) return null;
            if (!world)
            {
                // Match CEntityBolt's world-matrix fallback. An allocated bolt can have no target.
                var visited = new HashSet<long>();
                while (entity != 0)
                {
                    if (!visited.Add(entity) || visited.Count > 16) return null;
                    memory.Capture(guards, entity);
                    var origin = memory.Vector(entity + 0x20); var orientation = new[] { 0f, 0f, 0f, 1f }; var scale = new[] { 1f, 1f, 1f };
                    long transform = memory.Capture(guards, entity + 0x30);
                    if (transform != 0)
                    {
                        var bytes = memory.Read(transform + 0x10, 16);
                        orientation = Enumerable.Range(0, 4).Select(i => BitConverter.ToSingle(bytes, i * 4)).ToArray();
                        float uniform = BitConverter.ToSingle(memory.Read(transform + 0x6C, 4), 0);
                        scale = memory.Vector(transform + 0x70).Select(value => value * uniform).ToArray();
                    }
                    if (!origin.Concat(orientation).Concat(scale).All(RuntimeMemory.Finite)) return null;
                    double norm = orientation.Sum(value => (double)value * value);
                    if (Math.Abs(norm - 1) > 0.01) return null;
                    result = result.Select(point => CameraSelection.TransformPoint(point, origin, orientation, scale)).ToArray();
                    long bolt = memory.Capture(guards, entity + 0x10);
                    if (bolt == 0) break;
                    if (memory.TypeName(bolt) != "CEntityBolt") return null;
                    memory.Capture(guards, bolt);
                    // Named model/bone bolts use the engine's cached world spline instead.
                    if (memory.Capture(guards, bolt + 0x18) != 0) return null;
                    long target = memory.Capture(guards, bolt + 0x10);
                    entity = target == 0 ? 0 : memory.Capture(guards, target + 0x28);
                }
            }
            if (memory.Number(spline + 0x6C, 1) != 0) result = result.Concat(new[] { result[0] }).ToArray();
            return result;
        }
    }
}
