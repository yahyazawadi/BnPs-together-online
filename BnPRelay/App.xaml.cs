using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;

namespace BnPRelay
{
    /// <summary>
    /// App entry point. Handles single-instance enforcement and bnptogether:// deep links.
    /// </summary>
    public partial class App : Application
    {
        [DllImport("shell32.dll", SetLastError = true)]
        private static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

        private const string AppUserModelId = "BnPTogether.Online.Relay.1.0";
        private const string AppMutexName = "BnPTogether_SingleInstance_Mutex";
        private static System.Threading.Mutex? _appMutex;

        /// <summary>ZeroTier IP extracted from bnptogether:// deep link, if applicable.</summary>
        public static string? DeepLinkHostIp { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            // ─── SINGLE INSTANCE ENFORCEMENT ───
            _appMutex = new System.Threading.Mutex(true, AppMutexName, out bool createdNew);
            if (!createdNew)
            {
                // Bring existing window to the front
                IntPtr existingHwnd = FindWindow(null, "BnP Together ONLINE");
                if (existingHwnd != IntPtr.Zero)
                {
                    ShowWindow(existingHwnd, 9); // SW_RESTORE
                    SetForegroundWindow(existingHwnd);
                }
                Current.Shutdown();
                return;
            }

            base.OnStartup(e);

            try
            {
                SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
            }
            catch { }

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
