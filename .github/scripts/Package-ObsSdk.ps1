[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $ObsInstallRoot,

    [Parameter(Mandatory)]
    [string] $ObsSourceRoot,

    [string] $OutputDirectory,

    [string] $Configuration = "RelWithDebInfo"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot "..\..")
)

$ObsInstallRoot = (Resolve-Path -LiteralPath $ObsInstallRoot).Path
$ObsSourceRoot = (Resolve-Path -LiteralPath $ObsSourceRoot).Path

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot "artifacts\obs-sdk"
}

$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)

function Assert-File {
    param([Parameter(Mandatory)][string] $Path)

    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Required file was not found: $Path"
    }
}

function Assert-Directory {
    param([Parameter(Mandatory)][string] $Path)

    if (!(Test-Path -LiteralPath $Path -PathType Container)) {
        throw "Required directory was not found: $Path"
    }
}

function Copy-DirectoryContents {
    param(
        [Parameter(Mandatory)][string] $Source,
        [Parameter(Mandatory)][string] $Destination
    )

    Assert-Directory $Source

    New-Item -ItemType Directory -Path $Destination -Force |
        Out-Null

    Get-ChildItem -LiteralPath $Source -Force |
        ForEach-Object {
            Copy-Item `
                -LiteralPath $_.FullName `
                -Destination $Destination `
                -Recurse `
                -Force
        }
}

function Get-CMakeVariable {
    param(
        [Parameter(Mandatory)][string] $Contents,
        [Parameter(Mandatory)][string] $Name
    )

    $pattern = 'set\s*\(\s*' +
        [regex]::Escape($Name) +
        '\s+"([^"]+)"\s*\)'

    $match = [regex]::Match(
        $Contents,
        $pattern,
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase
    )

    if (!$match.Success) {
        throw "CMake variable was not found: $Name"
    }

    return $match.Groups[1].Value
}

function New-DeterministicZip {
    param(
        [Parameter(Mandatory)][string] $SourceDirectory,
        [Parameter(Mandatory)][string] $DestinationPath
    )

    Add-Type -AssemblyName System.IO.Compression

    if (Test-Path -LiteralPath $DestinationPath) {
        Remove-Item -LiteralPath $DestinationPath -Force
    }

    $stream = [System.IO.File]::Open(
        $DestinationPath,
        [System.IO.FileMode]::CreateNew,
        [System.IO.FileAccess]::Write,
        [System.IO.FileShare]::None
    )

    $archive = $null

    try {
        $archive = [System.IO.Compression.ZipArchive]::new(
            $stream,
            [System.IO.Compression.ZipArchiveMode]::Create,
            $false
        )

        $files = Get-ChildItem `
            -LiteralPath $SourceDirectory `
            -Recurse `
            -File |
            Sort-Object {
                [System.IO.Path]::GetRelativePath(
                    $SourceDirectory,
                    $_.FullName
                )
            }

        foreach ($file in $files) {
            $relativePath = [System.IO.Path]::GetRelativePath(
                $SourceDirectory,
                $file.FullName
            ).Replace("\", "/")

            $entry = $archive.CreateEntry(
                $relativePath,
                [System.IO.Compression.CompressionLevel]::Optimal
            )

            $entry.LastWriteTime = [System.DateTimeOffset]::new(
                2000,
                1,
                1,
                0,
                0,
                0,
                [System.TimeSpan]::Zero
            )

            $inputStream = [System.IO.File]::OpenRead($file.FullName)
            $outputStream = $entry.Open()

            try {
                $inputStream.CopyTo($outputStream)
            }
            finally {
                $outputStream.Dispose()
                $inputStream.Dispose()
            }
        }
    }
    finally {
        if ($null -ne $archive) {
            $archive.Dispose()
        }

        $stream.Dispose()
    }
}

$versionFile = Join-Path `
    $repositoryRoot `
    "cmake\dependencies\CastorObsSdkVersion.cmake"

$manifestTemplate = Join-Path `
    $repositoryRoot `
    "sdk\obs\manifest.template.json"

$cmakeConfig = Join-Path `
    $repositoryRoot `
    "sdk\obs\cmake\libobsConfig.cmake"

Assert-File $versionFile
Assert-File $manifestTemplate
Assert-File $cmakeConfig
Assert-Directory $ObsInstallRoot
Assert-Directory $ObsSourceRoot

$versionContents = Get-Content -LiteralPath $versionFile -Raw

$obsVersion = Get-CMakeVariable `
    -Contents $versionContents `
    -Name "CASTOR_OBS_VERSION"

$sdkRevision = Get-CMakeVariable `
    -Contents $versionContents `
    -Name "CASTOR_OBS_SDK_REVISION"

$platform = Get-CMakeVariable `
    -Contents $versionContents `
    -Name "CASTOR_OBS_SDK_PLATFORM"

$sdkVersion = "$obsVersion-castor.$sdkRevision"
$archiveName = "Castor.Obs.Sdk.$platform-$sdkVersion.zip"

$stagingRoot = Join-Path `
    $OutputDirectory `
    "staging-$sdkVersion"

$archivePath = Join-Path $OutputDirectory $archiveName
$checksumPath = "$archivePath.sha256"

if (Test-Path -LiteralPath $stagingRoot) {
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $OutputDirectory -Force |
    Out-Null

$includeRoot = Join-Path $stagingRoot "include"
$libraryRoot = Join-Path $stagingRoot "lib"
$cmakeRoot = Join-Path $stagingRoot "cmake"
$licenseRoot = Join-Path $stagingRoot "licenses"

$runtimeRoot = Join-Path $stagingRoot "runtime"
$runtimeBinRoot = Join-Path $runtimeRoot "bin\64bit"
$runtimePluginRoot = Join-Path $runtimeRoot "obs-plugins\64bit"
$runtimeDataRoot = Join-Path $runtimeRoot "data"

@(
    $includeRoot,
    $libraryRoot,
    $cmakeRoot,
    $licenseRoot,
    $runtimeBinRoot,
    $runtimePluginRoot,
    $runtimeDataRoot
) | ForEach-Object {
    New-Item -ItemType Directory -Path $_ -Force |
        Out-Null
}

Write-Host "Packaging Castor OBS SDK $sdkVersion"

# Public headers
Copy-DirectoryContents `
    -Source (Join-Path $ObsInstallRoot "include") `
    -Destination $includeRoot

# Import libraries
$importLibraries = @(
    "obs.lib",
    "w32-pthreads.lib"
)

foreach ($library in $importLibraries) {
    $source = Join-Path $ObsInstallRoot "lib\$library"

    Assert-File $source

    Copy-Item `
        -LiteralPath $source `
        -Destination $libraryRoot `
        -Force
}

# Relocatable CMake package
Copy-Item `
    -LiteralPath $cmakeConfig `
    -Destination (Join-Path $cmakeRoot "libobsConfig.cmake") `
    -Force

# Runtime DLLs. Qt, CEF and the OBS frontend are intentionally excluded.
$sourceBinRoot = Join-Path $ObsInstallRoot "bin\64bit"
Assert-Directory $sourceBinRoot

$excludedRuntimeDllPatterns = @(
    "Qt6*.dll",
    "libcef.dll",
    "chrome_elf.dll",
    "obs-frontend-api.dll",
    "obs-websocket-api.dll"
)

Get-ChildItem `
    -LiteralPath $sourceBinRoot `
    -Filter "*.dll" `
    -File |
    ForEach-Object {
        $excluded = $false

        foreach ($pattern in $excludedRuntimeDllPatterns) {
            if ($_.Name -like $pattern) {
                $excluded = $true
                break
            }
        }

        if (!$excluded) {
            Copy-Item `
                -LiteralPath $_.FullName `
                -Destination $runtimeBinRoot `
                -Force
        }
    }

# Required FFmpeg muxing helper
$ffmpegMux = Join-Path $sourceBinRoot "obs-ffmpeg-mux.exe"
Assert-File $ffmpegMux

Copy-Item `
    -LiteralPath $ffmpegMux `
    -Destination $runtimeBinRoot `
    -Force

# Core libobs data
Copy-DirectoryContents `
    -Source (Join-Path $ObsInstallRoot "data\libobs") `
    -Destination (Join-Path $runtimeDataRoot "libobs")

# Selected plugins
$plugins = @(
    "win-capture",
    "win-wasapi",
    "win-dshow",
    "obs-ffmpeg",
    "obs-outputs",
    "obs-x264",
    "obs-nvenc",
    "obs-qsv11",
    "image-source",
    "obs-filters",
    "obs-transitions",
    "rtmp-services"
)

foreach ($plugin in $plugins) {
    $pluginBinary = Join-Path `
        $ObsInstallRoot `
        "obs-plugins\64bit\$plugin.dll"

    Assert-File $pluginBinary

    Copy-Item `
        -LiteralPath $pluginBinary `
        -Destination $runtimePluginRoot `
        -Force

    $pluginData = Join-Path `
        $ObsInstallRoot `
        "data\obs-plugins\$plugin"

    if (Test-Path -LiteralPath $pluginData -PathType Container) {
        $pluginDataDestination = Join-Path `
            $runtimeDataRoot `
            "obs-plugins\$plugin"

        Copy-DirectoryContents `
            -Source $pluginData `
            -Destination $pluginDataDestination

        # Plugin data may contain debugging symbols for helper binaries.
        Get-ChildItem `
            -LiteralPath $pluginDataDestination `
            -Recurse `
            -File `
            -Filter "*.pdb" |
            ForEach-Object {
                Write-Host "Excluding plugin debug symbol: $($_.FullName)"

                Remove-Item `
                    -LiteralPath $_.FullName `
                    -Force
            }
    }
}

# OBS license
$obsLicense = Join-Path $ObsSourceRoot "COPYING"
Assert-File $obsLicense

$obsLicenseRoot = Join-Path $licenseRoot "obs-studio"
New-Item -ItemType Directory -Path $obsLicenseRoot -Force |
    Out-Null

Copy-Item `
    -LiteralPath $obsLicense `
    -Destination (Join-Path $obsLicenseRoot "COPYING") `
    -Force

$authorsFile = Join-Path $ObsSourceRoot "AUTHORS"

if (Test-Path -LiteralPath $authorsFile -PathType Leaf) {
    Copy-Item `
        -LiteralPath $authorsFile `
        -Destination (Join-Path $obsLicenseRoot "AUTHORS") `
        -Force
}

# Collect available third-party license notices.
$licenseSearchRoots = @(
    (Join-Path $ObsSourceRoot "deps"),
    (Join-Path $ObsSourceRoot "plugins"),
    (Join-Path $ObsSourceRoot "libobs"),
    (Join-Path $ObsSourceRoot "shared"),
    (Join-Path $ObsSourceRoot ".deps"),
    (Join-Path $ObsSourceRoot "build_x64\_deps")
)

foreach ($searchRoot in $licenseSearchRoots) {
    if (!(Test-Path -LiteralPath $searchRoot -PathType Container)) {
        continue
    }

    Get-ChildItem `
        -LiteralPath $searchRoot `
        -Recurse `
        -File `
        -Force |
        Where-Object {
            $_.Name -match '^(LICENSE|LICENCE|COPYING|NOTICE)(\..*)?$' -or
            $_.DirectoryName -match '[\\/]LICENSES?[\\/]'
        } |
        ForEach-Object {
            $relativePath = [System.IO.Path]::GetRelativePath(
                $ObsSourceRoot,
                $_.FullName
            )

            $destination = Join-Path `
                $licenseRoot `
                "third-party\$relativePath"

            $destinationDirectory = Split-Path `
                -Parent `
                $destination

            New-Item `
                -ItemType Directory `
                -Path $destinationDirectory `
                -Force |
                Out-Null

            Copy-Item `
                -LiteralPath $_.FullName `
                -Destination $destination `
                -Force
        }
}

# Generate manifest.json
$manifest = Get-Content `
    -LiteralPath $manifestTemplate `
    -Raw |
    ConvertFrom-Json

$manifest.sdkVersion = $sdkVersion
$manifest.obsVersion = $obsVersion
$manifest.castorRevision = [int] $sdkRevision
$manifest.runtimeIdentifier = $platform
$manifest.architecture = "x64"
$manifest.configuration = $Configuration

$manifestPath = Join-Path $stagingRoot "manifest.json"
$manifestJson = $manifest | ConvertTo-Json -Depth 10

[System.IO.File]::WriteAllText(
    $manifestPath,
    $manifestJson + [Environment]::NewLine,
    [System.Text.UTF8Encoding]::new($false)
)

# Final validation
$requiredPackageFiles = @(
    (Join-Path $stagingRoot "include\obs.h"),
    (Join-Path $stagingRoot "include\obsconfig.h"),
    (Join-Path $stagingRoot "lib\obs.lib"),
    (Join-Path $stagingRoot "lib\w32-pthreads.lib"),
    (Join-Path $stagingRoot "cmake\libobsConfig.cmake"),
    (Join-Path $stagingRoot "runtime\bin\64bit\obs.dll"),
    (Join-Path $stagingRoot "runtime\bin\64bit\w32-pthreads.dll"),
    (Join-Path $stagingRoot "runtime\bin\64bit\obs-ffmpeg-mux.exe"),
    $manifestPath
)

foreach ($requiredFile in $requiredPackageFiles) {
    Assert-File $requiredFile
}

$forbiddenFiles = Get-ChildItem `
    -LiteralPath $stagingRoot `
    -Recurse `
    -File |
    Where-Object {
        $_.Extension -eq ".pdb" -or
        $_.Name -eq "obs64.exe" -or
        $_.Name -like "Qt6*.dll" -or
        $_.Name -eq "libcef.dll"
    }

if ($forbiddenFiles) {
    $paths = $forbiddenFiles.FullName -join [Environment]::NewLine
    throw "Forbidden files were included in the SDK:`n$paths"
}

New-DeterministicZip `
    -SourceDirectory $stagingRoot `
    -DestinationPath $archivePath

$hash = Get-FileHash `
    -LiteralPath $archivePath `
    -Algorithm SHA256

$checksumContents = (
    "{0}  {1}{2}" -f
    $hash.Hash.ToLowerInvariant(),
    $archiveName,
    [Environment]::NewLine
)

[System.IO.File]::WriteAllText(
    $checksumPath,
    $checksumContents,
    [System.Text.UTF8Encoding]::new($false)
)

Remove-Item -LiteralPath $stagingRoot -Recurse -Force

Write-Host "Archive: $archivePath"
Write-Host "SHA-256: $($hash.Hash.ToLowerInvariant())"

if ($env:GITHUB_OUTPUT) {
    "archive-path=$archivePath" |
        Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8

    "checksum-path=$checksumPath" |
        Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8

    "archive-name=$archiveName" |
        Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8

    "sha256=$($hash.Hash.ToLowerInvariant())" |
        Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
}
