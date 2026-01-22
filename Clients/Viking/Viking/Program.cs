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
            UpgradeSettingsIfNeeded();

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
            CheckForUpdatesAtStartup();

            ApplicationSettings? appSettings = null;

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

            //Make sure the volume URL includes a file, if it does not then include Volume.VikingXML by default
            appSettings.VolumeURL = Viking.Common.Util.AppendDefaultVolumeFilenameIfMissing(appSettings.VolumeURL);
            Trace.WriteLine($"Loading: {appSettings.VolumeURL}", "Viking");

            // Populate annotation URL asynchronously (fire-and-forget, errors are logged)
            var populateTask = Task.Run(async () =>
                await PopulateAnnotationUrlFromVolumeAsync(appSettings).ConfigureAwait(false));
            populateTask.GetAwaiter().GetResult();

            // --------------------------------------------------------------------------------------


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
            Trace.Listeners.Add(Listener);

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

        /// <summary>
        /// Upgrades settings from previous versions to preserve user preferences across updates.
        /// This migrates settings from version-specific directories to the current version.
        /// Uses a simple approach: if current settings are empty/default, try to upgrade from previous version.
        /// </summary>
        private static void UpgradeSettingsIfNeeded()
        {
            try
            {
                var settings = Properties.Settings.Default;

                // Check if we have any meaningful settings already (if VolumeURLs is empty, likely first run or after update)
                var hasExistingSettings = settings.VolumeURLs != null && settings.VolumeURLs.Count > 0;

                if (!hasExistingSettings)
                {
                    Trace.WriteLine("[Viking] No existing settings found, attempting to upgrade from previous version...");

                    // Call the built-in Upgrade() method which migrates settings from previous versions
                    // This will look for settings in older version directories and copy them to current version
                    settings.Upgrade();
                    settings.Reload();

                    // Check if we now have settings after upgrade
                    var hasSettingsAfterUpgrade = settings.VolumeURLs != null && settings.VolumeURLs.Count > 0;
                    if (hasSettingsAfterUpgrade)
                    {
                        var count = settings.VolumeURLs?.Count ?? 0;
                        Trace.WriteLine($"[Viking] Settings upgraded successfully. Found {count} volume URL(s).");
                        settings.Save(); // Save the upgraded settings
                    }
                    else
                    {
                        Trace.WriteLine("[Viking] No settings found in previous version - this appears to be a fresh install.");
                    }
                }
                else
                {
                    Trace.WriteLine("[Viking] Existing settings found, skipping upgrade.");
                }
            }
            catch (Exception ex)
            {
                // Don't fail application startup if settings upgrade fails
                Trace.WriteLine($"[Viking] Warning: Failed to upgrade settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies the downloaded update and restarts the application.
        /// This method must be called on the UI thread.
        /// </summary>
        private static void ApplyUpdateAndRestart(UpdateManager mgr, VelopackAsset release, NuGet.Versioning.SemanticVersion version)
        {
            try
            {
                // Save all application settings before applying update
                try
                {
                    Properties.Settings.Default.Save();
                    Trace.WriteLine("[Velopack] Application settings saved before update.");
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[Velopack] Warning: Failed to save settings before update: {ex.Message}");
                }

                var versionString = version.ToString();
                Trace.WriteLine($"[Velopack] Calling ApplyUpdatesAndRestart for version {versionString}...");

                // Close all open forms gracefully before applying update
                // Velopack's ApplyUpdatesAndRestart will handle the exit, but we close forms to ensure clean state
                var forms = new List<Form>();
                foreach (Form form in Application.OpenForms)
                {
                    if (form != null && !form.IsDisposed)
                    {
                        forms.Add(form);
                    }
                }

                // Close forms in reverse order (top-most first)
                for (int i = forms.Count - 1; i >= 0; i--)
                {
                    SafeDisposeForm(forms[i]);
                }

                Application.DoEvents();

                // Apply updates and restart the application
                // This method replaces files and restarts the application
                // It does NOT return normally - it launches the updated app and exits the current one
                mgr.ApplyUpdatesAndRestart(release);

                // If we reach here, ApplyUpdatesAndRestart didn't work as expected
                // This should not happen, but handle it gracefully
                Trace.WriteLine($"[Velopack] Warning: ApplyUpdatesAndRestart returned unexpectedly. Forcing exit...");
                Application.Exit();
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Velopack] Error applying update: {ex.Message}");
                if (ex.StackTrace != null)
                {
                    Trace.WriteLine($"[Velopack] Stack trace: {ex.StackTrace}");
                }

                MessageBox.Show(
                    $"Error applying update:\n\n{ex.Message}\n\n" +
                    "Please restart the application manually. The update will be applied on the next launch.",
                    "Update Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        /// <summary>
        /// Creates a simple modal dialog with a message.
        /// All operations occur on the UI thread.
        /// </summary>
        private static Form CreateSimpleDialog(string title, string message, int width = 350, int height = 120)
        {
            var form = new Form()
            {
                Text = title,
                Width = width,
                Height = height,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterScreen,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                AutoSize = false,
                AutoScaleMode = AutoScaleMode.Font,
                Padding = new Padding(20)
            };

            var label = new Label()
            {
                Text = message,
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                AutoSize = false,
                Padding = new Padding(10)
            };

            form.Controls.Add(label);
            return form;
        }

        /// <summary>
        /// Creates a download progress dialog with progress bar and status label.
        /// All operations occur on the UI thread.
        /// </summary>
        private static (Form form, ProgressBar progressBar, Label statusLabel) CreateDownloadDialog(string version)
        {
            var form = new Form()
            {
                Text = "Downloading Update",
                Width = 450,
                Height = 200,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterScreen,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                AutoSize = false,
                AutoScaleMode = AutoScaleMode.Font,
                Padding = new Padding(20)
            };

            var tableLayout = new TableLayoutPanel()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 70F));
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 30F));

            var statusLabel = new Label()
            {
                Text = $"Downloading version {version}...",
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                AutoSize = false,
                AutoEllipsis = true,
                Padding = new Padding(10)
            };

            var progressBar = new ProgressBar()
            {
                Dock = DockStyle.Fill,
                Style = ProgressBarStyle.Continuous,
                Minimum = 0,
                Maximum = 100,
                Margin = new Padding(10, 5, 10, 5)
            };

            tableLayout.Controls.Add(statusLabel, 0, 0);
            tableLayout.Controls.Add(progressBar, 0, 1);
            form.Controls.Add(tableLayout);

            return (form, progressBar, statusLabel);
        }

        /// <summary>
        /// Safely disposes a form, ensuring it's closed and disposed on the UI thread.
        /// </summary>
        private static void SafeDisposeForm(Form? form)
        {
            if (form == null || form.IsDisposed)
                return;

            try
            {
                if (form.InvokeRequired)
                {
                    form.Invoke(new Action(() =>
                    {
                        try
                        {
                            form.Close();
                            form.Dispose();
                        }
                        catch { }
                    }));
                }
                else
                {
                    form.Close();
                    form.Dispose();
                }
            }
            catch { }
        }

        /// <summary>
        /// Shows a "Checking for updates" form and checks for updates at startup.
        /// This runs on the UI thread before the login dialog is shown.
        /// Uses a proper message loop to keep the form responsive during async operations.
        /// </summary>
        private static void CheckForUpdatesAtStartup()
        {
            // Show "Checking for updates" form
            Form? checkingForm = null;

            // Get the update URL from your deployment server
            string updateUrl = "https://websvc.codepharm.net/Software/Viking";

            var mgr = new UpdateManager(updateUrl);

            try
            {
                // Create and show checking form using helper
                checkingForm = CreateSimpleDialog("Checking for Updates", "Checking for updates...");
                checkingForm.Show();
                checkingForm.Refresh();
                Application.DoEvents(); // Ensure form is visible

                // Check for updates and handle download if available
                var (release, version) = CheckAndHandleUpdates(mgr);

                // Close the checking form
                SafeDisposeForm(checkingForm);
                checkingForm = null;
                Application.DoEvents(); // Process any pending messages

                // If update was downloaded, apply and restart
                if (release != null && version != null)
                {
                    ApplyUpdateAndRestart(mgr, release, version);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Velopack] Error during update check at startup: {ex.Message}");
                SafeDisposeForm(checkingForm);
                // Continue to login even if update check fails
            }
        }

        /// <summary>
        /// Checks for available updates and handles downloading/installing if found.
        /// Returns tuple with release and version, or (null, null) if no update or user declines.
        /// </summary>
        private static (VelopackAsset? release, NuGet.Versioning.SemanticVersion? version) CheckAndHandleUpdates(UpdateManager mgr)
        {
            try
            {
                var updateInfo = mgr.CheckForUpdates();

                if (updateInfo == null)
                {
                    Trace.WriteLine("[Velopack] No updates available - running latest version");
                    return (null, null);
                }

                var currentVersion = mgr.CurrentVersion;
                var newVersion = updateInfo.TargetFullRelease.Version;
                var newVersionString = newVersion.ToString();

                Trace.WriteLine($"[Velopack] Update available: {currentVersion} -> {newVersionString}");

                // Notify user about available update and prompt to download
                var result = MessageBox.Show(
                    $"A new version of Viking ({newVersionString}) is available.\n\n" +
                    $"Current version: {currentVersion}\n\n" +
                    "Would you like to download and install it now?\n\n" +
                    "Note: The application will close and restart after the update.",
                    "Update Available",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

                if (result == DialogResult.Yes)
                {
                    // Create download form on UI thread (we're already on UI thread at startup)
                    Form? downloadForm = null;
                    ProgressBar? progressBar = null;
                    Label? statusLabel = null;

                    try
                    {
                        // Create download progress dialog using helper
                        (downloadForm, progressBar, statusLabel) = CreateDownloadDialog(newVersionString);
                        downloadForm.Show();
                        downloadForm.Refresh();
                        Application.DoEvents(); // Ensure form is visible before starting download

                        // Download the update with progress callback
                        // Note: We're in a synchronous context, so we use DoEvents() loop to keep UI responsive
                        // The callback updates UI on the UI thread (we check InvokeRequired for safety)
                        var downloadTask = mgr.DownloadUpdatesAsync(updateInfo, (progress) =>
                        {
                            // Ensure UI updates happen on UI thread (they should already, but check for safety)
                            if (downloadForm != null && !downloadForm.IsDisposed)
                            {
                                if (downloadForm.InvokeRequired)
                                {
                                    downloadForm.Invoke(new Action(() =>
                                    {
                                        if (progressBar != null && !progressBar.IsDisposed)
                                            progressBar.Value = (int)progress;
                                        if (statusLabel != null && !statusLabel.IsDisposed)
                                            statusLabel.Text = $"Downloading version {newVersionString}... {progress}%";
                                        Application.DoEvents();
                                    }));
                                }
                                else
                                {
                                    progressBar!.Value = (int)progress;
                                    statusLabel!.Text = $"Downloading version {newVersionString}... {progress}%";
                                    Application.DoEvents();
                                }
                            }
                        });

                        // Keep UI responsive while downloading (necessary in synchronous context before Application.Run)
                        // Properly await the task with UI message pumping
                        while (!downloadTask.IsCompleted)
                        {
                            Application.DoEvents();
                            Thread.Sleep(50); // Small sleep to avoid 100% CPU
                        }

                        // Wait for task completion to catch any exceptions
                        // Use GetAwaiter().GetResult() here since we're in a synchronous method and already pumping messages
                        downloadTask.GetAwaiter().GetResult();

                        // Update status to show download complete
                        if (downloadForm != null && !downloadForm.IsDisposed)
                        {
                            statusLabel!.Text = "Download complete. Preparing to apply update...";
                            progressBar!.Value = 100;
                            Application.DoEvents();
                        }

                        Trace.WriteLine($"[Velopack] Update downloaded successfully. Version {newVersionString} ready to install.");

                        // Close and dispose the download form
                        SafeDisposeForm(downloadForm);
                        downloadForm = null; // Clear reference

                        // Brief pause to ensure form is fully disposed before proceeding
                        Application.DoEvents();
                        Thread.Sleep(100);

                        return (updateInfo.TargetFullRelease, newVersion);
                    }
                    catch (Exception downloadEx)
                    {
                        SafeDisposeForm(downloadForm);

                        var errorMessage = $"Error downloading update:\n\n{downloadEx.Message}";
                        if (downloadEx.StackTrace != null)
                        {
                            errorMessage += $"\n\nStack trace:\n{downloadEx.StackTrace}";
                        }

                        MessageBox.Show(
                            errorMessage,
                            "Update Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error
                        );
                        Trace.WriteLine($"[Velopack] Error downloading update: {downloadEx.Message}");
                        if (downloadEx.StackTrace != null)
                        {
                            Trace.WriteLine($"[Velopack] Stack trace: {downloadEx.StackTrace}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Silently handle update check failures - don't disrupt user experience
                Trace.WriteLine($"[Velopack] Error checking for updates: {ex.Message}");
            }

            return (null, null);
        }
    }
}
