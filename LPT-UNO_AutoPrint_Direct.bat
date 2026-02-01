@echo off
REM ========================================
REM LPT-UNO - Auto-Print DIRETO (sem dialogo)
REM ========================================
REM Este .bat abre o web interface com auto-save
REM Aguarda arquivo ser salvo e imprime DIRETO
REM na impressora padrao usando PowerShell

setlocal enabledelayedexpansion

set "HTML_FILE=%~dp0web_interface.html"
set "FILE_URL=file:///%HTML_FILE:\=/%"
set "DATA_FOLDER=%~dp0DATA"

REM Criar pasta DATA se nao existir
if not exist "%DATA_FOLDER%" mkdir "%DATA_FOLDER%"

echo.
echo ========================================
echo   LPT-UNO - Auto-Print DIRETO
echo ========================================
echo.
echo [1] Iniciando monitor de Downloads -> DATA...
REM Iniciar monitor PowerShell oculto (sem janela)
start "" powershell -WindowStyle Hidden -ExecutionPolicy Bypass -File "%~dp0LPT-UNO_MoveToData.ps1"

timeout /t 2 /nobreak >nul

echo [2] Monitor ativo (rodando em background)
echo     Downloads: %USERPROFILE%\Downloads
echo     Destino:   %DATA_FOLDER%
echo.
echo [3] Abrindo interface...
echo     - Auto-Save: ATIVO (via URL)
echo     - Auto-Print Browser: DESATIVADO (via URL)
echo     - Arquivos salvos em: Downloads (movidos automaticamente para DATA)
echo     - Impressao: Via PowerShell Out-Printer
echo.

REM Abrir com auto-print desativado (impressão será via .bat)
start "" "%FILE_URL%#autoprint=false"

timeout /t 3 /nobreak >nul

echo [4] Aguardando novos arquivos na pasta DATA...
echo     (Pressione Ctrl+C para cancelar)
echo.

REM Loop infinito monitorando pasta DATA
:LOOP
    REM Aguardar 2 segundos
    timeout /t 2 /nobreak >nul
    
    REM Procurar arquivos recentes (excluindo IMPRESSO_*)
    for /f "delims=" %%f in ('dir /b /o-d /tc "%DATA_FOLDER%\*_????-??-??_??-??-??.*" 2^>nul ^| findstr /v /i "^IMPRESSO_"') do (
        set "LATEST_FILE=%%f"
        goto :FOUND
    )
    
    goto :LOOP

:FOUND
echo.
echo ========================================
echo [5] Arquivo detectado: !LATEST_FILE!
echo ========================================
echo.

REM Aguardar 2 segundos para garantir que arquivo foi completamente salvo
echo [6] Aguardando arquivo ser completamente salvo...
timeout /t 2 /nobreak >nul

REM Imprimir usando PowerShell Out-Printer (impressora padrao)
echo [7] Enviando para impressora padrao...
echo     Arquivo: !LATEST_FILE!
echo.

REM Verificar extensão e imprimir adequadamente
echo !LATEST_FILE! | findstr /i ".txt .csv" >nul
if !errorlevel! equ 0 (
    REM Arquivos texto - usar Out-Printer
    powershell -Command "Get-Content '%DATA_FOLDER%\!LATEST_FILE!' | Out-Printer"
) else (
    REM Outros formatos - usar Start-Process com verbo Print
    powershell -Command "Start-Process -FilePath '%DATA_FOLDER%\!LATEST_FILE!' -Verb Print -Wait"
)

if !errorlevel! equ 0 (
    echo.
    echo ========================================
    echo   SUCESSO! Arquivo impresso.
    echo ========================================
    echo.
) else (
    echo.
    echo ========================================
    echo   ERRO ao imprimir!
    echo ========================================
    echo.
)

REM Renomear arquivo para evitar reimprimir
ren "%DATA_FOLDER%\!LATEST_FILE!" "IMPRESSO_!LATEST_FILE!"
echo [8] Arquivo renomeado: IMPRESSO_!LATEST_FILE!

REM Continuar monitorando para proximos arquivos
echo [9] Aguardando proximo arquivo...
echo.
goto :LOOP
