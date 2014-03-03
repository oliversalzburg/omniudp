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
    ///   The logging <see langword="interface" />
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
    ///   The path of the authentication file.
    /// </summary>
    private string[] authInf;

    /// <summary>
    ///   PriorityQueue for storing payload.
    /// </summary>
    private static ConcurrentQueue<string> recievedDevices;
    
    /// <summary>
    ///   Timer for retrying to send the payloads
    /// </summary>
    private static System.Timers.Timer retryTimer;

    /// <summary>
    ///   Construct a new RestEndpointStrategy instance.
    /// </summary>
    /// <param name="endpoint">The API endpoint to connect to.</param>
    /// <param name="insecureSSL">Ignore SSL certificate errors.</param>
    /// <param name="authFile">The location of the authentication file. Default: working directory.</param>
    /// <param name="formatter">The formatter to use to format the payloads.</param>
    public RestEndpointStrategy( string endpoint, bool insecureSSL, string authFile, JsonFormatter formatter ) {
      PreferredFormatter = formatter;
      InsecureSSL = insecureSSL;

      try {
        authInf = ReadFromAuthFile( authFile );
      } catch( Exception ex ) {
        Log.ErrorFormat( "Problem with authentication file: {0}", ex.Message );
        throw;
      }

      recievedDevices = new ConcurrentQueue<string>();

      retryTimer = new System.Timers.Timer();
      retryTimer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
      retryTimer.Interval = 10000;

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

      recievedDevices.Enqueue( payload );

       if( retryTimer.Enabled == false ) {
        Thread t = new Thread(new ThreadStart(sendRequest));
        t.Start();
      }
    }

    /// <summary>
    ///   Sends payloads from the ConcurrentQueue.
    /// </summary>
    private void sendRequest() {
      string payload = "";

      if( !recievedDevices.IsEmpty ) {
      recievedDevices.TryDequeue(out payload);
        HttpWebRequest request = (HttpWebRequest)( HttpWebRequest.Create( EndpointUri ) );
        request.Method = "POST";
        request.ContentType = "application/json";

        // Trying to get the authentication information. If an error occured, nothing will be sent.
        try {
          request.Headers.Add( "FM-Auth-Id", authInf[0] );
          request.Headers.Add( "FM-Auth-Code", authInf[1] );
        } catch( Exception ) {
          return;
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
            //Log.Info( response.StatusDescription );
            using( Stream dataStream = response.GetResponseStream() ) {
              using( StreamReader reader = new StreamReader( dataStream ) ) {
                // Read the content. 
                string responseFromServer = reader.ReadToEnd();
                // Display the content.
                Log.Info( responseFromServer );
              }
            }
          }
          // stop retry timer if sending was successful.
          retryTimer.Stop();
        } catch( WebException ex ) {
          Log.ErrorFormat( "Problem communication with RESTful endpoint: {0}", ex.Message );
          recievedDevices.Enqueue( payload );
          retryTimer.Start();
        }
      }
    }

    /// <summary>
    /// Reads text from the authentication file.
    /// </summary>
    /// <param name="path">The path of the authentication file.</param>
    /// <returns>String-Array with the Auth-ID (first) and the Auth-Code (second).</returns>
    private string[] ReadFromAuthFile( string path = "auth.txt" ) {
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
      return authInformation;
    }

    /// <summary>
    /// When Timer elapses, retry sending the recieved Devices.
    /// </summary>
    /// <param name="source"></param>
    /// <param name="e"></param>
    private void OnTimedEvent(object source, ElapsedEventArgs e) {
        Thread t = new Thread(new ThreadStart(sendRequest));
        t.Start();
    }
  }
}
