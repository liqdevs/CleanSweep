@echo off
setlocal
pushd "%~dp0"

echo [1/4] Running tests...
dotnet test CleanSweep.Tests\CleanSweep.Tests.csproj -c Release --nologo
if errorlevel 1 goto :fail

if not exist Release mkdir Release

echo [2/4] Publishing framework-dependent executable...
dotnet publish CleanSweep\CleanSweep.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o temp_fd
if errorlevel 1 goto :fail
copy /y temp_fd\CleanSweep.exe Release\CleanSweep-Lite.exe >nul
if errorlevel 1 goto :fail
rmdir /s /q temp_fd

echo [3/4] Publishing self-contained single-file executable...
dotnet publish CleanSweep\CleanSweep.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o temp_sc
if errorlevel 1 goto :fail
copy /y temp_sc\CleanSweep.exe Release\CleanSweep.exe >nul
if errorlevel 1 goto :fail
rmdir /s /q temp_sc

echo [4/4] Creating ZIP archive...
powershell -NoProfile -Command "Compress-Archive -Path Release\CleanSweep.exe -DestinationPath Release\CleanSweep-win-x64.zip -Force"
if errorlevel 1 goto :fail

popd
echo Build complete. Artifacts are in the Release folder.
exit /b 0

:fail
set "exitCode=%errorlevel%"
if exist temp_fd rmdir /s /q temp_fd
if exist temp_sc rmdir /s /q temp_sc
popd
echo Build failed.
exit /b %exitCode%
