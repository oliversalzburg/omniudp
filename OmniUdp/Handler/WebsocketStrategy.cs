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
    private IWebSocketConnection Connection { get; set; }

    /// <summary>
    ///   The logging interface.
    /// </summary>
    private readonly ILog Log = LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod().DeclaringType );


    public WebsocketStrategy( JsonFormatter formatter, int port = DefaultPort ) {
      PreferredFormatter = formatter;

      var server = new WebSocketServer( String.Format( "ws://0.0.0.0:{0}", port ) );
      server.Start( socket => {
        Connection = socket;
        socket.OnOpen = () => Console.WriteLine( "Open!" );
        socket.OnClose = () => Console.WriteLine( "Close!" );
        socket.OnMessage = message => socket.Send( message );
      } );
    }

    public void HandleErrorEvent( byte[] error ) {
      string payload = PreferredFormatter.GetPayloadForError( error );
    }

    public void HandleUidEvent( byte[] uid ) {
      string payload = PreferredFormatter.GetPayload( uid );
      Connection.Send( payload );
    }
  }
}
