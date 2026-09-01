using System;
using System.IO;

namespace BnPRelay
{
    /// <summary>
    /// Thread-safe logger writing timestamped diagnostic messages to bnprelay.log
    /// in both %LocalAppData%\BnPTogether\logs and the application folder.
    /// </summary>
    public static class Logger
    {
        private static readonly object _lock = new();
        private static string? _logPath;

        public static string LogPath
        {
            get
            {
                if (_logPath == null)
                {
                    string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BnPTogether", "logs");
                    Directory.CreateDirectory(dir);
                    _logPath = Path.Combine(dir, "bnprelay.log");
                }
                return _logPath;
            }
        }

        public static void Log(string message)
        {
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
            lock (_lock)
            {
                try
                {
                    File.AppendAllText(LogPath, line + Environment.NewLine);
                    // Also mirror into local directory if writable
                    string localLog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bnprelay.log");
                    if (localLog != LogPath)
                    {
                        try { File.AppendAllText(localLog, line + Environment.NewLine); } catch { }
                    }
                }
                catch { }
            }
        }

        public static void LogError(string context, Exception ex)
        {
            Log($"[ERROR] {context}: {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}");
        }
    }
}
