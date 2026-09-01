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

            // Parse deep link: bnptogether://10.147.17.5
            if (e.Args.Length > 0)
            {
                string arg = e.Args[0];
                if (arg.StartsWith("bnptogether://", StringComparison.OrdinalIgnoreCase))
                    DeepLinkHostIp = arg["bnptogether://".Length..].Trim('/');
            }
        }
    }
}
