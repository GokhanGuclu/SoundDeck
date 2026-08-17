using System;
using System.Windows.Forms;
#if !STORE
using Velopack;
#endif

namespace AudioDeviceTrayApp
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
#if !STORE
            // Must run first: handles Velopack install/update/uninstall hooks.
            // The Store build has no Velopack hooks at all — MSIX packages are
            // installed and updated by the Store, and self-updating is not allowed.
            VelopackApp.Build().Run();
#endif

            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
        }
    }
}
