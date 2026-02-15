<#
.SYNOPSIS
Builds the CognitivePlatform API for a specific environment.

.DESCRIPTION
Publishes the CognitivePlatform API using dotnet publish with environment-specific
assembly metadata. Outputs to the artifact staging directory for packaging.

.PARAMETER Environment
Target environment. Valid values: DEV, QA.
Note: PROD deployments use the promote workflow, not direct builds.

.PARAMETER Version
Version string for the build (e.g., 1.0.2223.947).

.PARAMETER OutputPath
Path where the published output should be placed.
Typically: C:\CP\Artifacts\API\{version}\{environment}

.EXAMPLE
.\API-Build.ps1 -Environment DEV -Version 1.0.2223.947 -OutputPath "C:\CP\Artifacts\API\1.0.2223.947\Dev"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet("DEV", "QA")]
    [string]$Environment,
    
    [Parameter(Mandatory)]
    [string]$Version,
    
    [Parameter(Mandatory)]
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ============================================================================
# Configuration
# ============================================================================

# Find the API project file
# The script is executed from bin\Debug\net10.0\scripts, so we need to traverse up
# to find the actual source repos directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Traverse up until we find the source\repos directory
$currentDir = $scriptDir
while ($currentDir -and -not (Test-Path (Join-Path $currentDir "..\..\..\..\..\CognitivePlatform"))) {
    $parent = Split-Path -Parent $currentDir
    if ($parent -eq $currentDir) {
        # Reached root without finding it
        break
    }
    $currentDir = $parent
}

# Common repo locations to try
$possiblePaths = @(
    "C:\Users\benho\source\repos\CognitivePlatform\CognitivePlatform\CognitivePlatform.Api.csproj"
    "$env:USERPROFILE\source\repos\CognitivePlatform\CognitivePlatform\CognitivePlatform.Api.csproj"
)

$apiProject = $null
foreach ($path in $possiblePaths) {
    if (Test-Path $path) {
        $apiProject = $path
        break
    }
}

# If still not found, prompt for manual entry
if (-not $apiProject) {
    Write-Host "Could not auto-detect API project location." -ForegroundColor Yellow
    Write-Host "Please enter the full path to CognitivePlatform.Api.csproj:"
    $apiProject = Read-Host "Project Path"
}

Write-Host "========================================"
Write-Host "Building CognitivePlatform API"
Write-Host "========================================"
Write-Host "Environment:   $Environment"
Write-Host "Version:       $Version"
Write-Host "Output Path:   $OutputPath"
Write-Host "Project Path:  $apiProject"
Write-Host ""

# ============================================================================
# Validation
# ============================================================================

if (-not (Test-Path $apiProject)) {
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "ERROR: API project not found" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "Expected path: $apiProject" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Expected project structure:"
    Write-Host "  C:\Users\benho\source\repos\"
    Write-Host "    ├── CognitivePlatform\CognitivePlatform\CognitivePlatform.Api.csproj"
    Write-Host "    └── ReleaseConsole\..."
    Write-Host ""
    exit 1
}

# ============================================================================
# Build
# ============================================================================

Write-Host "Cleaning previous build artifacts..."
if (Test-Path $OutputPath) {
    Remove-Item -Path $OutputPath -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null

Write-Host "Publishing API..."

# Environment-specific assembly metadata
$envTitleCase = (Get-Culture).TextInfo.ToTitleCase($Environment.ToLower())
$assemblyName = "CognitivePlatform.Api.$envTitleCase"
$assemblyTitle = "CognitivePlatform.Api ($Environment)"
$product = "CognitivePlatform.Api ($Environment)"

dotnet publish $apiProject `
    -c Release `
    -r win-x64 `
    --self-contained false `
    /p:UseAppHost=true `
    /p:AssemblyName=$assemblyName `
    /p:AssemblyTitle=$assemblyTitle `
    /p:Product=$product `
    /p:FileDescription=$assemblyName `
    /p:Version=$Version `
    /p:AssemblyVersion=$Version `
    /p:FileVersion=$Version `
    -o $OutputPath

if ($LASTEXITCODE -ne 0) {
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "ERROR: dotnet publish failed" -ForegroundColor Red  
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "Exit code: $LASTEXITCODE" -ForegroundColor Yellow
    exit $LASTEXITCODE
}

# ============================================================================
# Verification
# ============================================================================

Write-Host ""
Write-Host "Verifying output..."

$expectedExe = Join-Path $OutputPath "$assemblyName.exe"
if (-not (Test-Path $expectedExe)) {
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "ERROR: Expected executable not found" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "Expected: $expectedExe" -ForegroundColor Yellow
    exit 1
}

$fileCount = (Get-ChildItem -Path $OutputPath -File).Count
Write-Host "  Files generated: $fileCount"
Write-Host "  Executable: $expectedExe"

# ============================================================================
# Success
# ============================================================================

Write-Host ""
Write-Host "========================================"
Write-Host "Build Successful"
Write-Host "========================================"
Write-Host "Built: CognitivePlatform API v$Version ($Environment)"
Write-Host "Output: $OutputPath"
Write-Host ""

exit 0