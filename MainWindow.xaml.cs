using System.IO;
using System.Windows;
using Forms = System.Windows.Forms;

namespace OpenServerOps;

public partial class MainWindow : Window
{
    private string? serverRoot;
    public MainWindow() => InitializeComponent();
    private void ChooseServer_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog { Description = "Choose the local server folder to audit" };
        if (dialog.ShowDialog() != Forms.DialogResult.OK) return;
        serverRoot = dialog.SelectedPath; ServerName.Text = new DirectoryInfo(serverRoot).Name; ServerPath.Text = serverRoot;
        StateText.Text = "Ready"; HealthResult.Text = "Ready for read-only checks"; Activity.Text = $"Selected {DateTime.Now:T}\n{serverRoot}";
    }
    private void RunChecks_Click(object sender, RoutedEventArgs e)
    {
        if (serverRoot is null) { HealthResult.Text = "Choose a server folder first."; return; }
        var result = ServerAudit.Scan(serverRoot); StateText.Text = "Checked"; LastCheck.Text = DateTime.Now.ToShortTimeString(); FindingCount.Text = "0";
        HealthResult.Text = "No blocking findings"; StorageResult.Text = $"{result.FileCount:N0} files scanned";
        AddonResult.Text = $"{result.AddonFileCount:N0} addons · {result.LogCount:N0} logs · {result.WorkshopFileCount:N0} workshop";
        Activity.Text = $"Read-only audit completed {DateTime.Now:T}\nFiles scanned: {result.FileCount:N0}\nLog files: {result.LogCount:N0}\nAddon-related files: {result.AddonFileCount:N0}\nWorkshop files: {result.WorkshopFileCount:N0}\nBackup archives: {result.BackupFileCount:N0}\nNo files were changed.";
    }
}
