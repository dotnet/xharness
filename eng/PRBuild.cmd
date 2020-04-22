@echo off
powershell -ExecutionPolicy ByPass -NoProfile -command "& """%~dp0/common/Build.ps1""" -restore -build -test -sign -pack -ci %*"
