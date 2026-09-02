# ==============================================================================
# BnP Together ONLINE - Automated Game Role & Decoupled Hitbox Patcher
# ==============================================================================
# Purpose:
# 1. Bypasses the initial P1 vs P2 selection prompt by automatically setting
#    global.playerindexor in obj_time_Create_0:
#      - Host   -> global.playerindexor = 1 (Player 1 / Frisk)
#      - Client -> global.playerindexor = 2 (Player 2 / Companion)
#
# 2. Decouples heart hitboxes so each instance ONLY calculates local collisions:
#      - Host   -> Disables all 23 P2 collisions (P2 heart is a visual ghost)
#      - Client -> Disables all 79 P1 collisions (P1 heart is a visual ghost)
# ==============================================================================

param(
    [string]$UndertaleDataWinPath = "C:\Program Files (x86)\Steam\steamapps\common\Undertale\data.win",
    [string]$UtmtLibPath = "C:\Users\CLICK\Downloads\UTMT_CLI\UndertaleModLib.dll",
    [ValidateSet("Host", "Client")]
    [string]$Role = "Host"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $UndertaleDataWinPath)) {
    Write-Error "Could not find data.win at: $UndertaleDataWinPath"
    exit 1
}

if (-not (Test-Path $UtmtLibPath)) {
    Write-Error "Could not find UndertaleModLib.dll at: $UtmtLibPath"
    exit 1
}

Write-Host "Loading UndertaleModLib..." -ForegroundColor Cyan
Add-Type -Path $UtmtLibPath

Write-Host "Reading $UndertaleDataWinPath..." -ForegroundColor Cyan
$fs = [System.IO.File]::OpenRead($UndertaleDataWinPath)
$data = [UndertaleModLib.UndertaleIO]::Read($fs)
$fs.Close()

Write-Host "Configuring Game for Role: $Role..." -ForegroundColor Cyan

# ─── 1. AUTO-ASSIGN PLAYER ROLE & BYPASS SELECTION SCREEN ─────────────────────
$cTime = $data.Code | Where-Object { $_.Name.Content -eq 'gml_Object_obj_time_Create_0' }
if ($cTime -and $cTime.Instructions.Count -gt 9) {
    $playerIdx = if ($Role -eq "Host") { [short]1 } else { [short]2 }
    $cTime.Instructions[8].ValueShort = $playerIdx
    Write-Host "Auto-configured global.playerindexor = $playerIdx (Selection screen bypassed)." -ForegroundColor Green
}

# ─── 2. DECOUPLE HEART HITBOXES ──────────────────────────────────────────────
if ($Role -eq "Host") {
    # Host ignores all P2 collisions (P2 is remote visual avatar)
    $p2Codes = @($data.Code | Where-Object { $_.Name.Content -match 'Collision_1862' -or $_.Name.Content -eq 'gml_Object_obj_sans_bonebul_Collision_0' })
    Write-Host "Decoupling $($p2Codes.Count) P2 collision handlers on Host..." -ForegroundColor Yellow
    foreach ($c in $p2Codes) {
        $c.Instructions.Clear()
        $exitInst = New-Object UndertaleModLib.Models.UndertaleInstruction
        $exitInst.Kind = [UndertaleModLib.Models.UndertaleInstruction+Opcode]::Exit
        $exitInst.Type1 = [UndertaleModLib.Models.UndertaleInstruction+DataType]::Int32
        $c.Instructions.Add($exitInst)
    }
    Write-Host "P2 collisions decoupled on Host (Host only calculates P1 hits)." -ForegroundColor Green
} elseif ($Role -eq "Client") {
    # Client ignores all P1 collisions (P1 is remote visual avatar)
    $p1Codes = @($data.Code | Where-Object { $_.Name.Content -match 'Collision_af950111' -or $_.Name.Content -match 'Collision_752' })
    Write-Host "Decoupling $($p1Codes.Count) P1 collision handlers on Client..." -ForegroundColor Yellow
    foreach ($c in $p1Codes) {
        $c.Instructions.Clear()
        $exitInst = New-Object UndertaleModLib.Models.UndertaleInstruction
        $exitInst.Kind = [UndertaleModLib.Models.UndertaleInstruction+Opcode]::Exit
        $exitInst.Type1 = [UndertaleModLib.Models.UndertaleInstruction+DataType]::Int32
        $c.Instructions.Add($exitInst)
    }
    Write-Host "P1 collisions decoupled on Client (Client only calculates P2 hits)." -ForegroundColor Green
}

# ─── 3. WRITE OUT PATCHED DATA.WIN ───────────────────────────────────────────
$outFs = [System.IO.File]::Create($UndertaleDataWinPath)
[UndertaleModLib.UndertaleIO]::Write($outFs, $data)
$outFs.Close()

Write-Host "Successfully patched and saved $UndertaleDataWinPath for Role: $Role!" -ForegroundColor Green
