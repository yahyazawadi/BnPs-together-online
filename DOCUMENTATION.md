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
│   │   ├── LowLevelKeyboardHook.cs    # Low-level Windows WH_KEYBOARD_LL keyboard capture
│   │   ├── WindowsInputInjector.cs    # Windows SendInput API for injecting remote player actions
│   │   └── InputBitmask.cs            # 1-byte compact bitmask for Z, X, C, Up, Down, Left, Right
│   ├── Setup/
│   │   └── ZeroTierManager.cs         # ZeroTier silent installer, UAC network joiner, Firewall rules
│   └── UI/
│       ├── MainWindow.xaml / .cs      # Retro Undertale-styled main menu, host/join flows, overlay
│       ├── RestoreSaveWindow.xaml     # Save backup and restoration interface
│       └── Assets/                    # Dual-heart icon, Determination fonts, background art
├── Setup/
│   ├── Installer.iss                  # Inno Setup script (Interactive checkbox for cleanup, reset, self-destruct)
│   └── Fix-UndertaleDataWin.ps1       # Automated bytecode fixer for data.win switch symbols
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

## 4. Installer Lifecycle & Cleanup Options (`Installer.iss`)

1. **Conflict Avoidance:** Removed `SetupMutex` to eliminate `"Setup is currently running"` false-positives.
2. **Process Termination:** Kills existing `BnPRelay.exe` processes cleanly before overwrite.
3. **Delete Installer Checkbox (Configurable Cleanup):**
   - Instead of unconditional forced self-deletion, the Finished Page natively renders an interactive checkbox:
     `[x] Delete installer after installation` (Checked/True by default).
   - Injected into Inno Setup's native `WizardForm.RunList` upon entering `CurPageChanged(wpFinished)` so it renders directly below `[x] Launch BnP Together ONLINE` with proper DPI scaling and styling.
   - `DeinitializeSetup()` checks `WizardForm.RunList.Checked[DeleteCheckboxIndex]` and only deletes the `.exe` if the install completed and the user left the checkbox checked.
4. **Fresh Reset Routine (`OnUninstallClick`):** Completely purges installation folder, `%localappdata%\BnPTogether`, desktop shortcuts, and registry uninstall keys.

---

## 5. Undertale GameMaker Bytecode & Mod Engine Fixes (SOLVED & VERIFIED 🎉)

### A. The "switch_controller_vibration_permitted" Load Error
When launching the patched *Bits & Pieces* game executable, GameMaker previously crashed on load with:
```
Code Error
___________________________________________
Error on load
Unable to find function switch_controller_vibration_permitted
```

### B. Root Cause Analysis
1. **GameMaker Runner Symbol Table:** The Windows PC GameMaker runner (`UNDERTALE.exe` / `UNDERTALEBNP.exe`) iterates over the `FUNC` chunk (Function table) inside `data.win` at startup. Every function name in `FUNC` is resolved against the runner executable's internal symbol table.
2. **Nintendo Switch Leftovers in BnP Bytecode:** The *Bits & Pieces* mod base was compiled with Nintendo Switch console vibration and account management scripts. These generated 10 `switch_*` symbol entries in the `FUNC` table:
   - `switch_controller_vibration_permitted`
   - `switch_controller_vibrate_hd`
   - `switch_accounts_select_account`
   - `switch_controller_support_set_defaults`
   - `switch_controller_support_set_singleplayer_only`
   - `switch_controller_set_supported_styles`
   - `switch_controller_support_show`
   - `switch_controller_support_get_selected_id`
   - `switch_language_get_desired_language`
   - `switch_save_data_commit`
3. Because the Windows GameMaker runner does not contain Nintendo Switch native C++ functions, the runner halts immediately during initial asset loading.

### C. The Permanent Bytecode Patch
Using `UndertaleModLib`, the following bytecode transforms were applied to `data.win`:
1. **`gml_Script_scr_rumble_hd`:** Bytecode cleared and replaced with a single `exit.i` instruction (HD rumble is unused on PC).
2. **`gml_Object_obj_time_Step_1`:** Instruction `717` (`bf 00015`) converted to unconditional branch (`b 00015`), completely bypassing the Switch controller pairing popup screen if no gamepad is connected.
3. **Instruction Function Redirection:** All `Call` instructions referencing `switch_*` were redirected to safe native functions (`control_update`).
4. **`FUNC` Table Purging:** All 10 `switch_*` function entries were removed from `$data.Functions`.
5. **Automation Script:** Provided in `Setup/Fix-UndertaleDataWin.ps1` to re-run the fix in 1 click if needed.

### D. Verification
- Patched `data.win` tested against `UNDERTALE.exe` in `C:\Program Files (x86)\Steam\steamapps\common\Undertale`.
- `UNDERTALE.exe` launches immediately into memory (174+ MB allocated), runs smoothly with zero error popups, and connects to `BnPRelay.exe`.

---

## 6. How to Build & Package
1. **Publish .NET 8 Relay:**
   ```powershell
   dotnet publish BnPRelay/BnPRelay.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o Publish
   ```
2. **Compile Inno Setup Installer:**
   ```powershell
   & "C:\Users\CLICK\AppData\Local\Programs\Inno Setup 6\ISCC.exe" Setup/Installer.iss
   ```
3. **Final Executable Output:**
   `Output\BnP_Together_ONLINE_Setup.exe`

---

## 7. Persistent Configuration & Intro Animation Sync

### A. Persistent Configuration (`%AppData%\BnPTogether\config.ini`)
- **Remembered IP Address:** UI contains a `[x] Remember IP address` checkbox. Entering the Host IP saves `LastHostIp=<ip>` so clients never re-type IP on future launches.
- **Remembered 16-Digit Network ID:** Entering the 16-digit ZeroTier Network ID saves `LastNetworkId=<id>` and automatically pre-fills the input dialog box for single-click confirmation.

### B. Intro Animation Synchronization Policy
- **Independent Visuals / Cooperative Skips:**
  - Neither player's animations are forcibly frozen or locked.
  - If a player (e.g. Host) presses Confirm (`Z` / `Enter`), the game skips/advances normally.
  - Battle encounters synchronize the RNG seed dynamically in memory without pausing or freezing the attack animation frame loop.

---

## 8. Development Chronicle & Solved Hurdles (The Journey)

### 1. The Network & ZeroTier Setup Hurdles
- **Problem: Port Binding & Local IP Collisions:** `GetLocalIp()` was picking up WSL (`172.x.x.x`), LAN (`192.168.x.x`), or Docker virtual adapters, leading to connection timeouts and "No such host is known" errors.
  * *Fix:* Updated `MainWindow.xaml.cs` to filter `NetworkInterface.GetAllNetworkInterfaces()` and prioritize adapters explicitly named/described with `"ZeroTier"` (`10.x.x.x`).
- **Problem: ZeroTier Join Permission Denied:** Joining ZeroTier via CLI failed because `authtoken.secret` in `C:\ProgramData\ZeroTier\One` requires administrator elevation.
  * *Fix:* Executed the join command via elevated PowerShell (`Start-Process ... -Verb RunAs`).
- **Problem: Windows Firewall Blocks:** Inbound connections on TCP `7777` were silently dropped by Windows Defender.
  * *Fix:* Added `ZeroTierManager.EnsureFirewallRules()` to register firewall port rules automatically on first launch.

### 2. The GameMaker Bytecode Crash
- **Problem: `switch_controller_vibration_permitted` Fatal Error:** On game boot, GameMaker crashed with `Unable to find function switch_controller_vibration_permitted`.
  * *Fix:* Reverse-engineered `data.win` with `UndertaleModLib`, converted Nintendo Switch pairing check (`obj_time_Step_1:717`) into an unconditional branch, redirected calls, and purged all 10 `switch_*` symbol entries from the `FUNC` chunk. Provided 1-click script `Setup/Fix-UndertaleDataWin.ps1`.

### 3. The Intro Animation Sync & Black Screen Lock
- **Problem: Friend's Game Stuck on Intro Screen:** Local Undertale Together only mapped Player 2 to `WASD/F/G`. When the game booted to the intro, it waited specifically for `Z` or `Enter` (Player 1 confirm). Injecting `F` was ignored by the intro scene controller.
  * *Fix:* Updated `WindowsInputInjector.cs` to universally inject `VK_RETURN`, `VK_Z`, and `VK_SPACE` alongside P2 keys whenever Confirm is pressed, allowing host skips to advance both screens simultaneously.

### 4. UI "Not Responding" Hangs
- **Problem: SaveFileMirror UI Freeze:** `SaveFileMirror` executed synchronous `Thread.Sleep(100)` and file I/O operations directly on the thread during game save events, causing the WPF dispatcher to hang and trigger "Not Responding".
  * *Fix:* Refactored `OnFileChanged` to dispatch I/O asynchronously via `Task.Run` with non-blocking `await Task.Delay(100)`.

### 5. Single-Instance & Multiple Window Duplication
- **Problem: Duplicate Instances:** Opening shortcuts repeatedly created multiple relay instances competing for socket port `7777` and keyboard hooks.
  * *Fix:* Enforced a system-wide named Mutex (`BnPTogether_SingleInstance_Mutex`) in `App.xaml.cs`. If another instance exists, it calls Win32 `SetForegroundWindow` to bring the existing window to the front and terminates cleanly.

### 6. Installer Bulletproofing & Size Optimization
- **Problem: "Setup is currently running" Popup:** Inno Setup's `SetupMutex` blocked re-running installers.
  * *Fix:* Removed `SetupMutex`, added multi-directory setup auto-cleaners (`Downloads`, `Telegram Desktop`, `Desktop`), and added an automatic self-deletion routine (`DeleteSelfInstaller`).
- **Problem: 66 MB Setup vs 2 MB Lightweight Build:** Self-contained .NET 8 bundling inflated the installer to 66 MB.
  * *Fix:* Switched to framework-dependent single-file publish (`--self-contained false`), reducing the setup executable from **66 MB to 2.1 MB** and the release zip to **225 KB**.
- **Problem: In-App 1-Click Updater:** Added `[↓] UPDATE TO LATEST VERSION` button with semantic version comparison (`tag_name == currentVersion`) to check GitHub Releases without redundant downloads.
- **Problem: Missing Desktop Shortcut Icon:** Fixed `heart.ico` extraction directly into application root `{app}\heart.ico` and flushed the Windows shell icon cache.

---

## 9. Future Roadmap & Upcoming Planned Features

### A. Core Architecture & Relay Evolution
1. **Public Matchmaking & Lobby System (Zero-Config Connection):**
   - Implement an optional lightweight signaling server / matchmaking lobby so players can join via 4-character Room Codes (e.g. `ABCD`) without manually exchanging ZeroTier network IDs or IP addresses.
2. **Native Gamepad / XInput Controller Streaming:**
   - Extend `LowLevelKeyboardHook` and `WindowsInputInjector` with `XInput` / DirectInput hooks to allow full controller play (DualShock, Xbox, Switch Pro controllers) with custom button remapping.
3. **Automated GameMaker Bytecode Auto-Patcher:**
   - Integrate the `UndertaleModLib` patching routine directly into `BnPRelay` on launch so users never have to run PowerShell scripts manually if their `data.win` contains console vibration or Switch symbols.

### B. Battle & Game Synchronization Enhancements
1. **Automatic Battle State Detection & Turn Lockstep:**
   - Memory watcher for GameMaker global battle flags (`global.inbattle`, `global.myfight`) to automatically trigger `TurnSyncBarrier` without requiring manual sync hotkeys.
2. **Dynamic Cutscene & Dialogue Synchronization:**
   - Synchronize text progression and unskippable story sequences so both players experience narrative triggers, NPC dialogues, and boss transitions at identical frame rates.
3. **Smart Delta Rollback & Network Ping Compensation:**
   - Implement client-side input prediction and minor rollback buffering (< 50ms) to ensure smooth character movement even on higher ping connections.

### C. Distribution & Community
1. **Reddit & Community Showcase:**
   - Post to `r/Undertale` and `r/UndertaleMods` showcasing how *Bits & Pieces Together* was converted from single-PC local co-op into true online peer-to-peer multiplayer.
2. **Automated CI/CD GitHub Actions Pipeline:**
   - Build and release new versions automatically on Git tag push with release notes and binary attachments.
