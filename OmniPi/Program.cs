using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PCSC;

namespace OmniPi {
  internal class Program {
    private static void Main( string[] args ) {
      ConsoleKeyInfo keyinfo;

      Console.WriteLine( "This program will monitor all SmartCard readers and display all status changes." );
      Console.WriteLine( "Press a key to continue." );

      keyinfo = Console.ReadKey();

      // Retrieve the names of all installed readers.
      SCardContext ctx = new SCardContext();
      ctx.Establish( SCardScope.System );
      string[] readernames = ctx.GetReaders();
      ctx.Release();

      if( readernames == null || readernames.Length == 0 ) {
        Console.WriteLine( "There are currently no readers installed." );
        return;
      }

      // Create a monitor object with its own PC/SC context.
      SCardMonitor monitor = new SCardMonitor(
        new SCardContext(),
        SCardScope.System );

      // Point the callback function(s) to the static defined methods below.
      monitor.CardInserted     += new CardInsertedEvent( CardInserted );
      monitor.CardRemoved      += new CardRemovedEvent( CardRemoved );
      monitor.Initialized      += new CardInitializedEvent( Initialized );
      monitor.StatusChanged    += new StatusChangeEvent( StatusChanged );
      monitor.MonitorException += new MonitorExceptionEvent( MonitorException );

      foreach( string reader in readernames ) {
        Console.WriteLine( "Start monitoring for reader " + reader + "." );
      }

      monitor.Start( readernames );

      // Let the program run until the user presses a key
      keyinfo = Console.ReadKey();
      GC.KeepAlive( keyinfo );

      // Stop monitoring
      monitor.Cancel();
    }

    private static void CardInserted( object sender, CardStatusEventArgs args ) {
      SCardMonitor monitor = (SCardMonitor)sender;

      Console.WriteLine(
        ">> CardInserted Event for reader: "
        + args.ReaderName );
      Console.WriteLine( "   ATR: " + StringAtr( args.Atr ) );
      Console.WriteLine( "   State: " + args.State + "\n" );

      Foo( args.ReaderName );
    }

    private static void CardRemoved( object sender, CardStatusEventArgs args ) {
      SCardMonitor monitor = (SCardMonitor)sender;

      Console.WriteLine(
        ">> CardRemoved Event for reader: "
        + args.ReaderName );
      Console.WriteLine( "   ATR: " + StringAtr( args.Atr ) );
      Console.WriteLine( "   State: " + args.State + "\n" );
    }

    private static void Initialized( object sender, CardStatusEventArgs args ) {
      SCardMonitor monitor = (SCardMonitor)sender;

      Console.WriteLine(
        ">> Initialized Event for reader: "
        + args.ReaderName );
      Console.WriteLine( "   ATR: " + StringAtr( args.Atr ) );
      Console.WriteLine( "   State: " + args.State + "\n" );
    }

    private static void StatusChanged( object sender, StatusChangeEventArgs args ) {
      SCardMonitor monitor = (SCardMonitor)sender;

      Console.WriteLine(
        ">> StatusChanged Event for reader: "
        + args.ReaderName );
      Console.WriteLine( "   ATR: " + StringAtr( args.ATR ) );
      Console.WriteLine(
        "   Last state: " + args.LastState
        + "\n   New state: " + args.NewState + "\n" );
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

    private static void Foo( string readername ) {
      SCardContext ctx = new SCardContext();
      ctx.Establish( SCardScope.System );

      SCardReader RFIDReader = new SCardReader( ctx );
      SCardError rc = RFIDReader.Connect(
        readername,
        SCardShareMode.Shared,
        SCardProtocol.Any );

      if( rc != SCardError.Success ) {
        Console.WriteLine(
          "Unable to connect to RFID card / chip. Error: " +
          SCardHelper.StringifyError( rc ) );
        return;
      }

      // prepare APDU
      byte[] ucByteSend = new byte[] {
        0xFF, // the instruction class
        0xCA, // the instruction code 
        0x00, // parameter to the instruction
        0x00, // parameter to the instruction
        0x00 // size of I/O transfer
      };
      byte[] ucByteReceive = new byte[10];

      Console.Out.WriteLine( "Retrieving the UID .... " );

      rc = RFIDReader.BeginTransaction();
      if( rc != SCardError.Success ) {
        throw new Exception( "Could not begin transaction." );
      }

      SCardPCI ioreq = new SCardPCI(); /* creates an empty object (null).
                                        * IO returned protocol control information.
                                        */
      IntPtr sendPci = SCardPCI.GetPci( RFIDReader.ActiveProtocol );
      rc = RFIDReader.Transmit(
        sendPci, /* Protocol control information, T0, T1 and Raw
                  * are global defined protocol header structures.
                  */
        ucByteSend, /* the actual data to be written to the card */
        ioreq, /* The returned protocol control information */
        ref ucByteReceive );

      if( rc == SCardError.Success ) {
        Console.Write( "Uid: " );
        for( int i = 0; i < ( ucByteReceive.Length ); i++ ) {
          Console.Write( "{0:X2} ", ucByteReceive[ i ] );
        }
        Console.WriteLine( "" );
      } else {
        Console.WriteLine( "Error: " + SCardHelper.StringifyError( rc ) );
      }

      RFIDReader.EndTransaction( SCardReaderDisposition.Leave );
      RFIDReader.Disconnect( SCardReaderDisposition.Reset );

      return;
    }
  }
}