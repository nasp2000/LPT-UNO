@echo off
REM Desativa o auto-print removendo o arquivo .autoprint_enabled da pasta DATA
set "DATA_DIR=%~dp0DATA"
del /q "%DATA_DIR%\.autoprint_enabled"

REM Inicia o LPT-UNO_AutoPrint_Direct.bat para abrir a interface (sem auto-print)
call "%~dp0LPT-UNO_AutoPrint_Direct.bat"

echo Auto-print DESATIVADO e interface aberta.
pause