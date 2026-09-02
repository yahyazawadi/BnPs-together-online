using System;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace BnPRelay
{
    /// <summary>
    /// System-wide low-level keyboard hook using WH_KEYBOARD_LL.
    /// Fires for every keypress regardless of which window is focused —
    /// meaning it works while the player is looking at the Undertale window.
    ///
    /// We DO NOT suppress keypresses (always call next hook), so the game
    /// still receives the keystrokes directly for local input while we
    /// simultaneously relay them over the network.
    /// </summary>
    public class LowLevelKeyboardHook : IDisposable
    {
        // Win32 hook type: Low Level Keyboard
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN     = 0x0100;
        private const int WM_KEYUP       = 0x0101;
        private const int WM_SYSKEYDOWN  = 0x0104;
        private const int WM_SYSKEYUP   = 0x0105;

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn,
            IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        private IntPtr _hookHandle;
        private readonly LowLevelKeyboardProc _proc;  // keep alive — GC can't collect delegates

        // Raised on key state change for keys we care about
        public event Action<Key, bool>? KeyStateChanged;

        public LowLevelKeyboardHook()
        {
            _proc = HookCallback;
        }

        public void Install()
        {
            using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
            using var curModule  = curProcess.MainModule!;
            _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _proc,
                GetModuleHandle(curModule.ModuleName), 0);

            if (_hookHandle == IntPtr.Zero)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),
                    "Failed to install keyboard hook. Try running as Administrator.");
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                try
                {
                    var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                    bool isDown = wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN;
                    bool isUp   = wParam == WM_KEYUP   || wParam == WM_SYSKEYUP;

                    if (isDown || isUp)
                    {
                        var key = KeyInterop.KeyFromVirtualKey((int)kb.vkCode);
                        if (IsRelayKey(key))
                        {
                            ThreadPool.QueueUserWorkItem(_ =>
                            {
                                try { KeyStateChanged?.Invoke(key, isDown); } catch { }
                            });
                        }
                    }
                }
                catch { }
            }
            // ALWAYS pass through immediately — never delay or block the OS message pump
            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        /// <summary>
        /// Keys we intercept for relay. Covers both Host (P1) and Client (P2) key sets.
        /// Host relays: Up/Down/Left/Right/Z/X/C (Arrow keys)
        /// Client relays: W/A/S/D/F/G (WASD)
        /// Both also accept the alternate set since WASD/Arrows map to same bitmask bits.
        /// </summary>
        private static bool IsRelayKey(Key key) => key switch
        {
            Key.Up or Key.Down or Key.Left or Key.Right => true,
            Key.Z  or Key.X  or Key.C                  => true,
            Key.W  or Key.A  or Key.S  or Key.D        => true,
            Key.F  or Key.G                             => true,
            Key.Return or Key.LeftShift or Key.RightShift => true,
            _ => false
        };

        public void Dispose()
        {
            if (_hookHandle != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }
        }
    }
}
