using Fleck;
using log4net;
using OmniUdp.Payload;
using System;
using System.Security.Cryptography.X509Certificates;

namespace OmniUdp.Handler {
    class WebsocketSecureStrategy : WebsocketStrategy {
        /// <summary>
        ///   The logging interface.
        /// </summary>
        private readonly ILog Log = LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod().DeclaringType );

        private string CertificateFilename { get; set; }
        private string CertificatePassword { get; set; }

        public WebsocketSecureStrategy( string ipAddress, JsonFormatter formatter, string certificateFilename, int port = DefaultPort, string certificatePassword = null )
            : base( ipAddress, formatter, port ) {
            CertificateFilename = certificateFilename;
            CertificatePassword = certificatePassword;
        }

        override public void Initialize() {
            SocketServer = new WebSocketServer( String.Format( "wss://{0}:{1}", IpAddress, Port ) );

            if( string.IsNullOrEmpty( CertificatePassword ) ) {
                SocketServer.Certificate = new X509Certificate2( CertificateFilename );
            } else {
                SocketServer.Certificate = new X509Certificate2( CertificateFilename, CertificatePassword );
            }
            StartServer();
        }
    }
}
