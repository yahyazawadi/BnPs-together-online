using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using BnPRelay.Sync;

namespace BnPRelay
{
    public partial class MainWindow : Window
    {
        private HostSession?   _host;
        private ClientSession? _client;
        private readonly WindowsInputInjector _injector = new();
        private readonly SaveFileMirror _saveMirror = new();
        private readonly LowLevelKeyboardHook _keyHook = new();
        private readonly MemoryManager _mem = new();
        private TurnSyncBarrier? _turnSync;
        private InputBitmask _currentMask;
        private bool _isHost;

        public MainWindow()
        {
            InitializeComponent();

            try
            {
                var iconUri = new Uri("pack://application:,,,/UI/Assets/heart.ico", UriKind.Absolute);
                Icon = System.Windows.Media.Imaging.BitmapFrame.Create(iconUri);
            }
            catch { }

            _injector.OnWindowFound += hwnd =>
                Dispatcher.Invoke(() => SetStatus("Undertale window found — ready to relay!"));

            _saveMirror.SaveChanged += async (fileName, data) =>
            {
                if (_isHost && _host != null)
                    await _host.SendSaveFileAsync(fileName, data);
            };
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Install the system-wide keyboard hook
            _keyHook.KeyStateChanged += OnKeyStateChanged;
            _keyHook.Install();

            // Check if ZeroTier is installed for P2P multiplayer
            if (!BnPRelay.Setup.ZeroTierManager.IsInstalled())
            {
                var res = MessageBox.Show(
                    "* ZeroTier P2P Network Service is required for online multiplayer.\n\nWould you like to install ZeroTier automatically now?",
                    "BnP Together ONLINE",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (res == MessageBoxResult.Yes)
                {
                    SetStatus("* Installing ZeroTier P2P Service in background...");
                    _ = Task.Run(async () =>
                    {
                        bool ok = await BnPRelay.Setup.ZeroTierManager.InstallSilentlyAsync(msg => Dispatcher.Invoke(() => SetStatus(msg)));
                        Dispatcher.Invoke(() =>
                        {
                            if (ok)
                                SetStatus("* ZeroTier installed successfully!");
                            else
                                SetStatus("* ZeroTier setup completed. Please join your friend's network.");
                        });
                    });
                }
            }

            // If launched via bnptogether:// deep link, auto-fill IP and start join
            if (App.DeepLinkHostIp is { } ip)
            {
                TxtHostIp.Text = ip;
                ShowJoinInput();
                BeginJoin(ip);
            }
        }

        /// <summary>
        /// Fires on every captured keypress. Converts to bitmask delta and
        /// sends over the network. The game still receives the keystroke directly.
        /// </summary>
        private async void OnKeyStateChanged(Key key, bool isDown)
        {
            // Only relay when injection is enabled (after both clicked LAUNCH)
            if (!_injector.IsAttached) return;

            var newMask = InputNormalizer.FromKey(key, _currentMask, isDown);
            if (newMask.Value == _currentMask.Value) return;  // no change, skip
            _currentMask = newMask;

            try
            {
                if (_isHost && _host != null)
                    await _host.SendInputAsync(newMask);      // Host: relay P1 keys to client
                else if (!_isHost && _client != null)
                    await _client.SendInputAsync(newMask);    // Client: relay P2 keys to host
            }
            catch { /* session closed, ignore */ }
        }

        // ─── HOST ───────────────────────────────────────────────────────────────

        private async void BtnHost_Click(object sender, RoutedEventArgs e)
        {
            _isHost = true;
            PanelConnect.Visibility  = Visibility.Collapsed;
            PanelHosting.Visibility  = Visibility.Visible;

            // Display local ZeroTier IP (first non-loopback)
            string ip = GetLocalIp();
            TxtHostIpDisplay.Text = $"* Your IP: {ip}";
            SetStatus("Listening for connection...");

            _host = new HostSession();
            _host.StatusChanged         += s => Dispatcher.Invoke(() => SetStatus(s));
            _host.LatencyUpdated        += ms => Dispatcher.Invoke(() => TxtLatency.Text = $"* Ping: {ms}ms");
            _host.RemoteInputReceived   += mask => _injector.InjectDelta(mask);
            _host.ClientConnected       += () => Dispatcher.Invoke(OnConnected);
            _host.ClientDisconnected    += () => Dispatcher.Invoke(OnDisconnected);

            // Wire TurnSyncBarrier for host
            _turnSync = new TurnSyncBarrier(_mem,
                (seed, idx) => _host.SendTurnSeedAsync(seed, idx),
                () => Task.CompletedTask /* AttackGo sent internally by HostSession */);

            await _host.StartAsync();
        }

        // ─── JOIN ───────────────────────────────────────────────────────────────

        private void BtnJoin_Click(object sender, RoutedEventArgs e) => ShowJoinInput();

        private void ShowJoinInput()
        {
            PanelJoinInput.Visibility = Visibility.Visible;
            TxtHostIp.Focus();
        }

        private void TxtHostIp_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (TxtIpPlaceholder != null)
                TxtIpPlaceholder.Visibility = string.IsNullOrEmpty(TxtHostIp.Text)
                    ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            string ip = TxtHostIp.Text.Trim();
            if (string.IsNullOrEmpty(ip)) { SetStatus("Please enter the host IP."); return; }
            BeginJoin(ip);
        }

        private void BeginJoin(string ip)
        {
            _isHost = false;
            PanelConnect.Visibility = Visibility.Collapsed;
            SetStatus($"Connecting to {ip}...");

            _client = new ClientSession(ip);
            _client.StatusChanged       += s => Dispatcher.Invoke(() => SetStatus(s));
            _client.LatencyUpdated      += ms => Dispatcher.Invoke(() => TxtLatency.Text = $"* Ping: {ms}ms");
            _client.RemoteInputReceived += mask => _injector.InjectDelta(mask);
            _client.SaveFileReceived    += (name, data) => SaveFileMirror.WriteSaveFile(name, data);
            _client.TurnSeedReceived    += (seed, idx) =>
            {
                _turnSync ??= new TurnSyncBarrier(_mem, (_, _) => Task.CompletedTask, () => Task.CompletedTask);
                _turnSync.OnTurnSeedReceived(seed, (byte)idx);
            };
            _client.AttackGoReceived    += idx => _turnSync?.OnAttackGoReceived(idx);
            _client.HostConnected       += () => Dispatcher.Invoke(OnConnected);
            _client.HostDisconnected    += () => Dispatcher.Invoke(OnDisconnected);

            _ = _client.ConnectAsync();
        }

        // ─── CONNECTED STATE ─────────────────────────────────────────────────────

        private void OnConnected()
        {
            PanelHosting.Visibility   = Visibility.Collapsed;
            PanelConnected.Visibility = Visibility.Visible;
            OverlayDisconnect.Visibility = Visibility.Collapsed;

            // Host sends save file immediately on connect
            if (_isHost)
            {
                foreach (var file in new[] { "file0", "file8", "file9", "undertale.ini" })
                {
                    var data = SaveFileMirror.ReadSaveFile(file);
                    if (data != null)
                        _ = _host!.SendSaveFileAsync(file, data);
                }
            }

            SetStatus("* Both players connected. Click LAUNCH GAME when ready!");
        }

        private void OnDisconnected()
        {
            OverlayDisconnect.Visibility = Visibility.Visible;
            _injector.Disable();
            TxtReconnecting.Text = "* Attempting to reconnect...";
        }

        // ─── BUTTONS ────────────────────────────────────────────────────────────

        private void BtnLaunch_Click(object sender, RoutedEventArgs e)
        {
            _injector.Enable();
            SetStatus("* Game launched! Relaying inputs...");
            BtnLaunch.IsEnabled = false;

            // Attach memory manager after game launches
            _ = Task.Run(async () =>
            {
                await Task.Delay(3000); // give game time to boot
                if (_mem.Attach())
                    Dispatcher.Invoke(() => SetStatus("* Relay active — memory attached!"));
                else
                    Dispatcher.Invoke(() => SetStatus("* Relay active (memory attach failed — RNG sync limited)"));
            });

            // Launch Undertale via Steam
            try { Process.Start(new ProcessStartInfo("steam://run/1252690") { UseShellExecute = true }); }
            catch { SetStatus("Could not launch Undertale via Steam. Launch it manually."); }
        }

        private void BtnCopyLink_Click(object sender, RoutedEventArgs e)
        {
            string ip = GetLocalIp();
            Clipboard.SetText($"bnptogether://{ip}");
            SetStatus($"* Invite link copied! Share it with your friend.");
        }

        private void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            var restoreWin = new RestoreSaveWindow();
            restoreWin.Owner = this;
            restoreWin.ShowDialog();
        }

        // ─── UTILITIES ───────────────────────────────────────────────────────────

        private static string GetLocalIp()
        {
            // Find first ZeroTier-like IP (10.x.x.x range) or fall back to any local IP
            foreach (var addr in System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName()))
                if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                    addr.ToString().StartsWith("10."))
                    return addr.ToString();
            return "127.0.0.1";
        }

        private void SetStatus(string msg) => TxtStatus.Text = msg;

        protected override void OnClosed(EventArgs e)
        {
            _keyHook.Dispose();
            _injector.Dispose();
            _saveMirror.Dispose();
            _mem.Dispose();
            _turnSync?.Dispose();
            _host?.Dispose();
            _client?.Dispose();
            base.OnClosed(e);
        }
    }
}
