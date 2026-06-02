param(
    [string]$Configuration = "Debug",
    [string]$Platform = "x64",
    [string]$OutputDir = "artifacts\ui-regression",
    [int]$DelaySeconds = 2
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$runtimeId = if ($Platform -eq "Win32") { "win-x86" } else { "win-x64" }
$appExe = Join-Path $repoRoot "src\PromptInputMethod.App\bin\$Platform\$Configuration\net8.0-windows10.0.19041.0\$runtimeId\PromptInputMethod.App.exe"
if (-not (Test-Path $appExe)) {
    throw "App executable was not found. Build the app first: $appExe"
}

$captureRoot = Join-Path $repoRoot $OutputDir
New-Item -ItemType Directory -Force -Path $captureRoot | Out-Null

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;

public static class AipinWindowNative
{
    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
"@

function Wait-ForMainWindow {
    param([System.Diagnostics.Process]$Process)

    for ($i = 0; $i -lt 40; $i++) {
        $Process.Refresh()
        if ($Process.MainWindowHandle -ne [IntPtr]::Zero) {
            return $Process.MainWindowHandle
        }

        Start-Sleep -Milliseconds 250
    }

    throw "PromptInputMethod.App did not expose a main window handle."
}

function Capture-Window {
    param(
        [IntPtr]$Handle,
        [string]$Name,
        [int]$Width,
        [int]$Height
    )

    [AipinWindowNative]::SetWindowPos($Handle, [IntPtr]::Zero, 80, 80, $Width, $Height, 0x0040) | Out-Null
    Start-Sleep -Seconds $DelaySeconds

    $rect = New-Object AipinWindowNative+RECT
    [AipinWindowNative]::GetWindowRect($Handle, [ref]$rect) | Out-Null
    $captureWidth = [Math]::Max(1, $rect.Right - $rect.Left)
    $captureHeight = [Math]::Max(1, $rect.Bottom - $rect.Top)

    $bitmap = New-Object System.Drawing.Bitmap $captureWidth, $captureHeight
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bitmap.Size)
        $path = Join-Path $captureRoot "$Name.png"
        $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
        $hash = (Get-FileHash $path -Algorithm SHA256).Hash
        [PSCustomObject]@{
            Name = $Name
            Path = $path
            Width = $captureWidth
            Height = $captureHeight
            Sha256 = $hash
        }
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$process = Get-Process PromptInputMethod.App -ErrorAction SilentlyContinue | Select-Object -First 1
$startedHere = $false
if ($null -eq $process) {
    $process = Start-Process -FilePath $appExe -PassThru
    $startedHere = $true
}

$handle = Wait-ForMainWindow $process
$results = @()
$results += Capture-Window -Handle $handle -Name "expanded-1440x900" -Width 1440 -Height 900
$results += Capture-Window -Handle $handle -Name "compact-1024x700" -Width 1024 -Height 700
$results += Capture-Window -Handle $handle -Name "narrow-760x820" -Width 760 -Height 820
$results += Capture-Window -Handle $handle -Name "high-dpi-current-display" -Width 1280 -Height 800

$manifestPath = Join-Path $captureRoot "manifest.json"
$results | ConvertTo-Json -Depth 4 | Set-Content -Path $manifestPath -Encoding UTF8
$results | Format-Table -AutoSize
Write-Host "Screenshot manifest: $manifestPath"

if ($startedHere) {
    Write-Host "The app was started for screenshots and is left open for visual review."
}
