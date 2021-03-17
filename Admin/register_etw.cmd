@rem usage: register_etw.cmd
@echo off
cd /D %~dp0
PowerShell -NoProfile -ExecutionPolicy Bypass -Command "& '%~dp0\register_etw.ps1'"
