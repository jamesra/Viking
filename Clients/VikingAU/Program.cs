using Viking.AnnotationServiceTypes.Interfaces;
using CommandLine;
using CommandLine.Text;
using Geometry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Viking.VolumeModel;
using Viking.Tokens;
using WebAnnotationModel;
using WebAnnotationModel.gRPC;
using WebAnnotationModel.Objects;
using Viking.Common;
using System.Xml.Linq;

namespace Viking.AU
{
    partial class CommandLineOptions
    {
        [Option('v', "VolumeURL", Required = true, HelpText = "URL of VolumeXML file")]
        public string VolumeURL { get; set; }

        [Option('u', "username", Required = false, HelpText = "Username for identity server authentication")]
        public string Username { get; set; } = "Anonymous";

        [Option('p', "password", Required = false, HelpText = "Password")]
        public string Password { get; set; } = "connectome";

        [Option("identity-server-url", Required = false, HelpText = "Identity server URL (default from config or https://identity.codepharm.net:5001/)")]
        public string IdentityServerUrl { get; set; } = null;

        [Option('c', "closed_interpolation_points", Required = false, HelpText = "Number of closed curve interpolation points")]
        public int NumClosedInterpolationPoints { get; set; } = 10;

        [Option('o', "open_interpolation_points", Required = false, HelpText = "Number of open curve interpolation points")]
        public int NumOpenInterpolationPoints { get; set; } = 3;

        [Option('s', "sections", Required = false, HelpText = "Section Numbers to update")]
        public string SectionNumbersString { get; set; } = null;

        [Option('t', "threads", Required = false, HelpText = "Number of threads to process and submit updates on.  If VikingAU is reporting timeout errors lower this number.  If VikingAU isn't using 100% of the CPU you can try raising it.  Default value is the number of cores on the machine + 1")]
        public int? NumThreads { get; set; } = null;

        [Option('m', "translate", Required = false, HelpText = "Translation file, json each array entry is <section #> <X> <Y> <datetime>")]
        public string TranslateFile { get; set; } = null;

        public IList<long> Sections
        {
            get
            {
                if (this.SectionNumbersString is null)
                    return [];
                else
                    return NumberRangeToList(this.SectionNumbersString);
            }
        }


        private static bool IsNumberRange(string input)
        {
#if NETFRAMEWORK
            Regex regex = MyRegex;
#else
            Regex regex = MyRegex();
#endif
            var match = regex.Match(input);
            return match.Success;
        }


        /// <summary>
        /// Convert a string of two integers seperated by a hyphen to a list of integers
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private static List<long> NumberRangeToList(string input)
        {
            List<long> listNumbers;

            try
            {
                long SectionNumber = System.Convert.ToInt64(input);
                listNumbers =
                [
                    SectionNumber
                ];
                return listNumbers;
            }
            catch (FormatException e)
            { 
#if NETFRAMEWORK
                Regex regex = MyRegex;
#else
                Regex regex = MyRegex();
#endif
                Match m = regex.Match(input);

                long start = System.Convert.ToInt64(m.Groups[1].Value);
                long end = System.Convert.ToInt64(m.Groups[2].Value);

                listNumbers = new List<long>((int)(end - start) + 1);

                for (long val = start; val <= end; val++)
                {
                    listNumbers.Add(val);
                }

                return listNumbers;
            }
        }

        private static List<long> NumberStringToList(string input)
        {
            List<long> listNumbers = [];

            foreach (string chunk in input.Split(','))
            {
                string trimmed_chunk = chunk.Trim();

                if (IsNumberRange(trimmed_chunk))
                    listNumbers.AddRange(NumberRangeToList(input));
                else
                {
                    try
                    {
                        listNumbers.Add(System.Convert.ToInt64(trimmed_chunk));
                    }
                    catch (FormatException)
                    {

                    }
                }
            }

            return listNumbers;
        }

#if NETFRAMEWORK
        private static readonly Regex MyRegex = new(@"(\d+)\-(\d+)", RegexOptions.Compiled);
#else
        [GeneratedRegex(@"(\d+)\-(\d+)")]
        private static partial Regex MyRegex();
#endif
    }

    class ConsoleProgressReporter
    {
        int LastLineLength = 0;

        public ConsoleProgressReporter(Progress<ProgressInfo> progress = null)
        {
            if (progress != null)
            {
                progress.ProgressChanged += OnReport;
            }
        }

        public void OnReport(object sender, ProgressInfo info)
        {
            string message = info.Message;
            string ProgressPercentage = info.Progress.ToString("0.00");

            StringBuilder output = new();
            string Details = $"{ProgressPercentage}% {message}";
            int LineLength = Details.Length;
            output.Append('\b', LastLineLength);
            output.Append(Details);
            if (LastLineLength > Details.Length)
                output.Append(' ', LastLineLength - Details.Length);

            string final_output = output.ToString();
            LastLineLength = LineLength;

            Console.Write(final_output);
        }

        private static void TaskComplete() => Console.WriteLine("Task Complete");
    }

    class Program
    {
        private static readonly SemaphoreSlim ConsoleLock = new(1);

        static SectionTranslations SectionTranslations = [];


        static async Task Main(string[] args)
        {
            var parse_result = CommandLine.Parser.Default.ParseArguments<CommandLineOptions>(args);
            parse_result.WithNotParsed(e => ShowErrorsAsync(parse_result, e));
            await parse_result.WithParsedAsync(RunAsync);

        }

        static void ShowErrorsAsync(ParserResult<CommandLineOptions> result, IEnumerable<Error> errors)
        {
            // Create a new help text with error information
            HelpText errorHelpText = HelpText.AutoBuild(result);
            errorHelpText.AddPreOptionsLine("ERROR: Unable to parse command line arguments.");
            errorHelpText.AddPreOptionsLine("The following errors occurred:");

            foreach (var error in errors)
            {
                errorHelpText.AddPreOptionsLine($"  {error}");
            }

            errorHelpText.AddPreOptionsLine("");
            Console.WriteLine(errorHelpText);

            // Exit with error code
            Environment.Exit(1);
        }


        static async Task RunAsync(CommandLineOptions options)
        {
            if (options.TranslateFile != null)
            {
                SectionTranslations = SectionTranslations.CreateFromConfigFile(options.TranslateFile);
            }

            int numThreads = options.NumThreads ?? System.Environment.ProcessorCount + 1;

#if NETFRAMEWORK
            System.Data.Entity.SqlServer.SqlProviderServices.SqlServerTypesAssemblyName = "Microsoft.SqlServer.Types, Version=16.0.0.0, Culture=neutral, PublicKeyToken=89845dcd8080cc91";
#endif
            SqlServerTypesLoader.Loader.LoadNativeAssemblies(AppDomain.CurrentDomain.BaseDirectory);

            var credentials = new NetworkCredential(options.Username, options.Password);

            Console.WriteLine("Loading volume metadata...");
            var (volumeName, identityApiUrl) = await LoadAndParseVolumeXmlAsync(options.VolumeURL, credentials, options.IdentityServerUrl);

            var identityServerUrl = options.IdentityServerUrl ?? "https://identity.codepharm.net:5001/";
            if (!Uri.TryCreate(identityServerUrl, UriKind.Absolute, out Uri identityServerUri))
            {
                Console.Error.WriteLine("Error: Invalid Identity Server URL: " + identityServerUrl);
                Environment.Exit(1);
            }

            Console.WriteLine("Authenticating with identity server (Review rights required)...");
            try
            {
                var bearerToken = await VolumeAuthHelper.RequestVolumeBearerTokenAsync(
                    options.Username,
                    options.Password,
                    volumeName,
                    identityApiUrl,
                    identityServerUri,
                    requireReviewRights: true);

                TokenInjector.BearerToken = bearerToken;
                TokenInjector.BearerTokenAuthority = identityServerUri.ToString();
            }
            catch (Exception ex)
            {
                var message = TokenErrorHelper.ToExceptionMessage(ex);
                Console.Error.WriteLine("Authentication failed: " + message);
                Environment.Exit(1);
            }

            ConsoleProgressReporter progressReporter = new();
            Progress<ProgressInfo> progress = new();
            progress.ProgressChanged += progressReporter.OnReport;

            State.Volume = await Volume.CreateAsync(options.VolumeURL, State.CachePath, progress, CancellationToken.None);

            State.MappingsManager = new MappingManager(State.Volume);

            Console.Write($"Endpoint: {State.Volume.Endpoint.EndpointURL}");

            WebAnnotationModel.State.Endpoint = State.Volume.Endpoint.EndpointURL;
            WebAnnotationModel.State.UserCredentials = credentials;
            InitializeAnnotationStores(WebAnnotationModel.State.Endpoint);

            //Preload all structures
            Console.WriteLine("Begin preload all structures");
            await Store.Structures.GetAll();
            Console.WriteLine("Finished loading all structures");

            IList<long> SectionsToProcess = options.Sections.Count == 0
                ? [.. State.Volume.Sections.Values.Select(s => (long)s.Number)]
                : [.. options.Sections.Where(sectionNumber => State.Volume.Sections.ContainsKey((int)sectionNumber))];

            //OK.  Figure out which command we are executing.
            await UpdateVolumePositionsAsync(SectionsToProcess, numThreads, CancellationToken.None);
            //UpdateVolumePositions(SectionsToProcess);

            System.GC.Collect();
        }

        /*
        static async Task UpdateVolumePositions(IList<long> SectionNumbers)
        {
            SortedDictionary<long, Task<string>> tasks = new SortedDictionary<long, Task<string>>();
            foreach (long sectionNumber in SectionNumbers)
            {
                string result = UpdateVolumePositions(sectionNumber);
                Console.WriteLine(result);
                State.MappingsManager.SectionMappingCache.Remove((int)sectionNumber);
            }
        }
        */

        static void ReportTaskStatus(Task<string> task)
        {
            if (task.IsFaulted)
            {
                Console.WriteLine("Task faulted: " + task.Exception);
            }
            else if (task.IsCanceled)
            {
                Console.WriteLine("Task canceled");
            }
            else
            {
                Console.WriteLine(task.Result);
            }
        }

        static async Task UpdateVolumePositionsAsync(IList<long> SectionNumbers, int NumThreads, CancellationToken token)
        {
            //SortedDictionary<long, Task<string>> tasks = new SortedDictionary<long, Task<string>>();


            using System.Threading.SemaphoreSlim concurrencySemaphore = new(NumThreads); //))
            List<Task<string>> tasks = new(SectionNumbers.Count);

            foreach (long sectionNumber in SectionNumbers)
            {
                //    UpdateVolumePositions(sectionNumber);

                var task = Task.Run(() => UpdateVolumePositionsOnSectionAsync(sectionNumber, concurrencySemaphore, token), token);
                _ = task.ContinueWith((t) => Console.WriteLine(t.Result), TaskContinuationOptions.OnlyOnFaulted);
                tasks.Add(task);
                //var task = System.Threading.Tasks.Task.Run();
                //tasks.Add(sectionNumber, task);

                /*while (tasks.Keys.Count > 2)
                {
                    RemoveCompletedTasks(tasks);
                }*/
            }

            Task[] taskArray = [.. tasks.Cast<Task>()];
            Task.WaitAll(taskArray, token);

            /*
            while (tasks.Count > 0)
            {
                Task<string>[] taskArray = tasks.ToArray();
                int iTask = Task.WaitAny(taskArray);
                var finishedTask = tasks[iTask];
                var sectionNumber = taskSectionNumbers[iTask];

                tasks.RemoveAt(iTask);
                taskSectionNumbers.RemoveAt(iTask);
                string result = finishedTask.Result;

                Console.WriteLine(result);
                State.MappingsManager.SectionMappingCache.Remove((int)sectionNumber);
            }
            */

            /*
            foreach (long sectionNumber in tasks.Keys.ToArray())
            {
                var task = tasks[sectionNumber];
                task.Wait();
                Console.WriteLine(task.Result);
                tasks.Remove(sectionNumber);

                State.MappingsManager.SectionMappingCache.Remove((int)sectionNumber);
            }
            */
        }

        private static async Task<string> UpdateVolumePositionsOnSectionAsync(long sectionNumber, SemaphoreSlim concurrencySemaphore,
            CancellationToken token)
        {
            try
            {
                await concurrencySemaphore.WaitAsync(token).ConfigureAwait(false);
                if (token.IsCancellationRequested)
                    return null;

                string result = await UpdateSectionPositions(sectionNumber, token).ConfigureAwait(false);

                try
                {
                    await ConsoleLock.WaitAsync(token).ConfigureAwait(false);
                    Console.WriteLine(result);
                }
                finally
                {
                    ConsoleLock.Release();
                }

                State.MappingsManager.SectionMappingCache.Remove((int)sectionNumber);
                return result;
            }
            finally
            {
                concurrencySemaphore.Release();
            }
        }

        static string BuildSectionFeedbackString(long sectionNumber, long numUpdated, long numTotal, SectionTranslation? translation)
        {
            string output = $"Section {sectionNumber} : {numUpdated} of {numTotal} locations needed updates.";
            if (translation.HasValue)
            {
                output += $" Translated {translation.Value.Offset.X},{translation.Value.Offset.Y} before {translation.Value.TranslateBefore}";
            }

            return output;
        }

        // Bounds large enough to cover any real volume's coordinate space when requesting every
        // location on a section.  QueryTargets.Server always contacts the server for a full refresh.
        private static readonly Geometry.Rectangle WholeVolumeBounds = new(-1e9, 1e9, -1e9, 1e9);

        static async Task<string> UpdateSectionPositions(long SectionNumber, CancellationToken token)
        {
            int NumUpdated = 0;

            List<LocationObj> LocDict = await Store.LocationsByRegion.GetObjectsInRegionAsync(
                WholeVolumeBounds, 0, (int)SectionNumber, QueryTargets.Server, token, null);

            string feedback = null;
            if (LocDict.Count >= 0)
            {
                Viking.VolumeModel.Section section = State.Volume.Sections[(int)SectionNumber];

                MappingBase mapper = State.MappingsManager.GetMapping(State.Volume.DefaultVolumeTransform, (int)SectionNumber, section.DefaultChannel, section.DefaultPyramidTransform) ?? throw new Exception("No mapping found for section " + SectionNumber.ToString());
                await mapper.Initialize(token);

                SectionTranslation? translationData = null;
                if (SectionTranslations.TryGetValue(SectionNumber, out var sectionTranslationData))
                    translationData = sectionTranslationData;

                foreach (LocationObj loc in LocDict)
                {
                    try
                    {
                        bool result = UpdateVolumeShape(loc, mapper, translationData);
                        if (result)
                            NumUpdated++;
                    }
                    catch (ArgumentException e)
                    {
                        Console.WriteLine($"Location {loc.ID} could not be updated.  {e}");
                    }
                }

                feedback = BuildSectionFeedbackString(SectionNumber, NumUpdated, LocDict.Count, translationData);
            }
            else
            {
                feedback = $"Section {SectionNumber} : No locations found";
            }

            if (NumUpdated > 0)
            {
                try
                {
                    if (await Store.Locations.Save() == false)
                        feedback += $"\nSection {SectionNumber} : Failed to apply updates";
                }
                catch (Exception e)
                {
                    throw;
                    //Trace.WriteLine($"Exception saving volume locations:\n{e}");
                    //feedback += $"\nSection {SectionNumber} : Failed to apply updates with error{e}";
                    //Console.Write("...Locations updated");
                }
            }

            // TODO: Store.Locations is a shared, process-wide cache in the gRPC store model (unlike the old
            // per-thread WCF LocationStore), so we no longer evict a single section's objects here.  Revisit
            // if memory usage becomes a problem when processing many sections in parallel.

            return feedback;
        }

        /// <summary>
        /// Returns true if the new volume shape is significantly different than the old one
        /// </summary>
        /// <param name="Location"></param>
        /// <param name="mapping"></param>
        /// <returns></returns>
        static bool UpdateVolumeShape(LocationObj loc, MappingBase mapper, SectionTranslation? translation)
        {
            bool TypeUpdated = false;
            bool Translated = false;
            if (!IsLocationTypeValid(loc))
            {
                if (TryRepairLocationType(loc))
                {
                    Console.WriteLine($"Repaired Type for Location {loc.ID}");
                    TypeUpdated = true;
                }
                else
                    Console.WriteLine($"Unable to repair type for Location {loc.ID}");
            }

            SqlGeometry updatedVolumeShape = VolumeShapeForLocation(loc, mapper);
            if (updatedVolumeShape is null)
            {
                Console.WriteLine("Could not map location ID : " + loc.ID.ToString());
                return false;
            }

            //Translate if needed
            if (translation is not null)
            {
                if (loc.LastModified < translation.Value.TranslateBefore)
                {
                    updatedVolumeShape = updatedVolumeShape.Translate(translation.Value.Offset);
                    loc.MosaicShape = mapper.TryMapShapeVolumeToSection(updatedVolumeShape).ToShape2D();
                    Translated = true;
                }
            }

            if (!updatedVolumeShape.STIsValid())
            {
                Console.WriteLine($"Location {loc.ID} invalid : {updatedVolumeShape.IsValidDetailed()} ");
                return false;
            }

            Vector2[] OriginalVolumeControlPoints = loc.VolumeShape.ToPoints();
            Vector2[] UpdatedVolumeControlPoints = updatedVolumeShape.ToPoints();

            if (AnyPointsAreDifferent(OriginalVolumeControlPoints, UpdatedVolumeControlPoints) ||
                updatedVolumeShape.GeometryType() != loc.VolumeShape.ToSqlGeometry().GeometryType())
            {
                loc.VolumeShape = updatedVolumeShape.ToShape2D();
                return true;
            }

            return TypeUpdated || Translated;
        }

        static Vector2[] MosaicPointsForLocation(LocationObj loc)
        {
            Vector2[] mosaicPoints = loc.TypeCode switch
            {
                LocationType.POINT or LocationType.CIRCLE => [loc.Position],
                LocationType.POLYGON or LocationType.POLYLINE or LocationType.OPENCURVE or LocationType.CLOSEDCURVE or LocationType.CURVEPOLYGON => loc.MosaicShape.ToPoints(),
                _ => loc.MosaicShape.ToPoints(),
            };
            return mosaicPoints;
        }

        static SqlGeometry VolumeShapeForLocation(LocationObj loc, MappingBase mapper)
        {
            SqlGeometry UnsmoothedVolumeShape = mapper.TryMapShapeSectionToVolume(loc.MosaicShape.ToSqlGeometry());
            if (UnsmoothedVolumeShape is null)
                return null;

            //Check a rare case where points are stored as circles 
            SqlGeometry SmoothedVolumeShape = loc.TypeCode.GetSmoothedShape(UnsmoothedVolumeShape);
            return SmoothedVolumeShape;
        }

        /// <summary>
        /// Returns true if the MosaicShape geometry can represent the location's typecode
        /// </summary>
        /// <returns></returns>
        static bool IsLocationTypeValid(LocationObj loc)
        {
            switch (loc.MosaicShape.ToSqlGeometry().GeometryType())
            {
                case SupportedGeometryType.POINT:
                    if (loc.TypeCode != LocationType.POINT)
                        return false;
                    break;
                case SupportedGeometryType.CURVEPOLYGON:
                    if (loc.TypeCode != LocationType.CIRCLE)
                        return false;
                    break;
                case SupportedGeometryType.POLYLINE:
                    if (loc.TypeCode != LocationType.POLYLINE &&
                       loc.TypeCode != LocationType.OPENCURVE &&
                       loc.TypeCode != LocationType.CLOSEDCURVE)
                        return false;
                    break;
                case SupportedGeometryType.POLYGON:
                    if (loc.TypeCode != LocationType.POLYGON &&
                        loc.TypeCode != LocationType.CURVEPOLYGON)
                        return false;
                    break;
                default:
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Return true if the location type was repaired
        /// </summary>
        /// <param name="loc"></param>
        /// <returns></returns>
        static bool TryRepairLocationType(LocationObj loc)
        {
            switch (loc.MosaicShape.ToSqlGeometry().GeometryType())
            {
                case SupportedGeometryType.POINT:
                    if (loc.TypeCode != LocationType.POINT)
                    {
                        loc.TypeCode = LocationType.POINT;
                        return true;
                    }
                    break;
                case SupportedGeometryType.CURVEPOLYGON:
                    if (loc.TypeCode != LocationType.CIRCLE)
                    {
                        loc.TypeCode = LocationType.CIRCLE;
                        return true;
                    }
                    break;
                case SupportedGeometryType.POLYLINE:
                    if (loc.TypeCode == LocationType.CIRCLE)
                    {
                        loc.TypeCode = LocationType.POLYLINE;
                        loc.Width = 8.0;
                        return true;
                    }
                    //Convert a polyline to a polygon to match the location typecode
                    if (loc.TypeCode == LocationType.POLYGON || loc.TypeCode == LocationType.CURVEPOLYGON)
                    {
                        SqlGeometry newShape = loc.MosaicShape.ToPoints().ToPolygon();
                        if (newShape.STIsValid().IsTrue)
                        {
                            loc.MosaicShape = newShape.ToShape2D();
                            loc.Width = new long?();
                            return true;
                        }

                        return false;

                    }
                    break;
                case SupportedGeometryType.POLYGON:
                    if (loc.TypeCode == LocationType.CLOSEDCURVE || loc.TypeCode == LocationType.POLYLINE)
                    {
                        SqlGeometry newShape = loc.MosaicShape.ToPoints().ToSqlGeometry();
                        if (newShape.STIsValid().IsTrue)
                        {
                            loc.MosaicShape = newShape.ToShape2D();
                            loc.Width = 8;
                            return true;
                        }

                        return false;
                    }
                    break;
                default:
                    return false;
            }

            return false;
        }



        static async Task<(string volumeName, Uri identityApiUrl)> LoadAndParseVolumeXmlAsync(string volumeUrl, NetworkCredential credentials, string identityServerUrlFallback)
        {
            var xmlDoc = await Volume.LoadXDocumentAsync(volumeUrl, CancellationToken.None, credentials);

            var volumeElement = Volume.GetVolumeElement(xmlDoc);
            if (volumeElement is null)
                throw new Exception("Volume element not found in XML");

            var volumeName = volumeElement.Attributes()
                .FirstOrDefault(a => string.Compare(a.Name.LocalName, "name", StringComparison.OrdinalIgnoreCase) == 0)
                ?.Value;

            if (string.IsNullOrEmpty(volumeName))
                throw new Exception("Volume name not found in XML");

            Uri identityApiUrl = null;
            var endpointElement = volumeElement.Elements()
                .FirstOrDefault(d => string.Equals(d.Name.LocalName, "VolumeToEndpoint", StringComparison.OrdinalIgnoreCase));

            if (endpointElement != null)
            {
                var identityApiAttr = endpointElement.Attributes()
                    .FirstOrDefault(a => a.Name.LocalName == "IdentityApi")?.Value;

                if (!string.IsNullOrEmpty(identityApiAttr))
                    Uri.TryCreate(identityApiAttr, UriKind.Absolute, out identityApiUrl);

                if (identityApiUrl is null)
                {
                    var authAttr = endpointElement.Attributes()
                        .FirstOrDefault(a => a.Name.LocalName == "Authentication")?.Value;

                    if (!string.IsNullOrEmpty(authAttr))
                        Uri.TryCreate(authAttr, UriKind.Absolute, out identityApiUrl);
                }
            }

            Uri identityServerUrl = null;
            if (!string.IsNullOrEmpty(identityServerUrlFallback))
                Uri.TryCreate(identityServerUrlFallback, UriKind.Absolute, out identityServerUrl);

            identityApiUrl = IdentityEndpoints.ResolvePermissionsApiUrl(identityApiUrl, identityServerUrl);

            if (identityApiUrl is null)
                throw new Exception("Could not determine Identity API URL from volume XML or --identity-server-url");

            return (volumeName, identityApiUrl);
        }

        static bool AnyPointsAreDifferent(Vector2[] Original, Vector2[] New, double epsilonSquared = 0.25)
        {
            if (Original.Length != New.Length)
                return true;

            //Any with index is not available in this language version, so we have to do it the wordy way
            //return Original.Any((p, i) => Vector2.DistanceSquared(p, New[i]) > epsilonSquared);

            for (int i = 0; i < New.Length; i++)
            {
                if (Vector2.DistanceSquared(Original[i], New[i]) > epsilonSquared)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Compose gRPC-backed annotation stores and bind them to <see cref="Store"/>.
        /// </summary>
        static void InitializeAnnotationStores(Uri endpoint)
        {
            if (endpoint is null)
                throw new ArgumentNullException(nameof(endpoint));

            var services = new ServiceCollection();
            services.AddSingleton<IAnnotationAccessTokenProvider, VikingAuAnnotationAccessTokenProvider>();
            services.ConfigureAnnotationModel(
                opts => opts.Endpoint = endpoint,
                channelOpts =>
                {
#if NETFRAMEWORK
                    channelOpts.HttpHandler = CreateWinHttpHandler();
#else
                    _ = channelOpts;
#endif
                });

            var serviceProvider = services.BuildServiceProvider();
            var grpcSettings = serviceProvider.GetRequiredService<IOptions<GrpcRepositorySettings>>();
            grpcSettings.Value.Endpoint = endpoint;

            Store.Initialize(serviceProvider.GetRequiredService<IAnnotationStores>());
        }

#if NETFRAMEWORK
        /// <summary>
        /// WinHttpHandler is required for Grpc.Net.Client on .NET Framework and only
        /// supports gRPC over TLS. Accept the Docker localhost dev cert on loopback.
        /// </summary>
        static WinHttpHandler CreateWinHttpHandler()
        {
            return new WinHttpHandler
            {
                EnableMultipleHttp2Connections = true,
                ServerCertificateValidationCallback = (request, certificate, chain, errors) =>
                {
                    if (errors == System.Net.Security.SslPolicyErrors.None)
                        return true;

                    var host = request?.RequestUri?.Host;
                    return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
                        || host == "127.0.0.1"
                        || host == "::1";
                }
            };
        }
#endif

        private sealed class VikingAuAnnotationAccessTokenProvider : IAnnotationAccessTokenProvider
        {
            public string GetAccessToken() => TokenInjector.BearerToken?.AccessToken;
        }
    }
}
