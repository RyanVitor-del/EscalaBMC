@echo off
set MSBuildEnableWorkloadResolver=false
dotnet run --project "%~dp0EscalaBMC.csproj"
