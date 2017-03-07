using System;
using log4net;
using OmniUdp.Payload;

namespace OmniUdp.Handler {
	/// <summary>
	///     An event handling strategy that broadcasts events over UDP.
	/// </summary>
	internal class UdpBroadcastStrategy : IEventHandlingStrategy {
		/// <summary>
		///     The default UDP port to use for broadcasts.
		/// </summary>
		private const int DefaultPort = 30000;

		/// <summary>
		///     The logging <see langword="interface" />
		/// </summary>
		private readonly ILog Log = LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod().DeclaringType );

		public ByteArrayFormatter PreferredFormatter { get; private set; }

		/// <summary>
		///     The network interface from which to broadcast.
		/// </summary>
		public string NetworkInterface { get; private set; }

		/// <summary>
		///     The IP address from which to broadcast.
		/// </summary>
		public string IPAddress { get; private set; }

		/// <summary>
		///     The UDP port to broadcast to.
		/// </summary>
		public int Port { get; private set; }

		/// <summary>
		///     Use only the loopback device for broadcasting.
		/// </summary>
		public bool UseLoopback { get; private set; }

		/// <summary>
		///     Construct a new UdpBroadcastStrategy instance.
		/// </summary>
		/// <param name="networkInterface">
		///     The network interface from which to broadcast.
		/// </param>
		/// <param name="ipAddress">The IP address from which to broadcast.</param>
		/// <param name="useLoopback">
		///     Use only the loopback device for broadcasting.
		/// </param>
		/// <param name="formatter">The formatter to use to format the payloads.</param>
		/// <param name="port">The UDP port to broadcast to.</param>
		public UdpBroadcastStrategy( string networkInterface, string ipAddress, bool useLoopback, ByteArrayFormatter formatter, int port = DefaultPort ) {
			Log.Info( "Using UDP broadcast strategy." );

			// Check if a valid port was provided; otherwise use default port instead.
			if( 0 == port ) {
				port = DefaultPort;
			}

			PreferredFormatter = formatter;

			NetworkInterface = networkInterface;
			IPAddress = ipAddress;
			UseLoopback = useLoopback;
			Port = port;

			CheckConfiguration();
		}

		/// <summary>
		///     Validates the parameters used to construct the strategy and logs relevant details.
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
			Log.InfoFormat( "Using UDP broadcast to port '{0}'.", Port );
		}

		/// <summary>
		///     Handle an event that should be treated as an error.
		/// </summary>
		/// <param name="payload">The payload to send with the event.</param>
		public void HandleErrorEvent( byte[] error ) {
			byte[] payload = PreferredFormatter.GetPayloadForError( error );

			Log.InfoFormat( "Using payload '{0}'.", BitConverter.ToString( payload ).Replace( "-", string.Empty ) );

			if( UseLoopback ) {
				UdpBroadcaster.BroadcastLoopback( payload, Port );
			} else {
				UdpBroadcaster.Broadcast( payload, Port, IPAddress, NetworkInterface );
			}
		}

		/// <summary>
		///     Handle an event that should be treated as a UID being successfully read.
		/// </summary>
		/// <param name="payload">The payload to send with the event.</param>
		public void HandleUidEvent( byte[] uid ) {
			byte[] payload = PreferredFormatter.GetPayload( uid );

			Log.InfoFormat( "Using payload '{0}'.", BitConverter.ToString( payload ).Replace( "-", string.Empty ) );

			if( UseLoopback ) {
				UdpBroadcaster.BroadcastLoopback( payload, Port );
			} else {
				UdpBroadcaster.Broadcast( payload, Port, IPAddress, NetworkInterface );
			}
		}
	}
}