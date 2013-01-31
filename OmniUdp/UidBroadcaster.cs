using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using log4net;

namespace OmniUdp {
  internal class UidBroadcaster {
    /// <summary>
    /// The logging interface
    /// </summary>
    private static readonly ILog Log = LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod().DeclaringType );

    public void BroadcastUid( byte[] uid, int port ) {
      Dictionary<IPAddress, PhysicalAddress> ipMacTable = IPHelper.BuildIpMacTable();
      IPAddress[] ipAddresses = ipMacTable.Keys.ToArray();

      Socket broadcastSocket = null;
      foreach( IPAddress sourceIp in ipAddresses ) {
        try {
          Log.InfoFormat( "Broadcasting from '{0}'...", sourceIp );
          // The socket used for discovery
          broadcastSocket = new Socket( AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp );
          broadcastSocket.ReceiveTimeout = (int)TimeSpan.FromSeconds( 10 ).TotalMilliseconds;
          broadcastSocket.SetSocketOption( SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1 );
          broadcastSocket.Bind( new IPEndPoint( sourceIp, 0 ) );

          EndPoint receiveEndPoint = new IPEndPoint( IPAddress.Any, new Random().Next( 10000, 20000 ) );
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

      //Log.Error( "Error: Obtaining server configuration failed!" );
    }
  }
}