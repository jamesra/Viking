using System.Linq;
using System.Xml.Linq;
using Geometry;
using Geometry.Meshing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using UnitsAndScale;

namespace ColladaIOTest
{
    [TestClass]
    public class BasicColladaViewChildMaterialsTest
    {
        /// <summary>
        /// Parent→child hierarchy must register each child's material in library_materials,
        /// and shared type keys must appear once while both geometries bind that target.
        /// </summary>
        [TestMethod]
        public void AddModel_RegistersChildTypeMaterialsInLibrary()
        {
            var typePsd = new MaterialLighting("Type42", RgbaColor.FromRgb(200, 50, 50));
            var typeCell = new MaterialLighting("Type1", RgbaColor.FromRgb(100, 149, 237));

            Mesh3D empty = new();
            StructureModel childA = new(100, empty, typePsd, "PSD-100")
            {
                Translation = new Vector3(1, 0, 0)
            };
            StructureModel childB = new(101, empty, typePsd, "PSD-101")
            {
                Translation = new Vector3(2, 0, 0)
            };
            StructureModel parent = new(10, empty, typeCell, "Cell-10")
            {
                Translation = new Vector3(0, 0, 0)
            };
            parent.AddChild(childA);
            parent.AddChild(childB);

            BasicColladaView view = new(new AxisUnits(1e-6, "meter"), null)
            {
                SceneTitle = "ChildMaterials"
            };
            view.Add(parent);

            Assert.IsTrue(view.Materials.ContainsKey("Type1"));
            Assert.IsTrue(view.Materials.ContainsKey("Type42"));
            Assert.AreEqual(2, view.Materials.Count);

            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "BasicColladaViewChildMaterialsTest.dae");
            ColladaIO.DynamicRenderMeshColladaSerializer.SerializeToFile(view, path);

            XDocument doc = XDocument.Load(path);
            XNamespace ns = "http://www.collada.org/2008/03/COLLADASchema";

            var materialIds = doc.Descendants(ns + "library_materials")
                .Descendants(ns + "material")
                .Select(m => (string)m.Attribute("id"))
                .ToList();
            Assert.IsTrue(materialIds.Contains("Type1"), "cell type material missing");
            Assert.IsTrue(materialIds.Contains("Type42"), "shared PSD type material missing");
            Assert.AreEqual(1, materialIds.Count(id => id == "Type42"));

            var instanceTargets = doc.Descendants(ns + "instance_material")
                .Select(m => (string)m.Attribute("target"))
                .ToList();
            Assert.IsTrue(instanceTargets.Count(t => t == "#Type42") >= 2);

            var nodeNames = doc.Descendants(ns + "node")
                .Select(n => (string)n.Attribute("name"))
                .Where(n => n != null)
                .ToList();
            Assert.IsTrue(nodeNames.Contains("PSD-100"));
            Assert.IsTrue(nodeNames.Contains("PSD-101"));
            Assert.IsTrue(nodeNames.Contains("Cell-10"));
        }
    }
}
