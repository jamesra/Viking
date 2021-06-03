using Geometry;
using Geometry.Meshing;
using System.Collections.Generic;
using System.Linq;


namespace ColladaIO
{
    class MeshSerializer
    {
        /// <summary>
        /// Generates geometry for mesh.  All meshes are translated so the center of the bounding box is at 0,0,0 before generating geometry.
        /// Callers should translate geometry models as needed in the Collada Scene
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="name"></param>
        /// <param name="MaterialName"></param>
        /// <returns></returns>
        public static geometry_type CreateGeometry(IReadOnlyMesh3D<IVertex3D> mesh, string name, string MaterialName)
        {
            geometry_type geometry = new geometry_type();
            geometry.id = name + "-geometry";
            geometry.name = name;

            //Do not create geometry if we have no faces
            if (mesh.Faces.Count == 0)
                return null;

            geometry.Item = CreateMesh(mesh, geometry.id, MaterialName);

            return geometry;
        }

        private static mesh_type CreateMesh(IReadOnlyMesh3D<IVertex3D> mesh, string id, string MaterialName)
        {
            mesh_type dae_mesh = new mesh_type();

            GridVector3 center = -mesh.BoundingBox.CenterPoint;
             
            List<source_type> listSources = new List<ColladaIO.source_type>(2);
            listSources.Add(CreateSource(mesh.Verticies.Select(v => (v.Position + center) * 0.001).ToArray(), id, "positions"));
            listSources.Add(CreateSource(mesh.Verticies.Select(v => v.Normal).ToArray(), id, "normals"));

            dae_mesh.source = listSources.ToArray();

            dae_mesh.vertices = new vertices_type();
            dae_mesh.vertices.id = id + "-verticies";
              
            input_local_type input_type = new input_local_type();
            input_type.source = string.Format("#{0}-{1}", id, "positions");
            input_type.semantic = "POSITION";
             
            dae_mesh.vertices.input = new input_local_type[] { input_type };

            dae_mesh.Items = new object[] { CreateTriangles(mesh.Faces, id, MaterialName)};

            return dae_mesh;
        }

        private static source_type CreateSource(GridVector3[] verticies, string id, string array_type)
        {
            source_type source = new ColladaIO.source_type();
            source.id = string.Format("{0}-{1}", id, array_type);
            source.name = array_type;

            float_array_type float_array = new float_array_type();
            float_array.id = string.Format("{0}-{1}-{2}", id, array_type, "array");
            float_array.count = (ulong)verticies.LongLength * 3;

            float_array.Text = verticies.SelectMany(v => v.coords).ToArray();

            source.Item = float_array;
            source.technique_common = CreateStandardTechniqueForXYZ(verticies.LongLength, float_array.id);

            return source; 
        }

        private static source_typeTechnique_common CreateStandardTechniqueForXYZ(long ItemCount, string source_id)
        {
            source_typeTechnique_common technique = new source_typeTechnique_common();
            technique.accessor = new accessor_type();

            technique.accessor.param = CreateXYZFloatParams();
            technique.accessor.count = (ulong)ItemCount;
            technique.accessor.source = string.Format("#{0}", source_id);
            technique.accessor.stride = 3;

            return technique;
        }

        private static param_type[] CreateXYZFloatParams()
        {
            param_type X = new ColladaIO.param_type();
            X.name = "X";
            X.type = "float";

            param_type Y = new ColladaIO.param_type();
            Y.name = "Y";
            Y.type = "float";

            param_type Z = new ColladaIO.param_type();
            Z.name = "Z";
            Z.type = "float";

            return new param_type[] { X, Y, Z };
        }

        private static triangles_type CreateTriangles(ICollection<IFace> faces, string id, string MaterialName)
        {
            triangles_type triangles = new ColladaIO.triangles_type();
            triangles.count = (ulong)faces.Count;
            triangles.material = MaterialName;
            input_local_offset_type vertexInput = new ColladaIO.input_local_offset_type();
            input_local_offset_type normalInput = new ColladaIO.input_local_offset_type();

            vertexInput.offset = 0;
            vertexInput.semantic = "VERTEX";
            vertexInput.source = '#' + id + "-verticies";

            normalInput.offset = 0;
            normalInput.semantic = "NORMAL";
            normalInput.source = '#' + id + "-normals";

            triangles.input = new input_local_offset_type[] { vertexInput, normalInput };
            triangles.p = new ColladaIO.p_type();

            List<int> listFaceIndicies = new List<int>();
            listFaceIndicies.AddRange(faces.SelectMany(f => f.iVerts));
            triangles.p.Text = listFaceIndicies.Select(i => System.Convert.ToUInt64(i)).ToArray();

            return triangles;
        }
    }
}
