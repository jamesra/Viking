using System;
using CommandLine;
using CommandLine.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading.Tasks;

namespace MonogameTestbed
{
#if WINDOWS || LINUX

    /// <summary>
    /// The main class.
    /// </summary>
    public static class Program
    {
        public class CommandLineOptions
        {
            /// <summary>
            /// The raw StructureID arguments
            /// </summary>
            [Option('s', "SIDs", Required = false, HelpText = "Structure IDs", Separator = ' ')]
            public IEnumerable<string> StructureIDParams { get; set; }

            public List<ulong> StructureIDs { get; private set; }

            /// <summary>
            /// The raw LocationID arguments
            /// </summary>
            [Option('l', "LIDs", Required = false, HelpText = "Location IDs", Separator = ' ')]
            public IEnumerable<string> LocationIDParams { get; set; }

            public List<ulong> LocationIDs { get; private set; }

            [Option('e', "Endpoint", Required = false, HelpText = "Endpoint, either URL or one of [TEST, RC1, RC2, TEMPORALMONKEY, INFERIORMONKEY, RPC1]", Separator = ' ')]
            public string EndpointParam { get; set; }

            public Uri EndpointUri
            {
                get
                {
                    if (string.IsNullOrWhiteSpace(EndpointParam))
                    {
                        return null;
                    }

                    Uri Endpoint_uri;

                    try
                    {
                        var endpoint = EndpointParam.ToEnum<Endpoint>();
                        if(DataSource.EndpointMap.TryGetValue(endpoint, out Endpoint_uri))
                        {
                            return Endpoint_uri;
                        }
                    }
                    catch
                    {
                        
                    }

                    Console.WriteLine($"Could not convert {EndpointParam} to predefined Endpoint.  Trying as URI");

                    Endpoint_uri = new Uri(EndpointParam);
                    return Endpoint_uri;
                }
            }
             
            private static readonly Regex IntegerRegex = new Regex(@"(\d+)");
            private static readonly Regex IntegerRangeRegex = new Regex(@"(\d+)\-(\d+)");
            private static readonly Regex IntegerOrIntegerRangeRegex = new Regex(@"((\d+)\-(\d+))|(\d+)");

            /// <summary>
            /// Convert a number string, or a string of two integers seperated by a hyphen to a list of integers
            /// </summary>
            /// <param name="input"></param>
            /// <returns></returns>
            private static List<ulong> NumberRangeToList(string input)
            {
                List<ulong> listNumbers;

                try
                {
                    ulong SectionNumber = System.Convert.ToUInt64(input);
                    listNumbers = new List<ulong>();
                    listNumbers.Add(SectionNumber);
                    return listNumbers;
                }
                catch (FormatException e)
                { 
                    Match m = IntegerRangeRegex.Match(input);

                    ulong start = System.Convert.ToUInt64(m.Groups[1].Value);
                    ulong end = System.Convert.ToUInt64(m.Groups[2].Value);

                    listNumbers = new List<ulong>((int)(end - start) + 1);

                    for (ulong val = start; val <= end; val++)
                    {
                        listNumbers.Add(val);
                    }

                    return listNumbers;
                }
            }

            private static bool IsIntegerRange(string input)
            { 
                var match = IntegerRangeRegex.Match(input);
                return match.Success;
            }

            private static bool IsInteger(string input)
            {
                var match = IntegerRegex.Match(input);
                return match.Success;
            }

            private static bool IsIntegerOrIntegerRange(string input)
            {
                var match = IntegerRegex.Match(input);
                return match.Success;
            }

            private static List<ulong> InputParameterListToIDs(IEnumerable<string> input)
            {
                return input.SelectMany(param => InputParameterListToIDs(param)).ToList() ;
            }

            private static List<ulong> InputParameterListToIDs(string input)
            {
                List<ulong> listNumbers = new List<ulong>();

                foreach (string chunk in input.Split(new char[] { ',', ';' }).Select(s => s.Trim()))
                {
                    if (IsIntegerOrIntegerRange(chunk))
                    {
                        if (IsInteger(chunk))
                        {
                            listNumbers.Add(System.Convert.ToUInt64(chunk));
                        }
                        else if (IsIntegerRange(chunk))
                        {
                            listNumbers.AddRange(NumberRangeToList(input));
                        }
                        else
                        {
                            throw new ArgumentException($"Unexpected argument in ID list ${chunk}");
                        }
                    }
                    else
                    {  
                        listNumbers.AddRange(ParseFile(chunk)); 
                    }
                }

                return listNumbers;
            }

            private static List<ulong> ParseFile(string filename)
            {
                List<ulong> results = new List<ulong>();
                if (System.IO.File.Exists(filename))
                {
                    try
                    {
                        foreach (string line in System.IO.File.ReadLines(filename))
                        {
                            var IDs = InputParameterListToIDs(line);
                            results.AddRange(IDs); 
                        }
                    }
                    catch
                    {

                    }
                }
                else
                {
                    throw new ArgumentException($"File argument ${filename} was not found, is it in the path?");
                }

                return results; 
            }

            /// <summary>
            /// Parse a single line from an input file with IDs
            /// </summary>
            /// <param name="input"></param>
            /// <returns></returns>
            private static List<ulong> ParseFileLine(string input)
            {
                string data = input.Split('#').First(); //Anything to the right of a # is a comment and ignored
                return InputParameterListToIDs(data);
            }

            /// <summary>
            /// Convert links to files and number ranges into sets of numbers that programs can more easily access
            /// </summary>
            internal void ProcessStrings()
            {
                this.LocationIDs = InputParameterListToIDs(LocationIDParams);
                this.StructureIDs = InputParameterListToIDs(StructureIDParams);
            }
        }

        public static CommandLineOptions options;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            CommandLine.Parser.Default.ParseArguments<CommandLineOptions>(args)
                .WithParsed<CommandLineOptions>(o =>
                {
                    o.ToString();
                    o.ProcessStrings();
                    Program.options = o;
                })
                .WithNotParsed(errors =>
                {
                    System.Console.WriteLine($"Unable to parse command line arguments ${errors}, aborting");
                    return;
                });
             
            using (var game = new MonoTestbed())
                game.Run();
        }
    }
#endif
}
