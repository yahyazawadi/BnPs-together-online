# 🎮 BnP Together ONLINE — Complete Technical & Architectural Documentation

---

## 1. Project Identity & Metadata
- **Project Name:** BnP Together ONLINE (Bits & Pieces: Undertale Together Online Relay)
- **Primary Goal:** Enable seamless online peer-to-peer 2-player co-op for the *Undertale: Bits & Pieces* mod with zero complex port forwarding, real-time input replication, save synchronization, and RNG battle turn synchronization.
- **Repository:** `https://github.com/yahyazawadi/BnPs-together-online.git` (`main` branch)
- **Local Workspace Root:** `C:\Users\CLICK\.gemini\antigravity-ide\scratch\BnPs-together-online`
- **Primary Build Artifact:** `C:\Users\CLICK\.gemini\antigravity-ide\scratch\BnPs-together-online\Output\BnP_Together_ONLINE_Setup.exe`

---

## 2. Directory Structure & Key Files

```
BnPs-together-online/
├── BnPRelay/                          # Core WPF / .NET 8 Application
│   ├── BnPRelay.csproj                # Project config (SingleFile + embedded native DLLs)
│   ├── App.xaml / App.xaml.cs         # App lifecycle, AppUserModelID (Taskbar icon fix)
│   ├── Program.cs                     # STA thread entry point
│   ├── Network/
│   │   ├── HostSession.cs             # TCP Server listener on port 7777 with reconnect loop
│   │   ├── ClientSession.cs           # TCP Client connecting to host with backoff & error diagnosis
│   │   ├── PacketFramer.cs            # Length-prefixed framing for binary packet stream
│   │   └── PacketType.cs              # Enum for Inputs, SaveFiles, TurnSeeds, Heartbeats, Pause
│   ├── Memory/
│   │   ├── MemoryManager.cs           # Native Windows ReadProcessMemory/WriteProcessMemory
│   │   └── GameAddresses.cs           # Static pointers & offsets for Undertale GameMaker memory
│   ├── Sync/
│   │   ├── TurnSyncBarrier.cs         # Turn-based lockstep RNG seed synchronization for battles
│   │   ├── SaveFileMirror.cs          # %localappdata%/UNDERTALE save file replication
│   │   └── RoomPositionSync.cs        # Room transitions & player position coordinate sync
│   ├── Input/
│   │   ├── GlobalKeyboardHook.cs      # Low-level Windows WH_KEYBOARD_LL keyboard capture
│   │   ├── VirtualInputInjector.cs    # Windows SendInput API for injecting remote player actions
│   │   └── InputBitmask.cs            # 1-byte compact bitmask for Z, X, C, Up, Down, Left, Right
│   ├── Setup/
│   │   └── ZeroTierManager.cs         # ZeroTier silent installer, UAC network joiner, Firewall rules
│   └── UI/
│       ├── MainWindow.xaml / .cs      # Retro Undertale-styled main menu, host/join flows, overlay
│       ├── RestoreSaveWindow.xaml     # Save backup and restoration interface
│       └── Assets/                    # Dual-heart icon, Determination fonts, background art
├── Setup/
│   └── Installer.iss                  # Inno Setup script (Self-purging, clean reset, self-nuking)
├── Publish/                           # Single-file published binary output (.exe + .pdb)
└── Output/                            # Final compiled installer executable
```

---

## 3. Complete Network Protocol Specification

### A. Transport Layer
- **Protocol:** TCP over Virtual P2P Adapter (ZeroTier / Tailscale)
- **Default Port:** `7777`
- **Firewall Integration:** Auto-registered in Windows Firewall via `netsh advfirewall` on app startup.

### B. Packet Structure
Each packet sent over TCP is framed with a 3-byte header:
1. **Length (2 bytes, Big-Endian):** Size of Payload + 1 byte (Type).
2. **Packet Type (1 byte):**
   - `0x01 (Input)`: 1-byte bitmask of controller/keyboard buttons.
   - `0x02 (TurnSeed)`: 8-byte RNG seed + 1-byte Turn Index for battle consistency.
   - `0x03 (AttackGo)`: Battle timing synchronization signal.
   - `0x04 (SaveFile)`: Length-prefixed file name + raw save file byte array (`file0`, `file8`, `file9`, `undertale.ini`).
   - `0x05 (Pause)`: State broadcast when a player loses connection to freeze gameplay safely.
   - `0x06 (Resume)`: State broadcast when connection is re-established.
   - `0x07 (Heartbeat)`: Ping/pong latency measurement payload.

### C. ZeroTier Joining Architecture
- ZeroTier CLI command: `zerotier-one_x64.exe -q join <16-character-network-id>`
- Privilege Requirement: Executed via elevated PowerShell (`-Verb RunAs`) to permit reading `C:\ProgramData\ZeroTier\One\authtoken.secret`.
- Adapter Resolution: `GetLocalIp()` scans `NetworkInterface.GetAllNetworkInterfaces()` and prioritizes adapters containing `"ZeroTier"` to eliminate LAN (`192.168.x.x`) and WSL (`172.x.x.x`) mismatches.

---

## 4. Installer Lifecycle & Bulletproofing (`Installer.iss`)

1. **Conflict Avoidance:** Removed `SetupMutex` to eliminate `"Setup is currently running"` false-positives.
2. **Process Termination:** Kills existing `BnPRelay.exe` processes cleanly before overwrite.
3. **Workspace Purging (`CleanLegacyInstallerFiles`):** Automatically searches and purges older duplicate setup files from:
   - User `Downloads` directory (`*BnP*Together*ONLINE*Setup*.exe`).
   - `Downloads\Telegram Desktop`.
   - User `Desktop`.
   - Setup current folder.
4. **Self-Destruction (`DeleteSelfInstaller`):** Spawns an asynchronous ping-delayed `cmd.exe /c del /f /q` process upon setup finish to automatically delete the installer `.exe` from the host computer.
5. **Fresh Reset Routine (`OnUninstallClick`):** Completely purges installation folder, `%localappdata%\BnPTogether`, desktop shortcuts, and registry uninstall keys.

---

## 5. Undertale Game & Mod Architecture Handover

### A. Current Status
- Both Host and Client connect successfully over the P2P network.
- Game launcher locates `UNDERTALE.exe` across default Steam libraries (`C:\Program Files (x86)\Steam\steamapps\common\Undertale\UNDERTALE.exe`) or falls back to Steam App ID `391540`.

### B. Immediate Target for Next Conversation (GameMaker Error)
When launching the game, GameMaker outputs:
```
Code Error
___________________________________________
Error on load
Unable to find function switch_controller_vibration_permitted
```

#### Diagnostic Breakdown:
1. **Origin:** Undertale Bits and Pieces uses custom GameMaker bytecode modified from the console (Nintendo Switch/PS4) or PC releases.
2. **Root Cause:** GameMaker runner `UNDERTALE.exe` is missing the native external function stub `switch_controller_vibration_permitted`, which is called during the mod's initial boot script (`gml_Script_switch_controller_vibration_permitted` or room init).
3. **Action Items for New Chat:**
   - Check if the runner requires the matching Bits & Pieces GameMaker executable / runner DLLs (`YYC` or `VM`).
   - Provide a dummy script or replace the missing vibration check in `data.win` / mod package.
   - Verify the game version compatibility between Host and Client to ensure identical bytecode execution.
