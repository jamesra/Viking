// #define USEASPMEMBERSHIP

using CommandLine;
using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using System.Windows.Forms;
using CommandLine.Text;
using Viking.ProductVersioning;
using Viking.UI.Forms;
using VikingCoreResources = Viking.Properties.Resources;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Viking.UI.WPF;
using Viking.Services;
using Velopack;
using Newtonsoft.Json;


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
        static string? DebugLogPath;

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

            if (args != null && args.Any(static a =>
                    string.Equals(a, "--version", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(a, "/version", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    WriteVersionToParentConsole();
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    // A missing console must not fall through into the UI.
                }

                return;
            }
            
            // Upgrade settings from previous versions (preserves user settings across updates)
            SettingsManager.UpgradeSettingsIfNeeded();

            // Register viking:// URL protocol so the OS launches Viking when the user clicks a viking:// link
            VikingProtocolRegistration.RegisterIfNeeded();

            // Same-volume deep link with a full volume URL/name can be forwarded before UI setup.
            // Launch codes are exchanged first (below) so location/volume from Identity survive a stripped query.
            if (VikingDeepLinkParser.TryFindOpenUrl(args, out string? earlyOpenUrl)
                && earlyOpenUrl != null
                && VikingDeepLinkParser.TryParse(earlyOpenUrl, out VikingDeepLink? earlyLink)
                && earlyLink != null
                && string.IsNullOrWhiteSpace(earlyLink.Code)
                && VikingSingleInstance.TryForwardToExistingInstance(earlyOpenUrl))
            {
                return;
            }

            ConfigureHighDpiMode();
            Application.EnableVisualStyles();

            Assembly execAssembly = System.Reflection.Assembly.GetExecutingAssembly();

            // Leave the default listener in place. Clearing it made Release OutputDebugString
            // silent after the settings-upgrade line, so a DBWIN monitor missed the launch path.
            CreateDebugListener();
            WriteStartupMarker();
            SynchronizedDebugWriter?.WriteLine($"{DateTime.Now:O} {ProductVersion.Describe(execAssembly)}");
            SynchronizedDebugWriter?.Flush();
            InitializeMathnet();
            Viking.UI.GpuExceptionHandling.Register();

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

            // Handle viking://open?code=...&volume=...&location=... protocol (one-use launch code)
            if (TryHandleVikingOpenUrl(args, out appSettings, out bool forwardedToExisting))
            {
                if (forwardedToExisting)
                    return;
            }
            else
            {
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
            }

            //Close the program if no settings were provided or the volume is missing
            if (appSettings is null || string.IsNullOrWhiteSpace(appSettings.VolumeURL))
                return;

            //Make sure the volume URL includes a file, if it does not then include Volume.VikingXML by default
            appSettings.VolumeURL = Viking.Common.Util.AppendDefaultVolumeFilenameIfMissing(appSettings.VolumeURL);
            UI.State.VolumeUrl = appSettings.VolumeURL;
            Trace.WriteLine($"Loading: {appSettings.VolumeURL}", "Viking");

            // Populate annotation URL asynchronously (fire-and-forget, errors are logged)
            var populateTask = Task.Run(async () =>
                await PopulateAnnotationUrlFromVolumeAsync(appSettings).ConfigureAwait(false));
            populateTask.GetAwaiter().GetResult();

            // --------------------------------------------------------------------------------------

            VikingApplicationContext context = new(appSettings);
            context.Initialize();

            // After the main window and viewer exist, accept same-volume viking:// activations.
            if (context.MainForm != null && !string.IsNullOrWhiteSpace(UI.State.VolumeUrl))
            {
                context.MainForm.FormClosing += (_, e) =>
                {
                    if (!e.Cancel)
                        VikingSingleInstance.BeginShutdown();
                };
                string? volumeName = UI.State.IdentityVolumeName ?? UI.State.volume?.Name;
                VikingSingleInstance.StartListening(UI.State.VolumeUrl!, volumeName, VikingDeepLinkActivation.HandleIncomingUrl);
            }

            Application.Run(context);

            VikingSingleInstance.StopListening();
            VikingApplicationContext.ShutdownAfterMessageLoop();

            SynchronizedDebugWriter?.Close();
            DebugLogFile?.Close();
            Environment.Exit(0);
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            foreach (string? value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }

            return null;
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
        /// Handles viking://open?code=...&volume=...&location=... protocol.
        /// Returns true if args contained a viking:// URL. <paramref name="forwardedToExisting"/> is set
        /// when the running instance accepted the link and this process should exit.
        /// </summary>
        private static bool TryHandleVikingOpenUrl(string[] args, out ApplicationSettings? appSettings, out bool forwardedToExisting)
        {
            appSettings = null;
            forwardedToExisting = false;
            if (!VikingDeepLinkParser.TryFindOpenUrl(args, out string? vikingUrl) || string.IsNullOrEmpty(vikingUrl))
                return false;

            if (!VikingDeepLinkParser.TryParse(vikingUrl, out VikingDeepLink? link) || link is null)
                return false;

            ApplyStartupPlaceArguments(link);

            if (!string.IsNullOrEmpty(link.Code))
            {
                string? volumeHint = link.VolumeUrl ?? link.VolumeName;
                string baseUrl = Viking.Properties.Settings.Default.LaunchExchangeBaseUrl?.Trim() ?? "";
                if (string.IsNullOrEmpty(baseUrl))
                {
                    const string reason = "LaunchExchangeBaseUrl not configured.";
                    Trace.WriteLine("[Viking] viking://open with code ignored: " + reason, "Viking");
                    appSettings = ShowLoginWindow(volumeHint, null, null, autoAdvanceFromDeepLink: true,
                        launchStatusMessage: "The launch link could not be used (" + reason + "); please sign in.");
                    return true;
                }

                var exchangeUrl = baseUrl.TrimEnd('/') + "/api/viking/launch-exchange";
                VikingLaunchExchangeResult exchanged = ExchangeLaunchCodeAsync(exchangeUrl, link.Code!).GetAwaiter().GetResult();
                MergeExchangeIntoLink(link, exchanged);
                ApplyStartupPlaceArguments(link);

                string activationUrl = VikingDeepLinkParser.BuildActivationUrl(link);
                if (VikingSingleInstance.TryForwardToExistingInstance(activationUrl))
                {
                    forwardedToExisting = true;
                    return true;
                }

                if (string.IsNullOrEmpty(exchanged.AccessToken))
                {
                    var detail = exchanged.Error ?? "no token returned";
                    Trace.WriteLine("[Viking] Launch code exchange failed: " + detail, "Viking");
                    appSettings = ShowLoginWindow(link.VolumeUrl ?? link.VolumeName ?? volumeHint, null, null, autoAdvanceFromDeepLink: true,
                        launchStatusMessage: "The launch link could not be used (" + detail + "); please sign in.");
                    return true;
                }

                string? initialVolume = !string.IsNullOrEmpty(link.VolumeUrl) ? link.VolumeUrl : link.VolumeName;
                appSettings = ShowLoginWindowWithLaunchResult(exchanged.AccessToken!, exchanged.IdentityServerUrl ?? "", initialVolume, link.VolumeName);
                return true;
            }

            if (VikingSingleInstance.TryForwardToExistingInstance(VikingDeepLinkParser.BuildActivationUrl(link)))
            {
                forwardedToExisting = true;
                return true;
            }

            if (!string.IsNullOrEmpty(link.VolumeUrl) || !string.IsNullOrEmpty(link.VolumeName))
            {
                appSettings = ShowLoginWindow(link.VolumeUrl ?? link.VolumeName, null, null, autoAdvanceFromDeepLink: true);
                return true;
            }

            appSettings = ShowLoginWindow(null, null, null);
            return true;
        }

        /// <summary>
        /// Copies location / coordinate query params into StartupArguments for post-load navigation.
        /// Location ID wins over coordinates when both are present.
        /// </summary>
        private static void ApplyStartupPlaceArguments(VikingDeepLink link)
        {
            UI.State.StartupArguments = link.Place ?? [];
        }

        private static void MergeExchangeIntoLink(VikingDeepLink link, VikingLaunchExchangeResult exchanged)
        {
            if (!string.IsNullOrWhiteSpace(exchanged.VolumeUrl))
                link.VolumeUrl = exchanged.VolumeUrl.Trim();
            if (!string.IsNullOrWhiteSpace(exchanged.VolumeName))
                link.VolumeName = exchanged.VolumeName.Trim();

            var fromExchange = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(exchanged.Location))
                fromExchange["location"] = exchanged.Location.Trim();
            if (!string.IsNullOrWhiteSpace(exchanged.X))
                fromExchange["x"] = exchanged.X.Trim();
            if (!string.IsNullOrWhiteSpace(exchanged.Y))
                fromExchange["y"] = exchanged.Y.Trim();
            if (!string.IsNullOrWhiteSpace(exchanged.Z))
                fromExchange["z"] = exchanged.Z.Trim();
            if (!string.IsNullOrWhiteSpace(exchanged.Downsample))
                fromExchange["ds"] = exchanged.Downsample.Trim();

            if (fromExchange.Count > 0)
                VikingDeepLinkParser.MergePlace(link.Place, VikingDeepLinkParser.ParsePlaceArguments(fromExchange));
        }

        private static async Task<VikingLaunchExchangeResult> ExchangeLaunchCodeAsync(string exchangeUrl, string code)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(15);
                var body = new { code };
                var json = JsonConvert.SerializeObject(body);
                using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                using var response = await client.PostAsync(exchangeUrl, content).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    Trace.WriteLine($"[Viking] Launch code exchange HTTP {(int)response.StatusCode}.", "Viking");
                    return VikingLaunchExchangeParser.Failed($"exchange failed ({(int)response.StatusCode})");
                }

                var responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return VikingLaunchExchangeParser.ParseJson(responseJson);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Viking] Launch code exchange error: {ex.Message}", "Viking");
                return VikingLaunchExchangeParser.Failed(ex.Message);
            }
        }

        private static ApplicationSettings? ShowLoginWindowWithLaunchResult(string initialApiToken, string initialIdentityServerUrl, string? initialVolumeUrl, string? initialVolumeName = null)
        {
            LoginWindow wpfLoginWindow = new();
            wpfLoginWindow.InitialApiToken = initialApiToken;
            wpfLoginWindow.InitialIdentityServerUrl = string.IsNullOrWhiteSpace(initialIdentityServerUrl) ? null : initialIdentityServerUrl;
            wpfLoginWindow.InitialVolumeUrl = string.IsNullOrWhiteSpace(initialVolumeUrl) ? null : initialVolumeUrl;
            wpfLoginWindow.InitialVolumeName = string.IsNullOrWhiteSpace(initialVolumeName) ? null : initialVolumeName;
            wpfLoginWindow.AutoAdvanceFromDeepLink = true;
            return ShowLoginWindowFromDialog(wpfLoginWindow);
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

        private static ApplicationSettings? ShowLoginWindow(
            string? volumePath,
            string? username = null,
            string? password = null,
            bool autoAdvanceFromDeepLink = false,
            string? launchStatusMessage = null)
        {
            LoginWindow wpfLoginWindow = new();
            wpfLoginWindow.InitialVolumeUrl = string.IsNullOrWhiteSpace(volumePath) ? null : volumePath;
            wpfLoginWindow.InitialUsername = username;
            wpfLoginWindow.InitialPassword = password;
            wpfLoginWindow.AutoAdvanceFromDeepLink = autoAdvanceFromDeepLink && !string.IsNullOrWhiteSpace(volumePath);
            wpfLoginWindow.LaunchStatusMessage = launchStatusMessage;
            return ShowLoginWindowFromDialog(wpfLoginWindow);
        }

        private static ApplicationSettings? ShowLoginWindowFromDialog(LoginWindow wpfLoginWindow)
        {
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
            UI.State.IdentityVolumeName = string.IsNullOrWhiteSpace(wpfLoginWindow.VolumeName)
                ? null
                : wpfLoginWindow.VolumeName;
            if (!string.IsNullOrWhiteSpace(wpfLoginWindow.VolumeURL))
                UI.State.VolumeUrl = Viking.Common.Util.AppendDefaultVolumeFilenameIfMissing(wpfLoginWindow.VolumeURL);
            UI.State.SbfsemToolsOpenUrl = string.IsNullOrWhiteSpace(settings.SbfsemToolsOpenUrl)
                ? "https://sbfsem-tools.com/open"
                : settings.SbfsemToolsOpenUrl;
            UI.State.SbfsemToolsIdentityBounceUrl = string.IsNullOrWhiteSpace(settings.SbfsemToolsIdentityBounceUrl)
                ? "https://identity.codepharm.net:4001/SbfsemOpen/Redirect"
                : settings.SbfsemToolsIdentityBounceUrl;

            if (wpfLoginWindow.BearerToken != null)
            {
                Viking.Tokens.TokenInjector.BearerToken = wpfLoginWindow.BearerToken;
                // Prefer the URL from this login (launch-exchange) over a blank user setting.
                // Empty IdentityServerURL in settings must not block Authority (?? only skips null).
                var identityServerUrl = FirstNonEmpty(
                    wpfLoginWindow.IdentityServerUrl,
                    settings.IdentityServerURL);
                if (!string.IsNullOrEmpty(identityServerUrl))
                {
                    Viking.Tokens.TokenInjector.BearerTokenAuthority = identityServerUrl;
                }

                Trace.WriteLine(
                    "[Viking] Bearer token set. " +
                    $"Authority={(identityServerUrl ?? "(null)")} AccessTokenLength={wpfLoginWindow.BearerToken.AccessToken?.Length ?? 0}",
                    "Viking");

                if (string.IsNullOrEmpty(wpfLoginWindow.BearerToken.AccessToken))
                {
                    Trace.WriteLine("[Viking] Login finished but BearerToken.AccessToken is empty; annotation service will deny access.", "Viking");
                    System.Windows.Forms.MessageBox.Show(
                        "Sign-in completed, but the access token could not be read. Annotation loading will fail.\n\n" +
                        "Try signing in again with your username and password, or update Viking.",
                        "Viking",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Error);
                    return null;
                }
            }
            else
            {
                Trace.WriteLine("[Viking] Login finished with no BearerToken; annotation calls may be denied.", "Viking");
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

            UI.State.IdentityServerUrl = FirstNonEmpty(
                wpfLoginWindow.IdentityServerUrl,
                settings.IdentityServerURL);

            UI.State.RecentSegmentationServiceUrls = [];
            if (settings.SegmentationServiceUrls != null)
            {
                foreach (string url in settings.SegmentationServiceUrls)
                {
                    if (!string.IsNullOrWhiteSpace(url))
                        UI.State.RecentSegmentationServiceUrls.Add(url);
                }
            }

            UI.State.PersistSegmentationServiceSelection = endpoint =>
            {
                settings.LastSegmentationServiceUrl = endpoint ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(endpoint))
                {
                    var history = settings.SegmentationServiceUrls ?? [];
                    if (history.Contains(endpoint))
                        history.Remove(endpoint);
                    history.Insert(0, endpoint);
                    settings.SegmentationServiceUrls = history;
                }

                settings.Save();
            };

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
        /// <summary>
        /// Prints the entry-assembly version to the parent console.
        /// Viking is a WinExe, so the process has no console until one is attached,
        /// and <see cref="Console.Out"/> must be reopened afterwards or the write is dropped.
        /// Called for <c>--version</c> and <c>/version</c> before the UI starts.
        /// </summary>
        private static void WriteVersionToParentConsole()
        {
            // Do not AllocConsole: that opens a stray window when there is no parent console.
            var line = ProductVersion.Describe(Assembly.GetExecutingAssembly()) + Environment.NewLine;
            var handle = GetStdHandle(StdOutputHandle);
            if (handle != IntPtr.Zero && handle != new IntPtr(-1))
            {
                var bytes = Encoding.UTF8.GetBytes(line);
                using (var stream = new FileStream(new SafeFileHandle(handle, ownsHandle: false), FileAccess.Write))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush();
                }

                return;
            }

            if (!AttachConsole(AttachParentProcess))
                return;

            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Console.Write(line);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        private const int AttachParentProcess = -1;
        private const int StdOutputHandle = -11;

        private static void CreateDebugListener()
        {
            string LogPath = System.Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Viking\\Logs";
            if (!Directory.Exists(LogPath))
                Directory.CreateDirectory(LogPath);

            string FileName = LogPath + "\\" + DateTime.Now.ToString("MM.dd.yyyy HH.mm.ss") + ".log";

            DebugLogPath = FileName;
            DebugLogFile = System.IO.File.CreateText(FileName);
            SynchronizedDebugWriter = StreamWriter.Synchronized(DebugLogFile);

            TextWriterTraceListener Listener = new(SynchronizedDebugWriter, "Viking Log Listener");
            Trace.Listeners.Add(Listener);

            Trace.UseGlobalLock = true;
            TestCultureNumberParsing();
        }

        /// <summary>
        /// Records where Release launch traces went. Called from Main after CreateDebugListener.
        /// debug_output.txt next to Viking.exe is rewritten empty on each build; appending the
        /// log path there is what a monitor of that file can see on the next run.
        /// </summary>
        private static void WriteStartupMarker()
        {
            string line = "[Viking] Startup log: " + (DebugLogPath ?? "(none)");
            Trace.WriteLine(line, "Viking");
            try
            {
                string sideFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug_output.txt");
                File.AppendAllText(sideFile, line + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Trace.WriteLine("[Viking] Could not append debug_output.txt: " + ex.Message, "Viking");
            }
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
