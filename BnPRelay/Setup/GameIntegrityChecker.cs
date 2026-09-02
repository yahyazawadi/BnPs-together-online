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
        public const string UnifiedDataHash = "366ACE82B8A12E98E56DAC1EE77DE4EBF0F03D3199AFA6189E9D68FE0C76AEAE";
        private const string UnifiedDataUrl = "https://github.com/yahyazawadi/BnPs-together-online/releases/download/v1.2.8/data_unified.win.zip";

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
                if (Directory.Exists(path) && File.Exists(Path.Combine(path, "UNDERTALE.exe")))
                    return path;
            }
            return null;
        }

        public static string ComputeSha256(string filePath)
        {
            if (!File.Exists(filePath)) return "";
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hash = sha.ComputeHash(stream);
            return Convert.ToHexString(hash);
        }

        /// <summary>
        /// Checks if the local data.win matches the verified unified hash.
        /// If already valid, finishes instantly without downloading.
        /// If different or missing, downloads and swaps the verified pre-patched file.
        /// </summary>
        public static async Task<bool> EnsureGameFilesReadyAsync(bool isHost, Action<string> onProgress)
        {
            return await Task.Run(async () =>
            {
                string? gameDir = GetUndertaleDirectory();
                if (gameDir == null)
                {
                    string err = "* Undertale folder not found. Please install Undertale on Steam.";
                    Logger.Log($"[GameSync] {err}");
                    onProgress(err);
                    return false;
                }

                string dataWinPath = Path.Combine(gameDir, "data.win");
                string roleName = isHost ? "Host (Player 1)" : "Client (Player 2)";

                Logger.Log($"[GameSync] Verifying data.win for {roleName}...");
                onProgress($"* Verifying game data for {roleName}...");

                if (File.Exists(dataWinPath))
                {
                    string localHash = ComputeSha256(dataWinPath);
                    Logger.Log($"[GameSync] Local hash:    {localHash}");
                    Logger.Log($"[GameSync] Expected hash: {UnifiedDataHash}");

                    if (string.Equals(localHash, UnifiedDataHash, StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Log($"[GameSync] Hash verified for {roleName} — 100% up to date.");
                        onProgress($"* Game data verified — 100% up to date!");
                        return true;
                    }
                }

                // Hash differs or missing — download pre-patched data.win package
                Logger.Log($"[GameSync] Hash mismatch or missing. Downloading verified game data package (35 MB)...");
                onProgress($"* Synchronizing verified game data (35 MB)...");
                string tempDir = Path.Combine(Path.GetTempPath(), "BnPDataSync");
                Directory.CreateDirectory(tempDir);
                string zipPath = Path.Combine(tempDir, "data_unified.win.zip");

                try
                {
                    using var http = new HttpClient();
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("BnPRelay-GameSync");
                    byte[] zipBytes = await http.GetByteArrayAsync(UnifiedDataUrl);
                    await File.WriteAllBytesAsync(zipPath, zipBytes);
                    Logger.Log($"[GameSync] Downloaded {zipBytes.Length} bytes.");

                    onProgress("* Extracting and applying verified game data...");
                    string extractDir = Path.Combine(tempDir, "extracted");
                    if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
                    ZipFile.ExtractToDirectory(zipPath, extractDir);

                    string extractedWin = Path.Combine(extractDir, "data_unified.win");
                    if (!File.Exists(extractedWin))
                    {
                        string fallback = Path.Combine(extractDir, "data.win");
                        if (File.Exists(fallback)) extractedWin = fallback;
                    }

                    if (File.Exists(extractedWin))
                    {
                        // Backup original if not already backed up
                        string backupPath = Path.Combine(gameDir, "data.win.original_backup");
                        if (!File.Exists(backupPath) && File.Exists(dataWinPath))
                            File.Copy(dataWinPath, backupPath, false);

                        File.Copy(extractedWin, dataWinPath, true);
                        Logger.Log($"[GameSync] Successfully copied {extractedWin} to {dataWinPath}.");
                        onProgress($"* Successfully synchronized game data!");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("GameIntegrityChecker", ex);
                    onProgress($"* Game data sync warning: {ex.Message}");
                }

                return false;
            });
        }
    }
}
