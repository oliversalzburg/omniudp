using System;
using System.Text;
using System.Threading;
using PCSC;
using log4net;

namespace OmniUdp {
  internal class Application {
    /// <summary>
    ///   The logging <see langword="interface" />
    /// </summary>
    private readonly ILog Log = LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod().DeclaringType );

    /// <summary>
    ///   Smart card handling context
    /// </summary>
    private SCardContext Context { get; set; }

    /// <summary>
    ///   Set this signal to notify the application to exit.
    /// </summary>
    public ManualResetEvent ExitApplication;

    /// <summary>
    ///   The network interface from which to broadcast.
    /// </summary>
    public string NetworkInterface { get; private set; }

    /// <summary>
    ///   The IP address from which to broadcast.
    /// </summary>
    public string IPAddress { get; private set; }

    /// <summary>
    ///   Use only the loopback device for broadcasting.
    /// </summary>
    public bool UseLoopback { get; private set; }

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
    ///   Construct the application.
    /// </summary>
    /// <param name="networkInterface">
    ///   The network <see langword="interface" /> from which to broadcast.
    /// </param>
    /// <param name="ipAddress">The IP address from which to broadcast.</param>
    /// <param name="useLoopback">
    ///   Use only the loopback device for broadcasting.
    /// </param>
    /// <param name="identifier">A (usually unique) identification token for the reader connected to this OmniUDP instance.</param>
    /// <param name="ascii"></param>
    public Application( string networkInterface, string ipAddress, bool useLoopback, string identifier, bool ascii ) {
      UseLoopback = useLoopback;
      NetworkInterface = networkInterface;
      IPAddress = ipAddress;
      Identifier = (identifier != null) ? Encoding.ASCII.GetBytes(identifier) : null;
      Ascii = ascii;
    }

    /// <summary>
    ///   <see cref="Run" /> the application.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///   There are currently no readers installed.
    /// </exception>
    public void Run() {
      if( !UseLoopback ) {
        if( null != NetworkInterface ) {
          if( !IpHelper.DoesInterfaceExist( NetworkInterface ) ) {
            Console.Error.WriteLine( "The given interface '{0}' does not exist on the local system.", NetworkInterface );
            return;
          }
          Log.InfoFormat( "Broadcasts limited to interface '{0}'.", NetworkInterface );
        }
        if( null != IPAddress ) {
          Log.InfoFormat( "Broadcasts limited to address '{0}'.", IPAddress );
        }
      } else {
        Log.InfoFormat( "Sending UIDs only on the loopback device!" );
        IPAddress = "127.0.0.1";
      }

      if( null != Identifier && Identifier.Length != 0 ) {
        Log.InfoFormat( "Using identifier '{0}'.", Encoding.ASCII.GetString( Identifier ) );
      }

      // Retrieve the names of all installed readers.
      using( Context = new SCardContext() ) {
        Context.Establish( SCardScope.System );

        string[] readernames = Context.GetReaders();

        if( null == readernames || 0 == readernames.Length ) {
          throw new InvalidOperationException( "There are currently no readers installed." );
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
    ///   Sends out a UDP broadcast containing the UID
    /// </summary>
    /// <param name="uid">The UID that should be broadcast.</param>
    /// <param name="port">The UDP port to use.</param>
    private void BroadcastUidEvent( byte[] uid, int port = 30000 ) {
      byte[] payload = GetPayload( uid );

      Log.InfoFormat( "Using payload '{0}'.", BitConverter.ToString( payload ).Replace( "-", string.Empty ) );

      if( UseLoopback ) {
        UidBroadcaster.BroadcastLoopback( payload, port );
      } else {
        UidBroadcaster.BroadcastUid( payload, port, IPAddress, NetworkInterface );
      }
    }

    /// <summary>
    ///   Constructs the complete payload
    /// </summary>
    /// <param name="uid"></param>
    /// <returns></returns>
    private byte[] GetPayload( byte[] uid ) {
      if( Ascii ) {
        // Convert the UID value to a hex string representing the value of the UID.
        string byteString = BitConverter.ToString( uid ).Replace( "-", string.Empty );
        // Then convert the string back to a byte array.
        uid = Encoding.ASCII.GetBytes( byteString );
      }

      byte[] payload = uid;

      if( null != Identifier ) {
        byte[] delimiter = Encoding.ASCII.GetBytes( "::UID::" );
        payload = BufferUtils.Combine( Identifier, delimiter, uid );
      }
      return payload;
    }

    /// <summary>
    ///   Invoked when a new card was inserted into the reader
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    private void CardInserted( object sender, CardStatusEventArgs args ) {
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
      }
    }
  }
}