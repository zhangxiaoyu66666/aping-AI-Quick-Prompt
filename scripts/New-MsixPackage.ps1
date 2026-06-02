param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$Version = "1.0.2.0",
    [string]$Publisher = "CN=XiaYu Studio",
    [string]$PackageName = "XiaYuStudio.AIQuickPrompt",
    [string]$OutputRoot = "artifacts\msix",
    [switch]$SkipNativeBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$windowsKitBin = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64"
$makeAppx = Join-Path $windowsKitBin "makeappx.exe"
$signTool = Join-Path $windowsKitBin "signtool.exe"
if (-not (Test-Path $makeAppx) -or -not (Test-Path $signTool)) {
    throw "Windows SDK 10.0.26100.0 makeappx.exe/signtool.exe was not found."
}

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
        if ($LASTEXITCODE -ne 0) {
            throw "Native OCR worker build failed with exit code $LASTEXITCODE."
        }
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
    $versionLabel = $Version.TrimStart("v")
    $staging = Join-Path $packageRoot "staging-$architecture"
    if (Test-Path $staging) {
        Remove-Item -LiteralPath $staging -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $staging | Out-Null
    Copy-Item -Path (Join-Path $targetDir "*") -Destination $staging -Recurse -Force

    $manifestPath = Join-Path $staging "AppxManifest.xml"
    $manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Package
  xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
  xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
  xmlns:desktop="http://schemas.microsoft.com/appx/manifest/desktop/windows10"
  xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
  IgnorableNamespaces="uap desktop rescap">
  <Identity Name="$PackageName" Publisher="$Publisher" Version="$Version" ProcessorArchitecture="$architecture" />
  <Properties>
    <DisplayName>AI Quick Prompt</DisplayName>
    <PublisherDisplayName>XiaYu Studio</PublisherDisplayName>
    <Logo>Assets\logo.png</Logo>
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
        DisplayName="AI Quick Prompt"
        Description="A local-first Windows prompt workbench."
        BackgroundColor="transparent"
        Square44x44Logo="Assets\logo.png"
        Square150x150Logo="Assets\logo.png"
        AppListEntry="default" />
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

    $packagePath = Join-Path $packageRoot "AI-Quick-Prompt-$versionLabel-$architecture.msix"
    if (Test-Path $packagePath) {
        Remove-Item -LiteralPath $packagePath -Force
    }

    & $makeAppx pack /d $staging /p $packagePath /o
    if ($LASTEXITCODE -ne 0) {
        throw "makeappx failed with exit code $LASTEXITCODE."
    }

    $certDir = Join-Path $packageRoot "certificate"
    New-Item -ItemType Directory -Force -Path $certDir | Out-Null
    $cert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $Publisher `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -FriendlyName "AI Quick Prompt $versionLabel MSIX Sideloading Certificate" `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -KeyExportPolicy Exportable `
        -KeyUsage DigitalSignature `
        -NotAfter (Get-Date).AddYears(3)

    $passwordText = [Guid]::NewGuid().ToString("N")
    $password = ConvertTo-SecureString $passwordText -AsPlainText -Force
    $pfxPath = Join-Path $certDir "AI-Quick-Prompt-$versionLabel-sideloading.pfx"
    $cerPath = Join-Path $certDir "AI-Quick-Prompt-$versionLabel-sideloading.cer"
    Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $password | Out-Null
    Export-Certificate -Cert $cert -FilePath $cerPath | Out-Null

    & $signTool sign /fd SHA256 /f $pfxPath /p $passwordText $packagePath
    if ($LASTEXITCODE -ne 0) {
        throw "signtool failed with exit code $LASTEXITCODE."
    }

    Remove-Item -LiteralPath $pfxPath -Force
    $signature = Get-AuthenticodeSignature -FilePath $packagePath
    [PSCustomObject]@{
        Package = $packagePath
        Certificate = $cerPath
        SignatureStatus = $signature.Status
        Signer = $signature.SignerCertificate.Subject
    } | Format-List
}
finally {
    Pop-Location
}
