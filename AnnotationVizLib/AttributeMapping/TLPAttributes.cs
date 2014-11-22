using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
 
namespace AnnotationVizLib
{
    public static class TLPAttributes
    {
        public enum NodeShapes
        {
            Billboard = 7,
            ChristmasTree = 28,
            Circle = 14,
            Cone = 3,
            Cross = 8,
            Cube = 0,
            CubeOutlined = 1,
            CubeOutlinedTransparent = 9,
            Cylinder = 6,
            Diamond = 5,
            GlowSphere = 16,
            HalfCylinder = 10,
            Hexagon = 13,
            Pentagon = 12,
            Ring = 15,
            RoundedBox = 18,
            Sphere = 2,
            Square = 4,
            Triangle = 11,
            Window = 17,
            Star = 19
        }

        public enum EdgeShapes
        {
            Polyline=0,
            BezierCurve=4,
            CatmullRomCurve=8,
            CubicBSplineCurve=16
        }

        public enum EdgeExtremityShapes
        {
            Arrow=50,
            Circle=14,
            Cone=3,
            Cross=8,
            Cube=0,
            CubeOutlinedTransparent=9,
            Cylinder=6,
            Diamond=5,
            GlowSphere=16,
            Hexagon=13,
            Pentagon=12,
            Ring=15,
            Sphere=2,
            Square=4,
            Star=19
          }

        public static SortedDictionary<string, string> ColorMap = new SortedDictionary<string, string>()
        {
            { "clear", "(255,255,255,0)"},
            { "black", "(0,0,0,127)"},
            { "white", "(255,255,255,127)"},
            { "red", "(255,0,0,127)"},
            { "red3", "(192,0,0,127)"},
            { "green", "(0,255,0,127)"},
            { "green3", "(0,192,0,127)"},
            { "green4", "(0,127,0,127)"},
            { "cadetblue", "(95,158,0,160)"},
            { "purple", "(255,0,255, 127)"},

            { "yellow3", "(95,158,160,127)"},
            { "palevioletred1", "(222,22,137,127)"},
            { "palevioletred4", "(241,116,188,127)"},
            { "orchid", "(95,158,160,127)"},
            { "hotpink", "(255,98,211,127)"},
            { "brown1", "(211,140,33,127)"},
            
            { "blue", "(0,0,255,127)"},
            { "grey", "(127,127,127,127)"},
            { "orange", "(255,128,0,127)"},
            { "darkkhaki", "(194,111,61,127)"},
            { "saddlebrown", "(107,61,33,127)"},
            { "chartreuse4", "(69,139,0,127)"}
        };
          
        public static SortedDictionary<string, string> StandardGraphDefaultAttributeValues = new SortedDictionary<string, string>()
        {
            {"viewColor", " \"(128,128,128,64)\" \"(128,128,128,64)\"" },
            {"viewLabel", " \"\" \"\"" },
            {"viewSize", " \"(10,10,10)\" \"(10,10,10)\"" },
            {"viewShape", " \"0\" \"0\"" }
        };

        public static SortedDictionary<string, string> TLPTypeForAttribute = new SortedDictionary<string, string>()
        {
            {"viewColor", "color"},
            {"viewBorderColor", "color"},
            {"viewSelection", "bool"},
            {"viewLabel", "string"},
            {"viewLayout", "layout"}, 
            {"viewSize", "size"},
            {"viewShape", "int"},
            {"viewSrcAnchorShape", "int"},
            {"viewTgtAnchorShape", "int"},
        };

        public static SortedDictionary<string, string> DefaultForAttribute = new SortedDictionary<string, string>()
        {
            {"viewColor", string.Format("\"{0}\" \"{0}\"", ColorMap["grey"], ColorMap["grey"])},
            {"viewSelection", " \"false\" \"false\""},
            {"viewLabel", " \"\" \"\""},
            //{"viewLayout", " \"()\" \"()\""}, 
            {"viewSize", " \"(1,1,1)\" \"(0.5,0.5,0.5)\""},
            {"viewBorderColor", string.Format("\"{0}\" \"{0}\"", ColorMap["clear"], ColorMap["clear"]) },
            {"viewShape", string.Format(" \"0\" \"{0}\"", IntForEdgeShape(EdgeShapes.BezierCurve))},
        };
          
        public static string IntForShape(NodeShapes type)
        {
            return ((int)type).ToString();
        }

        public static string IntForEdgeTerminalShape(EdgeExtremityShapes type)
        {
            return ((int)type).ToString();
        }

        public static string IntForEdgeShape(EdgeShapes type)
        {
            return ((int)type).ToString();
        }
                
        public static new SortedList<string, string> UnknownTLPNodeAttributes = new SortedList<string,string> {
            {"viewColor", ColorMap["grey"]}
        };

        public static new SortedList<string, string> UnknownTLPEdgeAttributes = new SortedList<string, string> {
            {"viewColor", ColorMap["grey"]}
        };

        /// <summary>
        /// A mapping of labels to node properties.  
        /// An exact match is checked first, if it does not exist the
        /// we search the keys in the order they appear in the list.  The first partial match
        /// has its attributes returned.
        /// </summary>
        public static IList<AttributeMap> StandardLabelToNodeTLPAppearance = new List<AttributeMap>()
        {   
            new AttributeMap("AXON", new SortedList<string,string> {
                            {"viewColor", ColorMap["red3"]},
                            {"viewShape", IntForShape(NodeShapes.Hexagon)}
                }
            ),
            new AttributeMap("DENDRITE", new SortedList<string,string> {
                            {"viewColor", ColorMap["green3"]} 
                }
            ),
            new AttributeMap("CBAB", new SortedList<string,string> {
                            {"viewColor", ColorMap["green4"]} 
                }
            ),
            new AttributeMap("GBC", new SortedList<string,string> {
                            {"viewColor", ColorMap["cadetblue"]} 
                }
            ),
            new AttributeMap("CBB", new SortedList<string,string> {
                            {"viewColor", ColorMap["cadetblue"]} 
                }
            ),
            new AttributeMap("AII", new SortedList<string,string> {
                            {"viewColor", ColorMap["yellow3"]},
                            {"viewShape", IntForShape(NodeShapes.Hexagon)}
                }
            ),
            new AttributeMap("S1", new SortedList<string,string> {
                            {"viewColor", ColorMap["palevioletred1"]},
                            {"viewShape", IntForShape(NodeShapes.Diamond)}
                }
            ),
            new AttributeMap("S2", new SortedList<string,string> {
                            {"viewColor", ColorMap["palevioletred4"]},
                            {"viewShape", IntForShape(NodeShapes.Diamond)}
                }
            ),
            new AttributeMap("AI", new SortedList<string,string> {
                            {"viewColor", ColorMap["orchid"]},
                            {"viewShape", IntForShape(NodeShapes.Diamond)}
                }
            ),
            new AttributeMap("STARBURST", new SortedList<string,string> {
                           {"viewColor", ColorMap["hotpink"]},
                           {"viewShape", IntForShape(NodeShapes.Diamond)}
                }
            ),
            new AttributeMap("IAC", new SortedList<string,string> {
                            {"viewColor", ColorMap["brown1"]},
                            {"viewShape", IntForShape(NodeShapes.Cross)}
                }
            ),
            new AttributeMap("ROD BC", new SortedList<string,string> {
                            {"viewColor", ColorMap["purple"]} 
                }
            ),
            new AttributeMap("OFF", new SortedList<string,string> {
                            {"viewColor", ColorMap["blue"]} 
                }
            ),
            new AttributeMap("CBA", new SortedList<string,string> {
                            {"viewColor", ColorMap["blue"]} 
                }
            ),
            new AttributeMap("BC", new SortedList<string,string> {
                            {"viewColor", ColorMap["green"]} 
                }
            ),
            new AttributeMap("CB", new SortedList<string,string> {
                            {"viewColor", ColorMap["green"]} 
                }
            ),
            new AttributeMap("AXC", new SortedList<string,string> {
                            {"viewColor", ColorMap["orange"]},
                            {"viewShape", IntForShape(NodeShapes.Star)}
                }
            ),
            new AttributeMap("YAC", new SortedList<string,string> {
                            {"viewColor", ColorMap["red3"]},
                            {"viewShape", IntForShape(NodeShapes.Triangle)}
                }
            ),
            new AttributeMap("GABA", new SortedList<string,string> {
                            {"viewColor", ColorMap["red3"]},
                            {"viewShape",  IntForShape(NodeShapes.Triangle)}
                }
            ),
            new AttributeMap("GLY", new SortedList<string,string> {
                            {"viewColor", ColorMap["green3"]},
                            {"viewShape", IntForShape(NodeShapes.Triangle)}
                }
            ),
            new AttributeMap("GAC", new SortedList<string,string> {
                            {"viewColor", ColorMap["green3"]},
                            {"viewShape",  IntForShape(NodeShapes.Triangle)}
                }
            ),
            new AttributeMap("AC", new SortedList<string,string> {
                            {"viewColor", ColorMap["darkkhaki"]},
                            {"viewShape", IntForShape(NodeShapes.RoundedBox)}
                }
            ),
            new AttributeMap("GC", new SortedList<string,string> {
                            {"viewColor", ColorMap["saddlebrown"]}
                }
            )
        };

        public static IList<AttributeMap> StandardEdgeSourceLabelToTLPAppearance = new List<AttributeMap>()
        {   
            new AttributeMap("RIBBON SYNAPSE", new SortedList<string,string> {
                            {"viewTgtAnchorShape", IntForEdgeTerminalShape(EdgeExtremityShapes.Arrow)},
                            {"viewColor", ColorMap["chartreuse4"]}
                }
            ),
            new AttributeMap("CONVENTIONAL", new SortedList<string,string> {
                            {"viewTgtAnchorShape", IntForEdgeTerminalShape(EdgeExtremityShapes.Circle)},
                            {"viewColor", ColorMap["red3"]}
                }
            ),
            new AttributeMap("BC CONVENTIONAL", new SortedList<string,string> {
                            {"viewTgtAnchorShape", IntForEdgeTerminalShape(EdgeExtremityShapes.Arrow)},
                            {"viewColor", ColorMap["chartreuse4"]}
                }
            ),
            new AttributeMap("GAP JUNCTION", new SortedList<string,string> {
                            {"viewTgtAnchorShape", IntForEdgeTerminalShape(EdgeExtremityShapes.Diamond)},
                            {"viewSrcAnchorShape", IntForEdgeTerminalShape(EdgeExtremityShapes.Diamond)},
                            {"viewColor", ColorMap["purple"]}
                }
            ),
            new AttributeMap("FRONTIER", new SortedList<string,string> {
                            {"viewTgtAnchorShape", IntForEdgeTerminalShape(EdgeExtremityShapes.Arrow)},
                            {"viewSrcAnchorShape", IntForEdgeTerminalShape(EdgeExtremityShapes.Arrow)},
                            {"viewColor", ColorMap["white"]}
                }
            ),
            new AttributeMap("UNKNOWN", new SortedList<string,string> {
                            {"viewTgtAnchorShape", IntForEdgeTerminalShape(EdgeExtremityShapes.Arrow)},
                            {"viewSrcAnchorShape", IntForEdgeTerminalShape(EdgeExtremityShapes.Arrow)},
                            {"viewColor", ColorMap["black"]}
                }
            )
        };
    }
} 
