using System.IO;
using System.IO.Compression;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace OpenServerOps;

public sealed record PortResult(int Port, bool Open);
public sealed record AuditIssue(string Path, string Message);
public sealed record LogPreview(string Text, bool Truncated, int SeverityMatches);
public sealed record AuditResult(int FileCount, int LogCount, int AddonFileCount, int WorkshopFileCount, int BackupFileCount, int ValidBackupCount, int SuspiciousAddonFiles, int SuspiciousWorkshopFiles, int HashedArtifactCount, double? OldestBackupDays, long FreeBytes, int MatchingProcesses, string? LargestLog, IReadOnlyList<string> LogFiles, IReadOnlyList<PortResult> Ports)
{
    public IReadOnlyList<AuditIssue> Issues { get; init; } = Array.Empty<AuditIssue>();
    public int UnsupportedBackupCount { get; init; }
    public int InvalidZipCount { get; init; }
    public int SkippedLinks { get; init; }
    public int UnhashedArtifacts { get; init; }
    public IReadOnlyList<string> FailedZipFiles { get; init; } = Array.Empty<string>();
    public int FindingCount => SuspiciousAddonFiles + SuspiciousWorkshopFiles + InvalidZipCount + Issues.Count;
}

public static class ServerAudit
{
    public static AuditResult Scan(string root, IReadOnlyList<int>? ports = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        root = Path.GetFullPath(root);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Choose a real server folder, not a symbolic link or junction.");
        var requestedPorts = (ports ?? new[] { 27015 }).Distinct().ToArray();
        if (requestedPorts.Any(port => port is < 1 or > 65535)) throw new ArgumentOutOfRangeException(nameof(ports));
        var count = 0; var addons = 0; var workshop = 0; var backups = 0;
        var validBackups = 0; var invalidZips = 0; var unsupported = 0; var skippedLinks = 0;
        var suspiciousAddons = 0; var suspiciousWorkshop = 0; var hashes = 0; var unhashed = 0;
        var logFiles = new List<string>(); var issues = new List<AuditIssue>(); var failedZips = new List<string>();
        string? largestLog = null; long largestLogBytes = -1; double? oldest = null;
        var pending = new Stack<string>(); pending.Push(root);
        while (pending.TryPop(out var folder))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) { skippedLinks++; continue; }
                foreach (var entry in Directory.EnumerateFileSystemEntries(folder))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var attributes = File.GetAttributes(entry);
                        if ((attributes & FileAttributes.ReparsePoint) != 0) { skippedLinks++; continue; }
                        if ((attributes & FileAttributes.Directory) != 0) { pending.Push(entry); continue; }
                        var info = new FileInfo(entry); var length = info.Length; count++;
                        var relative = Path.GetRelativePath(root, entry);
                        var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        var isAddon = segments.SkipLast(1).Contains("addons", StringComparer.OrdinalIgnoreCase);
                        var isWorkshop = segments.SkipLast(1).Contains("workshop", StringComparer.OrdinalIgnoreCase) || info.Extension.Equals(".bin", StringComparison.OrdinalIgnoreCase);
                        if (info.Extension.Equals(".log", StringComparison.OrdinalIgnoreCase))
                        {
                            logFiles.Add(entry);
                            if (length > largestLogBytes) { largestLog = entry; largestLogBytes = length; }
                        }
                        if (isAddon) { addons++; if (length == 0) suspiciousAddons++; }
                        if (isWorkshop) { workshop++; if (length == 0) suspiciousWorkshop++; }
                        if (isAddon || isWorkshop)
                        {
                            if (length > 10 * 1024 * 1024) unhashed++;
                            else
                            {
                                try { using var stream = File.OpenRead(entry); _ = SHA256.HashData(stream); hashes++; }
                                catch (Exception ex) when (IsFileError(ex)) { unhashed++; issues.Add(new(relative, "Artifact could not be read for hashing.")); }
                            }
                        }
                        var zip = info.Extension.Equals(".zip", StringComparison.OrdinalIgnoreCase);
                        if (zip || info.Extension.Equals(".7z", StringComparison.OrdinalIgnoreCase))
                        {
                            backups++;
                            var age = Math.Max(0, (DateTime.UtcNow - info.LastWriteTimeUtc).TotalDays);
                            oldest = Math.Max(oldest ?? 0, age);
                            if (!zip) unsupported++;
                            else
                            {
                                try { using var archive = ZipFile.OpenRead(entry); _ = archive.Entries.Count; validBackups++; }
                                catch (Exception ex) when (IsFileError(ex) || ex is InvalidDataException) { invalidZips++; failedZips.Add(relative); }
                            }
                        }
                    }
                    catch (Exception ex) when (IsFileError(ex)) { issues.Add(new(Path.GetRelativePath(root, entry), "File metadata could not be read.")); }
                }
            }
            catch (Exception ex) when (IsFileError(ex)) { issues.Add(new(Path.GetRelativePath(root, folder), "Folder could not be fully enumerated.")); }
        }
        long freeBytes = -1;
        try { freeBytes = new DriveInfo(Path.GetPathRoot(root)!).AvailableFreeSpace; }
        catch (Exception ex) when (IsFileError(ex) || ex is ArgumentException) { issues.Add(new(".", "Free disk space is unavailable.")); }
        var matchingProcesses = 0;
        try
        {
            var processes = System.Diagnostics.Process.GetProcessesByName(new DirectoryInfo(root).Name);
            matchingProcesses = processes.Length;
            foreach (var process in processes) process.Dispose();
        }
        catch (InvalidOperationException) { }
        var portResults = new List<PortResult>();
        foreach (var port in requestedPorts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            portResults.Add(new(port, IsPortOpen(port, cancellationToken)));
        }
        logFiles.Sort(StringComparer.OrdinalIgnoreCase);
        return new(count, logFiles.Count, addons, workshop, backups, validBackups, suspiciousAddons, suspiciousWorkshop, hashes, oldest, freeBytes, matchingProcesses, largestLog, logFiles, portResults)
        { Issues = issues, InvalidZipCount = invalidZips, UnsupportedBackupCount = unsupported, SkippedLinks = skippedLinks, UnhashedArtifacts = unhashed, FailedZipFiles = failedZips };
    }

    public static LogPreview ReadLogPreview(string path)
    {
        const int maxBytes = 64 * 1024;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var truncated = stream.Length > maxBytes;
        if (truncated) stream.Seek(-maxBytes, SeekOrigin.End);
        var buffer = new byte[maxBytes]; var read = 0;
        while (read < buffer.Length)
        {
            var current = stream.Read(buffer, read, buffer.Length - read);
            if (current == 0) break;
            read += current;
        }
        var text = Encoding.UTF8.GetString(buffer, 0, read).TrimStart('\uFEFF');
        if (truncated && text.IndexOf('\n') is var newline && newline >= 0) text = text[(newline + 1)..];
        if (text.Length > 5000) { text = text[^5000..]; truncated = true; }
        var matches = text.Split('\n').Count(line => line.Contains("error", StringComparison.OrdinalIgnoreCase) || line.Contains("exception", StringComparison.OrdinalIgnoreCase));
        return new(text, truncated, matches);
    }

    static bool IsFileError(Exception ex) => ex is IOException or UnauthorizedAccessException or System.Security.SecurityException;
    static bool IsPortOpen(int port, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(120);
        try { using var client = new TcpClient(); client.ConnectAsync("127.0.0.1", port, timeout.Token).AsTask().GetAwaiter().GetResult(); return true; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return false; }
        catch (SocketException) { return false; }
    }
}
