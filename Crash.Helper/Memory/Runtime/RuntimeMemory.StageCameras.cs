using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Crash.Helper.Memory.Camera;

namespace Crash.Helper.Memory.Runtime
{
    internal sealed partial class RuntimeMemory
    {
        private bool TryLoadedCamera(long instance, float[] point, CameraPathReader paths, HashSet<long> registeredCameras,
            long registered, double registeredDistance, long recoveryCamera, double recoveryDistance,
            List<RuntimeGuard> guards, out long camera)
        {
            camera = 0;
            List<RuntimeGuard> bestGuards = null;
            double bestDistance = double.PositiveInfinity;
            try
            {
                bool registeredBehind = false;
                if (registered != 0)
                {
                    try
                    {
                        var registeredPath = paths.Read(registered, new List<RuntimeGuard>());
                        registeredBehind = registeredPath != null &&
                            CameraSelection.SideRailView(point, registeredPath, Vector(registered + 0x1C)[2],
                                out double depth, out _, out _) && depth < -20;
                    }
                    catch (Win32Exception) { }
                    catch (InvalidOperationException) { }
                }

                foreach (var entry in LoadedStageCameras(instance))
                {
                    if (entry.Item1 == recoveryCamera || registeredCameras.Contains(entry.Item1)) continue;
                    try
                    {
                        var path = paths.Read(entry.Item1, entry.Item2);
                        if (path == null || !CameraSelection.SideRailView(point, path, Vector(entry.Item1 + 0x1C)[2],
                            out double depth, out double distance, out double beyondEnd)) continue;

                        // These views have no trigger volume. Require a point in front of their
                        // authored rail and within the camera's own depth tether.
                        float tether = BitConverter.ToSingle(Read(entry.Item1 + 0x150, 4), 0);
                        if (!Finite(tether) || tether <= 0 || depth <= 0 || depth > tether || beyondEnd > tether / 4 ||
                            distance > 2.0 * tether * tether || distance >= bestDistance ||
                            (!registeredBehind && distance >= registeredDistance) ||
                            (recoveryCamera != 0 && !CameraSelection.WithinPathTie(distance, recoveryDistance))) continue;
                        if (!Matches(entry.Item2)) continue;
                        camera = entry.Item1;
                        bestDistance = distance;
                        bestGuards = entry.Item2;
                    }
                    catch (Win32Exception) { }
                    catch (InvalidOperationException) { }
                }
            }
            catch (Win32Exception) { }
            catch (InvalidOperationException) { }
            catch (OverflowException) { }
            if (bestGuards == null || !Matches(guards) || !Matches(bestGuards))
            {
                camera = 0;
                return false;
            }
            guards.AddRange(bestGuards);
            return true;
        }

        private IEnumerable<Tuple<long, List<RuntimeGuard>>> LoadedStageCameras(long instance)
        {
            var scene = Text(Base + 0x1A5C6E0);
            if (string.IsNullOrEmpty(scene)) yield break;
            var filePrefix = "maps:/" + scene + "_camera";
            var root = new List<RuntimeGuard>();
            long manager = Pointer(root, Base + 0x1AA1F68);
            Type(manager, "CStreamingLevelManager");
            if (Capture(root, manager + 0x28, 1) != 0 || Capture(root, manager + 0x29, 1) == 0 ||
                Capture(root, manager + 0x2A, 1) != 0) yield break;

            // The instance's constant settings directory gives us the actual type signature.
            long settingsDirectory = Pointer(root, instance + 0x158);
            Type(settingsDirectory, "igObjectDirectory");
            long directoryType = Capture(root, settingsDirectory);
            long[] levels = StreamingEntries(manager + 0x10, root, 32, out long levelArray);
            for (int i = 0; i < levels.Length; i++)
            {
                var levelGuards = new List<RuntimeGuard>(root);
                long level = levels[i];
                if (level == 0 || TypeName(level) != "CStandardStreamingLevel" ||
                    Capture(levelGuards, levelArray + i * 8L) != level ||
                    !string.Equals(Text(Pointer(levelGuards, level + 0x30)), scene, StringComparison.OrdinalIgnoreCase)) continue;
                Capture(levelGuards, level);
                if (Capture(levelGuards, level + 0x14, 4) != 2) continue;
                var chunks = StreamingEntries(level + 0x18, levelGuards, 128, out long chunkArray);
                for (int j = 0; j < chunks.Length; j++)
                {
                    var chunkGuards = new List<RuntimeGuard>(levelGuards);
                    long chunk = chunks[j];
                    if (chunk == 0 || TypeName(chunk) != "igStreamingChunk" ||
                        Capture(chunkGuards, chunkArray + j * 8L) != chunk ||
                        Capture(chunkGuards, chunk + 0x28, 4) != 2) continue;
                    Capture(chunkGuards, chunk);
                    var files = StreamingEntries(chunk + 0x78, chunkGuards, 8192, out long fileArray);
                    for (int k = 0; k < files.Length; k++)
                    {
                        long directory = files[k];
                        if (directory < 0x10000 || Number(directory) != directoryType) continue;
                        long file = Number(directory + 0x20);
                        if (file < 0x10000) continue;
                        string path = Text(file);
                        if (!path.StartsWith(filePrefix, StringComparison.OrdinalIgnoreCase) ||
                            path.Length <= filePrefix.Length ||
                            (path[filePrefix.Length] != '.' && path[filePrefix.Length] != '_') ||
                            !path.EndsWith(".igz", StringComparison.OrdinalIgnoreCase)) continue;

                        var fileGuards = new List<RuntimeGuard>(chunkGuards);
                        if (Capture(fileGuards, fileArray + k * 8L) != directory ||
                            Capture(fileGuards, directory) != directoryType ||
                            Capture(fileGuards, directory + 0x20) != file ||
                            Capture(fileGuards, directory + 0x7C, 4) == 0) continue;
                        long objects = Pointer(fileGuards, directory + 0x28);
                        Type(objects, "igObjectList");
                        var entries = List(objects, fileGuards, 4096, false);
                        long objectArray = Number(objects + 0x20);
                        for (int n = 0; n < entries.Length; n++)
                        {
                            long candidate = entries[n];
                            if (candidate == 0 || TypeName(candidate) != "CSplineCamera") continue;
                            var selected = new List<RuntimeGuard>(fileGuards);
                            if (Capture(selected, objectArray + n * 8L) != candidate) continue;
                            Capture(selected, candidate);
                            yield return Tuple.Create(candidate, selected);
                        }
                    }
                }
            }
        }

        private long[] StreamingEntries(long vector, List<RuntimeGuard> guards, int limit, out long array)
        {
            array = 0;
            int count = checked((int)Capture(guards, vector, 4));
            if (count == 0) return Array.Empty<long>();
            if (count > limit) throw new InvalidOperationException("The streaming object list is unavailable.");
            array = Pointer(guards, vector + 0x10);
            byte[] entries = Read(array, count * 8);
            return Enumerable.Range(0, count).Select(index => BitConverter.ToInt64(entries, index * 8)).ToArray();
        }
    }
}
