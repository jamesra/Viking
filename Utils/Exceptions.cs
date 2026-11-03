using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace Utils
{
    public class XMLMissingDataException : Exception
    {
        private readonly string XMLDataMessage;
        public override string Message => XMLDataMessage;

        public XMLMissingDataException(string parent, string element, string attribute = null) : base()
        {
            this.XMLDataMessage = BuildMessage(parent, element, attribute);
        }

        public XMLMissingDataException(XElement parent, string element, string attribute = null) : base()
        {
            // Name the parent by its root-to-element path rather than dumping its whole XML.
            string path = parent is null ? null : "\n" + parent.GetRootToElementList().PrintVikingXMLElementList();
            this.XMLDataMessage = BuildMessage(path, element, attribute);
        }

        private static string BuildMessage(string parent, string element, string attribute)
        {
            string what = attribute is null ?
                $"{element} element is missing" :
                $"{element} element's {attribute} attribute is missing";

            return parent is null ? what : $"{what} under {parent}";
        }

        public XMLMissingDataException(string message) : base()
        {
            this.XMLDataMessage = message;
        }
    }
}
