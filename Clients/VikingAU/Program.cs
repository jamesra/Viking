using Viking.AnnotationServiceTypes.Interfaces;
using CommandLine;
using CommandLine.Text;
using Geometry;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Viking.VolumeModel;
using WebAnnotationModel;
using System.Web;
using Viking.Common;

namespace Viking.AU
{
    partial class CommandLineOptions
    {
        [Option('v', "VolumeURL", Required = true, HelpText = "URL of VolumeXML file")]
        public string VolumeURL { get; set; }

        [Option('u', "username", Required = false, HelpText = "User name for the annotation server")]
        public string Username { get; set; } = "Anonymous";

        [Option('p', "password", Required = false, HelpText = "Password for the annotation server")]
        public string Password { get; set; } = "connectome";

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
        /// Convert a string of two integers separated by a hyphen to a list of integers
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

            System.Data.Entity.SqlServer.SqlProviderServices.SqlServerTypesAssemblyName = "Microsoft.SqlServer.Types, Version=16.0.0.0, Culture=neutral, PublicKeyToken=89845dcd8080cc91";
            SqlServerTypesLoader.Loader.LoadNativeAssemblies(AppDomain.CurrentDomain.BaseDirectory);

            ConsoleProgressReporter progressReporter = new();
            Progress<ProgressInfo> progress = new();
            progress.ProgressChanged += progressReporter.OnReport;

            CancellationTokenSource cancellationTokenSource = new();
            State.Volume = await Volume.CreateAsync(options.VolumeURL, State.CachePath, progress, CancellationToken.None);

            State.MappingsManager = new MappingManager(State.Volume);

            Console.Write($"Endpoint: {State.Volume.Endpoint.EndpointURL}");

            WebAnnotationModel.State.Endpoint = State.Volume.Endpoint.EndpointURL;
            WebAnnotationModel.State.UserCredentials = new System.Net.NetworkCredential(options.Username, options.Password);

            //Preload all structures
            Console.WriteLine("Begin preload all structures");
            Store.Structures.GetAllStructures();
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

        static async Task<string> UpdateSectionPositions(long SectionNumber, CancellationToken token)
        {
            LocationStore threadLocationStore = [];
            int NumUpdated = 0;

            var LocDict = threadLocationStore.GetObjectsForSection(SectionNumber);

            string feedback = null;
            if (LocDict.Count >= 0)
            {
                Viking.VolumeModel.Section section = State.Volume.Sections[(int)SectionNumber];

                MappingBase mapper = State.MappingsManager.GetMapping(State.Volume.DefaultVolumeTransform, (int)SectionNumber, section.DefaultChannel, section.DefaultPyramidTransform) ?? throw new Exception("No mapping found for section " + SectionNumber.ToString());
                await mapper.Initialize(token);

                SectionTranslation? translationData = null;
                if (SectionTranslations.TryGetValue(SectionNumber, out var sectionTranslationData))
                    translationData = sectionTranslationData;

                foreach (LocationObj loc in LocDict.Values)
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
                    if (threadLocationStore.Save() == false)
                        feedback += $"\nSection {SectionNumber} : Failed to apply updates";
                }
                catch (System.ServiceModel.FaultException e)
                {
                    throw;
                    //Trace.WriteLine($"Exception saving volume locations:\n{e}");
                    //feedback += $"\nSection {SectionNumber} : Failed to apply updates with error{e}";
                    //Console.Write("...Locations updated");
                }
                catch (Exception e)
                {
                    throw;
                    //Trace.WriteLine($"Exception saving volume locations:\n{e}");
                    //feedback += $"\nSection {SectionNumber} : Failed to apply updates with error{e}";
                    //Console.Write("...Locations updated");
                }
            }

            threadLocationStore.RemoveSection((int)SectionNumber);

            return feedback;
        }

        /// <summary>
        /// Sets the location's <c>VolumeShape</c> to the cache every Viking save writes, <see cref="VolumeCache.Shape"/> under
        /// <paramref name="mapper"/> (the volume's default transform), and returns true if it is significantly different
        /// than the old one, or the type was repaired or the location translated. A translation moves the location in volume
        /// space: the forward owner's geometry is moved, mapped back by the inverse owner and saved as the mosaic shape.
        /// See <c>.cursor/rules/annotation-volume-geometry.mdc</c>.
        /// </summary>
        static bool UpdateVolumeShape(LocationObj loc, MappingBase mapper, SectionTranslation? translation)
        {
            SqlGeometry originalVolumeShape = loc.VolumeShape;
            bool TypeUpdated = false;
            bool Translated = false;
            if (!IsLocationTypeValid(loc))
            {
                if (TryRepairLocationType(loc, mapper))
                {
                    Console.WriteLine($"Repaired Type for Location {loc.ID}");
                    TypeUpdated = true;
                }
                else
                    Console.WriteLine($"Unable to repair type for Location {loc.ID}");
            }

            //Translate if needed
            if (translation is not null && loc.LastModified < translation.Value.TranslateBefore)
            {
                if (TryTranslate(loc, mapper, translation.Value.Offset, out string failure))
                    Translated = true;
                else
                    Console.WriteLine($"Location {loc.ID} was not translated: {failure}");
            }

            SqlGeometry updatedVolumeShape;
            try
            {
                updatedVolumeShape = VolumeCache.Shape(loc, mapper);
            }
            catch (ArgumentException)
            {
                Console.WriteLine("Could not map location ID : " + loc.ID.ToString());
                return false;
            }

            if (!updatedVolumeShape.STIsValid())
            {
                Console.WriteLine($"Location {loc.ID} invalid : {updatedVolumeShape.IsValidDetailed()} ");
                return false;
            }

            if (originalVolumeShape is null ||
                AnyPointsAreDifferent(originalVolumeShape.ToPoints(), updatedVolumeShape.ToPoints()) ||
                updatedVolumeShape.GeometryType() != originalVolumeShape.GeometryType())
            {
                loc.VolumeShape = updatedVolumeShape;
                return true;
            }

            return TypeUpdated || Translated;
        }

        /// <summary>
        /// Moves the location by <paramref name="offset"/> in volume space: its geometry as the forward owner maps it, moved,
        /// mapped back to mosaic by <see cref="LocationVolumeGeometry.ToMosaic(Circle, IVolumeToSectionTransform)"/> and its
        /// overloads, and saved with <see cref="LocationObjExtensions.TryApply"/>. Lines keep the width they are drawn with.
        /// </summary>
        static bool TryTranslate(LocationObj loc, MappingBase mapper, Vector2 offset, out string failure)
        {
            LocationVolumeGeometry geometry = LocationVolumeGeometry.For(loc, mapper);
            if (!geometry.IsExact)
            {
                failure = "it lies partly outside the region the section's transform maps";
                return false;
            }

            MosaicGeometry mosaic = loc.TypeCode switch
            {
                LocationType.CIRCLE => LocationVolumeGeometry.ToMosaic(new Circle(geometry.Circle!.Value.Center + offset, geometry.Circle.Value.Radius), mapper),
                LocationType.POLYGON or LocationType.CURVEPOLYGON => LocationVolumeGeometry.ToMosaic(loc.TypeCode, geometry.ControlPolygon().Translate(offset), mapper),
                LocationType.POLYLINE or LocationType.OPENCURVE or LocationType.CLOSEDCURVE =>
                    LocationVolumeGeometry.ToMosaic(loc.TypeCode, geometry.ControlPoints.Select(p => p + offset).ToArray(), mapper, geometry.Width),
                _ => LocationVolumeGeometry.ToMosaic(loc.TypeCode, geometry.ControlPoints.Select(p => p + offset).ToArray(), mapper),
            };

            try
            {
                if (!loc.TryApply(mosaic, mapper))
                {
                    failure = mosaic.Failure;
                    return false;
                }
            }
            catch (ArgumentException e)
            {
                failure = e.Message;
                return false;
            }

            failure = null;
            return true;
        }

        /// <summary>
        /// Returns true if the MosaicShape geometry can represent the location's typecode
        /// </summary>
        /// <returns></returns>
        static bool IsLocationTypeValid(LocationObj loc)
        {
            switch (loc.MosaicShape.GeometryType())
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
        /// Saves a repaired mosaic shape through the owner (<see cref="LocationObjExtensions.SetShapeFromGeometryInSection"/>),
        /// which also derives its volume shape, with its new <paramref name="width"/>. False, with the location (its width
        /// included) unchanged, when the shape is invalid or unmappable.
        /// </summary>
        static bool TrySetRepairedShape(LocationObj loc, MappingBase mapper, SqlGeometry newShape, double? width)
        {
            if (!newShape.STIsValid().IsTrue)
                return false;

            // The width is set first because the cache is derived for it; a failed repair puts the old width back.
            double? oldWidth = loc.Width;
            loc.Width = width;
            try
            {
                loc.SetShapeFromGeometryInSection(mapper, newShape);
                return true;
            }
            catch (ArgumentException e)
            {
                loc.Width = oldWidth;
                Console.WriteLine($"Location {loc.ID}: the repaired shape could not be saved: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Return true if the location type was repaired
        /// </summary>
        /// <param name="loc"></param>
        /// <returns></returns>
        static bool TryRepairLocationType(LocationObj loc, MappingBase mapper)
        {
            switch (loc.MosaicShape.GeometryType())
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
                        return TrySetRepairedShape(loc, mapper, newShape, null);

                    }
                    break;
                case SupportedGeometryType.POLYGON:
                    if (loc.TypeCode == LocationType.CLOSEDCURVE || loc.TypeCode == LocationType.POLYLINE)
                    {
                        SqlGeometry newShape = loc.MosaicShape.ToPoints().ToSqlGeometry();
                        return TrySetRepairedShape(loc, mapper, newShape, 8);
                    }
                    break;
                default:
                    return false;
            }

            return false;
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
    }
}
