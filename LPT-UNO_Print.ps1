# ========================================
# LPT-UNO_Print.ps1
# Impressao de texto com configuracoes do web interface
# ========================================
# Uso: .\LPT-UNO_Print.ps1 -FilePath "caminho\para\arquivo.txt"
# Le configuracoes do sidecar: arquivo.txt.lptcfg.json (se existir)
# ========================================

param(
    [Parameter(Mandatory=$true)]
    [string]$FilePath
)

Add-Type -AssemblyName System.Drawing

# ─── Configuracoes padrao ─────────────────────────────────────────────────────
$cfg = @{
    fontSize    = "auto"
    margin      = "5mm"
    orientation = "portrait"
    paperSize   = "A4"
    lineHeight  = 1.2
    copies      = 1
    center      = $false
}

# ─── Carregar sidecar de configuracoes (se existir) ───────────────────────────
$sidecarPath = $FilePath + ".lptcfg.json"

if (Test-Path $sidecarPath) {
    try {
        $json = Get-Content $sidecarPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($json.fontSize)    { $cfg.fontSize    = $json.fontSize }
        if ($json.margin)      { $cfg.margin      = $json.margin }
        if ($json.orientation) { $cfg.orientation = $json.orientation }
        if ($json.paperSize)   { $cfg.paperSize   = $json.paperSize }
        if ($json.PSObject.Properties.Name -contains 'lineHeight') { $cfg.lineHeight = [double]$json.lineHeight }
        if ($json.PSObject.Properties.Name -contains 'copies')     { $cfg.copies     = [int]$json.copies }
        if ($json.PSObject.Properties.Name -contains 'center')     { $cfg.center     = [bool]$json.center }
        Write-Host "  Configuracoes carregadas: $sidecarPath" -ForegroundColor Cyan
    } catch {
        Write-Host "  [AVISO] Nao foi possivel ler configuracoes: $_" -ForegroundColor Yellow
    }
} else {
    Write-Host "  [INFO] Sem sidecar de configuracoes - a usar defaults" -ForegroundColor Gray
}

# ─── Validar ficheiro de texto ────────────────────────────────────────────────
if (-not (Test-Path $FilePath)) {
    Write-Host "[ERRO] Ficheiro nao encontrado: $FilePath" -ForegroundColor Red
    exit 1
}

# ─── Dimensoes do papel (mm) ──────────────────────────────────────────────────
$paperMmMap = @{
    "A4"     = @(210.0, 297.0)
    "A3"     = @(297.0, 420.0)
    "Letter" = @(215.9, 279.4)
    "Legal"  = @(215.9, 355.6)
}
$dims = $paperMmMap[$cfg.paperSize]
if (-not $dims) { $dims = $paperMmMap["A4"] }
$pageWmm = $dims[0]
$pageHmm = $dims[1]
if ($cfg.orientation -eq "landscape") { $tmp = $pageWmm; $pageWmm = $pageHmm; $pageHmm = $tmp }

# ─── Margem: converter mm → centesimos de polegada (unidade Margins) ─────────
$marginMm = 5.0
if ($cfg.margin -match '(\d+(\.\d+)?)mm') { $marginMm = [double]$matches[1] }
$marginHundredths = [int]($marginMm / 25.4 * 100)

# ─── Ler texto ────────────────────────────────────────────────────────────────
$rawLines = @(Get-Content $FilePath -Encoding UTF8 | ForEach-Object { $_.TrimEnd() })
Write-Host "  Linhas: $($rawLines.Count)" -ForegroundColor Gray

# ─── Calcular tamanho de letra ────────────────────────────────────────────────
$fontSizePt = 10.0

if ($cfg.fontSize -ne "auto") {
    if ($cfg.fontSize -match '(\d+(\.\d+)?)pt') { $fontSizePt = [double]$matches[1] }
} else {
    $printableWmm = $pageWmm - ($marginMm * 2)
    $printableHmm = $pageHmm - ($marginMm * 2)
    $printableWpt = $printableWmm / 25.4 * 72.0
    $printableHpt = $printableHmm / 25.4 * 72.0

    $maxLen = 1
    foreach ($l in $rawLines) { if ($l.Length -gt $maxLen) { $maxLen = $l.Length } }
    $lineCount = [Math]::Max($rawLines.Count, 1)

    # Courier New: ~0.6 ems de largura por caracter
    $wLimit = $printableWpt / ([Math]::Max(1, $maxLen * 0.6))
    $hLimit = $printableHpt / ([Math]::Max(1, $lineCount * $cfg.lineHeight))
    $fontSizePt = [Math]::Max(8, [int][Math]::Min($wLimit, $hLimit, 36))
}

Write-Host "  Papel: $($cfg.paperSize) $($cfg.orientation) | Margem: $($cfg.margin) | Letra: ${fontSizePt}pt | Copias: $($cfg.copies)" -ForegroundColor Cyan

# ─── Configurar PrintDocument ─────────────────────────────────────────────────
$script:printLines    = $rawLines
$script:printFont     = New-Object System.Drawing.Font("Courier New", [float]$fontSizePt, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Point)
$script:printBrush    = [System.Drawing.Brushes]::Black
$script:lineSpaceMult = [double]$cfg.lineHeight
$script:printCenter   = [bool]$cfg.center
$script:lineIndex     = 0

$pd = New-Object System.Drawing.Printing.PrintDocument
$pd.DocumentName = "LPT-UNO Print"

# Papel: procurar no printer o PaperKind correto
$paperKindMap = @{
    "A4"     = [System.Drawing.Printing.PaperKind]::A4
    "A3"     = [System.Drawing.Printing.PaperKind]::A3
    "Letter" = [System.Drawing.Printing.PaperKind]::Letter
    "Legal"  = [System.Drawing.Printing.PaperKind]::Legal
}
$targetKind = $paperKindMap[$cfg.paperSize]
if ($targetKind) {
    $matchedPS = $null
    foreach ($ps in $pd.PrinterSettings.PaperSizes) {
        if ($ps.Kind -eq $targetKind) { $matchedPS = $ps; break }
    }
    if ($matchedPS) {
        $pd.DefaultPageSettings.PaperSize = $matchedPS
    }
}

$pd.DefaultPageSettings.Landscape = ($cfg.orientation -eq "landscape")
$pd.DefaultPageSettings.Margins   = New-Object System.Drawing.Printing.Margins($marginHundredths, $marginHundredths, $marginHundredths, $marginHundredths)
# Nota: PrinterSettings.Copies e ignorado por muitos drivers - usamos loop manual abaixo

# ─── Evento PrintPage ─────────────────────────────────────────────────────────
$pd.add_PrintPage({
    $ev     = $args[1]
    $bounds  = $ev.MarginBounds
    $fontH   = $script:printFont.GetHeight($ev.Graphics)
    $spacing = $fontH * $script:lineSpaceMult
    $y       = [float]$bounds.Top

    while ($script:lineIndex -lt $script:printLines.Count) {
        $line = $script:printLines[$script:lineIndex]
        if ($script:printCenter) {
            $lw = $ev.Graphics.MeasureString($line, $script:printFont).Width
            $x  = [float]($bounds.Left + ($bounds.Width - $lw) / 2.0)
        } else {
            $x = [float]$bounds.Left
        }
        $ev.Graphics.DrawString($line, $script:printFont, $script:printBrush, $x, $y)
        $y += $spacing
        $script:lineIndex++
        if (($y + $spacing) -gt [float]$bounds.Bottom -and $script:lineIndex -lt $script:printLines.Count) {
            $ev.HasMorePages = $true
            return
        }
    }
    $ev.HasMorePages = $false
})

# ─── Imprimir (loop por copia para garantir compatibilidade com todos os drivers) ───
try {
    $totalCopies = [Math]::Max(1, [int]$cfg.copies)
    for ($copy = 1; $copy -le $totalCopies; $copy++) {
        $script:lineIndex = 0   # reiniciar para cada copia
        $pd.Print()
        Write-Host "  [OK] Copia $copy/$totalCopies impressa." -ForegroundColor Green
        if ($copy -lt $totalCopies) { Start-Sleep -Milliseconds 500 }
    }
} catch {
    Write-Host "  [ERRO] Falha na impressao: $_" -ForegroundColor Red
    exit 1
} finally {
    $script:printFont.Dispose()
    $pd.Dispose()
}
