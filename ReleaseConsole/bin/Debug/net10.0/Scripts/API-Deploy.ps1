<#
.SYNOPSIS
Deploys the CognitivePlatform API to the local deployment directory.

.DESCRIPTION
Deploys the API binaries to C:\CP\Deploy\Api\{Environment}, creating a backup
of the current deployment and preserving the Data folder for database files.

.PARAMETER Environment
Target environment. Valid values: Dev, Qa, Prod.

.PARAMETER SourcePath
Path to the extracted artifact directory containing the API binaries.

.PARAMETER Version
Version string of the artifact being deployed.

.PARAMETER Component
Component name (should be "API").

.PARAMETER CurrentVersion
Optional. Version string of the currently deployed API (for logging and backup naming).

.EXAMPLE
.\API-Deploy.ps1 -Environment Dev -SourcePath "C:\temp\api-deploy" -Version 1.0.2224.901 -Component API

.EXAMPLE
.\API-Deploy.ps1 -Environment Qa -SourcePath "C:\temp\api-deploy" -Version 1.0.2224.905 -Component API -CurrentVersion 1.0.2224.901
#>

# [CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet("Dev", "Qa", "Prod")]
    [string]$Environment,
    
    [Parameter(Mandatory)]
    [string]$SourcePath,
    
    [Parameter(Mandatory)]
    [string]$Version,
    
    [Parameter(Mandatory)]
    [string]$Component,
    
    [Parameter()]
    [string]$CurrentVersion
)

[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ============================================================================
# Configuration
# ============================================================================

$deployRoot = "C:\CP\Deploy\Api"
$deployPath = Join-Path $deployRoot $Environment
$backupRoot = Join-Path $deployPath "backups"
$dataFolder = Join-Path $deployPath "Data"

Write-Output "========================================"
Write-Output "Deploying CognitivePlatform API"
Write-Output "========================================"
Write-Output "Environment:       $Environment"
Write-Output "Version:           $Version"
Write-Output "Source:            $SourcePath"
Write-Output "Destination:       $deployPath"

if ($CurrentVersion) {
    Write-Output "Current Version:   $CurrentVersion"
}

Write-Output ""

# ============================================================================
# Validation
# ============================================================================

Write-Output "Validating source files..."

if (-not (Test-Path $SourcePath)) {
    Write-Output "========================================" -ForegroundColor Red
    Write-Output "ERROR: Source path not found" -ForegroundColor Red
    Write-Output "========================================" -ForegroundColor Red
    Write-Output "Path: $SourcePath" -ForegroundColor Yellow
    exit 1
}

$sourceFiles = Get-ChildItem -Path $SourcePath -File
if ($sourceFiles.Count -eq 0) {
    Write-Output "========================================" -ForegroundColor Red
    Write-Output "ERROR: Source directory is empty" -ForegroundColor Red
    Write-Output "========================================" -ForegroundColor Red
    Write-Output "Path: $SourcePath" -ForegroundColor Yellow
    exit 1
}

Write-Output "  Source files: $($sourceFiles.Count)"

# Check for expected executable
$expectedExe = "CognitivePlatform.Api.$Environment.exe"
$exePath = Join-Path $SourcePath $expectedExe

if (-not (Test-Path $exePath)) {
    Write-Output "========================================" -ForegroundColor Yellow
    Write-Output "WARNING: Expected executable not found" -ForegroundColor Yellow
    Write-Output "========================================" -ForegroundColor Yellow
    Write-Output "Expected: $expectedExe" -ForegroundColor Yellow
    Write-Output "Continuing anyway..." -ForegroundColor Gray
    Write-Output ""
}

# ============================================================================
# Backup Current Deployment
# ============================================================================

if (Test-Path $deployPath) {
    Write-Output "Creating backup of current deployment..."
    
    # Create backup directory structure
    New-Item -ItemType Directory -Force -Path $backupRoot | Out-Null
    
    # Generate backup folder name with timestamp
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $backupName = if ($CurrentVersion) {
        "v$CurrentVersion-$timestamp"
    } else {
        "backup-$timestamp"
    }
    
    $backupPath = Join-Path $backupRoot $backupName
    
    # Copy current deployment to backup (excluding Data folder and existing backups)
    Write-Output "  Backup location: $backupPath"
    
    New-Item -ItemType Directory -Force -Path $backupPath | Out-Null
    
    Get-ChildItem -Path $deployPath -Exclude "Data", "backups" | ForEach-Object {
        Copy-Item -Path $_.FullName -Destination $backupPath -Recurse -Force
    }
    
    $backupFileCount = (Get-ChildItem -Path $backupPath -Recurse -File).Count
    Write-Output "  ✅ Backed up $backupFileCount files to: $backupName"
    Write-Output ""
    
    # Optional: Clean up old backups (keep last 10)
    $allBackups = Get-ChildItem -Path $backupRoot -Directory | Sort-Object Name -Descending
    if ($allBackups.Count -gt 10) {
        Write-Output "Cleaning up old backups (keeping last 10)..."
        $backupsToDelete = $allBackups | Select-Object -Skip 10
        foreach ($oldBackup in $backupsToDelete) {
            Write-Output "  Removing: $($oldBackup.Name)"
            Remove-Item -Path $oldBackup.FullName -Recurse -Force
        }
        Write-Output ""
    }
} else {
    Write-Output "No existing deployment found (first-time deployment)"
    Write-Output ""
}

# ============================================================================
# Preserve Data Folder
# ============================================================================

$dataBackupPath = $null
$hasDataFolder = Test-Path $dataFolder

if ($hasDataFolder) {
    Write-Output "Preserving Data folder..."
    
    # Temporarily move Data folder out of the way
    $dataBackupPath = Join-Path $env:TEMP "CP-Deploy-Data-$Environment-$(Get-Date -Format 'yyyyMMddHHmmss')"
    Move-Item -Path $dataFolder -Destination $dataBackupPath -Force
    
    Write-Output "  ✅ Data folder preserved temporarily"
    Write-Output ""
}

# ============================================================================
# Deploy New Version
# ============================================================================

Write-Output "Deploying new version..."

# Create deployment directory if it doesn't exist
New-Item -ItemType Directory -Force -Path $deployPath | Out-Null

# Remove old files (but keep backups folder)
if (Test-Path $deployPath) {
    Get-ChildItem -Path $deployPath -Exclude "backups", "Data" | Remove-Item -Recurse -Force
}

# Copy new files
Write-Output "  Copying files from source..."
Copy-Item -Path "$SourcePath\*" -Destination $deployPath -Recurse -Force

$deployedFileCount = (Get-ChildItem -Path $deployPath -File).Count
Write-Output "  ✅ Deployed $deployedFileCount files"
Write-Output ""

# ============================================================================
# Restore Data Folder
# ============================================================================

if ($dataBackupPath -and (Test-Path $dataBackupPath)) {
    Write-Output "Restoring Data folder..."
    
    Move-Item -Path $dataBackupPath -Destination $dataFolder -Force
    
    Write-Output "  ✅ Data folder restored"
    Write-Output ""
}

# ============================================================================
# Verification
# ============================================================================

Write-Output "Verifying deployment..."

$deployedExe = Join-Path $deployPath $expectedExe
if (Test-Path $deployedExe) {
    Write-Output "  ✅ Executable found: $expectedExe"
} else {
    Write-Output "  ⚠️  Expected executable not found: $expectedExe" -ForegroundColor Yellow
}

# Check for config files
$configFiles = Get-ChildItem -Path $deployPath -Filter "appsettings*.json"
Write-Output "  Configuration files: $($configFiles.Count)"
foreach ($config in $configFiles) {
    Write-Output "    - $($config.Name)"
}

# ============================================================================
# Success
# ============================================================================

Write-Output ""
Write-Output "========================================"
Write-Output "✅ Deployment Successful"
Write-Output "========================================"
Write-Output "Deployed: CognitivePlatform API v$Version ($Environment)"
Write-Output "Location: $deployPath"
Write-Output ""

if ($hasDataFolder) {
    Write-Output "⚠️  IMPORTANT: Data folder was preserved" -ForegroundColor Yellow
    Write-Output "   Database files were NOT modified during deployment" -ForegroundColor Yellow
    Write-Output "   If you need to run migrations, do so manually." -ForegroundColor Yellow
    Write-Output ""
}

Write-Output "Next steps:" -ForegroundColor Cyan
Write-Output "  1. Verify configuration in appsettings.$Environment.json"
Write-Output "  2. Start the API manually from: $deployPath"
Write-Output "  3. Check that the API is responding correctly"
Write-Output ""

exit 0
