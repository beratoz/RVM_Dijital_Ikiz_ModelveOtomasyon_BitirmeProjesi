$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
# Local settings are ignored by Git.
$localConfig = Join-Path $PSScriptRoot 'ayarlar.local.ps1'
if (Test-Path -LiteralPath $localConfig) { . $localConfig }
$pythonPath = Join-Path $projectRoot '.venv-rvm-opcua\Scripts\python.exe'
if (-not (Test-Path -LiteralPath $pythonPath)) { throw 'Python ortami eksik; Kurulum.ps1 calistirin.' }
$health = $null
try { $health = Invoke-RestMethod 'http://127.0.0.1:8766/health' -TimeoutSec 2 } catch {}
if ($health) {
    if ($health.version -ne 106 -or $health.project -ne 'RVM_DENEYSEL_OLCUM_v106') { throw '8766 portunda farkli uygulama var.' }
    Write-Host 'v106 koprusu zaten acik.'
    $health | ConvertTo-Json -Depth 3
    exit
}
$bridgePath = Join-Path $PSScriptRoot 'bridge.py'
$p = Start-Process -FilePath $pythonPath -ArgumentList @('-B',('"'+$bridgePath+'"')) -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $PSScriptRoot 'bridge_stdout.log') -RedirectStandardError (Join-Path $PSScriptRoot 'bridge.log')
$p.Id | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'bridge.pid')
Start-Sleep -Seconds 2
if ($p.HasExited) { throw 'Kopru acilamadi; bridge.log dosyasina bakin.' }
Write-Host 'v106 koprusu baslatildi. Port 8766. PLC protokolu 106 olmali.'
Write-Host 'Unity: Assets/RVM/Scenes/RVM_InlineMeasurement_v106.unity'
