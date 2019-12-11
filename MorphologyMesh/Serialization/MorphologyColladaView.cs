using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry.Meshing;
using Geometry;
using AnnotationVizLib;
using System.Drawing;

namespace MorphologyMesh
{
    public class MaterialLighting
    {
        public static string CreateKey(COLORSOURCE source, IStructure structure)
        {
            switch (source)
            {
                case COLORSOURCE.STRUCTURE:
                    return string.Format("Structure{0}", structure.ID);
                case COLORSOURCE.STRUCTURETYPE:
                    return string.Format("Type{0}", structure.TypeID);
                case COLORSOURCE.LOCATION:
                    return string.Format("Structure{0}", structure.ID);
                default:
                    return string.Format("Default");
            }
        }

        /// <summary>
        /// Unique name for this material
        /// </summary>
        public readonly string Key = null;

        public Color Diffuse = Color.Empty;
        public Color Reflective = Color.Empty;
        public double Reflectivity = 0;
        public double RefractionIndex = 0;

        public MaterialLighting(string key, Color color)
        {
            Key = key;
            Diffuse = color;
            Reflective = color;
        }

        public MaterialLighting(COLORSOURCE source, IStructure structure, Color color)
        {
            Key = CreateKey(source, structure);
            Diffuse = color;
            Reflective = color;
        }

        public string FXName
        {
            get { return Key + "-fx"; }
        }
    }

    public class StructureModel
    {
        public readonly ulong ID;

        public string Name
        {
            get
            {
                return string.Format("Struct-{0}", ID);
            }
        }

        public string NodeName
        {
            get
            {
                return string.Format("NodeID-{0}", ID);
            }
        }

        public string GeometryURL
        {
            get; set;
        }

        /// <summary>
        /// url to use in the <node> elements within the <library visual scenes> element
        /// </summary>
        public string InstanceURL
        {
            get { return GeometryURL == null ? "#" + NodeName : string.Format("{0}#{1}", GeometryURL, NodeName); }
        }


        public Mesh3D<ulong> Mesh;

        public MaterialLighting Material;

        private SortedList<ulong, StructureModel> _ChildStructures = new SortedList<ulong, StructureModel>();

        public IReadOnlyDictionary<ulong, StructureModel> ChildStructures
        {
            get {
                return _ChildStructures as IReadOnlyDictionary<ulong, StructureModel>;
            }
         }

        public StructureModel(ulong id, Mesh3D<ulong> mesh, MaterialLighting mat)
        {
            ID = id;
            Mesh = mesh;
            Material = mat;

            GridVector3 TranslationVector = -mesh.BoundingBox.CenterPoint;

            Mesh.Translate(TranslationVector);

            Translation = TranslationVector; 
        }

        private GridVector3 _Translation;

        public GridVector3 Translation
        {
            get { return _Translation; }
            set { _Translation = value; }
        }

        /// <summary>
        /// Add a child structure and translate it to the correct position relative to our model
        /// </summary>
        /// <param name="model"></param>
        public void AddChild(StructureModel child)
        {
            child.Translation -= this.Translation;
            child.Translation = -child.Translation;
            _ChildStructures.Add(child.ID, child); 
        }

        /// <summary>
        /// Return the model and all child models recursively
        /// </summary>
        /// <returns></returns>
        public List<StructureModel> ModelsInTree()
        {
            List<StructureModel> listModel = new List<MorphologyMesh.StructureModel>();
            listModel.Add(this);

            listModel.AddRange(this.ChildStructures.Values.SelectMany(cs => cs.ModelsInTree()));
            return listModel; 
        }
    }


    /// <summary>
    /// Describes a scene containing one or more structures
    /// </summary>
    public class MorphologyColladaView
    {
        public readonly Scale Scale;

        public string SceneTitle = null;

        StructureMorphologyColorMap Colormap = null;

        public SortedDictionary<ulong, StructureModel> RootModels = new SortedDictionary<ulong, StructureModel>();

        public SortedDictionary<ulong, StructureModel> StructureModels = new SortedDictionary<ulong, StructureModel>();

        public SortedDictionary<string, MaterialLighting> Materials = new SortedDictionary<string, MaterialLighting>();

        public MorphologyColladaView(Geometry.Scale scale, StructureMorphologyColorMap colormap)
        {
            Colormap = colormap;
            if (Colormap == null)
                Colormap = new StructureMorphologyColorMap(null, null, null);

            Scale = scale;
        }

        /// <summary>
        /// Add a root level structure to the view
        /// </summary>
        /// <param name="structure"></param>
        public void Add(MorphologyGraph structure)
        {
            if (structure.Nodes.Count > 0)
            {
                StructureModel rootModel = AddModel(structure);
                RootModels[rootModel.ID] = rootModel;
            }
            else
            {
                foreach (var child in structure.Subgraphs.Values)
                {
                    Add(child);
                }
            }

        }

        private StructureModel AddModel(MorphologyGraph structure)
        {
            //MeshGraph meshGraph = structure.ConvertToMeshGraph();
            //SmoothMeshGenerator.Generate(meshGraph);
            //DynamicRenderMesh<ulong> structureMesh = TopologyMeshGenerator.Generate(meshGraph);
            Mesh3D<ulong> structureMesh = TopologyMeshGenerator.Generate(structure);
            StructureModel model = null;

            if (structureMesh != null)
            {
                structureMesh.Scale(0.001);
                COLORSOURCE source; 
                System.Drawing.Color color = Colormap.GetColor(structure, out source);

                MaterialLighting material = GetOrAddMaterial(source, structure.structure, color);
                model = new MorphologyMesh.StructureModel(structure.StructureID, structureMesh, material);

                StructureModels[structure.StructureID] = model;
            }

            foreach(var child in structure.Subgraphs.Values)
            {
                StructureModel childModel = AddModel(child);
                model.AddChild(childModel);
            } 

            return model;
        }

        /// <summary>
        /// Ensure the material is added to our dictionary and return the key
        /// </summary>
        /// <param name="source"></param>
        /// <param name="structure"></param>
        /// <returns></returns>
        private MaterialLighting GetOrAddMaterial(COLORSOURCE source, IStructure structure, Color color)
        {
            MaterialLighting matLighting = new MorphologyMesh.MaterialLighting(source, structure, color);
            if(!Materials.ContainsKey(matLighting.Key))
            {
                Materials.Add(matLighting.Key, matLighting);
            }
            else
            {
                matLighting = Materials[matLighting.Key];
            }

            return matLighting;
        }
    }
}
