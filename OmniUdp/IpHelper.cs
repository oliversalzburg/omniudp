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
    public static Dictionary<IPAddress, PhysicalAddress> BuildIpMacTable() {
      Dictionary<IPAddress,PhysicalAddress> table = new Dictionary<IPAddress, PhysicalAddress>();

      NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
      foreach( NetworkInterface networkInterface in networkInterfaces ) {
        IPInterfaceProperties ipInterfaceProperties = networkInterface.GetIPProperties();
        foreach( UnicastIPAddressInformation unicastAddress in
          ipInterfaceProperties.UnicastAddresses.Where( unicastAddress => unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork ) ) {
          table[ unicastAddress.Address ] = networkInterface.GetPhysicalAddress();
        }
      }

      return table;
    }
  }
}
