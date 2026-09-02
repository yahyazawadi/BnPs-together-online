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
        public const string HostDataHash = "FB0109E4D1D6165D9ECAB56274AB23B712FA40B57D52DA470F018437FDCD9283";
        public const string ClientDataHash = "1A06974EF26A7AADBB7310114809398C3D916B1CFF1AB305DE874F387100D0E5";

        private const string HostDataUrl = "https://github.com/yahyazawadi/BnPs-together-online/releases/download/v1.2.4/data_host.win.zip";
        private const string ClientDataUrl = "https://github.com/yahyazawadi/BnPs-together-online/releases/download/v1.2.4/data_client.win.zip";

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
        /// Checks if the local data.win matches the expected role hash.
        /// If already valid, finishes instantly without downloading.
        /// If different or missing, downloads and swaps the verified pre-patched file.
        /// </summary>
        public static async Task<bool> EnsureGameFilesReadyAsync(bool isHost, Action<string> onProgress)
        {
            string? gameDir = GetUndertaleDirectory();
            if (gameDir == null)
            {
                onProgress("* Undertale folder not found. Please install Undertale on Steam.");
                return false;
            }

            string dataWinPath = Path.Combine(gameDir, "data.win");
            string expectedHash = isHost ? HostDataHash : ClientDataHash;
            string roleName = isHost ? "Host (Player 1)" : "Client (Player 2)";

            onProgress($"* Verifying game data for {roleName}...");

            if (File.Exists(dataWinPath))
            {
                string localHash = await Task.Run(() => ComputeSha256(dataWinPath));
                if (string.Equals(localHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    onProgress($"* Game data verified for {roleName} — 100% up to date!");
                    return true;
                }
            }

            // Hash differs or missing — download pre-patched data.win package
            onProgress($"* Updating {roleName} data.win (35 MB)...");
            string downloadUrl = isHost ? HostDataUrl : ClientDataUrl;
            string tempDir = Path.Combine(Path.GetTempPath(), "BnPDataSync");
            Directory.CreateDirectory(tempDir);
            string zipPath = Path.Combine(tempDir, isHost ? "data_host.win.zip" : "data_client.win.zip");

            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.UserAgent.ParseAdd("BnPRelay-GameSync");
                byte[] zipBytes = await http.GetByteArrayAsync(downloadUrl);
                await File.WriteAllBytesAsync(zipPath, zipBytes);

                onProgress("* Extracting and applying verified game data...");
                string extractDir = Path.Combine(tempDir, "extracted");
                if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
                ZipFile.ExtractToDirectory(zipPath, extractDir);

                string extractedWin = Path.Combine(extractDir, isHost ? "data_host.win" : "data_client.win");
                if (!File.Exists(extractedWin))
                {
                    // Check if extracted as data.win
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
                    onProgress($"* Successfully synchronized {roleName} data.win!");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("GameIntegrityChecker", ex);
                onProgress($"* Game data sync warning: {ex.Message}");
            }

            return false;
        }
    }
}
