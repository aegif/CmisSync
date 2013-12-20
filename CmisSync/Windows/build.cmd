@echo off

set WinDirNet=%WinDir%\Microsoft.NET\Framework
set msbuild="%WinDirNet%\v4.0\msbuild.exe"
if not exist %msbuild% set msbuild="%WinDirNet%\v4.0.30319\msbuild.exe"

%msbuild% /t:Clean,Build /p:Configuration=Release /p:Platform="Any CPU" "%~dp0\CmisSync.sln"

if exist "%~dp0\InstallerBootstrapper\bin\Release\Oris4Sync.exe" (
	if not exist "%~dp0\..\..\bin" mkdir "%~dp0\..\..\bin"
	copy "%~dp0\InstallerBootstrapper\bin\Release\Oris4Sync.exe" "%~dp0\..\..\bin"
) else echo "Failed to build Oris4Sync.exe installer"
