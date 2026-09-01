using System;
using System.IO;
using System.Linq;

namespace BnPRelay.Sync
{
    /// <summary>
    /// Watches the Undertale save directory for changes and maintains
    /// rolling timestamped backups in %AppData%\BnPTogether\saves\.
    /// Covers file0, file8, file9, and undertale.ini.
    /// </summary>
    public class SaveFileMirror : IDisposable
    {
        private static readonly string UndertaleDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UNDERTALE");
        private static readonly string BackupDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BnPTogether", "saves");

        private static readonly string[] WatchedFiles = { "file0", "file8", "file9", "undertale.ini" };
        private const int MaxBackups = 5;

        private readonly FileSystemWatcher _watcher;

        /// <summary>Raised whenever a watched save file changes — payload is (fileName, fileData).</summary>
        public event Action<string, byte[]>? SaveChanged;

        public SaveFileMirror()
        {
            Directory.CreateDirectory(BackupDir);
            Directory.CreateDirectory(UndertaleDir);

            _watcher = new FileSystemWatcher(UndertaleDir)
            {
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            };
            _watcher.Changed += OnFileChanged;
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            string fileName = Path.GetFileName(e.Name);
            if (!WatchedFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                return;

            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                try
                {
                    if (File.Exists(e.FullPath))
                    {
                        byte[] data = File.ReadAllBytes(e.FullPath);
                        CreateBackup(fileName, data);
                        SaveChanged?.Invoke(fileName, data);
                    }
                }
                catch { /* Game may still have file locked — we'll catch the next write */ }
            });
        }

        private void CreateBackup(string fileName, byte[] data)
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupPath = Path.Combine(BackupDir, $"{fileName}_{stamp}.bak");
            File.WriteAllBytes(backupPath, data);
            PruneOldBackups(fileName);
        }

        private void PruneOldBackups(string fileName)
        {
            var old = Directory.GetFiles(BackupDir, $"{fileName}_*.bak")
                               .OrderByDescending(f => f)
                               .Skip(MaxBackups);
            foreach (var f in old) File.Delete(f);
        }

        /// <summary>Write a received save file to the Undertale directory (used by Client).</summary>
        public static void WriteSaveFile(string fileName, byte[] data)
        {
            string path = Path.Combine(UndertaleDir, fileName);
            File.WriteAllBytes(path, data);
        }

        /// <summary>Read the current save file bytes (used by Host to send at session start).</summary>
        public static byte[]? ReadSaveFile(string fileName)
        {
            string path = Path.Combine(UndertaleDir, fileName);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        /// <summary>Lists available backup saves for the Restore UI.</summary>
        public static SaveBackupEntry[] ListBackups(string fileName) =>
            Directory.GetFiles(BackupDir, $"{fileName}_*.bak")
                     .OrderByDescending(f => f)
                     .Select(f => new SaveBackupEntry(f))
                     .ToArray();

        public void Dispose() => _watcher.Dispose();
    }

    public record SaveBackupEntry(string Path)
    {
        private static readonly string[] Locations =
            { "Ruins", "Snowdin", "Waterfall", "Hotland", "CORE", "True Lab", "New Home" };

        public string FileName => System.IO.Path.GetFileName(Path);

        public string Timestamp
        {
            get
            {
                // Parse yyyyMMdd_HHmmss from filename
                var name = System.IO.Path.GetFileNameWithoutExtension(Path);
                var parts = name.Split('_');
                if (parts.Length >= 3 &&
                    DateTime.TryParseExact($"{parts[^2]}_{parts[^1]}", "yyyyMMdd_HHmmss",
                        null, System.Globalization.DateTimeStyles.None, out var dt))
                    return dt.ToString("MMM d, h:mm tt");
                return name;
            }
        }

        /// <summary>
        /// Tries to infer a friendly location name from save file content.
        /// Falls back to timestamp.
        /// </summary>
        public string FriendlyLabel
        {
            get
            {
                try
                {
                    string text = File.ReadAllText(Path);
                    foreach (var loc in Locations)
                        if (text.Contains(loc, StringComparison.OrdinalIgnoreCase))
                            return $"{loc}  [{Timestamp}]";
                }
                catch { }
                return Timestamp;
            }
        }

        public void RestoreTo(string targetFileName)
        {
            string dest = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "UNDERTALE", targetFileName);
            File.Copy(Path, dest, overwrite: true);
        }
    }
}
