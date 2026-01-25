@echo off
chcp 65001 > nul
title Iniciar LPT-UNO (Impressao Automatica)

echo ========================================================
echo   LPT-UNO Launcher - Impressao Automatica
echo ========================================================
echo.
echo ATENCAO: Feche outras janelas do navegador antes
echo para melhor funcionamento da impressao automatica.
echo.
echo Procurando Google Chrome ou Microsoft Edge...
echo.

:: Define o caminho do arquivo HTML
set "HTML_PATH=%~dp0web_interface.html"

:: Verificar se o arquivo existe
if not exist "%HTML_PATH%" goto :ERROR_NOTFOUND

:: Converter para formato URL
set "HTML_URL=%HTML_PATH%"
set "HTML_URL=%HTML_URL:\=/%"
set "HTML_URL=file:///%HTML_URL%?kiosk=true"

echo Arquivo encontrado OK
echo.

:: Tentar Google Chrome 32-bit
if exist "%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe" goto :CHROME32

:: Tentar Microsoft Edge 32-bit
if exist "%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe" goto :EDGE32

:: Tentar Google Chrome 64-bit
if exist "%ProgramFiles%\Google\Chrome\Application\chrome.exe" goto :CHROME64

:: Tentar Microsoft Edge 64-bit
if exist "%ProgramFiles%\Microsoft\Edge\Application\msedge.exe" goto :EDGE64

:: Navegador padrao
goto :DEFAULT

:CHROME32
echo [OK] Google Chrome encontrado
echo Abrindo interface...
start "" "%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe" --kiosk-printing --new-window "%HTML_URL%"
goto :SUCCESS

:CHROME64
echo [OK] Google Chrome encontrado
echo Abrindo interface...
start "" "%ProgramFiles%\Google\Chrome\Application\chrome.exe" --kiosk-printing --new-window "%HTML_URL%"
goto :SUCCESS

:EDGE32
echo [OK] Microsoft Edge encontrado
echo Abrindo interface...
start "" "%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe" --kiosk-printing --new-window "%HTML_URL%"
goto :SUCCESS

:EDGE64
echo [OK] Microsoft Edge encontrado
echo Abrindo interface...
start "" "%ProgramFiles%\Microsoft\Edge\Application\msedge.exe" --kiosk-printing --new-window "%HTML_URL%"
goto :SUCCESS

:DEFAULT
echo [INFO] Abrindo com navegador padrao...
start "" "%HTML_URL%"
goto :SUCCESS

:ERROR_NOTFOUND
echo.
echo [ERRO] Arquivo web_interface.html nao encontrado!
echo Caminho esperado: %HTML_PATH%
echo.
echo IMPORTANTE: O arquivo .bat deve estar na mesma
echo pasta que o arquivo web_interface.html
echo.
pause
exit

:ERROR_NOTFOUND
echo.
echo [ERRO] Arquivo web_interface.html nao encontrado!
echo Caminho esperado: %HTML_PATH%
echo.
echo IMPORTANTE: O arquivo .bat deve estar na mesma
echo pasta que o arquivo web_interface.html
echo.
pause
exit

:SUCCESS
echo.
echo ========================================================
echo   Interface LPT-UNO iniciada com sucesso!
echo ========================================================
echo.
echo O botao Auto-imprimir agora funciona sem confirmacao.
echo Voce pode fechar esta janela.
echo.
timeout /t 5
exit
