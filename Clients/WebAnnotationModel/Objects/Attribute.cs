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
            // Every structure and location in a region refresh carries this string, so the common shape is scanned
            // directly; XDocument costs about 10 KB per string. Anything the scanner does not recognise is parsed as XML.
            List<ObjAttribute> fast = TryScanAttributeXml(XMLString);
            if (fast != null)
                return fast;

            System.Xml.Linq.XDocument doc = System.Xml.Linq.XDocument.Load(new StringReader(XMLString));

            XElement structureElem = doc.Element("Structure");
            if (structureElem == null)
                return new List<ObjAttribute>();

            return ElementToAttribs(structureElem);

        }

        /// <summary>
        /// Scans the shape <see cref="ToXml"/> writes: an optional XML declaration, then
        /// <c>&lt;Structure&gt;</c> holding only <c>&lt;Attrib Name="…" Value="…"/&gt;</c> elements (either quote style,
        /// self-closing or with an empty end tag, the five predefined and numeric character references). Returns the
        /// same list <see cref="ElementToAttribs"/> would, or null for anything else (comments, other elements or
        /// attributes, CDATA, unknown entities, a different root), which the caller hands to the XML parser.
        /// </summary>
        internal static List<ObjAttribute> TryScanAttributeXml(string xml)
        {
            int i = 0;
            SkipWhitespace(xml, ref i);
            if (Matches(xml, i, "<?xml"))
            {
                int end = xml.IndexOf("?>", i, StringComparison.Ordinal);
                if (end < 0)
                    return null;
                i = end + 2;
                SkipWhitespace(xml, ref i);
            }

            if (!Matches(xml, i, "<Structure"))
                return null;
            i += "<Structure".Length;
            SkipWhitespace(xml, ref i);

            List<ObjAttribute> list = new List<ObjAttribute>();
            if (Matches(xml, i, "/>"))
            {
                i += 2;
                return Rest(xml, i) ? list : null;
            }

            if (!Matches(xml, i, ">"))
                return null;
            i++;

            while (true)
            {
                SkipWhitespace(xml, ref i);
                if (Matches(xml, i, "</Structure"))
                {
                    i += "</Structure".Length;
                    SkipWhitespace(xml, ref i);
                    if (!Matches(xml, i, ">"))
                        return null;
                    i++;
                    break;
                }

                if (!Matches(xml, i, "<Attrib"))
                    return null;
                i += "<Attrib".Length;
                if (i >= xml.Length || !(IsXmlWhitespace(xml[i]) || xml[i] == '/' || xml[i] == '>'))
                    return null;

                string name = null;
                string value = null;
                while (true)
                {
                    SkipWhitespace(xml, ref i);
                    if (Matches(xml, i, "/>"))
                    {
                        i += 2;
                        break;
                    }

                    if (Matches(xml, i, ">"))
                    {
                        i++;
                        SkipWhitespace(xml, ref i);
                        if (!Matches(xml, i, "</Attrib"))
                            return null;
                        i += "</Attrib".Length;
                        SkipWhitespace(xml, ref i);
                        if (!Matches(xml, i, ">"))
                            return null;
                        i++;
                        break;
                    }

                    string attribute = ReadName(xml, ref i);
                    if (attribute == null)
                        return null;
                    SkipWhitespace(xml, ref i);
                    if (!Matches(xml, i, "="))
                        return null;
                    i++;
                    SkipWhitespace(xml, ref i);
                    string text = ReadQuoted(xml, ref i);
                    if (text == null)
                        return null;

                    if (attribute == "Name" && name == null)
                        name = text;
                    else if (attribute == "Value" && value == null)
                        value = text;
                    else
                        return null;
                }

                //An entry without a name is not a tag, so it is skipped rather than kept as an empty-named attribute.
                if (!string.IsNullOrEmpty(name))
                    list.Add(new ObjAttribute(name, value ?? ""));
            }

            if (!Rest(xml, i))
                return null;

            list.Sort();
            return list;
        }

        static bool Rest(string xml, int i)
        {
            SkipWhitespace(xml, ref i);
            return i == xml.Length;
        }

        static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && IsXmlWhitespace(s[i]))
                i++;
        }

        static bool IsXmlWhitespace(char c) => c == ' ' || c == '\t' || c == '\r' || c == '\n';

        static bool Matches(string s, int i, string token) =>
            string.CompareOrdinal(s, i, token, 0, token.Length) == 0;

        static string ReadName(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_'))
                i++;
            return i > start ? s.Substring(start, i - start) : null;
        }

        /// <summary>A quoted attribute value with its character references decoded; null when malformed.</summary>
        static string ReadQuoted(string s, ref int i)
        {
            if (i >= s.Length || (s[i] != '"' && s[i] != '\''))
                return null;
            char quote = s[i++];
            int end = s.IndexOf(quote, i);
            if (end < 0)
                return null;

            string raw = s.Substring(i, end - i);
            i = end + 1;
            // XML turns literal tabs and line breaks in an attribute value into spaces; leave that to the XML parser.
            if (raw.IndexOf('<') >= 0 || raw.IndexOfAny(new[] { '\t', '\r', '\n' }) >= 0)
                return null;
            if (raw.IndexOf('&') < 0)
                return raw;

            StringBuilder decoded = new StringBuilder(raw.Length);
            for (int k = 0; k < raw.Length; k++)
            {
                if (raw[k] != '&')
                {
                    decoded.Append(raw[k]);
                    continue;
                }

                int semicolon = raw.IndexOf(';', k);
                if (semicolon < 0)
                    return null;
                string entity = raw.Substring(k + 1, semicolon - k - 1);
                switch (entity)
                {
                    case "amp": decoded.Append('&'); break;
                    case "lt": decoded.Append('<'); break;
                    case "gt": decoded.Append('>'); break;
                    case "quot": decoded.Append('"'); break;
                    case "apos": decoded.Append('\''); break;
                    default:
                        if (entity.Length > 1 && entity[0] == '#')
                        {
                            bool hex = entity[1] == 'x';
                            if (!int.TryParse(hex ? entity.Substring(2) : entity.Substring(1),
                                    hex ? System.Globalization.NumberStyles.AllowHexSpecifier : System.Globalization.NumberStyles.None,
                                    System.Globalization.CultureInfo.InvariantCulture, out int code) ||
                                code > 0x10FFFF || (code >= 0xD800 && code <= 0xDFFF) ||
                                (code < 0x20 && code != 0x9 && code != 0xA && code != 0xD))
                                return null;
                            decoded.Append(char.ConvertFromUtf32(code));
                            break;
                        }

                        return null;
                }

                k = semicolon;
            }

            return decoded.ToString();
        }

        private static List<ObjAttribute> ElementToAttribs(XElement structureElem)
        {
            List<ObjAttribute> listAttrib = new List<ObjAttribute>();
            foreach (XElement attribElem in structureElem.Elements("Attrib"))
            {
                //An entry without a name is not a tag, so it is skipped rather than kept as an empty-named attribute.
                var name = attribElem.Attribute("Name")?.Value;
                if (string.IsNullOrEmpty(name))
                    continue;

                var value = attribElem.Attribute("Value")?.Value ?? "";

                listAttrib.Add(new ObjAttribute(name, value));
            }

            listAttrib.Sort();

            return listAttrib;
        }

        internal static List<ObjAttribute> TagStringsToList(IEnumerable<string> tags)
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
