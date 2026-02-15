# status-check.ps1
Write-Host "=== ReleaseConsole Status Check ===" -ForegroundColor Cyan
Write-Host ""

# Git status
Write-Host "Git Status:" -ForegroundColor Yellow
git status --short

# Build check
Write-Host "`nBuild Test:" -ForegroundColor Yellow
cd ReleaseConsole
dotnet build --no-restore | Select-String "Build succeeded|Build FAILED|Error"

# File organization check
Write-Host "`nStructure Check:" -ForegroundColor Yellow
Write-Host "Commands: $((Get-ChildItem Commands -Filter *.cs).Count) files"
Write-Host "Core: $((Get-ChildItem Core -Filter *.cs).Count) files"
Write-Host "Services: $((Get-ChildItem Services -Filter *.cs -Recurse).Count) files"
Write-Host "Scripts: $((Get-ChildItem Scripts -Filter *.ps1).Count) files"