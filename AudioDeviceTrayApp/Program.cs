using System;
using System.Windows.Forms;
using Velopack;

namespace AudioDeviceTrayApp
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // Must run first: handles Velopack install/update/uninstall hooks.
            VelopackApp.Build().Run();

            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
        }
    }
}
