# OpenServerOps Windows installation

The release ZIP is self-contained for Windows x64. Extract it, then run `Install-OpenServerOps.ps1` from the extracted folder. The script installs to `%LOCALAPPDATA%\OpenServerOps` and creates a desktop shortcut. `Uninstall-OpenServerOps.ps1` removes that installation and shortcut.

Release signing is optional and transparent. Supply either a trusted Authenticode certificate thumbprint or an encrypted PFX plus a prompted `SecureString` password to `tools/Sign-Release.ps1`; releases without a certificate are explicitly marked unsigned rather than silently claiming trust.
