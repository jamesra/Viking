using MorphologyMesh;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnitsAndScale;

namespace ColladaIO
{
    public static class DynamicRenderMeshColladaSerializer
    {

        public static void SerializeToFile(IColladaScene scene, String Filename)
        {
            COLLADA dae = new COLLADA();

            dae.asset = AddStandardAssets(scene);

            List<object> listElements = new List<object>();
              
            listElements.Add(CreateGeometryLibrary(scene.StructureModels.Values));
            listElements.Add(CreateNodeLibrary(scene.RootModels.Values));
            listElements.Add(CreateMaterialsLibrary(scene.Materials.Values));
            listElements.Add(CreateEffectsLibrary(scene.Materials.Values));
            listElements.Add(CreateLibraryVisualScenes(scene));

            dae.scene = CreateScene();
            dae.Items = listElements.ToArray();

            if (System.IO.File.Exists(Filename))
                System.IO.File.Delete(Filename);

            dae.Save(Filename); 
        }

        public static void SerializeToFolder(IColladaScene scene, String Foldername)
        {
            if(!Directory.Exists(Foldername))
            {
                Directory.CreateDirectory(Foldername); 
            }

            //////////////////////////////////////
            //Create a file to hold all materials
            COLLADA materialDae = new COLLADA();
            materialDae.asset = AddStandardAssets(scene);

            string MaterialsURL = "Materials.dae";
            string MaterialsFullPath = System.IO.Path.Combine(Foldername, MaterialsURL);
            List<object> listMaterials = new List<object>();
            listMaterials.Add(CreateMaterialsLibrary(scene.Materials.Values));
            listMaterials.Add(CreateEffectsLibrary(scene.Materials.Values));
            materialDae.Items = listMaterials.ToArray();
            materialDae.Save(MaterialsFullPath); 
            /////////////////////////////////////
            
            ///////////////////////////////////////////
            //Create a file for each model in the scene
            foreach (StructureModel model in scene.RootModels.Values)
            {
                model.GeometryURL = string.Format("{0}.dae", model.ID);
                Serialize(model, scene.Scale, MaterialsURL, System.IO.Path.Combine(Foldername, model.GeometryURL));
            }
            ///////////////////////////////////////////

            /////////////////////////////////////////////////////////////////////
            //Create a scene file to instantiate every model in the various files
            COLLADA SceneDAE = new ColladaIO.COLLADA();
            SceneDAE.asset = AddStandardAssets(scene);
            SceneDAE.scene = CreateScene();

            List<object> listNodes = new List<object>();
            listNodes.Add(CreateLibraryVisualScenes(scene));

            SceneDAE.Items = listNodes.ToArray();
            string SceneFilename = System.IO.Path.Combine(Foldername, "Scene.dae");
            SceneDAE.Save(SceneFilename);

            /////////////////////////////////////////////////////////////////////
            
        }

        /// <summary>
        /// Used to create an individual file for a mesh that is linked from a master scene file.
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="Filename"></param>
        public static void Serialize(StructureModel model, IAxisUnits scale, String MaterialURL, String Filename)
        {  
            COLLADA dae = new COLLADA();

            ColladaIO.mesh_type mtype = new mesh_type();
            mtype.vertices = new vertices_type();

            dae.asset = AddStandardAssets(scale);

            List<object> listElements = new List<object>();
            List<StructureModel> modelArray = model.ModelsInTree();
            listElements.Add(CreateGeometryLibrary(modelArray));
            listElements.Add(CreateNodeLibrary(modelArray, MaterialURL));

            dae.Items = listElements.ToArray();

            dae.Save(Filename);
        }        

        private static COLLADAScene CreateScene()
        {
            COLLADAScene scene = new COLLADAScene();
            scene.instance_visual_scene = new ColladaIO.instance_with_extra_type();
            scene.instance_visual_scene.url = "#VisualSceneNode";

            return scene;
        }

        private static library_geometries_type CreateGeometryLibrary(IEnumerable<StructureModel> listModels)
        {
            library_geometries_type geomLib = new library_geometries_type();
              
#if DEBUG
            geomLib.geometry = listModels.Select(model => MeshSerializer.CreateGeometry(model.Mesh, model.Name, model.Material.Key)).Where(Geom => Geom != null).ToArray();
#else
            geomLib.geometry = listModels.Select(model => MeshSerializer.CreateGeometry(model.Mesh, model.Name, model.Material.Key)).AsParallel().Where(Geom => Geom != null).ToArray();
#endif

            return geomLib;
        }

        private static library_nodes_type CreateNodeLibrary(IEnumerable<StructureModel> listModels, string MaterialURL = null)
        {
            library_nodes_type nodesLib = new library_nodes_type();

            List<node_type> nodes = new List<node_type>();

            nodesLib.node = listModels.Select(model => CreateLibraryNode(model, MaterialURL, false)).ToArray();

            return nodesLib;
        }
        
        /// <summary>
        /// Create a library node that binds the geometry to a material.  Child nodes have a position relative to the parent
        /// </summary>
        /// <param name="model"></param>
        /// <param name="MaterialURL"></param>
        /// <param name="ApplyTranslation">If true create a translate element relative to the parent</param>
        /// <returns></returns>
        private static node_type CreateLibraryNode(StructureModel model, string MaterialURL, bool ApplyTranslation)
        {
            node_type node = new ColladaIO.node_type();
            node.id = model.NodeName;
            node.name = model.NodeName;

            List<object> NodeItems = new List<object>();

            instance_geometry_type instance_geometry = new instance_geometry_type();
            instance_geometry.url = "#" + model.Name + "-geometry";

            if(ApplyTranslation)
            {
                translate_type translation = new ColladaIO.translate_type();
                translation.sid = "translate";
                translation.Text = model.Translation.coords;
                NodeItems.Add(translation);
            }

            bind_material_type mat_binding = new bind_material_type();

            instance_material_type mat_instance = new instance_material_type();

            mat_instance.symbol = model.Material.Key;
            mat_instance.target = MaterialURL == null ? "#" + model.Material.Key : string.Format("{0}#{1}", MaterialURL, model.Material.Key);
            mat_binding.technique_common = new instance_material_type[] { mat_instance };

            instance_geometry.bind_material = mat_binding;
            
            node.instance_geometry = new instance_geometry_type[] { instance_geometry };

            //TODO: AsParallel?
            if (model.ChildStructures != null)
            {
                List<node_type> childNodes = new List<node_type>();
                node.node = model.ChildStructures.Values.Select(child => CreateLibraryNode(child, MaterialURL, true)).ToArray();
            }

            node.Items = NodeItems.ToArray();
            return node;
        }

        private static asset_type AddStandardAssets(IColladaScene scene)
        {
            return AddStandardAssets(scene.Scale);
        }

        private static asset_type AddStandardAssets(IAxisUnits scale)
        {
            asset_type asset = new asset_type();
            asset.contributor = new asset_typeContributor[] { CreateVikingContributorAsset() };

            DateTime rightNow = DateTime.UtcNow;
            asset.created = rightNow;
            asset.modified = rightNow;
            asset.up_axis = up_axis_enum.Z_UP;

            asset.unit = scale.AsTypeUnit();

            return asset;
        }

        public static asset_typeUnit AsTypeUnit(this IAxisUnits axis)
        {
            asset_typeUnit unit = new asset_typeUnit();
            unit.meter = axis.Value;
            unit.name = axis.Units;
            return unit;
        }

        private static asset_typeContributor CreateVikingContributorAsset()
        {
            asset_typeContributor contributor = new asset_typeContributor();
            contributor.authoring_tool = "Viking";
            contributor.author_website = "http://connectomes.utah.edu/";

            return contributor;
        }

        private static library_materials_type CreateMaterialsLibrary(IEnumerable<MaterialLighting> materials)
        {
            library_materials_type materials_library = new library_materials_type();

            List<material_type> listMaterials = new List<ColladaIO.material_type>();
            foreach(MaterialLighting material in materials)
            {
                listMaterials.Add(CreateMaterial(material));
            }

            materials_library.material = listMaterials.ToArray();

            return materials_library;
        }

        private static material_type CreateMaterial(MaterialLighting material)
        {
            material_type mat = new material_type();
            mat.id = material.Key;
            mat.name = material.Key;
            mat.instance_effect = new instance_effect_type();
            mat.instance_effect.url = string.Format("#{0}", material.FXName);

            return mat;
        }

        private static library_effects_type CreateEffectsLibrary(IEnumerable<MaterialLighting> materials)
        {
            library_effects_type effects_library = new ColladaIO.library_effects_type();

            List<effect_type> effects = new List<effect_type>();
            foreach (MaterialLighting mat in materials)
            {
                effect_type effect = CreateEffect(mat);
                effects.Add(effect);
            }

            effects_library.effect = effects.ToArray();

            return effects_library;
        }

        /// <summary>
        /// Create an effect to light a material
        /// </summary>
        /// <param name="matLighting"></param>
        /// <returns></returns>
        private static effect_type CreateEffect(MaterialLighting material)
        {
            effect_type effect = new ColladaIO.effect_type();
            effect.id = material.FXName;

            effect.profile_COMMON = new profile_common_type[] { CreateEffectProfile(material) };

            return effect;
        }

        private static profile_common_type CreateEffectProfile(MaterialLighting material)
        {
            profile_common_type profile = new ColladaIO.profile_common_type();

            profile.technique = CreateTechnique(material);

            return profile;
        }

        private static profile_common_typeTechnique CreateTechnique(MaterialLighting material)
        {
            profile_common_typeTechnique tech = new ColladaIO.profile_common_typeTechnique();
            tech.sid = "common";

            profile_common_typeTechniqueLambert lambert = new profile_common_typeTechniqueLambert();

            fx_common_color_or_texture_typeColor color = new fx_common_color_or_texture_typeColor();
            color.Text = material.Diffuse.ToElements();


            fx_common_color_or_texture_type item = new fx_common_color_or_texture_type();
            item.Item = color;

            lambert.diffuse = item;
            lambert.reflective = item;
              
            fx_common_float_or_param_type reflectivity = new fx_common_float_or_param_type();

            reflectivity.Item = material.Reflectivity.ToColladaFloat(); 

            lambert.reflectivity = reflectivity;

            fx_common_float_or_param_type index_of_refraction = new fx_common_float_or_param_type();
            index_of_refraction.Item = material.RefractionIndex.ToColladaFloat();

            lambert.index_of_refraction = index_of_refraction;

            tech.Item = lambert;

            return tech;
        }

        private static library_visual_scenes_type CreateLibraryVisualScenes(IColladaScene scene)
        {
            library_visual_scenes_type scene_library = new library_visual_scenes_type();

            visual_scene_type visual_scene = new ColladaIO.visual_scene_type();

            visual_scene.id = "VisualSceneNode";
            visual_scene.name = scene.Title ?? "untitled";

            List<node_type> listNodes = new List<node_type>();
            foreach(StructureModel model in scene.RootModels.Values)
            {
                listNodes.Add(CreateVisualSceneNodes(model));
            }

            visual_scene.node = listNodes.ToArray();

            scene_library.visual_scene = new visual_scene_type[] { visual_scene };
            return scene_library;
        }

        private static node_type CreateVisualSceneNodes(StructureModel model)
        {
            node_type node = new node_type();
            node.name = "node-" + model.Name;

            translate_type translation = new translate_type();
            translation.Text = model.Translation.coords;

            instance_node_type node_instance = new instance_node_type();
            node_instance.url = model.InstanceURL;

            node.instance_node = new instance_node_type[] { node_instance };

            node.Items = new object[] { translation };

            return node; 
        }
    }
}
