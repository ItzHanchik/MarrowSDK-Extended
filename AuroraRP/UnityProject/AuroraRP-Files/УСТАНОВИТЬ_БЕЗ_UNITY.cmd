@echo off
chcp 65001 >nul
title AuroraRP - установка в BONELAB
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0AuroraRpInstall.ps1"
if errorlevel 1 pause
