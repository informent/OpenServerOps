using System.IO;
using System.Text.Json;

namespace OpenServerOps;

public sealed class AppSettings
{
    public bool DarkMode { get; set; }
    public int Port { get; set; } = 27015;
    private static string PathName => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenServerOps", "settings.json");
    public static AppSettings Load() { try { return File.Exists(PathName) ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(PathName)) ?? new AppSettings() : new AppSettings(); } catch { return new AppSettings(); } }
    public void Save() { try { Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PathName)!); File.WriteAllText(PathName, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); } catch { } }
}
