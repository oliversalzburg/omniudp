using System;
using System.IO;
using System.IO.Pipes;
using log4net;
using OmniUdp.Payload;

namespace OmniUdp.Handler {
	/// <summary>
	///     An event handling strategy that sends events through an anonymous pipe stream.
	/// </summary>
	internal class PipeStreamStrategy : IEventHandlingStrategy, IDisposable {
		/// <summary>
		///     The logging <see langword="interface" />
		/// </summary>
		private readonly ILog Log = LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod().DeclaringType );

		public StringFormatter PreferredFormatter { get; private set; }

		/// <summary>
		///     The handle of the pipe stream through which we are sending.
		/// </summary>
		public string StreamHandle { get; private set; }

		/// <summary>
		///     The pipe through which we are sending the events.
		/// </summary>
		public PipeStream OutputStream { get; private set; }

		private StreamWriter _streamWriter;

		/// <summary>
		///     Construct a new PipeStreamStrategy instance.
		/// </summary>
		/// <param name="streamHandle">The handle of the stream to which we should send.</param>
		/// <param name="formatter">The formatter to use to format the payloads.</param>
		public PipeStreamStrategy( string streamHandle, StringFormatter formatter ) {
			Log.Info( "Using pipe stream strategy." );

			PreferredFormatter = formatter;

			StreamHandle = streamHandle;
			OutputStream = new AnonymousPipeClientStream( PipeDirection.Out, StreamHandle );
			_streamWriter = new StreamWriter( OutputStream ) {
				AutoFlush = true
			};
		}

		/// <summary>
		///     Handle an event that should be treated as an error.
		/// </summary>
		/// <param name="error">The error that happened.</param>
		public void HandleErrorEvent( byte[] error ) {
			string payload = PreferredFormatter.GetPayloadForError( error );

			Log.InfoFormat( "Using payload '{0}'.", payload );

			_streamWriter.WriteLine( payload );
		}

		/// <summary>
		///     Handle an event that should be treated as a UID being successfully read.
		/// </summary>
		/// <param name="uid">The UID that was read.</param>
		public void HandleUidEvent( byte[] uid ) {
			string payload = PreferredFormatter.GetPayload( uid );

			Log.InfoFormat( "Using payload '{0}'.", payload );

			_streamWriter.WriteLine( payload );
		}

		public void Dispose() {
			if( _streamWriter != null ) {
				_streamWriter.Dispose();
				_streamWriter = null;
			}
		}
	}
}