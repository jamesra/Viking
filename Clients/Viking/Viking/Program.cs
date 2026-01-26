// #define USEASPMEMBERSHIP

using CommandLine;
using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using CommandLine.Text;
using Viking.UI.Forms;
using VikingCoreResources = Viking.Properties.Resources;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Viking.UI.WPF;
using Viking.Services;
using Velopack;


namespace Viking
{
    class CommandLineOptions
    {
        [Option('v', "Volume", Required = true, HelpText = "URL of VolumeXML file")]
        public string VolumeURL { get; set; } = string.Empty;

        [Option('u', "user", Default = "Anonymous", Required = false, HelpText = "URL of VolumeXML file")]
        public string Username { get; set; } = string.Empty;

        [Option('p', "pwd", Default = "connectome", Required = false, HelpText = "URL of VolumeXML file")]
        public string Password { get; set; } = string.Empty;

        //[Option('c', "position", Required = false, HelpText= "Position to start viewer at")] 
    }

    static class Program
    {
        static System.IO.StreamWriter? DebugLogFile = null;
        public static TextWriter? SynchronizedDebugWriter = null;

        public static string AppWebsite = "";

        /// <summary>
        /// From Stack Overflow: http://stackoverflow.com/questions/8301587/how-to-detect-xna-version-at-runtime
        /// </summary>
        /// <param name="ok"></param>
        /// <returns></returns>
        public static bool XNAFrameworkInstalled(string baseKeyName)
        {
            Microsoft.Win32.RegistryKey FrameworkKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(baseKeyName);

            if (FrameworkKey is null)
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
        /// Check the known registry entries for an XNA install
        /// </summary>
        /// <returns></returns>
        public static bool XNAFrameworkInstalled()
        {
            return XNAFrameworkInstalled(@"SOFTWARE\Microsoft\XNA\Game Studio\v4.0") ||
             XNAFrameworkInstalled(@"SOFTWARE\Wow6432Node\Microsoft\XNA\Game Studio\v4.0") ||
             XNAFrameworkInstalled(@"SOFTWARE\Microsoft\XNA\Framework\v4.0") ||
             XNAFrameworkInstalled(@"SOFTWARE\Wow6432Node\Microsoft\XNA\Framework\v4.0");
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        { 
            // Velopack must run first to handle setup/uninstall/update hooks
            // Note: Velopack version 0.0.1298 doesn't have OnFirstRun/OnAfterUpdate hooks
            // Version will be displayed in About dialog from Assembly.GetEntryAssembly().GetName().Version
            VelopackApp.Build().Run();
            
            // Upgrade settings from previous versions (preserves user settings across updates)
            SettingsManager.UpgradeSettingsIfNeeded();

            //ConfigureHighDpiMode();
            Application.EnableVisualStyles();

            Assembly execAssembly = System.Reflection.Assembly.GetExecutingAssembly();
            CreateDebugListener();

            Trace.WriteLine("Arguments: " + args.ToString(), "Viking");
            Trace.WriteLine("Current Directory: " + System.Environment.CurrentDirectory, "Viking");
            Trace.WriteLine("Application Directory: " + execAssembly.Location, "Viking");

            CultureInfo culture = CultureInfo.CreateSpecificCulture("en-US");
            CultureInfo.DefaultThreadCurrentCulture = culture;

#if DEBUG
            //          System.Diagnostics.Debugger.Break();
#endif

            //Change to the executing assemblies directory so we can load modules correctly
            //  System.Environment.CurrentDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            System.Data.Entity.SqlServer.SqlProviderServices.SqlServerTypesAssemblyName = "Microsoft.SqlServer.Types, Version=14.0.0.0, Culture=neutral, PublicKeyToken=89845dcd8080cc91";
            SqlServerTypesLoader.Loader.LoadNativeAssemblies(AppDomain.CurrentDomain.BaseDirectory);


            System.Threading.ThreadPool.GetMaxThreads(out int workThreads, out int portThreads);
            System.Net.ServicePointManager.DefaultConnectionLimit = workThreads;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Check for updates before showing login dialog
            // This runs on the UI thread with proper message pumping
            UpdateService.CheckForUpdatesAtStartup();

            ApplicationSettings? appSettings = null;

            var options = CommandLine.Parser.Default.ParseArguments<CommandLineOptions>(args);

            options.WithParsed(o => appSettings = TryBypassSplash(o)).WithNotParsed(errors =>
            {
                // Create a new help text with error information
                HelpText errorHelpText = HelpText.AutoBuild(options);
                errorHelpText.AddPreOptionsLine("ERROR: Unable to parse command line arguments.");
                errorHelpText.AddPreOptionsLine("The following errors occurred:");

                foreach (var error in errors)
                {
                    errorHelpText.AddPreOptionsLine($"  {error}");
                }

                errorHelpText.AddPreOptionsLine("");
                Console.WriteLine(errorHelpText);

                // Show login window as fallback
                appSettings = ShowLoginWindow(null, null, null);
            });

            //Close the program if no settings were provided or the volume is missing
            if (appSettings is null || string.IsNullOrWhiteSpace(appSettings.VolumeURL))
                return;

            //Make sure the volume URL includes a file, if it does not then include Volume.VikingXML by default
            appSettings.VolumeURL = Viking.Common.Util.AppendDefaultVolumeFilenameIfMissing(appSettings.VolumeURL);
            Trace.WriteLine($"Loading: {appSettings.VolumeURL}", "Viking");

            // Populate annotation URL asynchronously (fire-and-forget, errors are logged)
            var populateTask = Task.Run(async () =>
                await PopulateAnnotationUrlFromVolumeAsync(appSettings).ConfigureAwait(false));
            populateTask.GetAwaiter().GetResult();

            // --------------------------------------------------------------------------------------

            VikingApplicationContext context = new(appSettings);
            context.Initialize();
            Application.Run(context);


            // Shutdown WPF Application instance if it exists
            System.Windows.Application.Current?.Shutdown();

            SynchronizedDebugWriter?.Close();
            DebugLogFile?.Close();
        }

        private static ApplicationSettings TryBypassSplash(CommandLineOptions options)
        {
            if (!string.IsNullOrWhiteSpace(options.VolumeURL) &&
                !string.IsNullOrWhiteSpace(options.Username) &&
                !string.IsNullOrWhiteSpace(options.Password))
            {
                UI.State.UserCredentials = new System.Net.NetworkCredential(options.Username, options.Password);
                return new ApplicationSettings
                {
                    VolumeURL = options.VolumeURL
                };
            }

            return ShowLoginWindow(options.VolumeURL, options.Username, options.Password);
        }


        /// <summary>
        /// Initialize the Mathnet Numerics lib
        /// </summary>
        private static void InitializeMathnet()
        {
            int numMathProcs = Environment.ProcessorCount - 1;
            if (numMathProcs < 1)
                numMathProcs = 1;

            MathNet.Numerics.Control.MaxDegreeOfParallelism = numMathProcs;
            bool MKLSuccess = Geometry.Global.TryUseNativeMKL();
            if (MKLSuccess)
                Console.WriteLine("Success loading MKL Library");
            else
            {
                Console.WriteLine("Unable to load MKL Libarry");
            }
        }

        private static ApplicationSettings? ShowLoginWindow(string? volumePath, string? username = null, string? password = null)
        {
            // Use new WPF-based login system
            LoginWindow wpfLoginWindow = new();
            ApplicationSettings appSettings = new();
            var settings = Viking.Properties.Settings.Default;

            // Provide recent volume URLs from settings
            wpfLoginWindow.RecentVolumeUrls = settings.VolumeURLs;
            wpfLoginWindow.RecentSegmentationServiceUrls = settings.SegmentationServiceUrls;

            var initialSegmentationUrl = settings.LastSegmentationServiceUrl;
            wpfLoginWindow.InitialSegmentationServiceUrl = string.IsNullOrWhiteSpace(initialSegmentationUrl) ? null : initialSegmentationUrl;

            var result = wpfLoginWindow.ShowDialog();

            if (result != true)
            {
                return null;
            }

            UI.State.UserBearerToken = wpfLoginWindow.BearerToken;
            UI.State.UserCredentials = wpfLoginWindow.Credentials;

            if (wpfLoginWindow.BearerToken != null)
            {
                Viking.Tokens.TokenInjector.BearerToken = wpfLoginWindow.BearerToken;
                var identityServerUrl = settings.IdentityServerURL;
                if (!string.IsNullOrEmpty(identityServerUrl))
                {
                    Viking.Tokens.TokenInjector.BearerTokenAuthority = identityServerUrl;
                }
            }

            bool settingsChanged = false;

            // Add selected volume to recent volumes
            appSettings.VolumeURL = wpfLoginWindow.VolumeURL;

            if (!string.IsNullOrEmpty(appSettings.VolumeURL))
            {
                if (settings.VolumeURLs is null)
                {
                    settings.VolumeURLs = [];
                    settingsChanged = true;
                }

                // Remove duplicate entries by URL (checking both "URL" and "URL|Name" formats)
                var volumeName = wpfLoginWindow.VolumeName;
                List<string> entriesToRemove = [];
                foreach (string entry in settings.VolumeURLs)
                {
                    if (string.IsNullOrWhiteSpace(entry))
                        continue;

                    // Parse entry to extract URL
                    var parts = entry.Split(['|'], 2);
                    var entryUrl = parts[0];

                    // If URLs match, mark for removal
                    if (string.Equals(entryUrl, appSettings.VolumeURL, StringComparison.OrdinalIgnoreCase))
                    {
                        entriesToRemove.Add(entry);
                    }
                }

                foreach (var entry in entriesToRemove)
                {
                    settings.VolumeURLs.Remove(entry);
                }

                // Format entry: "URL|Name" or just "URL" if name is null/empty
                string entryToAdd = !string.IsNullOrWhiteSpace(volumeName) ? $"{appSettings.VolumeURL}|{volumeName}" : appSettings.VolumeURL;

                // Insert at top of list (most recent)
                settings.VolumeURLs.Insert(0, entryToAdd);
                settingsChanged = true;

                System.Diagnostics.Trace.WriteLine($"[Viking] Saved volume to recent volumes: {entryToAdd}");
            }

            // Persist segmentation service selection
            var selectedSegmentationUrl = wpfLoginWindow.SegmentationServiceUrl;
            appSettings.SegmentationURL = selectedSegmentationUrl;
            settings.LastSegmentationServiceUrl = selectedSegmentationUrl ?? string.Empty;
            settingsChanged = true;

            if (!string.IsNullOrWhiteSpace(selectedSegmentationUrl))
            {
                var history = settings.SegmentationServiceUrls ?? [];
                if (history.Contains(selectedSegmentationUrl))
                {
                    history.Remove(selectedSegmentationUrl);
                }
                history.Insert(0, selectedSegmentationUrl);
                settings.SegmentationServiceUrls = history;
                settingsChanged = true;
            }

            if (settingsChanged)
            {
                settings.Save();
            }

            return appSettings;
        }

        private static async Task PopulateAnnotationUrlFromVolumeAsync(ApplicationSettings appSettings)
        {
            if (appSettings is null ||
                !string.IsNullOrWhiteSpace(appSettings.AnnotationURL) ||
                string.IsNullOrWhiteSpace(appSettings.VolumeURL))
            {
                return;
            }

            try
            {
                var volumeDocument = await Viking.VolumeModel.Volume.LoadXDocumentAsync(appSettings.VolumeURL, CancellationToken.None, UI.State.UserCredentials).ConfigureAwait(false);

                var volumeElement = Viking.VolumeModel.Volume.GetVolumeElement(volumeDocument);
                if (volumeElement is null)
                {
                    return;
                }

                var mappingElement = volumeElement
                    .Elements()
                    .FirstOrDefault(e => string.Equals(e.Name.LocalName, "VolumeToEndpoint", StringComparison.OrdinalIgnoreCase));

                var endpoint = GetAttributeValueCaseInsensitive(mappingElement, "Endpoint");
                if (!string.IsNullOrWhiteSpace(endpoint))
                {
                    appSettings.AnnotationURL = endpoint;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Viking] Failed to derive annotation endpoint from volume '{appSettings?.VolumeURL}': {ex.Message}");
            }
        }

        private static string? GetAttributeValueCaseInsensitive(XElement? element, string attributeName)
        {
            return element?
                .Attributes()
                .FirstOrDefault(a => string.Equals(a.Name.LocalName, attributeName, StringComparison.OrdinalIgnoreCase))
                ?.Value;
        }

        [Conditional("DEBUG")]
        private static void CreateDebugListener()
        {
            string LogPath = System.Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Viking\\Logs";
            if (!Directory.Exists(LogPath))
                Directory.CreateDirectory(LogPath);

            string FileName = LogPath + "\\" + DateTime.Now.ToString("MM.dd.yyyy HH.mm.ss") + ".log";

            DebugLogFile = System.IO.File.CreateText(FileName);

            TextWriter SynchronizedDebugWriter = StreamWriter.Synchronized(DebugLogFile);

            TextWriterTraceListener Listener = new(SynchronizedDebugWriter, "Viking Log Listener");
            Trace.Listeners.Add(Listener);

            Trace.UseGlobalLock = true;
            TestCultureNumberParsing();
        }

        private static void TestCultureNumberParsing()
        {
            NumberFormatInfo current1 = CultureInfo.CurrentCulture.NumberFormat;

            Debug.WriteLine("Decimal separator: " + current1.NumberDecimalSeparator);
            Debug.WriteLine("Group separator:   " + current1.NumberGroupSeparator);

            string[] testStrings = ["3,800000000000e+01",
                                    "3.800000000000e+01",
                                    "3.80e+01",
                                    "38"];

            foreach (string number in testStrings)
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

        private static void ConfigureHighDpiMode()
        {
            try
            {
                var applicationType = typeof(Application);
                MethodInfo setHighDpiMode = applicationType.GetMethod("SetHighDpiMode", BindingFlags.Public | BindingFlags.Static);
                if (setHighDpiMode != null)
                {
                    Type highDpiModeType = setHighDpiMode.GetParameters()[0].ParameterType;
                    object perMonitorV2Value = Enum.Parse(highDpiModeType, "PerMonitorV2");
                    setHighDpiMode.Invoke(null, [perMonitorV2Value]);
                    return;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Viking] Failed to call Application.SetHighDpiMode: {ex.Message}");
            }

            TrySetPerMonitorDpiAwareness();
        }

        private static void TrySetPerMonitorDpiAwareness()
        {
            try
            {
                // PROCESS_PER_MONITOR_DPI_AWARE = 2
                SetProcessDpiAwareness(2);
            }
            catch (DllNotFoundException)
            {
                Trace.WriteLine("[Viking] shcore.dll not available for DPI awareness.");
            }
            catch (EntryPointNotFoundException)
            {
                Trace.WriteLine("[Viking] SetProcessDpiAwareness not available on this OS.");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Viking] Failed to set DPI awareness via shcore.dll: {ex.Message}");
            }
        }

        [DllImport("Shcore.dll")]
        private static extern int SetProcessDpiAwareness(int awareness);


    }
}
