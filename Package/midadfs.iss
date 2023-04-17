
#define MyAppName "Mobile ID Authentication Provider for ADFS"
#define MyAppShortName "Mobile ID for ADFS"
#define MyAppAbb "MobileIdAdfs"
#define MyAppVersion "1.3"
#define MyAppFullVersion "1.3.4.0"

[Setup]
AppId={{609C382B-1D2D-40F5-B2ED-742C603AD024}
AppName={#MyAppName}
AppVersion={#MyAppFullVersion}
AppPublisher=Swisscom Ltd.
AppPublisherURL=https://www.swisscom.com/
AppSupportURL=https://github.com/MobileID-Strong-Authentication/mobileid-enabler-adfs
AppUpdatesURL=https://github.com/MobileID-Strong-Authentication/mobileid-enabler-adfs/tree/main/binaries
; AppUpdatesURL=http://goo.gl/cp1BCU
AppCopyright=(C) 2015-2023, Swisscom Ltd.
DefaultDirName={pf}\{#MyAppAbb}\v{#MyAppVersion}
DefaultGroupName={#MyAppName}
LicenseFile=..\LICENSE
;InfoAfterFile=post_install.txt
OutputDir=..\binaries
OutputBaseFilename=midadfs_setup_{#MyAppFullVersion}
Compression=lzma
SolidCompression=yes
SetupLogging=yes
PrivilegesRequired=admin
VersionInfoVersion={#MyAppFullVersion}
UninstallFilesDir={app}\inst
MinVersion=6.3.9200

; workaround for the unsupported {%envname} constant in SignTool value
#define PFXPass GetEnv('PFXPass')
SignTool=signtool /p {#PFXPASS} /d $q{#MyAppShortName}$q /du https://www.swisscom.com/mid $f
SignedUninstaller=yes

; build a ZIP file containing items of [Files]
#expr Exec("7z.exe", "a midadfs-bin_" + SetupSetting("AppVersion") + ".zip *.dll *.man de fr it", "..\binaries", 2)

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
;Source: "..\binaries\*.dll"; DestDir: "{app}\lib"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\binaries\*.resources.dll"; DestDir: "{app}\lib"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\binaries\MobileId.ClientService.dll"; DestDir: "{app}\lib"; Flags: ignoreversion
Source: "..\binaries\MobileId.Adfs.AuthnAdapter.dll"; DestDir: "{app}\lib"; Flags: ignoreversion
Source: "..\binaries\Microsoft.Diagnostics.Tracing.EventSource.dll"; DestDir: "{app}\lib"; Flags: ignoreversion
Source: "..\binaries\*.etwManifest.dll"; DestDir: "{app}\lib"; Flags: ignoreversion uninsneveruninstall
Source: "..\binaries\*.etwManifest.man"; DestDir: "{app}\lib"; Flags: ignoreversion uninsneveruninstall
Source: "..\AuthnAdapter\spin.min.js"; DestDir: "{app}\lib"
Source: "..\samples\MobileId.Adfs.AuthnAdapter-template.xml"; DestDir: "{app}"; DestName: "MobileId.Adfs.AuthnAdapter.xml"
; Source: "..\Admin\*.psm1"; DestDir: "{app}\lib"
; Before this script is compiled by ISCC, ..\Admin\*.psm1 are copied to ..\binaries and then signed.
Source: "..\binaries\*.psm1"; DestDir: "{app}\lib"; Flags: ignoreversion uninsneveruninstall
;Source: "..\Admin\*.ps1"; DestDir: "{app}"
;Source: "..\Admin\*.cmd"; DestDir: "{app}"
Source: "..\Admin\import_config.ps1"; DestDir: "{app}"
Source: "..\Admin\import_config.cmd"; DestDir: "{app}"
Source: "..\Admin\register_etw.ps1"; DestDir: "{app}"
Source: "..\Admin\register_etw.cmd"; DestDir: "{app}"
Source: "..\Admin\register_midadfs.ps1"; DestDir: "{app}"
Source: "..\Admin\register_midadfs.cmd"; DestDir: "{app}"
Source: "..\Admin\unregister_midadfs.ps1"; DestDir: "{app}"
Source: "..\Admin\unregister_midadfs.cmd"; DestDir: "{app}"
Source: "..\Admin\unregister_etw.ps1"; DestDir: "{app}"; Flags: ignoreversion uninsneveruninstall
Source: "..\Admin\unregister_etw.cmd"; DestDir: "{app}"; Flags: ignoreversion uninsneveruninstall
Source: "..\certs\mobileid-ca-ssl.crt"; DestDir: "{app}\certs"
Source: "..\certs\codesigning-swisscom.crt"; DestDir: "{app}\certs"
Source: "..\certs\Swisscom_Root_CA_2_der.crt"; DestDir: "{app}\certs"
Source: "..\3RD_PARTY.md"; DestDir: "{app}\license"
Source: "..\LICENSE"; DestDir: "{app}\license"; DestName: "MobileId_LICENSE.txt"
Source: "install_midadfs.cmd"; DestDir: "{app}"; Flags: deleteafterinstall

;[Icons]
;Name: "{group}\v{#MyAppVersion}\Uninstall"; Filename: "{uninstallexe}"

[Run]
; We redirect logs to files for troubleshooting purpose, so nothing is displayed in console
Filename: "{app}\install_midadfs.cmd"; WorkingDir: "{app}"; Parameters: ".\inst\setup_trace.log .\inst\setup.log"; StatusMsg: "Registering Mobile ID in ADFS..."; Flags: shellexec waituntilterminated runhidden

[UninstallDelete]
Name: "{app}\inst\setup.log"; Type: files
Name: "{app}\inst\setup_trace.log"; Type: files

[UninstallRun]
; we don't keep uninstall log but display them in console
Filename: "{app}\unregister_midadfs.cmd"; WorkingDir: "{app}"; StatusMsg: "Unregistering Mobile ID v{#MyAppFullVersion} from ADFS..."; Flags: shellexec waituntilterminated

