using Viking.AnnotationServiceTypes.Interfaces;
using SqlGeometryUtils;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;


namespace AnnotationVizLib
{
    /// <summary>
    /// Indicates the source of the color 
    /// </summary>
    public enum COLORSOURCE
    {
        NONE,
        STRUCTURE,
        STRUCTURETYPE,
        LOCATION
    }

    /// <summary>
    /// Color mapping for structures, based on StructureType, StructureID, and then morphology
    /// </summary>
    public class StructureColorMap
    {
        ColorMapWithLong structure_color_map = null;
        ColorMapWithLong structureType_color_map = null;

        public StructureColorMap(ColorMapWithLong structureTypeColorMap,
                                           ColorMapWithLong structureColorMap)
        {
            this.structure_color_map = structureColorMap;
            this.structureType_color_map = structureTypeColorMap;
        }

        /// <summary>
        /// Assign a color to the structure based on the mapping information we have
        /// </summary>
        /// <param name="structure"></param>
        /// <returns></returns>
        public virtual System.Drawing.Color GetColor(IStructure structure)
        {
            COLORSOURCE source;
            return GetColor(structure, out source);
        }

        /// <summary>
        /// Assign a color to the structure based on the mapping information we have
        /// </summary>
        /// <param name="structure"></param>
        /// <returns></returns>
        public virtual System.Drawing.Color GetColor(IStructure structure, out COLORSOURCE source)
        {
            if (structure == null)
            {
                source = COLORSOURCE.NONE;
                return System.Drawing.Color.Gray;
            }

            if (structure_color_map != null)
            {
                if (this.structure_color_map.ContainsKey((long)structure.ID))
                {
                    source = COLORSOURCE.STRUCTURE;
                    return structure_color_map.GetColor((long)structure.ID);
                }
            }

            if (structureType_color_map != null)
            {
                if (this.structureType_color_map.ContainsKey((long)structure.TypeID))
                {
                    source = COLORSOURCE.STRUCTURETYPE;
                    return structureType_color_map.GetColor((long)structure.TypeID);
                }
            }

            source = COLORSOURCE.NONE;
            return System.Drawing.Color.Gray;
        }
    }

    public class StructureMorphologyColorMap : StructureColorMap
    {
        ColorMapWithImages LocationColorMap = null;

        public StructureMorphologyColorMap(ColorMapWithLong structureTypeColorMap,
                                           ColorMapWithLong structureColorMap,
                                           ColorMapWithImages locationColorMap) : base(structureTypeColorMap, structureColorMap)
        {
            this.LocationColorMap = locationColorMap;
        }

        private System.Drawing.Color GetStructureColorFromMorphology(ICollection<ILocation> locations)
        {
            if (LocationColorMap == null)
                return System.Drawing.Color.Empty;

            return LocationColorMap.GetColor(locations);
        }

        private System.Drawing.Color GetStructureColorFromMorphology(ICollection<Geometry.GridVector3> points)
        {
            if (LocationColorMap == null)
                return System.Drawing.Color.Empty;

            return LocationColorMap.GetColor(points);
        }

        public System.Drawing.Color GetColor(MorphologyGraph graph)
        {
            COLORSOURCE source;
            return GetColor(graph, out source);
        }

        /// <summary>
        /// The standard color map returns the first color found in this list.
        /// 1. Structures are checked to see if they have a color explicitely defined. 
        /// 2. The structure type is then checkd to see if it has a color defined.
        /// 3. Annotations are tested to see if they intersect the image color map.
        /// 4. A standard grey color is returned.
        /// </summary>
        /// <param name="structure"></param>
        /// <returns></returns>
        public System.Drawing.Color GetColor(MorphologyGraph graph, out COLORSOURCE source)
        {
            if (graph.structure == null)
            {
                source = COLORSOURCE.NONE;
                return Color.Gray;
            }

            //Check for a default color.  If it does not exist use the morphology
            Color color = GetColor(graph.structure, out source);
            if (!color.IsEmpty)
            {
                return color;
            }

            if (LocationColorMap == null)
            {
                source = COLORSOURCE.NONE;
                return Color.Gray;
            }

            IEnumerable<MorphologyNode> nodes = graph.Nodes.Values.Where(v => LocationColorMap.SectionNumbers.Contains((int)v.Location.UnscaledZ));

            List<Geometry.GridVector3> listPoints = nodes.Select<MorphologyNode, Geometry.GridVector3>(n =>
                n.Geometry.Centroid().ToGridVector3(n.UnscaledZ)
             ).ToList();

            source = COLORSOURCE.LOCATION;
            return GetStructureColorFromMorphology(listPoints);
        }
    }
}
