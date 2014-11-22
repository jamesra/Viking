using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AnnotationVizLib
{
    public static class DOTAttributes
    {
        public static SortedDictionary<string, string> StandardGraphDOTAttributes = new SortedDictionary<string, string>()
        {
            {"splines", "true"},
            {"dpi", "600"},
            {"ratio", "compress"},
            {"regular", "true"}, 
            //{"page", "11,8.5"},
            //{"size", "10,7.5"}, 
            {"center", "true"},
            {"minlen", "1"},
            {"sep", "1"},
            {"ranksep", "0.1"},
            {"nodesep", "0.5"}, 
            {"mode", "major"},
            {"model", "subset"},
            {"maxiter", "10000"}
        };

        public static SortedDictionary<string, string> StandardNodeDOTAttributes = new SortedDictionary<string, string>() {
            {"peripheries", "3"},
            {"fontcolor", "white"},
            {"style", "filled"},
            {"penwidth", "0.0"},
            {"fontname", "Helvetica"}
        };

        /// <summary>
        /// A mapping of labels to node properties.  
        /// An exact match is checked first, if it does not exist the
        /// we search the keys in the order they appear in the list.  The first partial match
        /// has its attributes returned.
        /// </summary>
        public static IList<AttributeMap> StandardLabelToNodeDOTAppearance = new List<AttributeMap>()
        {   
            new AttributeMap("AXON", new SortedList<string,string> {
                            {"fillcolor", "Red3"},
                            {"shape", "hexagon"}
                }
            ),
            new AttributeMap("DENDRITE", new SortedList<string,string> {
                            {"fillcolor", "green3"} 
                }
            ),
            new AttributeMap("CBAB", new SortedList<string,string> {
                            {"fillcolor", "green4"} 
                }
            ),
            new AttributeMap("GBC", new SortedList<string,string> {
                            {"fillcolor", "cadetblue"} 
                }
            ),
            new AttributeMap("CBB", new SortedList<string,string> {
                            {"fillcolor", "cadetblue"} 
                }
            ),
            new AttributeMap("AII", new SortedList<string,string> {
                            {"fillcolor", "yellow3"},
                            {"shape", "hexagon"}
                }
            ),
            new AttributeMap("S1", new SortedList<string,string> {
                            {"fillcolor", "palevioletred1"},
                            {"shape", "diamond"}
                }
            ),
            new AttributeMap("S2", new SortedList<string,string> {
                            {"fillcolor", "palevioletred4"},
                            {"shape", "diamond"}
                }
            ),
            new AttributeMap("AI", new SortedList<string,string> {
                            {"fillcolor", "orchid"},
                            {"shape", "diamond"}
                }
            ),
            new AttributeMap("STARBURST", new SortedList<string,string> {
                           {"fillcolor", "hotpink"},
                           {"shape", "diamond"}
                }
            ),
            new AttributeMap("IAC", new SortedList<string,string> {
                            {"fillcolor", "brown1"},
                            {"shape", "invtrapezium"}
                }
            ),
            new AttributeMap("ROD BC", new SortedList<string,string> {
                            {"fillcolor", "purple"} 
                }
            ),
            new AttributeMap("OFF", new SortedList<string,string> {
                            {"fillcolor", "blue"} 
                }
            ),
            new AttributeMap("CBA", new SortedList<string,string> {
                            {"fillcolor", "blue"} 
                }
            ),
            new AttributeMap("BC", new SortedList<string,string> {
                            {"fillcolor", "grey"} 
                }
            ),
            new AttributeMap("AXC", new SortedList<string,string> {
                            {"fillcolor", "orange"},
                            {"shape", "doubleoctagon"}
                }
            ),
            new AttributeMap("YAC", new SortedList<string,string> {
                            {"fillcolor", "Red3"},
                            {"shape", "triangle"}
                }
            ),
            new AttributeMap("GABA", new SortedList<string,string> {
                            {"fillcolor", "Red3"},
                            {"shape", "triangle"}
                }
            ),
            new AttributeMap("GLY", new SortedList<string,string> {
                            {"fillcolor", "green3"},
                            {"shape", "invtriangle"}
                }
            ),
            new AttributeMap("GAC", new SortedList<string,string> {
                            {"fillcolor", "green3"},
                            {"shape", "invtriangle"}
                }
            ),
            new AttributeMap("AC", new SortedList<string,string> {
                            {"fillcolor", "darkkhaki"},
                            {"shape", "ellipse"}
                }
            ),
            new AttributeMap("GC", new SortedList<string,string> {
                            {"fillcolor", "saddlebrown"}
                }
            )
        };

        public static IList<AttributeMap> StandardEdgeSourceLabelToDOTAppearance = new List<AttributeMap>()
        {   
            new AttributeMap("RIBBON SYNAPSE", new SortedList<string,string> {
                            {"dir", "forward"},
                            {"arrowhead", "normal"},
                            {"arrowtail", "none"},
                            {"color", "chartreuse4"}
                }
            ),
            new AttributeMap("CONVENTIONAL", new SortedList<string,string> {
                            {"dir", "forward"},
                            {"arrowhead", "tee"},
                            {"arrowtail", "none"},
                            {"color", "red3"}
                }
            ),
            new AttributeMap("BC CONVENTIONAL", new SortedList<string,string> {
                            {"dir", "forward"},
                            {"arrowhead", "normal"},
                            {"arrowtail", "none"},
                            {"color", "chartreuse4"}
                }
            ),
            new AttributeMap("GAP JUNCTION", new SortedList<string,string> {
                            {"dir", "both"},
                            {"arrowhead", "open"},
                            {"arrowtail", "open"},
                            {"color", "purple"}
                }
            ),
            new AttributeMap("FRONTIER", new SortedList<string,string> {
                            {"dir", "forward"},
                            {"arrowhead", "none"},
                            {"arrowtail", "none"},
                            {"color", "white"}
                }
            ),
            new AttributeMap("UNKNOWN", new SortedList<string,string> {
                            {"dir", "forward"},
                            {"arrowhead", "none"},
                            {"arrowtail", "none"},
                            {"color", "black"}
                }
            )
        };
    }
}
