param(
    [Parameter(Mandatory=$true)]
    [string]$Environment,
    
    [Parameter(Mandatory=$true)]
    [string]$SourcePath,
    
    [Parameter(Mandatory=$true)]
    [string]$Version
)

Write-Host "Deploying API to $Environment (v$Version)..." -ForegroundColor Cyan
Write-Host "Source: $SourcePath" -ForegroundColor Gray

# Simulate deployment delay
Start-Sleep -Seconds 2

Write-Host "Deployment completed successfully" -ForegroundColor Green

exit 0