using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
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
    /// The authentication file path.
    /// </summary>
    private string authFile { get; set; }

    /// <summary>
    ///   Construct a new RestEndpointStrategy instance.
    /// </summary>
    /// <param name="endpoint">The API endpoint to connect to.</param>
    /// <param name="formatter">The formatter to use to format the payloads.</param>
    /// <param name="authFilePath">The location of the authentication file.</param>
    public RestEndpointStrategy( string endpoint, string authFilePath, JsonFormatter formatter ) {
      PreferredFormatter = formatter;

      Endpoint = endpoint;
      EndpointUri = new Uri( Endpoint );

      // Looking for authentication file
      if( authFilePath == null ) { 
        authFile = "auth.txt";
      } else {
        authFile = authFilePath;
      }

      CheckConfiguration();
    }

    /// <summary>
    ///   Validates the parameters used to construct the strategy and logs relevant details.
    /// </summary>
    private void CheckConfiguration() {
      Log.InfoFormat( "Using RESTful API endpoint '{0}'.", Endpoint );
      Log.InfoFormat( "Authentication file path: '{0}'.", authFile );
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
    ///   Sends a payload to the API endpoint.
    /// </summary>
    /// <param name="payload">The payload to send.</param>
    private void SendPayload( string payload ) {
      
      HttpWebRequest request = (HttpWebRequest)( HttpWebRequest.Create( EndpointUri ) );
      request.Method = "POST";
      request.ContentType = "application/json";
      request.ContentLength = payload.Length;

      // Authentication information
      string[] tokens = readFromAuthenticationFile();
      if( tokens.Length == 2 ) {
        request.Headers.Add("FM-Auth-Id", tokens[0]);
        request.Headers.Add("FM-Auth-Code", tokens[1]);
      } else {
        Log.ErrorFormat ( "Authentication failed! File or path broken." );
        return;
      }

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
      /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
      System.Threading.Thread.Sleep(60000);
              string responseFromServer = reader.ReadToEnd();
              // Display the content.
              Log.Info( responseFromServer );
            }
          }
        }
      } catch( WebException ex ) {
        Log.ErrorFormat( "Problem communication with RESTful endpoint: {0}", ex.Message );
      }
    }

    /// <summary>
    /// Reads the Auth-Id and Auth-Code from the authentication file.
    /// </summary>
    /// <returns>String array with the Auth-Id (first element) and Auth-Code (second element).</returns>
    private string[] readFromAuthenticationFile() {
      string auth = "";
      try {
        auth = System.IO.File.ReadAllText(authFile);
      }  catch( FileNotFoundException e ) {
        Log.ErrorFormat("The file was not found: {0}", e.Message);
      } catch( DirectoryNotFoundException e ) {
        Log.ErrorFormat("The given directory was not found: {0}", e.Message);
      } catch( Exception e) {
        Log.ErrorFormat("An error occured: {0}", e.Message);
      }
      return auth.Split(';');
    }
  }
}
