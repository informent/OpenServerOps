# OpenServerOps

A local Windows server inventory and troubleshooting console. Choose a server folder, run checks, and inspect findings and log previews. Scans do not modify server files or connect to a hosting account.

## Version 1.1.1

- Scan once in the background, with **Cancel scan** available while work runs.
- Skip symbolic links and junctions, including cycles. Unreadable paths produce a partial scan and visible findings.
- Classify addon and Workshop files by folder segments relative to the selected root, avoiding false counts from parent-folder names.
- Count empty addon/Workshop files, unreadable ZIP indexes and read failures as findings.
- Show logs by relative path so two `server.log` files remain distinct. UTF-8 previews read at most 64 KiB and display at most 5,000 characters.
- Distinguish readable ZIP indexes, failed ZIP checks and unsupported `.7z` archives. No archive is extracted or restored.
- Package Windows dependencies inside the standalone EXE.

## Run

Open `OpenServerOps.exe`, choose **Choose server**, and then **Run checks**. You can also preselect a folder without starting a scan:

```powershell
.\OpenServerOps.exe --folder "C:\Servers\Example"
```

The optional port check connects to localhost over TCP. It does not test UDP game-server availability, remote servers, or application health. Process counts match the selected folder's name and do not establish that a server is healthy.

ZIP checks read archive indexes only; they do not prove payload integrity or that a backup will restore. `.7z` content is not inspected. Files over 10 MiB are excluded from artifact hashing and included in the not-hashed count; computed hashes are not compared with a trusted baseline. Files can change during a live scan. Link skipping is not a security boundary against concurrent hostile filesystem changes.

## Build and validate

```powershell
dotnet run --project tests/ServerAuditTests.csproj -c Release
powershell -NoProfile -File tests/Test-Release.ps1
```

The engine suite uses temporary fixtures, including junctions and a permission-denied directory whose permissions are restored during cleanup. The release test opens the published desktop application and operates its controls, so run it on an available Windows desktop. GitHub Actions runs both suites on Windows. The **Verify published release** workflow tests the exact downloadable EXE on the runner.

See [VALIDATION.md](VALIDATION.md) for the release checks and limits.

## Principles

- No telemetry and no credential collection.
- Read-only checks before any mutating action.
- Clear evidence for every finding.
- Backups and rollback before future writes.

## License

MIT.
