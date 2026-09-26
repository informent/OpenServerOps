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
    public MainWindow()
    {
        InitializeComponent(); settings = AppSettings.Load(); DarkMode.IsChecked = settings.DarkMode; LogList.SelectionChanged += LogList_SelectionChanged;
        System.Windows.Automation.AutomationProperties.SetName(DarkMode, "Toggle dark theme"); System.Windows.Automation.AutomationProperties.SetName(LogFilter, "Filter log files"); System.Windows.Automation.AutomationProperties.SetName(LogList, "Discovered server logs"); System.Windows.Automation.AutomationProperties.SetName(Activity, "Audit activity and selected log preview");
        var dashboard = Content;
        var tabs = new System.Windows.Controls.TabControl();
        tabs.Items.Add(new System.Windows.Controls.TabItem { Header = "Dashboard", Content = dashboard });
        tabs.Items.Add(new System.Windows.Controls.TabItem { Header = "Settings", Content = BuildSettingsPanel() });
        Content = tabs;
        if (settings.DarkMode) ApplyTheme(true);
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
        serverRoot = dialog.SelectedPath; ServerName.Text = new DirectoryInfo(serverRoot).Name; ServerPath.Text = serverRoot;
        StateText.Text = "Ready"; HealthResult.Text = "Ready for read-only checks"; Activity.Text = $"Selected {DateTime.Now:T}\n{serverRoot}";
    }
    private void RunChecks_Click(object sender, RoutedEventArgs e)
    {
        if (serverRoot is null) { HealthResult.Text = "Choose a server folder first."; return; }
        var result = ServerAudit.Scan(serverRoot, new[] { settings.Port }); StateText.Text = "Checked"; LastCheck.Text = DateTime.Now.ToShortTimeString(); FindingCount.Text = "0";
        HealthResult.Text = $"{result.FreeBytes / (1024d * 1024 * 1024):N1} GB free · {result.MatchingProcesses} process(es) · port {result.Ports[0].Port} {(result.Ports[0].Open ? "open" : "closed")}";
        StorageResult.Text = $"{result.FileCount:N0} files scanned"; AddonResult.Text = $"{result.AddonFileCount:N0} addons · {result.LogCount:N0} logs · {result.WorkshopFileCount:N0} workshop"; BackupResult.Text = $"{result.ValidBackupCount:N0}/{result.BackupFileCount:N0} readable · oldest {(result.OldestBackupDays ?? 0):N0}d";
        logFiles = result.LogFiles; RefreshLogList();
        Activity.Text = $"Read-only audit completed {DateTime.Now:T}\nFiles scanned: {result.FileCount:N0}\nFree disk: {result.FreeBytes / (1024d * 1024 * 1024):N1} GB\nLog files: {result.LogCount:N0}\nLargest log: {result.LargestLog ?? "none"}\nAddon files: {result.AddonFileCount:N0} ({result.SuspiciousAddonFiles} empty)\nWorkshop files: {result.WorkshopFileCount:N0} ({result.SuspiciousWorkshopFiles} empty)\nArtifact hashes: {result.HashedArtifactCount:N0}\nBackups: {result.ValidBackupCount:N0}/{result.BackupFileCount:N0} readable; oldest {(result.OldestBackupDays ?? 0):N0} days\nPort {result.Ports[0].Port}: {(result.Ports[0].Open ? "open" : "closed")}\nNo files were changed.";
    }
    private void LogFilter_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => RefreshLogList();
    private void ClearLogFilter_Click(object sender, RoutedEventArgs e) => LogFilter.Clear();
    private void LogList_SelectionChanged(object? sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var name = LogList.SelectedItem as string;
        var path = logFiles.FirstOrDefault(x => Path.GetFileName(x).Equals(name, StringComparison.OrdinalIgnoreCase));
        if (path is null) return;
        try
        {
            var text = File.ReadAllText(path);
            var preview = text.Length > 5000 ? text[^5000..] : text;
            var errors = text.Split('\n').Count(x => x.Contains("error", StringComparison.OrdinalIgnoreCase) || x.Contains("exception", StringComparison.OrdinalIgnoreCase));
            Activity.Text = $"Log preview: {Path.GetFileName(path)}\nSeverity matches: {errors}\n\n{preview}";
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
        LogList.ItemsSource = logFiles.Where(x => string.IsNullOrWhiteSpace(filter) || x.Contains(filter, StringComparison.OrdinalIgnoreCase)).Select(Path.GetFileName).ToArray();
    }
}
