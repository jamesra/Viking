using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;

namespace AnnotationVizLib
{
    /// <summary>
    /// Describes the path between two substructures in a morphology graph
    /// </summary>
    public class PathData
    {
        public IList<ulong> Path;
        public MorphologyNode NearestNodeToSource;
        public MorphologyNode NearestNodeToTarget;
        public ulong SourceStructureID;
        public ulong TargetStructureID;
        public double Distance;


        public static string ToMatlabStructures(ICollection<PathData> paths, string VariableName = "Path")
        {
            StringBuilder sb = new StringBuilder();
            double[] distances = paths.Select(p => p.Distance).ToArray();
            sb.AppendFormat("{0}.Distances = {1};", VariableName, distances.ToMatlab());

            return sb.ToString();
        }
    }
}
