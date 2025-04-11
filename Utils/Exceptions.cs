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
            this.XMLDataMessage = attribute is null ?
                $"{element} element is missing under {parent}" :
                $"{element} element's {attribute} attribute is missing under {parent}";

            if(!(parent is null))
                this.XMLDataMessage += $"under parent: {parent}";
        }

        public XMLMissingDataException(XElement parent, string element, string attribute = null) : base()
        {
            List<XElement> roottoelement = parent?.GetRootToElementList();
            
            roottoelement?.PrintVikingXMLElementList();

            this.XMLDataMessage = attribute is null ?
                $"{element} element is missing under {parent.ToString()}" :
                $"{element} element's {attribute} attribute is missing under {parent}";

            if (!(parent is null))
                this.XMLDataMessage += $"under parent: {parent.ToString()}";
        }

        public XMLMissingDataException(string message) : base()
        {
            this.XMLDataMessage = message;
        }
    }
}
