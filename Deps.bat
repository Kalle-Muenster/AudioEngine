@if "%ECHO_STATE%"=="" ( @echo off ) else ( @echo %ECHO_STATE% )

if "%~1"=="dot48" (
set DotNetVersionString=dot48
echo Set Dotnet Version 4.8
goto END
)
if "%~1"=="core5" (
set DotNetVersionString=core5
echo Set Dotnet Version 5.0
goto END
)
if "%~1"=="dot60" (
set DotNetVersionString=dot60
echo Set Dotnet Version 6.0
goto END
)

if "%DotNetVersionString%"=="core5" (
set ConsolaProject=%~dp0..\Consola\ConsolaCore5
set ControllerProject=%~dp0..\ControlledValues\Core5Dll
set TaskAssistProject=%~dp0..\Motorsport-Taskassist
set WaveLibProject=%~dp0..\AudioDataHandling\dll\core5
) else if "%DotNetVersionString%"=="dot48" (
set ConsolaProject=%~dp0..\Consola\ConsolaDot48
set ControllerProject=%~dp0..\ControlledValues\DotnetDll
set TaskAssistProject=%~dp0..\Motorsport-Taskassist
set WaveLibProject=%~dp0..\AudioDataHandling\dll\dot48
) else if "%DotNetVersionString%"=="dot60" (
set ConsolaProject=%~dp0..\Consola\ConsolaDot60
set ControllerProject=%~dp0..\ControlledValues\Core5Dll
set TaskAssistProject=%~dp0..\Motorsport-Taskassist
set WaveLibProject=%~dp0..\AudioDataHandling\dll\core5
)
set Int24TypesProject=%~dp0..\Int24Types\%DotNetVersionString%


set ARCH=%~1
set CONF=%~2
set CLEAN=%~3

pushd %ConsolaProject%
call Build.cmd "%ARCH%" "%CONF%" %CLEAN%
call Build.cmd "%ARCH%" "%CONF%" Test %CLEAN%
popd

pushd "%Int24TypesProject%"
call Build.cmd "%ARCH%" "%CONF%" %CLEAN%
popd

pushd "%ControllerProject%"
call Build.cmd "%ARCH%" "%CONF%" %CLEAN%
popd

pushd "%TaskAssistProject%"
call Build.cmd "%ARCH%" "%CONF%" %CLEAN%
popd

pushd "%WaveLibProject%"
call Build.cmd "%ARCH%" "%CONF%" %CLEAN%
popd

pushd "%~dp0"
call Build.cmd "%ARCH%" "%CONF%" %CLEAN%
popd

:END
set ARCH=
set CONF=
set CLEAN=
