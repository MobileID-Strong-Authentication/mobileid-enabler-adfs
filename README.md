# Microsoft AD FS - MobileID MFA Integration

[![GitHub release](https://img.shields.io/github/v/release/MobileID-Strong-Authentication/mobileid-enabler-adfs?display_name=tag&sort=semver&style=for-the-badge)](https://github.com/MobileID-Strong-Authentication/mobileid-enabler-adfs/releases/latest)

[MobileID](https://mobileid.ch) for AD FS enables Multi-Factor-Authentication (MFA) capabilities for users that are logging on using Microsoft Active Directory Federation Services (AD FS).

You can install the MobileID MFA Adapter on a single AD FS instance. If you have an AD FS farm deployment, you will need to install MobileID MFA Adapter on all AD FS instances in the farm to enable MFA.

##### Table of Contents  
* [System Requirement ](#system-requirement)  
* [Runtime Installation - Full Tutorial](#runtime-installation---full-tutorial)  
* [Operational Tasks](#operational-tasks)  
* [Uninstallation of the binaries](#uninstallation-of-the-binaries)  
* [Upgrade](#upgrade)  
* [Troubleshooting](#troubleshooting)  
* [Installation of the Build Environment](#installation-of-the-build-environment)  

## System Requirement 

A [Mobile ID Account](swisscom.com/mid) is required. Please check the following points before you continue with this tutorial:
* Read the [Mobile ID Reference Guide](https://github.com/MobileID-Strong-Authentication/mobileid-api/blob/main/doc/) chapter 2 and 8
* You got your unique `AP_ID` (Application Provider Identifier) from Swisscom
* You have created a PFX file (PKCS#12 / private key / with a .p12 file extension) according to chapter 8. In this tutorial it is referred as `YourMobileIdKeyFile.p12`
* You have created a CRT-file (X509 Cert / public key) and wrote down the value of the thumbprint (sometimes called fingerprint). In this tutorial that value is required for the `SslMidClientCertThumbprint` configuration in the [myconfig13.xml](https://github.com/MobileID-Strong-Authentication/mobileid-enabler-adfs/blob/main/samples/myconfig13.xml). To get a SHA1 fingerprint: ```$ openssl x509 -in YourMobileIdCrtFile.crt -fingerprint -noout```
* The source IP address (or range) of your AD FS server (or the proxy) has been whitelisted on the Mobile ID Firwall (IP whitelist)

### Runtime Environment

#### Installer v1.3.x

* Works with Microsoft Windows Server 2016, 2019, 2022

#### Installer v1.2.x

* Works with Microsoft Windows Server 2012 R2

### Build Environment

If you don't want to build from source code, the compiled binaries and a setup program can be downloaded from the [binaries subfolder](./binaries).

* Microsoft Visual Studio 2022 (which includes Microsoft .NET Framework 4.8.*)
* The file `Microsoft.IdentityServer.Web.dll`. The DLL file can be copied from a Microsoft Windows Server instance.
* [InnoSetup](http://www.innosetup.org) if you want to rebuild the setup program

---

## Runtime Installation - Full Tutorial

For this tutorial we use a Windows Server 2022 instance from Amazon Elastic Compute Cloud (Amazon EC2) to build an environment from scratch to demo Mobile ID MFA using AD FS.

##### Tutorial Overview
[Step 1: Create Windows Server Instance](#step-1-create-windows-server-instance)  
[Step 2: Install AD Domain](#step-2-install-ad-domain)  
[Step 3: Install ADFS Core](#step-3-install-adfs-core)  
[Step 4: Add Test User](#step-4-add-test-user)  
[Step 5: Install MID/ADFS Enabler](#step-5-install-midadfs-enabler)  
[Step 6: Setup MID Certs](#step-6-setup-mid-certs)  
[Step 7: Login with MID](#step-7-login-with-mid)  

### Step 1: Create Windows Server Instance

#### Create a new Amazon Machine Image (AMI) Windows Server instance

If you are not familiar with Amazon EC2, please read their [GetStarted Tutorial](https://docs.aws.amazon.com/AWSEC2/latest/WindowsGuide/EC2_GetStarted.html).

* Select **Microsoft Windows Server 2022 Base (Datacenter edition)** as Amazon Machine Image (AMI)
* Select instance type. We recommend `t3.large` as a start.
* Create a VPC, enable Auto Public IP
* Create a SecurityGroup and make sure your source IP address is whitelisted in the inbound RDP rule
* Create and associate an Elastic IP Address (EIP) and make sure this IP address has been whitelisted on the Mobile ID Firewall. 
* Select **allow dissassociate**.
* Click the **Connect** Button, select **RDP client** and download the remote desktop file to your Desktop
* Click **Get password**, select your key pair and write down the Windows password

Note: You can anytime stop or start an instance, which helps to keep costs low. Only run the instance when it is really used.

#### Connect to the Windows Server instance

* On your Desktop, start the Remote Desktop Client and load the RDP Profile
* In the RDP Client, adjust the Screen Resolution
* In the RDP Client, select your local drive (gain access to local files)
* Save the changes to your local remote desktop file
* Connect to the Windows Server and login with the password retrieved in step above

#### Basic configuration

* Adjust the timezone of your Windows Server
* Adjust the region format
* Open Server Manager -> Local Server: Disable IE Enhanced Security Configuration
* Copy file from your local disk to the Windows Server's `C:\Users\Administrator\Downloads`. 

You need at least these files:
* `YourMobileIdKeyFile.p12` - Mobile ID account PFX file (PFX/PKCS#12 format with .p12 file extension)
* `midadfs_setup_1.3.*.exe` - Latest [Installer Binary](https://github.com/MobileID-Strong-Authentication/mobileid-enabler-adfs/releases)
* `myconfig13.xml` - [Configuration file](https://github.com/MobileID-Strong-Authentication/mobileid-enabler-adfs/tree/main/samples)
* `sdcs-root4.crt` - [Swisscom Root CA 2 Cert](http://aia.swissdigicert.ch/sdcs-root4.crt)
* `sdcs-root2.crt` - [Swisscom Root CA 4 Cert](http://aia.swissdigicert.ch/sdcs-root2.crt)

Please adapt the `myconfig13.xml` as you wish. At least the following parameters must be changed to match your own account and key details:
* `AP_ID`  (Application Provider Identifier - you will usually get this information with the Swisscom welcome email)
* `SslMidClientCertThumbprint` (the SHA1 thumbprint of your account's public certificate: ```$ openssl x509 -in YourMobileIdCrtFile.crt -fingerprint -noout```)
* `DtbsPrefix` (DataToBeSigned prefix text - you will usually get this information with the Swisscom welcome email) 

#### Verify the connectivity to Mobile ID API

You can either use Internet Explorer to try to connect to https://mobileid.swisscom.com (which should return a 404/PageNotFound) or you can use the PowerShell Script [Get-RemoteSSLCertificate.ps1](https://github.com/MobileID-Strong-Authentication/mobileid-enabler-adfs/tree/main/tools):

```
cd "C:\Users\Administrator\Downloads\"
$cert=(.\Get-RemoteSSLCertificate.ps1 mobileid.swisscom.com)
Set-Content mobileid.swisscom.com.cer -Encoding Byte -Value $cert.Export('Cert')
```

Now check if the file `mobileid.swisscom.com.cer` has been created by the script. 
If it exists it means the connectivity worked. Also check if the certificate is valid (open the file).

Note: There are also other critical connectivity requirements such as  ldap.swissdigitcert.ch. Refer to the [PDF Table 1](./doc/mobile_id_microsoft_adfs_solution_guide_v1_3.pdf).

### Step 2: Install AD Domain

```
$secpass=ConvertTo-SecureString "pass@word1" -AsPlainText -Force
Install-WindowsFeature -Name AD-Domain-Services -IncludeManagementTools
import-module ADDSDeployment
Install-ADDSForest -DomainName "contoso.intern" -InstallDNS -SafeModeAdministratorPassword $secpass
```

This will ask you to reboot the System.

### Step 3: Install ADFS Core

The ADFS Service will run in the context of a GMSA Account. Create a GSMA Account:

```
Add-KdsRootKey -EffectiveTime ((get-date).addhours(-10))
New-ADServiceAccount FsGmsa -DNSHostName adfs1.contoso.intern -ServicePrincipalNames http/adfs1.contoso.intern
```

For ADFS to work, we need a Certificate. The following lines create a self signed certificate with the required Subject Name and Subject Alternative Names.

```
$selfSignedCert = New-SelfSignedCertificate -DnsName adfs1.contoso.intern,enterpriseregistration.contoso.intern,adfs1.contoso.intern -CertStoreLocation cert:\LocalMachine\My
$certThumbprint = $selfSignedCert.Thumbprint
dir Cert:\LocalMachine\My
```

Install the self-signed certificate to have the required trust:

* Run mmc.exe
* Add snap-in Certificates (Computer)
* Go to **Certificates**>**Personal**>**Certificates**
* Right-click on **adfs1.contoso.intern** and select **export**
* Double-click the exported **adfs1.contoso.intern.cer** and install it to **Local Machine**

Install ADFS Federation:

```
Install-WindowsFeature -IncludeManagementTools -Name ADFS-Federation
Import-Module ADFS
Install-AdfsFarm -CertificateThumbprint $certThumbprint -FederationServiceDisplayName "Contost ADFS Test" -GroupServiceAccountIdentifier "contoso.intern\FsGmsa$" -FederationServiceName "adfs1.contoso.intern"
Set-AdfsProperties -EnableIdpInitiatedSignonPage $true
```

In the internal DNS Service of AD, configure the following A Record and a CNAME. Please **replace the IP accordingly**.

```
$ipAdress = "10.0.0.25"
Add-DnsServerResourceRecordA -Name "adfs1" -ZoneName "contoso.intern" -AllowUpdateAny -IPv4Address $ipAdress -TimeToLive 01:00:00
Add-DnsServerResourceRecordCName -Name "enterpriseregistration" -HostNameAlias "adfs1.contoso.intern" -ZoneName "contoso.intern"
```

* Open Edge Browser and visit: https://adfs1.contoso.intern/adfs/ls/IdpInitiatedSignon.aspx
* Add this site to the **trusted sites** list.
* View the site's certificate details and click **Install Certificate**, select **Local Machine**

At this point we have the basic ADFS Demo Setup (without MID/ADFS Authentication Provider) completed.

### Step 4: Add Test User

* Run **Server Manager** -> **Tools** -> **Active Directory Users and Computers**
* Go to **contoso.intern** -> Users -> right-click and select **New -> User**
* Set First- and Last name, User logon name, click next
* Set password and only select **Password never expires**, finish
* Double-click User and select **Telephones**-register
* Set a Mobile phone number that has an [active MobileID](https://mobileid.ch/login) account! It works in the format `+41-79 xxx xx xx`.

### Step 5: Install MID/ADFS Enabler

Run `midadfs_setup_1.3.*.exe` (as admin).
Check Logs in Event Viewer and in `C:\Program Files (x86)\MobileIdAdfs\v1.3\inst`.

Make sure, the it became available in ADFS Management Console:

```
Get-AdfsAuthenticationProvider -Name MobileID13
```

Enable MFA in ADFS, Run "Server Manager" -> "Tools" -> "AD FS Management"

* AD FS Management: AD FS -> Service -> Authentication Methods -> Edit Mulit-factor Authentication Methods -> Select **Mobile ID Authenticator v1.3**
* AD FS Management: AD FS -> Relying Party Trusts -> Add Relying Party Trust...
* Select **Claims aware**
* Select **Enter data about the relying party manually**
* Display name **mobileid.ch**
* Select only **Enable support for the SAML 2.0 WebSSO protocol** and set value https://mobileid.ch
* Next, add https://mobileid.ch as Relying party trust identifier!
* Next, select **Permit everyone and require MFA**
* Once finished, open it again and go to **Endpoints**-tab and edit the SAML endpoint to set Binding to **Redirect** (to https://mobileid.ch)

Note: The steps above are for simple demo purposes only!

Copy the Swisscom Root CA certificates to the folder `C:\Program Files (x86)\MobileIdAdfs\v1.3\certs`
* `C:\Program Files (x86)\MobileIdAdfs\v1.3\certs\sdcs-root2.crt`
* `C:\Program Files (x86)\MobileIdAdfs\v1.3\certs\sdcs-root4.crt`

Load the MID/ADFS configuration file `myConfig13.xml`.

```
cd "C:\Program Files (x86)\MobileIdAdfs\v1.3"
.\import_config.ps1 "C:\Users\Administrator\Downloads\myConfig13.xml"
```

### Step 6: Setup MID Certs

Right-click your SSL Client certificate file
* Install PFX
* Local Machine
* Click Next twice
* Enter passphrase
* click Next and finish

Only if AP Client Cert is self-signed, do this:

* Run mmc.exe
* Add/Remove Snap-in…, select Certificates
* snap-ins panel, click Add > Computer account, click Next, Finish
* Local Computer; right-click Trusted People, navigate to All Task, then Import,
* this opens the Certificate Import Wizard; clicks next, 
* locate the PFX file in File to Import, next, enter passphrase
* clicks next twice, finish

IMPORTANT: Always run `mmc.exe` as Administrator to import the certs into LocalMachine (the `certmgr.msc` imports to CurrentUser only)

Give ADFS Service Account the required access to the client certificate
If winhttpcertcfg is not in the path, you might find it in `C:\Program Files (x86)\Windows Resource Kits\Tools\`.
If you do not already have the WinHttpCertCfg.exe tool on your Windows Server, download and install it from [Microsoft](https://www.microsoft.com/en-us/download/details.aspx?id=19801).

Please change the subject (in the example below it is adfs-dev.swisscom.ch) according to your own client certificate subject.

```
cd "C:\Program Files (x86)\Windows Resource Kits\Tools"
.\winhttpcertcfg.exe -g -c LOCAL_MACHINE\My -s adfs-dev.swisscom.ch -a contoso\\fsgmsa$
```

### Step 7: Login with MID

Finally, open the Internet Browser (Ms Edge) and visit: https://adfs1.contoso.intern/adfs/ls/IdpInitiatedSignon.aspx

You should be able to select mobileid.ch and then enter the test user credentials. This should invoke a Mobile ID authentication request to the phone number configured for this test user.

---

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

---

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

---

## Upgrade

Unless otherwise specified, an binary upgrade is an uninstallation of the binaries, followed by
the installation of Mobile ID Authentication Provider (step 3).

---

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

---

## Installation of the Build Environment

1.	Check out the source code from here to your development PC, for example, folder `H:\midadfs` (subfolders are `Service` and `AuthnAdapter`).

2.	Copy the file Microsoft.IdentityServer.Web.dll from a Windows 2012 R2 server which has the role 
        `Active Directory Federation Services` (AD FS) installed. By default, the DLL file is located in
	`C:\Windows\ADFS` on your server. 
        The DLL file should be copied to the folder of the project `AuthnAdapter`. In the example above, it is `H:\midadfs\AuthnAdapter`.

3.	Create your own assembly-signing key `mobileid.snk`, either in visual studio (right-click a project > `Properties` > `Signing` > `Sign the assembly` > create new key), or with command line (`sn.exe -k 2048 mobileid.snk`).
	Place it in the folder where the *.sln file is located (`H:\midadfs` in the example).

The solution should be ready to build now. Each project folder has a README file which briefly describes the project. The target audience is developer.
