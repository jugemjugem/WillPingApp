using System.Diagnostics;
using System.Text;

namespace WillPingApp;

public sealed class MonitorLog
{
    private readonly string directory, logFile, csvFile;
    public MonitorLog(string directory)
    {
        this.directory = directory;
        Directory.CreateDirectory(directory);
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        logFile = Path.Combine(directory, $"monitor_{stamp}.txt");
        csvFile = Path.Combine(directory, $"monitor_{stamp}.csv");
        File.WriteAllText(csvFile, "Time,Gateway,IPv4_IP,IPv4_DNS,IPv6_DNS\r\n", Encoding.UTF8);
    }
    public string Write(string message)
    {
        string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {message}";
        File.AppendAllText(logFile, line + Environment.NewLine, Encoding.UTF8);
        return line;
    }
    public void WriteResults(IEnumerable<bool> results) => File.AppendAllText(csvFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{string.Join(',', results.Select(x => x ? "OK" : "FAIL"))}\r\n", Encoding.UTF8);

    public async Task<string> SaveDiagnosticsAsync(string reason, CancellationToken token)
    {
        string path = Path.Combine(directory, $"diagnostic_{DateTime.Now:yyyyMMdd_HHmmss_fff}.txt");
        var text = new StringBuilder($"Diagnostic time: {DateTime.Now:o}\r\nReason: {reason}\r\nComputer: {Environment.MachineName}\r\n");
        foreach (var command in new[] { "ipconfig /all", "route print -4", "powershell -NoProfile -Command Get-NetAdapter", "ipconfig /displaydns" })
        {
            token.ThrowIfCancellationRequested();
            text.AppendLine($"\r\n===== {command} =====");
            using var process = Process.Start(new ProcessStartInfo("cmd.exe", "/c " + command) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true })!;
            var stdout = process.StandardOutput.ReadToEndAsync(token); var stderr = process.StandardError.ReadToEndAsync(token);
            try { await process.WaitForExitAsync(token); } catch (OperationCanceledException) { try { process.Kill(true); } catch (InvalidOperationException) { } throw; }
            text.AppendLine(await stdout); text.AppendLine(await stderr);
        }
        await File.WriteAllTextAsync(path, text.ToString(), Encoding.UTF8, token);
        return path;
    }

}
