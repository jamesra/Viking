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
using System.Windows.Forms;
using CommandLine.Text;
using Newtonsoft.Json;
using Viking.UI.Forms;
using VikingCoreResources = Viking.Properties.Resources;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Viking.UI.WPF;
using Viking.Services;
using Velopack;
using Newtonsoft.Json.Linq;


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
        /// <summary>
        /// Pre-filled on the login volume stage when no <c>-v</c> argument is given
        /// (Viking No Args). Used by this branch to target the local gRPC test endpoint
        /// declared in VolumeTest.xml.
        /// </summary>
        internal const string DefaultVolumeUrl = "http://rogue1.codepharm.net/RABBIT/VolumeTest.xml";

        static System.IO.StreamWriter? DebugLogFile = null;
        public static TextWriter? SynchronizedDebugWriter = null;

        public static string AppWebsite = "";

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

            // Register viking:// URL protocol so the OS launches Viking when the user clicks a viking:// link
            VikingProtocolRegistration.RegisterIfNeeded();

            ConfigureHighDpiMode();
            Application.EnableVisualStyles();

            Assembly execAssembly = System.Reflection.Assembly.GetExecutingAssembly();

            // Remove the DefaultTraceListener so nothing writes to OutputDebugString.
            // A rotating file listener stays in Release so deep-link failures are diagnosable.
#if !DEBUG
            Trace.Listeners.Clear();
#endif
            CreateFileTraceListener();

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

            // Check for updates before showing login dialog, unless this process was
            // started from viking:// — the launch code lives five minutes and the
            // Velopack UI has blocked exchange for minutes on a cold start.
            ApplicationSettings? appSettings = null;
            if (!ArgsContainVikingOpen(args))
                UpdateService.CheckForUpdatesAtStartup();

            // Handle viking://open?code=...&volume=...&location=... protocol (one-use launch code)
            if (TryHandleVikingOpenUrl(args, out appSettings))
            {
                // appSettings set by TryHandleVikingOpenUrl; continue to volume load below
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
        /// Handles viking://open?code=...&volume=...&location=... protocol.
        /// Returns true if args contained a viking:// URL and it was handled (appSettings may be null if user cancelled).
        /// </summary>
        private static bool TryHandleVikingOpenUrl(string[] args, out ApplicationSettings? appSettings)
        {
            appSettings = null;
            string? vikingUrl = args?.FirstOrDefault(a => a?.StartsWith("viking://", StringComparison.OrdinalIgnoreCase) == true);
            if (string.IsNullOrEmpty(vikingUrl))
                return false;

            if (!Uri.TryCreate(vikingUrl, UriKind.Absolute, out Uri? uri) || string.IsNullOrEmpty(uri?.Query))
                return false;

            var query = ParseQueryString(uri.Query);
            ApplyStartupPlaceArguments(query);

            string? code = query.TryGetValue("code", out var c) ? c?.Trim() : null;
            string? volume = query.TryGetValue("volume", out var v) ? v?.Trim() : null;

            if (!string.IsNullOrEmpty(code))
            {
                string baseUrl = Viking.Properties.Settings.Default.LaunchExchangeBaseUrl?.Trim() ?? "";
                if (string.IsNullOrEmpty(baseUrl))
                {
                    const string reason = "LaunchExchangeBaseUrl not configured.";
                    Trace.WriteLine("[Viking] viking://open with code ignored: " + reason, "Viking");
                    appSettings = ShowLoginWindow(volume, null, null, autoAdvanceFromDeepLink: true,
                        launchStatusMessage: "The launch link could not be used (" + reason + "); please sign in.");
                    return true;
                }

                var exchangeUrl = baseUrl.TrimEnd('/') + "/api/viking/launch-exchange";
                var exchange = ExchangeLaunchCodeAsync(exchangeUrl, code).GetAwaiter().GetResult();
                if (exchange.AccessToken == null)
                {
                    var detail = exchange.Error ?? "no token returned";
                    Trace.WriteLine("[Viking] Launch code exchange failed: " + detail, "Viking");
                    appSettings = ShowLoginWindow(volume, null, null, autoAdvanceFromDeepLink: true,
                        launchStatusMessage: "The launch link could not be used (" + detail + "); please sign in.");
                    return true;
                }
                string? initialVolume = !string.IsNullOrEmpty(exchange.VolumeUrl) ? exchange.VolumeUrl : volume;
                appSettings = ShowLoginWindowWithLaunchResult(exchange.AccessToken, exchange.IdentityServerUrl ?? "", initialVolume, exchange.VolumeName);
                return true;
            }

            if (!string.IsNullOrEmpty(volume))
            {
                appSettings = ShowLoginWindow(volume, null, null, autoAdvanceFromDeepLink: true);
                return true;
            }

            appSettings = ShowLoginWindow(null, null, null);
            return true;
        }

        private static bool ArgsContainVikingOpen(string[] args) =>
            args?.Any(a => a != null && a.StartsWith("viking://", StringComparison.OrdinalIgnoreCase)) == true;

        /// <summary>
        /// Copies location / coordinate query params into StartupArguments for post-load navigation.
        /// Location ID wins over coordinates when both are present.
        /// </summary>
        private static void ApplyStartupPlaceArguments(Dictionary<string, string> query)
        {
            UI.State.StartupArguments = [];

            if (query.TryGetValue("location", out string? locationRaw) && !string.IsNullOrWhiteSpace(locationRaw))
            {
                locationRaw = locationRaw.Trim();
                if (long.TryParse(locationRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long locationId))
                {
                    UI.State.StartupArguments["Location"] = locationId.ToString(CultureInfo.InvariantCulture);
                    return;
                }

                // Comma-separated x,y,z[,downsample] from SBFSEM-tools pick readout
                string[] parts = locationRaw.Split(',');
                if (parts.Length >= 3)
                {
                    UI.State.StartupArguments["X"] = parts[0].Trim();
                    UI.State.StartupArguments["Y"] = parts[1].Trim();
                    UI.State.StartupArguments["Z"] = parts[2].Trim();
                    if (parts.Length >= 4)
                        UI.State.StartupArguments["DS"] = parts[3].Trim();
                    return;
                }
            }

            CopyQueryKey(query, "x", "X");
            CopyQueryKey(query, "y", "Y");
            CopyQueryKey(query, "z", "Z");
            CopyQueryKey(query, "ds", "DS");
        }

        private static void CopyQueryKey(Dictionary<string, string> query, string queryKey, string startupKey)
        {
            if (query.TryGetValue(queryKey, out string? value) && !string.IsNullOrWhiteSpace(value))
                UI.State.StartupArguments[startupKey] = value.Trim();
        }

        private static Dictionary<string, string> ParseQueryString(string query)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(query) || query[0] != '?')
                return dict;
            foreach (var pair in query.Substring(1).Split('&'))
            {
                var eq = pair.IndexOf('=');
                if (eq < 0)
                    continue;
                var key = Uri.UnescapeDataString(pair.Substring(0, eq).Replace('+', ' '));
                var value = Uri.UnescapeDataString(pair.Substring(eq + 1).Replace('+', ' '));
                dict[key] = value;
            }
            return dict;
        }

        private sealed class LaunchExchangeResult
        {
            public LaunchExchangeResult(string? accessToken, string? identityServerUrl, string? volumeUrl, string? volumeName, string? error)
            {
                AccessToken = accessToken;
                IdentityServerUrl = identityServerUrl;
                VolumeUrl = volumeUrl;
                VolumeName = volumeName;
                Error = error;
            }

            public string? AccessToken { get; }
            public string? IdentityServerUrl { get; }
            public string? VolumeUrl { get; }
            public string? VolumeName { get; }
            public string? Error { get; }
        }

        /// <summary>
        /// Reads launch-exchange JSON. Prefer snake_case (the documented contract);
        /// also accept camelCase so a mixed server build still works.
        /// </summary>
        private static async Task<LaunchExchangeResult> ExchangeLaunchCodeAsync(string exchangeUrl, string code)
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
                    return new LaunchExchangeResult(null, null, null, null, $"exchange failed ({(int)response.StatusCode})");
                var responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var obj = JsonConvert.DeserializeObject<JObject>(responseJson);
                if (obj == null)
                    return new LaunchExchangeResult(null, null, null, null, "invalid token response");
                var accessToken = FirstJsonString(obj, "access_token", "accessToken");
                if (string.IsNullOrEmpty(accessToken))
                    return new LaunchExchangeResult(null, null, null, null, "no token returned");
                return new LaunchExchangeResult(
                    accessToken,
                    FirstJsonString(obj, "identity_server_url", "identityServerUrl"),
                    FirstJsonString(obj, "volume_url", "volumeUrl"),
                    FirstJsonString(obj, "volume_name", "volumeName"),
                    null);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Viking] Launch code exchange error: {ex.Message}", "Viking");
                return new LaunchExchangeResult(null, null, null, null, ex.Message);
            }
        }

        private static string? FirstJsonString(JObject obj, params string[] names)
        {
            foreach (var name in names)
            {
                var token = obj[name];
                if (token != null && token.Type != JTokenType.Null)
                {
                    var value = token.ToString();
                    if (!string.IsNullOrEmpty(value))
                        return value;
                }
            }
            return null;
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
            wpfLoginWindow.InitialVolumeUrl = string.IsNullOrWhiteSpace(volumePath) ? DefaultVolumeUrl : volumePath;
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
            UI.State.SbfsemToolsOpenUrl = string.IsNullOrWhiteSpace(settings.SbfsemToolsOpenUrl)
                ? "https://sbfsem-tools.com/open"
                : settings.SbfsemToolsOpenUrl;
            UI.State.SbfsemToolsIdentityBounceUrl = string.IsNullOrWhiteSpace(settings.SbfsemToolsIdentityBounceUrl)
                ? "https://identity.codepharm.net:4001/SbfsemOpen/Redirect"
                : settings.SbfsemToolsIdentityBounceUrl;

            if (wpfLoginWindow.BearerToken != null)
            {
                Viking.Tokens.TokenStore.BearerToken = wpfLoginWindow.BearerToken;
                var identityServerUrl = settings.IdentityServerURL ?? wpfLoginWindow.IdentityServerUrl;
                if (!string.IsNullOrEmpty(identityServerUrl))
                    Viking.Tokens.TokenStore.BearerTokenAuthority = identityServerUrl;
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

        /// <summary>
        /// File listener under %LOCALAPPDATA%\Viking\Logs. Kept in Release so a failed
        /// viking:// exchange is not silent. Oldest files beyond five are deleted.
        /// </summary>
        private static void CreateFileTraceListener()
        {
            string logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Viking",
                "Logs");
            Directory.CreateDirectory(logPath);

            string fileName = Path.Combine(logPath, DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss") + ".log");
            DebugLogFile = File.CreateText(fileName);
            SynchronizedDebugWriter = TextWriter.Synchronized(DebugLogFile);
            Trace.Listeners.Add(new TextWriterTraceListener(SynchronizedDebugWriter, "Viking Log Listener"));
            Trace.UseGlobalLock = true;
            Trace.AutoFlush = true;
            PruneLogFiles(logPath, keep: 5);
#if DEBUG
            TestCultureNumberParsing();
#endif
        }

        private static void PruneLogFiles(string directory, int keep)
        {
            try
            {
                var extras = new DirectoryInfo(directory).GetFiles("*.log")
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .Skip(keep)
                    .ToArray();
                foreach (var file in extras)
                {
                    try { file.Delete(); }
                    catch (Exception ex)
                    {
                        Trace.WriteLine("[Viking] Could not delete old log " + file.Name + ": " + ex.Message, "Viking");
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("[Viking] Log prune failed: " + ex.Message, "Viking");
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
