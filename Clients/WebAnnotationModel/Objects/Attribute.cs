using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace WebAnnotationModel.Objects
{
    public static class ObjAttributeParser
    { 
        public static string ToXml(this IEnumerable<ObjAttribute> attributes)
        {
            if (attributes == null)
                return null;

            if (!attributes.Any())
                return null;

            StringBuilder sbuilder = new StringBuilder();
            using (System.Xml.XmlWriter xwriter = XmlWriter.Create(sbuilder))
            {
                xwriter.WriteStartElement("Structure");

                foreach (ObjAttribute attrib in attributes)
                {
                    xwriter.WriteStartElement("Attrib");

                    xwriter.WriteAttributeString("Name", attrib.Name);
                    if (attrib.Value != null)
                    {
                        if (attrib.Value.Length > 0)
                        {
                            xwriter.WriteAttributeString("Value", attrib.Value);
                        }
                    }

                    xwriter.WriteEndElement();
                }

                xwriter.WriteEndElement();
            }

            return sbuilder.ToString();
        }

        /// <summary>
        /// Selects whether to use legacy parser or xml to understand attribute string
        /// </summary>
        /// <param name="serverXml"></param>
        /// <returns></returns>
        public static List<ObjAttribute> ParseAttributes(this string attributes)
        {
            if (attributes == null)
                return new List<ObjAttribute>();

            if (attributes.StartsWith("<"))
            {
                return FromXml(attributes);
            }
            else
            {
                return TagStringsToList(attributes.Split(';'));
            }

        }

        private static List<ObjAttribute> FromXml(string XMLString)
        {
            System.Xml.Linq.XDocument doc = System.Xml.Linq.XDocument.Load(new StringReader(XMLString));

            XElement structureElem = doc.Element("Structure");
            if (structureElem == null)
                return new List<ObjAttribute>();

            return ElementToAttribs(structureElem);

        }

        private static List<ObjAttribute> ElementToAttribs(XElement structureElem)
        {
            List<ObjAttribute> listAttrib = new List<ObjAttribute>();
            foreach (XElement attribElem in structureElem.Elements("Attrib"))
            {
                var name = attribElem.Attribute("Name")?.Value ?? "";
                var value = attribElem.Attribute("Value")?.Value ?? "";

                listAttrib.Add(new ObjAttribute(name, value));
            }

            listAttrib.Sort();

            return listAttrib;
        }

        private static List<ObjAttribute> TagStringsToList(IEnumerable<string> tags)
        {
            if (tags == null)
                return new List<ObjAttribute>();

            List<ObjAttribute> listTags = new List<ObjAttribute>();

            foreach (string tagString in tags)
            {
                string trimmedTag = tagString.Trim();
                string key = trimmedTag;

                //Do not include empty keys
                if (key.Length == 0)
                    continue;

                string value = "";
                int iEquals = trimmedTag.IndexOf("=");
                if (iEquals >= 0)
                {
                    key = trimmedTag.Substring(0, iEquals).Trim();
                    value = trimmedTag.Substring(iEquals + 1).Trim();
                }

                listTags.Add(new ObjAttribute(key, value));
            }

            listTags.Sort();

            return listTags;
        }
    }

    public class ObjAttribute : Object, IComparable<ObjAttribute>, IEquatable<ObjAttribute>, IComparable<String>, IEquatable<String>
    {
        public string Name { get; }
        public string Value { get; set; }
         

        public ObjAttribute(string Name, string Value)
        {
            this.Name = Name;
            this.Value = Value;
        }

        public override string ToString()
        {  
            if (string.IsNullOrWhiteSpace(Value))
                return Name.Trim();
            
            return $"{Name.Trim()}: {Value.Trim()}";
        }


        public int CompareTo(ObjAttribute other)
        {
            int compval = String.Compare(this.Name, other.Name);
            if (compval == 0)
                return String.Compare(this.Value, other.Value);
            else
                return compval;
        }

        public int CompareTo(string other)
        {
            return String.Compare(this.Name, other);
        }

        public static bool operator ==(ObjAttribute A, ObjAttribute B)
        {
            bool ANull = object.ReferenceEquals(null, A);
            bool BNull = object.ReferenceEquals(null, B);
            if (ANull && BNull)
                return true;
            if (ANull || BNull)
                return false;

            return String.Compare(A.Name, B.Name) == 0;
        }

        public static bool operator !=(ObjAttribute A, ObjAttribute B)
        {
            bool ANull = object.ReferenceEquals(null, A);
            bool BNull = object.ReferenceEquals(null, B);
            if (ANull && BNull)
                return false;
            if (ANull || BNull)
                return true;

            return String.Compare(A.Name, B.Name) != 0;
        }

        public override bool Equals(object obj)
        {
            if (obj is string s)
                return Equals(s);

            if (obj is ObjAttribute o)
                return Equals(o);

            return base.Equals(obj);
        }

        public bool Equals(string other)
        {
            return this.Name == other;
        }

        public bool Equals(ObjAttribute other)
        {
            return this.Name == other.Name;
        }

        public override int GetHashCode()
        {
            return this.Name.GetHashCode();
        }
    }

}
