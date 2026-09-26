using System.IO;
using System.IO.Compression;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace OpenServerOps;

public sealed record PortResult(int Port, bool Open);
public sealed record AuditResult(int FileCount, int LogCount, int AddonFileCount, int WorkshopFileCount, int BackupFileCount, int ValidBackupCount, int SuspiciousAddonFiles, int SuspiciousWorkshopFiles, int HashedArtifactCount, double? OldestBackupDays, long FreeBytes, int MatchingProcesses, string? LargestLog, IReadOnlyList<string> LogFiles, IReadOnlyList<PortResult> Ports);

public static class ServerAudit
{
    public static AuditResult Scan(string root, IReadOnlyList<int>? ports = null)
    {
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories);
        var count = 0; var logs = 0; var addons = 0; var workshop = 0; var backups = 0;
        foreach (var file in files)
        {
            count++;
            if (Path.GetExtension(file).Equals(".log", StringComparison.OrdinalIgnoreCase)) logs++;
            if (file.Contains("addons", StringComparison.OrdinalIgnoreCase)) addons++;
            if (file.Contains("workshop", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(file).Equals(".bin", StringComparison.OrdinalIgnoreCase)) workshop++;
            if (file.Contains("backup", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(file).Equals(".zip", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(file).Equals(".7z", StringComparison.OrdinalIgnoreCase)) backups++;
        }
        var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(root))!);
        var processName = new DirectoryInfo(root).Name;
        var matchingProcesses = 0;
        try { matchingProcesses = System.Diagnostics.Process.GetProcessesByName(processName).Length; } catch { }
        string? largestLog = null; var logFiles = Array.Empty<string>();
        try { logFiles = Directory.EnumerateFiles(root, "*.log", SearchOption.AllDirectories).ToArray(); largestLog = logFiles.OrderByDescending(FileInfoLength).FirstOrDefault(); } catch { }
        var backupFiles = files.Where(x => x.Contains("backup", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(x).Equals(".zip", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(x).Equals(".7z", StringComparison.OrdinalIgnoreCase)).ToArray();
        var validBackups = backupFiles.Count(IsReadableArchive);
        var suspiciousAddons = files.Count(x => x.Contains("addons", StringComparison.OrdinalIgnoreCase) && FileInfoLength(x) == 0);
        var suspiciousWorkshop = files.Count(x => (x.Contains("workshop", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(x).Equals(".bin", StringComparison.OrdinalIgnoreCase)) && FileInfoLength(x) == 0);
        var hashedArtifacts = files.Count(x => (x.Contains("addons", StringComparison.OrdinalIgnoreCase) || x.Contains("workshop", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(x).Equals(".bin", StringComparison.OrdinalIgnoreCase)) && TryHash(x));
        var oldestBackupDays = backupFiles.Length == 0 ? (double?)null : backupFiles.Max(x => (DateTime.UtcNow - File.GetLastWriteTimeUtc(x)).TotalDays);
        var portResults = (ports ?? new[] { 27015 }).Select(port => new PortResult(port, IsPortOpen(port))).ToArray();
        return new AuditResult(count, logs, addons, workshop, backups, validBackups, suspiciousAddons, suspiciousWorkshop, hashedArtifacts, oldestBackupDays, drive.AvailableFreeSpace, matchingProcesses, largestLog, logFiles, portResults);
    }

    private static long FileInfoLength(string path) { try { return new FileInfo(path).Length; } catch { return 0; } }
    private static bool IsReadableArchive(string path) { try { using var archive = ZipFile.OpenRead(path); return archive.Entries.Count >= 0; } catch { return false; } }
    private static bool TryHash(string path) { try { var info = new FileInfo(path); if (info.Length > 10 * 1024 * 1024) return false; using var stream = info.OpenRead(); _ = SHA256.HashData(stream); return true; } catch { return false; } }
    private static bool IsPortOpen(int port)
    {
        try { using var client = new TcpClient(); var task = client.ConnectAsync("127.0.0.1", port); return task.Wait(120) && client.Connected; } catch { return false; }
    }
}
