# usage: & .\register_etw.ps1  [logfileName]

Import-Module -Name "$PWD\lib\MidAdfs.psm1" -Force

$global:VerbosePreference = "Continue"
$global:DebugPreference = "Continue"
$global:WarningPreference = "Continue"
$global:ErrorActionPreference = "Continue"

$shortVersion = "12"

if ($Args[0] -ne $null) {
  $logFile = $Args[0];
  ($rc = RegisterEtw $shortVersion) *> $logFile
  if ($rc -eq $true) {
    Write-Output "RegisterEtw succeeded." | Tee-Object -FilePath $logFile -Append
    exit 0;
  } else {
    Write-Output "RegisterEtw failed." | Tee-Object -FilePath $logFile -Append
    exit 1;
  }
} else {
  $rc = RegisterEtw $shortVersion
  if ($rc -eq $true) {
    Write-Output "RegisterEtw succeeded."
    exit 0;
  } else {
    Write-Output "RegisterEtw failed."
    exit 1;
  }
}