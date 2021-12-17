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

        [Option('u', "username", DefaultValue = "Anonymous", Required = false, HelpText = "URL of VolumeXML file")]
        public string Username { get; set; }

        [Option('p', "password", DefaultValue = "connectome", Required = false, HelpText = "URL of VolumeXML file")]
        public string Password { get; set; }

        [Option('c', "closed_interpolation_points", DefaultValue = 10, Required = false, HelpText = "Number of closed curve interpolation points")]
        public int NumClosedInterpolationPoints { get; set; }

        [Option('o', "open_interpolation_points", DefaultValue = 3, Required = false, HelpText = "Number of open curve interpolation points")]
        public int NumOpenInterpolationPoints { get; set; }

        [Option('s', "sections", DefaultValue = null, Required = false, HelpText = "Section Numbers to update")]
        public string SectionNumbersString { get; set; }

        public IList<long> Sections
        {
            get
            {
                if (this.SectionNumbersString == null)
                    return new List<long>();
                else
                    return NumberRangeToList(this.SectionNumbersString);
            }
        }

        [HelpOption]
        public string GetUsage()
        {
            return CommandLine.Text.HelpText.AutoBuild(this,
                (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
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
                listNumbers = new List<long>();
                listNumbers.Add(SectionNumber);
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
            string Details = string.Format("{0}% {1}", ProgressPercentage, message);
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
        static CommandLineOptions options = new CommandLineOptions();

        static async Task Main(string[] args)
        {
            if (!CommandLine.Parser.Default.ParseArguments(args, Program.options))
            {
                System.Console.WriteLine("Unable to parse command line arguments, aborting");
                return;
            }

            System.Data.Entity.SqlServer.SqlProviderServices.SqlServerTypesAssemblyName = "Microsoft.SqlServer.Types, Version=14.0.0.0, Culture=neutral, PublicKeyToken=89845dcd8080cc91";
            //SqlServerTypes.Utilities.LoadNativeAssemblies(AppDomain.CurrentDomain.BaseDirectory);

            ConsoleProgressReporter progressReporter = new AU.ConsoleProgressReporter();

            State.Volume = new Volume(Program.options.VolumeURL, State.CachePath, progressReporter);

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
            await UpdateVolumePositionsAsync(SectionsToProcess, CancellationToken.None);
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

        static async Task UpdateVolumePositionsAsync(IList<long> SectionNumbers, CancellationToken token)
        {
            //SortedDictionary<long, Task<string>> tasks = new SortedDictionary<long, Task<string>>();
            object LockConsole = new object();

            using (System.Threading.SemaphoreSlim concurrencySemaphore = new System.Threading.SemaphoreSlim(System.Environment.ProcessorCount * 2))
            {
                List<Task<string>> tasks = new List<Task<string>>(SectionNumbers.Count);
                List<long> taskSectionNumbers = new List<long>(SectionNumbers.Count);
                 
                foreach (long sectionNumber in SectionNumbers)
                {
                    //    UpdateVolumePositions(sectionNumber);
                    await concurrencySemaphore.WaitAsync(token);
                    if (token.IsCancellationRequested)
                        return;

                    taskSectionNumbers.Add(sectionNumber);
                    var task = Task.Run(async () =>
                    {
                        try
                        {
                            string result =  await UpdateSectionPositions(sectionNumber, token);
                            
                            lock (LockConsole)
                            {
                                Console.WriteLine(result);
                            }

                            State.MappingsManager.SectionMappingCache.Remove((int)sectionNumber);
                            return result;
                        }
                        finally
                        {
                            concurrencySemaphore.Release();
                        }
                    }
                    );

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

        private static void RemoveCompletedTasks(SortedDictionary<long, Task<string>> tasks)
        {
            bool AnyCompleted = false;
            foreach (long sectionNumber in tasks.Keys.ToArray())
            {
                var task = tasks[sectionNumber];
                if (task.IsCompleted)
                {
                    if (task.Result != null)
                        Console.WriteLine(task.Result);
                    else
                        Console.WriteLine("Task had null result");

                    tasks.Remove(sectionNumber);
                    AnyCompleted = true;
                    State.MappingsManager.SectionMappingCache.Remove((int)sectionNumber);
                }
            }

            if (!AnyCompleted)
            {
                System.Threading.Thread.Sleep(500);
            }
        }

        static async Task<string> UpdateSectionPositions(long SectionNumber, CancellationToken token)
        {
            LocationStore threadLocationStore = new LocationStore();
            int NumUpdated = 0;

            var LocDict = threadLocationStore.GetObjectsForSection(SectionNumber);

            if(LocDict.Count >= 0)
            {
                Viking.VolumeModel.Section section = State.Volume.Sections[(int)SectionNumber];

                MappingBase mapper = State.MappingsManager.GetMapping(State.Volume.DefaultVolumeTransform, (int)SectionNumber, section.DefaultChannel, section.DefaultPyramidTransform);
                if (mapper == null)
                {
                    throw new Exception("No mapping found for section " + SectionNumber.ToString());
                }

                await mapper.Initialize(token);

                foreach (LocationObj loc in LocDict.Values)
                {
                    bool result = UpdateVolumeShape(loc, mapper);
                    if (result)
                        NumUpdated++;
                }
            } 
             
            if (NumUpdated > 0)
            {
                try
                {
                    threadLocationStore.Save();
                }
                catch (System.ServiceModel.FaultException e)
                {
                    Trace.WriteLine(string.Format("Exception saving volume locations:\n{0}", e));
                    //Console.Write("...Locations updated");
                }
            }

            string Result = string.Format("Section {0} : {1} / {2} locations needed updates", SectionNumber, NumUpdated, LocDict.Count);

            threadLocationStore.RemoveSection((int)SectionNumber);

            return Result;
        }

        /// <summary>
        /// Returns true if the new volume shape is significantly different than the old one
        /// </summary>
        /// <param name="Location"></param>
        /// <param name="mapping"></param>
        /// <returns></returns>
        static bool UpdateVolumeShape(LocationObj loc, MappingBase mapper)
        {
            bool TypeUpdated = false;
            if (!IsLocationTypeValid(loc))
            {
                if (TryRepairLocationType(loc))
                {
                    Console.WriteLine(string.Format("Repaired Type for Location {0}", loc.ID));
                    TypeUpdated = true;
                }
                else
                    Console.WriteLine(string.Format("Unable to repair type for Location {0}", loc.ID));
            }

            SqlGeometry updatedVolumeShape = VolumeShapeForLocation(loc, mapper);
            if (updatedVolumeShape == null)
            {
                Console.WriteLine("Could not map location ID : " + loc.ID.ToString());
                return false;
            }
            if (!updatedVolumeShape.STIsValid())
            {
                Console.WriteLine(string.Format("Location {0} invalid : {1} ", loc.ID, updatedVolumeShape.IsValidDetailed()));
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

            return TypeUpdated;
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
            if (UnsmoothedVolumeShape == null)
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

            return Original.Where((p, i) => GridVector2.DistanceSquared(p, New[i]) > epsilonSquared).Any();
        }
    }
}
