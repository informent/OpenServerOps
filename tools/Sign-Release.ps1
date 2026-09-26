param([Parameter(Mandatory)][string]$Path, [string]$CertificateThumbprint)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($CertificateThumbprint)) { Write-Warning 'No certificate supplied; signing was skipped.'; exit 0 }
$cert = Get-ChildItem Cert:\CurrentUser\My\$CertificateThumbprint -ErrorAction Stop
Set-AuthenticodeSignature -FilePath $Path -Certificate $cert -HashAlgorithm SHA256
Write-Host "Signed $Path"
