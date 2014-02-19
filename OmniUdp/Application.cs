using System;
using System.Text;
using System.Threading;
using PCSC;
using log4net;
using OmniUdp.Handler;

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
    ///   A (usually unique) identification token for the reader connected to this
    ///   OmniUDP instance.
    /// </summary>
    public byte[] Identifier { get; private set; }

    /// <summary>
    ///   Encode the UID as an ASCII string before broadcasting.
    /// </summary>
    public bool Ascii { get; private set; }

    /// <summary>
    ///   Was the application instance destroyed?
    /// </summary>
    public bool Destroyed { get; set; }

    /// <summary>
    ///   Construct the application.
    /// </summary>
    /// <param name="identifier">A (usually unique) identification token for the reader connected to this OmniUDP instance.</param>
    /// <param name="ascii">Should the UID be encoded as ASCII inside the payload?</param>
    public Application( string identifier, bool ascii, IEventHandlingStrategy eventHandlingStrategy ) {
      // Event Handling Strategy cannot be null
      if( null == eventHandlingStrategy ) {
        throw new ArgumentNullException( "eventHandlingStrategy" );
      }

      Identifier = ( identifier != null ) ? Encoding.ASCII.GetBytes( identifier ) : null;
      Ascii = ascii;
      ApplicationEventHandler = eventHandlingStrategy;

      Destroyed = false;
    }

    /// <summary>
    ///   <see cref="Run" /> the application.
    /// </summary>
    public void Run() {
      if( null != Identifier && Identifier.Length != 0 ) {
        Log.InfoFormat( "Using identifier '{0}'.", Encoding.ASCII.GetString( Identifier ) );
      }

      ExecuteContext();
    }

    /// <summary>
    ///   Create UID reader context and wait for events.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///   There are currently no readers installed.
    /// </exception>
    protected virtual void ExecuteContext() {
      // Retrieve the names of all installed readers.
      using( Context = new SCardContext() ) {
        Context.Establish( SCardScope.System );

        string[] readernames = null;
        try {
          readernames = Context.GetReaders();
        } catch( PCSCException ) {} finally {
          if( null == readernames || 0 == readernames.Length ) {
            throw new InvalidOperationException( "There are currently no readers installed." );
          }
        }

        // Create a monitor object with its own PC/SC context.
        SCardMonitor monitor = new SCardMonitor( new SCardContext(), SCardScope.System );

        // Point the callback function(s) to the static defined methods below.
        monitor.CardInserted += CardInserted;

        foreach( string reader in readernames ) {
          Log.InfoFormat( "Start monitoring for reader '{0}'.", reader );
        }

        monitor.Start( readernames );

        // Wait for the parent application to signal us to exit.
        ExitApplication = new ManualResetEvent( false );
        ExitApplication.WaitOne();

        // Stop monitoring
        monitor.Cancel();
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
      byte[] receiveBuffer = new byte[10];

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
      byte[] payload = GetPayload( uid, "::UID::" );

      Log.InfoFormat( "Using payload '{0}'.", BitConverter.ToString( payload ).Replace( "-", string.Empty ) );

      ApplicationEventHandler.HandleUidEvent( payload );
    }

    /// <summary>
    ///   Sends out a UDP broadcast containing an error code.
    /// </summary>
    /// <param name="errorCode"></param>
    /// <param name="port"></param>
    protected void BroadcastErrorEvent( byte[] errorCode ) {
      byte[] payload = GetPayload( errorCode, "::ERROR::" );

      Log.InfoFormat( "Using payload '{0}'.", BitConverter.ToString( payload ).Replace( "-", string.Empty ) );

      ApplicationEventHandler.HandleErrorEvent( payload );
    }

    /// <summary>
    ///   Constructs the complete payload.
    /// </summary>
    /// <param name="data">The data to put into the payload.</param>
    /// <param name="delimiter">An optional delimiter to put between the data and the identfier for this instance.</param>
    /// <returns></returns>
    private byte[] GetPayload( byte[] data, string delimiter = "::::" ) {
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
        byte[] shortUid = new byte[4];
        Array.Copy( uid, shortUid, 4 );

        string uidString = BitConverter.ToString( shortUid ).Replace( "-", string.Empty );
        Log.InfoFormat( "Read UID '{0}' from '{1}'.", uidString, args.ReaderName );
        BroadcastUidEvent( shortUid );
      } catch( Exception ex ) {
        Log.Error( ex.Message );
        BroadcastErrorEvent( new byte[] {0} );
      }
    }

    /// <summary>
    ///   Handle keyboard input from the main application.
    /// </summary>
    /// <param name="key">The key information that was recorded.</param>
    public virtual void HandleKeyboardInput( ConsoleKeyInfo key ) {}
  }
}