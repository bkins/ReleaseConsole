<#
.SYNOPSIS
Installs a specific version of the Local AI Assistant APK to a connected Android device.

.DESCRIPTION
Installs the specified version of the MAUI APK for the given environment
onto a connected physical Android device.

.PARAMETER Environment
Target environment. Valid values: DEV, QA, PROD.

.PARAMETER Version
Version string matching the built artifact.

.EXAMPLE
.\Laa-Install.ps1 -Environment DEV -Version 1.0.20260125.153045
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
    Write-Host "[DEPLOY][$Environment] $Message"
}

try {

    Log "📱 Installing version [$Version] to [$Environment]"
    Log "Detecting Android devices"

    $devices = adb devices | Select-String "device$" | ForEach-Object {
        ($_ -split "\s+")[0]
    }

    if (-not $devices) {
        Write-Error "No Android devices detected"
        exit 1
    }

    $targetDevice = $devices | Where-Object { $_ -notmatch "^emulator-" } | Select-Object -First 1

    if (-not $targetDevice) {
        Log "No physical Android device detected"
        exit 1
    }

    Log "Using device: $targetDevice"

    $appId   = "com.snikpoh.localaiassistant.$Environment"
    $apkPath = "C:\Deploy\CP\MAUI\$Environment\com.snikpoh.localaiassistant.$Environment.apk"

    if (-not (Test-Path $apkPath)) {
        Log "APK not found: $apkPath"
        exit 1
    }

   
    adb -s $targetDevice uninstall "$appId.$Environment" | Out-Null
    adb -s $targetDevice install -r $apkPath
    

    Log "APK installed successfully"
    exit 0
}
catch {
    Write-Error "Deploy failed: $($_.Exception.Message)"
    exit 2
}
