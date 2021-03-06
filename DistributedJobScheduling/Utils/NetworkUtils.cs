using System.Net;
using System.Net.Sockets;

public static class NetworkUtils
{
    public static string GetRemoteIP(TcpClient client)
    {
        IPEndPoint endpoint = client.Client.RemoteEndPoint as IPEndPoint;
        return endpoint.Address.MapToIPv4().ToString();
    }

    public static string GetRemoteIP(UdpClient client)
    {
        IPEndPoint endpoint = client.Client.RemoteEndPoint as IPEndPoint;
        return endpoint.Address.MapToIPv4().ToString();
    }
}