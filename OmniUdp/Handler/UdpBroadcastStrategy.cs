using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net;

namespace OmniUdp.Handler {
  class UdpBroadcastStrategy : IEventHandlingStrategy {
    /// <summary>
    ///   The default UDP port to use for broadcasts.
    /// </summary>
    private const int DefaultPort = 30000;

    /// <summary>
    ///   The logging <see langword="interface" />
    /// </summary>
    private readonly ILog Log = LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod().DeclaringType );

    /// <summary>
    ///   The network interface from which to broadcast.
    /// </summary>
    public string NetworkInterface { get; private set; }

    /// <summary>
    ///   The IP address from which to broadcast.
    /// </summary>
    public string IPAddress { get; private set; }

    /// <summary>
    ///   The UDP port to broadcast to.
    /// </summary>
    public int Port { get; private set; }

    /// <summary>
    ///   Use only the loopback device for broadcasting.
    /// </summary>
    public bool UseLoopback { get; private set; }

    /// <summary>
    ///   Construct a new UdpBroadcastStrategy instance.
    /// </summary>
    /// <param name="networkInterface">
    ///   The network interface from which to broadcast.
    /// </param>
    /// <param name="ipAddress">The IP address from which to broadcast.</param>
    /// <param name="useLoopback">
    ///   Use only the loopback device for broadcasting.
    /// </param>
    public UdpBroadcastStrategy( string networkInterface, string ipAddress, bool useLoopback, int port = DefaultPort ) {
      NetworkInterface = networkInterface;
      IPAddress = ipAddress;
      UseLoopback = useLoopback;
      Port = port;

      CheckConfiguration();
    }

    /// <summary>
    ///   Validates the parameters used to construct the strategy and logs relevant details.
    /// </summary>
    private void CheckConfiguration() {
      if( !UseLoopback ) {
        if( null != NetworkInterface ) {
          if( !IpHelper.DoesInterfaceExist( NetworkInterface ) ) {
            Console.Error.WriteLine( "The given interface '{0}' does not exist on the local system.", NetworkInterface );
            throw new InvalidOperationException( String.Format( "The given interface '{0}' does not exist on the local system.", NetworkInterface ) );
          }
          Log.InfoFormat( "Broadcasts limited to interface '{0}'.", NetworkInterface );
        }
        if( null != IPAddress ) {
          Log.InfoFormat( "Broadcasts limited to address '{0}'.", IPAddress );
        }
      } else {
        Log.InfoFormat( "Sending UIDs only on the loopback device!" );
        IPAddress = "127.0.0.1";
      }
    }

    public void HandleErrorEvent( byte[] payload ) {
      if( UseLoopback ) {
        UdpBroadcaster.BroadcastLoopback( payload, Port );
      } else {
        UdpBroadcaster.Broadcast( payload, Port, IPAddress, NetworkInterface );
      }
    }

    public void HandleUidEvent( byte[] payload ) {
      if( UseLoopback ) {
        UdpBroadcaster.BroadcastLoopback( payload, Port );
      } else {
        UdpBroadcaster.Broadcast( payload, Port, IPAddress, NetworkInterface );
      }
    }
  }
}
