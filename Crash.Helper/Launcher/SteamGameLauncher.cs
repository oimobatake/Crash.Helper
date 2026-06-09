using System;
using System.Diagnostics;
using System.IO;

namespace Crash.Helper.Launcher
{
    internal static class SteamGameLauncher
    {
        private const string AppId = "731490";
        private const string DefaultMap = "crash2/l212_sewerorlater/l212_sewerorlater";

        public static bool Launch(string levelPath)
        {
            var steamExePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Steam",
                "steam.exe");

            if (!File.Exists(steamExePath))
            {
                return false;
            }

            var map = string.IsNullOrWhiteSpace(levelPath) ? DefaultMap : levelPath.Trim();

            var psi = new ProcessStartInfo
            {
                FileName = steamExePath,
                Arguments = string.Format("-applaunch {0} --overridemap {1}", AppId, map),
                UseShellExecute = false
            };

            Process.Start(psi);
            return true;
        }
    }
}