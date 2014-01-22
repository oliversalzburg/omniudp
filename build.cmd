@ECHO OFF
SETLOCAL
SET SOLUTION_FILENAME=OmniUdp.sln
IF EXIST packages\NuGet.exe (
  SET EnableNuGetPackageRestore=true
  echo Installing NuGet packages...
  FOR /F "delims=" %%n in ('dir /b /s packages.config') DO (
    packages\NuGet.exe install "%%n" -o packages
  )
)

IF NOT EXIST C:\Windows\Microsoft.NET\Framework\v* (
  ECHO No .NET Framework found. Go to http://www.microsoft.com/en-us/download/details.aspx?id=30653
) ELSE (
  FOR /F %%f in ('dir /B /O-N C:\Windows\Microsoft.NET\Framework\v*') DO (
    ECHO Using C:\Windows\Microsoft.NET\Framework\%%f
    CD /D C:\Windows\Microsoft.NET\Framework\%%f
    GOTO :build
  )
  :build
  ECHO Building...
  MSBuild.exe "%~dp0%SOLUTION_FILENAME%" /t:Rebuild /p:Configuration=Release /p:Platform="Any CPU"
)
ENDLOCAL
