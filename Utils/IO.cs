using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml.Linq;

namespace Utils
{
    public static class LinqXMLExtensions
    {
        public static XAttribute GetAttributeCaseInsensitive(this XElement element, string attribName)
        {
            XAttribute attrib = null;
            try
            {
                attrib = element.Attributes().SingleOrDefault(a => string.Compare(a.Name.ToString().ToLower(), attribName.ToLower()) == 0);
                
            }
            catch (Exception e)
            {
                System.Diagnostics.Trace.WriteLine(
                    $"Could not get {attribName} attribute from <{element.Name}> Element");
                System.Diagnostics.Trace.WriteLine(e.Message);

                throw;
            }

            if (attrib is null)
            {
                throw new XMLMissingDataException(element.Parent?.Name.ToString(), element.Name.ToString(), attribName);
            }

            return attrib;
        }

        public static bool HasAttributeCaseInsensitive(this XElement element, string AttribName)
        {
            return element.Attributes().Any(a => string.Compare(a.Name.ToString().ToLower(), AttribName.ToLower()) == 0);
        } 

        /// <summary>
        /// Loads a URI into an XDocument, determines whether path refers to XML file or a local directory
        /// </summary>
        /// <param name="path"></param>
        public static XDocument Load(Uri path)
        {
            XDocument XDoc;
            if (path.Scheme == "http" || path.Scheme == "https")
                XDoc = LoadHTTP(path);
            else
                XDoc = XDocument.Load(path.LocalPath);

            return XDoc;
        }

        private static XDocument LoadHTTP(Uri path)
        {
            //Remove the .xml file from the path
            int iRemove = path.OriginalString.LastIndexOf('/');
            string VolumePath = path.OriginalString;
            if (iRemove > 0)
            {
                VolumePath = VolumePath.Remove(iRemove);
            }

            HttpWebRequest request = WebRequest.Create(path) as HttpWebRequest;

            request.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.Revalidate);

            WebResponse response = null;
            try
            {
                response = request.GetResponse();
            }
            catch (WebException e)
            {
                /*PORT: Don't have forms, throw a better exception*/
                throw new WebException("Error connecting to volume server: \n" + path + "\n" + e.Message, e);
            }

            Stream responseStream = response.GetResponseStream();

            StreamReader XMLStream = new StreamReader(responseStream);

            XDocument reader = XDocument.Parse(XMLStream.ReadToEnd());

            XMLStream.Close();
            responseStream.Close();
            response.Close();

            return reader;
        }

        /// <summary>
        /// Return a list, in order, starting with the root element down to the passed element
        /// </summary>
        /// <param name=""></param>
        /// <returns></returns>
        public static List<XElement> GetRootToElementList(this XElement input)
        {
            List<XElement> output = new List<XElement>();
            var parent = input.Parent;
            while(!(parent is null))
            {
                output.Insert(0, parent);
                parent = parent.Parent;
            }

            output.Add(input);
            return output;
        }

        public static string PrintVikingXMLElementList(this List<XElement> list)
        {
            int tablevel = 0;
            StringBuilder sb = new StringBuilder();
            foreach (XElement elem in list)
            {
                sb.Append('\t' * tablevel);
                sb.Append('<');
                sb.Append(elem.Name);
                if(elem.HasAttributeCaseInsensitive("name"))
                {
                    var attrib = elem.GetAttributeCaseInsensitive("name");
                    sb.Append(" name=");
                    sb.Append(attrib);
                }
                if (elem.HasAttributeCaseInsensitive("path"))
                {
                    var attrib = elem.GetAttributeCaseInsensitive("path");
                    sb.Append(" path=");
                    sb.Append(attrib);
                }
                sb.Append(">\n");
            }

            return sb.ToString();

        }
    }

}
