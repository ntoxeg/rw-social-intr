@echo off
setlocal enabledelayedexpansion
set SRC=%~dp0SocialInteractions
set DST=C:\Games\Steam\steamapps\common\RimWorld\Mods\Social Interactions

echo === Building SocialInteractions ===
dotnet build "%SRC%\SocialInteractions.csproj" -c Release
if !ERRORLEVEL! NEQ 0 (
    echo ERROR: Build failed with exit code !ERRORLEVEL!
    pause
    exit /b 1
)
echo.

echo === Deploying Social Interactions mod ===
echo Source: %SRC%
echo Target: %DST%
echo.

for %%D in (About 1.5 Languages Defs) do (
    robocopy "%SRC%\%%D" "%DST%\%%D" /MIR /NJH /NJS
    if !ERRORLEVEL! GEQ 8 (
        echo ERROR: robocopy failed on %%D with exit code !ERRORLEVEL!
        pause
        exit /b 1
    )
)

echo.
echo Deploy complete.
pause
