using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace AnnotationUtils
{
    public class AttributeMap
    {
        public string Key;
        public SortedList<string, string> Attributes;

        public AttributeMap(string key, SortedList<string, string> attributes)
        {
            Key = key;
            Attributes = attributes; 
        }
    }

    static class AttributeMaps
    {
        public static SortedDictionary<string, string> StandardGraphAttributes = new SortedDictionary<string, string>()
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

        public static SortedDictionary<string, string> StandardNodeAttributes = new SortedDictionary<string,string>() {
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
        public static IList<AttributeMap> StandardLabelToNodeAppearance = new List<AttributeMap>()
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

        public static IList<AttributeMap> StandardEdgeLabelToAppearance = new List<AttributeMap>()
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
                            {"color", "goldenrod4"}
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

        public static IDictionary<string, string> AttribsForLabel(string label, IList<AttributeMap> LabelToAttribMap)
        {
            label = label.ToUpper(); 

            //Check for an exact mapping first
            foreach(AttributeMap map in LabelToAttribMap)
            {
                if (map.Key == label)
                    return map.Attributes;
            }

            
            //Check for a partial match in the order of the LabelToAttribMap list
            //The need to check in a specific non alphabetic order is why we do
            //not use a hashing or sorted data structure
            foreach(AttributeMap map in LabelToAttribMap)
            {
                if(label.Contains(map.Key))
                    return map.Attributes;
            }
            
            return null;
        }
    }
}
