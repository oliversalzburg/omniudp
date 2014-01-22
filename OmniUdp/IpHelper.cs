using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace OmniUdp {
  /// <summary>
  /// Helper methods related IP.
  /// </summary>
  public static class IpHelper {
    /// <summary>
    /// Builds an IP to MAC lookup table.
    /// </summary>
    /// <param name="limitToInterface">Only include the given interface in the table.</param>
    public static Dictionary<IPAddress, PhysicalAddress> BuildIpMacTable( string limitToInterface = null ) {
      Dictionary<IPAddress, PhysicalAddress> table = new Dictionary<IPAddress, PhysicalAddress>();

      NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
      foreach( NetworkInterface networkInterface in networkInterfaces ) {
        // Skip interfaces if a limitation is given
        if( null != limitToInterface && networkInterface.Name != limitToInterface ) {
          continue;
        }
        IPInterfaceProperties ipInterfaceProperties = networkInterface.GetIPProperties();
        foreach( UnicastIPAddressInformation unicastAddress in
          ipInterfaceProperties.UnicastAddresses.Where(
            unicastAddress =>
            unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork
            && !IPAddress.IsLoopback( unicastAddress.Address )
            && !unicastAddress.Address.ToString().StartsWith( "169.254." ) ) ) {
          table[ unicastAddress.Address ] = networkInterface.GetPhysicalAddress();
        }
      }

      return table;
    }

    /// <summary>
    /// Check if a given network interface name exists on the local system.
    /// </summary>
    /// <param name="interfaceName">The name of the network interface to look for.</param>
    /// <returns>true if the interface exists; false otherwise.</returns>
    public static bool DoesInterfaceExist( string interfaceName ) {
      NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
      return networkInterfaces.Any( n => n.Name == interfaceName );
    }
  }
}