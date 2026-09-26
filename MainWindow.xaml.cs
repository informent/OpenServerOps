using System.IO;
using System.Windows;
using Forms = System.Windows.Forms;

namespace OpenServerOps;

public partial class MainWindow : Window
{
    private string? serverRoot;
    private IReadOnlyList<string> logFiles = Array.Empty<string>();
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
        HealthResult.Text = $"{result.FreeBytes / (1024d * 1024 * 1024):N1} GB free · {result.MatchingProcesses} matching process(es)";
        StorageResult.Text = $"{result.FileCount:N0} files scanned"; AddonResult.Text = $"{result.AddonFileCount:N0} addons · {result.LogCount:N0} logs · {result.WorkshopFileCount:N0} workshop"; BackupResult.Text = $"{result.BackupFileCount:N0} archive(s) found";
        logFiles = result.LogFiles; RefreshLogList();
        Activity.Text = $"Read-only audit completed {DateTime.Now:T}\nFiles scanned: {result.FileCount:N0}\nFree disk: {result.FreeBytes / (1024d * 1024 * 1024):N1} GB\nLog files: {result.LogCount:N0}\nLargest log: {result.LargestLog ?? "none"}\nAddon-related files: {result.AddonFileCount:N0}\nWorkshop files: {result.WorkshopFileCount:N0}\nBackup archives: {result.BackupFileCount:N0}\nNo files were changed.";
    }
    private void LogFilter_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => RefreshLogList();
    private void ClearLogFilter_Click(object sender, RoutedEventArgs e) => LogFilter.Clear();
    private void DarkMode_Click(object sender, RoutedEventArgs e)
    {
        var dark = DarkMode.IsChecked == true;
        System.Windows.Application.Current.Resources["Bg"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(dark ? "#121820" : "#F4F6F8"));
        System.Windows.Application.Current.Resources["Panel"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(dark ? "#1B2530" : "#FFFFFF"));
        System.Windows.Application.Current.Resources["Text"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(dark ? "#EDF3F8" : "#17212B"));
        System.Windows.Application.Current.Resources["Muted"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(dark ? "#A8B7C6" : "#667586"));
        System.Windows.Application.Current.Resources["Line"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(dark ? "#314252" : "#D8E0E8"));
    }
    private void RefreshLogList()
    {
        if (LogList is null) return;
        var filter = LogFilter?.Text ?? string.Empty;
        LogList.ItemsSource = logFiles.Where(x => string.IsNullOrWhiteSpace(filter) || x.Contains(filter, StringComparison.OrdinalIgnoreCase)).Select(Path.GetFileName).ToArray();
    }
}
