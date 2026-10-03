using System.Net;
using System.IO;
using System.Text;
using System.Text.Json;

namespace OpenServerOps;

public sealed record AuditSnapshot(string ServerRoot, DateTimeOffset CreatedAt, string HealthGrade, int RiskScore, AuditResult Result);

public static class AuditReport
{
    public static AuditSnapshot Create(string root, AuditResult result)
    {
        var score = Math.Clamp(result.FindingCount * 8 + result.InvalidZipCount * 12 + result.Issues.Count * 5
            + (result.FreeBytes >= 0 && result.FreeBytes < 5L * 1024 * 1024 * 1024 ? 25 : 0), 0, 100);
        var grade = score switch { <= 5 => "A", <= 20 => "B", <= 40 => "C", <= 65 => "D", _ => "F" };
        return new(Path.GetFullPath(root), DateTimeOffset.Now, grade, score, result);
    }

    public static void WriteJson(AuditSnapshot snapshot, string path) =>
        WriteAtomically(path, stream => JsonSerializer.Serialize(stream, snapshot, new JsonSerializerOptions { WriteIndented = true }));

    public static void WriteHtml(AuditSnapshot snapshot, string path)
    {
        static string H(object? value) => WebUtility.HtmlEncode(value?.ToString() ?? "");
        var r = snapshot.Result;
        var rows = string.Join("", r.Issues.Select(x => $"<tr><td>{H(x.Path)}</td><td>{H(x.Message)}</td></tr>"));
        var ports = string.Join(", ", r.Ports.Select(x => $"{x.Port}: {(x.Open ? "open" : "not reached")}"));
        var free = r.FreeBytes < 0 ? "Unknown" : $"{r.FreeBytes / (1024d * 1024 * 1024):N1} GB";
        var html = $$"""
        <!doctype html><html><head><meta charset="utf-8"><title>OpenServerOps audit</title>
        <style>body{font-family:Segoe UI,Arial;margin:40px;color:#17212b;background:#f4f6f8}main{max-width:980px;margin:auto}header,.card{background:white;border:1px solid #d8e0e8;border-radius:10px;padding:22px;margin-bottom:16px}.grade{font-size:44px;font-weight:700;color:#2563d9}.grid{display:grid;grid-template-columns:repeat(4,1fr);gap:12px}.metric{background:#f7f9fb;padding:14px;border-radius:8px}.metric b{display:block;font-size:22px;margin-top:5px}table{width:100%;border-collapse:collapse}td,th{text-align:left;padding:9px;border-bottom:1px solid #e4e9ee}small{color:#667586}</style></head>
        <body><main><header><small>OPENSERVEROPS 2.0 AUDIT</small><h1>{{H(new DirectoryInfo(snapshot.ServerRoot).Name)}}</h1><div>{{H(snapshot.ServerRoot)}}</div><p>Created {{H(snapshot.CreatedAt)}}</p><div class="grade">{{H(snapshot.HealthGrade)}}</div><div>Risk score {{snapshot.RiskScore}} / 100</div></header>
        <section class="card grid"><div class="metric">Files<b>{{r.FileCount:N0}}</b></div><div class="metric">Findings<b>{{r.FindingCount}}</b></div><div class="metric">Logs<b>{{r.LogCount}}</b></div><div class="metric">Free space<b>{{H(free)}}</b></div></section>
        <section class="card"><h2>Coverage</h2><p>Addons: {{r.AddonFileCount:N0}} files · Workshop: {{r.WorkshopFileCount:N0}} files · Hashed: {{r.HashedArtifactCount:N0}} · Unhashed: {{r.UnhashedArtifacts:N0}}</p><p>Backups: {{r.ValidBackupCount}} readable ZIP · {{r.InvalidZipCount}} invalid · {{r.UnsupportedBackupCount}} unsupported</p><p>Local TCP: {{H(ports)}} · Links skipped: {{r.SkippedLinks}}</p></section>
        <section class="card"><h2>Read issues</h2><table><tr><th>Path</th><th>Finding</th></tr>{{rows}}</table></section>
        <section class="card"><small>Generated locally by OpenServerOps. Scans are read-only; TCP checks do not test UDP game ports and ZIP indexing does not prove restorability.</small></section></main></body></html>
        """;
        WriteAtomically(path, stream =>
        {
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
            writer.Write(html);
            writer.Flush();
        });
    }

    private static void WriteAtomically(string path, Action<Stream> write)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(write);
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)!;
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                write(stream);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}
