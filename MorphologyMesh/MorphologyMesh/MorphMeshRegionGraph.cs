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
                var set = new SortedSet<double>(this.Nodes.SelectMany(n => n.Value.Key.ZLevel).Distinct());
                return set.ToImmutableSortedSet();
            }
        }

        public void AddNode(MorphMeshRegion region)
        {
            this.AddNode(new GraphLib.Node<MorphMeshRegion, MorphMeshRegionGraphEdge>(region));
        }
    }

    public class MorphMeshRegionGraphEdge : GraphLib.Edge<MorphMeshRegion>
    {
        public MorphMeshRegionGraphEdge(MorphMeshRegion SourceNode, MorphMeshRegion TargetNode) : base(SourceNode, TargetNode, false)
        {
        }
    }

}
