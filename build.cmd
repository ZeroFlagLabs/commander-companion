@echo off
setlocal
REM One build file: PowerShell logic is embedded below, with no separate PS1.
set "RDC_BUILD_FILE=%~f0"
set "RDC_BUILD_MODE=%~1"
if not "%~2"=="" (
    echo Usage: build.cmd [-VerifyHashes ^| -HashesOnly]
    exit /b 2
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; try { $text=[IO.File]::ReadAllText($env:RDC_BUILD_FILE); $match=[regex]::Match($text,'(?m)^# BUILD_PAYLOAD\r?$'); if(-not $match.Success){throw 'Build payload is missing.'}; & ([ScriptBlock]::Create($text.Substring($match.Index+$match.Length))) } catch { [Console]::Error.WriteLine($_.Exception.Message); exit 1 }"
exit /b %ERRORLEVEL%

# BUILD_PAYLOAD
$mode = [string]$env:RDC_BUILD_MODE
if ($mode -notin @('', '-VerifyHashes', '-HashesOnly')) {
    throw 'Usage: build.cmd [-VerifyHashes | -HashesOnly]'
}
$VerifyHashes = $mode -eq '-VerifyHashes'
$HashesOnly = $mode -eq '-HashesOnly'
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath([IO.Path]::GetDirectoryName($env:RDC_BUILD_FILE))
$prefix = 'Remote_Desktop_Commander_Launcher'
$manifestPath = Join-Path $projectRoot 'SHA256SUMS.txt'
$utf8 = New-Object Text.UTF8Encoding($false)

# Use .NET directly so hashing also works in a clean Windows PowerShell session.
function Get-Sha256([string]$path) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    $stream = $null
    try {
        $stream = [IO.File]::OpenRead($path)
        ([BitConverter]::ToString($algorithm.ComputeHash($stream))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        if ($stream) { $stream.Dispose() }
        $algorithm.Dispose()
    }
}

function Get-ProjectFiles {
    # Only shipped files are hashed. Temporary compiler files are never releases.
    Get-ChildItem -LiteralPath $projectRoot -Recurse -File |
        Where-Object {
            $_.FullName -ne $manifestPath -and
            -not $_.FullName.StartsWith((Join-Path $projectRoot 'obj\'), [StringComparison]::OrdinalIgnoreCase) -and
            -not $_.FullName.StartsWith((Join-Path $projectRoot 'bin\'), [StringComparison]::OrdinalIgnoreCase) -and
            -not $_.FullName.StartsWith((Join-Path $projectRoot 'preview\bin\'), [StringComparison]::OrdinalIgnoreCase) -and
            -not $_.FullName.StartsWith((Join-Path $projectRoot '.git\'), [StringComparison]::OrdinalIgnoreCase)
        } | Sort-Object FullName
}

function Get-RelativeName($file) {
    $file.FullName.Substring($projectRoot.Length + 1).Replace('\', '/')
}

if ($VerifyHashes) {
    if (-not (Test-Path -LiteralPath $manifestPath)) { throw 'SHA256SUMS.txt is missing.' }
    $expected = @{}
    foreach ($line in [IO.File]::ReadAllLines($manifestPath)) {
        if ($line -notmatch '^([0-9a-fA-F]{64})  (.+)$') { throw "Invalid hash entry: $line" }
        if ($expected.ContainsKey($Matches[2])) { throw 'Duplicate hash entry.' }
        $expected[$Matches[2]] = $Matches[1]
    }
    $files = @(Get-ProjectFiles)
    if ($files.Count -ne $expected.Count) { throw 'The project file list differs from SHA256SUMS.txt.' }
    foreach ($file in $files) {
        $name = Get-RelativeName $file
        if (-not $expected.ContainsKey($name) -or (Get-Sha256 $file.FullName) -ne $expected[$name]) {
            throw "Hash check failed: $name"
        }
    }
    Write-Output "SHA-256 verified: $($files.Count) files."
    return
}

if (-not $HashesOnly) {
    $launcher = Join-Path $projectRoot 'scripts\DesktopCommander-Remote.cmd'
    if (-not (Test-Path -LiteralPath $launcher -PathType Leaf)) {
        throw 'The Commander launcher is missing from the scripts folder.'
    }

    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    if (-not (Test-Path -LiteralPath $compiler)) {
        $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
    }
    if (-not (Test-Path -LiteralPath $compiler)) { throw 'The Windows .NET Framework C# compiler was not found.' }

    $objectRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'obj'))
    # Guard cleanup before using a recursive delete, even though obj is a fixed child.
    if ($objectRoot -ne "$projectRoot\obj") { throw 'Unexpected temporary build path.' }
    New-Item -ItemType Directory -Path $objectRoot -Force | Out-Null
    try {
        $testExe = Join-Path $objectRoot "$prefix.Tests.exe"
        $testSources = @(
            (Join-Path $projectRoot "tests\$prefix.Tests.cs"),
            (Join-Path $projectRoot "tests\$prefix.UsageTests.cs"),
            (Join-Path $projectRoot "tests\$prefix.ReleaseTests.cs"),
            (Join-Path $projectRoot "src\App\$prefix.AppInfo.cs"),
            (Join-Path $projectRoot "src\App\$prefix.ReleaseInfo.cs"),
            (Join-Path $projectRoot "src\Usage\$prefix.UsageSnapshot.cs"),
            (Join-Path $projectRoot "src\Connection\$prefix.AgentMatcher.cs"),
            (Join-Path $projectRoot "src\Terminal\$prefix.ConsoleCell.cs"),
            (Join-Path $projectRoot "src\Terminal\$prefix.ConsoleFrame.cs")
        )
        & $compiler /nologo /target:exe /optimize+ /warnaserror+ /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll "/out:$testExe" @testSources
        if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
        & $testExe
        if ($LASTEXITCODE -ne 0) { throw 'Tests failed. The current app has not been replaced.' }

        $sources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Recurse -Filter '*.cs' -File | Sort-Object FullName | ForEach-Object { $_.FullName })
        $preparedExe = Join-Path $objectRoot "$prefix.exe"
        $icon = Join-Path $projectRoot "resources\$prefix.ico"
        $manifest = Join-Path $projectRoot "resources\$prefix.manifest"
        $webViewRoot = Join-Path $projectRoot 'lib\WebView2'
        $core = Join-Path $webViewRoot 'Microsoft.Web.WebView2.Core.dll'
        $forms = Join-Path $webViewRoot 'Microsoft.Web.WebView2.WinForms.dll'
        $notices = Join-Path $objectRoot 'Notices.txt'
        $noticeParts = @('LICENSE', 'THIRD_PARTY_NOTICES.md', 'lib\WebView2\LICENSE.txt', 'lib\WebView2\NOTICE.txt') |
            ForEach-Object { $_ + [Environment]::NewLine + ('=' * 72) + [Environment]::NewLine + [IO.File]::ReadAllText((Join-Path $projectRoot $_)) }
        [IO.File]::WriteAllText($notices, ($noticeParts -join ([Environment]::NewLine + [Environment]::NewLine)), $utf8)
        $payload = @(
            "/resource:$core,Payload.Core",
            "/resource:$forms,Payload.Forms",
            "/resource:$webViewRoot\x64\WebView2Loader.dll,Payload.Loader.x64",
            "/resource:$webViewRoot\x86\WebView2Loader.dll,Payload.Loader.x86",
            "/resource:$webViewRoot\arm64\WebView2Loader.dll,Payload.Loader.arm64",
            "/resource:$projectRoot\resources\$prefix.png,Payload.Logo",
            "/resource:$launcher,Payload.Script",
            "/resource:$notices,Payload.Notices"
        )
        & $compiler /nologo /target:winexe /platform:anycpu /optimize+ /warnaserror+ /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Management.dll /reference:System.Core.dll /reference:System.Web.Extensions.dll "/reference:$core" "/reference:$forms" "/win32icon:$icon" "/win32manifest:$manifest" "/out:$preparedExe" @payload @sources
        if ($LASTEXITCODE -ne 0) { throw 'App compilation failed. The current app has not been replaced.' }
        $binaryRoot = Join-Path $projectRoot 'bin'
        New-Item -ItemType Directory -Path $binaryRoot -Force | Out-Null
        $output = Join-Path $binaryRoot 'CommanderCompanion.exe'
        Copy-Item -LiteralPath $preparedExe -Destination $output -Force
        Write-Output "Built: $output"
    }
    finally {
        if (Test-Path -LiteralPath $objectRoot) { Remove-Item -LiteralPath $objectRoot -Recurse -Force }
    }
}

$lines = @(Get-ProjectFiles | ForEach-Object {
    '{0}  {1}' -f (Get-Sha256 $_.FullName), (Get-RelativeName $_)
})
[IO.File]::WriteAllLines($manifestPath, [string[]]$lines, $utf8)
Write-Output "Saved SHA-256 hashes for $($lines.Count) files."
