param(
    [string]$WebView2Version = "1.0.2365.46",
    [string]$WebView2Sdk = ""
)

$ErrorActionPreference = "Stop"

$appRoot = $PSScriptRoot
$repoRoot = Split-Path -Parent $appRoot
$src = Join-Path $appRoot "src"
$dist = Join-Path $appRoot "dist"
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"

function Get-WebView2SdkDirectory {
    param([string]$Version)

    if (-not [string]::IsNullOrWhiteSpace($WebView2Sdk)) {
        return $WebView2Sdk
    }

    $cacheRoot = Join-Path $repoRoot ".packages\webview2"
    $packageRoot = Join-Path $cacheRoot "extract-$Version"
    $core = Join-Path $packageRoot "lib\net45\Microsoft.Web.WebView2.Core.dll"
    if (Test-Path $core) {
        return $packageRoot
    }

    New-Item -ItemType Directory -Force -Path $cacheRoot | Out-Null
    $nupkg = Join-Path $cacheRoot "microsoft.web.webview2.$Version.nupkg"
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    function Test-Nupkg {
        param([string]$Path)
        if (-not (Test-Path $Path)) {
            return $false
        }
        try {
            $archive = [System.IO.Compression.ZipFile]::OpenRead($Path)
            $archive.Dispose()
            return $true
        }
        catch {
            return $false
        }
    }

    if (-not (Test-Nupkg $nupkg)) {
        if (Test-Path $nupkg) {
            Remove-Item -LiteralPath $nupkg -Force
        }

        $url = "https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/$Version/microsoft.web.webview2.$Version.nupkg"
        $part = "$nupkg.part"
        if (Test-Path $part) {
            Remove-Item -LiteralPath $part -Force
        }

        Write-Host "下载 WebView2 SDK $Version..." -ForegroundColor DarkGray
        $downloaded = $false

        if (Get-Command curl.exe -ErrorAction SilentlyContinue) {
            & curl.exe -L --fail --retry 3 --retry-delay 2 --max-time 300 --silent --show-error -o $part $url
            if ($LASTEXITCODE -eq 0 -and (Test-Nupkg $part)) {
                Move-Item -LiteralPath $part -Destination $nupkg -Force
                $downloaded = $true
            }
            elseif (Test-Path $part) {
                Remove-Item -LiteralPath $part -Force
            }
        }

        if (-not $downloaded) {
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
            try {
                Invoke-WebRequest -Uri $url -OutFile $part -UseBasicParsing -TimeoutSec 300
                if (Test-Nupkg $part) {
                    Move-Item -LiteralPath $part -Destination $nupkg -Force
                    $downloaded = $true
                }
            }
            catch {
            }
            finally {
                if (Test-Path $part) {
                    Remove-Item -LiteralPath $part -Force
                }
            }
        }

        if (-not $downloaded) {
            throw "下载 WebView2 SDK 失败，请检查网络，或使用 -WebView2Sdk 指定本地 SDK 目录。"
        }
    }

    $extract = Join-Path $cacheRoot "extract-$Version"
    if (Test-Path $extract) {
        Remove-Item -LiteralPath $extract -Recurse -Force
    }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($nupkg, $extract)
    return $extract
}

$sdk = Get-WebView2SdkDirectory -Version $WebView2Version
if (Test-Path (Join-Path $sdk "Microsoft.Web.WebView2.Core.dll")) {
    $coreDll = Join-Path $sdk "Microsoft.Web.WebView2.Core.dll"
    $winFormsDll = Join-Path $sdk "Microsoft.Web.WebView2.WinForms.dll"
    $loaderDll = Join-Path $sdk "WebView2Loader.dll"
}
else {
    $coreDll = Join-Path $sdk "lib\net45\Microsoft.Web.WebView2.Core.dll"
    $winFormsDll = Join-Path $sdk "lib\net45\Microsoft.Web.WebView2.WinForms.dll"
    $loaderDll = Join-Path $sdk "runtimes\win-x64\native\WebView2Loader.dll"
    if (-not (Test-Path $loaderDll)) {
        $loaderDll = Join-Path $sdk "build\native\x64\WebView2Loader.dll"
    }
}
if (-not (Test-Path $coreDll) -or -not (Test-Path $winFormsDll) -or -not (Test-Path $loaderDll)) {
    throw "WebView2 SDK 文件不完整。请检查版本或 -WebView2Sdk 目录。"
}

if (Test-Path $dist) {
    Remove-Item -LiteralPath $dist -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $dist | Out-Null

Copy-Item $coreDll $dist
Copy-Item $winFormsDll $dist
Copy-Item $loaderDll $dist

$arguments = @(
    "/nologo",
    "/target:winexe",
    "/platform:x64",
    "/out:$dist\BilibiliMediaApp.exe",
    "/win32icon:$src\app.ico",
    "/resource:$src\control.js,ControlScript",
    "/resource:$src\app.ico,AppIcon",
    "/reference:System.dll",
    "/reference:System.Core.dll",
    "/reference:System.Windows.Forms.dll",
    "/reference:System.Drawing.dll",
    "/reference:System.Web.Extensions.dll",
    "/reference:$dist\Microsoft.Web.WebView2.Core.dll",
    "/reference:$dist\Microsoft.Web.WebView2.WinForms.dll",
    "$src\Program.cs",
    "$src\MainForm.cs",
    "$src\SettingsDialog.cs",
    "$src\HotkeyBinding.cs",
    "$src\HotkeyCaptureBox.cs",
    "$src\AppSettings.cs",
    "$src\NativeMethods.cs",
    "$src\AssemblyInfo.cs"
)

& $csc @arguments
if ($LASTEXITCODE -ne 0) {
    throw "C# 编译失败"
}

$license = Join-Path $repoRoot "LICENSE"
if (Test-Path $license) {
    Copy-Item $license (Join-Path $dist "LICENSE.txt")
}
$notices = Join-Path $repoRoot "THIRD-PARTY-NOTICES.txt"
if (Test-Path $notices) {
    Copy-Item $notices (Join-Path $dist "THIRD-PARTY-NOTICES.txt")
}

Write-Host "构建完成：$dist" -ForegroundColor Green
Get-ChildItem $dist | Select-Object Name, Length | Format-Table -AutoSize
