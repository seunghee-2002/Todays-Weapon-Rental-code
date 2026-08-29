@echo off
REM Balance Simulator - endless template difficulty sweep
REM Usage: RunEndlessSweep.bat [seeds] [weeks]     (default 60 / 100)
REM Runs baseline (sequential replay) + each endless template pinned from week 61 on,
REM measuring pass rate / bankruptcy / survival past the campaign.
REM Weeks must exceed 60 or no run reaches the endless section.
REM Output: Documents/Simulation/sim_endless_sweep_*.md
REM Cost: 17 configs x personas x seeds. Unattended.
REM Log:  Tools/BalanceSim/endless_log.txt
REM NOTE: Close the Unity Editor first - an open project locks the folder.
REM ASCII only. cmd.exe reads this file as the OEM codepage; non-ASCII breaks parsing.

setlocal

set "UNITY=C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe"
set "PROJ=%~dp0..\.."
set "LOG=%~dp0endless_log.txt"

set "SEEDS=%~1"
if "%SEEDS%"=="" set "SEEDS=60"
set "WEEKS=%~2"
if "%WEEKS%"=="" set "WEEKS=100"

if not exist "%UNITY%" (
    echo [RunEndlessSweep] Unity not found: %UNITY%
    exit /b 9
)

echo [RunEndlessSweep] seeds=%SEEDS% weeks=%WEEKS% - running endless template sweep...

"%UNITY%" -batchmode -quit -nographics -projectPath "%PROJ%" -executeMethod TodaysWeaponRental.BalanceSimulatorWindow.RunBatch -simSeeds %SEEDS% -simWeeks %WEEKS% -simEndlessSweep -logFile "%LOG%"

set "RC=%ERRORLEVEL%"
echo [RunEndlessSweep] exit code %RC% - log: %LOG%
exit /b %RC%
