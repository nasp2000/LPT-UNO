# ========================================
# LPT-UNO - Monitor Downloads -> DATA
# ========================================

$downloadsPath = "$env:USERPROFILE\Downloads"
$dataPath = Split-Path -Parent $PSCommandPath | Join-Path -ChildPath "DATA"

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
            
            try {
                # Tentar mover arquivo
                $destPath = Join-Path $dataPath $file.Name
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

# Só imprime se o flag .autoprint_enabled existir
$autoPrintFlag = Join-Path $dataPath ".autoprint_enabled"
$autoPrintActive = Test-Path $autoPrintFlag
if (-not $autoPrintActive) {
    Write-Host "[INFO] Impressão automática DESATIVADA (.autoprint_enabled não encontrado)" -ForegroundColor Yellow
    Start-Sleep -Seconds 3
    continue
}
