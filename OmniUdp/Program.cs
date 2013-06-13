using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NDesk.Options;
using PCSC;
using log4net;

namespace OmniUdp {
  /// <summary>
  ///   Main application class
  /// </summary>
  internal static class Program {
    /// <summary>
    ///   The logging <see langword="interface" />
    /// </summary>
    private static readonly ILog Log = LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod().DeclaringType );

    /// <summary>
    ///   Smart card handling context
    /// </summary>
    private static SCardContext Context { get; set; }

    /// <summary>
    ///   The network <see langword="interface" /> from which to broadcast.
    /// </summary>
    private static string NetworkInterface { get; set; }

    /// <summary>
    ///   The IP address from which to broadcast.
    /// </summary>
    private static string IPAddress { get; set; }

    /// <summary>
    ///   Use only the loopback device for broadcasting.
    /// </summary>
    private static bool UseLoopback { get; set; }

    /// <summary>
    ///   Should the command line help be displayed?
    /// </summary>
    private static bool ShowHelp { get; set; }

    /// <summary>
    ///   <see cref="Program.Main" /> entry point
    /// </summary>
    private static void Main( string[] args ) {
      if( ParseCommandLine( args ) ) {
        return;
      }

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
        IPAddress = "127.0.0.1";
      }

      // Retrieve the names of all installed readers.
      using( Context = new SCardContext() ) {
        Context.Establish( SCardScope.System );

        string[] readernames;
        try {
          readernames = Context.GetReaders();
        } catch( PCSCException ex ) {
          Log.Error( "Unable to get readers. Press any key to exit.", ex );
          Console.ReadKey();
          return;
        }

        if( null == readernames || 0 == readernames.Length ) {
          Log.Error( "There are currently no readers installed." );
          return;
        }

        // Create a monitor object with its own PC/SC context.
        SCardMonitor monitor = new SCardMonitor( new SCardContext(), SCardScope.System );

        // Point the callback function(s) to the static defined methods below.
        monitor.CardInserted += CardInserted;

        foreach( string reader in readernames ) {
          Log.InfoFormat( "Start monitoring for reader {0}.", reader );
        }
        Console.Title = "Press any key to exit.";

        monitor.Start( readernames );

        // Let the program run until the user presses a key
        Console.ReadKey();

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
    private static byte[] UidFromConnectedCard( string readername ) {
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
    private static void BroadcastUidEvent( byte[] uid, int port = 30000 ) {
      if( UseLoopback ) {
        UidBroadcaster.BroadcastLoopback( uid, port );
      } else {
        UidBroadcaster.BroadcastUid( uid, port, IPAddress, NetworkInterface );
      }
    }

    /// <summary>
    ///   Invoked when a new card was inserted into the reader
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    private static void CardInserted( object sender, CardStatusEventArgs args ) {
      try {
        byte[] uid = UidFromConnectedCard( args.ReaderName );

        // We only care about the first 4 bytes
        byte[] shortUid = new byte[4];
        Array.Copy( uid, shortUid, 4 );

        string uidString = BitConverter.ToString( shortUid );

        Log.Info( uidString );
        BroadcastUidEvent( shortUid );
      } catch( Exception ex ) {
        Log.Error( ex.Message );
      }
    }

    /// <summary>
    ///   Parses command line parameters.
    /// </summary>
    /// <param name="args">
    ///   The command line parameters passed to the program.
    /// </param>
    /// <returns>
    ///   <see langword="true" /> if the application should exit.
    /// </returns>
    private static bool ParseCommandLine( IEnumerable<string> args ) {
      OptionSet options = new OptionSet {
        {"interface=", "The network interface from which to broadcast. By default all interfaces are used.", v => NetworkInterface = v},
        {"ip=", "The IP address from which to broadcast. By default all addresses are used.", v => IPAddress = v},
        {"loopback", "Use only the loopback device. Overrides other options.", v => UseLoopback = true},
        {"h|?|help", "Shows this help message", v => ShowHelp = v != null}
      };

      try {
        options.Parse( args );
      } catch( OptionException ex ) {
        Console.Write( "{0}:", new FileInfo( Assembly.GetExecutingAssembly().Location ).Name );
        Console.WriteLine( ex.Message );
        Console.WriteLine(
          "Try '{0} --help' for more information.", new FileInfo( Assembly.GetExecutingAssembly().Location ).Name );
        return true;
      }

      if( ShowHelp ) {
        Console.WriteLine( "Usage: {0} [OPTIONS]", new FileInfo( Assembly.GetExecutingAssembly().Location ).Name );
        Console.WriteLine();
        Console.WriteLine( "Options:" );
        Console.WriteLine();
        options.WriteOptionDescriptions( Console.Out );
        return true;
      }

      return false;
    }
  }
}