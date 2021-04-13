using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

public static class NetworkUtils
{
    public static string GetRemoteIP(TcpClient client)
    {
        IPEndPoint endpoint = client.Client.RemoteEndPoint as IPEndPoint;
        return endpoint.Address.MapToIPv4().ToString();
    }

    public static string GetLocalIP()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
            if (ip.AddressFamily == AddressFamily.InterNetwork)
                return ip.ToString();
        return "UNKNOWN";
    }

    public static async Task ConnectAsync(this TcpClient tcpClient, string host, int port, CancellationToken cancellationToken) 
    {
        using (cancellationToken.Register(cancellationToken.ThrowIfCancellationRequested)) {
            try {
                cancellationToken.ThrowIfCancellationRequested();
                await tcpClient.ConnectAsync(host, port).ConfigureAwait(false);
            } catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }

    public static bool IsAnIp(string ip) {
        return IPAddress.TryParse(ip, out _);
    }
}