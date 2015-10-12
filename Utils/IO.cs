using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Net; 
using System.IO; 

namespace Utils
{
    public static class IO
    {
        public static XAttribute GetAttributeCaseInsensitive(this XElement element, string AttribName)
        {
            return element.Attributes().SingleOrDefault(a => string.Compare(a.Name.ToString().ToLower(), AttribName.ToLower()) == 0);
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
    }
}
