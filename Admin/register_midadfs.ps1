# usage: & .\register_midadfs.ps1  [logfileName]

Import-Module -Name "$PWD\lib\MidAdfs.psm1" -Force

$global:VerbosePreference = "Continue"
$global:DebugPreference = "Continue"
$global:WarningPreference = "Continue"
$global:ErrorActionPreference = "Continue"

$shortVersion = "13"
$fullVersion = "1.3.4.0"

if ($Args[0] -ne $null) {
  $logFile = $Args[0];
  ($rc = RegisterMobileID $shortVersion $fullVersion "5bb8c6d7272b1a01") *> $logFile
  if ($rc -eq $true) {
    Write-Output "RegisterMobileID succeeded." | Tee-Object -FilePath $logFile -Append
    exit 0;
  } else {
    Write-Output "RegisterMobileID failed." | Tee-Object -FilePath $logFile -Append
    exit 1;
  }
} else {
  $rc = RegisterMobileID $shortVersion $fullVersion "5bb8c6d7272b1a01"
  if ($rc -eq $true) {
    Write-Output "RegisterMobileID succeeded."
    exit 0;
  } else {
    Write-Output "RegisterMobileID failed."
    exit 1;
  }
}

