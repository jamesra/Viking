using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace MorphologyMesh
{
    public class MorphMeshRegionGraph : GraphLib.Graph<MorphMeshRegion, GraphLib.Node<MorphMeshRegion, MorphMeshRegionGraphEdge>, MorphMeshRegionGraphEdge>
    {
        public ImmutableSortedSet<double> ZLevels
        {
            get
            {
                SortedSet<double> set = [.. this.Nodes.SelectMany(n => n.Value.Key.ZLevel).Distinct()];
                return [.. set];
            }
        }

        public void AddNode(MorphMeshRegion region) => this.AddNode(new GraphLib.Node<MorphMeshRegion, MorphMeshRegionGraphEdge>(region));
    }

    public class MorphMeshRegionGraphEdge(MorphMeshRegion SourceNode, MorphMeshRegion TargetNode) : GraphLib.Edge<MorphMeshRegion>(SourceNode, TargetNode, false)
    {
    }

}
