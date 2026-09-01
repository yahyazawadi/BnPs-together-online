using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace BnPRelay.Setup
{
    /// <summary>
    /// Configures Windows Firewall rules and registers the bnptogether:// URL protocol.
    /// </summary>
    public static class SystemConfigHelper
    {
        private const string ProtocolName = "bnptogether";

        /// <summary>
        /// Registers the bnptogether:// URI handler in the Windows registry
        /// so clicking invite links opens BnPRelay.exe directly.
        /// </summary>
        public static void RegisterProtocol(string exePath)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProtocolName}");
                key.SetValue("", "URL:BnP Together ONLINE Protocol");
                key.SetValue("URL Protocol", "");

                using var defaultIcon = key.CreateSubKey("DefaultIcon");
                defaultIcon.SetValue("", $"\"{exePath}\",0");

                using var command = key.CreateSubKey(@"shell\open\command");
                command.SetValue("", $"\"{exePath}\" \"%1\"");
            }
            catch { }
        }

        /// <summary>
        /// Silently adds inbound/outbound Windows Firewall rules for BnPRelay.exe.
        /// </summary>
        public static void AddFirewallRules(string exePath)
        {
            try
            {
                var psiIn = new ProcessStartInfo("netsh", 
                    $"advfirewall firewall add rule name=\"BnP Together ONLINE\" dir=in action=allow program=\"{exePath}\" enable=yes")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psiIn)?.WaitForExit(3000);

                var psiOut = new ProcessStartInfo("netsh", 
                    $"advfirewall firewall add rule name=\"BnP Together ONLINE\" dir=out action=allow program=\"{exePath}\" enable=yes")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psiOut)?.WaitForExit(3000);
            }
            catch { }
        }
    }
}
