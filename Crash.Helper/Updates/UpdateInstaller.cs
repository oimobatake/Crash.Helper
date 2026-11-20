using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Crash.Helper.Updates
{
    internal static class UpdateInstaller
    {
        internal const string Switch = "--apply-update";
        internal const string RecoverySwitch = "--skip-update-once";
        internal const string CleanupSwitch = "--clean-up-update";
        private const string WorkerName = "updater.exe";
        private const string CompletionMarker = "installed";
        internal static string StagingRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashHelper", "Updates");

        internal static void Start(UpdatePackage package)
        {
            package.Validate(package.PayloadPath);
            using (var parent = Process.GetCurrentProcess())
            {
                package.ParentId = parent.Id;
                package.ParentStartTicks = parent.StartTime.ToUniversalTime().Ticks;
            }
            package.Save();
            string worker = Path.Combine(package.DirectoryPath, WorkerName);
            File.Copy(typeof(Program).Assembly.Location, worker, false);
            string config = typeof(Program).Assembly.Location + ".config";
            if (File.Exists(config)) File.Copy(config, worker + ".config", false);
            using (var process = Process.Start(new ProcessStartInfo(worker, Switch)
            {
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = package.DirectoryPath
            }))
                if (process == null) throw new IOException("Could not start the update installer.");
        }

        internal static void Run()
        {
            UpdatePackage package = null;
            bool parentExited = false;
            try
            {
                package = UpdatePackage.Load(AppDomain.CurrentDomain.BaseDirectory);
                if (package.ParentId <= 0 || package.ParentId == Process.GetCurrentProcess().Id || package.ParentStartTicks <= 0)
                    throw new InvalidDataException("The update parent process is invalid.");
                Process parent = null;
                try { parent = Process.GetProcessById(package.ParentId); } catch (ArgumentException) { }
                using (parent)
                {
                    // Never kill the helper: normal closing must restore all game patches first.
                    if (parent != null)
                    {
                        try
                        {
                            if (parent.StartTime.ToUniversalTime().Ticks == package.ParentStartTicks && !parent.WaitForExit(60000))
                                throw new TimeoutException("The helper has not finished shutting down. The update was not installed.");
                        }
                        catch (InvalidOperationException) when (parent.HasExited) { }
                    }
                }
                parentExited = true;
                string id = new DirectoryInfo(package.DirectoryPath).Name;
                string arguments = IsUpdateId(id) ? CleanupSwitch + " " + id : string.Empty;
                Install(package, path => Restart(path, arguments));
                // The restarted helper removes the worker once Windows releases its executable.
                try { File.WriteAllText(Path.Combine(package.DirectoryPath, CompletionMarker), ""); }
                catch (Exception ex) { HelperLog.Error("Mark update as installed", ex); }
            }
            catch (Exception ex)
            {
                Log(package, ex);
                if (parentExited && package != null)
                {
                    try { Restart(package.TargetPath, RecoverySwitch); }
                    catch (Exception restartError) { Log(package, restartError); }
                }
            }
        }

        internal static void Install(UpdatePackage package, Action<string> restart)
        {
            string target = Path.GetFullPath(package.TargetPath);
            if (!Path.IsPathRooted(package.TargetPath) || !target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                Path.GetDirectoryName(target).Equals(Path.GetFullPath(package.DirectoryPath).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The update target is invalid.");
            package.Validate(package.PayloadPath);
            var current = AssemblyName.GetAssemblyName(target);
            Version incoming;
            if (current.Name != "Crash.Helper" || !UpdateRelease.TryVersion(package.Tag, out incoming) || incoming <= current.Version ||
                !string.Equals(UpdatePackage.Hash(target), package.PreviousSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The installed executable changed after the update was prepared.");

            // Copy beside the target so atomic replacement also works across different drives.
            string suffix = Guid.NewGuid().ToString("N");
            string candidate = target + ".update-" + suffix;
            string backup = target + ".previous-" + suffix;
            bool replaced = false;
            try
            {
                File.Copy(package.PayloadPath, candidate, false);
                package.Validate(candidate);
                File.Replace(candidate, target, backup);
                replaced = true;
                restart(target);
            }
            catch
            {
                if (replaced) File.Replace(backup, target, null);
                throw;
            }
            finally
            {
                try { File.Delete(candidate); } catch { }
            }
            // Cleanup failure must not roll back an application that has already started.
            try { File.Delete(backup); }
            catch (Exception ex) { HelperLog.Error("Remove update backup", ex); }
        }

        internal static void CleanupCompleted(string root, string pendingId)
        {
            try
            {
                if (!Directory.Exists(root) || (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) return;
                bool launchedByUpdater = IsUpdateId(pendingId);
                // Older workers remove the payload and manifest just after launching the helper.
                if (!launchedByUpdater) Thread.Sleep(1000);
                foreach (string directory in Directory.EnumerateDirectories(root))
                {
                    string id = Path.GetFileName(directory);
                    if (!string.Equals(id, pendingId, StringComparison.OrdinalIgnoreCase))
                        TryRemoveCompletedPackage(root, id);
                }
                if (!launchedByUpdater) return;
                var wait = Stopwatch.StartNew();
                while (wait.Elapsed < TimeSpan.FromSeconds(30))
                {
                    if (TryRemoveCompletedPackage(root, pendingId)) return;
                    Thread.Sleep(250);
                }
                HelperLog.Error("Clean up update", new IOException("The update worker did not release its staging files."));
            }
            catch (Exception ex) { HelperLog.Error("Clean up update", ex); }
        }

        private static bool IsUpdateId(string id) => id?.Length == 32 && Guid.TryParseExact(id, "N", out _);

        internal static bool TryRemoveCompletedPackage(string root, string id)
        {
            if (!IsUpdateId(id)) return false;
            string directory = Path.Combine(root, id);
            string marker = Path.Combine(directory, CompletionMarker);
            try
            {
                if (!Directory.Exists(directory)) return true;
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) return false;
                string worker = Path.Combine(directory, WorkerName);
                string config = worker + ".config";
                // Old installers left only these two files when the replacement succeeded.
                if (!File.Exists(marker) && ((!File.Exists(worker) && !File.Exists(config)) ||
                    Directory.EnumerateFileSystemEntries(directory).Any(path =>
                        !string.Equals(path, worker, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(path, config, StringComparison.OrdinalIgnoreCase)))) return false;
                File.Delete(worker);
                File.Delete(config);
                var package = new UpdatePackage { DirectoryPath = directory };
                File.Delete(package.PayloadPath);
                File.Delete(package.ManifestPath);
                File.Delete(marker);
                if (!Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
                return true;
            }
            catch (DirectoryNotFoundException) { return true; }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        private static void Restart(string path, string arguments)
        {
            using (var process = Process.Start(new ProcessStartInfo(path, arguments)
            {
                UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(path)
            }))
                if (process == null) throw new IOException("Could not restart Crash Helper.");
        }

        private static void Log(UpdatePackage package, Exception error)
        {
            HelperLog.Error("Install update", error);
            if (package == null) return;
            try
            {
                File.AppendAllText(Path.Combine(Path.GetDirectoryName(package.TargetPath), "CrashHelper.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " Install update: " + error + Environment.NewLine);
            }
            catch { }
        }
    }
}
