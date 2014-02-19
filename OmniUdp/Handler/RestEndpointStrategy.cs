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

    public StringFormatter PreferredFormatter { get; private set; }

    /// <summary>
    ///   The RESTful API endpoint to use.
    /// </summary>
    protected string Endpoint { get; private set; }

    /// <summary>
    ///   The endpoint expressed in a Uri instance.
    /// </summary>
    private Uri EndpointUri { get; set; }

    /// <summary>
    ///   Construct a new RestEndpointStrategy instance.
    /// </summary>
    /// <param name="endpoint">The API endpoint to connect to.</param>
    /// <param name="formatter">The formatter to use to format the payloads.</param>
    public RestEndpointStrategy( string endpoint, StringFormatter formatter ) {
      PreferredFormatter = formatter;

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
      string payload = PreferredFormatter.GetPayload( error, "::ERROR::" );

      Log.InfoFormat( "Using payload '{0}'.", payload );

      throw new NotImplementedException();
    }

    /// <summary>
    ///   Handle an event that should be treated as a UID being successfully read.
    /// </summary>
    /// <param name="payload">The payload to send with the event.</param>
    public void HandleUidEvent( byte[] uid ) {

      string payload = PreferredFormatter.GetPayload( uid, "::UID::" );

      string JsonData = String.Format(
        "{{ \"data\": \"{0}\" }}",
         payload
        );
      
      HttpWebRequest request = (HttpWebRequest)( HttpWebRequest.Create( EndpointUri ) );
      request.Method = "POST";
      request.ContentType = "application/json";
      request.ContentLength = JsonData.Length;
      try {

        using( Stream requestStream = request.GetRequestStream() ) {
          using( StreamWriter writer = new StreamWriter( requestStream ) ) {
            Log.InfoFormat( "Using JSON Payload: '{0}'", JsonData );
            writer.Write( JsonData );
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
      } catch( WebException ex ) {
        Log.ErrorFormat( "Problem communication with RESTful endpoint: {0}", ex.Message );
      }
    }
  }
}
