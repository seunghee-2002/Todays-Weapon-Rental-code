@echo off
REM Balance Simulator - legacy upgrade solo-effect sweep (roadmap 1-4)
REM Usage: RunSweep.bat [seeds] [weeks]     (default 100 / 70)
REM Runs baseline + 20 mirrored upgrades, each as "that one upgrade at max level, rest 0",
REM 1 career run per seed. Output: Documents/Simulation/sim_upgrade_sweep_*.md
REM Cost: 21 configs x personas x seeds - roughly 9x a normal full run. Unattended.
REM Log:  Tools/BalanceSim/sweep_log.txt
REM NOTE: Close the Unity Editor first - an open project locks the folder.
REM ASCII only. cmd.exe reads this file as the OEM codepage; non-ASCII breaks parsing.

setlocal

set "UNITY=C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe"
set "PROJ=%~dp0..\.."
set "LOG=%~dp0sweep_log.txt"

set "SEEDS=%~1"
if "%SEEDS%"=="" set "SEEDS=100"
set "WEEKS=%~2"
if "%WEEKS%"=="" set "WEEKS=70"

if not exist "%UNITY%" (
    echo [RunSweep] Unity not found: %UNITY%
    exit /b 9
)

echo [RunSweep] seeds=%SEEDS% weeks=%WEEKS% - running upgrade sweep...

"%UNITY%" -batchmode -quit -nographics -projectPath "%PROJ%" -executeMethod TodaysWeaponRental.BalanceSimulatorWindow.RunBatch -simSeeds %SEEDS% -simWeeks %WEEKS% -simUpgradeSweep -logFile "%LOG%"

set "RC=%ERRORLEVEL%"
echo [RunSweep] exit code %RC% - log: %LOG%
exit /b %RC%
