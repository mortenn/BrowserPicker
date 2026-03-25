# Export docs/assets/web_icon.svg to BrowserPicker.UI Resources/web_icon.png + web_icon.ico using Svg.Skia.Converter + Pillow.
# Run from repo root:  powershell -File tools/run_web_icon.ps1
$ErrorActionPreference = "Stop"
$repo = (Resolve-Path (Split-Path -Parent $PSScriptRoot)).Path
Set-Location $repo

dotnet tool restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$svg = Join-Path $repo "docs\assets\web_icon.svg"
if (-not (Test-Path $svg)) {
    Write-Error "Missing $svg"
}

$outPng = Join-Path $repo "src\BrowserPicker.UI\Resources\web_icon.png"
$outIco = Join-Path $repo "src\BrowserPicker.UI\Resources\web_icon.ico"
$tmpDir = Join-Path $env:TEMP "browserpicker-web-icon-out"
$tmpPng = Join-Path $tmpDir "web_icon.png"

if (Test-Path $tmpDir) { Remove-Item $tmpDir -Recurse -Force }
New-Item -ItemType Directory -Path $tmpDir | Out-Null

dotnet tool run Svg.Skia.Converter -- -f $svg -o $tmpDir --format png
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
if (-not (Test-Path $tmpPng)) {
    Write-Error "Svg.Skia.Converter did not write $tmpPng"
}

python (Join-Path $repo "tools\postprocess_web_icon_png.py") $tmpPng $outPng $outIco
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Remove-Item $tmpDir -Recurse -Force -ErrorAction SilentlyContinue
