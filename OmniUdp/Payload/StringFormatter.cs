using System;
using System.Diagnostics;
using log4net;

namespace OmniUdp.Payload {
	/// <summary>
	///     Formats a payload as a string.
	/// </summary>
	internal class StringFormatter {
		/// <summary>
		///     The logging interface.
		/// </summary>
		private readonly ILog Log = LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod().DeclaringType );

		/// <summary>
		///     Should the UID be converted to an ASCII string instead of a number?
		/// </summary>
		public bool Ascii { get; private set; }

		/// <summary>
		///     A (usually unique) identification token for the reader connected to this
		///     OmniUDP instance.
		/// </summary>
		public string Identifier { get; private set; }

		/// <summary>
		///     Construct a new StringFormatter.
		/// </summary>
		/// <param name="ascii">Should the UID be converted to an ASCII string instead of a number?</param>
		/// <param name="identifier">A (usually unique) identification token for the reader connected to this OmniUDP instance.</param>
		public StringFormatter( bool ascii, string identifier ) {
			Ascii = ascii;
			Identifier = identifier;
		}

		/// <summary>
		///     Construct a payload the contains the formatted UID.
		/// </summary>
		/// <param name="uid">The UID that should be contained in the payload.</param>
		/// <returns>The formatted payload.</returns>
		public virtual string GetPayload( byte[] uid ) {
			// Convert the UID value to a hex string representing the value of the UID.
			string byteString = ( Ascii ) ? BitConverter.ToString( uid ).Replace( "-", string.Empty ) : BitConverter.ToInt32( uid, 0 ).ToString();

			return byteString;
		}

		/// <summary>
		///     Construct a payload for an error.
		/// </summary>
		/// <param name="error">The error code.</param>
		/// <returns>The formatted payload.</returns>
		public virtual string GetPayloadForError( byte[] error ) {
			return string.Empty;
		}
	}
}