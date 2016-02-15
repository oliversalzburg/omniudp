using log4net;
using OmniUdp.Handler;
using PCSC;
using System;
using System.Diagnostics.Eventing.Reader;
using System.Threading;
using Timer = System.Timers.Timer;

namespace OmniUdp {
    /// <summary>
    ///   The actual application logic.
    /// </summary>
    internal class Application {
        /// <summary>
        ///   The event handler for the application.
        /// </summary>
        protected IEventHandlingStrategy ApplicationEventHandler { get; set; }

        /// <summary>
        ///   The logging <see langword="interface" />
        /// </summary>
        private readonly ILog Log = LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod().DeclaringType );

        /// <summary>
        ///   Smart card handling context
        /// </summary>
        protected SCardContext Context { get; set; }

        /// <summary>
        ///   Set this signal to notify the application to exit.
        /// </summary>
        public ManualResetEvent ExitApplication;

        /// <summary>
        ///   Was the application instance destroyed?
        /// </summary>
        public bool Destroyed { get; set; }

        /// <summary>
        /// Used to periodically test for newly connected readers.
        /// </summary>
        private System.Timers.Timer RetryTimer { get; set; }

        /// <summary>
        ///   Construct the application.
        /// </summary>
        public Application( IEventHandlingStrategy eventHandlingStrategy ) {
            // Event Handling Strategy cannot be null
            if( null == eventHandlingStrategy ) {
                throw new ArgumentNullException( "eventHandlingStrategy" );
            }

            ApplicationEventHandler = eventHandlingStrategy;

            Destroyed = false;
        }

        /// <summary>
        ///   <see cref="Run" /> the application.
        /// </summary>
        public void Run() {
            ExecuteContext();
        }

        /// <summary>
        ///   Create UID reader context and wait for events.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        ///   There are currently no readers installed.
        /// </exception>
        protected virtual void ExecuteContext() {
	        try {
		        // Retrieve the names of all installed readers.
		        using( Context = new SCardContext() ) {
			        Context.Establish( SCardScope.System );

			        string[] readernames = null;
			        try {
				        Log.Info( "Attempting to retrieve connected readers..." );
				        readernames = Context.GetReaders();

			        } catch( PCSCException ) { }

			        SCardMonitor monitor = null;

			        if( null == readernames || 0 == readernames.Length ) {
				        //throw new InvalidOperationException( "There are currently no readers installed." );
				        Log.Warn( "There are currently no readers installed. Re-attempting in 10 seconds." );

				        if( null == RetryTimer ) {
					        RetryTimer = new Timer( TimeSpan.FromSeconds( 10 ).TotalMilliseconds );
					        RetryTimer.Elapsed += ( e, args ) => { ExecuteContext(); };
					        RetryTimer.Start();
				        }

			        } else {
				        if( null != RetryTimer ) {
					        RetryTimer.Stop();
					        RetryTimer.Dispose();
				        }

				        // Create a monitor object with its own PC/SC context.
				        monitor = new SCardMonitor( new SCardContext(), SCardScope.System );

				        // Point the callback function(s) to the static defined methods below.
				        monitor.CardInserted += CardInserted;

				        foreach( string reader in readernames ) {
					        Log.InfoFormat( "Start monitoring for reader '{0}'.", reader );
				        }

				        monitor.Start( readernames );
			        }

			        // Wait for the parent application to signal us to exit.
			        if( null == ExitApplication ) {
				        ExitApplication = new ManualResetEvent( false );
			        }
			        ExitApplication.WaitOne();

			        // Stop monitoring
			        if( null != monitor ) {
				        monitor.Cancel();
				        monitor.Dispose();
				        monitor = null;
			        }
		        }

	        } catch( PCSCException pcscException ) {
		        Log.Error( "Failed to run application", pcscException );
	        }
        }

        /// <summary>
        ///   Retrieves the UID of a card that is currently connected to the given
        ///   reader.
        /// </summary>
        /// <param name="readername"></param>
        /// <exception cref="Exception">Could not begin transaction.</exception>
        private byte[] UidFromConnectedCard( string readername ) {
            SCardReader rfidReader = new SCardReader( Context );
            SCardError resultCode = rfidReader.Connect( readername, SCardShareMode.Shared, SCardProtocol.Any );

            if( resultCode != SCardError.Success ) {
                throw new Exception( "Unable to connect to RFID card / chip. Error: " + SCardHelper.StringifyError( resultCode ) );
            }

            // prepare APDU
            byte[] payload = new byte[] {
        0xFF, // the instruction class
        0xCA, // the instruction code 
        0x00, // parameter to the instruction
        0x00, // parameter to the instruction
        0x00 // size of I/O transfer
      };
            byte[] receiveBuffer = new byte[ 10 ];

            resultCode = rfidReader.BeginTransaction();
            if( resultCode != SCardError.Success ) {
                throw new Exception( "Could not begin transaction." );
            }

            SCardPCI ioreq = new SCardPCI();

            IntPtr protocolControlInformation = SCardPCI.GetPci( rfidReader.ActiveProtocol );
            resultCode = rfidReader.Transmit( protocolControlInformation, payload, ioreq, ref receiveBuffer );

            if( resultCode != SCardError.Success ) {
                Log.Error( SCardHelper.StringifyError( resultCode ) );
                receiveBuffer = null;
            }

            rfidReader.EndTransaction( SCardReaderDisposition.Leave );
            rfidReader.Disconnect( SCardReaderDisposition.Reset );

            return receiveBuffer;
        }

        /// <summary>
        ///   Sends out a UDP broadcast containing the UID.
        /// </summary>
        /// <param name="uid">The UID that should be broadcast.</param>
        /// <param name="port">The UDP port to use.</param>
        protected void BroadcastUidEvent( byte[] uid ) {
            ApplicationEventHandler.HandleUidEvent( uid );
        }

        /// <summary>
        ///   Sends out a UDP broadcast containing an error code.
        /// </summary>
        /// <param name="errorCode"></param>
        /// <param name="port"></param>
        protected void BroadcastErrorEvent( byte[] errorCode ) {
            ApplicationEventHandler.HandleErrorEvent( errorCode );
        }

        /// <summary>
        ///   Invoked when a new card was inserted into the reader
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        protected void CardInserted( object sender, CardStatusEventArgs args ) {
            Log.Info( "Card detected." );
            try {
                byte[] uid = UidFromConnectedCard( args.ReaderName );

                // We only care about the first 4 bytes
                byte[] shortUid = new byte[ 4 ];
                Array.Copy( uid, shortUid, 4 );
                string uidString = BitConverter.ToString( shortUid ).Replace( "-", string.Empty );
                Log.InfoFormat( "Read UID '{0}' from '{1}'.", uidString, args.ReaderName );
                BroadcastUidEvent( shortUid );
            } catch( Exception ex ) {
                Log.Error( ex.Message );
                BroadcastErrorEvent( new byte[] { 0 } );
            }
        }

        /// <summary>
        ///   Handle keyboard input from the main application.
        /// </summary>
        /// <param name="key">The key information that was recorded.</param>
        public virtual void HandleKeyboardInput( ConsoleKeyInfo key ) { }
    }
}