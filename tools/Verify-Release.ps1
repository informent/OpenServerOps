param([Parameter(Mandatory)][string]$Executable)
$ErrorActionPreference = 'Stop'
if (!(Test-Path $Executable)) { throw "Executable not found: $Executable" }
$signature = Get-AuthenticodeSignature $Executable
if ($signature.Status -eq 'Valid') { Write-Host "PASS: valid Authenticode signature ($($signature.SignerCertificate.Subject))"; exit 0 }
if ($signature.Status -eq 'NotSigned') { Write-Warning 'Release is unsigned; provide a certificate before claiming signed distribution.'; exit 0 }
throw "Signature status: $($signature.Status)"
