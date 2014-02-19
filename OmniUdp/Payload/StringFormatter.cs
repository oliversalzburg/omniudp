using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net;

namespace OmniUdp.Payload {
  /// <summary>
  ///   Formats a payload as a string.
  /// </summary>
  class StringFormatter {
    /// <summary>
    ///   The logging <see langword="interface" />
    /// </summary>
    private readonly ILog Log = LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod().DeclaringType );

    /// <summary>
    ///   A (usually unique) identification token for the reader connected to this
    ///   OmniUDP instance.
    /// </summary>
    public string Identifier { get; private set; }

    /// <summary>
    ///   Construct a new StringFormatter.
    /// </summary>
    /// <param name="identifier">A (usually unique) identification token for the reader connected to this OmniUDP instance.</param>
    public StringFormatter( string identifier ) {
      Identifier = identifier;
      
      if( null != Identifier && Identifier.Length != 0 ) {
        Log.InfoFormat( "Using identifier '{0}'.", Identifier );
      }
    }

    /// <summary>
    ///   Generate a payload the contains the ASCII formatted UID.
    /// </summary>
    /// <param name="uid">The UID that should be contained in the payload.</param>
    /// <param name="delimiter">The delimiter to use if an identifier has to be put into the payload.</param>
    /// <returns>The formatted payload.</returns>
    public string GetPayload( byte[] uid, string delimiter = "::::" ) {
      // Convert the UID value to a hex string representing the value of the UID.
      string byteString = BitConverter.ToString( uid ).Replace( "-", string.Empty );

      string payload = byteString;
      if( null != Identifier ) {
        payload = String.Format( "{0}{1}{2}", Identifier, delimiter, payload );
      }
      return payload;
    }
  }
}
