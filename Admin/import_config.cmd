@rem usage: import_config.cmd config_file_name
@echo off
PowerShell -NoProfile -ExecutionPolicy Bypass -Command "& '%~dp0\import_config.ps1' ""%1"""

