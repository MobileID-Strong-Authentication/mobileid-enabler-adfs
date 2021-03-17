# usage: & .\import_config.ps1  configfilePath [logFile]

if (($args.Count -eq 0) -or ($args.Count -gt 2)) {
  Write-Error "usage: & .\import_config.ps1  configfilePath [logFile]";
  exit 2;
}

$shortVersion = "12"
Import-Module -Name "$PSScriptRoot\lib\MidAdfs.psm1" -Force

$global:VerbosePreference = "Continue"
$global:DebugPreference = "Continue"
$global:WarningPreference = "Continue"
$global:ErrorActionPreference = "Continue"

if ($Args[1] -ne $null) {
  $cfgFile = $Args[0];
  $logFile = $Args[1];
  ($rc = ImportMidAdfsConfig $cfgFile $shortVersion) *> $logFile
  if ($rc -eq $true) {
    Write-Output "Import of config file '$cfgFile' succeeded." | Tee-Object -FilePath "$logFile" -Append
    exit 0;
  } else {
    Write-Output "Import of config file '$cfgFile' failed." | Tee-Object -FilePath "$logFile" -Append
    exit 1;
  }
} else {
  $cfgFile = $Args[0];
  $rc = ImportMidAdfsConfig $cfgFile $shortVersion
  if ($rc -eq $true) {
    Write-Output "Import of config file '$cfgFile' succeeded."
    exit 0;
  } else {
    Write-Output "Import of config file '$cfgFile' failed."
    exit 1;
  }
}

