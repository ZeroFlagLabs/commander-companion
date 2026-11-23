@echo off
setlocal
set "compiler=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%compiler%" set "compiler=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%compiler%" (
    echo The Windows .NET Framework C# compiler was not found.
    exit /b 1
)
if not exist "%~dp0bin" mkdir "%~dp0bin"
"%compiler%" /nologo /target:winexe /platform:anycpu /optimize+ /warnaserror+ /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll "/win32icon:%~dp0..\resources\Remote_Desktop_Commander_Launcher.ico" "/win32manifest:%~dp0..\resources\Remote_Desktop_Commander_Launcher.manifest" "/resource:%~dp0..\resources\Remote_Desktop_Commander_Launcher.png,LauncherLogo" "/out:%~dp0bin\Remote_Desktop_Commander_Launcher_Usage_Preview.exe" "%~dp0src\Remote_Desktop_Commander_Launcher_Usage_Preview.cs" "%~dp0..\src\App\Remote_Desktop_Commander_Launcher.StatusView.cs" "%~dp0..\src\App\Remote_Desktop_Commander_Launcher.AboutButton.cs"
if errorlevel 1 exit /b %ERRORLEVEL%
call "%~dp0..\build.cmd" -HashesOnly
exit /b %ERRORLEVEL%
