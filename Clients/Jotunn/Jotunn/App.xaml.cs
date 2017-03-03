using System;
using System.Reflection; 
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Windows;
using System.Diagnostics;
using System.Xml.Linq;
 

namespace Jotunn
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Global.Initialize();

            this.ShutdownMode = ShutdownMode.OnMainWindowClose;

#if (DEBUG)
            RunInDebugMode();
#else
            RunInReleaseMode();
#endif
        } 

        private static void RunInDebugMode()
        {
            BootStrapper bootstrapper = new Jotunn.BootStrapper();
            bootstrapper.Run();
        }

        private static void RunInReleaseMode()
        {
            try
            {
                Jotunn.BootStrapper bootstrapper = new Jotunn.BootStrapper();
                bootstrapper.Run();
            }
            catch (Exception )
            {

            }
        }

	}
}