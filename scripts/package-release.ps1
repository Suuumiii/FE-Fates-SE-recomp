param(
    [Parameter(Mandatory = $true)]
    [string] $LauncherExe,

    [Parameter(Mandatory = $true)]
    [string] $ToolsDirectory,

    [Parameter(Mandatory = $true)]
    [string] $OutputZip,

    [switch] $Force
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$launcherPath = (Resolve-Path $LauncherExe).Path
$toolsRoot = (Resolve-Path $ToolsDirectory).Path
$outputPath = [System.IO.Path]::GetFullPath($OutputZip)

$files = @(
    @{ Source = $launcherPath; Entry = 'Fates Launcher.exe' },
    @{ Source = (Join-Path $repoRoot 'recomplauncher.json'); Entry = 'recomplauncher.json' },
    @{ Source = (Join-Path $repoRoot 'README.md'); Entry = 'README.md' },
    @{ Source = (Join-Path $repoRoot 'LEGAL.md'); Entry = 'LEGAL.md' },
    @{ Source = (Join-Path $repoRoot 'LICENSE'); Entry = 'LICENSE' },
    @{ Source = (Join-Path $repoRoot 'THIRD_PARTY_NOTICES.txt'); Entry = 'THIRD_PARTY_NOTICES.txt' },
    @{ Source = (Join-Path $toolsRoot '3dsrecomp\3dsrecomp.exe'); Entry = 'tools/3dsrecomp/3dsrecomp.exe' },
    @{ Source = (Join-Path $toolsRoot 'zakuro\zakuro.exe'); Entry = 'tools/zakuro/zakuro.exe' }
)

foreach ($file in $files) {
    if (-not (Test-Path -LiteralPath $file.Source -PathType Leaf)) {
        throw "Required release file is missing: $($file.Source)"
    }
}

$outDir = Split-Path -Parent $outputPath
if (-not (Test-Path -LiteralPath $outDir -PathType Container)) {
    [System.IO.Directory]::CreateDirectory($outDir) | Out-Null
}
if (Test-Path -LiteralPath $outputPath) {
    if (-not $Force) {
        throw "Refusing to overwrite existing archive: $outputPath (pass -Force to replace it)"
    }
    [System.IO.File]::Delete($outputPath)
}

$archive = [System.IO.Compression.ZipFile]::Open($outputPath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in $files) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive,
            $file.Source,
            $file.Entry,
            [System.IO.Compression.CompressionLevel]::Optimal
        ) | Out-Null
    }
}
catch {
    $archive.Dispose()
    throw
}
$archive.Dispose()

Write-Host "Created release archive: $outputPath"
Write-Host "Included $($files.Count) curated files; game images, dump metadata and generated game code are not packaged."
