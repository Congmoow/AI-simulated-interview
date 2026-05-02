[CmdletBinding()]
param([switch]$Full)

try { chcp 65001 | Out-Null } catch {}
try {
    [Console]::OutputEncoding = [System.Text.UTF8Encoding]::UTF8
    $OutputEncoding = [System.Text.UTF8Encoding]::UTF8
} catch {}

$ProgressPreference = 'SilentlyContinue'
$root    = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$mode    = if ($Full) { 'full' } else { 'dev' }
$logsDir = Join-Path $root '.dev-logs'
$pidFile = Join-Path $root '.dev-pids.json'
Set-Location $root

# ── Helpers ───────────────────────────────────────────────────────────────────
function Test-TcpPort([int]$Port) {
    try {
        $tcp = [System.Net.Sockets.TcpClient]::new()
        $ar  = $tcp.BeginConnect('127.0.0.1', $Port, $null, $null)
        $ok  = $ar.AsyncWaitHandle.WaitOne(1000)
        $ret = $ok -and $tcp.Connected
        try { $tcp.Close() } catch {}
        return $ret
    } catch { return $false }
}

function Test-HttpOk([string]$Url) {
    try {
        $r = Invoke-WebRequest -Uri $Url -TimeoutSec 5 -UseBasicParsing -ErrorAction Stop
        return ($r.StatusCode -ge 200 -and $r.StatusCode -lt 400)
    } catch { return $false }
}

function Get-FrontendPort([string]$LogPath) {
    if (-not (Test-Path $LogPath)) { return $null }
    $text = Get-Content $LogPath -Raw -ErrorAction SilentlyContinue
    if ($text -match 'Local:.*?localhost:(\d+)') { return [int]$Matches[1] }
    return $null
}

function Show-LogTail([string]$Label, [string]$LogPath, [int]$Lines = 30) {
    Write-Host ''
    Write-Host "──── $Label stdout (末尾 $Lines 行) ────" -ForegroundColor Red
    if (Test-Path $LogPath) {
        Get-Content $LogPath -Tail $Lines -ErrorAction SilentlyContinue | ForEach-Object { Write-Host $_ }
        $errPath = [IO.Path]::ChangeExtension($LogPath, '.err')
        if ((Test-Path $errPath) -and (Get-Item $errPath -ErrorAction SilentlyContinue).Length -gt 0) {
            Write-Host "──── $Label stderr ────" -ForegroundColor Red
            Get-Content $errPath -Tail $Lines -ErrorAction SilentlyContinue | ForEach-Object { Write-Host $_ }
        }
    } else {
        Write-Host '(日志文件不存在)'
    }
}

function Write-PidFile {
    param($Entries, [string]$Path)
    try {
        # JSON Lines format (one compact JSON object per line) avoids PS5.1
        # ConvertFrom-Json array-wrapping quirk that merges all objects into one.
        $lines = @()
        foreach ($e in $Entries) {
            $obj = [PSCustomObject]@{
                name      = [string]$e.name
                pid       = [int]$e.pid
                port      = $e.port
                logPath   = [string]$e.logPath
                startTime = [string]$e.startTime
            }
            $lines += ($obj | ConvertTo-Json -Compress -Depth 3)
        }
        [System.IO.File]::WriteAllText($Path, ($lines -join "`n") + "`n")
    } catch {
        Write-Host "  ⚠️  PID 文件写入失败: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

# ── Banner ────────────────────────────────────────────────────────────────────
Write-Host ''
Write-Host '============================================' -ForegroundColor Cyan
Write-Host "  AI 模拟面试  本地开发启动  [$($mode.ToUpper())]" -ForegroundColor Cyan
Write-Host '============================================' -ForegroundColor Cyan

# ── Prerequisites ─────────────────────────────────────────────────────────────
foreach ($cmd in @('node', 'dotnet', 'docker')) {
    if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) {
        Write-Host "❌ 未找到 $cmd，请安装后重试" -ForegroundColor Red; exit 1
    }
}
if ($Full -and -not (Get-Command 'uv' -ErrorAction SilentlyContinue)) {
    Write-Host '❌ Full 模式需要 uv: https://docs.astral.sh/uv/' -ForegroundColor Red; exit 1
}
if (-not (Test-Path (Join-Path $root '.env.run'))) {
    Write-Host '❌ 缺少 .env.run，请先执行: Copy-Item .env.example .env.run' -ForegroundColor Red; exit 1
}
if (-not (Test-Path (Join-Path $root 'node_modules'))) {
    Write-Host '根目录依赖未安装，执行 npm install...' -ForegroundColor Yellow
    & npm install --no-audit --no-fund
    if ($LASTEXITCODE -ne 0) { Write-Host '❌ npm install 失败' -ForegroundColor Red; exit 1 }
}

# ── Predev checks (Docker infra + frontend deps) ───────────────────────────────
Write-Host ''
Write-Host '[预检] 检查 Docker 基础设施...' -ForegroundColor Cyan
& node (Join-Path $root 'scripts/predev.mjs') $mode
if ($LASTEXITCODE -ne 0) {
    Write-Host '❌ 预检失败，请确认 Docker Desktop 已启动，.env.run 配置正确' -ForegroundColor Red; exit 1
}

# ── Auto-stop previous run if PID file exists ───────────────────────────────
if (Test-Path $pidFile) {
    Write-Host ''
    Write-Host '[清理] 检测到上次运行记录，停止旧服务进程...' -ForegroundColor Yellow
    try {
        $oldData = @(Get-Content $pidFile -Encoding UTF8 | Where-Object { $_ -match '\{' } | ForEach-Object { ConvertFrom-Json $_ })
        foreach ($s in $oldData) {
            try {
                $op = [int]$s.pid
                if (Get-Process -Id $op -ErrorAction SilentlyContinue) {
                    & taskkill /F /T /PID $op 2>&1 | Out-Null
                    Write-Host "  已停止 $($s.name) PID=$op"
                }
            } catch {}
        }
    } catch {}
    Remove-Item $pidFile -Force -ErrorAction SilentlyContinue
}
# Sweep residual processes on dev ports (protects against zombie procs from corrupted PID history)
$skipProcs  = @('docker', 'com.docker.backend', 'dockerd', 'wsl', 'wslrelay', 'vmmem', 'services', 'svchost', 'lsass', 'csrss', 'winlogon', 'system')
$sweptPids  = [System.Collections.Generic.HashSet[int]]::new()
$sweepLines = & netstat -ano 2>$null
foreach ($sweepPort in @(3000, 3001, 8080, 8000)) {
    foreach ($netLine in $sweepLines) {
        if ($netLine -match ":$sweepPort\s+\S+\s+LISTENING\s+(\d+)") {
            $sweepPid = [int]$Matches[1]
            if ($sweptPids.Contains($sweepPid)) { continue }
            $sweepProc = Get-Process -Id $sweepPid -ErrorAction SilentlyContinue
            $sweepName = if ($sweepProc) { $sweepProc.Name } else { $null }
            if ($sweepName -and ($skipProcs -notcontains $sweepName.ToLower())) {
                & taskkill /F /T /PID $sweepPid 2>&1 | Out-Null
                $sweptPids.Add($sweepPid) | Out-Null
                Write-Host "  清理端口 ${sweepPort} 残留进程 $sweepName PID=$sweepPid"
            }
        }
    }
}
Start-Sleep 1

# ── Prepare log dir ───────────────────────────────────────────────────────────
New-Item -ItemType Directory -Force -Path $logsDir | Out-Null

# ── Start services ─────────────────────────────────────────────────────────────
Write-Host ''
Write-Host '[启动] 后台启动各服务...' -ForegroundColor Cyan

# Frontend
$fLog  = Join-Path $logsDir 'frontend.log'
$fErr  = Join-Path $logsDir 'frontend.err'
Remove-Item $fLog, $fErr -Force -ErrorAction SilentlyContinue
$fProc = Start-Process pwsh `
    -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', 'npm --prefix frontend run dev') `
    -WorkingDirectory $root -WindowStyle Hidden `
    -RedirectStandardOutput $fLog -RedirectStandardError $fErr -PassThru
Write-Host "  [frontend]   PID $($fProc.Id)  → $fLog"

# Backend
$bLog  = Join-Path $logsDir 'backend.log'
$bErr  = Join-Path $logsDir 'backend.err'
Remove-Item $bLog, $bErr -Force -ErrorAction SilentlyContinue
$bProc = Start-Process dotnet `
    -ArgumentList @('run', '--project', 'backend/src/AiInterview.Api/AiInterview.Api.csproj') `
    -WorkingDirectory $root -WindowStyle Hidden `
    -RedirectStandardOutput $bLog -RedirectStandardError $bErr -PassThru
Write-Host "  [backend]    PID $($bProc.Id)  → $bLog"

# AI Service
$aProc = $null
$aLog  = $null
if ($Full) {
    $aiDir = Join-Path $root 'ai-service'
    $aLog  = Join-Path $logsDir 'ai-service.log'
    $aErr  = Join-Path $logsDir 'ai-service.err'
    Remove-Item $aLog, $aErr -Force -ErrorAction SilentlyContinue
    $aProc = Start-Process uv `
        -ArgumentList @('run', 'uvicorn', 'app.main:app', '--host', '0.0.0.0', '--port', '8000', '--reload', '--reload-dir', 'app') `
        -WorkingDirectory $aiDir -WindowStyle Hidden `
        -RedirectStandardOutput $aLog -RedirectStandardError $aErr -PassThru
    Write-Host "  [ai-service] PID $($aProc.Id)  → $aLog"
}

# ── Initial PID file ──────────────────────────────────────────────────────────
$now      = Get-Date -Format 'yyyy-MM-ddTHH:mm:ss'
$pidList  = [System.Collections.Generic.List[hashtable]]::new()
$pidList.Add(@{ name = 'frontend';   pid = $fProc.Id; port = $null; logPath = $fLog; startTime = $now })
$pidList.Add(@{ name = 'backend';    pid = $bProc.Id; port = 8080;  logPath = $bLog; startTime = $now })
if ($Full -and $aProc) {
    $pidList.Add(@{ name = 'ai-service'; pid = $aProc.Id; port = 8000; logPath = $aLog; startTime = $now })
}
Write-PidFile -Entries $pidList -Path $pidFile

# Service list for loop checks
$svcMeta = @(
    @{ proc = $fProc; name = 'frontend';   log = $fLog }
    @{ proc = $bProc; name = 'backend';    log = $bLog }
)
if ($Full -and $aProc) { $svcMeta += @{ proc = $aProc; name = 'ai-service'; log = $aLog } }

# ── Health check loop ─────────────────────────────────────────────────────────
Write-Host ''
Write-Host '[健检] 等待各服务就绪（最多 90 秒）...' -ForegroundColor Cyan

$deadline      = [DateTime]::Now.AddSeconds(90)
$frontendReady = $false
$backendReady  = $false
$aiReady       = (-not $Full)  # trivially true when not Full
$frontendPort  = $null

while ([DateTime]::Now -lt $deadline) {
    # Early exit: check if any service process has already died
    foreach ($s in $svcMeta) {
        if ($s.proc.HasExited) {
            Write-Host ''
            $ec2 = if ($null -ne $s.proc.ExitCode) { $s.proc.ExitCode } else { 'N/A' }
            Write-Host "❌ [$($s.name)] 进程已退出 (ExitCode=$ec2)" -ForegroundColor Red
            Show-LogTail $s.name $s.log
            exit 1
        }
    }

    # Frontend: trust log-parsed port (prevents false-positives from other processes on 3000)
    if (-not $frontendReady) {
        $parsed = Get-FrontendPort $fLog
        if ($parsed -and (Test-TcpPort $parsed)) {
            $frontendPort = $parsed; $frontendReady = $true
        } elseif (-not $parsed) {
            # Log not yet written; try TCP + HTTP to avoid false-positive on Docker port 3000
            foreach ($p in @(3000, 3001, 3002)) {
                if ((Test-TcpPort $p) -and (Test-HttpOk "http://localhost:$p")) {
                    $frontendPort = $p; $frontendReady = $true; break
                }
            }
        }
    }

    # Backend
    if (-not $backendReady) { $backendReady = Test-HttpOk 'http://localhost:8080/health' }

    # AI Service
    if ($Full -and -not $aiReady) { $aiReady = Test-HttpOk 'http://localhost:8000/health' }

    if ($frontendReady -and $backendReady -and $aiReady) { break }
    Write-Host -NoNewline '.'; Start-Sleep 3
}
Write-Host ''

# ── Update PID file with resolved frontend port ───────────────────────────────
for ($i = 0; $i -lt $pidList.Count; $i++) {
    if ($pidList[$i].name -eq 'frontend') { $pidList[$i].port = $frontendPort }
}
Write-PidFile -Entries $pidList -Path $pidFile

# ── Final status ──────────────────────────────────────────────────────────────
Write-Host ''
Write-Host '============================================' -ForegroundColor Cyan
Write-Host '  服务状态' -ForegroundColor Cyan
Write-Host '============================================' -ForegroundColor Cyan

$allOk = $true

if ($frontendReady) {
    Write-Host "  frontend  : ✅  http://localhost:$frontendPort" -ForegroundColor Green
} else {
    Write-Host '  frontend  : ⚠️  健检超时' -ForegroundColor Yellow
    Show-LogTail 'frontend' $fLog; $allOk = $false
}
if ($backendReady) {
    Write-Host '  backend   : ✅  http://localhost:8080' -ForegroundColor Green
} else {
    Write-Host '  backend   : ⚠️  健检超时' -ForegroundColor Yellow
    Show-LogTail 'backend' $bLog; $allOk = $false
}
if ($Full) {
    if ($aiReady) {
        Write-Host '  ai-service: ✅  http://localhost:8000' -ForegroundColor Green
    } else {
        Write-Host '  ai-service: ⚠️  健检超时' -ForegroundColor Yellow
        Show-LogTail 'ai-service' $aLog; $allOk = $false
    }
}

Write-Host ''
if ($allOk) {
    Write-Host '✅ 全部服务已就绪，终端已解锁' -ForegroundColor Green
    Write-Host "   前端:  http://localhost:$frontendPort" -ForegroundColor White
    Write-Host '   后端:  http://localhost:8080  (/health /swagger)' -ForegroundColor White
    if ($Full) { Write-Host '   AI:    http://localhost:8000  (/health)' -ForegroundColor White }
    Write-Host ''
    Write-Host "停止服务:  .\stop.ps1" -ForegroundColor Yellow
    Write-Host "日志目录:  $logsDir" -ForegroundColor DarkGray
} else {
    Write-Host '⚠️  部分服务未就绪，请检查上方日志输出' -ForegroundColor Yellow
    Write-Host "日志目录:  $logsDir" -ForegroundColor DarkGray
    exit 1
}
