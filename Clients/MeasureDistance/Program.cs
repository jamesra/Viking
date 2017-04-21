using System;
using CommandLine;
using CommandLine.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnnotationVizLib;
using AnnotationVizLib.SimpleOData;
using ODataClient;

namespace MeasureDistance
{
    class CommandLineOptions
    {
        [Option('v', "VolumeURL", Required = true, HelpText = "URL of VolumeXML file")]
        public string VolumeURL { get; set; }
        
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

            List<long> listNumbers = new List<long>((int)(end - start) + 1);

            for (long val = start; val <= end; val++)
            {
                listNumbers.Add(val);
            }

            return listNumbers;
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
    
    public class LabelMeasurement
    {
        public string Label;

        /// <summary>
        /// The paths found from structures of a given type to the destination
        /// </summary>
        public Dictionary<ulong, PathData[]> PathsForType = new Dictionary<ulong, PathData[]>();

        public override string ToString()
        {
            return Label;
        }

        /// <summary>
        /// Convert a collection of meassurements for a label to a Matlab structure
        /// </summary>
        /// <param name="labels"></param>
        /// <param name="VariableName">The variable name in the generated matlab script</param>
        /// <returns></returns>
        public static string ToMatlabStructures(IList<LabelMeasurement> labels, string VariableName = "Label")
        {
            StringBuilder sb = new StringBuilder();
            for(int i = 0; i < labels.Count; i++)
            {
                LabelMeasurement m = labels[i];
                sb.AppendFormat("%--- Begin {0} ---\n", m.Label);

                string paths_varname = string.Format("{0}_paths", m.Label);

                //Create the structure for individual type measurements
                int iSources = 1;
                foreach(ulong sourceTypeID in m.PathsForType.Keys)
                {
                    string indexed_path_varname = string.Format("{0}({1})", paths_varname, iSources);
                    PathData[] paths = m.PathsForType[sourceTypeID];
                    sb.AppendFormat("{0}.SourceTypeID = {1};\n", indexed_path_varname, sourceTypeID);
                    sb.AppendLine(PathData.ToMatlabStructures(paths, indexed_path_varname));
                    iSources++;
                }
                
                sb.AppendFormat("{0}({1}).Label = '{2}';\n", VariableName, i+1, m.Label);
                sb.AppendFormat("{0}({1}).Paths = {2};\n", VariableName, i+1, paths_varname);
                
                sb.AppendFormat("%--- End   {0} ---\n", m.Label);
            }

            return sb.ToString();
        }
    }

    class Program
    {
        static CommandLineOptions options = new CommandLineOptions();

        private static Simple.OData.Client.ODataClient CreateODataClient()
        {
            return new Simple.OData.Client.ODataClient(options.VolumeURL);
        }

        static void Main(string[] args)
        {
            SqlServerTypes.Utilities.LoadNativeAssemblies(AppDomain.CurrentDomain.BaseDirectory);

            if (!CommandLine.Parser.Default.ParseArguments(args, Program.options))
            {
                System.Console.WriteLine("Unable to parse command line arguments, aborting");
                return;
            }

            MeasureDistanceForAllLabelClasses();
        }
        
        public static void MeasureDistanceForAllLabelClasses()
        {
            var client = CreateODataClient();

            var T = client.FindEntriesAsync("Structures/ConnectomeODataV4.DistinctLabels");

            T.Wait();

            List<string> labels = new List<string>(T.Result.Count());

            foreach (IDictionary<string, object> dict in T.Result)
            {
                labels.AddRange(dict.Values.Select(v => v as string));
            }

            //string[] labels = T.Result;

            string OutputPath = "C:\\Temp\\DistanceToAdherensResults.m";

            if (System.IO.File.Exists(OutputPath))
                System.IO.File.Delete(OutputPath);

            //string[] distinctLabels = labels.ToArray(); //.Distinct().ToArray();
            string[] distinctLabels = { "CBb5" };

            Dictionary<string, LabelMeasurement> LabelDict = new Dictionary<string, LabelMeasurement>();

            foreach (string label in distinctLabels)
            {
                LabelDict[label] = BulkMeasureForLabel(label);
            }

            System.IO.File.AppendAllText(OutputPath, LabelMeasurement.ToMatlabStructures(LabelDict.Values.ToArray()));
        }

        public static LabelMeasurement BulkMeasureForLabel(string Label)
        {
            Dictionary<ulong, PathData[]> distanceForLabel = new Dictionary<ulong, PathData[]>();
            if (Label == null)
                return new LabelMeasurement { Label = Label };

            var client = CreateODataClient();

            ODataClient.ConnectomeODataV4.Container container = new ODataClient.ConnectomeODataV4.Container(new Uri(options.VolumeURL));
            var structures = container.Structures.Where(s => s.Label.ToLower().Equals(Label.ToLower())).Select(s => new { ID = s.ID, Label = s.Label }).ToArray();
            long[] IDs = structures.Select(s => s.ID).ToArray();

            MorphologyGraph graph = ODataMorphologyFactory.FromOData(IDs, true, new Uri(options.VolumeURL));

            SortedSet<ulong> TargetTypes = new SortedSet<ulong>(new ulong[] { 85 }); //Adherens
            SortedSet<ulong> SourceTypes = new SortedSet<ulong>(new ulong[] { 28, 34, 35, 73 });

            foreach (ulong SourceType in SourceTypes)
            {
                PathData[] Distances;
                SortedSet<ulong> Types = new SortedSet<ulong>();
                Types.Add(SourceType);
                Distances = MeasureDistances(graph, Types, TargetTypes);

                distanceForLabel[SourceType] = Distances;
            }

            return new LabelMeasurement { Label = Label, PathsForType = distanceForLabel };
        }

        public static PathData[] MeasureDistances(MorphologyGraph graph, SortedSet<ulong> SourceTypes, SortedSet<ulong> TargetTypes)
        {
            List<PathData> accumulated_distances = new List<PathData>();
            foreach (MorphologyGraph cell_graph in graph.Subgraphs.Values)
            {
                PathData[] distances = MorphologyGraph.DistancesBetweenSubgraphsByType(cell_graph, SourceTypes, TargetTypes);
                accumulated_distances.AddRange(distances);
            }

            return accumulated_distances.ToArray();
        } 
    }    
}
