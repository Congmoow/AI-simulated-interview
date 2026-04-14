[CmdletBinding()]
param(
    [switch]$CleanByPort,
    [switch]$KeepLogs
)

try { chcp 65001 | Out-Null } catch {}
try {
    [Console]::OutputEncoding = [System.Text.UTF8Encoding]::UTF8
    $OutputEncoding = [System.Text.UTF8Encoding]::UTF8
} catch {}

$root    = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$pidFile = Join-Path $root '.dev-pids.json'
$logsDir = Join-Path $root '.dev-logs'

Write-Host ''
Write-Host '============================================' -ForegroundColor Cyan
Write-Host '  AI 模拟面试  停止本地服务' -ForegroundColor Cyan
Write-Host '============================================' -ForegroundColor Cyan

$stoppedAny = $false

# ── Step 1: PID-based stop ────────────────────────────────────────────────────
if (Test-Path $pidFile) {
    Write-Host ''
    Write-Host '[1] 按 PID 树停止服务...' -ForegroundColor Cyan
    $pidData = @(Get-Content $pidFile -Encoding UTF8 | Where-Object { $_ -match '\{' } | ForEach-Object { ConvertFrom-Json $_ })
    foreach ($svc in $pidData) {
        $name   = $svc.name
        $pidVal = [int]$svc.pid
        $port   = $svc.port
        Write-Host -NoNewline "  [$name] PID=$pidVal port=$port ... "
        $proc = Get-Process -Id $pidVal -ErrorAction SilentlyContinue
        if ($proc) {
            $result = & taskkill /F /T /PID $pidVal 2>&1
            if ($LASTEXITCODE -eq 0) {
                Write-Host '已停止 ✅' -ForegroundColor Green
            } else {
                $ec = $LASTEXITCODE
                Write-Host "taskkill 返回 ${ec}: $result" -ForegroundColor Yellow
            }
        } else {
            Write-Host '(进程已不存在)' -ForegroundColor DarkGray
        }
        $stoppedAny = $true
    }
    Remove-Item $pidFile -Force -ErrorAction SilentlyContinue
    Write-Host "  已删除: $pidFile"
} else {
    Write-Host ''
    Write-Host "[!] 未找到 $pidFile" -ForegroundColor Yellow
    if (-not $CleanByPort) {
        Write-Host '    如需按端口强制清理，请加 -CleanByPort 参数' -ForegroundColor Yellow
    }
}

# ── Step 2: Port-based stop (only with -CleanByPort) ─────────────────────────
if ($CleanByPort) {
    Write-Host ''
    Write-Host '[2] 按端口清理进程 (-CleanByPort)...' -ForegroundColor Cyan
    $skipProcs = @('docker', 'com.docker.backend', 'dockerd', 'wsl', 'wslrelay', 'vmmem', 'services', 'svchost', 'lsass', 'csrss', 'winlogon', 'system')
    $killedPids = [System.Collections.Generic.HashSet[int]]::new()
    foreach ($port in @(3000, 3001, 8080, 8000)) {
        $netLines = & netstat -ano 2>$null
        foreach ($line in $netLines) {
            if ($line -match ":$port\s+\S+\s+LISTENING\s+(\d+)") {
                $pid2  = [int]$Matches[1]
                if ($killedPids.Contains($pid2)) { continue }
                $proc2 = Get-Process -Id $pid2 -ErrorAction SilentlyContinue
                $pname = if ($proc2) { $proc2.Name } else { '未知' }
                if ($skipProcs -contains $pname.ToLower()) {
                    Write-Host "  端口 $port → PID=$pid2 ($pname) — 跳过(系统进程)" -ForegroundColor DarkGray
                    continue
                }
                Write-Host -NoNewline "  端口 $port → PID=$pid2 ($pname) ... "
                & taskkill /F /T /PID $pid2 2>&1 | Out-Null
                if ($LASTEXITCODE -eq 0) {
                    Write-Host '已停止 ✅' -ForegroundColor Green
                    $killedPids.Add($pid2) | Out-Null
                } else {
                    $ec = $LASTEXITCODE
                    Write-Host "taskkill 返回 ${ec}" -ForegroundColor Yellow
                }
                $stoppedAny = $true
            }
        }
    }
}

# ── Log cleanup ───────────────────────────────────────────────────────────────
if (-not $KeepLogs) {
    if (Test-Path $logsDir) {
        Remove-Item $logsDir -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host ''
        Write-Host "已删除日志目录: $logsDir  (保留日志请加 -KeepLogs)" -ForegroundColor DarkGray
    }
} else {
    Write-Host "日志保留于: $logsDir" -ForegroundColor DarkGray
}

Write-Host ''
if ($stoppedAny) {
    Write-Host '✅ 完成' -ForegroundColor Green
} else {
    Write-Host '⚠️  未找到可停止的服务' -ForegroundColor Yellow
}
