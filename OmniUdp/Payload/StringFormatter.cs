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
    ///   The logging interface.
    /// </summary>
    private readonly ILog Log = LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod().DeclaringType );

    /// <summary>
    ///   Should the UID be converted to an ASCII string instead of a number?
    /// </summary>
    public bool Ascii { get; private set; }

    /// <summary>
    ///   A (usually unique) identification token for the reader connected to this
    ///   OmniUDP instance.
    /// </summary>
    public string Identifier { get; private set; }

    /// <summary>
    ///   Construct a new StringFormatter.
    /// </summary>
    /// <param name="ascii">Should the UID be converted to an ASCII string instead of a number?</param>
    /// <param name="identifier">A (usually unique) identification token for the reader connected to this OmniUDP instance.</param>
    public StringFormatter( bool ascii, string identifier ) {
      Ascii = ascii;
      Identifier = identifier;
    }

    /// <summary>
    ///   Construct a payload the contains the formatted UID.
    /// </summary>
    /// <param name="uid">The UID that should be contained in the payload.</param>
    /// <returns>The formatted payload.</returns>
    public virtual string GetPayload( byte[] uid ) {
      return GetPayload( uid, "::UID::" );
    }

    /// <summary>
    ///   Construct a payload for an error.
    /// </summary>
    /// <param name="error">The error code.</param>
    /// <returns>The formatted payload.</returns>
    public virtual string GetPayloadForError( byte[] error ) {
      return GetPayload( error, "::ERROR::" );
    }

    /// <summary>
    ///   Generate a payload the contains the ASCII formatted UID.
    /// </summary>
    /// <param name="uid">The UID that should be contained in the payload.</param>
    /// <param name="delimiter">The delimiter to use if an identifier has to be put into the payload.</param>
    /// <returns>The formatted payload.</returns>
    private string GetPayload( byte[] uid, string delimiter = "::::" ) {
      // Convert the UID value to a hex string representing the value of the UID.
      string byteString = (Ascii) ? BitConverter.ToString( uid ).Replace( "-", string.Empty ) : BitConverter.ToInt32( uid, 0 ).ToString();

      string payload = byteString;
      if( null != Identifier ) {
        payload = String.Format( "{0}{1}{2}", Identifier, delimiter, payload );
      }
      return payload;
    }
  }
}
