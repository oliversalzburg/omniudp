using Fleck;
using log4net;
using OmniUdp.Payload;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace OmniUdp.Handler {
  class WebsocketStrategy : IEventHandlingStrategy {
    /// <summary>
    ///   The default TCP port to use for broadcasts.
    /// </summary>
    private const int DefaultPort = 81;

    /// <summary>
    ///   The formatter to use to format the payload.
    /// </summary>
    public JsonFormatter PreferredFormatter { get; private set; }

    private WebSocketServer SocketServer { get; set; }
    private List<IWebSocketConnection> Connections { get; set; }

    /// <summary>
    ///   The logging interface.
    /// </summary>
    private readonly ILog Log = LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod().DeclaringType );


    public WebsocketStrategy( string ipAddress, JsonFormatter formatter, int port = DefaultPort ) {
      Log.Info( "Using websocket strategy." );

      Connections = new List<IWebSocketConnection>();

      // Check if a valid port was provided; otherwise use default port instead.
      if( 0 == port ) {
        port = DefaultPort;
      }
      // Check if a valid IP address was provided; otherwise listen on all IP addresses.
      if( null == ipAddress ) {
        ipAddress = "0.0.0.0";
      }

      PreferredFormatter = formatter;

      Fleck.FleckLog.Level = LogLevel.Error;
      var server = new WebSocketServer( String.Format( "ws://{0}:{1}", ipAddress, port ) );
      server.Start( socket => {
        
        socket.OnOpen = () => {
          Log.InfoFormat( "New websocket connection from {0}:{1}.", socket.ConnectionInfo.ClientIpAddress, socket.ConnectionInfo.ClientPort );
          Connections.Add( socket );
        };
        socket.OnClose = () => {
          Log.InfoFormat( "Lost websocket connection to {0}:{1}.", socket.ConnectionInfo.ClientIpAddress, socket.ConnectionInfo.ClientPort );
          Connections.Remove( socket );
        };
        socket.OnError = ( ex ) => {
          Log.Info( "Error on websocket." );
          Connections.Remove( socket );
        };
      } );
    }

    public void HandleErrorEvent( byte[] error ) {
      if( !Connections.Any() ) {
        Log.Debug( "No connected websockets. Nothing to send." );
        return;
      }

      string payload = PreferredFormatter.GetPayloadForError( error );
      Log.InfoFormat( "Sending error event to {0} connected clients.", Connections.Count );
      foreach( IWebSocketConnection socket in Connections ) {
        socket.Send( payload );
      }
    }

    public void HandleUidEvent( byte[] uid ) {
      if( !Connections.Any() ) {
        Log.Debug( "No connected websockets. Nothing to send." );
        return;
      }

      string payload = PreferredFormatter.GetPayload( uid );
      Log.InfoFormat( "Sending payload to {0} connected clients.", Connections.Count );
      foreach( IWebSocketConnection socket in Connections ) {
        socket.Send( payload );
      }
    }
  }
}
