$ErrorActionPreference = 'Stop'
$pidFile = Join-Path $PSScriptRoot 'bridge.pid'
if (-not (Test-Path -LiteralPath $pidFile)) { Write-Host 'Kayitli v106 kopru islemi yok.'; exit }
$bridgeProcessId = [int](Get-Content -LiteralPath $pidFile)
$processInfo = Get-CimInstance Win32_Process -Filter "ProcessId=$bridgeProcessId"
$expectedScript = Join-Path $PSScriptRoot 'bridge.py'
if ($processInfo -and $processInfo.CommandLine -and $processInfo.CommandLine.Contains($expectedScript)) {
    Stop-Process -Id $bridgeProcessId
    Remove-Item -LiteralPath $pidFile
    Write-Host 'Yalniz bu klasorun v106 koprusu durduruldu.'
} elseif ($processInfo) { throw 'PID baska bir uygulamaya ait; durdurulmadi.' }
else { Write-Host 'Kayitli islem artik calismiyor.' }
