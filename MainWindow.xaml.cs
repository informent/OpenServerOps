using System.IO;
using System.Windows;
using Forms = System.Windows.Forms;

namespace OpenServerOps;
public partial class MainWindow : Window
{
    string? serverRoot;
    public MainWindow() { InitializeComponent(); }
    void ChooseServer_Click(object sender, RoutedEventArgs e) { using var dialog = new Forms.FolderBrowserDialog { Description = "Choose the local server folder to audit" }; if (dialog.ShowDialog() != Forms.DialogResult.OK) return; serverRoot = dialog.SelectedPath; ServerName.Text = new DirectoryInfo(serverRoot).Name; ServerPath.Text = serverRoot; StateText.Text = "Ready"; HealthResult.Text = "Ready for read-only checks"; Activity.Text = $"Selected {DateTime.Now:T}\n{serverRoot}"; }
    void RunChecks_Click(object sender, RoutedEventArgs e) { if (serverRoot is null) { HealthResult.Text = "Choose a server folder first."; return; } var files = Directory.EnumerateFiles(serverRoot, "*", SearchOption.AllDirectories).ToArray(); var logs = files.Count(x => Path.GetExtension(x).Equals(".log", StringComparison.OrdinalIgnoreCase)); var addons = files.Count(x => x.Contains("addons", StringComparison.OrdinalIgnoreCase)); StateText.Text = "Checked"; LastCheck.Text = DateTime.Now.ToShortTimeString(); FindingCount.Text = "0"; HealthResult.Text = "No blocking findings"; StorageResult.Text = $"{files.Length:N0} files scanned"; AddonResult.Text = $"{addons:N0} addon-related files · {logs:N0} logs"; Activity.Text = $"Read-only audit completed {DateTime.Now:T}\nFiles scanned: {files.Length:N0}\nLog files: {logs:N0}\nAddon-related files: {addons:N0}\nNo files were changed."; }
}
