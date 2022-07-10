@if "%ECHO_STATE%"=="" (@echo off ) else (@echo %ECHO_STATE% )

if "%DotNetVersionString%"=="" goto ERROR
if "%DotNetVersionString%"=="core5" (
set _vers_=50
set _tool_=v143
)
if "%DotNetVersionString%"=="dot48" (
set _vers_=48
set _tool_=v140
)
if "%DotNetVersionString%"=="dot60" (
set _vers_=60
set _tool_=v143
)

:: Prepare locations
set _name_=Tonegenerator
set _call_=%CD%
cd %~dp0
set _here_=%CD%
set _root_=%CD%

:: Set VersionNumber
set TonegeneratorVersionNumber=00000001
set TonegeneratorVersionString=0.0.0.1

:: Set Dependencies
if "%ConsolaBinRoot%"=="" (
set ConsolaBinRoot=%_root_%\..\Consola\bin\%DotNetVersionString%
)
if "%Int24TypesBinRoot%"=="" (
set Int24TypesBinRoot=%_root_%\..\Int24Types\bin\%DotNetVersionString%
)
if "%ControlledValuesBinRoot%"=="" (
set ControlledValuesBinRoot=%_root_%\..\ControlledValues\bin\%DotNetVersionString%
)
if "%MotorsportBinRoot%"=="" (
set MotorsportBinRoot=%_root_%\..\Motorsport-Taskassist\bin\%DotNetVersionString%
)
if "%WaveFileHandlingBinRoot%"=="" (
set WaveFileHandlingBinRoot=%_root_%\..\AudioDataHandling\bin\%DotNetVersionString%
)

:: Set parameters and solution files
call %_root_%\Args "%~1" "%~2" "%~3" "%~4" %_name_%%_vers_%.sln
set _vers_=

:: Do the Build
cd %_here_%
call MsBuild %_target_% %_args_%
cd %_call_%

:: Cleanup Environment
call %_root_%\Args ParameterCleanUp

goto DONE

:ERROR
echo.
echo Variable 'DotNetVersionString' must be set
echo.
:DONE


