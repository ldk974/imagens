# ============================
# baixar_imagem_releases_final.ps1
# ============================
# SALVE ESTE ARQUIVO COMO "UTF-8 with BOM"
# ============================

# Força a consola a usar UTF-8 (corrige acentuação)
try { chcp 65001 > $null } catch {}
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::InputEncoding  = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

# ---------------------------------------
# CONFIGURAÇÕES (edite apenas se necessário)
# ---------------------------------------
$owner = "ldk974"
$repo  = "imagens"
$releasesApi = "https://api.github.com/repos/$owner/$repo/releases?per_page=100"

# Destino final (sem extensão), sobrescreve sempre
$destFolder = Join-Path $env:APPDATA "Microsoft\Windows\Themes"
$destFileName = "TranscodedWallpaper.jpg"   # sem extensão
$finalPath = Join-Path $destFolder $destFileName

# Tempo / retries / headers
$maxDownloadAttempts = 3
$timeoutSec = 30
$commonHeaders = @{ "User-Agent" = "baixar_imagem_script/1.0" }

# Forçar TLS1.2
try { [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 } catch {}

# ---------------------------------------
# FUNÇÕES AUXILIARES
# ---------------------------------------
function Write-ErrAndExit([string]$msg, [int]$code = 1) {
    Write-Host $msg -ForegroundColor Red
    Start-Sleep -Seconds 2
    exit $code
}

function IsLikelyJPEG($path) {
    try {
        $bytes = Get-Content -Path $path -Encoding Byte -TotalCount 2 -ErrorAction Stop
        if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xD8) { return $true }
    } catch {}
    return $false
}

function Try-DownloadWithRetries($url, $outFile) {
    for ($attempt = 1; $attempt -le $maxDownloadAttempts; $attempt++) {
        try {
            Invoke-WebRequest -Uri $url -OutFile $outFile -Headers $commonHeaders -TimeoutSec $timeoutSec -ErrorAction Stop

            if (-not (Test-Path $outFile) -or (Get-Item $outFile).Length -eq 0) {
                throw "Arquivo vazio após download."
            }

            # quick check for HTML/429 by reading a bit as text
            $maybeText = $null
            try { $maybeText = Get-Content -Path $outFile -TotalCount 5 -Encoding UTF8 -ErrorAction SilentlyContinue -Raw } catch {}
            if ($maybeText -and ($maybeText -match '(?i)Too many requests|<html|<!doctype|429: Too many requests')) {
                throw "Provavel resposta HTML/429 retornada em vez do binário."
            }

            return $true
        } catch {
            Write-Host ("Tentativa {0}/{1} falhou: {2}" -f $attempt, $maxDownloadAttempts, $_.Exception.Message) -ForegroundColor Yellow
            Remove-Item -Path $outFile -ErrorAction SilentlyContinue
            if ($attempt -lt $maxDownloadAttempts) {
                $wait = [math]::Pow(2, $attempt)  # backoff: 2,4,8...
                Write-Host ("Aguardando {0} segundos antes de nova tentativa..." -f $wait)
                Start-Sleep -Seconds $wait
            }
        }
    }
    return $false
}

function Extract-Number($name) {
    if ($name -match '^0*([0-9]+)\.jpg$') { return [int]$matches[1] } else { return $null }
}

# ---------------------------------------
# AVISO INICIAL (consentimento)
# ---------------------------------------
Clear-Host
Write-Host "============================================" -ForegroundColor Yellow
Write-Host "  AVISO IMPORTANTE - ALTERAÇÕES NO SISTEMA  " -ForegroundColor Red
Write-Host "============================================" -ForegroundColor Yellow
Write-Host ""
Write-Host " - Este script substitui um arquivo na pasta de temas do Windows." -ForegroundColor Yellow
Write-Host " - As imagens contem conteúdo NSFW e explicíto." -ForegroundColor Yellow
Write-Host ""
Write-Host "Se você NÃO concorda, digite 'Q' e pressione ENTER para sair."
Write-Host "Se concorda, pressione ENTER para continuar."
Write-Host "`n"
$resp = Read-Host "Pressione ENTER para continuar ou Q para sair"
if ($resp -match '^(q|Q)$') { Write-Host "Saindo..."; Start-Sleep -Seconds 1; exit 0 }

# ---------------------------------------
# 1) Obter releases e agregar assets .jpg
# ---------------------------------------
Clear-Host
Write-Host "Consultando releases em $owner/$repo ..." -ForegroundColor Cyan
try {
    $releases = Invoke-RestMethod -Uri $releasesApi -Headers $commonHeaders -TimeoutSec $timeoutSec -ErrorAction Stop
} catch {
    Write-ErrAndExit "Erro ao consultar GitHub API: $($_.Exception.Message)"
}
if (-not $releases -or $releases.Count -eq 0) { Write-ErrAndExit "Nenhuma release encontrada no repositório." }

$allAssets = @()
foreach ($rel in $releases) {
    if ($null -eq $rel.assets -or $rel.assets.Count -eq 0) { continue }
    foreach ($a in $rel.assets) {
        if ($a.name -match '\.jpg$') {
            $obj = [PSCustomObject]@{
                Name = $a.name
                Url  = $a.browser_download_url
                Downloads = $a.download_count
                ReleaseTag = $rel.tag_name
                AssetId = $a.id
            }
            $allAssets += $obj
        }
    }
}
if ($allAssets.Count -eq 0) { Write-ErrAndExit "Nenhum asset .jpg encontrado em todas as releases." }

# remove duplicatas e ordena (numérico quando possível)
$uniqueAssets = @(); $seenNames = @{}
foreach ($it in $allAssets) {
    if (-not $seenNames.ContainsKey($it.Name)) { $uniqueAssets += $it; $seenNames[$it.Name] = $true }
}
$sorted = $uniqueAssets | Sort-Object @{Expression = { $n = Extract-Number $_.Name; if ($n -ne $null) { return $n } else { return [int]::MaxValue } }; Ascending = $true }, @{Expression = { $_.Name }; Ascending = $true }

# ---------------------------------------
# 2) Mostrar menu com contador de downloads
# ---------------------------------------
Clear-Host
Write-Host "`nImagens disponíveis (ordenadas):`n" -ForegroundColor Cyan
for ($i = 0; $i -lt $sorted.Count; $i++) {
    $idx = $i + 1
    $name = $sorted[$i].Name
    $dl   = $sorted[$i].Downloads
    $tag  = $sorted[$i].ReleaseTag
    Write-Host ("[{0}] {1}    | Downloads: {2}    | Release: {3}" -f $idx, $name, $dl, $tag)
}

$sel = Read-Host "`nDigite o número da imagem desejada (ou 'q' para sair)"
if ($sel -eq 'q' -or $sel -eq 'Q') { Write-Host "Saindo..."; Start-Sleep -Seconds 1; exit 0 }
if (-not ($sel -match '^\d+$')) { Write-ErrAndExit "Seleção inválida." }
$index = [int]$sel - 1
if ($index -lt 0 -or $index -ge $sorted.Count) { Write-ErrAndExit "Índice fora do intervalo." }

$chosen = $sorted[$index]
Write-Host ("Você escolheu: {0} (Release: {1})" -f $chosen.Name, $chosen.ReleaseTag) -ForegroundColor Green

# confirmação final antes do download/substituição
$confirm = Read-Host "`nTem certeza que deseja baixar e substituir o arquivo do sistema? (S/N)"
if ($confirm -notmatch '^(s|S|y|Y)$') { Write-Host "Operação cancelada pelo usuário."; Start-Sleep -Seconds 1; exit 0 }

# ---------------------------------------
# 3) Download resiliente
# ---------------------------------------
$tempFile = Join-Path $env:TEMP ("download_asset_{0}_{1}" -f $chosen.AssetId, $chosen.Name)
Write-Host ("`nBaixando {0} ..." -f $chosen.Name)
$ok = Try-DownloadWithRetries -url $chosen.Url -outFile $tempFile
if (-not $ok) { Write-ErrAndExit "Falha ao baixar o asset depois de várias tentativas." }

if (-not (IsLikelyJPEG $tempFile)) {
    Remove-Item -Path $tempFile -ErrorAction SilentlyContinue
    Write-ErrAndExit "O arquivo baixado não parece ser um JPEG válido (provavel resposta HTML/erro)."
}

# ---------------------------------------
# 4) Substituir o arquivo do sistema
# ---------------------------------------
try {
    if (-not (Test-Path $destFolder)) { New-Item -ItemType Directory -Path $destFolder -Force | Out-Null }
} catch {
    Remove-Item -Path $tempFile -ErrorAction SilentlyContinue
    Write-ErrAndExit "Não foi possível garantir a pasta destino: $destFolder"
}

try {
    Move-Item -Path $tempFile -Destination $finalPath -Force
} catch {
    Remove-Item -Path $tempFile -ErrorAction SilentlyContinue
    Write-ErrAndExit "Erro ao mover o arquivo para o destino: $($_.Exception.Message)"
}

Write-Host "`nConcluído: arquivo substituído em: $finalPath" -ForegroundColor Green

# ---------------------------------------
# 5) Menu final: Reiniciar / Fechar / Desligar
# ---------------------------------------
while ($true) {
    Write-Host ""
    Write-Host "O novo papel de parede apenas será aplicado quando o computador for reiniciado." -ForegroundColor Yellow
	Write-Host ""
    Write-Host "Escolha o que deseja fazer agora:" -ForegroundColor Cyan
    Write-Host "[1] Reiniciar agora (aplicará o papel de parede)"
    Write-Host "[2] Fechar o script (não reiniciar agora)"
    Write-Host "[3] Desligar agora"
    $choice = Read-Host "`nDigite 1, 2 ou 3 (ou Q para sair sem ação)"
    if ($choice -match '^(q|Q)$') { Write-Host "Saindo sem reiniciar..."; Start-Sleep -Seconds 1; exit 0 }

    switch ($choice) {
        '1' {
            $ok2 = Read-Host "Você tem certeza que quer REINICIAR AGORA e aplicar o wallpaper? (S/N)"
            if ($ok2 -match '^(s|S|y|Y)$') {
                Write-Host "Reiniciando em 5 segundos. Salve seu trabalho agora..." -ForegroundColor Yellow
                Start-Sleep -Seconds 2
                # reinicia (usa shutdown.exe para ser compatível)
                & shutdown.exe /r /t 5 /c "Reiniciando para aplicar wallpaper." 
                exit 0
            } else { Write-Host "Reinício cancelado. Voltando ao menu..." -ForegroundColor Cyan; continue }
        }
        '2' {
            Write-Host "Fechando script sem reiniciar..." -ForegroundColor Green
            Start-Sleep -Seconds 1
            exit 0
        }
        '3' {
            $ok3 = Read-Host "Você tem certeza que quer DESLIGAR AGORA? (S/N)"
            if ($ok3 -match '^(s|S|y|Y)$') {
                Write-Host "Desligando em 5 segundos. Salve seu trabalho agora..." -ForegroundColor Yellow
                Start-Sleep -Seconds 2
                & shutdown.exe /s /t 5 /c "Desligando após operação."
                exit 0
            } else { Write-Host "Desligamento cancelado. Voltando ao menu..." -ForegroundColor Cyan; continue }
        }
        default { Write-Host "Opção inválida. Digite 1, 2, 3 ou Q." -ForegroundColor Yellow }
    }
}

# fim do script
