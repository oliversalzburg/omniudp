using log4net;
using System;
using System.Text;

namespace OmniUdp.Payload {
    /// <summary>
    ///   Formats a payload an a byte array.
    /// </summary>
    class ByteArrayFormatter {
        /// <summary>
        ///   The logging interface.
        /// </summary>
        private readonly ILog Log = LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod().DeclaringType );

        /// <summary>
        ///   A (usually unique) identification token for the reader connected to this
        ///   OmniUDP instance.
        /// </summary>
        public byte[] Identifier { get; private set; }

        /// <summary>
        ///   Encode the UID as an ASCII string before broadcasting.
        /// </summary>
        public bool Ascii { get; private set; }

        /// <summary>
        ///   Construct a new ByteArrayFormatter.
        /// </summary>
        /// <param name="identifier">A (usually unique) identification token for the reader connected to this OmniUDP instance.</param>
        /// <param name="ascii">Should the UID be encoded as ASCII inside the payload?</param>
        public ByteArrayFormatter( string identifier, bool ascii ) {
            Identifier = ( identifier != null ) ? Encoding.ASCII.GetBytes( identifier ) : null;
            Ascii = ascii;
        }

        /// <summary>
        ///   Generate a payload that contains the given UID in a byte array.
        /// </summary>
        /// <param name="uid">The UID to put into the payload.</param>
        /// <param name="delimiter">A delimiter to use if additional information is put into the payload.</param>
        /// <returns>The formatted payload.</returns>
        public byte[] GetPayload( byte[] uid ) {
            byte[] payload = GeneratePayload( uid, "::UID::" );
            return payload;
        }

        /// <summary>
        ///   Generate a payload for an error code.
        /// </summary>
        /// <param name="error">The error code to put into the payload.</param>
        /// <returns>The formatted payload.</returns>
        public byte[] GetPayloadForError( byte[] error ) {
            byte[] payload = GeneratePayload( error, "::ERROR::" );
            return payload;
        }

        /// <summary>
        ///   Constructs the complete payload.
        /// </summary>
        /// <param name="data">The data to put into the payload.</param>
        /// <param name="delimiter">An optional delimiter to put between the data and the identfier for this instance.</param>
        /// <returns>The formatted payload.</returns>
        private byte[] GeneratePayload( byte[] data, string delimiter ) {
            if( Ascii ) {
                // Convert the UID value to a hex string representing the value of the UID.
                string byteString = BitConverter.ToString( data ).Replace( "-", string.Empty );
                // Then convert the string back to a byte array.
                data = Encoding.ASCII.GetBytes( byteString );
            }

            byte[] payload = data;

            if( null != Identifier ) {
                byte[] delimiterBytes = Encoding.ASCII.GetBytes( delimiter ?? "::::" );
                payload = BufferUtils.Combine( Identifier, delimiterBytes, data );
            }
            return payload;
        }
    }
}
