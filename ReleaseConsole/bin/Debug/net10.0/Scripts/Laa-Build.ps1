<#
.SYNOPSIS
Builds the Local AI Assistant MAUI application for a given environment.

.DESCRIPTION
This script builds the MAUI Android application for the specified environment
and version. It produces a signed APK artifact in the environment-specific
output directory.

This script is intended to be invoked by ReleaseConsole.
It does not generate versions, enforce promotion rules, or manage artifacts.

.PARAMETER Environment
Target environment. Valid values: DEV, QA, PROD.

.PARAMETER Version
Version string provided by ReleaseConsole (e.g. 1.0.20260125.153045).

.EXAMPLE
.\Laa-Build.ps1 -Environment DEV -Version 1.0.20260125.153045
#>

[CmdletBinding()]
param (
    [Parameter(Mandatory)]
    [ValidateSet("DEV", "QA", "PROD")]
    [string]$Environment,

    [Parameter(Mandatory)]
    [string]$Version
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Log {
    param ([string] $Message)
    Write-Host "[BUILD][$Environment] $Message"
}

try {

    $TargetFramework="net9.0-android"

    Write-Host "🔧 Starting build for environment [$Environment], version [$Version]"

    $mauiProject = "C:\Users\benho\source\repos\LocalAIAssistant\LocalAIAssistant.Ui.Maui.csproj"

    if (-not (Test-Path $mauiProject)) {
        Write-Error "MAUI project not found: $mauiProject"
        exit 1
    }

    switch ($Environment) {
        "DEV" {
            $apiBaseUrl = "http://192.168.0.33:5273"
            $outDir     = "C:\Deploy\CP\MAUI\Dev\"
            $emoji      = "🟢"
        }
        "QA" {
            $apiBaseUrl = "http://192.168.0.33:5274"
            $outDir     = "C:\Deploy\CP\MAUI\QA\"
            $emoji      = "🟡"
        }
        "PROD" {
            $apiBaseUrl = "http://192.168.0.33:5275"
            $outDir     = "C:\Deploy\CP\MAUI\Prod\"
            $emoji      = "🔴"
        }
    }

    Write-Host "$emoji Building LocalAIAssistant ($Environment)"

    Log "Cleaning project"
    dotnet clean $mauiProject | Out-Host
    
    Log "Updating Android manifest label"
    
    $manifestPath = "C:\Users\benho\source\repos\LocalAIAssistant\Platforms\Android\AndroidManifest.xml"

    [xml]$manifest = Get-Content $manifestPath
    $manifest.manifest.application.SetAttribute(
        "android:label",
        "Laa ($Environment)"
    )
    $manifest.Save($manifestPath)
    

    Log "Publishing MAUI app to $outDir"

    
    $intermediateOutDir = "C:\Users\benho\source\repos\LocalAIAssistant\bin\Release\net9.0-android\publish"
    $keystorePath = "$env:USERPROFILE\.android\debug.keystore"

    if (-not (Test-Path $keystorePath)) {
        Write-Error "Debug keystore not found at $keystorePath. Run 'keytool -genkey -v -keystore debug.keystore ...' to create it."
        exit 1
    }
    
    Log "Using keystore: $keystorePath"
   
    dotnet publish $mauiProject `
        -c Release `
        -f net9.0-android `
        /p:ApplicationLabel="Laa ($Environment)" `
        /p:ApplicationDisplayVersion=$Version-$Environment `
        /p:ApiEnvironmentName=$Environment `
        /p:ApplicationId=com.snikpoh.localaiassistant.$Environment `
        /p:AppEnvironment=$Environment `
        /p:ApiBaseUrl=$apiBaseUrl `
        /p:AndroidPackageFormat=apk `
        /p:AndroidKeyStore=true `
        /p:AndroidSigningKeyStore="$env:USERPROFILE\.android\debug.keystore" `
        /p:AndroidSigningKeyAlias=androiddebugkey `
        /p:AndroidSigningKeyPass=android `
        /p:AndroidSigningStorePass=android `
        -o $outDir

     if ($LASTEXITCODE -ne 0) { 
         throw "Dotnet publish failed with exit code $LASTEXITCODE. Check for missing Android SDKs or keystore issues." 
     }
     
     $projectDir = Split-Path $mauiProject -Parent
     $signedApk = Get-ChildItem -Path "$projectDir\bin\Release\net9.0-android\*-Signed.apk" | Select-Object -First 1
     
     $keystorePath = "$env:USERPROFILE\.android\debug.keystore"

    if (-not (Test-Path $keystorePath)) {
        Write-Error "Debug keystore not found at $keystorePath. Run 'keytool -genkey -v -keystore debug.keystore ...' to create it."
        exit 1
    }
    
    # Copy the signed APK with the expected name
    $targetApk = Join-Path $outDir "com.snikpoh.localaiassistant.$Environment.apk"
    Copy-Item -Path $signedApk.FullName -Destination $targetApk -Force


    Log "✅ $Environment build complete."
    Log "    Using keystore: $keystorePath"
    Log "    APK: $targetApk"
    Log "    Version: $Version"
    exit 0
}
catch {
    Write-Error "Build failed: $($_.Exception.Message)"
    exit 2
}
finally {
    Pop-Location
}
