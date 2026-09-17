@echo off
REM Build the segmentation server using docker-compose from the repository root
cd /d "%~dp0..\.."
docker-compose build segmentation-server