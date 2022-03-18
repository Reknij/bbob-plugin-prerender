using System.Net.NetworkInformation;
using Bbob.Plugin;

namespace bbob_plugin_prerender;

public static class MyHelper
{
    public static readonly string prerenderDirectory = Path.Combine(PluginHelper.CurrentDirectory, "prerender");

    public static int GetAvailablePort(int startingPort)
    {
        if (startingPort > ushort.MaxValue) throw new ArgumentException($"Can't be greater than {ushort.MaxValue}", nameof(startingPort));
        var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();

        var connectionsEndpoints = ipGlobalProperties.GetActiveTcpConnections().Select(c => c.LocalEndPoint);
        var tcpListenersEndpoints = ipGlobalProperties.GetActiveTcpListeners();
        var udpListenersEndpoints = ipGlobalProperties.GetActiveUdpListeners();
        var portsInUse = connectionsEndpoints.Concat(tcpListenersEndpoints)
                                             .Concat(udpListenersEndpoints)
                                             .Select(e => e.Port);

        return Enumerable.Range(startingPort, ushort.MaxValue - startingPort + 1).Except(portsInUse).FirstOrDefault();
    }
}