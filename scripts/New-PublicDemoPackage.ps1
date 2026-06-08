param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$OutputRoot = "artifacts\public-demo",
    [switch]$SkipNativeBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$msbuild = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe"
if (-not (Test-Path $msbuild)) {
    $msbuildCommand = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($null -eq $msbuildCommand) {
        throw "MSBuild was not found. Install Visual Studio Build Tools or run from a Developer PowerShell."
    }

    $msbuild = $msbuildCommand.Source
}

function Copy-WinUiBuildResources {
    param(
        [Parameter(Mandatory)][string]$Configuration,
        [Parameter(Mandatory)][string]$Platform,
        [Parameter(Mandatory)][string]$RuntimeId,
        [Parameter(Mandatory)][string]$PublishRoot
    )

    $targetFramework = "net8.0-windows10.0.19041.0"
    $buildOutput = Join-Path $repoRoot "src\PromptInputMethod.App\bin\$Platform\$Configuration\$targetFramework\$RuntimeId"
    if (-not (Test-Path $buildOutput)) {
        throw "WinUI build output was not found: $buildOutput"
    }

    foreach ($required in @("App.xbf", "PromptInputMethod.App.pri", "localization.pri")) {
        $requiredPath = Join-Path $buildOutput $required
        if (-not (Test-Path $requiredPath)) {
            throw "Required WinUI resource was not found: $requiredPath"
        }
    }

    foreach ($pattern in @("*.xbf", "*.pri")) {
        Get-ChildItem -LiteralPath $buildOutput -Filter $pattern -File |
            Where-Object { $_.Name -ne "resources.pri" } |
            ForEach-Object {
                Copy-Item -LiteralPath $_.FullName -Destination $PublishRoot -Force
            }
    }
}

Push-Location $repoRoot
try {
    if (-not $SkipNativeBuild) {
        cargo build -p fire-eye-ocr-worker --manifest-path native\Cargo.toml --release
    }

    $runtimeId = if ($Platform -eq "Win32") { "win-x86" } else { "win-x64" }
    $packageRoot = Join-Path $repoRoot $OutputRoot
    $publishRoot = Join-Path $packageRoot "publish-$Platform"
    if (Test-Path $publishRoot) {
        Remove-Item -LiteralPath $publishRoot -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $publishRoot | Out-Null

    & $msbuild src\PromptInputMethod.App\PromptInputMethod.App.csproj /t:Restore,Publish /p:Configuration=$Configuration /p:Platform=$Platform /p:RuntimeIdentifier=$runtimeId /p:SelfContained=true /p:PublishSelfContained=true /p:PublishDir="$publishRoot" /m /v:minimal
    if ($LASTEXITCODE -ne 0) {
        throw "MSBuild failed with exit code $LASTEXITCODE."
    }
    Copy-WinUiBuildResources -Configuration $Configuration -Platform $Platform -RuntimeId $runtimeId -PublishRoot $publishRoot

    $targetDir = $publishRoot
    if (-not (Test-Path $targetDir)) {
        throw "Build output was not found: $targetDir"
    }

    $staging = Join-Path $packageRoot "AI-Quick-Prompt-1.0.6-$Platform"
    if (Test-Path $staging) {
        Remove-Item -LiteralPath $staging -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $staging | Out-Null
    Copy-Item -Path (Join-Path $targetDir "*") -Destination $staging -Recurse -Force

    $noticeDir = Join-Path $staging "release-notices"
    New-Item -ItemType Directory -Force -Path $noticeDir | Out-Null
    foreach ($file in @(
        "LICENSE",
        "NOTICE.md",
        "THIRD_PARTY_NOTICES.md",
        "README.md",
        "docs\license-inventory.md",
        "docs\ocr-model-license-review.md",
        "docs\privacy.md"
    )) {
        $source = Join-Path $repoRoot $file
        if (Test-Path $source) {
            $destination = Join-Path $noticeDir $file
            New-Item -ItemType Directory -Force -Path (Split-Path $destination) | Out-Null
            Copy-Item -LiteralPath $source -Destination $destination -Force
        }
    }

    $licenseRoot = Join-Path $noticeDir "native-licenses"
    foreach ($file in @(
        "native\ocr-rs-patched\LICENSE",
        "native\ocr-rs-patched\3rd_party\MNN\LICENSE.txt"
    )) {
        $source = Join-Path $repoRoot $file
        if (Test-Path $source) {
            $destination = Join-Path $licenseRoot $file
            New-Item -ItemType Directory -Force -Path (Split-Path $destination) | Out-Null
            Copy-Item -LiteralPath $source -Destination $destination -Force
        }
    }

    Get-ChildItem -Path (Join-Path $repoRoot "native\ocr-rs-patched\3rd_party\MNN") -Recurse -File -Include "LICENSE", "LICENSE.txt", "license.txt" |
        ForEach-Object {
            $relative = $_.FullName.Substring($repoRoot.Path.Length + 1)
            $destination = Join-Path $licenseRoot $relative
            New-Item -ItemType Directory -Force -Path (Split-Path $destination) | Out-Null
            Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
        }

    $worker = Join-Path $staging "fire-eye-ocr-worker.exe"
    if (-not (Test-Path $worker)) {
        Write-Warning "fire-eye-ocr-worker.exe is not in the package. Build native OCR first if this demo should include Fire Eye OCR."
    }

    $zipPath = "$staging.zip"
    if (Test-Path $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    Compress-Archive -Path (Join-Path $staging "*") -DestinationPath $zipPath -Force
    Write-Host "Public demo package created: $zipPath"
}
finally {
    Pop-Location
}
