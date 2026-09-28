using System.IO.Compression;
using System.Net;
using System.IO;
using System.Net.Sockets;
using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Security.Cryptography;
using OpenServerOps;

var root = Path.Combine(Path.GetTempPath(), "openserverops-addons-workshop-backup-" + Guid.NewGuid().ToString("N"));
var outside = root + "-outside";
Directory.CreateDirectory(root); Directory.CreateDirectory(outside);
void Write(string relative, string content)
{
    var path = Path.Combine(root, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, content);
}
void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
var links = new List<string>();
try
{
    Write("server.log", "ok"); Write("addons/Example/addon.txt", "ok"); Write("workshop.bin", "ok");
    Write("backup.zip", "invalid archive"); Write("backup-notes.txt", "not an archive"); Write("notaddons/other.txt", "not an addon");
    Write("first/server.log", "first log"); Write("second/server.log", "second log");
    Write("addons/empty.txt", ""); Write("addons/locked.txt", "locked artifact"); Write("unsupported.7z", "not inspected");
    using (var stream = File.Create(Path.Combine(root, "huge.bin"))) stream.SetLength(11 * 1024 * 1024);
    using (var zip = ZipFile.Open(Path.Combine(root, "good.zip"), ZipArchiveMode.Create))
    using (var writer = new StreamWriter(zip.CreateEntry("data.txt").Open())) writer.Write("backup payload");
    var before = Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(path => path, Hash);
    using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    AuditResult result;
    using (var locked = new FileStream(Path.Combine(root, "addons/locked.txt"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        result = ServerAudit.Scan(root, new[] { port });
    Require(result.FileCount == 13 && result.LogCount == 3, "File/log inventory mismatch.");
    Require(result.AddonFileCount == 3 && result.WorkshopFileCount == 2, "Classification leaked from a parent name or substring.");
    Require(result.BackupFileCount == 3 && result.ValidBackupCount == 1 && result.InvalidZipCount == 1 && result.UnsupportedBackupCount == 1, "Archive classifications are incorrect.");
    Require(result.SuspiciousAddonFiles == 1 && result.HashedArtifactCount == 3 && result.UnhashedArtifacts == 2, "Artifact coverage is incorrect.");
    Require(result.Issues.Count == 1 && result.FindingCount == 3, "Read failures or findings were hidden.");
    Require(result.Ports.Single().Open, "Listening localhost TCP port was not detected.");
    Require(result.LogFiles.Any(path => path.EndsWith(Path.Combine("first", "server.log"))) && result.LogFiles.Any(path => path.EndsWith(Path.Combine("second", "server.log"))), "Duplicate log filenames were lost.");
    foreach (var (path, hash) in before) Require(Hash(path) == hash, "Read-only scan changed a source file.");
    Console.WriteLine("PASS: single inventory, relative classifications, archive distinctions, findings, locked files, hash limits, TCP and read-only hashes");

    using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
    try { ServerAudit.Scan(root, cancellationToken: cancelled.Token); throw new Exception("Cancellation ignored."); } catch (OperationCanceledException) { }
    try { ServerAudit.Scan(root, new[] { 0 }); throw new Exception("Invalid port accepted."); } catch (ArgumentOutOfRangeException) { }
    try { ServerAudit.Scan(Path.Combine(root, "missing")); throw new Exception("Missing folder accepted."); } catch (DirectoryNotFoundException) { }
    var empty = Path.Combine(root, "empty"); Directory.CreateDirectory(empty);
    Require(ServerAudit.Scan(empty, Array.Empty<int>()) is { FileCount: 0, OldestBackupDays: null, FindingCount: 0 }, "Empty-folder scan failed.");
    Console.WriteLine("PASS: cancellation, invalid ports, missing and empty folders");

    var blocked = new DirectoryInfo(Path.Combine(root, "blocked")); blocked.Create();
    File.WriteAllText(Path.Combine(blocked.FullName, "private.txt"), "fixture");
    var originalAcl = blocked.GetAccessControl(); var deniedAcl = blocked.GetAccessControl();
    deniedAcl.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.ListDirectory, AccessControlType.Deny));
    try
    {
        blocked.SetAccessControl(deniedAcl);
        var partial = ServerAudit.Scan(root, Array.Empty<int>());
        Require(partial.Issues.Any(issue => issue.Path == "blocked") && partial.FileCount == 13, "Inaccessible directory did not produce a partial scan.");
    }
    finally
    {
        deniedAcl.SetSecurityDescriptorBinaryForm(originalAcl.GetSecurityDescriptorBinaryForm(), AccessControlSections.Access);
        blocked.SetAccessControl(deniedAcl);
    }
    Console.WriteLine("PASS: inaccessible folder reported while accessible files remain inventoried");

    var log = Path.Combine(root, "large.log");
    using (var writer = new StreamWriter(log)) { writer.WriteLine("ERROR outside preview"); for (var i = 0; i < 100000; i++) writer.WriteLine("ordinary historical line"); writer.WriteLine("ERROR final marker"); }
    var preview = ServerAudit.ReadLogPreview(log);
    Require(preview.Truncated && preview.Text.Length <= 5000 && preview.Text.Contains("ERROR final marker") && !preview.Text.Contains("outside preview") && preview.SeverityMatches == 1, "Bounded log preview failed.");
    using (var writer = new FileStream(log, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
        Require(ServerAudit.ReadLogPreview(log).Text.Contains("final marker"), "Active log could not be previewed.");
    Console.WriteLine("PASS: bounded multi-megabyte UTF-8 preview and active-log sharing");

    File.WriteAllText(Path.Combine(outside, "external.log"), "must not be scanned");
    foreach (var pair in new[] { ("external-link", outside), ("cycle-link", root) })
    {
        var link = Path.Combine(root, pair.Item1);
        var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "/c", "mklink", "/J", link, pair.Item2 }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!; process.WaitForExit();
        Require(process.ExitCode == 0, "Could not create junction test fixture."); links.Add(link);
    }
    var linked = ServerAudit.Scan(root, Array.Empty<int>());
    Require(linked.SkippedLinks == 2 && !linked.LogFiles.Any(path => path.Contains("external-link")), "Junction traversal escaped scope.");
    try { ServerAudit.Scan(links[0]); throw new Exception("Linked root accepted."); } catch (IOException) { }
    Console.WriteLine("PASS: external and cyclic junctions skipped; linked scan root rejected");
}
finally
{
    foreach (var link in links) Directory.Delete(link);
    Directory.Delete(root, true); Directory.Delete(outside, true);
}
