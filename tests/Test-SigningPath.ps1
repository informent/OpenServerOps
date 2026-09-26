$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$script = Join-Path $root 'tools/Sign-Release.ps1'
$content = Get-Content $script -Raw
foreach ($required in @('CertificateThumbprint','PfxPath','PfxPassword','HasPrivateKey','EphemeralKeySet')) { if ($content -notlike "*$required*") { throw "Signing path missing: $required" } }
Write-Host 'PASS: certificate-store and encrypted-PFX signing paths are present'
