using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace WillPingApp;

public static class PingService
{
    public static string? GetGateway() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(n => n.OperationalStatus == OperationalStatus.Up)
        .SelectMany(n => n.GetIPProperties().GatewayAddresses)
        .Select(g => g.Address).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !a.Equals(IPAddress.Any))?.ToString();

    public static async Task<(int Received, string Detail)> CheckAsync(string? host, AddressFamily family, CancellationToken token)
    {
        if (host is null) return (0, "デフォルトゲートウェイが見つかりません");
        int received = 0; var details = new List<string>();
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, family, token);
            if (addresses.Length == 0) return (0, "対象アドレスが見つかりません");
            using var ping = new Ping();
            for (int i = 0; i < 4; i++)
            {
                token.ThrowIfCancellationRequested();
                var reply = await ping.SendPingAsync(addresses[0], TimeSpan.FromMilliseconds(1200), cancellationToken: token);
                if (reply.Status == IPStatus.Success) { received++; details.Add($"{reply.RoundtripTime}ms"); }
                else details.Add(reply.Status.ToString());
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is PingException or SocketException or ArgumentException) { details.Add(ex.Message); }
        return (received, string.Join(", ", details));
    }

}
