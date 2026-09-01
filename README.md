# 🎮 BnP Together ONLINE

**BnP Together ONLINE** is a standalone peer-to-peer multiplayer relay and synchronization application for **Undertale: Bits & Pieces Together (2-Player Co-op)**.

---

## ✨ Features
- **Zero-Config P2P Multiplayer:** Fast, low-latency online co-op over ZeroTier virtual LAN.
- **Full Input Synchronization:** Real-time keyboard/controller input streaming between Host and Client.
- **Save State Mirroring:** Automatically synchronizes save files (`file0`, `file8`, `file9`, `undertale.ini`) between players.
- **Battle RNG & Turn Sync:** Synchronized battle seed barriers to keep both game clients in lockstep.
- **Smart Game Launcher & Fixer:** Auto-locates Steam Undertale installations and includes bytecode fixes for GameMaker load errors.
- **Customizable Single-File Installer:** Modern Inno Setup installer with an option to `[x] Delete installer after installation` (checked by default).

---

## 🚀 Quick Start
1. Download and run **`BnP_Together_ONLINE_Setup.exe`** from the `Output/` folder or GitHub Releases.
2. Launch **BnP Together ONLINE**.
3. **Player 1 (Host):** Click `HOST`, copy the invite link / IP, and share it with your friend.
4. **Player 2 (Join):** Paste the host IP or open the `bnptogether://` link and click `CONNECT`.
5. Once connected, click **`LAUNCH GAME`** to start playing together!

---

## 🛠️ Building from Source

### 1. Build & Publish the Relay Application
```powershell
dotnet publish BnPRelay/BnPRelay.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o Publish
```

### 2. Compile Installer with Inno Setup
```powershell
& "C:\Users\CLICK\AppData\Local\Programs\Inno Setup 6\ISCC.exe" Setup/Installer.iss
```
The compiled installer will be saved to `Output/BnP_Together_ONLINE_Setup.exe`.

---

## 📖 Technical Documentation
For full network protocol details, architecture diagrams, and GameMaker bytecode patch details, see [DOCUMENTATION.md](file:///c:/Users/CLICK/.gemini/antigravity-ide/scratch/BnPs-together-online/DOCUMENTATION.md).
