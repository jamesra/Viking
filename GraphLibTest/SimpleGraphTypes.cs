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
        public SimpleEdge(long Source, long Target) : base(Source, Target, false)
        { }

        public SimpleEdge(long Source, long Target, bool Directional) : base(Source, Target, Directional)
        { }

        public override string ToString()
        {
            return string.Format("{0} - {1} {2}", SourceNodeKey, TargetNodeKey, Directional ? "D" : "U");

        }
    }

    public class SimpleNode : GraphLib.Node<long, SimpleEdge>
    {
        public SimpleNode(long ID) : base(ID)
        { }

        public override string ToString()
        {
            return this.Key.ToString();
        }
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

        public static SimpleGraph CreateGraphWithCycle()
        {
            /*
             *  1 - 2  - 3 - 11
             *          /    |
             *         /     |
             *        /      |
             *       4       6 - 7
             *        \     /
             *         \   /
             *          \ /
             *     9     5
             *     |     | 
             *     10    6
             */
            SimpleGraph graph = new GraphLibTest.SimpleGraph();

            graph.AddNode(1);
            graph.AddNode(2);
            graph.AddNode(3);
            graph.AddNode(4);
            graph.AddNode(5);
            graph.AddNode(6);
            graph.AddNode(7);
            graph.AddNode(8);
            graph.AddNode(9);
            graph.AddNode(10);
            graph.AddNode(11);

            graph.AddEdge(1, 2);
            graph.AddEdge(2, 3);
            graph.AddEdge(3, 4);
            graph.AddEdge(3, 11);
            graph.AddEdge(11, 6);
            graph.AddEdge(4, 5);
            graph.AddEdge(6, 5);
            graph.AddEdge(6, 7);
            graph.AddEdge(5, 8);
            graph.AddEdge(9, 10);

            return graph;
        }
    }
}
