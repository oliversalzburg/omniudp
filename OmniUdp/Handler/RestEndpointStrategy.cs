using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Timers;
using log4net;
using OmniUdp.Payload;
using Timer = System.Timers.Timer;

namespace OmniUdp.Handler {
	/// <summary>
	///     An event handling strategy that sends events to a RESTful API endpoint.
	/// </summary>
	internal class RestEndpointStrategy : IEventHandlingStrategy {
		/// <summary>
		///     A set of credentials to use for authentication with the REST endpoint.
		/// </summary>
		private class Credentials {
			/// <summary>
			///     The bearer token for the Authentication HTTP header.
			/// </summary>
			public string Code;

			/// <summary>
			///     Construct a new Credentials isntance.
			/// </summary>
			/// <param name="code">The bearer token for the Authentication HTTP header.</param>
			public Credentials( string code ) {
				Code = code;
			}
		}

		/// <summary>
		///     Encapsulates a UID send request.
		/// </summary>
		private class UidRequest {
			/// <summary>
			///     The payload that should be sent to the server.
			/// </summary>
			public string Payload { get; set; }

			/// <summary>
			///     How often was the request retried?
			/// </summary>
			public int RetryCount { get; set; }
		}

		/// <summary>
		///     The logging interface.
		/// </summary>
		private readonly ILog Log = LogManager.GetLogger( MethodBase.GetCurrentMethod().DeclaringType );

		/// <summary>
		///     The formatter to use to format the payload.
		/// </summary>
		public JsonFormatter PreferredFormatter { get; private set; }

		/// <summary>
		///     The RESTful API endpoint to use.
		/// </summary>
		protected string Endpoint { get; private set; }

		/// <summary>
		///     The endpoint expressed in a Uri instance.
		/// </summary>
		private Uri EndpointUri { get; set; }

		/// <summary>
		///     Ignore SSL certificate errors.
		/// </summary>
		private bool InsecureSSL;

		/// <summary>
		///     The IP address to send the requests from.
		/// </summary>
		private string IpAddress;

		/// <summary>
		///     The credentials to use for authentication on the server.
		/// </summary>
		private Credentials AuthInfo;

		/// <summary>
		///     PriorityQueue for storing payload.
		/// </summary>
		private static ConcurrentQueue<UidRequest> RecievedPayloads;

		/// <summary>
		///     Timer for retrying to send the payloads
		/// </summary>
		private static Timer RetryTimer;

		/// <summary>
		///     Construct a new RestEndpointStrategy instance.
		/// </summary>
		/// <param name="endpoint">The API endpoint to connect to.</param>
		/// <param name="insecureSSL">Ignore SSL certificate errors.</param>
		/// <param name="ipAddress">The IP address to send the requests from.</param>
		/// <param name="authFile">The location of the authentication file.</param>
		/// <param name="formatter">The formatter to use to format the payloads.</param>
		public RestEndpointStrategy( string endpoint, bool insecureSSL, string ipAddress, string authFile, JsonFormatter formatter ) {
			Log.Info( "Using REST endpoint strategy." );

			PreferredFormatter = formatter;
			InsecureSSL = insecureSSL;
			IpAddress = ipAddress ?? "0.0.0.0";

			if( !string.IsNullOrEmpty( authFile ) ) {
				if( !File.Exists( authFile ) ) {
					Log.WarnFormat( "The file '{0}' doesn't exist. No authentication credentials will be available!", authFile );
				} else {
					try {
						AuthInfo = ReadFromAuthFile( authFile );
						Log.InfoFormat( "Read authentication data from '{0}'", authFile );
					} catch( Exception ex ) {
						Log.ErrorFormat( "Problem with authentication file: {0}", ex.Message );
						throw;
					}
				}
			}

			Log.InfoFormat( "REST requests will be sent from '{0}'.", IpAddress );

			RecievedPayloads = new ConcurrentQueue<UidRequest>();

			RetryTimer = new Timer();
			RetryTimer.Elapsed += OnTimedEvent;
			RetryTimer.Interval = TimeSpan.FromSeconds( 10.0 ).TotalMilliseconds;
			RetryTimer.Start();

			Endpoint = endpoint;
			EndpointUri = new Uri( Endpoint );

			CheckConfiguration();
		}

		/// <summary>
		///     Validates the parameters used to construct the strategy and logs relevant details.
		/// </summary>
		private void CheckConfiguration() {
			Log.InfoFormat( "Using RESTful API endpoint '{0}'.", Endpoint );
		}

		/// <summary>
		///     Handle an event that should be treated as an error.
		/// </summary>
		/// <param name="payload">The payload to send with the event.</param>
		public void HandleErrorEvent( byte[] error ) {
			string payload = PreferredFormatter.GetPayloadForError( error );
			SendPayload( payload );
		}

		/// <summary>
		///     Handle an event that should be treated as a UID being successfully read.
		/// </summary>
		/// <param name="payload">The payload to send with the event.</param>
		public void HandleUidEvent( byte[] uid ) {
			string payload = PreferredFormatter.GetPayload( uid );
			SendPayload( payload );
		}

		/// <summary>
		///     Starts a Thread for sending a payload to the API endpoint.
		/// </summary>
		/// <param name="payload">The payload to send.</param>
		private void SendPayload( string payload ) {
			if( InsecureSSL ) {
				ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
			}

			UidRequest uidPayload = new UidRequest {
				Payload = payload
			};

			Thread t = new Thread(
				() => { SendRequest( uidPayload ); } );
			t.Start();
		}

		/// <summary>
		///     Sends payloads from the ConcurrentQueue.
		/// </summary>
		private void SendRequest( UidRequest payload = null ) {
			UidRequest uidRequest = payload;
			if( !RecievedPayloads.IsEmpty && uidRequest == null ) {
				RecievedPayloads.TryDequeue( out uidRequest );
			}
			if( uidRequest != null ) {
				HttpWebRequest request = (HttpWebRequest)( HttpWebRequest.Create( EndpointUri ) );
				request.Method = "POST";
				request.ContentType = "application/json";

				// Trying to get the authentication information. If an error occured, nothing will be sent.
				if( null != AuthInfo ) {
					request.Headers.Add( "Authorization", "Bearer " + AuthInfo.Code );
				}

				request.ContentLength = uidRequest.Payload.Length;

				request.ServicePoint.BindIPEndPointDelegate = ( servicePoint, remoteEndPoint, retryCount ) => new IPEndPoint( IPAddress.Parse( IpAddress ), 0 );

				try {
					using( Stream requestStream = request.GetRequestStream() ) {
						using( StreamWriter writer = new StreamWriter( requestStream ) ) {
							Log.InfoFormat( "Using JSON Payload: '{0}' (Retry: {1})", uidRequest.Payload, uidRequest.RetryCount );
							writer.Write( uidRequest.Payload );
						}
					}

					using( HttpWebResponse response = (HttpWebResponse)request.GetResponse() ) {
						using( Stream dataStream = response.GetResponseStream() ) {
							using( StreamReader reader = new StreamReader( dataStream ) ) {
								// Read the content. 
								string responseFromServer = reader.ReadToEnd();
								// Display the content.
								Log.Info( responseFromServer );
							}
						}
					}

					if( RecievedPayloads.IsEmpty ) {
						Log.Info( "Retry queue cleared" );
					}
				} catch( WebException ex ) {
					Log.ErrorFormat( "Problem communicating with RESTful endpoint while delivering '{1}': {0} (Retry: {2})", ex.Message, uidRequest.Payload, uidRequest.RetryCount );

					if( uidRequest.RetryCount <= 9 ) {
						RecievedPayloads.Enqueue( uidRequest );
						++uidRequest.RetryCount;
					} else {
						Log.WarnFormat( "Giving up on payload after {0} retries!", uidRequest.RetryCount );
					}
				}
			}
		}

		/// <summary>
		///     Reads text from the authentication file.
		/// </summary>
		/// <param name="path">The path of the authentication file.</param>
		/// <returns>Credentials</returns>
		private Credentials ReadFromAuthFile( string path = "auth.txt" ) {
			string authInformation;

			if( string.IsNullOrEmpty( path ) ) {
				throw new ArgumentNullException( "path" );
			}
			if( !File.Exists( path ) ) {
				throw new InvalidOperationException( String.Format( "Auth file {0} does not exist.", path ) );
			}

			// Read data from file.
			authInformation = File.ReadAllText( path );
			if( String.IsNullOrEmpty( authInformation ) ) {
				throw new Exception( "Invalid auth token." );
			}

			return new Credentials( authInformation );
		}

		/// <summary>
		///     When Timer elapses, retry sending the recieved Devices.
		/// </summary>
		/// <param name="source"></param>
		/// <param name="e"></param>
		private void OnTimedEvent( object source, ElapsedEventArgs e ) {
			Thread t = new Thread( () => SendRequest() );
			t.Start();
		}
	}
}