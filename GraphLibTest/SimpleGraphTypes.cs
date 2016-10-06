using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GraphLib;

namespace GraphLibTest
{
    public class SimpleEdge : GraphLib.Edge<long>
    {
        public SimpleEdge(long Source, long Target) : base(Source, Target)
        { }
    }

    public class SimpleNode : GraphLib.Node<long, SimpleEdge>
    {
        public SimpleNode(long ID) : base(ID)
        { }
    }

    public class SimpleGraph : GraphLib.Graph<long, SimpleNode, SimpleEdge>
    {
        public void AddNode(long ID)
        {
            this.AddNode(new SimpleNode(ID));
        }

        public void AddEdge(long Source, long Target)
        {
            this.AddEdge(new SimpleEdge(Source, Target));
        }
    }
}
