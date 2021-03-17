# usage: & .\register_midadfs.ps1  [logfileName]

Import-Module -Name "$PWD\lib\MidAdfs.psm1" -Force

$global:VerbosePreference = "Continue"
$global:DebugPreference = "Continue"
$global:WarningPreference = "Continue"
$global:ErrorActionPreference = "Continue"

$shortVersion = "12"
$fullVersion = "1.2.0.0"

if ($Args[0] -ne $null) {
  $logFile = $Args[0];
  ($rc = RegisterMobileID $shortVersion $fullVersion "2d8af5277000f5f0") *> $logFile
  if ($rc -eq $true) {
    Write-Output "RegisterMobileID succeeded." | Tee-Object -FilePath $logFile -Append
    exit 0;
  } else {
    Write-Output "RegisterMobileID failed." | Tee-Object -FilePath $logFile -Append
    exit 1;
  }
} else {
  $rc = RegisterMobileID $shortVersion $fullVersion "2d8af5277000f5f0"
  if ($rc -eq $true) {
    Write-Output "RegisterMobileID succeeded."
    exit 0;
  } else {
    Write-Output "RegisterMobileID failed."
    exit 1;
  }
}

