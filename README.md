# Mobile ID Authentication Provider for Active Directory Federation Service (ADFS)

This is an Active Directory Federation Service (ADFS) external authentication provider
which authenticates end users with [Mobile ID](https://www.swisscom.ch/mid).

The current document is a destilled version of [Mobile ID Microsoft ADFS Solution Guide](./doc/mobile_id_microsoft_adfs_solution_guide_v1_3.pdf).
If you are familiar with the contents in Integration Guide, you can skip the rest of this document.

## System Requirement 

### Runtime Environment:

* Microsoft Windows Server 2012 R2
* Mobile ID Application Provider Account (`AP_ID`)

### Build Environment:

(If you don't want to build from source code, the compiled binaries and a setup program can be downloaded from the [binaries subfolder](./binaries)).

* Microsoft Visual Studio 2022 (which includes Microsoft .NET Framework 4.8.*)
* The file Microsoft.IdentityServer.Web.dll. The DLL file can be copied from a Microsoft Windows Server 2012 R2 server.
* [InnoSetup](http://www.innosetup.org) if you want to rebuild the setup program

## Installation of the Runtime Environment:

### Step 0: Installation of ADFS service

Refer to the Microsoft official documentation on how to install and deploy this service in your environnment.
There is also a [walkthrough guide](https://technet.microsoft.com/en-us/library/dn280939.aspx "Set up the lab environment for AD FS in Windows Server 2012 R2") for development lab.

### Step 1: Establishment of Connectivity between ADFS server(s) and Mobile ID servers

#### 1.1: IP connectivity between Mobile ID client and Mobile ID server

The Mobile ID service (*MID*) can only be accessed by servers from specific IP addresses.
The address range is specific to an Application Provider (*AP*) and configured by the MID service during the enrollment process for the AP.

#### 1.2. SSL connectivity between Mobile ID client and Mobile ID server

An ADFS server must establish a mutually authenticated SSL/TLS connection with a MID server before calling the MID services.
During the enrollment process of Mobile ID Application Provider Account, you have created a SSL/TLS client certificate for 
your Mobile ID connection.
You also need the certificate of the Certificate Authority (*CA*) for the MID servers, which is located in [certs](certs) folder.

##### Configuration for SSL/TLS:

1.2.1. Import your SSL client certificate file (PFX/PKCS#12 format) into *computer* certificate store:

  Right-click your SSL Client certificate file, select `Install PFX`, the Certificate Import Wizard will pop up.
  Select `Local Machine` as Store Location, Click `Next` twice, then enter the passphrase of the PFX file, 
  click `Next` and click `Finish`.

1.2.2. If your SSL client certificate is issued by a Certificate Authority trusted by your organisation, you
  can skip this step, otherwise (e.g. self-signed certificate), you need explicitly configure trust for it:
  Run `mmc.exe`, navigate to `File` > `Add/Remove Snap-in...`, select `Certificates` in left `Available snap-ins` panel, click `Add >`,
  choose `Computer account`, click `Next`, `Finish`, `OK`, the `Certificates (Local Computer)` snap-in is added to Management Console.

  In the Certificate Management Console for Local Computer;
  right-click `Trusted People`, navigate to `All Task`, then `Import...`, this opens the `Certificate Import Wizard`;
  Clicks `Next`, locates the PFX file in `File to Import`, `Next`, enter passphrase for the private key, clicks `Next` twice and `Finish`.

  Make sure that the service account of ADFS role service has access to the imported key/certificate.

1.2.3. Verify the SSL client certificate has been correctly imported and trusted:

  In Certificate Management Console (`certmgr.msc`), navigate to Personal > Certificates, double-click the certificate imported in step 1, 
  select `Certification Path`, the `Certificate status` should displays "This certificate is OK". Do not close the console now.

1.2.4. Configure trust to Root CA of MID servers:
   In the open console, navigate to `Trusted Root Certificate Authority`,
   Right-click `Certificates`, select `All Tasks`, `Import...` , then `Next`,
   select the file *.crt containing the Root CA of MID servers,
   `Next` twice, `Finish`, confirm `Yes` on the Security Warning "You are about to install a certificate from a certificate authority (CA) claiming to represent: ... Thumbprint (sha1): ..."
   Click `OK`.

1.2.5. Verify the SSL/TLS connectivity:
   Use a browser such as IE to connect to the URL https://mobileid.swisscom.com
   The browser should display a HTTP 404 / NOT FOUND. It means that SSL/TLS connectivity was successful. Because the browser is not sending a valid SOAP request (missing client certificate for authentication) a HTTP 404 is returned by the Mobile ID server.

### Step 2: Configuration of Mobile ID Authentication Provider

The Mobile ID Authentication Provider can be configured with a XML file, e.g. `C:\midadfs\MobileId.Adfs.AuthnAdapter.xml`.
The folder [samples](samples) contain several examples. The content of the configuration file looks like

`````
<?xml version="1.0" encoding="utf-8" ?>
<appConfig>
  <mobileIdClient
    AP_ID="mid://dev.swisscom.ch"
    SslKeystore="LocalMachine"
    SslCertThumbprint="452409b86fb9541eb9dd8e3312b80a2fe2d6daac"
    DtbsPrefix="Test: "
  />
  <mobileIdAdfs/>
</appConfig>

`````
The configuration contains two elements. The element `mobileIdClient` specifies the Mobild ID Service
while the element `mobileIdAdfs` specifies the integration of Mobile ID with ADFS. The semantics of the important attributes are:

* Element `mobileIdClient`:
  + `AP_ID`: Your Application Provider ID, as assigned by Mobile ID Service Provider. Mandatory.
  + `DtbsPrefix`: This string will be prepended to the language-specific login prompt sent to a mobile device. Default: ""
  + `ServiceUrlPrefix`: URL for Mobile ID service, must end with `/`. Default: `https://mobileid.swisscom.com/soap/services/`
  + `SslKeystore`: Store location of certificate/key used for Mobile ID connectivity. For ADFS, the value should be usually `LocalMachine`. Default: `CurrentUser`
  + `SslCertThumbprint`: The SHA1 Thumbprint of certificate used for Mobile ID connectivity. The thumbprint can be read out of the `Certificate` GUI (i.e. double-click the certificate file), or with a PowerShell cmdlet like `Get-ChildItem -Path cert:\\LocalMachine\My`. Mandatory.
  + `SslRootCaCertDN`: Distinguished Name of the Root Certificate in the certificate chain of Mobile ID servers. Default: "CN=Swisscom Root CA 2, OU=Digital Certificate Services, O=Swisscom, C=ch"
  + `SslRootCaCertFiles`: Additional certificate files
  + `UserSerialNumberPolicy`: Flags that determine how the serial number in user’s certificate is used in the authentication.
     Supported flags are warnMismatch(1), allowAbsence(2), allowMismatch (4). Default: "6"
  + `SanitizePhoneNumber`: If this parameter is `true`, phone numbers read from the attribute store are transformed before use in Mobile ID calls. The transformation is specified by `SanitizePhoneNumberPattern` and `SanitizePhoneNumberReplacement`. Default: remove all non-digits
  + `SanitizePhoneNumberPattern`: Only effective when `SanitizePhoneNumber` is true. This parameter is the regular expression for matching a pattern in phone number. Default: `\D`
  + `SanitizePhoneNumberReplacement`: Only effective when `SanitizePhoneNumber` is true. This parameter is the replace string for matched pattern defined by `SanitizePhoneNumberPattern`. Default: ""
  + `SecurityProtocolType`: The TLS Version for Mobile ID connectivity. The following Values are allowed: "Tls", "Tls11", "Tls12", "Tls13". Default: "Tls" for Tls Version 1.0
  + `SignatureProfile`: The signature profile value for authentication requests. Default: "http://mid.swisscom.ch/MID/v1/AuthProfile1"

* Element `mobileIdAdfs`:
  + `AdAttrMobile`: Attribute name of AD user object for the mobile number. The attribute should have exactly one value. Default: `mobile`.
  + `AdAttrSerialNumber`: Attribute name of AD user object for the Serial Number of Mobile ID. The attribute should have at most one value. Default: `serialNumber`
  + `LoginPrompt.`xx (xx=`en`,`de`,`fr`,`it`): Login message sent to the mobile phone.
     The value can optionally contains one place holder `#TransId#` which expands to a 5-char random string.
  + `LoginNonceLength`: Length of the random string to be included in the login prompt (see parameter `LoginPrompt.`xx). Default: 5
  + `SessionMaxTries`:  In an *Mobile ID authentication session", a user can retry the Mobile ID after an unsuccessful login. This is the maximum number of unsucessful login tries in a Mobile ID authentication session. Default: `5`.
  + `SessionTimeoutSeconds`: Maximum duration, in seconds, of a Mobile ID authentication session. Default: `300`.
  + `ShowDebugMsg`: If this parameter is `true`, debugging information may be displayed in web browser in case of errors. Otherwise the debugging information is not displayed. Default: `false`

### Step 3a: Installation of Mobile ID Authentication Provider for ADFS with Installer

The installation is automated by the [setup](./binaries) program. We recommend you to use the installer (i.e. start `midadfs_setup_1.3.0.0.exe` and follow the wizard).
The setup program 

* unpacks all necessary files to the file system
* install all necessary assemblies and resource to GAC
* register EventSource for Windows EventLog and ETW providers
* register Mobile ID Authentication Provider in ADFS (it starts ADFS service if it was not running)
* install static resource in ADFS web scheme
* restart ADFS and dependent services

The trace file `inst\setup_trace.log` in the installation folder records what the setup program was doing.

For a deployment in an ADFS farm, this step must be run on ADFS member servers, with identical installation path.

### Step 3b: Manual installation of Mobile ID Authentication Provider for ADFS

Note 1: The version numbers in the commands may change on version upgrade. You may need to adapt the version parameters for your version.

Note 2: For a deployment in an ADFS farm, this step must be run on ADFS member servers, unless otherwise specified.

1. Download (or build) all DLLs (e.g. `MobileId.Adfs.AuthnAdapter.dll`) from the [binaries](../binaries), for example to `C:\midadfs\v1.3`.

2. Install the all DLLs into Global Assembly Cache (GAC): Open a Windows PowerShell prompt, enters
   `````
   Set-location "C:\midadfs\v1.3"
   [System.Reflection.Assembly]::Load("System.EnterpriseServices, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")
   $publish = New-Object System.EnterpriseServices.Internal.Publish
   $publish.GacInstall("C:\midadfs\v1.3\MobileId.Adfs.AuthnAdapter.dll")
   `````
   Alternatively, you can also install the DLL with command `gacutil.exe /i MobileId.Adfs.AuthnAdapter.dll`. 
   (`gacutil.exe` is available in Visual Studio 2013, default location `C:\Program Files (x86)\Microsoft SDKs\Windows\v8.1A\bin\NETFX 4.5.1 Tools`.)
   Repeat these for all DLLs.

3. Register the DLL with ADFS on the primary ADFS server: Take a note of the version of `MobileId.Adfs.AuthenticationAdapter.dll` (right-click the DLL file in Windows Explorer, select `Properties`, `Details`, read `File Version`). In the example below, we assume it is `1.3.0.0`. In Windows PowerShell prompt, entertaining
   `````
   $TypeName = "MobileId.Adfs.AuthenticationAdapter, MobileId.Adfs.AuthnAdapter, Version=1.3.0.0, Culture=neutral, PublicKeyToken=2d8af5277000f5f0, processorArchitecture=MSIL"
   Register-AdfsAuthenticationProvider -ConfigurationFilePath "C:\midadfs\MobileId.Adfs.AuthnAdapter.xml" -TypeName $TypeName -Name "MobileID13"
   `````
   Notes: 
   * If you build the DLL from source, you may have a different `PublicKeyToken` value. In this case, you need to modify the value `PublicKeyToken` in the command above.
   * If the DLL has a different value, you need to replace the value of `Version`.

4. Install static web resources (`C:\midadfs\v1.3\spin.min.js` in this example) into ADFS on the primary ADFS server: In Windows PowerShell prompt, enters
   `````
   New-AdfsWebTheme -Name custom -SourceName default
   Set-AdfsWebTheme -TargetName custom -AdditionalFileResource @{Uri="/adfs/portal/script/spin.js";path="C:\midadfs\v1.3\spin.min.js"}
   Set-AdfsWebConfig -ActiveThemeName custom
   `````

5. Install ETW providers:
   Close `Windows Event Viewer` if it is open.
   In Windows PowerShell prompt, enters

   `````
   wevtutil.exe um C:\midadfs\v1.3\lib\MobileId.ClientService.Swisscom-MobileID-Client.etwManifest.man
   wevtutil.exe im C:\midadfs\v1.3\lib\MobileId.ClientService.Swisscom-MobileID-Client.etwManifest.man /rf:C:\midadfs\v1.3\lib\MobileId.ClientService.Swisscom-MobileID-Client.etwManifest.dll /mf:C:\midadfs\v1.3\lib\MobileId.ClientService.Swisscom-MobileID-Client.etwManifest.dll
   wevtutil.exe um C:\midadfs\v1.3\lib\MobileId.Adfs.AuthnAdapter.Swisscom-MobileID-Adfs.etwManifest.man
   wevtutil.exe im C:\midadfs\v1.3\lib\MobileId.Adfs.AuthnAdapter.Swisscom-MobileID-Adfs.etwManifest.man /rf:C:\midadfs\v1.3\lib\MobileId.Adfs.AuthnAdapter.Swisscom-MobileID-Adfs.dll /mf:C:\midadfs\v1.3\lib\MobileId.Adfs.AuthnAdapter.Swisscom-MobileID-Adfs.dll

   `````
   In PowerShell:
   `````
   [System.Diagnostics.EventLog]::CreateEventSource("MobileId.Client","Application");
   [System.Diagnostics.EventLog]::CreateEventSource("MobileId.Adfs","Application");
   `````

6. Restart the `Active Directory Federation Services` and dependent services (if any), e.g. in command line prompt
   `````
   net stop drs
   net stop adfsSrv
   net start adfsSrv
   net start drs
   `````

7. Verify the installation of the DLL on the primary ADFS server
   `````
   Get-AdfsAuthenticationProvider "MobileID13"
   `````

### Step 4: Configuration of ADFS

This depends on your use case. For the verification purpose, configure ADFS as follows:

1. Open the `AD FS Management Console`:
   start `Server Manager`, select `Tools`, `AD FS Management`.

2. In `Authentication Policies`, edit `Global Authentication Policy`.
   For Primary Authentication, enable `Form Authentication` for `Extranet` and `Intranet` but do not enable `device authentication`.
   For Multi-Factor Authentication, require MFA for both `Intranet`and `Extranet`, select 'Mobile ID Authentication' as `additional authentication method`.

3. Make sure that the service account of ADFS have access to the certificate/key used by Mobile ID (step 1.2.2). `winhttpcertcfg.exe` can be used to grant access.

### Step 5: Verification

You can verify the installation by login to the ADFS login web page with a test user.

Assuming you have done the user mapping (see a few lines below) for the test user, you can connect your web browser
`https://<your.adfs.server.dns>/adfs/ls/IdpInitiatedSignon.aspx`. 
After login with user@domain / password, Mobile ID login should occur.

## Operational Tasks

### Mapping of user attributes

Mobile ID authentication provider need to retrieve the mobile ID of the user once the user has been identified with the primary authentication.
The current release relies on the following LDAP attributes in Active Directory:

* `userPrincipleName`:	this is the username that the user authenticates with the primary authentication. Example: `tester1@contoso.com`
* `mobile`:	a telephone number to which the Mobile ID authentication message will be sent to. Example: `+41791234567` or `41791234567`.
* `serialNumber`:	(only when the config parameter `UserSerialNumberPolicy` has a non-default value) the serial number of the user. Example: `MIDCHE0123456789`

For Mobile ID authentication, `userPrincipleName` and `mobile` must be defined.

### Configuration change

If you have modified a configuration file, say `C:\midadfs\v1.3\MobileId.Adfs.AuthnAdapter.xml`, after installation,
you need re-import the config file into ADFS with PowerShell and restart the and restart the `Active Directory Federation Services` and any dependent services:
`````
Import-AdfsAuthenticationProviderConfigurationData -FilePath "C:\midadfs\v1.3\MobileId.Adfs.AuthnAdapter.xml" -Name "MobileID13"
net stop drs
net stop adfssrv
net start adfssrv
net start drs
`````

### Monitoring

Mobile ID authentication provider writes logs to Windows Event Log in containers `Application and Services\Swisscom\MobileID`.
Events with severity level `Error` in `Admin` channels should be monitored by ADFS operators.
The [Mobile ID Microsoft ADFS Solution Guide](https://www.swisscom.ch/en/business/mobile-id/technical-details/technical-documents.html)
contains a list of all Event IDs issued by the Mobile ID authentication provider.

## Uninstallation of the binaries

If you have installed Mobile ID authentication provider with the setup program, you can uninstall it via Control Panel > Uninstall program.

If you have installed Mobile ID authentication provider without a setup program, you can uninstall it as follows:

1. In ADFS Management Console, unselect `Mobile ID Authentication` from any configured Multi-factor authentications.

2. Unregister the Mobile ID Authentication Provider from ADFS: In Windows PowerShell prompt, enter
   `Unregister-AdfsAuthenticationProvider MobileID`

3. Restart ADFS service and dependent services.

4. Remove all DLLs of Mobile ID Authentication Providers from GAC:
   If `gacutil.exe` is available in your runtime environment, you can also remove the DLL from GAC with `gacutil.exe /u MobileId.Adfs.AuthnAdapter` (repeat it for all DLLs installed earlier).

5. If you don't want to keep the Mobile ID logs in Windows Event Log:
   Close `Windows Event Viewer`.
   In Windows Command Prompt, enter
   `````
   wevtutil.exe um C:\midadfs\v1.3\lib\MobileId.ClientService.Swisscom-MobileID-Client.etwManifest.man
   wevtutil.exe um C:\midadfs\v1.3\lib\MobileId.Adfs.AuthnAdapter.Swisscom-MobileID-Adfs.etwManifest.man
   del /Q C:\Windows\System32\winevt\Logs\Swisscom-MobileID-*.*

   `````

## Upgrade

Unless otherwise specified, an binary upgrade is an uninstallation of the binaries, followed by
the installation of Mobile ID Authentication Provider (step 3).

## Troubleshooting

### Windows Event Log

The `Analytic` and/or `Debug` channels `Application and Services\Swisscom\MobileID\`* can be enabled / disabled on demand.
Alternatively, [PerfView](https://www.google.com/search?q=PerfView) can be used to capture the logging events written by Mobile ID Authentication Provider on demand.

### Trace files

The logging / tracing of Mobile ID Authentication Provider can also be controlled via the dotNet tracing
configuration mechanism. The configuration file is shared with configuration file ADFS service, 
which is located in `C:\Windows\ADFS\Microsoft.IdentityServer.ServiceHost.exe.config`.
Mobile ID Authentication Provider writes tracing messages to `MobileId.WebClient` and `MobileId.Adfs.AuthnAdapter`.
You can modify the configuration file to enable / adjust tracing messages of Mobile ID Authentication Provider.

The sample configuration segment write all tracing messages to Windows Event Log and the files 
`C:\midadfs\MobileIdClient.log`, `C:\midadfs\MobileIdAdfs.log`.

`````
...
<system.diagnostics>
  <switches>
    <!--  The next setting specifies the "global" logging severity threshold. In order of decreasing verbosity,
          the value can be one of "All", "Verbose", "Information", "Warning", "Error", "Critical", "None".
    -->
    <add name="MobileId.WebClient.TraceSeverity" value="All"/>
    <add name="MobileId.Adfs.TraceSeverity" value="All"/>
  </switches>

  <sources>
    ...
    <source name="MobileId.WebClient" switchName="MobileId.WebClient.TraceSeverity" switchType="System.Diagnostics.SourceSwitch">
      <listeners>
        <remove name="Default"/>
        <!-- This listener writes to Windows Event Log (Log=Application, EventSource="MobileID") -->
        <add name="eventLog" type="System.Diagnostics.EventLogTraceListener" initializeData="MobileID">
          <filter type="System.Diagnostics.EventTypeFilter" initializeData="Information" />
        </add>
        <!-- This listeners appends to a file for debugging purpose -->
        <add name="logfile" type="System.Diagnostics.TextWriterTraceListener" initializeData="C:\midadfs\MobileIdClient.log">
          <filter type="System.Diagnostics.EventTypeFilter" initializeData="All"/>
        </add>
      </listeners>
    </source>
    <source name="MobileId.Adfs.AuthnAdapter" switchName="MobileId.Adfs.TraceSeverity" switchType="System.Diagnostics.SourceSwitch">
      <listeners>
        <remove name="Default"/>
        <!-- This listener writes to Windows Event Log (Log=Application, EventSource="MobileID.Adfs") -->
        <add name="eventLog" type="System.Diagnostics.EventLogTraceListener" initializeData="MobileID.Adfs">
          <filter type="System.Diagnostics.EventTypeFilter" initializeData="Information" />
        </add>
        <!-- This listens appends to a file for debugging purpose -->
        <add name="logfile" type="System.Diagnostics.TextWriterTraceListener" traceOutputOptions="ProcessId,ThreadId,DateTime" initializeData="C:\midadfs\MobileIdAdfs.log">
          <filter type="System.Diagnostics.EventTypeFilter" initializeData="All"/>
        </add>
      </listeners>
    </source>
  </sources>
  <trace autoflush="true" indentsize="2"></trace>
</system.diagnostics>

`````

## Installation of the Build Environment:

1.	Check out the source code from here to your development PC, for example, folder `H:\midadfs` (subfolders are `Service` and `AuthnAdapter`).

2.	Copy the file Microsoft.IdentityServer.Web.dll from a Windows 2012 R2 server which has the role 
        `Active Directory Federation Services` (AD FS) installed. By default, the DLL file is located in
	`C:\Windows\ADFS` on your server. 
        The DLL file should be copied to the folder of the project `AuthnAdapter`. In the example above, it is `H:\midadfs\AuthnAdapter`.

3.	Create your own assembly-signing key `mobileid.snk`, either in visual studio (right-click a project > `Properties` > `Signing` > `Sign the assembly` > create new key), or with command line (`sn.exe -k 2048 mobileid.snk`).
	Place it in the folder where the *.sln file is located (`H:\midadfs` in the example).

The solution should be ready to build now. Each project folder has a README file which briefly describes the project. The target audience is developer.

# Known Issues

* HTTP Proxy between Mobile ID Servers and ADFS Server(s): currently untested.

__END__
