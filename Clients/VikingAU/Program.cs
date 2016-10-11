using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;
using Viking.VolumeModel;

namespace Viking.AU
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
    }

    class ConsoleProgressReporter : Viking.Common.IProgressReporter
    {
        int LastLineLength = 0;

        public void ReportProgress(double ProgressPercentage, string message)
        {
            int LineLength = message.Length;
                        
            StringBuilder output = new StringBuilder();
            output.Append('\b', LastLineLength);
            output.AppendFormat("{0}% {1}", ProgressPercentage, message);

            string final_output = output.ToString();
            LastLineLength = final_output.Length;

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
            if(!CommandLine.Parser.Default.ParseArguments(args, Program.options))
            {
                System.Console.WriteLine("Unable to parse command line arguments, aborting");
                return;
            }

            ConsoleProgressReporter progressReporter = new AU.ConsoleProgressReporter();

            State.Volume = new Volume(Program.options.VolumeURL, State.CachePath, progressReporter);

            //OK.  Figure out which command we are executing.
            UpdateVolumePositions(State.Volume.Sections.Select(s => (long)s.Number).ToList());
        } 
        
        static void UpdateVolumePositions(IList<long> SectionNumbers)
        {
            foreach(long sectionNumber in SectionNumbers)
            {
                UpdateVolumePositions(sectionNumber);
            }
        }

        static void UpdateVolumePositions(long SectionNumber)
        { 
            var LocDict = WebAnnotationModel.Store.Locations.GetObjectsForSection(SectionNumber);

            foreach(long locID in LocDict.Keys)
            {
            }
        }
    }
}
