# ========================================
# LPT-UNO - Monitor Downloads -> DATA
# ========================================

$downloadsPath = "$env:USERPROFILE\Downloads"
$dataPath   = Join-Path $PSScriptRoot 'DATA'
$rootPath   = $PSScriptRoot

# Criar pasta DATA se não existir
if (-not (Test-Path $dataPath)) {
    New-Item -ItemType Directory -Path $dataPath -Force | Out-Null
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  LPT-UNO - Monitor de Downloads" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Downloads: $downloadsPath" -ForegroundColor Yellow
Write-Host "DATA:      $dataPath" -ForegroundColor Yellow
Write-Host ""
Write-Host "Aguardando arquivos com timestamp..." -ForegroundColor Green
Write-Host "(Padrão: *_YYYY-MM-DD_HH-MM-SS.*)" -ForegroundColor Gray
Write-Host "(Pressione Ctrl+C para cancelar)" -ForegroundColor Gray
Write-Host ""

# DEBUG: Mostrar arquivos recentes em Downloads
Write-Host "[DEBUG] Listando arquivos recentes em Downloads:" -ForegroundColor Magenta
$recentFiles = Get-ChildItem -Path $downloadsPath | Sort-Object LastWriteTime -Descending | Select-Object -First 5
foreach ($f in $recentFiles) {
    Write-Host "  - $($f.Name) (Modificado: $($f.LastWriteTime))" -ForegroundColor DarkGray
}
Write-Host ""

# Loop infinito
while ($true) {
    # Verificar se _lptcfg.json chegou em Downloads (configurações de impressão)
    # → Mover para a pasta raiz LPT-UNO (ao lado do web_interface.html e dos scripts)
    $cfgFile = Join-Path $downloadsPath '_lptcfg.json'
    if (Test-Path $cfgFile) {
        $timestamp = Get-Date -Format "HH:mm:ss"
        $cfgDest   = Join-Path $rootPath '_lptcfg.json'
        Write-Host "[$timestamp] Config de impressão detectada: _lptcfg.json" -ForegroundColor Cyan
        try {
            Move-Item -Path $cfgFile -Destination $cfgDest -Force -ErrorAction Stop
            Write-Host "           [OK] _lptcfg.json atualizada em: $rootPath" -ForegroundColor Green
        } catch {
            Write-Host "           [AVISO] Nao foi possivel mover _lptcfg.json: $_" -ForegroundColor Yellow
        }
    }

    # Procurar arquivos com padrão de timestamp: *_YYYY-MM-DD_HH-MM-SS.*
    # Isso captura arquivos salvos pelo auto-save independente do nome configurado
    $files = Get-ChildItem -Path $downloadsPath -Filter "*_????-??-??_??-??-??.*" -ErrorAction SilentlyContinue
    
    if ($files.Count -gt 0) {
        Write-Host "[DEBUG] Encontrados $($files.Count) arquivo(s)" -ForegroundColor Cyan
    }
    
    foreach ($file in $files) {
        # Verificar se é txt, csv ou pdf
        if ($file.Extension -match '\.(txt|csv|pdf)$') {
            $timestamp = Get-Date -Format "HH:mm:ss"
            Write-Host "[$timestamp] Detectado: $($file.Name)" -ForegroundColor White
            Write-Host "           Tamanho: $($file.Length) bytes" -ForegroundColor Gray
            Write-Host "           Caminho: $($file.FullName)" -ForegroundColor Gray
            
            # Aguardar 5 segundos para browser liberar completamente
            Write-Host "           Aguardando 5 segundos..." -ForegroundColor Yellow
            Start-Sleep -Seconds 5
            
            # Verificar se arquivo ainda existe
            if (-not (Test-Path $file.FullName)) {
                Write-Host "           Arquivo desapareceu!" -ForegroundColor Red
                Write-Host ""
                continue
            }
            
            # Verificar se ficheiro está bloqueado (ainda a ser escrito pelo browser)
            $fileLocked = $true
            try {
                $stream = [System.IO.File]::Open($file.FullName, 'Open', 'Read', 'None')
                $stream.Close()
                $stream.Dispose()
                $fileLocked = $false
            } catch {
                $fileLocked = $true
            }
            if ($fileLocked) {
                Write-Host "           [AVISO] Ficheiro ainda bloqueado - aguardar próximo ciclo." -ForegroundColor Yellow
                Write-Host ""
                continue
            }
            
            try {
                # Criar subpasta DATA\YYYY-MM-DD\ para o dia de hoje
                $dateFolder = Get-Date -Format "yyyy-MM-dd"
                $datePath   = Join-Path $dataPath $dateFolder
                if (-not (Test-Path $datePath)) {
                    New-Item -ItemType Directory -Path $datePath -Force | Out-Null
                    Write-Host "           [OK] Subpasta criada: $dateFolder" -ForegroundColor Cyan
                }

                # Tentar mover arquivo para subpasta do dia
                $destPath = Join-Path $datePath $file.Name
                Write-Host "           Tentando mover para: $destPath" -ForegroundColor Gray
                
                Move-Item -Path $file.FullName -Destination $destPath -Force -ErrorAction Stop
                
                Write-Host "           [OK] Movido com sucesso!" -ForegroundColor Green
                Write-Host ""
            }
            catch {
                Write-Host "           [ERRO] $($_.Exception.Message)" -ForegroundColor Red
                Write-Host "           Tentando copiar em vez de mover..." -ForegroundColor Yellow
                
                try {
                    # Plano B: Copiar e depois deletar
                    $dateFolder = Get-Date -Format "yyyy-MM-dd"
                    $datePath   = Join-Path $dataPath $dateFolder
                    if (-not (Test-Path $datePath)) {
                        New-Item -ItemType Directory -Path $datePath -Force | Out-Null
                    }
                    $destPath = Join-Path $datePath $file.Name
                    Copy-Item -Path $file.FullName -Destination $destPath -Force -ErrorAction Stop
                    Start-Sleep -Milliseconds 500
                    Remove-Item -Path $file.FullName -Force -ErrorAction Stop
                    Write-Host "           [OK] Copiado e deletado!" -ForegroundColor Green
                }
                catch {
                    Write-Host "           [FALHA] $($_.Exception.Message)" -ForegroundColor Red
                }
                Write-Host ""
            }
        }
    }
    
    # Aguardar 3 segundos antes de próxima verificação
    Start-Sleep -Seconds 3
}
