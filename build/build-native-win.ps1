#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds msdf-atlas-gen.exe and msdf_atlas_c.dll for win-x64 (MSVC) and
    stages them into artifacts/native/win-x64/.

.DESCRIPTION
    Invokes CMake on native/ (which wraps external/msdf-atlas-gen) with
    freetype + libpng provided via vcpkg (manifest mode, static triplet).
    Skia geometry-preprocessing is disabled, so both binaries are self-contained.

.PARAMETER VcpkgRoot
    Path to a vcpkg checkout. Defaults to $env:VCPKG_ROOT, else bootstraps a
    local copy under build/vcpkg-local.
#>
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',
    [string]$Generator = '',
    [string]$VcpkgRoot = ''
)
$ErrorActionPreference = 'Stop'
$repo = Resolve-Path "$PSScriptRoot/.."
$msdf = Join-Path $repo 'external/msdf-atlas-gen'
$src = Join-Path $repo 'native'
$triplets = Join-Path $repo 'build/triplets'
$targetTriplet = 'x64-windows-static-release'

if (!(Test-Path (Join-Path $msdf 'CMakeLists.txt'))) {
    Write-Error "msdf-atlas-gen submodule missing at $msdf. Run: git submodule update --init --recursive"
}

# Disambiguate the BitmapConstSection C-style cast in main.cpp so the source
# compiles identically across toolchains (harmless on MSVC). Idempotent.
$mainCpp = Join-Path $msdf 'msdf-atlas-gen/main.cpp'
$content = [System.IO.File]::ReadAllText($mainCpp)
$patched = $content -replace '\(msdfgen::BitmapConstSection<T, N>\) generator\.atlasStorage\(\)', 'generator.atlasStorage().operator msdfgen::BitmapConstSection<T, N>()'
if ($patched -ne $content) {
    Write-Host "[build-native-win] Patching main.cpp: disambiguate BitmapConstSection cast."
    [System.IO.File]::WriteAllText($mainCpp, $patched)
}

# Resolve vcpkg.
if (-not $VcpkgRoot) {
    if ($env:VCPKG_ROOT) {
        $VcpkgRoot = $env:VCPKG_ROOT
    }
    else {
        $VcpkgRoot = Join-Path $repo 'build/vcpkg-local'
        if (!(Test-Path (Join-Path $VcpkgRoot 'vcpkg.exe'))) {
            Write-Host "[build-native-win] Bootstrapping local vcpkg at $VcpkgRoot"
            if (!(Test-Path $VcpkgRoot)) {
                git clone --depth 1 https://github.com/microsoft/vcpkg.git $VcpkgRoot
            }
            & (Join-Path $VcpkgRoot 'bootstrap-vcpkg.bat') -disableMetrics
            if ($LASTEXITCODE -ne 0) { Write-Error "vcpkg bootstrap failed." }
        }
    }
}
$toolchain = Join-Path $VcpkgRoot 'scripts/buildsystems/vcpkg.cmake'
if (!(Test-Path $toolchain)) {
    Write-Error "vcpkg toolchain not found at $toolchain. Set -VcpkgRoot or `$env:VCPKG_ROOT."
}

# Pick a VS generator (newest installed, unless overridden).
if (-not $Generator) {
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $installVer = (& $vswhere -latest -prerelease -property installationVersion 2>$null)
        $major = if ($installVer) { ($installVer -split '\.')[0] } else { '' }
        switch ($major) {
            '18' { $Generator = 'Visual Studio 18 2026' }
            '17' { $Generator = 'Visual Studio 17 2022' }
            '16' { $Generator = 'Visual Studio 16 2019' }
            default { $Generator = 'Visual Studio 17 2022' }
        }
    }
    else {
        $Generator = 'Visual Studio 17 2022'
    }
}

$buildDir = Join-Path $repo 'build/build-win-x64'
if (Test-Path $buildDir) { Remove-Item -Recurse -Force $buildDir }
New-Item -ItemType Directory -Path $buildDir -Force | Out-Null

Write-Host "[build-native-win] cmake configure (Generator='$Generator', vcpkg='$VcpkgRoot')"
& cmake -S $src -B $buildDir -G $Generator -A x64 `
    "-DCMAKE_TOOLCHAIN_FILE=$toolchain" `
    "-DVCPKG_OVERLAY_TRIPLETS=$triplets" `
    "-DVCPKG_TARGET_TRIPLET=$targetTriplet" `
    -DMSDF_ATLAS_USE_VCPKG=ON `
    -DMSDF_ATLAS_USE_SKIA=OFF `
    -DMSDF_ATLAS_NO_ARTERY_FONT=ON `
    -DMSDF_ATLAS_MSDFGEN_EXTERNAL=OFF `
    -DMSDF_ATLAS_BUILD_STANDALONE=ON `
    -DMSDF_ATLAS_INSTALL=OFF `
    -DMSDF_ATLAS_DYNAMIC_RUNTIME=OFF `
    -DBUILD_SHARED_LIBS=OFF
if ($LASTEXITCODE -ne 0) { Write-Error "cmake configure failed: $LASTEXITCODE" }

Write-Host "[build-native-win] cmake --build $buildDir --config $Configuration"
& cmake --build $buildDir --config $Configuration --parallel
if ($LASTEXITCODE -ne 0) { Write-Error "cmake --build failed: $LASTEXITCODE" }

$exe = Get-ChildItem -Path $buildDir -Filter 'msdf-atlas-gen.exe' -Recurse | Select-Object -First 1
if (-not $exe) { Write-Error "msdf-atlas-gen.exe not produced; inspect $buildDir." }
$dll = Get-ChildItem -Path $buildDir -Filter 'msdf_atlas_c.dll' -Recurse | Select-Object -First 1
if (-not $dll) { Write-Error "msdf_atlas_c.dll not produced; inspect $buildDir." }

$nativeOut = Join-Path $repo 'artifacts/native/win-x64'
New-Item -ItemType Directory -Path $nativeOut -Force | Out-Null
Copy-Item -Path $exe.FullName -Destination (Join-Path $nativeOut 'msdf-atlas-gen.exe') -Force
Copy-Item -Path $dll.FullName -Destination (Join-Path $nativeOut 'msdf_atlas_c.dll') -Force

Write-Host "[build-native-win] Staged win-x64 artifacts:"
Get-ChildItem $nativeOut | Format-Table Name, Length

$dumpbin = $null
$vsInstaller = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (Test-Path $vsInstaller) {
    $vsPath = (& $vsInstaller -latest -prerelease -property installationPath 2>$null)
    if ($vsPath) {
        $dumpbin = Get-ChildItem -Path (Join-Path $vsPath 'VC\Tools\MSVC') -Filter 'dumpbin.exe' -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match 'Hostx64\\x64' } | Select-Object -First 1
    }
}
if ($dumpbin) {
    $dllPath = Join-Path $nativeOut 'msdf_atlas_c.dll'
    $exports = & $dumpbin.FullName /nologo /exports $dllPath |
        ForEach-Object { if ($_ -match '^\s+\d+\s+[0-9A-Fa-f]+\s+[0-9A-Fa-f]{8}\s+(\S+)') { $Matches[1] } }
    if (-not $exports) { Write-Error "msdf_atlas_c.dll exports nothing." }
    $unexpected = $exports | Where-Object { $_ -notlike 'msdf_atlas_*' }
    if ($unexpected) { Write-Error "Unexpected exports in msdf_atlas_c.dll: $($unexpected -join ', ')" }
    $crt = (& $dumpbin.FullName /nologo /dependents $dllPath) -match 'VCRUNTIME|MSVCP|api-ms-win-crt|ucrtbase'
    if ($crt) { Write-Error "msdf_atlas_c.dll depends on the dynamic CRT: $($crt -join ', ')" }
    Write-Host "[build-native-win] msdf_atlas_c.dll exports $($exports.Count) symbols, all msdf_atlas_*."
}
else {
    Write-Warning "[build-native-win] dumpbin.exe not found; skipping export and CRT checks."
}

& (Join-Path $nativeOut 'msdf-atlas-gen.exe') -version
$global:LASTEXITCODE = 0
exit 0
