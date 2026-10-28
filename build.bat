@echo off
setlocal
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (echo .NET Framework compiler not found. & exit /b 1)
if not exist "%~dp0dist" mkdir "%~dp0dist"
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /out:"%~dp0dist\PinkWechat.exe" /win32icon:"%~dp0pinkcat.ico" "/resource:%~dp0pinkcat.png,pinkcat.png" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "%~dp0Audio.cs" "%~dp0Program.cs"
exit /b %errorlevel%

