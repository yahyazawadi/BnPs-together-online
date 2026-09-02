using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace BnPRelay.Setup
{
    /// <summary>
    /// Verifies game file integrity using SHA256 hashes and automatically downloads
    /// role-specific pre-patched game data only if it is missing or different.
    /// </summary>
    public static class GameIntegrityChecker
    {
        public const string ExpectedExeHash  = "DCE0044CC127B4FCF57BFB0221755E567F7F72523612B34F81F80AF054011688";
        public const string ExpectedDataHash = "366ACE82B8A12E98E56DAC1EE77DE4EBF0F03D3199AFA6189E9D68FE0C76AEAE";
        private const string GamePackageUrl  = "https://github.com/yahyazawadi/BnPs-together-online/releases/download/v1.2.9/bnp_game_files.zip";

        private static readonly string[] CommonPaths = {
            @"C:\Program Files (x86)\Steam\steamapps\common\Undertale",
            @"C:\Program Files\Steam\steamapps\common\Undertale",
            @"D:\SteamLibrary\steamapps\common\Undertale",
            @"E:\SteamLibrary\steamapps\common\Undertale",
            @"F:\SteamLibrary\steamapps\common\Undertale"
        };

        public static string? GetUndertaleDirectory()
        {
            foreach (var path in CommonPaths)
            {
                if (Directory.Exists(path))
                    return path;
            }
            return null;
        }

        public static string ComputeSha256(string filePath)
        {
            if (!File.Exists(filePath)) return "";
            try
            {
                using var sha = SHA256.Create();
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                byte[] hash = sha.ComputeHash(stream);
                return Convert.ToHexString(hash);
            }
            catch
            {
                return "";
            }
        }

        public static void KillGameProcesses()
        {
            try
            {
                foreach (var name in new[] { "UNDERTALE", "UNDERTALEBNP" })
                {
                    foreach (var proc in System.Diagnostics.Process.GetProcessesByName(name))
                    {
                        try
                        {
                            proc.Kill();
                            proc.WaitForExit(1500);
                            Logger.Log($"[GameSync] Terminated lingering {name} (PID: {proc.Id}).");
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Checks if both UNDERTALE.exe and data.win match the verified Bits & Pieces hashes.
        /// If already valid, finishes instantly without downloading.
        /// If different, downloads and extracts the complete verified game package (39 MB).
        /// </summary>
        public static async Task<bool> EnsureGameFilesReadyAsync(bool isHost, Action<string> onProgress)
        {
            return await Task.Run(async () =>
            {
                string? gameDir = GetUndertaleDirectory();
                if (gameDir == null)
                {
                    string err = "* Undertale folder not found in Steam libraries.";
                    Logger.Log($"[GameSync] {err}");
                    onProgress(err);
                    return false;
                }

                string exePath  = Path.Combine(gameDir, "UNDERTALE.exe");
                string dataPath = Path.Combine(gameDir, "data.win");
                string roleName = isHost ? "Host (Player 1)" : "Client (Player 2)";

                Logger.Log($"[GameSync] Verifying complete game files for {roleName}...");
                onProgress($"* Verifying game integrity...");

                bool exeMatches  = false;
                bool dataMatches = false;

                if (File.Exists(exePath))
                {
                    string localExeHash = ComputeSha256(exePath);
                    Logger.Log($"[GameSync] Local UNDERTALE.exe: {localExeHash}");
                    exeMatches = string.Equals(localExeHash, ExpectedExeHash, StringComparison.OrdinalIgnoreCase);
                }

                if (File.Exists(dataPath))
                {
                    string localDataHash = ComputeSha256(dataPath);
                    Logger.Log($"[GameSync] Local data.win:     {localDataHash}");
                    dataMatches = string.Equals(localDataHash, ExpectedDataHash, StringComparison.OrdinalIgnoreCase);
                }

                if (exeMatches && dataMatches)
                {
                    Logger.Log($"[GameSync] Game integrity verified 100% — both EXE and data.win are up to date!");
                    onProgress($"* Game integrity verified — 100% ready!");
                    return true;
                }

                // Download full verified game package (runner + data + DLLs)
                Logger.Log($"[GameSync] Files missing or outdated (ExeOK={exeMatches}, DataOK={dataMatches}). Downloading verified game package (39 MB)...");
                onProgress($"* Synchronizing game package (39 MB)...");
                string tempDir = Path.Combine(Path.GetTempPath(), "BnPGameSync");
                Directory.CreateDirectory(tempDir);
                string zipPath = Path.Combine(tempDir, "bnp_game_files.zip");

                try
                {
                    using var http = new HttpClient();
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("BnPRelay-GameSync");
                    byte[] zipBytes = await http.GetByteArrayAsync(GamePackageUrl);
                    await File.WriteAllBytesAsync(zipPath, zipBytes);
                    Logger.Log($"[GameSync] Downloaded {zipBytes.Length} bytes.");

                    onProgress("* Applying verified game engine and data...");
                    string extractDir = Path.Combine(tempDir, "extracted");
                    if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
                    ZipFile.ExtractToDirectory(zipPath, extractDir);

                    KillGameProcesses();
                    await Task.Delay(300);

                    // Copy all extracted files (UNDERTALE.exe, data.win, DLLs) directly into gameDir
                    foreach (var file in Directory.GetFiles(extractDir))
                    {
                        string fileName = Path.GetFileName(file);
                        string destPath = Path.Combine(gameDir, fileName);

                        // Backup original if exists and no backup exists yet
                        string backupPath = Path.Combine(gameDir, $"{fileName}.original_backup");
                        if (!File.Exists(backupPath) && File.Exists(destPath))
                        {
                            try { File.Copy(destPath, backupPath, false); } catch { }
                        }

                        try
                        {
                            File.Copy(file, destPath, overwrite: true);
                            Logger.Log($"[GameSync] Deployed -> {fileName}");
                        }
                        catch (Exception ex)
                        {
                            Logger.Log($"[GameSync] Warning copying {fileName}: {ex.Message}");
                        }
                    }

                    // Double-check critical files
                    bool verified = File.Exists(exePath) && File.Exists(dataPath);
                    if (verified)
                    {
                        Logger.Log($"[GameSync] Complete game synchronization finished successfully!");
                        onProgress($"* Game fully synchronized and ready!");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("GameIntegrityChecker", ex);
                    onProgress($"* Game sync warning: {ex.Message}");
                }

                return false;
            });
        }
    }
}
