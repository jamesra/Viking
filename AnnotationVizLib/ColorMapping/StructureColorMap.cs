﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;


namespace AnnotationVizLib
{ 
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
        public virtual System.Drawing.Color GetColor(AnnotationService.Structure structure)
        {
            if (structure == null)
                return System.Drawing.Color.Gray;

            if (structure_color_map != null)
            {
                if (this.structure_color_map.ContainsKey(structure.ID))
                {
                    return structure_color_map.GetColor(structure.ID);
                }
            }

            if (structureType_color_map != null)
            {
                if (this.structureType_color_map.ContainsKey(structure.TypeID))
                {
                    return structureType_color_map.GetColor(structure.TypeID);
                }
            }

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
         
        public System.Drawing.Color GetStructureColorFromMorphology(List<AnnotationService.Location> locations)
        {
            if (LocationColorMap == null)
                return System.Drawing.Color.Empty;

            return LocationColorMap.GetColor(locations);
        }

        public System.Drawing.Color GetStructureColorFromMorphology(List<AnnotationService.AnnotationPoint> points)
        {
            if (LocationColorMap == null)
                return System.Drawing.Color.Empty;

            return LocationColorMap.GetColor(points);
        } 

        /// <summary>
        /// Assign a color to the structure based on the mapping information we have
        /// </summary>
        /// <param name="structure"></param>
        /// <returns></returns>
        public System.Drawing.Color GetColor(MorphologyGraph graph)
        {
            if (graph.structure == null)
                return Color.Gray; 

            //Check for a default color.  If it does not exist use the morphology
            Color color = GetColor(graph.structure);
            if (!color.IsEmpty)
                return color;

            if (LocationColorMap == null)
                return Color.Gray;

            IEnumerable<MorphologyNode> nodes = graph.Nodes.Values.Where(v => LocationColorMap.SectionNumbers.Contains((int)v.Location.VolumePosition.Z));

            List<AnnotationService.AnnotationPoint> listPoints = nodes.Select<MorphologyNode, AnnotationService.AnnotationPoint>(n => n.Location.VolumePosition).ToList();
            return GetStructureColorFromMorphology(listPoints);
        } 
    }

}
