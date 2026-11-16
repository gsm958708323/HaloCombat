@echo off
setlocal EnableExtensions DisableDelayedExpansion
title HaloCombat Skill Lab
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\SkillLab\Start.ps1" %*
set "SkillLabExitCode=%ERRORLEVEL%"
if not "%SkillLabExitCode%"=="0" (
    echo.
    echo SkillLab failed to start. See the error above.
    pause
)
exit /b %SkillLabExitCode%
