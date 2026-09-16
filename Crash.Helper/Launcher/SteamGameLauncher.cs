using System;
using System.Diagnostics;

namespace Crash.Helper.Launcher
{
    internal sealed class SteamGameLauncher
    {
        private readonly HelperSettings settings;
        public SteamGameLauncher(HelperSettings settings) { this.settings = settings; }

        internal ProcessStartInfo CreateStartInfo(string levelPath)
        {
            const string commandPrefix = "loadmap ";
            if (levelPath != null && levelPath.StartsWith(commandPrefix, StringComparison.Ordinal))
                levelPath = levelPath.Substring(commandPrefix.Length);

            return new ProcessStartInfo
            {
                FileName = settings.SteamPath,
                Arguments = $"-applaunch 731490 --overridemap {levelPath}",
                UseShellExecute = true
            };
        }

        public void Launch(string levelPath) { Process.Start(CreateStartInfo(levelPath)); }
    }
}
