using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace BnPRelay
{
    /// <summary>
    /// Injects keystrokes directly into the Undertale window via PostMessage,
    /// targeting by HWND so the game window does not need to be in focus.
    /// Maps InputBitmask bits → the exact virtual key codes BnP Together expects for P2.
    ///   UP=W(0x57)  LEFT=A(0x41)  DOWN=S(0x53)  RIGHT=D(0x44)
    ///   CONFIRM=F(0x46)  CANCEL=G(0x47)
    /// </summary>
    public class WindowsInputInjector : IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP   = 0x0101;

        // P1 virtual key codes
        private const int VK_LEFT   = 0x25;
        private const int VK_UP     = 0x26;
        private const int VK_RIGHT  = 0x27;
        private const int VK_DOWN   = 0x28;
        private const int VK_Z      = 0x5A;
        private const int VK_X      = 0x58;
        private const int VK_C      = 0x43;

        // P2 virtual key codes
        private const int VK_W      = 0x57;
        private const int VK_A      = 0x41;
        private const int VK_S      = 0x53;
        private const int VK_D      = 0x44;
        private const int VK_F      = 0x46;
        private const int VK_G      = 0x47;

        private IntPtr _hwnd = IntPtr.Zero;
        private InputBitmask _lastMask;
        private readonly Timer _pollTimer;
        private volatile bool _enabled = false;

        public bool IsAttached => _hwnd != IntPtr.Zero;

        public WindowsInputInjector()
        {
            // Poll for the Undertale window every 500ms until found
            _pollTimer = new Timer(TryFindWindow, null, 0, 500);
        }

        private void TryFindWindow(object? _)
        {
            if (_hwnd != IntPtr.Zero) return;

            // Try window titles first
            string[] titles = { "UNDERTALE: Bits and Pieces", "UNDERTALE", "UNDERTALE " };
            IntPtr hwnd = IntPtr.Zero;
            foreach (var title in titles)
            {
                hwnd = FindWindow(null, title);
                if (hwnd != IntPtr.Zero) break;
            }

            if (hwnd == IntPtr.Zero)
            {
                // Fallback: search by process name (UNDERTALE or UNDERTALEBNP)
                var procs = Process.GetProcessesByName("UNDERTALE");
                if (procs.Length == 0) procs = Process.GetProcessesByName("UNDERTALEBNP");
                if (procs.Length > 0) hwnd = procs[0].MainWindowHandle;
            }

            if (hwnd != IntPtr.Zero)
            {
                _hwnd = hwnd;
                OnWindowFound?.Invoke(hwnd);
            }
        }

        public event Action<IntPtr>? OnWindowFound;

        /// <summary>Inject the full bitmask state — sends KEYDOWN/KEYUP for any changed bits.</summary>
        public void InjectDelta(InputBitmask newMask, bool isHost = true)
        {
            if (_hwnd == IntPtr.Zero || !_enabled) return;

            if (isHost)
            {
                // Host receives Client (P2) inputs -> Injects P2 WASD/F/G keys
                ApplyKey(VK_W, _lastMask.Up,      newMask.Up);
                ApplyKey(VK_A, _lastMask.Left,    newMask.Left);
                ApplyKey(VK_S, _lastMask.Down,    newMask.Down);
                ApplyKey(VK_D, _lastMask.Right,   newMask.Right);
                ApplyKey(VK_F, _lastMask.Confirm, newMask.Confirm);
                ApplyKey(VK_G, _lastMask.Cancel,  newMask.Cancel);
            }
            else
            {
                // Client receives Host (P1) inputs -> Injects P1 Arrow/Z/X keys
                ApplyKey(VK_UP,    _lastMask.Up,      newMask.Up);
                ApplyKey(VK_LEFT,  _lastMask.Left,    newMask.Left);
                ApplyKey(VK_DOWN,  _lastMask.Down,    newMask.Down);
                ApplyKey(VK_RIGHT, _lastMask.Right,   newMask.Right);
                ApplyKey(VK_Z,     _lastMask.Confirm, newMask.Confirm);
                ApplyKey(VK_X,     _lastMask.Cancel,  newMask.Cancel);
            }

            _lastMask = newMask;
        }

        private void ApplyKey(int vk, bool wasDown, bool isDown)
        {
            if (!wasDown && isDown)
                PostMessage(_hwnd, WM_KEYDOWN, (IntPtr)vk, IntPtr.Zero);
            else if (wasDown && !isDown)
                PostMessage(_hwnd, WM_KEYUP, (IntPtr)vk, IntPtr.Zero);
        }

        /// <summary>Call to allow injection (set after both players confirm READY).</summary>
        public void Enable()  => _enabled = true;
        /// <summary>Call to freeze all injection (disconnect, pause, etc.).</summary>
        public void Disable()
        {
            _enabled = false;
            // Release any keys that were held down
            ReleaseAll();
        }

        private void ReleaseAll()
        {
            if (_hwnd == IntPtr.Zero) return;
            foreach (var vk in new[] { VK_W, VK_A, VK_S, VK_D, VK_F, VK_G, VK_UP, VK_LEFT, VK_DOWN, VK_RIGHT, VK_Z, VK_X })
                PostMessage(_hwnd, WM_KEYUP, (IntPtr)vk, IntPtr.Zero);
            _lastMask = default;
        }

        public void Dispose()
        {
            _pollTimer.Dispose();
            ReleaseAll();
        }
    }
}
