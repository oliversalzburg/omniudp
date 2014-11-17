using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Timers;
using log4net;
using OmniUdp.Payload;

namespace OmniUdp.Handler {
  /// <summary>
  ///   An event handling strategy that sends events to a RESTful API endpoint.
  /// </summary>
  class RestEndpointStrategy : IEventHandlingStrategy {
    /// <summary>
    ///   A set of credentials to use for authentication with the REST endpoint.
    /// </summary>
    private class Credentials {
      /// <summary>
      ///   A UUID that is used for the FM-Auth-Id HTTP header.
      /// </summary>
      public string Uuid;

      /// <summary>
      ///   A code that is used for the FM-Auth-Code HTTP header.
      /// </summary>
      public string Code;

      /// <summary>
      ///   Construct a new Credentials isntance.
      /// </summary>
      /// <param name="uuid">A UUID that is used for the FM-Auth-Id HTTP header.</param>
      /// <param name="code">A code that is used for the FM-Auth-Code HTTP header.</param>
      public Credentials( string uuid, string code ) {
        Uuid = uuid;
        Code = code;
      }
    }

    /// <summary>
    ///   The logging interface.
    /// </summary>
    private readonly ILog Log = LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod().DeclaringType );

    /// <summary>
    ///   The formatter to use to format the payload.
    /// </summary>
    public JsonFormatter PreferredFormatter { get; private set; }

    /// <summary>
    ///   The RESTful API endpoint to use.
    /// </summary>
    protected string Endpoint { get; private set; }

    /// <summary>
    ///   The endpoint expressed in a Uri instance.
    /// </summary>
    private Uri EndpointUri { get; set; }

    /// <summary>
    ///   Ignore SSL certificate errors.
    /// </summary>
    private bool InsecureSSL;

    /// <summary>
    ///   The credentials to use for authentication on the server.
    /// </summary>
    private Credentials AuthInfo;

    /// <summary>
    ///   PriorityQueue for storing payload.
    /// </summary>
    private static ConcurrentQueue<string> RecievedPayloads;
    
    /// <summary>
    ///   Timer for retrying to send the payloads
    /// </summary>
    private static System.Timers.Timer RetryTimer;

    /// <summary>
    ///   Construct a new RestEndpointStrategy instance.
    /// </summary>
    /// <param name="endpoint">The API endpoint to connect to.</param>
    /// <param name="insecureSSL">Ignore SSL certificate errors.</param>
    /// <param name="authFile">The location of the authentication file.</param>
    /// <param name="formatter">The formatter to use to format the payloads.</param>
    public RestEndpointStrategy( string endpoint, bool insecureSSL, string authFile, JsonFormatter formatter ) {
      Log.Info( "Using REST endpoint strategy." );

      PreferredFormatter = formatter;
      InsecureSSL = insecureSSL;

      if( !string.IsNullOrEmpty( authFile ) && File.Exists( authFile ) ) {
        try {
          AuthInfo = ReadFromAuthFile( authFile );
        } catch( Exception ex ) {
          Log.ErrorFormat( "Problem with authentication file: {0}", ex.Message );
          throw;
        }
      }

      RecievedPayloads = new ConcurrentQueue<string>();

      RetryTimer = new System.Timers.Timer();
      RetryTimer.Elapsed += new ElapsedEventHandler( OnTimedEvent );
      RetryTimer.Interval = TimeSpan.FromSeconds( 10.0 ).TotalMilliseconds;

      Endpoint = endpoint;
      EndpointUri = new Uri( Endpoint );

      CheckConfiguration();
    }

    /// <summary>
    ///   Validates the parameters used to construct the strategy and logs relevant details.
    /// </summary>
    private void CheckConfiguration() {
      Log.InfoFormat( "Using RESTful API endpoint '{0}'.", Endpoint );
    }

    /// <summary>
    ///   Handle an event that should be treated as an error.
    /// </summary>
    /// <param name="payload">The payload to send with the event.</param>
    public void HandleErrorEvent( byte[] error ) {
      string payload = PreferredFormatter.GetPayloadForError( error );
      SendPayload( payload );
    }

    /// <summary>
    ///   Handle an event that should be treated as a UID being successfully read.
    /// </summary>
    /// <param name="payload">The payload to send with the event.</param>
    public void HandleUidEvent( byte[] uid ) {
      string payload = PreferredFormatter.GetPayload( uid );
      SendPayload( payload );
    }

    /// <summary>
    ///   Starts a Thread for sending a payload to the API endpoint.
    /// </summary>
    /// <param name="payload">The payload to send.</param>
    private void SendPayload( string payload ) {
      if( InsecureSSL ) {
        ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
      }

      RecievedPayloads.Enqueue( payload );

      if( RetryTimer.Enabled == false ) {
        Thread t = new Thread( SendRequest );
        t.Start();
      }
    }

    /// <summary>
    ///   Sends payloads from the ConcurrentQueue.
    /// </summary>
    private void SendRequest() {
      string payload = "";
      if( !RecievedPayloads.IsEmpty && RecievedPayloads.TryDequeue( out payload ) ) {
        HttpWebRequest request = (HttpWebRequest)( HttpWebRequest.Create( EndpointUri ) );
        request.Method = "POST";
        request.ContentType = "application/json";

        // Trying to get the authentication information. If an error occured, nothing will be sent.
        if( null != AuthInfo ) {
          request.Headers.Add( "FM-Auth-Id", AuthInfo.Uuid );
          request.Headers.Add( "FM-Auth-Code", AuthInfo.Code );
        } 

        request.ContentLength = payload.Length;

        try {
          using( Stream requestStream = request.GetRequestStream() ) {
            using( StreamWriter writer = new StreamWriter( requestStream ) ) {
              Log.InfoFormat( "Using JSON Payload: '{0}'", payload );
              writer.Write( payload );
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
          // Stop retry timer if sending was successful.
          RetryTimer.Stop();

        } catch( WebException ex ) {
          Log.ErrorFormat( "Problem communication with RESTful endpoint: {0}", ex.Message );
          
          RecievedPayloads.Enqueue( payload );
          RetryTimer.Start();
        }
      }
    }

    /// <summary>
    /// Reads text from the authentication file.
    /// </summary>
    /// <param name="path">The path of the authentication file.</param>
    /// <returns>String-Array with the Auth-ID (first) and the Auth-Code (second).</returns>
    private Credentials ReadFromAuthFile( string path = "auth.txt" ) {
      string data = "";
      string[] authInformation = new string[2];

      if( string.IsNullOrEmpty( path ) ) {
        throw new ArgumentNullException( "path" );
      }
      if( !File.Exists( path ) ) {
        throw new InvalidOperationException( String.Format( "Auth file {0} does not exist.", path ) );
      }

      // Read data from file.
      data = System.IO.File.ReadAllText( path );
      // Checking read data.
      authInformation = data.Split( ';' );
      if( authInformation.Length != 2 ) {
        throw new Exception( "File wrong formatted." );
      }

      // File was correct formated.
      return new Credentials( authInformation[ 0 ], authInformation[ 1 ] );
    }

    /// <summary>
    /// When Timer elapses, retry sending the recieved Devices.
    /// </summary>
    /// <param name="source"></param>
    /// <param name="e"></param>
    private void OnTimedEvent(object source, ElapsedEventArgs e) {
        Thread t = new Thread( new ThreadStart( SendRequest ) );
        t.Start();
    }
  }
}
