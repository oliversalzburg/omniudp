using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using log4net;

namespace OmniUdp {
  /// <summary>
  ///   Allows for simple data packet broadcasting via UDP
  /// </summary>
  internal static class UdpBroadcaster {
    /// <summary>
    ///   The logging <see langword="interface" />
    /// </summary>
    private static readonly ILog Log = LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod().DeclaringType );

    /// <summary>
    ///   Broadcast the provided payload on all interfaces
    /// </summary>
    /// <param name="payload">The payload to broadcast</param>
    /// <param name="port">The target UDP port that should be used.</param>
    /// <param name="limitToAddress">
    ///   Only broadcast from the given IP address. By default, all IP addresses
    ///   are used.
    /// </param>
    /// <param name="limitToInterface">
    ///   Only broadcast from this interface. By default, all interfaces are used.
    /// </param>
    /// <exception cref="InvalidOperationException">
    ///   The given IP address isn't assigned to any local network adapter.
    /// </exception>
    public static void Broadcast( byte[] payload, int port, string limitToAddress = null, string limitToInterface = null ) {
      Dictionary<IPAddress, PhysicalAddress> ipMacTable = IpHelper.BuildIpMacTable( limitToInterface );
      IPAddress[] ipAddresses = ipMacTable.Keys.ToArray();

      if( null != limitToAddress ) {
        IPAddress ipAddress = ipAddresses.SingleOrDefault( i => i.ToString() == limitToAddress );
        if( null == ipAddress ) {
          throw new InvalidOperationException( "The given IP address isn't assigned to any local network adapter." );

        } else {
          ipAddresses = new[] {ipAddress};
        }
      }

      Socket broadcastSocket = null;
      foreach( IPAddress sourceIp in ipAddresses ) {
        try {
          Log.InfoFormat( "Broadcasting from '{0}'...", sourceIp );
          broadcastSocket = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp );
          broadcastSocket.ReceiveTimeout = (int)TimeSpan.FromSeconds( 10 ).TotalMilliseconds;
          broadcastSocket.SetSocketOption( SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1 );
          broadcastSocket.Bind( new IPEndPoint( sourceIp, 0 ) );

          IPEndPoint sendEndPoint = new IPEndPoint( IPAddress.Broadcast, port );

          broadcastSocket.SendTo( payload, sendEndPoint );

        } catch( Exception e ) {
          Log.Error( e.Message );
          Log.Debug( e.StackTrace );

        } finally {
          if( null != broadcastSocket ) {
            broadcastSocket.Close();
          }
        }
      }
    }

    /// <summary>
    ///   "Broadcast" the UID on the loopback device.
    /// </summary>
    /// <param name="payload">The payload to broadcast</param>
    /// <param name="port">The target UDP port that should be used.</param>
    public static void BroadcastLoopback( byte[] payload, int port ) {
      Log.InfoFormat( "Broadcasting locally..." );
      Socket broadcastSocket = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp );
      broadcastSocket.ReceiveTimeout = (int)TimeSpan.FromSeconds( 10 ).TotalMilliseconds;
      broadcastSocket.SetSocketOption( SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1 );
      broadcastSocket.Bind( new IPEndPoint( IPAddress.Loopback, 0 ) );

      IPEndPoint sendEndPoint = new IPEndPoint( IPAddress.Broadcast, port );

      broadcastSocket.SendTo( payload, sendEndPoint );
    }
  }
}