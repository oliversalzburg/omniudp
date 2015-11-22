using System;

namespace OmniUdp.Payload {
    /// <summary>
    ///   Formats a payload as JSON.
    /// </summary>
    class JsonFormatter : StringFormatter {
        /// <summary>
        ///   Construct a new JsonFormatter instance.
        /// </summary>
        /// <param name="ascii">Should the UID be converted to an ASCII string instead of a number?</param>
        /// <param name="identifier">A (usually unique) identification token for the reader connected to this OmniUDP instance.</param>
        public JsonFormatter( bool ascii, string identifier ) : base( ascii, identifier ) { }

        /// <summary>
        ///   Generate a payload the contains the ASCII formatted UID.
        /// </summary>
        /// <param name="uid">The UID that should be contained in the payload.</param>
        /// <returns>The formatted payload.</returns>
        override public string GetPayload( byte[] uid ) {
            // Convert the UID value to a hex string representing the value of the UID.
            string byteString = ( Ascii ) ? BitConverter.ToString( uid ).Replace( "-", string.Empty ) : BitConverter.ToInt32( uid, 0 ).ToString();

            string payload = String.Format(
              "{{ \"uid\": \"{0}\", \"identifier\": \"{1}\" }}",
               byteString, Identifier
              );

            return payload;
        }

        /// <summary>
        ///   Generate a payload for an error.
        /// </summary>
        /// <param name="error">The error code to transmit.</param>
        /// <returns>The formatted payload.</returns>
        public override string GetPayloadForError( byte[] error ) {
            // Convert the UID value to a hex string representing the value of the UID.
            string byteString = ( Ascii ) ? BitConverter.ToString( error ).Replace( "-", string.Empty ) : BitConverter.ToInt32( error, 0 ).ToString();

            string payload = String.Format(
              "{{ \"error\": \"{0}\", \"identifier\": \"{1}\" }}",
               byteString, Identifier
              );

            return payload;
        }
    }
}
