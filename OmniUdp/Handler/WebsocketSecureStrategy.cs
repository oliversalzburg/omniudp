using Fleck;
using OmniUdp.Payload;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace OmniUdp.Handler {
  class WebsocketSecureStrategy : WebsocketStrategy {
    /// <summary>
    ///   The logging interface.
    /// </summary>
    private readonly ILog Log = LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod().DeclaringType );

    private string CertificateFilename { get; set; }

    public WebsocketSecureStrategy( string ipAddress, JsonFormatter formatter, string certificateFilename, int port = DefaultPort ) : base( ipAddress, formatter, port ) {
      CertificateFilename = certificateFilename; 
    }

    override public void Initialize() {
      SocketServer = new WebSocketServer( String.Format( "wss://{0}:{1}", IpAddress, Port ) );
      SocketServer.Certificate = new X509Certificate2( CertificateFilename );
      StartServer();
    }
  }
}
