@echo off
REM Regenerate WeeklyQuestData assets from WeeklyQuestGenerator.
REM Output: Assets/_Projects/Data/WeeklyQuests/*.asset
REM Log:    Tools/BalanceSim/quest_log.txt
REM NOTE: Close the Unity Editor first - an open project locks the folder.
REM ASCII only. cmd.exe reads this file as the OEM codepage; non-ASCII breaks parsing.

setlocal

set "UNITY=C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe"
set "PROJ=%~dp0..\.."
set "LOG=%~dp0quest_log.txt"

if not exist "%UNITY%" (
    echo [GenQuests] Unity not found: %UNITY%
    exit /b 9
)

echo [GenQuests] regenerating weekly quest assets...

"%UNITY%" -batchmode -quit -nographics -projectPath "%PROJ%" -executeMethod TodaysWeaponRental.WeeklyQuestGenerator.Generate -logFile "%LOG%"

set "RC=%ERRORLEVEL%"
echo [GenQuests] exit code %RC% - log: %LOG%
exit /b %RC%
