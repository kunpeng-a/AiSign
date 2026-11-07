@echo off
setlocal EnableExtensions
cd /d "%~dp0"

set "FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
set "WPF=%FW%\WPF"
if not exist "%FW%\csc.exe" (
  set "FW=%WINDIR%\Microsoft.NET\Framework\v4.0.30319"
  set "WPF=%FW%\WPF"
)

if not exist "%FW%\csc.exe" (
  echo [ERROR] csc.exe not found. .NET Framework 4.x is required.
  exit /b 1
)
if not exist "%WPF%\PresentationFramework.dll" (
  echo [ERROR] WPF assemblies not found: %WPF%
  exit /b 1
)

taskkill /IM AiSign.exe /F >nul 2>&1

pushd src
dir /s /b *.cs > ..\sources.rsp
popd

"%FW%\csc.exe" /nologo /langversion:5 /target:winexe /platform:anycpu /optimize+ /debug- /warn:4 ^
  /out:AiSign.exe ^
  /win32icon:app.ico ^
  /win32manifest:app.manifest ^
  /lib:"%FW%" /lib:"%WPF%" ^
  /r:System.dll /r:System.Core.dll /r:System.Xml.dll /r:System.Drawing.dll ^
  /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /r:System.Security.dll ^
  /r:WindowsBase.dll /r:PresentationCore.dll /r:PresentationFramework.dll /r:System.Xaml.dll ^
  @sources.rsp

set "RC=%ERRORLEVEL%"
del /q sources.rsp >nul 2>&1
if not "%RC%"=="0" (
  echo [ERROR] BUILD FAILED
  exit /b 1
)
echo [OK] BUILD OK
for %%F in (AiSign.exe) do echo [OK] %%~zF bytes
exit /b 0
