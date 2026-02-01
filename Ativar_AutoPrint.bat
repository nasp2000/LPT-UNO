@echo off
REM Ativa o auto-print criando o arquivo .autoprint_enabled na pasta DATA
set "DATA_DIR=%~dp0DATA"
if not exist "%DATA_DIR%" mkdir "%DATA_DIR%"
echo Auto-print ativado > "%DATA_DIR%\.autoprint_enabled"

REM Inicia o LPT-UNO_AutoPrint_Direct.bat para abrir a interface e monitorar impressao
call "%~dp0LPT-UNO_AutoPrint_Direct.bat"

echo Auto-print ATIVADO e interface aberta.
pause