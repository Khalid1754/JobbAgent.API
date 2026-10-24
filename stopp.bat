@echo off
rem Stoppar JobbAgent. CV, sokningar och brev finns kvar i databasen till nasta start.
cd /d "%~dp0"
docker compose stop
pause
