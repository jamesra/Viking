using System;
using System.Windows;


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