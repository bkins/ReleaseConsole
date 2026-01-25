param(
    [Parameter(Mandatory=$true)]
    [string]$Environment,
    
    [Parameter(Mandatory=$true)]
    [string]$Version
)

Write-Host "Building API for $Environment (v$Version)..." -ForegroundColor Cyan

# Create fake output directory
$outputPath = "..\publish\API\$Environment"
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

# Create a dummy file
"Fake API Build v$Version" | Out-File "$outputPath\api.dll"

Write-Host "Build completed successfully" -ForegroundColor Green
Write-Host "Output: $outputPath" -ForegroundColor Gray

exit 0