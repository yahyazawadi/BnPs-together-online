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
   - Instead of unconditional forced self-deletion, the Finished Page renders an interactive checkbox:
     `[x] Delete installer after installation` (Checked/True by default).
   - Positioned cleanly below `WizardForm.RunList` on `WizardForm.FinishedPage`.
   - `DeinitializeSetup()` verifies `InstallSuccessful` and `DeleteInstallerCheckbox.Checked` before spawning the asynchronous self-purge process (`cmd.exe /c del /f /q`).
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
