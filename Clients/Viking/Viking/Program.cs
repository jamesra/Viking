using System;
using System.Resources;
using System.Collections.Generic;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using Viking.UI.Forms; 

namespace Viking
{
    static class Program
    {
        static System.IO.StreamWriter DebugLogFile = null;
        public static TextWriter SynchronizedDebugWriter = null;

        public static string AppWebsite = ""; 

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Assembly execAssembly = System.Reflection.Assembly.GetExecutingAssembly();
            CreateDebugListener();

            Trace.WriteLine("Arguments: " + args.ToString(), "Viking");
            Trace.WriteLine("Current Directory: " + System.Environment.CurrentDirectory, "Viking");
            Trace.WriteLine("Application Directory: " + System.IO.Path.GetDirectoryName(execAssembly.CodeBase), "Viking");
#if DEBUG
  //          System.Diagnostics.Debugger.Break();
#endif

            //Change to the executing assemblies directory so we can load modules correctly
            //  System.Environment.CurrentDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            SqlServerTypes.Utilities.LoadNativeAssemblies(AppDomain.CurrentDomain.BaseDirectory);

            int workThreads;
            int portThreads;
            System.Threading.ThreadPool.GetMaxThreads(out workThreads, out portThreads);
            System.Net.ServicePointManager.DefaultConnectionLimit = workThreads;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string website = null;
            
           
            if (args.Length > 0)
            {
                website = args[0]; 
            }
            else
            {
                bool ShowUsage = true;
                if (AppDomain.CurrentDomain.SetupInformation.ActivationArguments != null)
                {
                    string[] ClickOnceArgs = AppDomain.CurrentDomain.SetupInformation.ActivationArguments.ActivationData;
                    if (ClickOnceArgs != null && ClickOnceArgs.Length > 0)
                    {
                        Trace.WriteLine("ActivationArguments: ");
                        foreach (string arg in ClickOnceArgs)
                            Trace.WriteLine(arg, "Viking");

                        string FirstArg = System.Web.HttpUtility.HtmlDecode(ClickOnceArgs[0]);
                        string[] Args = FirstArg.Split('?');

                        Program.AppWebsite = Args[0]; //The website we use to launch Viking
                        Trace.WriteLine("Application Website: " + Program.AppWebsite, "Viking");

                        if (Args.Length == 0)
                        {
                            //Sometimes the only argument passed is the application directory
                            if (!Args[0].ToLower().EndsWith(".application"))
                            {
                                website = Args[1];
                                ShowUsage = false;
                            }
                        }
                        //Parse the arguments
                        else if (Args.Length > 1)
                        {
                            System.Collections.Specialized.NameValueCollection QueryTable = System.Web.HttpUtility.ParseQueryString(Args[1]);

                            if (QueryTable.HasKeys())
                            {
                                UI.State.StartupArguments = QueryTable; 
                                string VolumeValue = QueryTable["Volume"];
                                if (VolumeValue != null)
                                {
                                    website = VolumeValue;
                                    ShowUsage = false;
                                }
                            }
                            else
                            {
                                website = Args[1];
                                ShowUsage = false;
                            }
                        }
                    }
                }
                
                if(ShowUsage)
                {
                    //Launch the viking home page and exit
                    //System.Windows.Forms.MessageBox.Show("No volume definition file was specified.  Loading RC1 by default.  You can pass a website as the first argument to launch a different volume, or select a volume definition from the website: http://connectomes.utah.edu/", "Viking", MessageBoxButtons.OK);
                    //System.Diagnostics.Process WebBrowser = new System.Diagnostics.Process();
                    //WebBrowser.StartInfo.FileName = homepage;
                    //WebBrowser.Start();
                }
            }

            // ----------------------------------------------------------------------------
            //   Logon nag screen, I've only added this tiny code here, and made a logon form in 
            //  Viking/UI/forms


            using (Logon vikingLogon = new Logon("https://connectomes.utah.edu/Viz/Account/Authenticate", website))
            {
                vikingLogon.ShowDialog();

                if (vikingLogon.Result == DialogResult.Cancel)
                { 
                    return;
                }

                website = vikingLogon.VolumeURL;
                UI.State.UserCredentials = vikingLogon.Credentials;

            }

            //Make sure the website includes a file, if it does not then include Volume.VikingXML by default
            Uri WebsiteURI = new Uri(website);
            string path = WebsiteURI.GetComponents(UriComponents.Path, UriFormat.SafeUnescaped);
            if (path.Contains(".") == false)
            {
                if (website.EndsWith("/") == false)
                    website = website + "/";

                website = website + "volume.VikingXML";
            }

            // --------------------------------------------------------------------------------------

            Trace.WriteLine("Loading: " + website, "Viking");

            using (SplashForm Splash = new SplashForm(website))
            {

                Splash.ShowDialog();
                DialogResult splashResult = Splash.Result;

                if (splashResult == DialogResult.Cancel)
                {
                    return;
                }
            }

            Application.Run(new VikingMain());

            if (SynchronizedDebugWriter != null)
                SynchronizedDebugWriter.Close();

            if (DebugLogFile != null)
                DebugLogFile.Close(); 
        }

        [Conditional("DEBUG")]
        private static void CreateDebugListener()
        {
            return; 

            string LogPath = System.Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Viking\\Logs";
            if (!Directory.Exists(LogPath))
                Directory.CreateDirectory(LogPath);

            string FileName = LogPath +"\\" + DateTime.Now.ToString("MM.dd.yyyy HH.mm.ss") + ".log";

            DebugLogFile = System.IO.File.CreateText(FileName);

            TextWriter SynchronizedDebugWriter = StreamWriter.Synchronized(DebugLogFile);

            TextWriterTraceListener Listener = new TextWriterTraceListener(SynchronizedDebugWriter, "Viking Log Listener");

            Trace.Listeners.Add(Listener);
            Debug.Listeners.Add(Listener);
            
            ConsoleTraceListener DebugOutputListener = new ConsoleTraceListener(true);
            Trace.Listeners.Add(DebugOutputListener);
            Debug.Listeners.Add(DebugOutputListener);

            Trace.UseGlobalLock = true; 
        }
    }
}