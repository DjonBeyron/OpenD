@echo off
rem Builds OpenD with the C# compiler built into Windows (.NET Framework 4.8). Nothing to install.
rem lib\ holds the WebView2 DLLs (login window); they are copied next to OpenD.exe.
setlocal
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist dist mkdir dist
"%CSC%" /nologo /target:winexe /optimize+ /codepage:65001 /out:dist\OpenD.exe /win32manifest:src\app.manifest ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll ^
  /r:lib\Microsoft.Web.WebView2.Core.dll /r:lib\Microsoft.Web.WebView2.WinForms.dll ^
  /resource:assets\fonts\Montserrat-Medium.ttf,OpenD.Montserrat-Medium.ttf ^
  /resource:assets\fonts\Montserrat-Regular.ttf,OpenD.Montserrat-Regular.ttf ^
  /resource:assets\fonts\Montserrat-SemiBold.ttf,OpenD.Montserrat-SemiBold.ttf ^
  src\*.cs
if errorlevel 1 (echo BUILD FAILED & exit /b 1)
copy /y lib\*.dll dist\ >nul
echo Done: dist\OpenD.exe
