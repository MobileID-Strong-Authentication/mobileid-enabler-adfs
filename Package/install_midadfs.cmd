@rem usage: install_midadfs.cmd setup_trace.log setup.log
@echo off
PowerShell -NoProfile -ExecutionPolicy Bypass -Command "& Import-Certificate certs\codesigning-swisscom.crt -CertStoreLocation Cert:\LocalMachine\TrustedPublisher"
PowerShell -NoProfile -ExecutionPolicy Bypass -Command "& '%~dp0\register_midadfs.ps1' %1 *> %2"
