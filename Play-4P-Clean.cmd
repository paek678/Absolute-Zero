@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0.codex\scripts\launch_four_player.ps1" %*
if errorlevel 1 pause
