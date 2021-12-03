# usage: & .\register_etw.ps1  [logfileName]

Import-Module -Name "$PWD\lib\MidAdfs.psm1" -Force

$global:VerbosePreference = "Continue"
$global:DebugPreference = "Continue"
$global:WarningPreference = "Continue"
$global:ErrorActionPreference = "Continue"

$shortVersion = "13"

if ($Args[0] -ne $null) {
  $logFile = $Args[0];
  ($rc = UnregisterEtw $shortVersion) *> $logFile
  if ($rc -eq $true) {
    Write-Output "UnregisterEtw succeeded." | Tee-Object -FilePath $logFile -Append
    exit 0;
  } else {
    Write-Output "UnregisterEtw failed." | Tee-Object -FilePath $logFile -Append
    exit 1;
  }
} else {
  $rc = UnregisterEtw $shortVersion
  if ($rc -eq $true) {
    Write-Output "UnregisterEtw succeeded."
    exit 0;
  } else {
    Write-Output "UnregisterEtw failed."
    exit 1;
  }
}