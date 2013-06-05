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
  internal static class UidBroadcaster {
    /// <summary>
    ///   The logging <see langword="interface" />
    /// </summary>
    private static readonly ILog Log = LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod().DeclaringType );

    /// <summary>
    ///   Broadcast the provided UID on all interfaces
    /// </summary>
    /// <param name="uid">The UID to broadcast</param>
    /// <param name="port">The target UDP port that should be used.</param>
    /// <param name="limitToAddress">
    ///   Only broadcast from the given IP address. By default, all IP addresses
    ///   are used.
    /// </param>
    /// <exception cref="InvalidOperationException">
    ///   The given IP address isn't assigned to any local network adapter.
    /// </exception>
    public static void BroadcastUid( byte[] uid, int port, string limitToAddress = null ) {
      Dictionary<IPAddress, PhysicalAddress> ipMacTable = IpHelper.BuildIpMacTable();
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

          broadcastSocket.SendTo( uid, sendEndPoint );
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
  }
}