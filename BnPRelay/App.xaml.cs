using System;
using System.Windows;
using System.Windows.Input;

namespace BnPRelay
{
    /// <summary>
    /// App entry point. Handles the bnptogether:// deep link if launched from an invite URL.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>ZeroTier IP extracted from bnptogether:// deep link, if applicable.</summary>
        public static string? DeepLinkHostIp { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Logger.Log("=== BnPRelay Started ===");
            Logger.Log($"Operating System: {Environment.OSVersion}");
            Logger.Log($"Base Directory: {AppDomain.CurrentDomain.BaseDirectory}");

            AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
            {
                if (ev.ExceptionObject is Exception ex)
                    Logger.LogError("AppDomain UnhandledException", ex);
            };

            DispatcherUnhandledException += (s, ev) =>
            {
                Logger.LogError("DispatcherUnhandledException", ev.Exception);
            };

            // Parse deep link: bnptogether://10.147.17.5
            if (e.Args.Length > 0)
            {
                string arg = e.Args[0];
                Logger.Log($"Received launch argument: {arg}");
                if (arg.StartsWith("bnptogether://", StringComparison.OrdinalIgnoreCase))
                {
                    DeepLinkHostIp = arg["bnptogether://".Length..].Trim('/');
                    Logger.Log($"Extracted DeepLink Host IP: {DeepLinkHostIp}");
                }
            }
        }
    }
}
