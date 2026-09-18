using Geometry;
using Geometry.Meshing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using System.Linq;
using System.Xml.Linq;
using UnitsAndScale;

namespace ColladaIOTest
{
    [TestClass]
    public class ColladaXmlIdTests
    {
        [TestMethod]
        public void CreateDisplayName_UsesStructureIdThenTrimmedTypeLabel()
        {
            Assert.AreEqual("9025-Cell", StructureModel.CreateDisplayName(9025, "Cell".PadRight(128)));
            Assert.AreEqual("9025", StructureModel.CreateDisplayName(9025, null));
        }

        [TestMethod]
        public void ToColladaXmlId_StripsFixedLengthTypeNamePadding()
        {
            string padded = "9025-" + "Cell".PadRight(128);
            Assert.AreEqual("id-9025-Cell", StructureModel.ToColladaXmlId(padded));
        }

        /// <summary>
        /// Outliner names are StructureID-TypeLabel. XML ids cannot start with a digit, so
        /// geometry/source ids get an id- prefix. nchar padding must not appear in either.
        /// </summary>
        [TestMethod]
        public void Serialize_PaddedCellName_WritesSpacelessSourceIds()
        {
            Mesh3D mesh = new();
            mesh.AddVertex(new Vertex3D(new Vector3(0, 0, 0), new Vector3(0, 0, 1)));
            mesh.AddVertex(new Vertex3D(new Vector3(1, 0, 0), new Vector3(0, 0, 1)));
            mesh.AddVertex(new Vertex3D(new Vector3(0, 1, 0), new Vector3(0, 0, 1)));
            mesh.AddFace(0, 1, 2);

            StructureModel model = new(9025, mesh, new MaterialLighting("Type1", RgbaColor.FromRgb(1, 2, 3)),
                StructureModel.CreateDisplayName(9025, "Cell".PadRight(128)));
            Assert.AreEqual("9025-Cell", model.Name);
            Assert.AreEqual("id-9025-Cell", model.GeometryId);

            BasicColladaView view = new(new AxisUnits(1e-6, "meter"), null)
            {
                SceneTitle = "padded-cell"
            };
            view.Add(model);

            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ColladaXmlIdTests.dae");
            ColladaIO.DynamicRenderMeshColladaSerializer.SerializeToFile(view, path);

            XDocument doc = XDocument.Load(path);
            XNamespace ns = "http://www.collada.org/2008/03/COLLADASchema";
            string[] sourceIds = [.. doc.Descendants(ns + "source").Select(e => (string)e.Attribute("id"))];
            CollectionAssert.Contains(sourceIds, "id-9025-Cell-geometry-positions");
            CollectionAssert.Contains(sourceIds, "id-9025-Cell-geometry-normals");
            Assert.IsFalse(sourceIds.Any(id => id.Contains(' ')),
                "Collada source ids must not contain spaces: " + string.Join(",", sourceIds));

            string[] nodeNames = [.. doc.Descendants(ns + "node").Select(e => (string)e.Attribute("name")).Where(n => n != null)];
            Assert.IsTrue(nodeNames.Contains("9025-Cell"));
        }
    }
}
