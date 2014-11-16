using System;
using System.Data;
using System.Configuration;
using System.Collections.Generic; 
using System.Linq; 
using System.Xml.Linq;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AnnotationVizLib
{
    /// <summary>
    /// Base class for both edges and nodes.  Contains a set of attributes.
    /// </summary>
    /// <typeparam name="KEY"></typeparam>
    public class GraphViewEntity<KEY>
        where KEY : IComparable<KEY>
    {
        public string label;

        public SortedDictionary<string, string> Attributes = new SortedDictionary<string, string>();

        /// <summary>
        /// Add the list of attributes to the node
        /// </summary>
        /// <param name="node"></param>
        /// <param name="attribs"></param>
        /// <returns></returns>
        public void AddAttributes(System.Collections.Generic.IDictionary<string, string> attribs)
        {

            foreach (string key in attribs.Keys)
            {
                Trace.WriteLineIf(this.Attributes.ContainsKey(key),
                                  "AddAttributes replacing existing key: " + key + " in " + this.ToString());

                this.Attributes[key] = attribs[key];
            }
        }

        public override string ToString()
        {
            string str = label + "\n";
            foreach (string key in this.Attributes.Keys)
            {
                str = str + "\t" + key + " : " + this.Attributes[key];
            }

            return str;
        }
    }

    public class GraphViewNode<KEY> : GraphViewEntity<KEY>
        where KEY : IComparable<KEY>
    {
        public KEY ID;
        
        public GraphViewNode(KEY id, string lbl)
        {
            this.ID = id;
            this.label = lbl;
        }

        public GraphViewNode(KEY id)
        {
            this.ID = id;
            this.label = id.ToString();
        } 
    }

    public class GraphViewEdge<KEY> : GraphViewEntity<KEY>, ICloneable
        where KEY : IComparable<KEY>
    {
        public KEY from;
        public KEY to;
        
        #region ICloneable Members

        public object Clone()
        {
            GraphViewEdge<KEY> clone = new GraphViewEdge<KEY>();
            clone.label = label;
            clone.from = from;
            clone.to = to;
            foreach (string key in Attributes.Keys)
            {
                clone.Attributes.Add(key, Attributes[key]);
            }

            return clone; 
        }

        /// <summary>
        /// This string lists the parent structures connected, i.e. cells
        /// </summary>
        public string KeyString
        {
            get
            {
                return to + "->" + from;
            }
        }

        #endregion
    }

    /// <summary>
    /// Holds a graph that we are going to build a view for.  Nodes and Edges can hold arbitrary lists of attributes.
    /// </summary>
    /// <typeparam name="KEY"></typeparam>
    public class GraphViewEngine<KEY> : GraphViewEntity<KEY>
        where KEY : IComparable<KEY>
    { 
        public SortedDictionary<KEY, GraphViewNode<KEY>> nodes = new SortedDictionary<KEY, GraphViewNode<KEY>>();
        public SortedDictionary<string, List<KEY>> subgraphs = new SortedDictionary<string, List<KEY>>(); 
        public List<GraphViewEdge<KEY>> edges = new List<GraphViewEdge<KEY>>();
        
        public void AssignNodeToSubgraph(string subgraphName, KEY NodeID)
        {
            //Determine if we have an entry in our subgraph or if we need to add it.
            if (!this.subgraphs.ContainsKey(subgraphName))
            {
                List<KEY> listSubgraphNodes = new List<KEY>();
                listSubgraphNodes.Add(NodeID);
                subgraphs.Add(subgraphName, listSubgraphNodes);
            }
            else
            {
                subgraphs[subgraphName].Add(NodeID);
            }
        }


        public GraphViewNode<KEY> addNode(KEY ID)
        {
            GraphViewNode<KEY> tempNode = new GraphViewNode<KEY>(ID);
            nodes.Add(ID,tempNode);
            return tempNode;
        }

        public void removeNode(KEY label)
        {
            if (nodes.ContainsKey(label))
                nodes.Remove(label);
        }

        public void addEdge(GraphViewEdge<KEY> edge)
        {
            edges.Add(edge); 
        }

        public void removeEdge(GraphViewEdge<KEY> edge)
        {
            if(edges.Contains(edge))
                edges.Remove(edge);
        }

       
    }
}
