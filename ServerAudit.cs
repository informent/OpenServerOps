using System.IO;

namespace OpenServerOps;

public sealed record AuditResult(int FileCount, int LogCount, int AddonFileCount);

public static class ServerAudit
{
    public static AuditResult Scan(string root)
    {
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories);
        var count = 0; var logs = 0; var addons = 0;
        foreach (var file in files)
        {
            count++;
            if (Path.GetExtension(file).Equals(".log", StringComparison.OrdinalIgnoreCase)) logs++;
            if (file.Contains("addons", StringComparison.OrdinalIgnoreCase)) addons++;
        }
        return new AuditResult(count, logs, addons);
    }
}
