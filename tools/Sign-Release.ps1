param([Parameter(Mandatory)][string]$Path, [string]$CertificateThumbprint, [string]$PfxPath, [SecureString]$PfxPassword)
$ErrorActionPreference = 'Stop'
if (![string]::IsNullOrWhiteSpace($PfxPath)) {
    if (!$PfxPassword) { throw 'PfxPassword is required when PfxPath is supplied.' }
    $cert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($PfxPath, $PfxPassword, [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet)
} elseif (![string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    $cert = Get-ChildItem Cert:\CurrentUser\My\$CertificateThumbprint -ErrorAction Stop
} else { Write-Warning 'No certificate supplied; signing was skipped.'; exit 0 }
if (!$cert.HasPrivateKey) { throw 'The selected certificate has no private key.' }
Set-AuthenticodeSignature -FilePath $Path -Certificate $cert -HashAlgorithm SHA256
Write-Host "Signed $Path"
