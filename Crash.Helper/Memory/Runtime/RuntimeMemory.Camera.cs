using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Crash.Helper.Memory.Camera;

namespace Crash.Helper.Memory.Runtime
{
    internal sealed partial class RuntimeMemory
    {
        internal long Camera(List<RuntimeGuard> guards)
        {
            Capture(guards, Base + 0x1A5C6E0); Capture(guards, Base + 0x1A5C6E8);
            long player = LocalPlayer(guards), system = Pointer(guards, Base + 0x1A890E0); Type(system, "CCameraSystem");
            long cameras = Pointer(guards, system + 0x20), selected = 0, unowned = 0;
            int unownedCount = 0;
            foreach (long instance in List(cameras, guards, 16))
            {
                if (instance == 0) continue; Type(instance, "CCameraSystemInstance");
                long owner = Capture(guards, instance + 0x70);
                if (owner != 0 && Capture(guards, owner + 0x28) != player) continue;
                if (Number(instance + 0x178, 1) == 0 || Number(instance + 0x179, 1) == 0 || Number(instance + 0x2D0, 1) == 0) continue;
                Capture(guards, instance + 0x178, 1); Capture(guards, instance + 0x179, 1); Capture(guards, instance + 0x2D0, 1);
                if (owner == 0) { unowned = instance; unownedCount++; }
                else { if (selected != 0) return 0; selected = instance; }
            }
            // Single-player levels can create their only gameplay camera without an owner handle.
            // Never guess between several unowned cameras or select another player's camera.
            if (selected == 0 && unownedCount == 1) selected = unowned;
            return selected != 0 && Matches(guards) ? selected + 0x80 : 0;
        }
        internal float? PlayerCameraHeight(long instance, long actor)
        {
            try
            {
                var guards = new List<RuntimeGuard>();
                long targeting = Pointer(guards, instance + 0x48);
                long heroGroup = Pointer(guards, targeting + 0x58);
                Type(heroGroup, "CCameraTargetGroup");
                long targets = Pointer(guards, heroGroup + 0x10);
                foreach (long handle in List(targets, guards, 16))
                {
                    if (handle == 0) continue;
                    long target = Capture(guards, handle + 0x28);
                    if (target == 0 || TypeName(target) != "CCameraTarget") continue;
                    Capture(guards, target);
                    long entity = Capture(guards, target + 0x10);
                    if (entity == 0 || Capture(guards, entity + 0x28) != actor) continue;
                    float height = BitConverter.ToSingle(Read(target + 0x2C, 4), 0);
                    if (Finite(height) && height > 0 && Matches(guards)) return height;
                }
            }
            catch (Win32Exception) { }
            catch (InvalidOperationException) { }
            catch (OverflowException) { }
            return null;
        }

        internal long NearestCamera(long instance, long actor, List<RuntimeGuard> guards, out long selectedCamera)
            => SelectCamera(instance, actor, 0, guards, out selectedCamera);

        internal long CameraBeyondRail(long instance, long actor, long activeCamera, List<RuntimeGuard> guards, out long selectedCamera)
            => SelectCamera(instance, actor, activeCamera, guards, out selectedCamera);

        private long SelectCamera(long instance, long actor, long recoveryCamera, List<RuntimeGuard> guards, out long selectedCamera)
        {
            selectedCamera = 0;
            long database = Pointer(guards, instance + 0x2B0); Type(database, "CCameraBoxDatabase");
            long list = Pointer(guards, database + 0x10); Type(list, "CCameraBoxList");
            var boxes = List(list, guards, 2048, false); long array = Number(list + 0x20);
            long defaultBox = Capture(guards, instance + 0x150);
            float[] point = Number(actor + 0x10) == 0 ? Vector(actor + 0x20) : null;
            if (point == null)
            {
                long targeting = Pointer(guards, instance + 0x48), primary = Pointer(guards, targeting + 0x50);
                point = Vector(primary + 0x18);
            }
            if (!point.All(Finite)) return 0;
            var paths = new CameraPathReader(this);
            double recoveryDistance = 0;
            if (recoveryCamera != 0)
            {
                if (ReadCurrentCamera(instance, guards) != recoveryCamera) return 0;
                var currentPath = paths.Read(recoveryCamera, guards);
                if (currentPath == null) return 0;
                recoveryDistance = CameraSelection.MeasurePath(point, currentPath, out double beyondEnd);
                // Leave the native camera alone through shot changes and the overlap between rails.
                if (beyondEnd < 20) return 0;
            }
            long nearest = 0, chosenCamera = 0;
            int bestRank = int.MaxValue;
            double best = double.PositiveInfinity, entranceBest = double.PositiveInfinity;
            List<RuntimeGuard> selectedGuards = null, currentGuards = null;
            long current = CurrentCamera(instance), currentBox = 0;
            int currentRank = int.MaxValue;
            double currentScore = double.PositiveInfinity;
            var databaseBoxes = new HashSet<long>(boxes);
            var previousBoxes = new HashSet<long>();
            var registeredCameras = new HashSet<long>();
            for (int i = 0; i < boxes.Length; i++)
            {
                long box = boxes[i];
                if (box == 0 || box == defaultBox) continue;
                try
                {
                    var source = new List<RuntimeGuard>();
                    if (Capture(source, array + i * 8L) != box) continue;
                    Type(box, "CCameraBox");
                    Capture(source, box);
                    registeredCameras.Add(Number(box + 0x158));
                    if (Number(box + 0x191, 1) != 0) Consider(box, source, false);

                    // Some authored cameras (including a level's opening rail) exist only as
                    // the previous view of a transition. The game gives them a reusable box.
                    var previousSource = new List<RuntimeGuard>(source);
                    long previous = Capture(previousSource, box + 0x150);
                    if (previous != 0 && previous != defaultBox && !databaseBoxes.Contains(previous) && !previousBoxes.Contains(previous))
                    {
                        long authored = Capture(previousSource, box + 0x168);
                        if (authored != 0 && TypeName(previous) == "CCameraBox" && Capture(previousSource, previous + 0x158) == authored)
                        {
                            previousBoxes.Add(previous);
                            Consider(previous, previousSource, true);
                        }
                    }
                }
                catch (Win32Exception) { }
                catch (InvalidOperationException) { }
            }

            void Consider(long box, List<RuntimeGuard> source, bool previousOnly)
            {
                var guardsForBox = new List<RuntimeGuard>(source);
                Capture(guardsForBox, box);
                if (Capture(guardsForBox, box + 0x191, 1) == 0 || Capture(guardsForBox, box + 0x158) == 0) return;
                double distance = double.PositiveInfinity;
                if (!previousOnly)
                {
                    // The native volume query uses the camera transform, in radians and with uniform scale.
                    var position = Vector(box + 0x100);
                    var rotation = Vector(box + 0x10C);
                    float scale = BitConverter.ToSingle(Read(box + 0x118, 4), 0);
                    var min = Vector(box + 0x120);
                    var max = Vector(box + 0x12C);
                    if (!position.Concat(rotation).Concat(min).Concat(max).All(Finite) || !Finite(scale) || scale <= 0 ||
                        Enumerable.Range(0, 3).Any(axis => min[axis] > max[axis])) return;
                    distance = CameraSelection.DistanceSquared(point, position, rotation, min, max, scale);
                }
                foreach (var candidate in CameraChoices(box, guardsForBox, new HashSet<long>(), 0))
                {
                    registeredCameras.Add(candidate.Item1);
                    if (recoveryCamera != 0 && candidate.Item1 == recoveryCamera) continue;
                    var candidateGuards = candidate.Item2;
                    float[][] path = null;
                    try { path = paths.Read(candidate.Item1, candidateGuards); }
                    catch (Win32Exception) { continue; }
                    catch (InvalidOperationException) { continue; }
                    if (path == null && previousOnly) continue;
                    int rank = distance < 0.0001 ? 0 : path != null ? 1 : 2;
                    double beyondEnd = 0;
                    double score = path != null ? CameraSelection.MeasurePath(point, path, out beyondEnd) : distance;
                    if (recoveryCamera != 0)
                    {
                        // A replacement must cover the player rather than end at the same junction.
                        if (path == null || beyondEnd > 0 || !CameraSelection.WithinPathTie(score, recoveryDistance))
                            continue;
                    }
                    if (path != null && candidate.Item1 == current && (rank < currentRank || (rank == currentRank && score < currentScore)))
                    {
                        currentBox = box; currentRank = rank; currentScore = score; currentGuards = candidateGuards;
                    }
                    if (rank < bestRank || (rank == bestRank && (score < best - 0.01 || (Math.Abs(score - best) < 0.01 && distance < entranceBest))))
                    {
                        bestRank = rank; best = score; entranceBest = distance; nearest = box;
                        chosenCamera = candidate.Item1; selectedGuards = candidateGuards;
                    }
                }
            }

            // Adjacent rails can overlap, and our control-point segments approximate curved paths.
            // Keep the engine's active view for a near tie instead of changing its authored offset.
            if (currentBox != 0 && currentRank == bestRank && CameraSelection.WithinPathTie(currentScore, best))
            { nearest = currentBox; chosenCamera = current; selectedGuards = currentGuards; best = currentScore; }
            if (bestRank != 0 && TryLoadedCamera(instance, point, paths, registeredCameras, chosenCamera, best, recoveryCamera,
                recoveryDistance, guards, out selectedCamera))
                return 0; // A loaded rail without an activation box is reset directly.
            if (nearest == 0) return 0;
            guards.AddRange(selectedGuards);
            long active = Pointer(guards, instance + 0x58); Type(active, "CCameraBoxList");
            if (!Matches(guards)) return 0;
            selectedCamera = chosenCamera;
            return nearest;
        }
        internal long CurrentCamera(long instance) => ReadCurrentCamera(instance, new List<RuntimeGuard>());

        private long ReadCurrentCamera(long instance, List<RuntimeGuard> guards)
        {
            try
            {
                long list = Pointer(guards, instance + 0x58);
                long count = Capture(guards, list + 0x10, 4);
                if (count == 0 || count > 128) return 0;
                long array = Pointer(guards, list + 0x20);
                long camera = Capture(guards, array);
                var visited = new HashSet<long>();
                while (camera != 0 && visited.Add(camera) && visited.Count <= 8)
                {
                    Capture(guards, camera);
                    if (TypeName(camera) != "CCameraBox") return Matches(guards) ? camera : 0;
                    long handle = Capture(guards, camera + 0x160), active = handle == 0 ? 0 : Capture(guards, handle + 0x28);
                    camera = active != 0 ? active : Capture(guards, camera + 0x158);
                }
            }
            catch (Win32Exception) { }
            catch (InvalidOperationException) { }
            return 0;
        }
        private IEnumerable<Tuple<long, List<RuntimeGuard>>> CameraChoices(long box, List<RuntimeGuard> prefix, HashSet<long> visited, int depth)
        {
            if (depth > 8 || !visited.Add(box)) yield break;
            var references = new List<Tuple<long, List<RuntimeGuard>>>();
            var primary = new List<RuntimeGuard>(prefix); long camera = Capture(primary, box + 0x158);
            if (camera != 0) references.Add(Tuple.Create(camera, primary));
            var active = new List<RuntimeGuard>(prefix); long handle = Capture(active, box + 0x160);
            if (handle != 0) { camera = Capture(active, handle + 0x28); if (camera != 0) references.Add(Tuple.Create(camera, active)); }
            long blends = Number(box + 0x170);
            if (blends != 0)
            {
                int count = checked((int)Number(blends + 0x10, 4));
                if (count < 0 || count > 128) throw new InvalidOperationException("The camera blend list is unavailable.");
                long array = Number(blends + 0x20);
                for (int i = 0; i < count; i++)
                {
                    var blend = new List<RuntimeGuard>(prefix); Capture(blend, box + 0x170); Capture(blend, blends + 0x10, 4); Capture(blend, blends + 0x20);
                    camera = Capture(blend, array + i * 32L); if (camera != 0) references.Add(Tuple.Create(camera, blend));
                }
            }
            foreach (var reference in references)
            {
                Capture(reference.Item2, reference.Item1);
                if (TypeName(reference.Item1) == "CCameraBox")
                {
                    foreach (var nested in CameraChoices(reference.Item1, reference.Item2, visited, depth + 1)) yield return nested;
                }
                else if (visited.Add(reference.Item1)) yield return reference;
            }
        }
    }
}
