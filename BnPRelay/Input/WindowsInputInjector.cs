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

        // P2 virtual key codes
        private const int VK_W = 0x57;
        private const int VK_A = 0x41;
        private const int VK_S = 0x53;
        private const int VK_D = 0x44;
        private const int VK_F = 0x46;
        private const int VK_G = 0x47;

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

            // Try window title first
            IntPtr hwnd = FindWindow(null, "UNDERTALE");
            if (hwnd == IntPtr.Zero)
            {
                // Fallback: search by process name
                var procs = Process.GetProcessesByName("UNDERTALE");
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
        public void InjectDelta(InputBitmask newMask)
        {
            if (_hwnd == IntPtr.Zero || !_enabled) return;

            ApplyKey(VK_W, _lastMask.Up,      newMask.Up);
            ApplyKey(VK_A, _lastMask.Left,    newMask.Left);
            ApplyKey(VK_S, _lastMask.Down,    newMask.Down);
            ApplyKey(VK_D, _lastMask.Right,   newMask.Right);
            ApplyKey(VK_F, _lastMask.Confirm, newMask.Confirm);
            ApplyKey(VK_G, _lastMask.Cancel,  newMask.Cancel);

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
            foreach (var vk in new[] { VK_W, VK_A, VK_S, VK_D, VK_F, VK_G })
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
