using System;
using System.Text.RegularExpressions;

using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;
using Viking.VolumeModel;
using System.Diagnostics;
using WebAnnotationModel; 
using Geometry;
using SqlGeometryUtils;
using Microsoft.SqlServer.Types;

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

        [ParserState]
        public IParserState LastParsertState { get; set; }

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
            
            Regex regex = new Regex(@"(\d+)\-(\d+)");
            Match m = regex.Match(input);

            long start = System.Convert.ToInt64(m.Groups[1].Value);
            long end = System.Convert.ToInt64(m.Groups[2].Value);

            List<long> listNumbers = new List<long>((int)(end - start)+1);

            for (long val = start; val <= end; val++)
            {
                listNumbers.Add(val);
            }

            return listNumbers;
        }

        private static List<long> NumberStringToList(string input)
        {
            List<long> listNumbers = new List<long>();

            foreach(string chunk in input.Split(','))
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

        static void Main(string[] args)
        { 
            if (!CommandLine.Parser.Default.ParseArguments(args, Program.options))
            {
                System.Console.WriteLine("Unable to parse command line arguments, aborting");
                return;
            }

            ConsoleProgressReporter progressReporter = new AU.ConsoleProgressReporter();

            State.Volume = new Volume(Program.options.VolumeURL, State.CachePath, progressReporter);

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
            UpdateVolumePositions(SectionsToProcess);

            System.GC.Collect();
        }

        static void UpdateVolumePositions(IList<long> SectionNumbers)
        {
            SortedDictionary<long, Task<string>> tasks = new SortedDictionary<long, Task<string>>();
            foreach (long sectionNumber in SectionNumbers)
            {
                //UpdateVolumePositions(sectionNumber);
            
                var task = System.Threading.Tasks.Task.Run(() => UpdateVolumePositions(sectionNumber));
                tasks.Add(sectionNumber, task);
            }

            foreach(long sectionNumber in tasks.Keys)
            {
                var task = tasks[sectionNumber];
                task.Wait();
                Console.WriteLine(task.Result);
                
                State.MappingsManager.SectionMappingCache.Remove((int)sectionNumber);
            }
        }

        static string UpdateVolumePositions(long SectionNumber)
        {
            var LocDict = WebAnnotationModel.Store.Locations.GetObjectsForSection(SectionNumber);

            Viking.VolumeModel.Section section = State.Volume.Sections[(int)SectionNumber];

            MappingBase mapper = State.MappingsManager.GetMapping(State.Volume.DefaultVolumeTransform, (int)SectionNumber, section.DefaultChannel, section.DefaultPyramidTransform);
            if (mapper == null)
            {
                throw new Exception("No mapping found for section " + SectionNumber.ToString());
            }

            int NumUpdated = 0;
            foreach (LocationObj loc in LocDict.Values)
            {
                bool result = UpdateVolumeShape(loc, mapper);
                if (result)
                    NumUpdated++;
            }
            

            if (NumUpdated > 0)
            {
                Store.Locations.Save(); 
                //Console.Write("...Locations updated");
            }

            string Result = string.Format("Section {0} : {1} / {2} locations needed updates", SectionNumber, NumUpdated, LocDict.Count);

            Store.Locations.RemoveSection((int)SectionNumber);

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
            SqlGeometry updatedVolumeShape = VolumeShapeForLocation(loc, mapper);
            if(updatedVolumeShape == null)
            {
                Console.WriteLine("Could not map location ID : " + loc.ID.ToString());
                return false; 
            }
            if(!updatedVolumeShape.STIsValid())
            {
                Console.WriteLine(string.Format("Location {0} invalid : {1} ", loc.ID, updatedVolumeShape.IsValidDetailed()));
                return false; 
            }
             
            GridVector2[] OriginalVolumeControlPoints = loc.VolumeShape.ToPoints();
            GridVector2[] UpdatedVolumeControlPoints = updatedVolumeShape.ToPoints();
             
            if (AnyPointsAreDifferent(OriginalVolumeControlPoints, UpdatedVolumeControlPoints))
            {
                loc.VolumeShape = updatedVolumeShape;
                return true;
            }

            return false;
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
            GridVector2[] MosaicControlPoints = MosaicPointsForLocation(loc);
            GridVector2[] VolumeControlPoints;

            bool mapped = mapper.TrySectionToVolume(MosaicControlPoints, out VolumeControlPoints).All(result => result == true);
            if (!mapped)
                return null;
 
            switch (loc.TypeCode)
            {
                case LocationType.POINT:
                    return VolumeControlPoints[0].ToGeometryPoint();
                case LocationType.CIRCLE:
                    return SqlGeometryUtils.GeometryExtensions.ToCircle(VolumeControlPoints[0].X,
                                   VolumeControlPoints[0].Y,
                                   loc.Z,
                                   loc.Radius); 
                case LocationType.POLYGON:
                case LocationType.POLYLINE:
                    return SqlGeometryUtils.GeometryExtensions.ToGeometry(loc.MosaicShape.STGeometryType(), VolumeControlPoints);
                case LocationType.OPENCURVE:
                    {
                        GridVector2[] curvePoints = VolumeControlPoints.CalculateCurvePoints((uint)options.NumOpenInterpolationPoints, false).ToArray();
                        return SqlGeometryUtils.GeometryExtensions.ToGeometry(loc.MosaicShape.STGeometryType(), curvePoints);
                    }
                case LocationType.CLOSEDCURVE:
                    {
                        GridVector2[] curvePoints = VolumeControlPoints.CalculateCurvePoints((uint)options.NumOpenInterpolationPoints, true).ToArray();
                        return SqlGeometryUtils.GeometryExtensions.ToGeometry(loc.MosaicShape.STGeometryType(), curvePoints);
                    }
                default:
                    return SqlGeometryUtils.GeometryExtensions.ToGeometry(loc.MosaicShape.STGeometryType(), VolumeControlPoints);
            }
        }
         

        static bool AnyPointsAreDifferent(GridVector2[] Original, GridVector2[] New, double epsilonSquared = 0.25)
        {
            if (Original.Length != New.Length)
                return true;

            return Original.Where((p, i) => GridVector2.DistanceSquared(p, New[i]) > epsilonSquared).Any();
        }
    }
}
