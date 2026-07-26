@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0local-production.ps1" %*
