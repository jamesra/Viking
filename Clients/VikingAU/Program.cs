using Viking.AnnotationServiceTypes.Interfaces;
using CommandLine;
using CommandLine.Text;
using Geometry;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Viking.VolumeModel;
using WebAnnotationModel;

namespace Viking.AU
{
    class CommandLineOptions
    {
        [Option('v', "VolumeURL", Required = true, HelpText = "URL of VolumeXML file")]
        public string VolumeURL { get; set; }

        [Option('u', "username", Required = false, HelpText = "URL of VolumeXML file")]
        public string Username { get; set; } = "Anonymous";

        [Option('p', "password",Required = false, HelpText = "URL of VolumeXML file")]
        public string Password { get; set; } = "connectome";

        [Option('c', "closed_interpolation_points", Required = false, HelpText = "Number of closed curve interpolation points")]
        public int NumClosedInterpolationPoints { get; set; } = 10;

        [Option('o', "open_interpolation_points", Required = false, HelpText = "Number of open curve interpolation points")]
        public int NumOpenInterpolationPoints { get; set; } = 3;

        [Option('s', "sections", Required = false,  HelpText = "Section Numbers to update")]
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
                    return new List<long>();
                else
                    return NumberRangeToList(this.SectionNumbersString);
            }
        }


        private static bool IsNumberRange(string input)
        {
            Regex regex = new Regex(@"(\d+)\-(\d+)");
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
                listNumbers = new List<long>
                {
                    SectionNumber
                };
                return listNumbers;
            }
            catch (FormatException e)
            {
                Regex regex = new Regex(@"(\d+)\-(\d+)");
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
            List<long> listNumbers = new List<long>();

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

    }

    class ConsoleProgressReporter : Viking.Common.IProgressReporter
    {
        int LastLineLength = 0;

        public void ReportProgress(double ProgressPercentage, string message)
        {

            StringBuilder output = new StringBuilder();
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

        public void TaskComplete()
        {
            Console.WriteLine("Task Complete");
        }
    }

    class Program
    { 
        private static readonly SemaphoreSlim ConsoleLock = new SemaphoreSlim(1);

        static SectionTranslations SectionTranslations = new SectionTranslations();
         

        static async Task Main(string[] args)
        {  
            var parse_result = await CommandLine.Parser.Default.ParseArguments<CommandLineOptions>(args) 
                .WithParsedAsync(RunAsync);
             
        }
        //static void ShowErrors(IEnumerable<Error> errors)
        //{ 
        //    System.Console.WriteLine(help);
        //    System.Console.ForegroundColor = ConsoleColor.Red;
        //    System.Console.WriteLine("Unable to parse command line arguments, aborting");
        //    foreach(Error e in errors)
        //    {
        //        Console.WriteLine(e.ToString());
        //    }
        //    System.Console.ResetColor();
        //    return; 
        //}
        
        
        static async Task RunAsync(CommandLineOptions options)
        { 
            
            if (options.TranslateFile != null)
            {
                SectionTranslations = SectionTranslations.CreateFromConfigFile(options.TranslateFile);
            }   

            int numThreads = options.NumThreads ?? System.Environment.ProcessorCount + 1;

            System.Data.Entity.SqlServer.SqlProviderServices.SqlServerTypesAssemblyName = "Microsoft.SqlServer.Types, Version=14.0.0.0, Culture=neutral, PublicKeyToken=89845dcd8080cc91";
            SqlServerTypes.Utilities.SqlServerTypesUtilities.LoadNativeAssemblies(AppDomain.CurrentDomain.BaseDirectory);

            ConsoleProgressReporter progressReporter = new AU.ConsoleProgressReporter();

            State.Volume = new Volume(options.VolumeURL, State.CachePath, progressReporter);

            var cancellationTokenSource = new CancellationTokenSource();
            await State.Volume.Initialize(cancellationTokenSource.Token, progressReporter);

            State.MappingsManager = new MappingManager(State.Volume);

            Console.Write("Endpoint: " + State.Volume.Endpoint.EndpointURL.ToString());

            WebAnnotationModel.State.Endpoint = State.Volume.Endpoint.EndpointURL;
            WebAnnotationModel.State.UserCredentials = new System.Net.NetworkCredential(options.Username, options.Password);

            //Preload all structures
            Console.WriteLine("Begin preload all structures");
            Store.Structures.GetAllStructures();
            Console.WriteLine("Finished loading all structures");

            IList<long> SectionsToProcess;

            if (options.Sections.Count == 0)
                SectionsToProcess = State.Volume.Sections.Values.Select(s => (long)s.Number).ToList();
            else
            {
                SectionsToProcess = options.Sections.Where(sectionNumber => State.Volume.Sections.ContainsKey((int)sectionNumber)).ToList();

            }

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
            

            using (System.Threading.SemaphoreSlim concurrencySemaphore = new System.Threading.SemaphoreSlim(NumThreads)) //))
            {
                List<Task<string>> tasks = new List<Task<string>>(SectionNumbers.Count); 
                 
                foreach (long sectionNumber in SectionNumbers)
                {
                    //    UpdateVolumePositions(sectionNumber);

                    var task = Task.Run(() => UpdateVolumePositionsOnSectionAsync(sectionNumber, concurrencySemaphore, token),token);
                    task.ContinueWith((t) => Console.WriteLine(t.Result), TaskContinuationOptions.OnlyOnFaulted);
                    tasks.Add(task);
                    //var task = System.Threading.Tasks.Task.Run();
                    //tasks.Add(sectionNumber, task);

                    /*while (tasks.Keys.Count > 2)
                    {
                        RemoveCompletedTasks(tasks);
                    }*/
                }

                Task[] taskArray = tasks.Cast<Task>().ToArray();
                Task.WaitAll(taskArray);

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
            }

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
            if(translation.HasValue)
            {
                output += $" Translated {translation.Value.Offset.X},{translation.Value.Offset.Y} before {translation.Value.TranslateBefore}";
            }

            return output;
        }
         
        static async Task<string> UpdateSectionPositions(long SectionNumber, CancellationToken token)
        {
            LocationStore threadLocationStore = new LocationStore();
            int NumUpdated = 0;

            var LocDict = threadLocationStore.GetObjectsForSection(SectionNumber);

            string feedback = null;
            if(LocDict.Count >= 0)
            {
                Viking.VolumeModel.Section section = State.Volume.Sections[(int)SectionNumber];

                MappingBase mapper = State.MappingsManager.GetMapping(State.Volume.DefaultVolumeTransform, (int)SectionNumber, section.DefaultChannel, section.DefaultPyramidTransform) ?? throw new Exception("No mapping found for section " + SectionNumber.ToString());
                await mapper.Initialize(token);

                SectionTranslation? translationData = null;
                if(SectionTranslations.TryGetValue(SectionNumber, out var sectionTranslationData))
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
                    if(threadLocationStore.Save() == false)
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
            if(!(translation is null))
            {
                if(loc.LastModified < translation.Value.TranslateBefore)
                { 
                    updatedVolumeShape = updatedVolumeShape.Translate(translation.Value.Offset);
                    loc.MosaicShape = mapper.TryMapShapeVolumeToSection(updatedVolumeShape);
                    Translated = true;
                }
            }

            if (!updatedVolumeShape.STIsValid())
            {
                Console.WriteLine($"Location {loc.ID} invalid : {updatedVolumeShape.IsValidDetailed()} ");
                return false;
            }

            GridVector2[] OriginalVolumeControlPoints = loc.VolumeShape.ToPoints();
            GridVector2[] UpdatedVolumeControlPoints = updatedVolumeShape.ToPoints();

            if (AnyPointsAreDifferent(OriginalVolumeControlPoints, UpdatedVolumeControlPoints) ||
                updatedVolumeShape.GeometryType() != loc.VolumeShape.GeometryType())
            {
                loc.VolumeShape = updatedVolumeShape;
                return true;
            }

            return TypeUpdated || Translated;
        }

        static GridVector2[] MosaicPointsForLocation(LocationObj loc)
        {
            GridVector2[] mosaicPoints;
            switch (loc.TypeCode)
            {
                case LocationType.POINT:
                case LocationType.CIRCLE:
                    mosaicPoints = new GridVector2[] { loc.Position };
                    break;
                case LocationType.POLYGON:
                case LocationType.POLYLINE:
                case LocationType.OPENCURVE:
                case LocationType.CLOSEDCURVE:
                case LocationType.CURVEPOLYGON:
                    mosaicPoints = loc.MosaicShape.ToPoints();
                    break;
                default:
                    mosaicPoints = loc.MosaicShape.ToPoints();
                    break;
            }

            return mosaicPoints;
        }

        static SqlGeometry VolumeShapeForLocation(LocationObj loc, MappingBase mapper)
        {
            SqlGeometry UnsmoothedVolumeShape = mapper.TryMapShapeSectionToVolume(loc.MosaicShape);
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
        /// Return true if the location type was repaired
        /// </summary>
        /// <param name="loc"></param>
        /// <returns></returns>
        static bool TryRepairLocationType(LocationObj loc)
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
                        if (newShape.STIsValid().IsTrue)
                        {
                            loc.MosaicShape = newShape;
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
                            loc.MosaicShape = newShape;
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



        static bool AnyPointsAreDifferent(GridVector2[] Original, GridVector2[] New, double epsilonSquared = 0.25)
        {
            if (Original.Length != New.Length)
                return true;

            //Any with index is not available in this language version, so we have to do it the wordy way
            //return Original.Any((p, i) => GridVector2.DistanceSquared(p, New[i]) > epsilonSquared);

            for (int i = 0; i < New.Length; i++)
            {
                if (GridVector2.DistanceSquared(Original[i], New[i]) > epsilonSquared)
                    return true;
            }
             
            return false;
        }
    }
}
