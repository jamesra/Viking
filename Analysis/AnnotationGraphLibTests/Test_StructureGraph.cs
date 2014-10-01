using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using AnnotationUtils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnnotationUtilsTests
{
    /// <summary>
    /// Summary description for Test_StructureGraph
    /// </summary>
    [TestClass]
    public class Test_StructureGraph
    {
        public string Endpoint = "https://connectomes.utah.edu/Services/RabbitBinary/Annotate.svc";
        private System.Net.NetworkCredential userCredentials;

        public Test_StructureGraph()
        {
            userCredentials = new System.Net.NetworkCredential("jamesan", "4%w%o06");
            ConnectionFactory.SetConnection(Endpoint, userCredentials);
        }

        [TestMethod]
        public void GenerateStructureLocationGraph()
        { 
            long structureID = 166;
            AnnotationUtils.Graph.Structures.StructureGraph graph = AnnotationUtils.Graph.Structures.StructureGraph.BuildGraph(structureID, this.Endpoint, this.userCredentials);

            System.Diagnostics.Debug.Assert(graph != null);

            string SWCPath = string.Format("C:\\Temp\\SWCDump\\{0}.swc", structureID.ToString());

            AnnotationUtils.Graph.Structures.StructureSWCView swcView = new AnnotationUtils.Graph.Structures.StructureSWCView(graph);

            string SWCData = swcView.ToString();

            swcView.Save(SWCPath);
        }

        [TestMethod]
        public void GenerateSWCForAllStructures()
        { 
           /* AnnotationUtils.AnnotationService.AnnotateStructureTypesClient client = ConnectionFactory.CreateStructureTypesClient();

            AnnotationUtils.AnnotationService.Structure[] structures = client.GetStructuresForType(1);

            foreach (AnnotationUtils.AnnotationService.Structure s in structures)
            { 
                AnnotationUtils.Graph.Structures.StructureGraph graph = AnnotationUtils.Graph.Structures.StructureGraph.BuildGraph(s.ID, this.Endpoint, this.userCredentials);

                if (graph.Nodes.Count < 64)
                {
                    continue;
                }

                System.Diagnostics.Debug.Assert(graph != null);

                string SWCPath = string.Format("C:\\Temp\\SWCDump\\{0}.swc", s.ID.ToString());

                AnnotationUtils.Graph.Structures.StructureSWCView swcView = new AnnotationUtils.Graph.Structures.StructureSWCView(graph);

                string SWCData = swcView.ToString();

                swcView.Save(SWCPath);
            }
            */
        }
        
    }
}
