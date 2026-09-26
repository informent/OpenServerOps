using System.IO;
using OpenServerOps;

var root = Path.Combine(Path.GetTempPath(), "openserverops-test-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(root, "addons", "Example"));
File.WriteAllText(Path.Combine(root, "server.log"), "ok");
File.WriteAllText(Path.Combine(root, "addons", "Example", "addon.txt"), "ok");
var result = ServerAudit.Scan(root);
if (result is not { FileCount: 2, LogCount: 1, AddonFileCount: 1 }) throw new Exception($"Unexpected audit result: {result}");
Directory.Delete(root, true);
Console.WriteLine("PASS: ServerAudit counts files, logs, and addons");
