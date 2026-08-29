@echo off
REM Balance Simulator - batch runner
REM Usage: RunSim.bat [seeds] [weeks] [force|-] [runs] [goldPerLegacy|-] [traits] [seer] [named] [preferNamed]
REM        (default 100 / 100 / - / 5 / asset / 1 / 1 / 1 / 1)
REM        3rd arg "force" = force morning-event participation (diagnostic run), "-" = skip
REM        4th arg = max career runs per seed (legacy rebirth loop)
REM        5th arg = goldPerLegacy override for A/B ("-" or omit = LegacyConfig.asset value)
REM        6th arg = traits 1|0 (0 = disable trait mirror for integrity A/B, default 1)
REM        7th arg = seer 1|0 (0 = disable seer mirror for integrity A/B, default 1)
REM        8th arg = named 1|0 (0 = disable named/affection mirror, default 1)
REM        9th arg = preferNamed 1|0 (0 = named-blind bot policy, default 1)
REM       10th arg = craft 1|0 (0 = disable weapon craft/reroll mirror, default 1)
REM Output: Documents/Simulation/sim_summary_*.md, sim_raw_*.csv
REM Log:    Tools/BalanceSim/sim_log.txt
REM NOTE: Close the Unity Editor first - an open project locks the folder.
REM ASCII only. cmd.exe reads this file as the OEM codepage; non-ASCII breaks parsing.

setlocal

set "UNITY=C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe"
set "PROJ=%~dp0..\.."
set "LOG=%~dp0sim_log.txt"

set "SEEDS=%~1"
if "%SEEDS%"=="" set "SEEDS=100"
set "WEEKS=%~2"
if "%WEEKS%"=="" set "WEEKS=100"
set "FORCE="
if /i "%~3"=="force" set "FORCE=-simForceMorning"
set "RUNS=%~4"
if "%RUNS%"=="" set "RUNS=5"
set "GPL="
if not "%~5"=="" if not "%~5"=="-" set "GPL=-simGoldPerLegacy %~5"
set "TRAITS=%~6"
if "%TRAITS%"=="" set "TRAITS=1"
set "SEER=%~7"
if "%SEER%"=="" set "SEER=1"
set "NAMED=%~8"
if "%NAMED%"=="" set "NAMED=1"
set "PREFNAMED=%~9"
if "%PREFNAMED%"=="" set "PREFNAMED=1"
shift
set "CRAFT=%~9"
if "%CRAFT%"=="" set "CRAFT=1"

if not exist "%UNITY%" (
    echo [RunSim] Unity not found: %UNITY%
    exit /b 9
)

echo [RunSim] seeds=%SEEDS% weeks=%WEEKS% force=%FORCE% runs=%RUNS% gpl=%GPL% traits=%TRAITS% seer=%SEER% named=%NAMED% prefer=%PREFNAMED% craft=%CRAFT% - running...

"%UNITY%" -batchmode -quit -nographics -projectPath "%PROJ%" -executeMethod TodaysWeaponRental.BalanceSimulatorWindow.RunBatch -simSeeds %SEEDS% -simWeeks %WEEKS% %FORCE% -simRuns %RUNS% %GPL% -simTraits %TRAITS% -simSeer %SEER% -simNamed %NAMED% -simPreferNamed %PREFNAMED% -simCraft %CRAFT% -logFile "%LOG%"

set "RC=%ERRORLEVEL%"
echo [RunSim] exit code %RC% - log: %LOG%
exit /b %RC%
