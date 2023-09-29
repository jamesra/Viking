using Viking.AnnotationServiceTypes.Interfaces;
using AnnotationVizLib;
using Geometry;
using Geometry.Meshing;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using UnitsAndScale;

namespace MorphologyMesh
{
    public class MaterialLighting
    {
        /// <summary>
        /// Helper function to create consistent key names for structure materials.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="structure"></param>
        /// <returns></returns>
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
        /// Helper function to create consistent key names for structure materials.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="structure"></param>
        /// <returns></returns>
        public static string CreateKey(COLORSOURCE source, ulong ID)
        {
            switch (source)
            {
                case COLORSOURCE.STRUCTURE:
                    return string.Format("Structure{0}", ID);
                case COLORSOURCE.STRUCTURETYPE:
                    return string.Format("Type{0}", ID);
                case COLORSOURCE.LOCATION:
                    return string.Format("Structure{0}", ID);
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
                return $"Struct-{ID}";
            }
        }

        public string NodeName
        {
            get
            {
                return $"NodeID-{ID}";
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


        public readonly IReadOnlyMesh3D<IVertex3D> Mesh;

        public readonly MaterialLighting Material;

        private readonly SortedList<ulong, StructureModel> _ChildStructures = new SortedList<ulong, StructureModel>();

        public IReadOnlyDictionary<ulong, StructureModel> ChildStructures
        {
            get {
                return _ChildStructures as IReadOnlyDictionary<ulong, StructureModel>;
            }
         }

        public StructureModel(ulong id, IReadOnlyMesh3D<IVertex3D> mesh, MaterialLighting mat)
        {
            ID = id;
            Mesh = mesh;
            Material = mat;
            
            //GridVector3 TranslationVector = mesh.BoundingBox.CenterPoint;

            //Mesh.Translate(TranslationVector);

            //Translation = TranslationVector;
        }

        private GridVector3 _Translation;

        /// <summary>
        /// The translation vector required to place the model's bounding box center at 0,0,0
        /// </summary>
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
            //child.Translation = child.Translation;
            _ChildStructures.Add(child.ID, child); 
        }

        /// <summary>
        /// Return the model and all child models recursively
        /// </summary>
        /// <returns></returns>
        public List<StructureModel> ModelsInTree()
        {
            List<StructureModel> listModel = new List<MorphologyMesh.StructureModel>
            {
                this
            };

            listModel.AddRange(this.ChildStructures.Values.SelectMany(cs => cs.ModelsInTree()));
            return listModel; 
        }
    }

    /// <summary>
    /// An interface to an object that describes a scene the ColladaIO package can serialize
    /// </summary>
    public interface IColladaScene
    {

        string Title { get; }

        /// <summary>
        /// Scale should be the same in all three axes in a DAE scene
        /// </summary>
        UnitsAndScale.IAxisUnits Scale { get; }

        IReadOnlyDictionary<ulong, StructureModel> RootModels { get; }
        IReadOnlyDictionary<ulong, StructureModel> StructureModels { get; }
        IReadOnlyDictionary<string, MaterialLighting> Materials { get; }
    }

    /// <summary>
    /// A simple implementation of a Collada scene that assumes all meshes are generated and contained in StructureModels that will be added to the scene by the caller
    /// </summary>
    public class BasicColladaView : IColladaScene
    {
        public string SceneTitle = null;


        readonly StructureColorMap Colormap = null;

        public SortedDictionary<ulong, StructureModel> RootModels = new SortedDictionary<ulong, StructureModel>();

        public SortedDictionary<ulong, StructureModel> StructureModels = new SortedDictionary<ulong, StructureModel>();

        public SortedDictionary<string, MaterialLighting> Materials = new SortedDictionary<string, MaterialLighting>();

        #region IColladaScene
        string IColladaScene.Title => SceneTitle;

        public IAxisUnits Scale { get; private set; }

        IReadOnlyDictionary<ulong, StructureModel> IColladaScene.RootModels => RootModels;

        IReadOnlyDictionary<ulong, StructureModel> IColladaScene.StructureModels => StructureModels;

        IReadOnlyDictionary<string, MaterialLighting> IColladaScene.Materials => Materials;
        #endregion


        public BasicColladaView(IAxisUnits scale, StructureMorphologyColorMap colormap)
        {
            Colormap = colormap;
            if (Colormap == null)
                Colormap = new StructureColorMap(null, null);

            this.Scale = scale;
        }

        /// <summary>
        /// Add a root level structure to the view
        /// </summary>
        /// <param name="structure"></param>
        public void Add(StructureModel structure)
        {
            RootModels[structure.ID] = structure;
            AddModel(structure);
        }

        /// <summary>
        /// Add a structure, and all of its children, to the scene
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        private StructureModel AddModel(IStructure structure, IReadOnlyMesh3D<IVertex3D> structureMesh)
        {
            COLORSOURCE source = COLORSOURCE.STRUCTURE;
            System.Drawing.Color color = Colormap.GetColor(structure, out source);

            MaterialLighting material = GetOrAddMaterial(source, structure, color);
            StructureModel model = new MorphologyMesh.StructureModel(structure.ID, structureMesh, material);

            AddModel(model);

            return model;
        }

        /// <summary>
        /// Add a structure, and all of its children, to the scene
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        private void AddModel(StructureModel model)
        {
            StructureModels[model.ID] = model;
            
            GetOrAddMaterial(model.Material);
            
            foreach (var child in model.ChildStructures.Values)
            {
                StructureModels[child.ID] = child;
            }
        }

        /// <summary>
        /// Ensure the material is added to our dictionary and return the key
        /// </summary>
        /// <param name="source"></param>
        /// <param name="structure"></param>
        /// <returns></returns>
        private MaterialLighting GetOrAddMaterial(MaterialLighting material)
        {
            MaterialLighting matLighting = material;
            if (!Materials.ContainsKey(matLighting.Key))
            {
                Materials.Add(matLighting.Key, matLighting);
            }
            else
            {
                matLighting = Materials[matLighting.Key];
            }

            return matLighting;
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
            if (!Materials.ContainsKey(matLighting.Key))
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

    /// <summary>
    /// Converts a MorphologyGraph into a ColladaScene
    /// </summary>
    public class MorphologyColladaView : IColladaScene
    {
        public readonly IScale Scale;

        public string SceneTitle = null;

        readonly StructureMorphologyColorMap Colormap = null;

        public SortedDictionary<ulong, StructureModel> RootModels = new SortedDictionary<ulong, StructureModel>();

        public SortedDictionary<ulong, StructureModel> StructureModels = new SortedDictionary<ulong, StructureModel>();

        public SortedDictionary<string, MaterialLighting> Materials = new SortedDictionary<string, MaterialLighting>();

        #region IColladaScene
        string IColladaScene.Title => SceneTitle;

        IAxisUnits IColladaScene.Scale => Scale.X;

        IReadOnlyDictionary<ulong, StructureModel> IColladaScene.RootModels => RootModels;

        IReadOnlyDictionary<ulong, StructureModel> IColladaScene.StructureModels => StructureModels;

        IReadOnlyDictionary<string, MaterialLighting> IColladaScene.Materials => Materials;
        #endregion


        public MorphologyColladaView(UnitsAndScale.IScale scale, StructureMorphologyColorMap colormap)
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
            Mesh3D<IVertex3D<ulong>> structureMesh = TopologyMeshGenerator.Generate(structure);
            StructureModel model = null;

            if (structureMesh != null)
            {
                structureMesh.Scale(0.001);
                System.Drawing.Color color = Colormap.GetColor(structure, out COLORSOURCE source);

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
