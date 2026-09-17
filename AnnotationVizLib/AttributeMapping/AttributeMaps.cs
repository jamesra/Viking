using System.Collections.Generic;
using System.Diagnostics;

namespace AnnotationVizLib
{
    /// <summary>
    /// We build pre-defined dictionaries that map an attribute, such as a node label, to a set of other properties we wish to add to a view of a graph node, such as color, size,and shape.
    /// </summary>
    public readonly struct AttributeMap(string key, SortedList<string, string> attributes)
    {
        public readonly string Key = key;
        public readonly SortedList<string, string> Attributes = attributes;

        public override string ToString() => Key.ToString() + " - " + Attributes.ToString();
    }

    /// <summary>
    /// Assigns attributes to GraphViews based on properties of the input
    /// </summary>
    static class AttributeMapper
    {
        public static void CopyAttributes(IDictionary<string, string> source, IDictionary<string, string> target)
        {
            foreach (string key in source.Keys)
            {
                target[key] = source[key];
            }
        }

        public static IDictionary<string, string> AttribsForLabel(string label, IList<AttributeMap> LabelToAttribMap)
        {

            SortedDictionary<string, string> entity_attributes = [];

            if (label is null)
                return entity_attributes;

            label = label.ToUpper();

            //Check for an exact mapping first
            foreach (AttributeMap map in LabelToAttribMap)
            {
                if (map.Key == label)
                {
                    CopyAttributes(map.Attributes, entity_attributes);
                    return entity_attributes;
                }
            }


            //Check for a partial match in the order of the LabelToAttribMap list
            //The need to check in a specific non alphabetic order is why we do
            //not use a hashing or sorted data structure
            foreach (AttributeMap map in LabelToAttribMap)
            {
                if (label.Contains(map.Key))
                {
                    CopyAttributes(map.Attributes, entity_attributes);
                    return entity_attributes;
                }
            }

            Trace.WriteLine(string.Format("No mapping found for label {0}", label));

            return entity_attributes;
        }
    }
}
