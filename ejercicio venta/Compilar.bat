@echo off
chcp 65001 >nul
cd /d "%~dp0"
title Compilar Punto de Venta

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo No se encontro el compilador de C# de .NET Framework 4.
  echo Instala .NET Framework 4.8 o Visual Studio y vuelve a intentar.
  pause
  exit /b 1
)

if not exist Program.cs (
  echo No se encontro Program.cs. Debe estar en la misma carpeta que este .bat
  pause
  exit /b 1
)

echo Compilando...
"%CSC%" /nologo /target:winexe /codepage:65001 /out:PuntoDeVenta.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll Program.cs
if errorlevel 1 (
  echo.
  echo ERROR al compilar. Revisa los mensajes de arriba.
  pause
  exit /b 1
)

echo.
echo Listo: PuntoDeVenta.exe creado en esta carpeta.
echo Usuario inicial: admin   Contrasena: admin123
choice /m "Abrir la aplicacion ahora"
if errorlevel 2 exit /b 0
start "" "%~dp0PuntoDeVenta.exe"
