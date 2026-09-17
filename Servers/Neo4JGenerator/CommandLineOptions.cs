using CommandLine;
using CommandLine.Text;

namespace Neo4JGenerator
{

    class CommandLineOptions
    {
        [Option('w', "URL", Required = false, HelpText = "URL of JSON Exporter")]
        public string JsonURL { get; set; }

        [Option('f', "File", Required = false, HelpText = "JSON File")]
        public string JsonFilename { get; set; }

        [Option('s', "Neo4JDatabase", Required = true, HelpText = "Neo4J Database to update/create")]
        public string Neo4JDatabase { get; set; }

        [Option('u', "username", Required = false, HelpText = "URL of VolumeXML file")]
        public string Username { get; set; } = "Anonymous";

        [Option('p', "password", Required = false, HelpText = "URL of VolumeXML file")]
        public string Password { get; set; } = "connectome";

        [Option('v', "verbose", Required = false, HelpText = "Provide more detailed output")]
        public bool Verbose { get; set; } = false;
         
        [Option('q', "quiet", Required = false, HelpText = "Provide no console output")]
        public bool Quiet { get; set; } = false;
        
        [Option('o', "odata", Required = false, HelpText = "OData Endpoint for update. This should be an OData URL that can be filtered by both StructureID and ParentID")]
        public string ODataEndpoint { get; set; }
    }
}
