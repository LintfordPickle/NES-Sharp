using System;
using System.IO;
using System.Windows.Forms;

namespace NESSharpWinForm
{
    public class Program
    {
        public const string ApplicationName = "NESSharp";

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Refactor to allow for easier loading of ROMS (WinForms: open file dialog)
            MainForm mainForm = new MainForm();

            // Load the ROMS from the command line parameters from the application data directory
            string applicationDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + 
                                              Path.DirectorySeparatorChar +
                                              ApplicationName +
                                              Path.DirectorySeparatorChar;

            Directory.CreateDirectory(applicationDataDirectory);

            // Optionally pass the filename of a ROM to load on startup
            if(args.Length > 0 && args[0].GetType() == typeof(String))
                mainForm.LoadROM(applicationDataDirectory + args[0]);
            
            Application.Run(mainForm);

        }

    }

}
