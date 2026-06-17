@echo off
REM Build do EscalaBMC: publica um executavel self-contained (.NET 8) em dist\EscalaBMC.
REM Nao precisa de Python: o PDF e gerado pelo proprio app (C#/QuestPDF).
REM
REM IMPORTANTE: a copia para dist NUNCA toca em data\ nem output\. O app e publicado em uma
REM pasta temporaria e somente os arquivos do programa sao espelhados para dist, preservando
REM integralmente os dados reais (militares, escalas, configuracoes) e os PDFs gerados.
set MSBuildEnableWorkloadResolver=false

set "DIST_DIR=%~dp0dist\EscalaBMC"
set "STAGE_DIR=%TEMP%\EscalaBMC-stage-%RANDOM%-%RANDOM%"

echo Publicando o app...
dotnet publish "%~dp0EscalaBMC.csproj" -c Release -r win-x64 --self-contained true -o "%STAGE_DIR%"
if errorlevel 1 (
  rmdir /s /q "%STAGE_DIR%" 2>nul
  exit /b 1
)

REM Espelha os arquivos do app para dist, EXCLUINDO as pastas data e output:
REM   - nao copia o "data" semente do publish (preserva os dados reais do dist);
REM   - nao apaga data\ nem output\ existentes no dist durante o /MIR.
robocopy "%STAGE_DIR%" "%DIST_DIR%" /MIR /XD data output /NFL /NDL /NJH /NJS /NP >nul
if errorlevel 8 (
  echo.
  echo ERRO ao copiar para dist\EscalaBMC. Feche o EscalaBMC se estiver aberto e rode novamente.
  rmdir /s /q "%STAGE_DIR%" 2>nul
  exit /b 1
)

REM Apenas em instalacao NOVA (dist ainda sem data): leva os dados semente do publish.
if not exist "%DIST_DIR%\data" robocopy "%STAGE_DIR%\data" "%DIST_DIR%\data" /E /NFL /NDL /NJH /NJS /NP >nul

REM GARANTIA: o gerador ANTIGO de tabela (Python/reportlab) nunca volta para o dist.
REM O PDF e gerado SOMENTE pelo motor C#/QuestPDF (PdfExport.cs), compilado dentro do exe.
if exist "%DIST_DIR%\pdf_exporter" rmdir /s /q "%DIST_DIR%\pdf_exporter"
if exist "%DIST_DIR%\python_pdf" rmdir /s /q "%DIST_DIR%\python_pdf"

rmdir /s /q "%STAGE_DIR%" 2>nul
echo.
echo Executavel publicado em:
echo %DIST_DIR%\EscalaBMC.exe
