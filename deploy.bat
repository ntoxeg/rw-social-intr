@echo off
set SRC=C:\Users\gento\dev\GitHub\rimworldmods\SocialInteractions
set DST=C:\Games\Steam\steamapps\common\RimWorld\Mods\Social Interactions

echo Deploying Social Interactions mod...
echo Source: %SRC%
echo Target: %DST%
echo.

robocopy "%SRC%\About" "%DST%\About" /MIR /NJH /NJS
robocopy "%SRC%\1.5" "%DST%\1.5" /MIR /NJH /NJS
robocopy "%SRC%\Languages" "%DST%\Languages" /MIR /NJH /NJS
robocopy "%SRC%\Defs" "%DST%\Defs" /MIR /NJH /NJS

echo.
echo Deploy complete.
pause
