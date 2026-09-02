using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace BnPRelay.Setup
{
    /// <summary>
    /// Checks GitHub Releases API for new updates and downloads the latest installer.
    /// </summary>
    public static class UpdateChecker
    {
        private const string RepoApiUrl = "https://api.github.com/repos/yahyazawadi/BnPs-together-online/releases/latest";
        public const string CurrentVersion = "v1.2.9";

        public record ReleaseInfo(string TagName, string DownloadUrl, string Body);

        public static async Task<ReleaseInfo?> CheckForUpdatesAsync()
        {
            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.UserAgent.ParseAdd("BnPRelay-Updater");
                var response = await http.GetStringAsync(RepoApiUrl);
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                string tagName = root.GetProperty("tag_name").GetString() ?? "";
                string body = root.GetProperty("body").GetString() ?? "";
                string downloadUrl = "";

                if (root.TryGetProperty("assets", out var assets) && assets.GetArrayLength() > 0)
                {
                    downloadUrl = assets[0].GetProperty("browser_download_url").GetString() ?? "";
                }

                if (string.IsNullOrEmpty(downloadUrl))
                {
                    downloadUrl = root.GetProperty("html_url").GetString() ?? "";
                }

                if (!string.IsNullOrEmpty(tagName) && tagName != CurrentVersion)
                {
                    return new ReleaseInfo(tagName, downloadUrl, body);
                }
            }
            catch { }
            return null;
        }

        public static void OpenDownloadPage(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch { }
        }
    }
}
