using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace BnPRelay.Setup
{
    /// <summary>
    /// Manages ZeroTier installation detection, automated silent MSI download,
    /// and CLI connection for P2P multiplayer.
    /// </summary>
    public static class ZeroTierManager
    {
        // Public ZeroTier Network for BnP Together ONLINE (Auto-joined with No Authorization required)
        public const string DefaultPublicNetworkId = "a09acf0207fae019";

        private const string ZeroTierMsiUrl = "https://download.zerotier.com/dist/ZeroTier%20One.msi";
        private const string ZeroTierExePath = @"C:\Program Files (x86)\ZeroTier\One\zerotier-one_x64.exe";
        private const string ZeroTierCliPath = @"C:\Program Files (x86)\ZeroTier\One\zerotier-cli.bat";

        /// <summary>Checks if ZeroTier service is installed on the local system.</summary>
        public static bool IsInstalled()
        {
            return File.Exists(ZeroTierExePath) || 
                   File.Exists(@"C:\Program Files\ZeroTier\One\zerotier-one_x64.exe") ||
                   File.Exists(@"C:\ProgramData\ZeroTier\One\zerotier-one_x64.exe") ||
                   Directory.Exists(@"C:\ProgramData\ZeroTier\One");
        }

        /// <summary>Downloads and silently installs ZeroTier One via msiexec.</summary>
        public static async Task<bool> InstallSilentlyAsync(Action<string>? progressCallback = null)
        {
            try
            {
                progressCallback?.Invoke("* Downloading ZeroTier P2P Network Service...");
                string tempMsi = Path.Combine(Path.GetTempPath(), "ZeroTierOne_Setup.msi");

                using (var http = new HttpClient())
                using (var s = await http.GetStreamAsync(ZeroTierMsiUrl))
                using (var fs = new FileStream(tempMsi, FileMode.Create))
                {
                    await s.CopyToAsync(fs);
                }

                progressCallback?.Invoke("* Installing ZeroTier silently...");
                var psi = new ProcessStartInfo("msiexec.exe", $"/i \"{tempMsi}\" /qn /norestart")
                {
                    UseShellExecute = true,
                    Verb = "runas" // Elevate UAC
                };

                var proc = Process.Start(psi);
                if (proc != null)
                {
                    await proc.WaitForExitAsync();
                    File.Delete(tempMsi);
                    bool ok = proc.ExitCode == 0;
                    if (ok)
                    {
                        LaunchZeroTierUi();
                    }
                    return ok;
                }
            }
            catch (Exception ex)
            {
                progressCallback?.Invoke($"[!] ZeroTier install error: {ex.Message}");
            }
            return false;
        }

        /// <summary>Launches the ZeroTier tray / desktop UI.</summary>
        public static void LaunchZeroTierUi()
        {
            string[] uiPaths = {
                @"C:\Program Files (x86)\ZeroTier\One\zerotier_desktop_ui.exe",
                @"C:\Program Files\ZeroTier\One\zerotier_desktop_ui.exe",
                @"C:\Program Files (x86)\ZeroTier\One\ZeroTier One.exe",
                @"C:\Program Files\ZeroTier\One\ZeroTier One.exe"
            };

            foreach (var path in uiPaths)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                        break;
                    }
                    catch { }
                }
            }
        }

        /// <summary>Joins a given ZeroTier network ID via CLI.</summary>
        public static bool JoinNetwork(string networkId)
        {
            try
            {
                string[] possibleClis = {
                    @"C:\Program Files (x86)\ZeroTier\One\zerotier-cli.bat",
                    @"C:\Program Files\ZeroTier\One\zerotier-cli.bat",
                    @"C:\ProgramData\ZeroTier\One\zerotier-one_x64.exe"
                };

                foreach (var cli in possibleClis)
                {
                    if (File.Exists(cli))
                    {
                        string args = cli.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                            ? $"-q join {networkId}"
                            : $"join {networkId}";

                        var psi = new ProcessStartInfo(cli, args)
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        var p = Process.Start(psi);
                        p?.WaitForExit(5000);
                        if (p?.ExitCode == 0) return true;
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>Automatically joins the built-in public ZeroTier network in background.</summary>
        public static void AutoJoinDefaultNetwork()
        {
            _ = Task.Run(() =>
            {
                try
                {
                    JoinNetwork(DefaultPublicNetworkId);
                }
                catch { }
            });
        }

        /// <summary>Ensures port 7777 and BnPRelay.exe are permitted through Windows Firewall.</summary>
        public static void EnsureFirewallRules()
        {
            try
            {
                string exe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                string script = $"netsh advfirewall firewall add rule name=\"BnP Together ONLINE Port\" dir=in action=allow protocol=TCP localport=7777; " +
                                $"netsh advfirewall firewall add rule name=\"BnP Together ONLINE App\" dir=in action=allow program=\"{exe}\" enable=yes";

                var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -WindowStyle Hidden -Command \"{script}\"")
                {
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Process.Start(psi);
            }
            catch { }
        }
    }
}
