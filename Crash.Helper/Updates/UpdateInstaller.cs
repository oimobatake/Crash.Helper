using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;

namespace Crash.Helper.Updates
{
    internal static class UpdateInstaller
    {
        internal const string Switch = "--apply-update";
        internal const string RecoverySwitch = "--skip-update-once";

        internal static void Start(UpdatePackage package)
        {
            package.Validate(package.PayloadPath);
            using (var parent = Process.GetCurrentProcess())
            {
                package.ParentId = parent.Id;
                package.ParentStartTicks = parent.StartTime.ToUniversalTime().Ticks;
            }
            package.Save();
            string worker = Path.Combine(package.DirectoryPath, "updater.exe");
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
                Install(package, path => Restart(path, false));
                try { File.Delete(package.PayloadPath); File.Delete(package.ManifestPath); } catch { }
            }
            catch (Exception ex)
            {
                Log(package, ex);
                if (parentExited && package != null)
                {
                    try { Restart(package.TargetPath, true); }
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
                // Keep the previous executable available if the new application cannot start normally.
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
        }

        private static void Restart(string path, bool recovery)
        {
            using (var process = Process.Start(new ProcessStartInfo(path, recovery ? RecoverySwitch : "")
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
