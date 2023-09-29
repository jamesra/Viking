using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace AnnotationVizLib
{
    public class ObjAttribute : Object, IComparable<ObjAttribute>
    {
        public readonly string Name;
        public string Value { get; set; }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        public ObjAttribute(string Name)
        {
            this.Name = Name;
            this.Value = "";
        }

        public ObjAttribute(string Name, string Value)
        {
            this.Name = Name;
            this.Value = Value;
        }

        public override string ToString()
        {
            string Name = this.Name.Trim();

            if (null == this.Value)
                return Name;

            string Value = this.Value.Trim();

            if (this.Value.Trim().Length == 0)
                return Name;

            return Name + '=' + Value;
        }

        public static string ToXml(IEnumerable<ObjAttribute> attributes)
        {
            if (attributes.Count() == 0)
            {
                return null;
            }

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
        /// The legacy version of this function returned semicolon delimited strings.  The new version returns XML.
        /// </summary>
        /// <param name="serverXml"></param>
        /// <returns></returns>
        public static List<ObjAttribute> Parse(string serverXml)
        {
            if (serverXml == null)
                return new List<ObjAttribute>();

            if (serverXml.StartsWith("<"))
            {
                return ObjAttribute.FromXml(serverXml);
            }
            else
            {
                return ObjAttribute.TagStringsToList(serverXml.Split(';'));
            }

        }

        private static List<ObjAttribute> FromXml(string XMLString)
        {
            System.Xml.Linq.XDocument doc = System.Xml.Linq.XDocument.Load(new StringReader(XMLString));

            XElement structureElem = doc.Element("Structure");
            if (structureElem == null)
                return new List<ObjAttribute>();

            return ObjAttribute.ElementToAttribs(structureElem);
        }

        /// <summary>
        /// Convert XML attributes to a semicolon delimited string
        /// </summary>
        /// <param name="xml"></param>
        /// <returns></returns>
        public static string AttributesToString(string xml)
        {
            List<ObjAttribute> listAttribs = ObjAttribute.Parse(xml).Where(a => !string.IsNullOrEmpty(a.Name)).ToList();

            StringBuilder sb = new StringBuilder();
            bool FirstRow = true;
            foreach (ObjAttribute a in listAttribs)
            {
                if (FirstRow)
                {
                    FirstRow = false;
                }
                else
                {
                    sb.Append(';');
                }

                sb.Append(a.ToString());
            }

            return sb.ToString();
        }

        private static List<ObjAttribute> ElementToAttribs(XElement structureElem)
        {
            List<ObjAttribute> listAttrib = new List<ObjAttribute>();
            foreach (XElement attribElem in structureElem.Elements("Attrib"))
            {
                ObjAttribute a = new ObjAttribute(attribElem.Attribute("Name").Value);
                if (attribElem.Attribute("Value") != null)
                {
                    a.Value = attribElem.Attribute("Value").Value;
                }

                listAttrib.Add(a);
            }

            listAttrib.Sort();

            return listAttrib;
        }

        public static List<ObjAttribute> TagStringsToList(IEnumerable<string> tags)
        {
            if (tags == null)
                return new List<ObjAttribute>();

            List<ObjAttribute> listTags = new List<ObjAttribute>();

            foreach (string tagString in tags)
            {
                ObjAttribute tag;
                string trimmedTag = tagString.Trim();
                string key = trimmedTag;

                //Do not include empty keys
                if (key.Length == 0)
                    continue;

                string value = "";
                int iEquals = trimmedTag.IndexOf("=");
                if (iEquals >= 0)
                {
                    key = trimmedTag.Substring(0, iEquals);
                    value = trimmedTag.Substring(iEquals + 1);

                    tag = new ObjAttribute(key.Trim(), value.Trim());
                }
                else
                {
                    tag = new ObjAttribute(key.Trim(), value.Trim());
                }

                listTags.Add(tag);
            }

            listTags.Sort();

            return listTags;
        }

        public int CompareTo(ObjAttribute other)
        {
            int compval = String.Compare(this.Name, other.Name);
            if (compval == 0)
                return String.Compare(this.Value, other.Value);
            else
                return compval;
        }

        public static bool operator ==(ObjAttribute A, ObjAttribute B)
        {
            bool ANull = A is null;
            bool BNull = B is null;
            if (ANull && BNull)
                return true;
            if (ANull || BNull)
                return false;

            return String.Compare(A.Name, B.Name) == 0;
        }

        public static bool operator !=(ObjAttribute A, ObjAttribute B)
        {
            bool ANull = A is null;
            bool BNull = B is null;
            if (ANull && BNull)
                return false;
            if (ANull || BNull)
                return true;

            return String.Compare(A.Name, B.Name) != 0;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is ObjAttribute Other))
                return false;

            return Other.Name == this.Name;
        }

    }

}
