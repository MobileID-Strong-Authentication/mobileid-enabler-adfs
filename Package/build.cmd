@rem This script assumes the folllowing:
@rem 1) ISCC.exe (Inno Setup Command Line Compiler) is in PATH, otherwise you need to adapt the variable ISCC
@rem 2) signtool.exe (from Windows SDK) must be in PATH, otherwise you have to add its path to PATH via Control Panel (not temporarily via PATH=%PATH%;... in Command Windows), or adapt the variable psigntool.

set ISCC=ISCC.exe
set b=..\binaries
set /P PFXPass=Enter Coder Signer Password (no white space, '"', '^', '$'): 
set sign=signtool.exe sign /f codesigner.pfx /p %PFXPass% /fd SHA256 /t http://timestamp.verisign.com/scripts/timstamp.dll

del /Q %b%\*.psm1
copy ..\Admin\*.psm1 %b%

@rem Don't sign *.resouces.dll, otherwise midadfs won't load
%sign% %b%\MobileId.ClientService.dll %b%\MobileId.Adfs.AuthnAdapter.dll %b%\*.psm1

for %%i in (signtool.exe) do set psigntool=%%~dp$PATH:i
%ISCC% "/Ssigntool=$q%psigntool%signtool.exe$q sign /f %~dp0codesigner.pfx /fd SHA256 /t http://timestamp.verisign.com/scripts/timstamp.dll $p" midadfs.iss

set PFXPass=blank
set sign=blank
