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
                /*
                foreach(JToken token in nodes.First)
                {
                    AddCellToGraph(session, token as JObject);
                }
                */

                BulkAddCellsToGraph(session, nodes.First);

                JProperty edges = json.Property("edges");
                
                foreach(JToken edge in edges.First)
                {
                    AddAggregateEdgeToGraph(session, edge as JObject);
                    AddEdgesToGraph(session, edge as JObject);
                }
            }

            Console.WriteLine("All done!");
        }

        private static void ClearDatabase(ISession session)
        {
            ClearRelationships(session);
            ClearNodes(session);
        }

        private static int ClearRelationships(ISession session)
        {
            bool AllDeleted = false;
            int TotalDeleted = 0;
            //Delete all the things!
            while (!AllDeleted)
            {
                IStatementResult result = session.Run("MATCH() -[r]->() WITH r LIMIT 5000 DELETE r RETURN count(r) as deletedCount");
                int NumDeleted = System.Convert.ToInt32(result.First()["deletedCount"]);
                TotalDeleted += NumDeleted;
                AllDeleted = NumDeleted == 0;
            }

            return TotalDeleted;
            
        }

        private static int ClearNodes(ISession session)
        {
            bool AllDeleted = false;
            int TotalDeleted = 0;
            //Delete all the things!
            while (!AllDeleted)
            {
                IStatementResult result = session.Run("MATCH (n) WITH n LIMIT 5000 DELETE n RETURN count(n) as deletedCount");
                int NumDeleted = System.Convert.ToInt32(result.First()["deletedCount"]);
                TotalDeleted += NumDeleted;
                AllDeleted = NumDeleted == 0;
            }

            return TotalDeleted;

        }

        private static void AddCellToGraph(ISession session, JObject node)
        {
            string neo4Jcmd = CreateAddCellCmd(session, node);
            Console.WriteLine(neo4Jcmd);
            session.Run(neo4Jcmd);
        }

        /// <summary>
        /// Add all nodes in a single command
        /// </summary>
        /// <param name="session"></param>
        /// <param name="nodes"></param>
        private static void BulkAddCellsToGraph(ISession session, JToken nodes, int BulkSize = 100)
        {
            StringBuilder sb = new StringBuilder();

            int count = 0;
            string neo4Jcmd = null;
            foreach (JToken node in nodes)
            {
                sb.Append(CreateAddCellCmd(session, node as JObject));
                sb.Append(" ");
                count++; 
                if(count >= BulkSize)
                {
                    neo4Jcmd = sb.ToString();
                    Console.WriteLine(neo4Jcmd);
                    session.Run(neo4Jcmd);
                    sb.Clear();
                }
            }

            neo4Jcmd = sb.ToString();
            Console.WriteLine(neo4Jcmd);
            session.Run(neo4Jcmd);
        }

        private static string CreateAddCellCmd(ISession session, JObject node)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("CREATE (c{0}:Cell ", GetIDForCell(node));

            sb.Append(EncodePropertySet(node.Children()));

            sb.Append(")");

            return sb.ToString();
        }

        private static long GetIDForCell(JObject node)
        {
            return node["StructureID"].Value<long>();
        }

        private static void AddAggregateEdgeToGraph(ISession session, JObject edge)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(BuildNodeQueryForEdge(edge));

            sb.AppendLine(CreateAggregateEdgeCommand(edge, false, 0));
            if (IsEdgeBidirectional(edge))
            {
                sb.AppendLine(CreateAggregateEdgeCommand(edge, true, 1));
            }

            string neo4Jcmd = sb.ToString();
            Console.WriteLine(neo4Jcmd);
            session.Run(neo4Jcmd); 
        }

        private static string CreateAggregateEdgeCommand(JObject edge, bool ReverseDirection, int iVarNumber)
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
            listTokens.Add(new JProperty("LinkCount", LinkCount(edge)));

            //Do not include the links as properties
            RemoveToken(listTokens, "Links");
            RemoveToken(listTokens, "ID");
            RemoveToken(listTokens, "SourceStructureID");
            RemoveToken(listTokens, "TargetStructureID");
            RemoveToken(listTokens, "Directional");

            sb.Append(EncodePropertySet(listTokens));

            sb.Append("]");

            if (!ReverseDirection)
                sb.AppendLine("->");
            else
                sb.AppendLine("-");

            sb.Append("(t)");

            return sb.ToString();
        }

        /// <summary>
        /// This list should be a dictionary, but time constrained before a flight.  Jamesan
        /// </summary>
        /// <param name="listTokens"></param>
        /// <param name="name"></param>
        private static  bool RemoveToken(List<JToken> listTokens, string name)
        {
            int iLinks = listTokens.FindIndex((t) => ((JProperty)t).Name == name);
            if (iLinks >= 0)
            {
                listTokens.RemoveAt(iLinks);
                return true;
            }
            return false;
        }
        
        private static string BuildNodeQueryForEdge(JObject edge)
        {
            return "MATCH(s:Cell { StructureID: " + edge["SourceStructureID"] +
                            " }), (t:Cell { StructureID: " + edge["TargetStructureID"] + " })";
        }

        private static bool IsEdgeBidirectional(JObject edge)
        {
            return edge["Directional"].Value<bool>() == false;
        }

        private static int LinkCount(JObject edge)
        {
            JProperty Links = edge.Property("Links");
            return Links.First.Count();
        }

        private static void AddEdgesToGraph(ISession session, JObject edge)
        { 
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(BuildNodeQueryForEdge(edge));

            int iFirstVarNumber = 0;
            sb.AppendLine(CreateEdgesForLinks(edge, false, ref iFirstVarNumber));
            if (IsEdgeBidirectional(edge))
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
