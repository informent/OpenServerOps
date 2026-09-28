# OpenServerOps 1.1.0 validation

## Automated coverage

- File inventory and relative-path classification, including misleading parent and sibling names.
- Readable ZIP indexes, unreadable ZIPs, unsupported `.7z` files and visible finding counts.
- Locked artifacts, hash-size limits, inaccessible folders, missing/empty roots and cancellation.
- External and cyclic junctions are skipped; linked scan roots are rejected.
- A real localhost TCP listener is detected.
- Multi-megabyte UTF-8 logs produce bounded previews; logs shared by an active writer remain readable.
- Source-file hashes match before and after scans.
- The packaged UI scans a fixture, displays two expected findings, opens both duplicate-name logs correctly, filters the list and preserves fixture hashes.
- A separate workflow downloads published assets, checks their GitHub digests and exercises the downloaded EXE on a Windows runner.

The packaged UI workflow uses the documented `--folder` startup option. It does not claim native folder-picker coverage. Static installer/signing-script checks are not installer round-trip tests or proof of a signed executable.

## Limits

No remote or live hosting account is touched. No UDP protocol check, real backup restore, `.7z` parsing, trusted artifact baseline, exhaustive DPI/accessibility coverage, or independent security audit is claimed. Archives are checked for index readability only. Scans are not atomic snapshots. Log preview supports UTF-8; other encodings may display incorrectly.

Interactive runtime checks run on GitHub's Windows runner.
