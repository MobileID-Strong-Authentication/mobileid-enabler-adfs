@rem usage: unregister_etw.cmd
@echo off
cd /D %~dp0
PowerShell -NoProfile -ExecutionPolicy Bypass -Command "& '%~dp0\unregister_etw.ps1'"
