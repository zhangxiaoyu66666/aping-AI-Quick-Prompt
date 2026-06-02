param(
    [string]$Configuration = "Release",
    [ValidateSet("x64", "Win32")]
    [string]$Platform = "x64",
    [string]$Version = "1.0.0.0",
    [string]$PackageName = "XiaYuStudio.AIQuickPrompt",
    [string]$Publisher = "CN=XiaYu Studio",
    [string]$PublisherDisplayName = "XiaYu Studio",
[string]$DisplayName = "AI Quick Prompt",
    [string]$Description = "AI Quick Prompt prompt workbench.",
    [string]$OutputRoot = "artifacts\store",
    [string]$CertificateThumbprint = "",
    [string]$PfxPath = "",
    [string]$PfxPassword = "",
    [switch]$SkipNativeBuild,
    [switch]$AllowPlaceholderIdentity
)

$ErrorActionPreference = "Stop"

function Get-WindowsSdkTool {
    param([Parameter(Mandatory)][string]$ToolName)

    $sdkRoot = "C:\Program Files (x86)\Windows Kits\10\bin"
    if (-not (Test-Path $sdkRoot)) {
        throw "Windows SDK bin folder was not found: $sdkRoot"
    }

    $tool = Get-ChildItem -LiteralPath $sdkRoot -Directory |
        Sort-Object Name -Descending |
        ForEach-Object { Join-Path $_.FullName "x64\$ToolName" } |
        Where-Object { Test-Path $_ } |
        Select-Object -First 1

    if ([string]::IsNullOrWhiteSpace($tool)) {
        throw "$ToolName was not found under $sdkRoot."
    }

    return $tool
}

function Escape-Xml {
    param([string]$Value)
    return [System.Security.SecurityElement]::Escape($Value)
}

function New-LogoAsset {
    param(
        [Parameter(Mandatory)][string]$SourcePath,
        [Parameter(Mandatory)][string]$DestinationPath,
        [Parameter(Mandatory)][int]$Width,
        [Parameter(Mandatory)][int]$Height
    )

    Add-Type -AssemblyName System.Drawing

    $source = [System.Drawing.Image]::FromFile($SourcePath)
    try {
        $bitmap = New-Object System.Drawing.Bitmap $Width, $Height
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

                $scale = [Math]::Min($Width / $source.Width, $Height / $source.Height)
                $drawWidth = [int][Math]::Round($source.Width * $scale)
                $drawHeight = [int][Math]::Round($source.Height * $scale)
                $x = [int][Math]::Round(($Width - $drawWidth) / 2)
                $y = [int][Math]::Round(($Height - $drawHeight) / 2)

                $graphics.DrawImage($source, $x, $y, $drawWidth, $drawHeight)
            }
            finally {
                $graphics.Dispose()
            }

            $destinationDir = Split-Path -Parent $DestinationPath
            New-Item -ItemType Directory -Force -Path $destinationDir | Out-Null
            $bitmap.Save($DestinationPath, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally {
            $bitmap.Dispose()
        }
    }
    finally {
        $source.Dispose()
    }
}

if (-not $AllowPlaceholderIdentity) {
    if ($PackageName -eq "XiaYuStudio.AIQuickPrompt" -or $Publisher -eq "CN=XiaYu Studio") {
        throw "Pass the real Partner Center Package/Identity/Name and Publisher, or use -AllowPlaceholderIdentity only for local packaging smoke tests."
    }
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$makeAppx = Get-WindowsSdkTool "makeappx.exe"
$signTool = Get-WindowsSdkTool "signtool.exe"

$msbuild = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe"
if (-not (Test-Path $msbuild)) {
    $msbuildCommand = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($null -eq $msbuildCommand) {
        throw "MSBuild was not found. Install Visual Studio Build Tools or run from a Developer PowerShell."
    }

    $msbuild = $msbuildCommand.Source
}

Push-Location $repoRoot
try {
    if (-not $SkipNativeBuild) {
        cargo build -p fire-eye-ocr-worker --manifest-path native\Cargo.toml --release
    }

    & $msbuild src\PromptInputMethod.App\PromptInputMethod.App.csproj /t:Restore,Build /p:Configuration=$Configuration /p:Platform=$Platform /m /v:minimal
    if ($LASTEXITCODE -ne 0) {
        throw "MSBuild failed with exit code $LASTEXITCODE."
    }

    $runtimeId = if ($Platform -eq "Win32") { "win-x86" } else { "win-x64" }
    $architecture = if ($Platform -eq "Win32") { "x86" } else { "x64" }
    $targetDir = Join-Path $repoRoot "src\PromptInputMethod.App\bin\$Platform\$Configuration\net8.0-windows10.0.19041.0\$runtimeId"
    if (-not (Test-Path $targetDir)) {
        throw "Build output was not found: $targetDir"
    }

    $packageRoot = Join-Path $repoRoot $OutputRoot
    $staging = Join-Path $packageRoot "staging-$architecture"
    $uploadStaging = Join-Path $packageRoot "upload-$architecture"
    foreach ($path in @($staging, $uploadStaging)) {
        if (Test-Path $path) {
            Remove-Item -LiteralPath $path -Recurse -Force
        }
        New-Item -ItemType Directory -Force -Path $path | Out-Null
    }

    Copy-Item -Path (Join-Path $targetDir "*") -Destination $staging -Recurse -Force

    $sourceLogo = Join-Path $repoRoot "src\PromptInputMethod.App\Assets\logo.png"
    $storeAssetDir = Join-Path $staging "Assets\Store"
    New-LogoAsset $sourceLogo (Join-Path $storeAssetDir "Square44x44Logo.png") 44 44
    New-LogoAsset $sourceLogo (Join-Path $storeAssetDir "Square71x71Logo.png") 71 71
    New-LogoAsset $sourceLogo (Join-Path $storeAssetDir "Square150x150Logo.png") 150 150
    New-LogoAsset $sourceLogo (Join-Path $storeAssetDir "Square310x310Logo.png") 310 310
    New-LogoAsset $sourceLogo (Join-Path $storeAssetDir "StoreLogo.png") 50 50
    New-LogoAsset $sourceLogo (Join-Path $storeAssetDir "Wide310x150Logo.png") 310 150

    Get-ChildItem -LiteralPath $staging -Filter *.pdb -Recurse -ErrorAction SilentlyContinue |
        Remove-Item -Force

    $manifestPath = Join-Path $staging "AppxManifest.xml"
    $manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Package
  xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
  xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
  xmlns:desktop="http://schemas.microsoft.com/appx/manifest/desktop/windows10"
  xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
  IgnorableNamespaces="uap desktop rescap">
  <Identity Name="$(Escape-Xml $PackageName)" Publisher="$(Escape-Xml $Publisher)" Version="$(Escape-Xml $Version)" ProcessorArchitecture="$architecture" />
  <Properties>
    <DisplayName>$(Escape-Xml $DisplayName)</DisplayName>
    <PublisherDisplayName>$(Escape-Xml $PublisherDisplayName)</PublisherDisplayName>
    <Logo>Assets\Store\StoreLogo.png</Logo>
  </Properties>
  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.17763.0" MaxVersionTested="10.0.26100.0" />
  </Dependencies>
  <Resources>
    <Resource Language="zh-CN" />
    <Resource Language="en-US" />
  </Resources>
  <Applications>
    <Application Id="App" Executable="PromptInputMethod.App.exe" EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements
        DisplayName="$(Escape-Xml $DisplayName)"
        Description="$(Escape-Xml $Description)"
        BackgroundColor="transparent"
        Square44x44Logo="Assets\Store\Square44x44Logo.png"
        Square150x150Logo="Assets\Store\Square150x150Logo.png"
        AppListEntry="default">
        <uap:DefaultTile
          Wide310x150Logo="Assets\Store\Wide310x150Logo.png"
          Square71x71Logo="Assets\Store\Square71x71Logo.png"
          Square310x310Logo="Assets\Store\Square310x310Logo.png" />
      </uap:VisualElements>
      <Extensions>
        <desktop:Extension Category="windows.fullTrustProcess" Executable="PromptInputMethod.App.exe" />
      </Extensions>
    </Application>
  </Applications>
  <Capabilities>
    <rescap:Capability Name="runFullTrust" />
  </Capabilities>
</Package>
"@
    Set-Content -LiteralPath $manifestPath -Value $manifest -Encoding UTF8

    $packageBaseName = "AI-Quick-Prompt-Store-$Version-$architecture"
    $packagePath = Join-Path $packageRoot "$packageBaseName.msix"
    $appxSymPath = Join-Path $packageRoot "$packageBaseName.appxsym"
    $uploadPath = Join-Path $packageRoot "$packageBaseName.msixupload"
    foreach ($path in @($packagePath, $appxSymPath, $uploadPath)) {
        if (Test-Path $path) {
            Remove-Item -LiteralPath $path -Force
        }
    }

    & $makeAppx pack /d $staging /p $packagePath /o
    if ($LASTEXITCODE -ne 0) {
        throw "makeappx failed with exit code $LASTEXITCODE."
    }

    $signed = $false
    if (-not [string]::IsNullOrWhiteSpace($PfxPath)) {
        $signArgs = @("sign", "/fd", "SHA256", "/f", $PfxPath)
        if (-not [string]::IsNullOrWhiteSpace($PfxPassword)) {
            $signArgs += @("/p", $PfxPassword)
        }
        $signArgs += $packagePath
        & $signTool @signArgs
        if ($LASTEXITCODE -ne 0) {
            throw "signtool failed with exit code $LASTEXITCODE."
        }
        $signed = $true
    }
    elseif (-not [string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
        & $signTool sign /fd SHA256 /sha1 $CertificateThumbprint $packagePath
        if ($LASTEXITCODE -ne 0) {
            throw "signtool failed with exit code $LASTEXITCODE."
        }
        $signed = $true
    }
    else {
        Write-Warning "The MSIX was not signed. Partner Center can sign Store-delivered packages, but local installation will require a signed package."
    }

    $pdbs = Get-ChildItem -LiteralPath $targetDir -Filter *.pdb -Recurse -ErrorAction SilentlyContinue
    if ($pdbs.Count -gt 0) {
        $symbolStaging = Join-Path $packageRoot "symbols-$architecture"
        if (Test-Path $symbolStaging) {
            Remove-Item -LiteralPath $symbolStaging -Recurse -Force
        }
        New-Item -ItemType Directory -Force -Path $symbolStaging | Out-Null
        foreach ($pdb in $pdbs) {
            Copy-Item -LiteralPath $pdb.FullName -Destination $symbolStaging -Force
        }
        $appxSymZipPath = Join-Path $packageRoot "$packageBaseName.appxsym.zip"
        if (Test-Path $appxSymZipPath) {
            Remove-Item -LiteralPath $appxSymZipPath -Force
        }
        Compress-Archive -Path (Join-Path $symbolStaging "*") -DestinationPath $appxSymZipPath -Force
        Move-Item -LiteralPath $appxSymZipPath -Destination $appxSymPath -Force
    }

    Copy-Item -LiteralPath $packagePath -Destination $uploadStaging -Force
    if (Test-Path $appxSymPath) {
        Copy-Item -LiteralPath $appxSymPath -Destination $uploadStaging -Force
    }

    $uploadZip = Join-Path $packageRoot "$packageBaseName.zip"
    if (Test-Path $uploadZip) {
        Remove-Item -LiteralPath $uploadZip -Force
    }
    Compress-Archive -Path (Join-Path $uploadStaging "*") -DestinationPath $uploadZip -Force
    Move-Item -LiteralPath $uploadZip -Destination $uploadPath -Force

    [PSCustomObject]@{
        MsixUpload = $uploadPath
        Msix = $packagePath
        AppxSym = if (Test-Path $appxSymPath) { $appxSymPath } else { "" }
        Signed = $signed
        PackageName = $PackageName
        Publisher = $Publisher
        DisplayName = $DisplayName
        Architecture = $architecture
        Version = $Version
    } | Format-List
}
finally {
    Pop-Location
}
