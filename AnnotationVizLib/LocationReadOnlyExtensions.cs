using Microsoft.SqlServer.Types;
using System.Data.SqlTypes;
using System.Text;
using System.Xml;
using Viking.AnnotationServiceTypes.Interfaces;

namespace AnnotationVizLib
{
    /// <summary>
    /// ILocationReadOnly (AnnotationInterfaces) exposes geometry as WKT strings and attributes as a
    /// dictionary so the interface has no dependency on Microsoft.SqlServer.Types.  These bridge
    /// extensions restore the SqlGeometry/XML-attribute-string conveniences the older WCF/OData based
    /// AnnotationVizLib code was written against, without reintroducing that dependency into the interface.
    /// </summary>
    public static class LocationReadOnlyExtensions
    {
        public static SqlGeometry Geometry(this ILocationReadOnly loc)
        {
            if (string.IsNullOrEmpty(loc.VolumeGeometryWKT))
                return null;

            return SqlGeometry.STGeomFromText(new SqlChars(loc.VolumeGeometryWKT), 0);
        }

        public static SqlGeometry MosaicGeometry(this ILocationReadOnly loc)
        {
            if (string.IsNullOrEmpty(loc.MosaicGeometryWKT))
                return null;

            return SqlGeometry.STGeomFromText(new SqlChars(loc.MosaicGeometryWKT), 0);
        }

        /// <summary>
        /// Renders Attributes back into the "&lt;Structure&gt;&lt;Attrib Name="" Value=""/&gt;...&lt;/Structure&gt;"
        /// XML format expected by AnnotationVizLib.ObjAttribute.AttributesToString/Parse.
        /// </summary>
        public static string TagsXml(this ILocationReadOnly loc)
        {
            if (loc.Attributes == null || loc.Attributes.Count == 0)
                return null;

            StringBuilder sb = new StringBuilder();
            using (XmlWriter writer = XmlWriter.Create(sb))
            {
                writer.WriteStartElement("Structure");
                foreach (var kvp in loc.Attributes)
                {
                    writer.WriteStartElement("Attrib");
                    writer.WriteAttributeString("Name", kvp.Key);
                    if (!string.IsNullOrEmpty(kvp.Value))
                        writer.WriteAttributeString("Value", kvp.Value);
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }

            return sb.ToString();
        }
    }
}
