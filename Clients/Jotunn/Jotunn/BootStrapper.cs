using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Microsoft.Practices.Prism;
using Microsoft.Practices.Prism.Modularity;
using Microsoft.Practices.Prism.MefExtensions;
using Microsoft.Practices.Prism.MefExtensions;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition;
using System.Diagnostics;

namespace Jotunn
{
    class BootStrapper : MefBootstrapper
    {
        SplashScreen splashScreen = null;        

        delegate void FinishInitializeModulesDelegate();

        protected override void ConfigureContainer()
        {
            //We want to call this, but check the Patterns & Practices docs if you modify this function
            base.ConfigureContainer();

            ShellParameterService shellParamService = new ShellParameterService();
            this.Container.ComposeExportedValue<IShellParameters>(shellParamService);
        }
        
        protected override void ConfigureAggregateCatalog()
        {
            base.ConfigureAggregateCatalog();

            string AssemblyDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            AssemblyDir += System.IO.Path.DirectorySeparatorChar + "Modules";

            //Load files in module directory and all sub directories
            
            IEnumerable<string> ModuleDirectories = System.IO.Directory.EnumerateDirectories(AssemblyDir, "*", SearchOption.TopDirectoryOnly);
            foreach (string dir in ModuleDirectories)
            {
                /*
                string[] files = System.IO.Directory.GetFiles(dir, "*.dll");
                foreach(string dll in files)
                {
                    System.Reflection.Assembly a = System.Reflection.Assembly.LoadFile(dll); 
                    AssemblyCatalog assemblyCat = new AssemblyCatalog(a); 
                    AggregateCatalog.Catalogs.Add(assemblyCat); 
                }
                 */

                Trace.WriteLine("Adding aggregate catalog for " + dir); 
                DirectoryCatalog dirCat = new DirectoryCatalog(dir);

                AggregateCatalog.Catalogs.Add(dirCat); 
            }
            
        }
        protected override System.Windows.DependencyObject CreateShell()
        {
            Shell = new MainWindow();

            return Shell; 
        }

        protected override void InitializeShell()
        {
            System.Windows.Application.Current.MainWindow = this.Shell as System.Windows.Window;

            splashScreen = new SplashScreen();
            splashScreen.Show(); 
            this.Container.ComposeExportedValue<System.ComponentModel.BackgroundWorker>("InitializeBackgroundWorker", splashScreen.InitializeWorker); 
        }

        protected void InitializeModulesThreadStart(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            base.InitializeModules();

            FinishInitializeModulesDelegate del = new FinishInitializeModulesDelegate(this.FinishInitializeModules);
            Shell.Dispatcher.BeginInvoke(del, System.Windows.Threading.DispatcherPriority.Background, null);
        }

        protected override void InitializeModules()
        {
            splashScreen.InitializeWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(InitializeModulesThreadStart);
            splashScreen.InitializeWorker.RunWorkerAsync();
        }

        protected void FinishInitializeModules()
        {
            //Hide the splash screen
            splashScreen.Close();

            System.Windows.Application.Current.ShutdownMode = System.Windows.ShutdownMode.OnMainWindowClose;
            System.Windows.Application.Current.MainWindow.Show();

            
        }

    }
}
