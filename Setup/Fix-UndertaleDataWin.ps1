# ==============================================================================
# BnP Together ONLINE - Undertale data.win Switch Symbol Fixer
# ==============================================================================
# Root Cause:
# Undertale Bits and Pieces bytecode contains Nintendo Switch-specific function
# references (e.g. switch_controller_vibration_permitted, switch_controller_vibrate_hd,
# switch_accounts_select_account, etc.) inside the FUNC chunk of data.win.
# The standard Windows GameMaker runner (UNDERTALE.exe / UNDERTALEBNP.exe) checks
# every function in the FUNC chunk at engine boot and aborts with:
#   "Error on load: Unable to find function switch_controller_vibration_permitted"
#
# This script:
# 1. Loads data.win using UndertaleModLib
# 2. Empties gml_Script_scr_rumble_hd into a safe 'exit.i' opcode
# 3. Redirects dormant switch calls to safe engine opcodes
# 4. Removes all 10 switch_* symbols from data.Functions (FUNC chunk)
# 5. Saves the clean, Windows runner-compatible data.win
# ==============================================================================

param(
    [string]$UndertaleDataWinPath = "C:\Program Files (x86)\Steam\steamapps\common\Undertale\data.win",
    [string]$UtmtLibPath = "C:\Users\CLICK\Downloads\UTMT_CLI\UndertaleModLib.dll"
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

$dummyFunc = $data.Functions | Where-Object { $_.Name.Content -eq 'control_update' } | Select-Object -First 1

# 1. Update gml_Script_scr_rumble_hd
$cRumble = $data.Code | Where-Object { $_.Name.Content -eq 'gml_Script_scr_rumble_hd' }
if ($cRumble) {
    $cRumble.Instructions.Clear()
    $exitInst = New-Object UndertaleModLib.Models.UndertaleInstruction
    $exitInst.Kind = [UndertaleModLib.Models.UndertaleInstruction+Opcode]::Exit
    $exitInst.Type1 = [UndertaleModLib.Models.UndertaleInstruction+DataType]::Int32
    $cRumble.Instructions.Add($exitInst)
    Write-Host "Patched gml_Script_scr_rumble_hd to exit immediately." -ForegroundColor Green
}

# 2. Redirect all switch instructions to safe symbols
foreach ($code in $data.Code) {
    if ($code.Instructions -ne $null) {
        for ($i = 0; $i -lt $code.Instructions.Count; $i++) {
            $inst = $code.Instructions[$i]
            if ($inst.ResolvedFunction -ne $null -and $inst.ResolvedFunction.Name.Content -match 'switch_') {
                $inst.ValueFunction = $dummyFunc
                $inst.ArgumentsCount = 0
            }
        }
    }
}

# 3. Patch obj_time_Step_1 instruction 717 to branch unconditionally away from switch pairing
$cStep1 = $data.Code | Where-Object { $_.Name.Content -eq 'gml_Object_obj_time_Step_1' }
if ($cStep1 -and $cStep1.Instructions.Count -gt 717) {
    $inst717 = $cStep1.Instructions[717]
    if ($inst717.ToString() -match 'bf') {
        $inst717.Kind = [UndertaleModLib.Models.UndertaleInstruction+Opcode]::B
    }
}

# 4. Remove all switch_ functions from FUNC chunk
$toRemove = @($data.Functions | Where-Object { $_.Name.Content -match 'switch_' })
foreach ($f in $toRemove) {
    $data.Functions.Remove($f) | Out-Null
    Write-Host ("Removed from FUNC chunk: " + $f.Name.Content) -ForegroundColor Yellow
}

# 5. Write out patched data.win
$outFs = [System.IO.File]::Create($UndertaleDataWinPath)
[UndertaleModLib.UndertaleIO]::Write($outFs, $data)
$outFs.Close()

Write-Host "Successfully patched data.win! Switch symbol errors permanently resolved." -ForegroundColor Green
