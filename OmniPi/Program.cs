using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PCSC;

namespace OmniPi {
  internal class Program {

    private static SCardContext Context { get; set; }

    private static void Main( string[] args ) {
      
      // Retrieve the names of all installed readers.
      using( Context = new SCardContext() ) {
        Context.Establish( SCardScope.System );
        string[] readernames = Context.GetReaders();

        if( null == readernames || 0 == readernames.Length ) {
          Console.Error.WriteLine( "There are currently no readers installed." );
          return;
        }

        // Create a monitor object with its own PC/SC context.
        SCardMonitor monitor = new SCardMonitor( new SCardContext(), SCardScope.System );

        // Point the callback function(s) to the static defined methods below.
        monitor.CardInserted += CardInserted;
        /*
        monitor.CardRemoved      += new CardRemovedEvent( CardRemoved );
        monitor.Initialized      += new CardInitializedEvent( Initialized );
        monitor.StatusChanged    += new StatusChangeEvent( StatusChanged );
        monitor.MonitorException += new MonitorExceptionEvent( MonitorException );
        */

        foreach( string reader in readernames ) {
          Console.WriteLine( "Start monitoring for reader " + reader + "." );
        }
        Console.WriteLine( "Press any key to exit." );

        monitor.Start( readernames );

        // Let the program run until the user presses a key
        Console.ReadKey();

        // Stop monitoring
        monitor.Cancel();
      }
    }

    private static void CardInserted( object sender, CardStatusEventArgs args ) {
      SCardMonitor monitor = (SCardMonitor)sender;
      /*
      Console.WriteLine( ">> CardInserted Event for reader: " + args.ReaderName );
      Console.WriteLine( "   ATR: {0}", StringAtr( args.Atr ) );
      Console.WriteLine( "   State: {0}\n", args.State );
      */
      PrintUID( args.ReaderName );
    }

    private static void CardRemoved( object sender, CardStatusEventArgs args ) {
      SCardMonitor monitor = (SCardMonitor)sender;

      Console.WriteLine( ">> CardRemoved Event for reader: " + args.ReaderName );
      Console.WriteLine( "   ATR: {0}", StringAtr( args.Atr ) );
      Console.WriteLine( "   State: {0}\n", args.State );
    }

    private static void Initialized( object sender, CardStatusEventArgs args ) {
      SCardMonitor monitor = (SCardMonitor)sender;

      Console.WriteLine( ">> Initialized Event for reader: " + args.ReaderName );
      Console.WriteLine( "   ATR: {0}", StringAtr( args.Atr ) );
      Console.WriteLine( "   State: {0}\n", args.State );
    }

    private static void StatusChanged( object sender, StatusChangeEventArgs args ) {
      SCardMonitor monitor = (SCardMonitor)sender;

      Console.WriteLine( ">> StatusChanged Event for reader: " + args.ReaderName );
      Console.WriteLine( "   ATR: {0}", StringAtr( args.ATR ) );
      Console.WriteLine( "   Last state: {0}\n   New state: {1}\n", args.LastState, args.NewState );
    }

    private static void MonitorException( object sender, PCSCException ex ) {
      Console.WriteLine( "Monitor exited due an error:" );
      Console.WriteLine( SCardHelper.StringifyError( ex.SCardError ) );
    }

    /// <summary>
    /// Helper function that translates a byte array into an hex-encoded ATR
    /// string.
    /// </summary>
    /// <param name="atr">Contains the SmartCard ATR.</param>
    /// <returns></returns>
    private static string StringAtr( byte[] atr ) {
      if( atr == null ) {
        return null;
      }

      StringBuilder sb = new StringBuilder();
      foreach( byte b in atr ) {
        sb.AppendFormat( "{0:X2}", b );
      }

      return sb.ToString();
    }

    /// <summary>
    /// Prints the UID of a card that is currently connected to the given reader.
    /// </summary>
    /// <param name="readername"></param>
    /// <exception cref="Exception">Could not begin transaction.</exception>
    private static void PrintUID( string readername ) {
      SCardReader rfidReader = new SCardReader( Context );
      SCardError resultCode = rfidReader.Connect( readername, SCardShareMode.Shared, SCardProtocol.Any );

      if( resultCode != SCardError.Success ) {
        Console.Error.WriteLine( "Unable to connect to RFID card / chip. Error: " + SCardHelper.StringifyError( resultCode ) );
        return;
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

      SCardPCI ioreq = new SCardPCI(); /* creates an empty object (null).
                                        * IO returned protocol control information.
                                        */

      IntPtr protocolControlInformation = SCardPCI.GetPci( rfidReader.ActiveProtocol );
      resultCode = rfidReader.Transmit(
        protocolControlInformation, /* Protocol control information, T0, T1 and Raw
                  * are global defined protocol header structures.
                  */
        payload, /* the actual data to be written to the card */
        ioreq, /* The returned protocol control information */
        ref receiveBuffer );

      if( resultCode == SCardError.Success ) {
        Console.Write( "UID: " );
        for( int i = 0; i < ( receiveBuffer.Length ); i++ ) {
          Console.Write( "{0:X2} ", receiveBuffer[ i ] );
        }
        Console.WriteLine();

      } else {
        Console.Error.WriteLine( "Error: " + SCardHelper.StringifyError( resultCode ) );
      }

      rfidReader.EndTransaction( SCardReaderDisposition.Leave );
      rfidReader.Disconnect( SCardReaderDisposition.Reset );
    }
  }
}