param(
  [string]$EnvName = ".venv-mlagents",
  [string]$Python = "python"
)

Write-Host "Creating virtual environment: $EnvName" -ForegroundColor Cyan
$venvPath = Join-Path $PSScriptRoot $EnvName

if (!(Test-Path $venvPath)) {
  & $Python -m venv $venvPath
} else {
  Write-Host "Virtual env already exists, reusing." -ForegroundColor Yellow
}

$bin = if ($IsWindows) { "Scripts" } else { "bin" }
$pyExe = Join-Path $venvPath $bin
$pyExe = Join-Path $pyExe "python.exe"

Write-Host "Upgrading pip..." -ForegroundColor Cyan
& $pyExe -m pip install --upgrade pip setuptools wheel

# Pin to a version compatible with com.unity.ml-agents 2.0.1 (release_20)
$mlagentsVersion = "0.30.0"
Write-Host "Installing mlagents==$mlagentsVersion ..." -ForegroundColor Cyan
& $pyExe -m pip install "mlagents==$mlagentsVersion"

Write-Host "Done. Activate with:`n`t$EnvName\Scripts\Activate.ps1" -ForegroundColor Green
Write-Host "Train with:" -ForegroundColor Green
Write-Host "`nmlagents-learn ..\\ml-agents-configs\\ppo_vampire.yaml --run-id VS-`$(Get-Date -Format yyyyMMddHHmmss) --time-scale=20"