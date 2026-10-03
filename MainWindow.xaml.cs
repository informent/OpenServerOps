using System.IO;
using System.Windows;
using Forms = System.Windows.Forms;

namespace OpenServerOps;

public partial class MainWindow : Window
{
    private string? serverRoot;
    private IReadOnlyList<string> logFiles = Array.Empty<string>();
    private readonly AppSettings settings;
    private System.Windows.Controls.TextBox? portEditor;
    public MainWindow(string? initialFolder = null)
    {
        InitializeComponent(); settings = AppSettings.Load(); DarkMode.IsChecked = settings.DarkMode; LogList.SelectionChanged += LogList_SelectionChanged;
        System.Windows.Automation.AutomationProperties.SetName(DarkMode, "Toggle dark theme"); System.Windows.Automation.AutomationProperties.SetName(LogFilter, "Filter log files"); System.Windows.Automation.AutomationProperties.SetName(LogList, "Discovered server logs"); System.Windows.Automation.AutomationProperties.SetName(Activity, "Audit activity and selected log preview");
        var dashboard = Content;
        Content = null;
        var tabs = new System.Windows.Controls.TabControl();
        tabs.Items.Add(new System.Windows.Controls.TabItem { Header = "Dashboard", Content = dashboard });
        tabs.Items.Add(new System.Windows.Controls.TabItem { Header = "Settings", Content = BuildSettingsPanel() });
        Content = tabs;
        if (settings.DarkMode) ApplyTheme(true);
        Closed += (_, _) => scanCancellation?.Cancel();
        if (initialFolder is not null) SelectServer(initialFolder);
    }
    private System.Windows.Controls.Panel BuildSettingsPanel()
    {
        var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(32) };
        panel.Children.Add(new System.Windows.Controls.TextBlock { Text = "Settings", FontSize = 26, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new System.Windows.Controls.TextBlock { Text = "Local scan rules are saved only on this computer.", Foreground = (System.Windows.Media.Brush)FindResource("Muted"), Margin = new Thickness(0, 6, 0, 24) });
        panel.Children.Add(new System.Windows.Controls.TextBlock { Text = "SERVER PORT TO PROBE", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = (System.Windows.Media.Brush)FindResource("Muted") });
        portEditor = new System.Windows.Controls.TextBox { Text = settings.Port.ToString(), Width = 120, Height = 32, Margin = new Thickness(0, 8, 0, 18) };
        panel.Children.Add(portEditor);
        var save = new System.Windows.Controls.Button { Content = "Save scan settings", Width = 170, HorizontalAlignment = System.Windows.HorizontalAlignment.Left, Background = (System.Windows.Media.Brush)FindResource("Blue"), Foreground = System.Windows.Media.Brushes.White };
        save.Click += (_, _) => SaveSettings(); panel.Children.Add(save);
        return panel;
    }
    private void SaveSettings()
    {
        if (portEditor is not null && int.TryParse(portEditor.Text, out var port) && port is > 0 and < 65536) settings.Port = port;
        settings.DarkMode = DarkMode.IsChecked == true; settings.Save();
    }
    private void ChooseServer_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog { Description = "Choose the local server folder to audit" };
        if (dialog.ShowDialog() != Forms.DialogResult.OK) return;
        SelectServer(dialog.SelectedPath);
    }
    private void SelectServer(string path)
    {
        if (!Directory.Exists(path)) { HealthResult.Text = "The selected server folder does not exist."; return; }
        serverRoot = Path.GetFullPath(path); ServerName.Text = new DirectoryInfo(serverRoot).Name; ServerPath.Text = serverRoot;
        logFiles = Array.Empty<string>(); RefreshLogList(); FindingCount.Text = "—"; LastCheck.Text = "—";
        StorageResult.Text = "Not checked"; AddonResult.Text = "Not checked"; BackupResult.Text = "Not checked";
        StateText.Text = "Ready"; HealthResult.Text = "Ready for read-only checks"; Activity.Text = $"Selected {DateTime.Now:T}\n{serverRoot}";
    }
    private CancellationTokenSource? scanCancellation;
    private async void RunChecks_Click(object sender, RoutedEventArgs e)
    {
        if (scanCancellation is not null) { scanCancellation.Cancel(); return; }
        if (serverRoot is null) { HealthResult.Text = "Choose a server folder first."; return; }
        using var cancellation = new CancellationTokenSource();
        scanCancellation = cancellation;
        FindingCount.Text = "—"; StorageResult.Text = "Awaiting results"; AddonResult.Text = "Awaiting results"; BackupResult.Text = "Awaiting results";
        logFiles = Array.Empty<string>(); RefreshLogList();
        ChooseServer.IsEnabled = false; RunChecks.Content = "Cancel scan";
        StateText.Text = "Scanning"; Activity.Text = "Reading the selected folder. You can cancel this scan.";
        var root = serverRoot; var port = settings.Port;
        try
        {
            var result = await Task.Run(() => ServerAudit.Scan(root, new[] { port }, cancellation.Token));
            StateText.Text = result.Issues.Count > 0 ? "Partial scan" : "Checked";
            LastCheck.Text = DateTime.Now.ToShortTimeString(); FindingCount.Text = result.FindingCount.ToString();
            var disk = result.FreeBytes < 0 ? "Disk space unavailable" : $"{result.FreeBytes / (1024d * 1024 * 1024):N1} GB free";
            HealthResult.Text = $"{disk} · localhost TCP {port} {(result.Ports[0].Open ? "open" : "not reached")}";
            StorageResult.Text = $"{result.FileCount:N0} files; {result.SkippedLinks:N0} links skipped";
            AddonResult.Text = $"{result.AddonFileCount:N0} addon files · {result.LogCount:N0} logs · {result.WorkshopFileCount:N0} workshop files";
            BackupResult.Text = $"{result.ValidBackupCount:N0} ZIP indexes readable · {result.InvalidZipCount:N0} failed · {result.UnsupportedBackupCount:N0} unsupported";
            logFiles = result.LogFiles; RefreshLogList();
            var age = result.OldestBackupDays is double days ? $"{days:N0} days" : "none found";
            Activity.Text = $"Read-only audit completed {DateTime.Now:T}\nFiles: {result.FileCount:N0}; findings: {result.FindingCount}\nLargest log: {result.LargestLog ?? "none"}\nEmpty addon files: {result.SuspiciousAddonFiles}; empty workshop files: {result.SuspiciousWorkshopFiles}\nArtifacts hashed: {result.HashedArtifactCount}; not hashed: {result.UnhashedArtifacts}\nOldest archive: {age}\nSkipped links: {result.SkippedLinks}; read failures: {result.Issues.Count}\nProcess-name matches: {result.MatchingProcesses} (not a health check)\nTCP checks do not test UDP game ports. ZIP indexes do not prove restore integrity.\nNo files were changed.";
            foreach (var issue in result.Issues.Take(20)) Activity.Text += $"\n{issue.Path}: {issue.Message}";
            foreach (var archive in result.FailedZipFiles.Take(20)) Activity.Text += $"\n{archive}: ZIP index could not be read.";
        }
        catch (OperationCanceledException) { StateText.Text = "Cancelled"; HealthResult.Text = "Scan cancelled."; Activity.Text = "Scan cancelled. No files were changed."; }
        catch (Exception ex) { StateText.Text = "Failed"; HealthResult.Text = "Scan could not finish."; Activity.Text = ex.Message; }
        finally { scanCancellation = null; ChooseServer.IsEnabled = true; RunChecks.Content = "Run checks"; }
    }
    private void LogFilter_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => RefreshLogList();
    private void ClearLogFilter_Click(object sender, RoutedEventArgs e) => LogFilter.Clear();
    private void LogList_SelectionChanged(object? sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var name = LogList.SelectedItem as string;
        var path = logFiles.FirstOrDefault(x => serverRoot is not null && Path.GetRelativePath(serverRoot, x).Equals(name, StringComparison.OrdinalIgnoreCase));
        if (path is null) return;
        try
        {
            var preview = ServerAudit.ReadLogPreview(path);
            Activity.Text = $"Log preview: {name}\nSeverity matches in displayed text: {preview.SeverityMatches}\n{(preview.Truncated ? "Showing the tail of this UTF-8 log." : "UTF-8 log preview.")}\n\n{preview.Text}";
        }
        catch (Exception ex) { Activity.Text = $"Unable to read selected log: {ex.Message}"; }
    }
    private void DarkMode_Click(object sender, RoutedEventArgs e)
    {
        var dark = DarkMode.IsChecked == true;
        settings.DarkMode = dark; settings.Save(); ApplyTheme(dark);
    }
    private static void ApplyTheme(bool dark)
    {
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
        LogList.ItemsSource = logFiles.Select(x => Path.GetRelativePath(serverRoot!, x)).Where(x => string.IsNullOrWhiteSpace(filter) || x.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToArray();
    }
}
