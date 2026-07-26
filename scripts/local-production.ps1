[CmdletBinding()]
param(
    [ValidateSet("Start", "Stop", "Status", "Logs", "Backup")]
    [string]$Action = "Start",
    [switch]$NoBrowser
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$environmentFile = Join-Path $repositoryRoot ".env.local"
$composeFile = Join-Path $repositoryRoot "docker-compose.yml"
$env:COMPOSE_PARALLEL_LIMIT = "2"

function New-HexSecret([int]$byteCount) {
    $bytes = New-Object byte[] $byteCount
    $generator = [Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $generator.GetBytes($bytes)
    } finally {
        $generator.Dispose()
    }
    return ([BitConverter]::ToString($bytes) -replace "-", "").ToLowerInvariant()
}

function Ensure-Docker {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        $dockerCandidates = @(
            (Join-Path $env:LOCALAPPDATA "Programs\DockerDesktop\resources\bin\docker.exe"),
            "C:\Program Files\Docker\Docker\resources\bin\docker.exe"
        )
        $dockerExecutable = $dockerCandidates |
            Where-Object { Test-Path -LiteralPath $_ } |
            Select-Object -First 1
        if ($dockerExecutable) {
            $env:PATH = "$(Split-Path -Parent $dockerExecutable);$env:PATH"
        }
    }
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        throw "Docker Desktop is required. Install it, start Docker Desktop, then run this script again."
    }
    & docker info *> $null
    if ($LASTEXITCODE -ne 0) {
        throw "Docker Desktop is installed but is not running. Start it, wait until it is ready, then retry."
    }
}

function Ensure-EnvironmentFile {
    if (Test-Path -LiteralPath $environmentFile) { return }
    $content = @(
        "POSTGRES_PASSWORD=$(New-HexSecret 32)"
        "JWT_KEY=$(New-HexSecret 64)"
        "INTERNAL_API_KEY=$(New-HexSecret 64)"
        "STRIPE_SECRET_KEY="
        "STRIPE_WEBHOOK_SECRET="
        "STRIPE_PRICE_ID="
    )
    [IO.File]::WriteAllLines($environmentFile, $content)
    Write-Host "Created a private local environment with strong generated secrets." -ForegroundColor Green
}

function Invoke-Compose([string[]]$Arguments) {
    & docker compose --env-file $environmentFile -f $composeFile @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Docker Compose failed while running: $($Arguments -join ' ')"
    }
}

Set-Location $repositoryRoot
Ensure-Docker

if ($Action -eq "Start") {
    Ensure-EnvironmentFile
    foreach ($service in @("identity", "catalog", "subscriptions", "gateway", "web")) {
        Write-Host "Building $service..." -ForegroundColor Cyan
        Invoke-Compose -Arguments @("build", $service)
    }
    Invoke-Compose -Arguments @("up", "--detach", "--remove-orphans", "--no-build")

    $ready = $false
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri "http://127.0.0.1:5173/healthz" -TimeoutSec 2
            if ($response.StatusCode -eq 200) {
                $ready = $true
                break
            }
        } catch {
            Start-Sleep -Seconds 2
        }
    }
    if (-not $ready) {
        Invoke-Compose -Arguments @("ps")
        throw "OpenFlix did not become healthy in time. Run this script with -Action Logs for details."
    }

    Write-Host "OpenFlix is healthy at http://localhost:5173" -ForegroundColor Green
    Write-Host "Your database and generated secrets persist between restarts."
    if (-not $NoBrowser) {
        Start-Process "http://localhost:5173"
    }
    exit 0
}

if (-not (Test-Path -LiteralPath $environmentFile)) {
    throw "No local production environment exists yet. Run this script with -Action Start first."
}

switch ($Action) {
    "Stop" {
        Invoke-Compose -Arguments @("down", "--remove-orphans")
        Write-Host "OpenFlix stopped. Database volumes were preserved." -ForegroundColor Green
    }
    "Status" {
        Invoke-Compose -Arguments @("ps")
    }
    "Logs" {
        Invoke-Compose -Arguments @("logs", "--follow", "--tail", "150")
    }
    "Backup" {
        $backupDirectory = Join-Path $repositoryRoot "backups"
        [IO.Directory]::CreateDirectory($backupDirectory) | Out-Null
        $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $backupFile = Join-Path $backupDirectory "openflix-$timestamp.sql"
        $output = & docker compose --env-file $environmentFile -f $composeFile exec -T postgres `
            pg_dumpall --clean --if-exists --username openflix
        if ($LASTEXITCODE -ne 0) {
            throw "The PostgreSQL backup failed."
        }
        [IO.File]::WriteAllLines($backupFile, $output)
        Write-Host "Backup created: $backupFile" -ForegroundColor Green
    }
}
