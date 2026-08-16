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
            COLLADA dae = new()
            {
                asset = AddStandardAssets(scene)
            };

            List<object> listElements =
            [
                CreateGeometryLibrary(scene.StructureModels.Values),
                CreateNodeLibrary(scene.RootModels.Values),
                CreateMaterialsLibrary(scene.Materials.Values),
                CreateEffectsLibrary(scene.Materials.Values),
                CreateLibraryVisualScenes(scene)
            ];

            dae.scene = CreateScene();
            dae.Items = [.. listElements];

            if (System.IO.File.Exists(Filename))
                System.IO.File.Delete(Filename);

            dae.Save(Filename);
        }

        public static void SerializeToFolder(IColladaScene scene, String Foldername)
        {
            if (!Directory.Exists(Foldername))
            {
                Directory.CreateDirectory(Foldername);
            }

            //////////////////////////////////////
            //Create a file to hold all materials
            COLLADA materialDae = new()
            {
                asset = AddStandardAssets(scene)
            };

            string MaterialsURL = "Materials.dae";
            string MaterialsFullPath = System.IO.Path.Combine(Foldername, MaterialsURL);
            List<object> listMaterials =
            [
                CreateMaterialsLibrary(scene.Materials.Values),
                CreateEffectsLibrary(scene.Materials.Values)
            ];
            materialDae.Items = [.. listMaterials];
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
            COLLADA SceneDAE = new()
            {
                asset = AddStandardAssets(scene),
                scene = CreateScene()
            };

            List<object> listNodes =
            [
                CreateLibraryVisualScenes(scene)
            ];

            SceneDAE.Items = [.. listNodes];
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
            COLLADA dae = new();

            ColladaIO.mesh_type mtype = new()
            {
                vertices = new vertices_type()
            };

            dae.asset = AddStandardAssets(scale);

            List<object> listElements = [];
            List<StructureModel> modelArray = model.ModelsInTree();
            listElements.Add(CreateGeometryLibrary(modelArray));
            listElements.Add(CreateNodeLibrary(modelArray, MaterialURL));

            dae.Items = [.. listElements];

            dae.Save(Filename);
        }

        private static COLLADAScene CreateScene()
        {
            COLLADAScene scene = new()
            {
                instance_visual_scene = new ColladaIO.instance_with_extra_type
                {
                    url = "#VisualSceneNode"
                }
            };

            return scene;
        }

        private static library_geometries_type CreateGeometryLibrary(IEnumerable<StructureModel> listModels)
        {
            library_geometries_type geomLib = new();

#if DEBUG
            geomLib.geometry = [.. listModels.Select(model => MeshSerializer.CreateGeometry(model.Mesh, model.Name, model.Material.Key)).Where(Geom => Geom != null)];
#else
            geomLib.geometry = listModels.Select(model => MeshSerializer.CreateGeometry(model.Mesh, model.Name, model.Material.Key)).AsParallel().Where(Geom => Geom != null).ToArray();
#endif

            return geomLib;
        }

        private static library_nodes_type CreateNodeLibrary(IEnumerable<StructureModel> listModels, string MaterialURL = null)
        {
            library_nodes_type nodesLib = new();

            List<node_type> nodes = [];

            nodesLib.node = [.. listModels.Select(model => CreateLibraryNode(model, MaterialURL, false))];

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
            node_type node = new()
            {
                id = model.NodeName,
                name = model.NodeName
            };

            List<object> NodeItems = [];

            instance_geometry_type instance_geometry = new()
            {
                url = $"#{model.Name}-geometry"
            };

            if (ApplyTranslation)
            {
                translate_type translation = new()
                {
                    sid = "translate",
                    Text = model.Translation.Coords
                };
                NodeItems.Add(translation);
            }

            bind_material_type mat_binding = new();

            instance_material_type mat_instance = new()
            {
                symbol = model.Material.Key,
                target = MaterialURL is null ? "#" + model.Material.Key : string.Format("{0}#{1}", MaterialURL, model.Material.Key)
            };
            mat_binding.technique_common = [mat_instance];

            instance_geometry.bind_material = mat_binding;

            node.instance_geometry = [instance_geometry];

            //TODO: AsParallel?
            if (model.ChildStructures != null)
            {
                List<node_type> childNodes = [];
                node.node = [.. model.ChildStructures.Values.Select(child => CreateLibraryNode(child, MaterialURL, true))];
            }

            node.Items = [.. NodeItems];
            return node;
        }

        private static asset_type AddStandardAssets(IColladaScene scene) => AddStandardAssets(scene.Scale);

        private static asset_type AddStandardAssets(IAxisUnits scale)
        {
            asset_type asset = new()
            {
                contributor = [CreateVikingContributorAsset()]
            };

            DateTime rightNow = DateTime.UtcNow;
            asset.created = rightNow;
            asset.modified = rightNow;
            asset.up_axis = up_axis_enum.Z_UP;

            asset.unit = scale.AsTypeUnit();

            return asset;
        }

        public static asset_typeUnit AsTypeUnit(this IAxisUnits axis)
        {
            asset_typeUnit unit = new()
            {
                meter = axis.Value,
                name = axis.Units
            };
            return unit;
        }

        private static asset_typeContributor CreateVikingContributorAsset()
        {
            asset_typeContributor contributor = new()
            {
                authoring_tool = "Viking",
                author_website = "http://codepharm.net/"
            };

            return contributor;
        }

        private static library_materials_type CreateMaterialsLibrary(IEnumerable<MaterialLighting> materials)
        {
            library_materials_type materials_library = new();

            List<material_type> listMaterials = [];
            foreach (MaterialLighting material in materials)
            {
                listMaterials.Add(CreateMaterial(material));
            }

            materials_library.material = [.. listMaterials];

            return materials_library;
        }

        private static material_type CreateMaterial(MaterialLighting material)
        {
            material_type mat = new()
            {
                id = material.Key,
                name = material.Key,
                instance_effect = new instance_effect_type
                {
                    url = $"#{material.FXName}"
                }
            };

            return mat;
        }

        private static library_effects_type CreateEffectsLibrary(IEnumerable<MaterialLighting> materials)
        {
            library_effects_type effects_library = new();

            List<effect_type> effects = [];
            foreach (MaterialLighting mat in materials)
            {
                effect_type effect = CreateEffect(mat);
                effects.Add(effect);
            }

            effects_library.effect = [.. effects];

            return effects_library;
        }

        /// <summary>
        /// Create an effect to light a material
        /// </summary>
        /// <param name="matLighting"></param>
        /// <returns></returns>
        private static effect_type CreateEffect(MaterialLighting material)
        {
            effect_type effect = new()
            {
                id = material.FXName,

                profile_COMMON = [CreateEffectProfile(material)]
            };

            return effect;
        }

        private static profile_common_type CreateEffectProfile(MaterialLighting material)
        {
            profile_common_type profile = new()
            {
                technique = CreateTechnique(material)
            };

            return profile;
        }

        private static profile_common_typeTechnique CreateTechnique(MaterialLighting material)
        {
            profile_common_typeTechnique tech = new()
            {
                sid = "common"
            };

            profile_common_typeTechniqueLambert lambert = new();

            fx_common_color_or_texture_typeColor color = new()
            {
                Text = material.Diffuse.ToElements()
            };


            fx_common_color_or_texture_type item = new()
            {
                Item = color
            };

            lambert.diffuse = item;
            lambert.reflective = item;

            fx_common_float_or_param_type reflectivity = new()
            {
                Item = material.Reflectivity.ToColladaFloat()
            };

            lambert.reflectivity = reflectivity;

            fx_common_float_or_param_type index_of_refraction = new()
            {
                Item = material.RefractionIndex.ToColladaFloat()
            };

            lambert.index_of_refraction = index_of_refraction;

            tech.Item = lambert;

            return tech;
        }

        private static library_visual_scenes_type CreateLibraryVisualScenes(IColladaScene scene)
        {
            library_visual_scenes_type scene_library = new();

            visual_scene_type visual_scene = new()
            {
                id = "VisualSceneNode",
                name = scene.Title ?? "untitled"
            };

            List<node_type> listNodes = [];
            foreach (StructureModel model in scene.RootModels.Values)
            {
                listNodes.Add(CreateVisualSceneNodes(model));
            }

            visual_scene.node = [.. listNodes];

            scene_library.visual_scene = [visual_scene];
            return scene_library;
        }

        private static node_type CreateVisualSceneNodes(StructureModel model)
        {
            node_type node = new()
            {
                name = $"node-{model.Name}"
            };

            translate_type translation = new()
            {
                Text = model.Translation.Coords
            };

            instance_node_type node_instance = new()
            {
                url = model.InstanceURL
            };

            node.instance_node = [node_instance];

            node.Items = [translation];

            return node;
        }
    }
}
