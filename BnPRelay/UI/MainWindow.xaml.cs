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

        private static readonly string ConfigPath =
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BnPTogether", "config.ini");

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Install the system-wide keyboard hook
            _keyHook.KeyStateChanged += OnKeyStateChanged;
            _keyHook.Install();

            // Load remembered configuration (IP & Network ID)
            LoadSavedConfig();

            // Ensure Windows Firewall permits incoming P2P connections on port 7777
            _ = Task.Run(() => BnPRelay.Setup.ZeroTierManager.EnsureFirewallRules());

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
            string raw = TxtHostIp.Text.Trim();
            if (string.IsNullOrEmpty(raw)) { SetStatus("Please enter the host IP."); return; }
            BeginJoin(raw);
        }

        private void BeginJoin(string rawIp)
        {
            // Sanitize input: Strip bnptogether://, http://, trailing slashes, and port if user pasted full URL
            string ip = rawIp.Trim();
            if (ip.StartsWith("bnptogether://", StringComparison.OrdinalIgnoreCase))
                ip = ip["bnptogether://".Length..];
            if (ip.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                ip = ip["http://".Length..];
            ip = ip.Trim().TrimEnd('/');
            if (ip.Contains(':'))
                ip = ip.Split(':')[0]; // keep only IP part

            TxtHostIp.Text = ip;
            if (ChkRememberIp.IsChecked == true)
                SaveConfigKey("LastHostIp", ip);

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
            _client.HostDisconnected    += () => Dispatcher.Invoke(() => OnDisconnected($"Connecting to {ip} failed"));
            _client.ConnectionFailed    += reason => Dispatcher.Invoke(() =>
            {
                TxtDisconnectTarget.Text = $"* Target IP: {ip}:7777";
                TxtDisconnectReason.Text = $"* Reason: {reason}";
            });

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

        private void OnDisconnected(string? reason = null)
        {
            OverlayDisconnect.Visibility = Visibility.Visible;
            _injector.Disable();
            if (!string.IsNullOrEmpty(reason))
                TxtDisconnectReason.Text = $"* {reason}";
            TxtReconnecting.Text = "* Attempting to reconnect...";
        }

        private void BtnCancelReconnect_Click(object sender, RoutedEventArgs e)
        {
            _client?.Dispose();
            _client = null;
            OverlayDisconnect.Visibility = Visibility.Collapsed;
            PanelConnected.Visibility = Visibility.Collapsed;
            PanelHosting.Visibility = Visibility.Collapsed;
            PanelConnect.Visibility = Visibility.Visible;
            PanelJoinInput.Visibility = Visibility.Visible;
            SetStatus("Connection cancelled. Enter host IP and try again.");
        }

        private void BtnJoinZeroTier_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show(
                "* Would you like to open the ZeroTier website (my.zerotier.com) to create or manage your network?\n\n(Click 'Yes' to open website, or 'No' to enter an existing 16-digit Network ID)",
                "ZeroTier Network Setup",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                try
                {
                    Process.Start(new ProcessStartInfo("https://my.zerotier.com/network") { UseShellExecute = true });
                }
                catch { }
            }
            else if (res == MessageBoxResult.No)
            {
                string networkId = Microsoft.VisualBasic.Interaction.InputBox(
                    "* Enter the 16-digit ZeroTier Network ID:",
                    "Join ZeroTier Network",
                    _lastNetworkId);

                if (!string.IsNullOrWhiteSpace(networkId))
                {
                    networkId = networkId.Trim();
                    _lastNetworkId = networkId;
                    SaveConfigKey("LastNetworkId", networkId);
                    SetStatus($"* Joining ZeroTier Network {networkId}...");
                    bool ok = BnPRelay.Setup.ZeroTierManager.JoinNetwork(networkId);
                    if (ok)
                    {
                        MessageBox.Show("* Joined ZeroTier network!\n\nCheck the box [✓] to authorize this device in your my.zerotier.com dashboard.",
                            "ZeroTier", MessageBoxButton.OK, MessageBoxImage.Information);
                        SetStatus("* Joined ZeroTier network! Waiting for authorization...");
                    }
                    else
                    {
                        SetStatus("* Failed to join ZeroTier network automatically.");
                    }
                }
            }
        }

        private string _lastNetworkId = "";

        private void LoadSavedConfig()
        {
            try
            {
                if (System.IO.File.Exists(ConfigPath))
                {
                    var lines = System.IO.File.ReadAllLines(ConfigPath);
                    foreach (var line in lines)
                    {
                        var parts = line.Split('=', 2);
                        if (parts.Length == 2)
                        {
                            string k = parts[0].Trim();
                            string v = parts[1].Trim();
                            if (k == "LastHostIp" && !string.IsNullOrEmpty(v))
                                TxtHostIp.Text = v;
                            else if (k == "LastNetworkId" && !string.IsNullOrEmpty(v))
                                _lastNetworkId = v;
                        }
                    }
                }
            }
            catch { }
        }

        private static void SaveConfigKey(string key, string value)
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(ConfigPath)!;
                if (!System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);

                var map = new System.Collections.Generic.Dictionary<string, string>();
                if (System.IO.File.Exists(ConfigPath))
                {
                    foreach (var line in System.IO.File.ReadAllLines(ConfigPath))
                    {
                        var parts = line.Split('=', 2);
                        if (parts.Length == 2)
                            map[parts[0].Trim()] = parts[1].Trim();
                    }
                }
                map[key] = value;

                var outLines = new System.Collections.Generic.List<string>();
                foreach (var kvp in map)
                    outLines.Add($"{kvp.Key}={kvp.Value}");
                System.IO.File.WriteAllLines(ConfigPath, outLines);
            }
            catch { }
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

            // Launch Undertale directly if found, otherwise via Steam AppID 391540
            string[] gamePaths = {
                @"C:\Program Files (x86)\Steam\steamapps\common\Undertale\UNDERTALE.exe",
                @"C:\Program Files\Steam\steamapps\common\Undertale\UNDERTALE.exe",
                @"D:\SteamLibrary\steamapps\common\Undertale\UNDERTALE.exe",
                @"E:\SteamLibrary\steamapps\common\Undertale\UNDERTALE.exe"
            };

            bool launched = false;
            foreach (var path in gamePaths)
            {
                if (System.IO.File.Exists(path))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(path)
                        {
                            WorkingDirectory = System.IO.Path.GetDirectoryName(path),
                            UseShellExecute = true
                        });
                        launched = true;
                        break;
                    }
                    catch { }
                }
            }

            if (!launched)
            {
                try
                {
                    // Undertale Steam AppID is 391540
                    Process.Start(new ProcessStartInfo("steam://run/391540") { UseShellExecute = true });
                }
                catch
                {
                    SetStatus("Please launch Undertale manually.");
                }
            }
        }

        private void BtnCopyLink_Click(object sender, RoutedEventArgs e)
        {
            string ip = GetLocalIp();
            string link = $"bnptogether://{ip}";

            try
            {
                // STA-safe Ole clipboard set
                System.Windows.Clipboard.SetDataObject(link, true);
                SetStatus($"* Invite link copied! ({link})");
            }
            catch
            {
                SetStatus($"* Invite Link: {link}");
            }
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
            try
            {
                // 1. Prioritize ZeroTier Virtual Network Adapter
                foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up &&
                        (ni.Description.Contains("ZeroTier", StringComparison.OrdinalIgnoreCase) ||
                         ni.Name.Contains("ZeroTier", StringComparison.OrdinalIgnoreCase)))
                    {
                        foreach (var ip in ni.GetIPProperties().UnicastAddresses)
                        {
                            if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                                !System.Net.IPAddress.IsLoopback(ip.Address))
                            {
                                return ip.Address.ToString();
                            }
                        }
                    }
                }

                // 2. Fall back to Tailscale (100.x.x.x) or other 10.x.x.x
                var addrs = System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName());
                foreach (var addr in addrs)
                {
                    if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        string s = addr.ToString();
                        if (s.StartsWith("10.") || s.StartsWith("100."))
                            return s;
                    }
                }

                // 3. Fall back to any non-loopback IPv4 (e.g. 192.168.x.x)
                foreach (var addr in addrs)
                {
                    if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                        !System.Net.IPAddress.IsLoopback(addr))
                        return addr.ToString();
                }
            }
            catch { }

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
