using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Neo4j.Driver.V1;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Converters;
using CommandLine; 

namespace Neo4JGenerator
{
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

            Newtonsoft.Json.Linq.JObject json = null;
            if (Program.options.JsonFilename != null)
            {
                json = DeserializeFromStream(System.IO.File.OpenRead(Program.options.JsonFilename));
            }
            else if(Program.options.JsonURL != null)
            {
                System.Net.WebClient client = new System.Net.WebClient();
                System.IO.Stream response = client.OpenRead(Program.options.JsonURL);

                json = DeserializeFromStream(response); 
            }

            if(json == null)
            {
                Console.WriteLine("Unable to load JSON data");
            }
             
            using (var driver = GraphDatabase.Driver(Program.options.Neo4JDatabase, AuthTokens.Basic(Program.options.Username, Program.options.Password)))
            using (var session = driver.Session())
            {
                JProperty nodes = json.Property("nodes");

                ClearDatabase(session);
                
                foreach(JToken token in nodes.First)
                {
                    AddCellToGraph(session, token as JObject);
                }
                
                JProperty edges = json.Property("edges");

                foreach(JToken edge in edges.First)
                {
                    AddEdgesToGraph(session, edge as JObject);
                }
            }

            Console.WriteLine("All done!");
        }

        private static void ClearDatabase(ISession session)
        {
            //Delete all the things!
            session.Run("MATCH ()-[r]->() delete r");
            session.Run("MATCH (n) delete n");
        }

        private static void AddCellToGraph(ISession session, JObject node)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("CREATE (c:Cell ");

            sb.Append(EncodePropertySet(node.Children()));

            sb.Append(")");

            string neo4Jcmd = sb.ToString();
            Console.WriteLine(neo4Jcmd);
            session.Run(neo4Jcmd);
        }

        private static void AddAggregateEdgeToGraph(ISession session, JObject edge, bool ReverseDirection)
        {
            StringBuilder sb = new StringBuilder();
            string neo4Jcmd = sb.AppendLine(AddAggregateEdgeToGraph2(edge, false, 0);
            session.Run(neo4Jcmd); 
        }

        private static void AddAggregateEdgeToGraph2(JObject edge, bool ReverseDirection, int iVarNumber)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("\tCREATE (s)");

            if (!ReverseDirection)
                sb.Append("-");
            else
                sb.Append("<-");

            sb.AppendFormat("[e{0}:{1} ", iVarNumber, "AggregateLink");
            
            List<JToken> listTokens = new List<JToken>();
            listTokens.AddRange(edge.Children());
            sb.Append(EncodePropertySet(listTokens));

            sb.Append("]");

            if (!ReverseDirection)
                sb.AppendLine("->");
            else
                sb.AppendLine("-");

            sb.Append("(t)");

            return sb.ToString();
        }
        
        private static string BuildNodeQueryForEdge(JObject edge)
        {
            return "MATCH(s:Cell { StructureID: " + edge["SourceStructureID"] +
                            " }), (t:Cell { StructureID: " + edge["TargetStructureID"] + " })";
        }

        private static void AddEdgesToGraph(ISession session, JObject edge)
        { 
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(BuildNodeQueryForEdge(edge));

            int iFirstVarNumber = 0;
            sb.AppendLine(CreateEdgesForLinks(edge, false, ref iFirstVarNumber));
            if (edge["Directional"].Value<bool>() == false)
            {
                sb.AppendLine(CreateEdgesForLinks(edge, true, ref iFirstVarNumber));
            }

            string neo4Jcmd = sb.ToString();
            Console.WriteLine(neo4Jcmd);
            session.Run(neo4Jcmd); 
        }

        private static string CreateEdgesForLinks(JObject edge, bool ReverseDirection, ref int iFirstVarNumber)
        {
            JProperty Links = edge.Property("Links");

            StringBuilder sb = new StringBuilder();
            foreach (var Link in Links.First)
            {
                sb.Append("\tCREATE (s)");

                if (!ReverseDirection)
                    sb.Append("-");
                else
                    sb.Append("<-");
                
                sb.AppendFormat("[e{0}:{1} ", iFirstVarNumber, CleanEdgeName(edge));

                List<JToken> listTokens = new List<JToken>();
                listTokens.AddRange(Link.Children());
                listTokens.Add(edge.Property("Type"));
                sb.Append(EncodePropertySet(listTokens));

                sb.Append("]");

                if (!ReverseDirection)
                    sb.AppendLine("->");
                else
                    sb.AppendLine("-");

                sb.Append("(t)");

                iFirstVarNumber++;
            }

            return sb.ToString();
        }

        private static string CleanEdgeName(JObject edge)
        {
            string name = edge["Type"].ToString(); 
            name = name.Replace(" ", "");
            name = name.Replace("-", "");
            return name;
        }

        private static string EncodePropertySet(IEnumerable<JToken> tokens)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{");
            bool first = true;
            foreach (JToken token in tokens)
            {
                if (token is JProperty)
                {
                    if (!first)
                    {
                        sb.Append(", ");
                    }

                    first = false;

                    JProperty prop = token as JProperty;
                    sb.Append(EncodeProperty(prop));
                }
            }
            sb.Append("}");
            return sb.ToString();
        }

        private static string EncodeProperty(JProperty prop)
        {
            switch (prop.Value.Type)
            {
                case JTokenType.String:
                    return string.Format("{0}:'{1}'", prop.Name, prop.Value);
                case JTokenType.Null:
                case JTokenType.None:
                    return string.Format("{0}:''", prop.Name);
                default:
                    return string.Format("{0}:{1}", prop.Name, prop.Value);
            }
        }

        public static Newtonsoft.Json.Linq.JObject DeserializeFromStream(System.IO.Stream stream)
        {
            var serializer = new JsonSerializer();

            using (var sr = new System.IO.StreamReader(stream))
            using (var jsonTextReader = new JsonTextReader(sr))
            {
                return (Newtonsoft.Json.Linq.JObject)serializer.Deserialize(jsonTextReader);
            }
        }
    }
}
