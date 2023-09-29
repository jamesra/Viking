#define USEASPMEMBERSHIP

using CommandLine;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Globalization;
using Viking.UI.Forms;


namespace Viking
{
    class CommandLineOptions
    {
        [Option('v', "Volume", Required = true, HelpText = "URL of VolumeXML file")]
        public string VolumeURL { get; set; }

        [Option('u', "user", Default = "Anonymous", Required = false, HelpText = "URL of VolumeXML file")]
        public string Username { get; set; }

        [Option('p', "pwd", Default = "connectome", Required = false, HelpText = "URL of VolumeXML file")]
        public string Password { get; set; }
         
        //[Option('c', "position", Required = false, HelpText= "Position to start viewer at")]
        
    }

    static class Program
    {
        static System.IO.StreamWriter DebugLogFile = null;
        public static TextWriter SynchronizedDebugWriter = null;

        public static string AppWebsite = "";

        /// <summary>
        /// From Stack Overflow: http://stackoverflow.com/questions/8301587/how-to-detect-xna-version-at-runtime
        /// </summary>
        /// <param name="ok"></param>
        /// <returns></returns>
        public static bool XNAFrameworkInstalled(string baseKeyName)
        {
            Microsoft.Win32.RegistryKey FrameworkKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(baseKeyName);

            if (FrameworkKey == null)
            {
                return false;
            }

            if (FrameworkKey.GetValueKind("Installed") != Microsoft.Win32.RegistryValueKind.DWord)
            {
                return false;
            }

            int installedValue = Convert.ToInt32(FrameworkKey.GetValue("Installed"));

            return installedValue != 0;
        }

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
            Trace.WriteLine("Application Directory: " + execAssembly.Location, "Viking");

            var culture = CultureInfo.CreateSpecificCulture("en-US");
            CultureInfo.DefaultThreadCurrentCulture = culture;

#if DEBUG
            //          System.Diagnostics.Debugger.Break();
#endif

            //Change to the executing assemblies directory so we can load modules correctly
            //  System.Environment.CurrentDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            System.Data.Entity.SqlServer.SqlProviderServices.SqlServerTypesAssemblyName = "Microsoft.SqlServer.Types, Version=14.0.0.0, Culture=neutral, PublicKeyToken=89845dcd8080cc91";
            SqlServerTypesUtilities.LoadNativeAssemblies(AppDomain.CurrentDomain.BaseDirectory);

            try
            {
                MathNet.Numerics.Control.UseNativeMKL();
            }
            catch (Exception e)
            {
                Trace.WriteLine("Unable to load Native MKL library.  Exception text:\n" + e.Message);
            }

            System.Threading.ThreadPool.GetMaxThreads(out int workThreads, out int portThreads);
            System.Net.ServicePointManager.DefaultConnectionLimit = workThreads;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string website = null;

            if (!(XNAFrameworkInstalled(@"SOFTWARE\Microsoft\XNA\Game Studio\v4.0") ||
                 XNAFrameworkInstalled(@"SOFTWARE\Wow6432Node\Microsoft\XNA\Game Studio\v4.0") ||
                 XNAFrameworkInstalled(@"SOFTWARE\Microsoft\XNA\Framework\v4.0") ||
                 XNAFrameworkInstalled(@"SOFTWARE\Wow6432Node\Microsoft\XNA\Framework\v4.0")))
            {
                MessageBox.Show("XNA framework 4.0 does not appear to be installed.  Viking will display a blank gray screen without it.  Please check the documentation or internet for links to the XNA Framework 4.0 Redistributable.", "Missing XNA 4.0 Redistributable", MessageBoxButtons.OK);
            }

            var options = CommandLine.Parser.Default.ParseArguments<CommandLineOptions>(args);

            /*
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

                if (ShowUsage)
                {
                    //Launch the viking home page and exit
                    //System.Windows.Forms.MessageBox.Show("No volume definition file was specified.  Loading RC1 by default.  You can pass a website as the first argument to launch a different volume, or select a volume definition from the website: http://connectomes.utah.edu/", "Viking", MessageBoxButtons.OK);
                    //System.Diagnostics.Process WebBrowser = new System.Diagnostics.Process();
                    //WebBrowser.StartInfo.FileName = homepage;
                    //WebBrowser.Start();
                } 
            }
            */
            // ----------------------------------------------------------------------------
            //   Logon nag screen, I've only added this tiny code here, and made a logon form in 
            //  Viking/UI/forms

            options.WithParsed((o) =>
            {
                website = o.VolumeURL;
                TryBypassSplash(o);
            });

            options.WithNotParsed((o) => { website = ShowLoginWindow(website); });

            //Close the program if no website is configured
            if (website is null)
                return;
            /*
#if !USEASPMEMBERSHIP
            using (Logon vikingLogon = new Logon(website))
            {
                vikingLogon.ShowDialog();

                if (vikingLogon.Result == DialogResult.Cancel)
                { 
                    return;
                }

                website = vikingLogon.VolumeURL;

                UI.State.UserBearerToken = vikingLogon.BearerToken;
                UI.State.UserCredentials = vikingLogon.Credentials;

                Viking.Tokens.TokenInjector.BearerToken = vikingLogon.BearerToken;
                Viking.Tokens.TokenInjector.BearerTokenAuthority = "https://identity.connectomes.utah.edu";
            }
#else
            using (LogonASPMembership vikingLogon = new LogonASPMembership(website))
            {
                vikingLogon.ShowDialog();

                if (vikingLogon.Result == DialogResult.Cancel)
                {
                    return;
                }

                website = vikingLogon.VolumeURL;
                UI.State.UserCredentials = vikingLogon.Credentials;
            }
#endif 
            */

            //Make sure the website includes a file, if it does not then include Volume.VikingXML by default
            website = Viking.Common.Util.AppendDefaultVolumeFilenameIfMissing(website);

            // --------------------------------------------------------------------------------------

            Trace.WriteLine($"Loading: {website}", "Viking");

            /*

            using (SplashForm Splash = new SplashForm(website))
            {
                UI.State.volume = new Viking.VolumeModel.Volume(this.VolumePath, UI.State.CachePath, progressReporter);
                Splash.ShowDialog();
                DialogResult splashResult = Splash.Result;

                if (splashResult == DialogResult.Cancel)
                {
                    return;
                }
            }
            */

            VikingApplicationContext context = new VikingApplicationContext(website);
            context.Initialize(website);
            Application.Run(context);

            SynchronizedDebugWriter?.Close();
            DebugLogFile?.Close();
        }

        private static string TryBypassSplash(CommandLineOptions options)
        {
            string VolumeURL; 
            if (options.VolumeURL != null && options.Username != null && options.Password != null)
            {
                UI.State.UserCredentials = new System.Net.NetworkCredential(options.Username, options.Password);
                VolumeURL = options.VolumeURL;
            }
            else
            {
                VolumeURL = ShowLoginWindow(options.VolumeURL, options.Username, options.Password); 
            }

            return VolumeURL;
        }

        private static string ShowLoginWindow(string VolumePath, string username=null, string password=null)
        {

#if !USEASPMEMBERSHIP
            using (Logon vikingLogon = new Logon(VolumePath))
            {
                vikingLogon.ShowDialog();

                if (vikingLogon.Result == DialogResult.Cancel)
                { 
                    return null;
                } 

                UI.State.UserBearerToken = vikingLogon.BearerToken;
                UI.State.UserCredentials = vikingLogon.Credentials;

                Viking.Tokens.TokenInjector.BearerToken = vikingLogon.BearerToken;
                Viking.Tokens.TokenInjector.BearerTokenAuthority = vikingLogon.AuthenticationServiceURL;

                return vikingLogon.VolumeURL;
            }
#else
            using (LogonASPMembership vikingLogon = new LogonASPMembership(VolumePath, username, password))
            {
                vikingLogon.ShowDialog();

                if (vikingLogon.Result == DialogResult.Cancel)
                {
                    return null;
                }
                 
                UI.State.UserCredentials = vikingLogon.Credentials;
                return vikingLogon.VolumeURL;
            }
#endif
        } 


        [Conditional("DEBUG")]
        private static void CreateDebugListener()
        { 
            string LogPath = System.Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Viking\\Logs";
            if (!Directory.Exists(LogPath))
                Directory.CreateDirectory(LogPath);

            string FileName = LogPath +"\\" + DateTime.Now.ToString("MM.dd.yyyy HH.mm.ss") + ".log";

            DebugLogFile = System.IO.File.CreateText(FileName);

            TextWriter SynchronizedDebugWriter = StreamWriter.Synchronized(DebugLogFile);

            TextWriterTraceListener Listener = new TextWriterTraceListener(SynchronizedDebugWriter, "Viking Log Listener"); 
            Trace.Listeners.Add(Listener);
            Debug.Listeners.Add(Listener);
            
            /*ConsoleTraceListener DebugOutputListener = new ConsoleTraceListener(true);
            Trace.Listeners.Add(DebugOutputListener);
            Debug.Listeners.Add(DebugOutputListener);*/

            Trace.UseGlobalLock = true;
            //CultureInfo[] cultures = { new CultureInfo("en-US") };
            //CultureInfo provider = cultures[0];
            TestCultureNumberParsing();
        }

        private static void TestCultureNumberParsing()
        { 
            NumberFormatInfo current1 = CultureInfo.CurrentCulture.NumberFormat;
             
            Debug.WriteLine("Decimal separator: " + current1.NumberDecimalSeparator);
            Debug.WriteLine("Group separator:   " + current1.NumberGroupSeparator);

            string[] testStrings = {"3,800000000000e+01",
                                    "3.800000000000e+01",
                                    "3.80e+01",
                                    "38"};

            foreach(string number in testStrings)
            {
                try
                {
                    Debug.WriteLine($"Parsing {number} yields {System.Convert.ToDouble(number)}");
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Could not parse {number}\n{e}");
                }
            }
        }
    }
}