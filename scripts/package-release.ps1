param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [bool]$SelfContained = $false
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $root "MzGraphConvApp\MzRubyConvGui\MzRubyConvGui.csproj"
$publishRoot = Join-Path $root "publish"
$distRoot = Join-Path $root "dist"
$publishDir = Join-Path $publishRoot "MzRubyConvGui-$Runtime"
$zipPath = Join-Path $distRoot "MzRubyConvGui-$Runtime.zip"

if (!(Test-Path $project)) {
    throw "Project file was not found: $project"
}

Remove-Item -LiteralPath $publishDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $publishDir | Out-Null
New-Item -ItemType Directory -Force $distRoot | Out-Null

$selfContainedText = if ($SelfContained) { "true" } else { "false" }

dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained $selfContainedText `
    -o $publishDir

$packageReadme = @"
RetroPC Graphic Converter GUI

This package contains the Windows GUI only.

Ruby and pngconvMZ.rb are not bundled. Install Ruby separately, then specify
the converter script path in the GUI Script field.

Typical first-run settings:

Ruby:
  ruby

Script:
  path\to\pngconvMZ.rb

For PNG-only use, enable:
  MZ-2500専用ファイルを出力しない

For MZ-2500 file output, leave that option unchecked.
"@

[System.IO.File]::WriteAllText(
    (Join-Path $publishDir "README.txt"),
    $packageReadme,
    [System.Text.UTF8Encoding]::new($true)
)

Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force

Write-Host "Created package:"
Write-Host $zipPath
