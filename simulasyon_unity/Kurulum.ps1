param([string]$PythonCommand = 'python')
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$venvPath = Join-Path $taskRoot '.venv-rvm-opcua'
$pythonPath = Join-Path $venvPath 'Scripts/python.exe'
if (-not (Test-Path -LiteralPath $pythonPath)) {
    & $PythonCommand -m venv $venvPath
    if ($LASTEXITCODE -ne 0) { throw 'Python sanal ortami olusturulamadi. Python 3.11 veya 3.12 kurulumunu kontrol edin.' }
}
& $pythonPath -m pip install -r (Join-Path $PSScriptRoot 'requirements.txt')
if ($LASTEXITCODE -ne 0) { throw 'Python bagimliliklari kurulamadi.' }
Push-Location $PSScriptRoot
try {
    & $pythonPath -m unittest test_contract -v
    if ($LASTEXITCODE -ne 0) { throw 'Veri sozlesmesi testleri basarisiz.' }
} finally { Pop-Location }
Write-Host 'Kurulum tamamlandi. CODESYS hazir oldugunda Baslat.ps1 calistirin.'
