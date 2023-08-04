using System;
using System.Windows.Forms;
using CommandLine;

namespace JarInfectionScanner {
  internal static class Program {
    internal static Options parsedOptions;
    internal class Options {
      [Option('s', "scan", Required = false, HelpText = "Automatically starts the scanning process as soon as the program starts up.")]
      public bool Scan {
        get; set;
      }
      [Option('p', "path", Required = false, HelpText = "Prefill the path to be scanned.")]
      public string Path {
        get; set;
      }
    }
    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args) {
      parsedOptions = Parser.Default.ParseArguments<Options>(args).Value;
      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);
      Application.Run(new Form1());
    }
  }
}
