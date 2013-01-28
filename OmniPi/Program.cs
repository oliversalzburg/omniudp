﻿using System;
using System.Net;
using System.Net.Sockets;
using PCSC;

namespace OmniPi {
  /// <summary>
  /// Main application class
  /// </summary>
  internal static class Program {

    /// <summary>
    /// Smart card handling context
    /// </summary>
    private static SCardContext Context { get; set; }

    /// <summary>
    /// Main entry point
    /// </summary>
    private static void Main() {
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

        foreach( string reader in readernames ) {
          Console.WriteLine( "Start monitoring for reader {0}.", reader );
        }
        Console.WriteLine( "Press any key to exit." );

        monitor.Start( readernames );

        // Let the program run until the user presses a key
        Console.ReadKey();

        // Stop monitoring
        monitor.Cancel();
      }
    }

    /// <summary>
    /// Retrieves the UID of a card that is currently connected to the given reader.
    /// </summary>
    /// <param name="readername"></param>
    /// <exception cref="Exception">Could not begin transaction.</exception>
    private static byte[] UidFromConnectedCard( string readername ) {
      SCardReader rfidReader = new SCardReader( Context );
      SCardError resultCode = rfidReader.Connect( readername, SCardShareMode.Shared, SCardProtocol.Any );

      if( resultCode != SCardError.Success ) {
        Console.Error.WriteLine( "Unable to connect to RFID card / chip. Error: " + SCardHelper.StringifyError( resultCode ) );
        return null;
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
        Console.Error.WriteLine( "Error: " + SCardHelper.StringifyError( resultCode ) );
        receiveBuffer = null;
      }

      rfidReader.EndTransaction( SCardReaderDisposition.Leave );
      rfidReader.Disconnect( SCardReaderDisposition.Reset );

      return receiveBuffer;
    }

    /// <summary>
    /// Sends out a UDP broadcast containing the UID
    /// </summary>
    /// <param name="uid">The UID that should be broadcast.</param>
    /// <param name="port">The UDP port to use.</param>
    private static void BroadcastUidEvent( byte[] uid, int port = 30000 ) {
      UdpClient udpClient = new UdpClient {
        EnableBroadcast = true
      };
      udpClient.Send( uid, uid.Length, new IPEndPoint( IPAddress.Broadcast, port ) );
    }

    /// <summary>
    /// Invoked when a new card was inserted into the reader
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    private static void CardInserted( object sender, CardStatusEventArgs args ) {
      byte[] uid = UidFromConnectedCard( args.ReaderName );
      string uidString = BitConverter.ToString( uid );
      Console.Out.WriteLine( uidString );
      BroadcastUidEvent( uid );
    }
  }
}