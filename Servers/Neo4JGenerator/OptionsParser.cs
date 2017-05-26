using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;

namespace Neo4JGenerator
{

    class CommandLineOptions
    {
        [Option('u', "URL", Required = true, HelpText = "URL of JSON Exporter")]
        public string JsonURL { get; set; }

        [Option('s', "Neo4JDatabase", Required =true, HelpText ="Neo4J Database to update/create")]
        public string Neo4JDatabase { get; set; }

        [Option('u', "username", DefaultValue = "Anonymous", Required = false, HelpText = "URL of VolumeXML file")]
        public string Username { get; set; }

        [Option('p', "password", DefaultValue = "connectome", Required = false, HelpText = "URL of VolumeXML file")]
        public string Password { get; set; }
        
        [ParserState]
        public IParserState LastParsertState { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return CommandLine.Text.HelpText.AutoBuild(this,
                (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }

    }
}
