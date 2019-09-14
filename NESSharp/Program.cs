using NESSharp.Hardware;
using System;
using System.Windows.Forms;

namespace NESSharp
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Create new instance of the main form (for display and input capture).
            MainForm mainForm = new MainForm();

            // Create an instance of the NES Core (contains 'hardware' components for NES)
            NESCore nesCore = new NESCore();

            mainForm.DoOnInput += nesCore.OnInput;
            mainForm.DoOnUpdate += nesCore.OnUpdate;
            mainForm.DoOnRender += nesCore.OnRender;

            // TODO: Tie grpahics, input and sound together
            // The NES Core will have to expose the relevant parts of the system memory
            // which can be fed to the MainForm.

            // Start the application
            Application.Run(mainForm);
        }
    }
}
