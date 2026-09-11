//#define USEASPMEMBERSHIP

using CommandLine;
using IdentityModel.Client;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
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
#if DEBUG
            //          System.Diagnostics.Debugger.Break();
#endif

            //Change to the executing assemblies directory so we can load modules correctly
            //  System.Environment.CurrentDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            //System.Data.Entity.SqlServer.SqlProviderServices.SqlServerTypesAssemblyName = "Microsoft.SqlServer.Types, Version=14.0.0.0, Culture=neutral, PublicKeyToken=89845dcd8080cc91";
            //SqlServerTypesUtilities.LoadNativeAssemblies(AppDomain.CurrentDomain.BaseDirectory);

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

            if (args.Length > 0 && args[0].StartsWith("viking://", StringComparison.OrdinalIgnoreCase))
            {
                website = TryOpenFromVikingProtocol(args[0]);
                if (website is null)
                    return;
                if (UI.State.UserBearerToken == null || string.IsNullOrEmpty(UI.State.UserBearerToken.AccessToken))
                {
                    website = ShowLoginWindow(website);
                    if (website is null)
                        return;
                }
            }
            else
            {
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
            }

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

            if (SynchronizedDebugWriter != null)
                SynchronizedDebugWriter.Close();

            if (DebugLogFile != null)
                DebugLogFile.Close();
        }

        /// <summary>
        /// Handles viking://open?code=&amp;volume=&amp;location=&amp;api= from Identity CreateCode / SBFSEM-tools.
        /// Location may be a Location ID or x,y,z[,downsample]. Volume is required for an unambiguous open.
        /// </summary>
        private static string TryOpenFromVikingProtocol(string vikingUri)
        {
            Trace.WriteLine("Protocol launch: " + vikingUri, "Viking");
            Uri uri;
            try
            {
                uri = new Uri(vikingUri);
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Invalid viking:// URI: " + ex.Message, "Viking");
                MessageBox.Show("Could not parse the Viking launch link.", "Viking", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }

            var query = System.Web.HttpUtility.ParseQueryString(uri.Query ?? "");
            UI.State.StartupArguments = query;

            ApplyLocationStartupArguments(query["location"] ?? query["Location"]);

            var volume = query["volume"] ?? query["Volume"];
            if (string.IsNullOrWhiteSpace(volume))
            {
                MessageBox.Show("The Viking launch link did not include a volume. Volume is required.", "Viking", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }

            var code = query["code"] ?? query["Code"];
            var apiBase = query["api"] ?? query["Api"];
            if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(apiBase))
            {
                try
                {
                    if (!TryExchangeLaunchCode(apiBase.Trim(), code.Trim(), out var exchangedVolume, out var identityUrl, out var exchangedVolumeName))
                    {
                        Trace.WriteLine("Launch code exchange failed; falling back to login.", "Viking");
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(exchangedVolume))
                            volume = exchangedVolume;
                        if (!string.IsNullOrWhiteSpace(exchangedVolumeName))
                            UI.State.StartupArguments["volumeName"] = exchangedVolumeName;
                    }

                    if (!string.IsNullOrWhiteSpace(identityUrl))
                        Viking.Tokens.TokenInjector.BearerTokenAuthority = identityUrl;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine("Launch code exchange exception: " + ex.Message, "Viking");
                }
            }

            return volume.Trim();
        }

        private static void ApplyLocationStartupArguments(string location)
        {
            if (string.IsNullOrWhiteSpace(location))
                return;

            location = location.Trim();
            if (long.TryParse(location, out _))
            {
                UI.State.StartupArguments["location"] = location;
                return;
            }

            // x,y,z[,downsample] — same shape as the SBFSEM-tools pick readout
            var parts = location.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
            {
                UI.State.StartupArguments["X"] = parts[0].Trim();
                UI.State.StartupArguments["Y"] = parts[1].Trim();
                UI.State.StartupArguments["Z"] = parts[2].Trim();
                if (parts.Length >= 4)
                    UI.State.StartupArguments["DS"] = parts[3].Trim();
            }
            else
            {
                UI.State.StartupArguments["location"] = location;
            }
        }

        private static bool TryExchangeLaunchCode(string apiBase, string code, out string volumeUrl, out string identityServerUrl, out string volumeName)
        {
            volumeUrl = null;
            identityServerUrl = null;
            volumeName = null;

            var exchangeUrl = apiBase.TrimEnd('/') + "/api/viking/launch-exchange";
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                var body = new StringContent("{\"code\":\"" + code.Replace("\"", "") + "\"}", Encoding.UTF8, "application/json");
                var response = client.PostAsync(exchangeUrl, body).GetAwaiter().GetResult();
                var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                {
                    Trace.WriteLine($"Launch exchange HTTP {(int)response.StatusCode}: {json}", "Viking");
                    return false;
                }

                using (var doc = Newtonsoft.Json.Linq.JObject.Parse(json))
                {
                    string accessToken = (string)(doc["accessToken"] ?? doc["access_token"]);
                    volumeUrl = (string)(doc["volumeUrl"] ?? doc["volume_url"]);
                    identityServerUrl = (string)(doc["identityServerUrl"] ?? doc["identity_server_url"]);
                    volumeName = (string)(doc["volumeName"] ?? doc["volume_name"]);

                    if (string.IsNullOrEmpty(accessToken))
                        return false;

                    var oauthJson = "{\"access_token\":\"" + accessToken.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\",\"token_type\":\"Bearer\"}";
                    var oauthResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new StringContent(oauthJson, Encoding.UTF8, "application/json")
                    };
                    var tokenResponse = ProtocolResponse.FromHttpResponseAsync<TokenResponse>(oauthResponse).GetAwaiter().GetResult();
                    UI.State.UserBearerToken = tokenResponse;
                    Viking.Tokens.TokenInjector.BearerToken = tokenResponse;
                    return true;
                }
            }
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
            return;
            /*
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
            */
        }
    }
}