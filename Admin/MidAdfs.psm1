$VERSION = "0.2";

function _hadError($err) {
  return ! ([System.String]::IsNullOrEmpty($err));
}

# restart the main Service and its dependent running services.
# stopped dependent services are not restarted.
# return $true if the main service can be re-started (dependent services may fail), $false otherwise.
function _restartServices($mainServiceName) {
    $dependentSvc = Get-Service -Name $mainServiceName -DependentServices | Where-Object {$_.Status -eq "Running"};
    $hadError = $false;
    $localError = $null;
    if ($dependentSvc) {
      Write-Verbose "stop dependent services";
      Stop-Service $dependentSvc;
      # error will be caught by Restart-Service adfssrv
    };
    Write-Verbose "Restart-Service -Name $mainServiceName";
    Restart-Service -Name $mainServiceName -ErrorVariable localError;
    if (_hadError($localError)) {
      Write-Error $localError[0];
      return $false;
    }
    if ($dependentSvc) {
      Write-Verbose "start dependent services";
      Start-Service $dependentSvc -ErrorVariable localError;
      if (_hadError($localError)) {
        Write-Error $localError[0];
      }
    };
    return $true;
}

function IsMidInAdfsRegistry($version)  {
  $midVersion = "MobileID$version";
  Write-Debug "CALL: Get-AdfsAuthenticationProvider -Name $midVersion"
  $ret = Get-AdfsAuthenticationProvider -Name $midVersion
  return ($ret -ne $null);
}

function IsMidInAdfsPolicyStore($version) {
   $midVersion = "MobileID$version";
   Write-Debug "CALL: Get-AdfsGlobalAuthenticationPolicy | select -Property AdditionalAuthenticationProvider"
   $ret = Get-AdfsGlobalAuthenticationPolicy | select -Property AdditionalAuthenticationProvider
   if ($ret -ne $null) {
     return $ret.AdditionalAuthenticationProvider.Contains($midVersion);
   } else {
     return $false;
   }
}


function IsAdfsRoleInstalled($startService) {
  $localError = $null;
  $svc = Get-Service -Name "AdfsSrv" -ErrorVariable localError;
  if (_hadError($localError)) {
    if ($localError[0] -notcontains "NoServiceFoundForGivenName") {
      Write-Error $localError[0];
    };
    return $false;
  } else {
    if ($startService -and ( ($svc).Status -ne "Running" ) ) {
      Write-Debug "Start-Service -Name AdfsSrv";
      Start-Service -Name "AdfsSrv" -ErrorVariable localError;
      if (_hadError($localError)) {
        Write-Error $localError[0];
      } else {
        Write-Warning "ADFS Service has been started in order to determine its configuration";
      };
    };
    return $true;
  }
}

function IsMidAdfsRunning($version) {
  if ( ! (IsAdfsRoleInstalled($true) )) {
    Write-Verbose "ADFS has not been installed";
    return $false;
  }
  if ( ! (IsMidInAdfsRegistry($version))) {
    Write-Verbose "MobileID$version is not registered in ADFS";
    return $false;
  }
  if ( ! (IsMidInAdfsPolicyStore($version))) {
    Write-Verbose "MobileID$version is not enabled in ADFS";
    return $false;
  }
  return ( (Get-Service -Name "AdfsSrv").Status -eq "Running" );
}

$MidDlls = @(
  "MobileId.Adfs.AuthnAdapter.dll",
  "de\MobileId.Adfs.AuthnAdapter.resources.dll",
  "fr\MobileId.Adfs.AuthnAdapter.resources.dll",
  "it\MobileId.Adfs.AuthnAdapter.resources.dll",
  "MobileId.ClientService.dll",
  "de\MobileId.ClientService.resources.dll",
  "fr\MobileId.ClientService.resources.dll",
  "it\MobileId.ClientService.resources.dll"
);

# DLLs in $InstallOnlyMidDlls will not be removed by installer. They may be used by non-Mobile ID applications.
$InstallOnlyMidDlls = @(
  "Microsoft.Diagnostics.Tracing.EventSource.dll"
);

# Remove the specified Mobile ID version completely from ADFS policy, ADFS provider and GAC.
# return ($res, @undoActions), where $res is  true on success, false on eror.
function UnregisterMobileID($version) {
  Write-Debug "UnregisterMobileID($version)";
  $undoActions = @();
  $midVersion = "MobileID$version";

  if (! (isAdfsRoleInstalled($true)) ) {
    Write-Error "ADFS has not been installed. ABORT.";
    return ($false,$undoActions);
  };

  $isPrimaryComputer = ((Get-AdfsSyncProperties | select -Property Role).Role -eq "PrimaryComputer");

  if ($isPrimaryComputer) {
    if (IsMidInAdfsPolicyStore($version)) {
    Write-Verbose "# remove $midVersion from AdfsGlobalAuthenticationPolicy";

    # record current state
    $res = (Get-AdfsGlobalAuthenticationPolicy | select -Property AdditionalAuthenticationProvider).AdditionalAuthenticationProvider;
    $undoActions = $undoActions + @("Set-AdfsGlobalAuthenticationPolicy -AdditionalAuthenticationProvider $res");
    Write-Debug "Get-AdfsGlobalAuthenticationPolicy => $res";

    # remove $midVersion from AdfsGlobalAuthenticationPolicy
    $res = $res.Where({$_ -ne $midVersion});
    Write-Verbose "Set-AdfsGlobalAuthenticationPolicy -AdditionalAuthenticationProvider $res";
    $localError = $null;
    Set-AdfsGlobalAuthenticationPolicy -AdditionalAuthenticationProvider $res -ErrorVariable localError;
    if (_hadError($localError)) {
        Write-Error $localError[0];
        return ($false, @());
    }
    } else {
    Write-Verbose "# $midVersion not in ADFS policy store. NEXT.";
    }

    if (IsMidInAdfsRegistry($version)) {
    Write-Verbose "# remove $midVersion from Adfs";
    Write-Verbose "Unregister-AdfsAuthenticationProvider -Name $midVersion -Confirm:\$false";
    $locaError = $null;
    Unregister-AdfsAuthenticationProvider -Name $midVersion -Confirm:$false -ErrorVariable localError;
    if (_hadError($localError)) {
        Write-Error $localError[0];
        return ($false, $undoActions);
    }
    $undoActions = $undoActions + "Register-AdfsAuthenticationProvider -Name $midVersion";
    Write-Verbose "# restart ADFS service and its running dependencies";
    if (! (_restartServices("adfssrv"))) {
        return ($false, $undoActions);
    };
    # $undoActions = $undoActions + "Start-Service -Name AdfsSrv";
    } else {
    Write-Verbose "# $midVersion not in ADFS registry. NEXT.";
    }
  };

  # Write-Verbose "# remove Mobile ID from ETW";
  # _unregisterEtw;

  Write-Verbose "# remove Mobile ID DLLs from GAC";
  if ( $null -eq ([AppDomain]::CurrentDomain.GetAssemblies() |? { $_.FullName -eq "System.EnterpriseServices, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a" }) ) {
    Write-Debug "Load System.EnterpriseServices";
    [System.Reflection.Assembly]::Load("System.EnterpriseServices, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
  };
  $publish = New-Object System.EnterpriseServices.Internal.Publish;
  foreach ($dll in $MidDlls) {
    Write-Debug "GacRemove('$pwd\lib\$dll')";
    $publish.GacRemove("$pwd\lib\$dll");
  };

  return $true;
}

# Determine the name of scheme which should be updated with the spin.js resource, and
# return the name on success, $null on error.
# The scheme will be created if it did not exist.
#
# The name of the updated scheme is guessed with the following algorithm:
# 0) return $null if error occurs in any step described below;
# 1) if a non-default scheme has been specified, return it;
# 2) if the currently active schema is non-default, return  it;
# 3) if there is a scheme named "custom", set it to active (with warning) and return it;
# 4) if there is only one non-default scheme, activate (with warning) and return it;
# 5) create and activate (with warning) a new scheme "custom" and return it.
#
function _guessSchemeName($schemeName) {
  if ( ! ([System.String]::IsNullOrWhiteSpace($schemeName)) -and ! ([System.String]::Compare($schemeName, "Default", $true)) ) {
    Write-Debug "GuessSchemeName: use input '$schemeName'";
    return $schemeName;
  };
  $sh = (Get-AdfsWebConfig).ActiveThemeName;
  if ($sh -ne "Default") {
    Write-Debug "GuessSchemeName: use ActiveTheme '$sh'";
    return $sh;
  };
  $sh = Get-AdfsWebTheme | Where-Object {$_.Name -eq "custom"};
  if ($sh -ne $null) {
    Write-Warning "GuessSchemeName: Existing non-active scheme 'custom' will be updated and activated";
    $localError = $null;
    Set-AdfsWebConfig -ActiveThemeName "custom" -ErrorVariable localError;
    if ($localError -ne $null) {
      Write-Error $localError;
      return $null;
    } else {
      Write-Verbose "Set-AdfsWebConfig -ActiveThemeName 'custom'"
      return "custom";
    };
  };
  $sh = (Get-AdfsWebTheme | Where-Object {$_.Name -ne "Default"}).Count;
  if ($sh -eq 1) {
    $sh = (Get-AdfsWebTheme | Where-Object {$_.Name -ne "Default"})[0].Name;
    Write-Warning "GuessSchemeName: Existing non-active scheme $sh will be updated and activated";
    $localError = $null;
    Set-AdfsWebConfig -ActiveThemeName $sh -ErrorVariable localError;
    if ($localError -ne $null) {
      Write-Error $localError;
      return $null;
    } else {
      Write-Verbose "Set-AdfsWebConfig -ActiveThemeName '$sh'"
      return $sh;
    };
  };
  $localError = $null;
  $sh = "custom";
  Write-Warning "GuessSchemeName: the new WebTheme $sh will be created & activated";
  New-AdfsWebTheme -Name $sh -SourceName "Default" -ErrorVariable localError;
  Set-AdfsWebConfig -ActiveThemeName $sh -ErrorVariable +localError;
  if ($localError -ne $null) {
    Write-Error "GuessSchemeName tried to create & activate WebTheme but got the error: $localError";
    return $null;
  } else {
    Write-Verbose "New-AdfsWebTheme -Name '$sh' -SourceName 'Default'";
    Write-Verbose "Set-AdfsWebConfig -ActiveThemeName '$sh'";
    return $sh;
  };
}

# Add the static file resource spin.js to ADFS scheme, return $true on success, $false on failure.
function _replaceSpinJs($filePath, $schemeName) {
  Write-Debug "_replaceSpinJs($filePath, $schemeName)";
  $schemeNameToBeUsed = _guessSchemeName($schemeName);
  if ($schemeNameToBeUsed -ne $null) {
    $localError = $null;
    Write-Debug "Set-AdfsWebTheme -TargetName $schemeNameToBeUsed -AdditionalFileResource @{Uri='/adfs/portal/script/spin.js';path='$filePath'}";
    Set-AdfsWebTheme -TargetName $schemeNameToBeUsed -AdditionalFileResource @{Uri="/adfs/portal/script/spin.js";path="$filePath"} -ErrorVariable localError;
    if (! ([System.String]::IsNullOrEmpty($localError)) ) {
      Write-Warning "Set-AdfsWebTheme: '$localError'";
      return $false;
    };
    # $localError cannot be fully trusted, we need extra validation
    $s = (Get-AdfsWebTheme -Name $schemeNameToBeUsed).AdditionalFileResources["/adfs/portal/script/spin.js"];
    if ($s -eq $null) {
      Write-Warning "Set-AdfsWebTheme: could not set spin.js";
      return $false;
    } else {
      Write-Verbose "Set-AdfsWebTheme -TargetName $schemeNameToBeUsed for spin.js succeed";
      return $true;
    };
  } else {
    Write-Warning "_replaceSpinJs: could not guess schemeName";
    return $false;
  };
}

function RegisterEtw($version) {
    $exe = "wevtutil.exe";
    foreach ($dll in @("MobileId.ClientService.Swisscom-MobileID-Client", "MobileId.Adfs.AuthnAdapter.Swisscom-MobileID-Adfs")) {
      $dll = "$pwd\lib\$dll$version.etwManifest";
      Write-Debug "Start-Process -FilePath ""$exe"" -ArgumentList @(""um"",""""""$dll.man"""""") -PassThru -Wait";
      $p = Start-Process -FilePath $exe -ArgumentList @("um", """$dll.man""") -PassThru -Wait;
      if ($p.ExitCode -ne 0) {
        Write-Error ("$exe um $dll.man exit code " + $p.ExitCode)
        return $false;
      } else {
        Write-Debug "unregister exitCode=0";
      }
      Write-Debug ("Start-Process -FilePath ""$exe"" -ArgumentList @(""im"", ""$dll.man"", ""/rf:$dll.dll"", ""/mf:$dll.dll"") -PassThrus -Wait");
      $p = Start-Process -FilePath $exe -ArgumentList @("im", """$dll.man""", "/rf:""$dll.dll""", "/mf:""$dll.dll""") -PassThru -Wait;
      if ($p.ExitCode -ne 0) {
        Write-Error ("'$exe im $dll' exit code " + $p.ExitCode)
        return $false;
      } else {
        Write-Debug "register exitCode=0";
      }
    };
    return $true;
}

function UnregisterEtw($version) {
    $exe = "wevtutil.exe";
    foreach ($dll in @("MobileId.ClientService.Swisscom-MobileID-Client", "MobileId.Adfs.AuthnAdapter.Swisscom-MobileID-Adfs")) {
     $dll = "$pwd\lib\$dll$version.etwManifest";
     Write-Debug "Start-Process -FilePath ""$exe"" -ArgumentList ""um"",""""""$dll.man"""""") -PassThru -Wait";
     $p = Start-Process -FilePath $exe -ArgumentList @("um", """$dll.man""") -PassThru -Wait;
     if ($p.ExitCode -ne 0) {
       Write-Error ("$exe um $dll.man exit code" + $p.ExitCode)
       return $false;
     } else {
       Write-Debug "unregister ExitCode=0";
     }
   };
   return $true;
}

function _createEventSource() {
  $eventSources = @("MobileId.Client","MobileId.Adfs");
  foreach($e in $eventSources) {
    if ([System.Diagnostics.EventLog]::SourceExists($e) -eq $false) {
        Write-Debug "creating eventsource $e in Application Log";
        [System.Diagnostics.EventLog]::CreateEventSource($e,"Application");
    }
  }
}

# return status (true on success, false on failure)
function RegisterMobileID($version,$versionQdot,$publicKeyToken) {
  if (! (isAdfsRoleInstalled($true)) ) {
     Write-Error "ADFS has not been installed. ABORT.";
     return $false;
  };

  $isPrimaryComputer = ((Get-AdfsSyncProperties | select -Property Role).Role -eq "PrimaryComputer");

  if ( IsMidInAdfsPolicyStore($version)) {
    Write-Verbose "Mobild ID v$version is already in ADFS policy store. SKIP.";
    if ($isPrimaryComputer) {return $true;}
  };

  if ( IsMidInAdfsRegistry($version)) {
    Write-Verbose "Mobild ID v$version is already register. SKIP.";
    if ($isPrimaryComputer) {return $true;}
  };
  

  Write-Verbose "# publish Mobile ID v$version DLL to GAC";
  if ( $null -eq ([AppDomain]::CurrentDomain.GetAssemblies() |? { $_.FullName -eq "System.EnterpriseServices, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a" }) ) {
    Write-Debug "Load System.EnterpriseServices";
    [System.Reflection.Assembly]::Load("System.EnterpriseServices, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
  };
  $publish = New-Object System.EnterpriseServices.Internal.Publish;
  foreach ($dll in $MidDlls) {
    Write-Debug "GacInstall('$pwd\lib\$dll')";
    $publish.GacInstall("$pwd\lib\$dll");
  };
  foreach ($dll in $InstallOnlyMidDlls) {
    Write-Debug "GacInstall('$pwd\lib\$dll')";
    $publish.GacInstall("$pwd\lib\$dll");
  };

  Write-Verbose "# register Mobile ID in ETW";
  RegisterEtw($version);

  Write-Verbose "# create non-existing EventSource in EventLog";
  _createEventSource;

  if ($isPrimaryComputer) {
    Write-Verbose "# register Mobile ID v$version to ADFS";
    $typeName = "MobileId.Adfs.AuthenticationAdapter, MobileId.Adfs.AuthnAdapter, Version=$versionQdot, Culture=neutral, PublicKeyToken=$publicKeyToken, processorArchitecture=MSIL";
    $cmd = "Register-AdfsAuthenticationProvider -ConfigurationFilePath .\MobileId.Adfs.AuthnAdapter.xml -TypeName ""$typeName"" -Name MobileID$version";
    Write-Verbose $cmd;
    Register-AdfsAuthenticationProvider -ConfigurationFilePath .\MobileId.Adfs.AuthnAdapter.xml -TypeName $typeName -Name MobileID$version -ErrorVariable localError;
    if (_hadError($localError)) {
    Write-Error $localError[0];
    return $false;
    }

    Write-Verbose "# install static web resource in ADFS";
    _replaceSpinJs("lib/spin.min.js"); # possible error is not fatal, continue
  };

  Write-Verbose "# restart ADFS service and its running dependencies";
  return _restartServices("adfssrv");
}

# import config file $configFile to MidAdfs (short) version $midAdfsVersion, return true on success, false otherwise
function ImportMidAdfsConfig($configFile, $midAdfsVersion) {
  if (! (Test-Path -PathType Leaf -Path $configFile)) {
    Write-Error "Cannot read file $configFile. Error";
    return $false;
  };
  if (! (IsMidAdfsRunning($midAdfsVersion))) {
    Write-Error "Mobile ID Authentication Provider v$midAdfsVersion is not installed";
    return $false;
  };
  Write-Debug "Import-AdfsAuthenticationProviderConfigurationData -FilePath $configFile -Name 'MobileID$midAdfsVersion'";
  $localError = $null;
  Import-AdfsAuthenticationProviderConfigurationData -FilePath $configFile -Name "MobileID$midAdfsVersion" -ErrorVariable localError;
  if (_hadError($localError)) {
     Write-Error $localError[0];
     return $false;
  } else {
     return _restartServices("adfssrv");
  };
}

Export-ModuleMember -Function RegisterMobileID,UnregisterMobileID,IsMidAdfsRunning,ImportMidAdfsConfig,RegisterEtw,UnregisterEtw

# Test
#$DebugPreference = "Continue";
#$VerbosePreference = "Continue";

# IsMidInAdfsRegistry "11";
# IsMidAdfsRunning "10"
#UnregisterMobileID "10"
#_restartServices("adfssrv");
# RegisterMobileID "10" "1.0.0.0" "2d8af5277000f5f0"
#_replaceSpinJs("lib/spin.min.js");
# ImportMidAdfsConfig "midadfs_preprod.xml" "10";
# ImportMidAdfsConfig "notexisting.xml" "10";
# ImportMidAdfsConfig "midadfs_preprod.xml" "99";
# ImportMidAdfsConfig "midadfs_preprod.xml";