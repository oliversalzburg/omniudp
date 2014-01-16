using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using NDesk.Options;
using PCSC;
using log4net;

namespace OmniUdp {
  /// <summary>
  ///   Main application class
  /// </summary>
  internal static class Program {
    /// <summary>
    ///   Commandline options for this application
    /// </summary>
    private static class CommandLineOptions {
      /// <summary>
      ///   The network <see langword="interface" /> from which to broadcast.
      /// </summary>
      internal static string NetworkInterface { get; set; }

      /// <summary>
      ///   The IP address from which to broadcast.
      /// </summary>
      internal static string IPAddress { get; set; }

      /// <summary>
      ///   Use only the loopback device for broadcasting.
      /// </summary>
      internal static bool UseLoopback { get; set; }

      /// <summary>
      ///   The identifier to send with each broadcasted UID.
      /// </summary>
      internal static string Identifier { get; set; }

      /// <summary>
      ///   Encode the UID as an ASCII string before broadcasting.
      /// </summary>
      internal static bool Ascii { get; set; }

      /// <summary>
      ///   Should the command line help be displayed?
      /// </summary>
      internal static bool ShowHelp { get; set; }
    }

    /// <summary>
    ///   The logging interface.
    /// </summary>
    private static readonly ILog Log = LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod().DeclaringType );

    /// <summary>
    ///   <see cref="Program.Main" /> entry point
    /// </summary>
    private static void Main( string[] args ) {
      if( ParseCommandLine( args ) ) {
        return;
      }

      try {
        Console.WindowWidth = 160;
        Console.WindowHeight = 50;
      } catch( IOException ) {
        // Maybe there is no console window (stream redirection)
      }

      // Construct the core application and run it in a separate thread.
      Application app = new Application( CommandLineOptions.NetworkInterface, CommandLineOptions.IPAddress, CommandLineOptions.UseLoopback, CommandLineOptions.Identifier, CommandLineOptions.Ascii );
      Thread applicationThread = new Thread( () => ApplicationHandler( app ) );
      applicationThread.Start();

      // Let the program run until the user presses a key
      Console.Title = "Press ESC to exit.";

      bool exitApplication = false;
      do {
        if( Console.KeyAvailable ) {
          ConsoleKey consoleKey = Console.ReadKey( true ).Key;
          if( ConsoleKey.Escape == consoleKey ) {
            exitApplication = true;
          }

        } else {
          if( app.Destroyed ) {
            break;
          }
          Thread.Sleep( TimeSpan.FromSeconds( 1.0 ) );
        }
        
      } while( !exitApplication );

      // Signal the application thread to exit.
      if( null != app.ExitApplication ) {
        app.ExitApplication.Set();
      }
    }

    /// <summary>
    ///   Handles running the core application.
    /// </summary>
    /// <param name="app">The application to wrap.</param>
    private static void ApplicationHandler( Application app ) {
      try {
        app.Run();
      } catch( PCSCException ex ) {
        Log.Error( "Unable to get readers. Press any key to exit.", ex );
        Console.ReadKey();
      } catch( InvalidOperationException ex ) {
        Log.Error( ex );
        Console.ReadKey();
      } finally {
        app.Destroyed = true;
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
        {"interface=", "The network interface from which to broadcast. By default all interfaces are used.", v => CommandLineOptions.NetworkInterface = v},
        {"ip=", "The IP address from which to broadcast. By default all addresses are used.", v => CommandLineOptions.IPAddress = v},
        {"loopback", "Use only the loopback device. Overrides other options.", v => CommandLineOptions.UseLoopback = true},
        {"identifier=", "The identifier to broadcast with every UID.", v => CommandLineOptions.Identifier = v},
        {"ascii", "Encode the UID as an ASCII string before broadcasting.", v => CommandLineOptions.Ascii = true},
        {"h|?|help", "Shows this help message", v => CommandLineOptions.ShowHelp = v != null}
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

      if( CommandLineOptions.ShowHelp ) {
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