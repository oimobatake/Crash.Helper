using System;
using System.Diagnostics;
using System.IO;

namespace Crash.Helper
{
    internal static class HelperLog
    {
        private static readonly object Sync = new object();
        private static string lastMessage;
        private static DateTime lastWritten;

        internal static void Error(string operation, Exception error)
        {
            string message = operation + ": " + error;
            lock (Sync)
            {
                if (message == lastMessage && DateTime.UtcNow - lastWritten < TimeSpan.FromSeconds(30)) return;
                lastMessage = message;
                lastWritten = DateTime.UtcNow;
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + message + Environment.NewLine;
                try { File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CrashHelper.log"), line); }
                catch
                {
                    try
                    {
                        string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashHelper");
                        Directory.CreateDirectory(directory);
                        File.AppendAllText(Path.Combine(directory, "CrashHelper.log"), line);
                    }
                    catch { Trace.WriteLine(line); }
                }
            }
        }
    }
}
