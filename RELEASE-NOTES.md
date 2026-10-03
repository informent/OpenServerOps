# OpenServerOps 1.1.1

This replaces 1.1.0 after downloaded-release testing found that log filters could match the parent folder name. Filters now use relative log paths, and the packaged regression fixture always reproduces the original collision.

- Keep the window responsive during scans and add cancellation through the existing scan button.
- Replace repeated recursive scans with one inventory pass; skip linked folders and report inaccessible paths.
- Correct addon/Workshop counts and display actual findings instead of a fixed zero.
- Distinguish failed ZIP indexes and unsupported archives, with failed ZIP paths shown in activity.
- Fix duplicate-name log selection and bound log reads to 64 KiB.
- Clarify localhost TCP checks and backup-check limits.
- Stop recording normal window closure as a crash.
- Ship a self-contained standalone EXE with native Windows dependencies included.
- Add engine and packaged-app regression gates, with isolated release-test output.

Windows x64. The app remains a read-only local inventory tool. This release does not add remote server access, UDP health checks, backup restore validation, or signed distribution.
