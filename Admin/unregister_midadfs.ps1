# usage: & .\unregister_midadfs.ps1  [traceFileName]

Import-Module -Name "$PWD\lib\MidAdfs.psm1" -Force

$global:VerbosePreference = "Continue"
$global:DebugPreference = "Continue"
$global:WarningPreference = "Continue"
$global:ErrorActionPreference = "Continue"

$shortVersion = "13";

if ($Args[0] -ne $null) {
  $logFile = $Args[0];
  Get-Date *>> $logFile
  ($rc = UnregisterMobileID $shortVersion) *> $logFile
  if ($rc -eq $true) {
    Write-Output "UnRegisterMobileID succeeded." | Tee-Object -FilePath $logFile -Append
    exit 0;
  } else {
    Write-Output "UnRegisterMobileID failed." | Tee-Object -FilePath $logFile -Append
    exit 1;
  }
} else {
  $rc = UnregisterMobileID $shortVersion
  if ($rc -eq $true) {
    Write-Output "UnRegisterMobileID succeeded."
    exit 0;
  } else {
    Write-Output "UnRegisterMobileID failed."
    exit 1;
  }
}

