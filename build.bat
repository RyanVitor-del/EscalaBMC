@echo off
set MSBuildEnableWorkloadResolver=false
if not exist "%~dp0python_pdf\pdf_export_cli.py" (
  echo Exportador Python local nao encontrado em "%~dp0python_pdf".
  exit /b 1
)
if not exist "%~dp0python_pdf\pdf_export.py" (
  echo pdf_export.py local nao encontrado em "%~dp0python_pdf".
  exit /b 1
)
if not exist "%~dp0python_pdf\escala_logic.py" (
  echo escala_logic.py local nao encontrado em "%~dp0python_pdf".
  exit /b 1
)
if not exist "%~dp0python_pdf\models.py" (
  echo models.py local nao encontrado em "%~dp0python_pdf".
  exit /b 1
)
echo Recompilando exportador Python...
if exist "%~dp0pdf_exporter" rmdir /s /q "%~dp0pdf_exporter"
python -m PyInstaller --noconfirm --clean --onedir --name EscalaPdfExporter --distpath "%~dp0obj\pyinstaller-pdf\dist" --workpath "%~dp0obj\pyinstaller-pdf\build" --specpath "%~dp0obj\pyinstaller-pdf\spec" --paths "%~dp0python_pdf" --add-data "%~dp0assets;assets" --collect-all reportlab "%~dp0python_pdf\pdf_export_cli.py"
if errorlevel 1 exit /b %errorlevel%
robocopy "%~dp0obj\pyinstaller-pdf\dist\EscalaPdfExporter" "%~dp0pdf_exporter" /MIR >nul
if errorlevel 8 exit /b %errorlevel%
for /f "delims=" %%v in ('"%~dp0pdf_exporter\EscalaPdfExporter.exe" --version') do echo %%v

set "DIST_DIR=%~dp0dist\EscalaBMC"
set "BACKUP_DIR=%TEMP%\EscalaBMC-build-backup-%RANDOM%-%RANDOM%"
if exist "%DIST_DIR%\data" (
  if not exist "%BACKUP_DIR%" mkdir "%BACKUP_DIR%"
  robocopy "%DIST_DIR%\data" "%BACKUP_DIR%\data" /MIR >nul
  if errorlevel 8 exit /b %errorlevel%
)
if exist "%DIST_DIR%\output" (
  if not exist "%BACKUP_DIR%" mkdir "%BACKUP_DIR%"
  robocopy "%DIST_DIR%\output" "%BACKUP_DIR%\output" /MIR >nul
  if errorlevel 8 exit /b %errorlevel%
)

if exist "%DIST_DIR%" rmdir /s /q "%DIST_DIR%"
if exist "%DIST_DIR%" (
  echo Nao foi possivel limpar a pasta dist\EscalaBMC. Feche o EscalaBMC aberto e rode novamente.
  exit /b 1
)
dotnet publish "%~dp0EscalaBMC.csproj" -c Release -r win-x64 --self-contained true -o "%DIST_DIR%"
if errorlevel 1 exit /b %errorlevel%
if exist "%BACKUP_DIR%\data" (
  robocopy "%BACKUP_DIR%\data" "%DIST_DIR%\data" /MIR >nul
  if errorlevel 8 exit /b %errorlevel%
)
if exist "%BACKUP_DIR%\output" (
  robocopy "%BACKUP_DIR%\output" "%DIST_DIR%\output" /MIR >nul
  if errorlevel 8 exit /b %errorlevel%
)
if exist "%BACKUP_DIR%" rmdir /s /q "%BACKUP_DIR%"
echo.
echo Executavel publicado em:
echo %DIST_DIR%\EscalaBMC.exe
