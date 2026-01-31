@echo off
:: ========================================
:: LPT-UNO - Auto-Print Mode Launcher
:: ========================================
:: Este script abre o LPT-UNO Web Interface com auto-print ativado
:: Desenvolvido por: nasp2000
:: Data: 2026-01-31
:: ========================================

title LPT-UNO - Auto-Print Mode
color 0A

echo.
echo ========================================
echo   LPT-UNO - Auto-Print Mode
echo ========================================
echo.
echo Iniciando Web Interface com auto-print ativado...
echo.

:: Caminho para o arquivo HTML
set "HTML_FILE=%~dp0web_interface.html"

:: Verificar se o arquivo existe
if not exist "%HTML_FILE%" (
    echo [ERRO] Arquivo web_interface.html nao encontrado!
    echo Procurando em: %HTML_FILE%
    echo.
    pause
    exit /b 1
)

:: Converter caminho para URL file:// (substituir \ por /)
set "FILE_URL=file:///%HTML_FILE:\=/%"

:: Abrir no navegador padrão com parâmetro de auto-print
echo Abrindo navegador...
start "" "%FILE_URL%#autoprint=true"

echo.
echo Web Interface iniciado com sucesso!
echo Auto-Print: ATIVADO
echo.
echo O navegador ira abrir automaticamente.
echo Feche esta janela apos a abertura.
echo.
timeout /t 3 /nobreak >nul
exit
