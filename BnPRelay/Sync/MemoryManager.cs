using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace BnPRelay.Sync
{
    /// <summary>
    /// Manages the connection to the running Undertale process.
    /// Provides:
    ///   1. HWND detection (for key injection targeting)
    ///   2. ReadProcessMemory / WriteProcessMemory access
    ///   3. RNG seed writing — writes the synchronized seed into the GameMaker
    ///      runner's random number state BEFORE a battle turn's bullets spawn.
    ///
    /// GameMaker Studio 2 stores its global RNG state as two 32-bit integers
    /// (seed + state) in the runner heap. We locate them by scanning for the
    /// known seed value that both instances agreed on at session start.
    /// </summary>
    public class MemoryManager : IDisposable
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr h);

        [DllImport("kernel32.dll")]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBase,
            byte[] buf, int size, out int read);

        [DllImport("kernel32.dll")]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBase,
            byte[] buf, int size, out int written);

        [DllImport("kernel32.dll")]
        private static extern bool VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress,
            out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORY_BASIC_INFORMATION
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint   AllocationProtect;
            public IntPtr RegionSize;
            public uint   State;
            public uint   Protect;
            public uint   Type;
        }

        private const uint PROCESS_ALL_ACCESS = 0x1F0FFF;
        private const uint MEM_COMMIT  = 0x1000;
        private const uint PAGE_READWRITE = 0x04;

        private IntPtr _hProcess = IntPtr.Zero;
        private IntPtr _rngSeedAddress = IntPtr.Zero;
        private Process? _gameProcess;

        public bool IsAttached => _hProcess != IntPtr.Zero;

        /// <summary>
        /// Finds the Undertale process and opens a handle for memory access.
        /// Must be called after the game is already running.
        /// Returns true on success.
        /// </summary>
        public bool Attach()
        {
            var procs = Process.GetProcessesByName("UNDERTALE");
            if (procs.Length == 0) procs = Process.GetProcessesByName("UNDERTALEBNP");
            if (procs.Length == 0) return false;

            _gameProcess = procs[0];
            _hProcess = OpenProcess(PROCESS_ALL_ACCESS, false, _gameProcess.Id);
            return _hProcess != IntPtr.Zero;
        }

        /// <summary>
        /// Scans game memory for the current RNG seed value and caches its address.
        /// Call this once right after launch with a known seed to calibrate the address.
        /// </summary>
        public bool FindRngAddress(int knownSeed)
        {
            if (!IsAttached) return false;

            byte[] needle = BitConverter.GetBytes(knownSeed);
            IntPtr address = IntPtr.Zero;

            while (VirtualQueryEx(_hProcess, address, out var mbi, (uint)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()))
            {
                // Only search committed, readable/writable memory regions
                if (mbi.State == MEM_COMMIT && mbi.Protect == PAGE_READWRITE)
                {
                    long regionSize = mbi.RegionSize.ToInt64();
                    if (regionSize > 0 && regionSize < 256 * 1024 * 1024) // skip absurdly large regions
                    {
                        byte[] buffer = new byte[regionSize];
                        if (ReadProcessMemory(_hProcess, mbi.BaseAddress, buffer, buffer.Length, out int read) && read > 4)
                        {
                            int offset = FindPattern(buffer, needle, read);
                            if (offset >= 0)
                            {
                                _rngSeedAddress = IntPtr.Add(mbi.BaseAddress, offset);
                                return true;
                            }
                        }
                    }
                }

                // Advance to next region
                long next = address.ToInt64() + mbi.RegionSize.ToInt64();
                if (next <= address.ToInt64()) break;
                address = new IntPtr(next);
            }

            return false;
        }

        /// <summary>
        /// Overwrites the RNG seed in game memory to synchronize battle patterns.
        /// Called by TurnSyncBarrier just before the attack animation begins.
        /// </summary>
        public bool WriteRngSeed(int seed)
        {
            if (!IsAttached || _rngSeedAddress == IntPtr.Zero) return false;
            byte[] data = BitConverter.GetBytes(seed);
            return WriteProcessMemory(_hProcess, _rngSeedAddress, data, data.Length, out _);
        }

        /// <summary>Reads the current RNG seed from game memory (used by Host to broadcast).</summary>
        public int ReadRngSeed()
        {
            if (!IsAttached || _rngSeedAddress == IntPtr.Zero) return 0;
            byte[] buf = new byte[4];
            ReadProcessMemory(_hProcess, _rngSeedAddress, buf, 4, out _);
            return BitConverter.ToInt32(buf, 0);
        }

        private static int FindPattern(byte[] data, byte[] pattern, int length)
        {
            int end = length - pattern.Length;
            for (int i = 0; i < end; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (data[i + j] != pattern[j]) { match = false; break; }
                }
                if (match) return i;
            }
            return -1;
        }

        public void Dispose()
        {
            if (_hProcess != IntPtr.Zero)
            {
                CloseHandle(_hProcess);
                _hProcess = IntPtr.Zero;
            }
        }
    }
}
