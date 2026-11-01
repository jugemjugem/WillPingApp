using System.Diagnostics;
using System.Net.Sockets;

namespace WillPingApp;

public sealed class MainForm : Form
{
    private readonly Button start = new() { Text = "監視開始", AutoSize = true };
    private readonly Button stop = new() { Text = "停止", AutoSize = true, Enabled = false };
    private readonly Button openLogs = new() { Text = "ログフォルダーを開く", AutoSize = true };
    private readonly Label state = new() { Text = "停止中", AutoSize = true, Padding = new Padding(12, 8, 0, 0) };
    private readonly TextBox output = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, WordWrap = false };
    private readonly string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WillPingApp", "settings.json");
    private string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WillPingApp", "NetworkMonitorLogs");
    private readonly Button chooseLogs = new() { Text = "保存先を変更", AutoSize = true };
    private readonly TextBox logPath = new() { ReadOnly = true, Dock = DockStyle.Top };
    private readonly NotifyIcon trayIcon = new()
    {
        Icon = SystemIcons.Application,
        Text = "WillPingApp - ダブルクリックで表示"
    };
    private FormWindowState restoreWindowState = FormWindowState.Normal;
    private MonitorLog? logger;
    private CancellationTokenSource? cancellation;
    private Task? monitoring;

    public MainForm()
    {
        Text = "ネットワーク監視";
        Width = 900; Height = 600; MinimumSize = new Size(650, 400);
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
        LoadSettings();
        logPath.Text = logDir;
        bar.Controls.AddRange([start, stop, chooseLogs, openLogs, state]);
        Controls.Add(output); Controls.Add(logPath); Controls.Add(bar);
        chooseLogs.Click += (_, _) => ChooseLogFolder();
        start.Click += (_, _) => StartMonitoring();
        stop.Click += (_, _) => cancellation?.Cancel();
        openLogs.Click += (_, _) => { try { Directory.CreateDirectory(logDir); Process.Start(new ProcessStartInfo(logDir) { UseShellExecute = true }); } catch (Exception ex) { MessageBox.Show(ex.Message, "ログを開けません"); } };
        FormClosing += OnClosing;
        Resize += OnWindowResize;
        trayIcon.DoubleClick += (_, _) => RestoreFromTray();
    }

    private void OnWindowResize(object? sender, EventArgs e)
    {
        if (WindowState != FormWindowState.Minimized)
        {
            restoreWindowState = WindowState;
            return;
        }

        trayIcon.Visible = true;
        ShowInTaskbar = false;
        Hide();
    }

    private void RestoreFromTray()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = restoreWindowState;
        Activate();
        trayIcon.Visible = false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            trayIcon.Dispose();
        base.Dispose(disposing);
    }

    private void StartMonitoring()
    {
        try
        {
            logger = new MonitorLog(logDir);
            cancellation = new CancellationTokenSource();
            start.Enabled = false; chooseLogs.Enabled = false; stop.Enabled = true; state.Text = "監視中";
            monitoring = MonitorAsync(cancellation.Token);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "監視を開始できません"); }
    }

    private void Log(string message)
    {
        output.AppendText(logger!.Write(message) + Environment.NewLine);
    }

    private async Task MonitorAsync(CancellationToken token)
    {
        DateTime? incident = null;
        try
        {
            Log("監視開始。各宛先に4回送信し、検査終了後60秒待機します。");
            while (true)
            {
                token.ThrowIfCancellationRequested();
                string? gateway = PingService.GetGateway();
                var targets = new (string Label, string? Host, AddressFamily Family)[] {
                    ("ルーター", gateway, AddressFamily.InterNetwork), ("外部 IPv4", "8.8.8.8", AddressFamily.InterNetwork),
                    ("IPv4 DNS + 疎通", "google.com", AddressFamily.InterNetwork), ("IPv6 DNS + 疎通", "google.com", AddressFamily.InterNetworkV6) };
                var results = new List<bool>();
                Log("----- 検査開始 -----");
                foreach (var target in targets)
                {
                    token.ThrowIfCancellationRequested();
                    var result = await PingService.CheckAsync(target.Host, target.Family, token);
                    results.Add(result.Received > 0);
                    Log($"{target.Label} ({target.Host ?? "未検出"}): {(result.Received > 0 ? "OK" : "FAIL")} {result.Received}/4 応答; {result.Detail}");
                }
                logger!.WriteResults(results);
                if (results.Contains(false))
                {
                    string reason = string.Join("; ", targets.Where((_, i) => !results[i]).Select(x => x.Label));
                    if (incident is null) { incident = DateTime.Now; Log("障害検出: " + reason); Log("診断情報を保存: " + await logger!.SaveDiagnosticsAsync(reason, token)); }
                    else Log("障害継続: " + reason);
                    state.Text = "障害検出";
                }
                else if (incident is not null) { Log($"復旧を検出。検出からの経過時間: {DateTime.Now - incident.Value:g}"); incident = null; state.Text = "監視中"; }
                else { Log("すべての宛先から応答しました。"); state.Text = "監視中"; }
                await Task.Delay(TimeSpan.FromSeconds(60), token);
            }
        }
        catch (OperationCanceledException) { try { Log("監視を停止しました。"); } catch (IOException) { } }
        catch (Exception ex) { MessageBox.Show(ex.Message, "監視エラー"); }
        finally { start.Enabled = true; chooseLogs.Enabled = true; stop.Enabled = false; state.Text = "停止中"; cancellation?.Dispose(); cancellation = null; }
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(settingsPath))
            {
                var saved = System.Text.Json.JsonSerializer.Deserialize<Settings>(File.ReadAllText(settingsPath));
                if (!string.IsNullOrWhiteSpace(saved?.LogDirectory) && Path.IsPathFullyQualified(saved.LogDirectory)) logDir = saved.LogDirectory;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            MessageBox.Show("設定を読み込めません。既定の保存先を使用します。\n" + ex.Message, "設定");
        }
    }

    private void ChooseLogFolder()
    {
        using var dialog = new FolderBrowserDialog { Description = "ログの保存先を選択してください", SelectedPath = logDir, UseDescriptionForTitle = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(settingsPath, System.Text.Json.JsonSerializer.Serialize(new Settings(dialog.SelectedPath)));
            logDir = dialog.SelectedPath;
            logPath.Text = logDir;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(ex.Message, "設定を保存できません");
        }
    }

    private sealed record Settings(string LogDirectory);

    private async void OnClosing(object? sender, FormClosingEventArgs e)
    {
        if (monitoring is { IsCompleted: false })
        {
            e.Cancel = true; Enabled = false; cancellation?.Cancel();
            await monitoring;
            Close();
        }
    }
}
