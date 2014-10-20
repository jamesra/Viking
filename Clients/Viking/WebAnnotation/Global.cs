using System;
using System.ServiceModel; 
using System.IO; 
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Xml;  
using System.Xml.Linq; 
using System.Text;
using Viking.Common;
using Viking;
using System.Diagnostics;
using Viking.VolumeModel;
using connectomes.utah.edu.XSD.WebAnnotationUserSettings.xsd;
//using WebAnnotation.AuthenticationService;

namespace WebAnnotation
{
    public class Global : IInitExtensions
    {
        /*
        static private EndpointAddress _EndpointAddress = null;
        static public EndpointAddress EndpointAddress
        {
            get
            {
                return _EndpointAddress;
            }
        }
         */
        /*
        static private EndpointAddress _AuthenticationAddress = null;
        static public EndpointAddress AuthenticationAddress
        {
            get
            {
                return _AuthenticationAddress;
            }
        }
         */

        internal static double DefaultLocationJumpDownsample = 4; //Jumping to a location causes it's diameter to occupy 1/8 the width of the screen
        internal static int NumSectionsInMemory = 15;

        /// <summary>
        /// This is hardcoded for now, but should be read from the VikingXML file
        /// </summary>
        internal static Geometry.GridVector3 Scale;

        static string WebAnnotationPath = Viking.UI.State.VolumeCachePath + System.IO.Path.DirectorySeparatorChar + "WebAnnotation";

        /// <summary>
        /// Bookmark filename only
        /// </summary>
        static string UserSettingsFileName = "UserSettings.xml";

        /// <summary>
        /// The full name of the settings file including filename and path
        /// </summary>
        static string UserSettingsFilePath = WebAnnotationPath + System.IO.Path.DirectorySeparatorChar + UserSettingsFileName;

        static XElement UserSettingsElement = null;

        /// <summary>6
        /// The home of the user settings XSD file
        /// </summary>
//        static internal readonly string XSDUri = "http://connectomes.utah.edu/XSD/BookmarkSchema.xsd";

        private static XRoot UserSettingsDoc;

        public static string EndpointName
        {
            get;
            internal set; 
        }

        static internal UserSettings UserSettings
        {
         get{
             if (UserSettingsDoc == null)
             {
                 LoadUserPreferences();
             }
             
             return UserSettingsDoc.UserSettings;
         }   
        
        }

        #region IInitExtensions Members

        /*
         //This function was intended to determine what access level the user had to the annotations
        bool ValidateUser()
        {
            
            AuthenticationServiceClient proxy = new AuthenticationServiceClient("BasicHttpBinding_AuthenticationService",
                                                                                Global.AuthenticationAddress);

            
            proxy.Open();

            if (proxy.IsLoggedIn())
                return true;

            proxy.Login(Viking.UI.State.UserCredentials.UserName, Viking.UI.State.UserCredentials.Password, "", true);
            
           
            return false;
        }
         */

        bool IInitExtensions.Initialize()
        {
#if DEBUG
//           return false;
#endif
            //Find the server hosting the volume.  Look for an XML file mapping the volume to an endpoint.
            //return true; 

            Scale = new Geometry.GridVector3(2.18, 2.18, 90.0); 

            Viking.ViewModels.VolumeViewModel volume = Viking.UI.State.volume;

            if (volume == null)
                return false;

            WebAnnotationModel.State.UserCredentials = Viking.UI.State.UserCredentials; 

            //Check the VikingXML for the endpoint first.  
            if (GetEndpointFromXML(volume.VolumeXML))
            {
                LoadUserPreferences(); 
                return true;
            }

            LoadUserPreferences(); 

            //LEGACY, check the about.xml file for the endpoint.  This needs to be removed when we have rebuilt the VikingXML files using new
            //CreateXML.py script from 11/2/10

            //See if we can load the WebAnnotationMapping file
            Uri MappingURI = new Uri(volume.Host + "/About.xml");
            HttpWebRequest request = WebRequest.Create(MappingURI) as HttpWebRequest;
            if (MappingURI.Scheme.ToLower() == "https")
                request.Credentials = Viking.UI.State.UserCredentials;


            XDocument XMLMapping = null; 
            try
            {
                using (WebResponse response = request.GetResponse())
                {
                    using (Stream responseStream = response.GetResponseStream())
                    {
                        using (StreamReader XMLStream = new StreamReader(responseStream))
                        {
                            XMLMapping = XDocument.Parse(XMLStream.ReadToEnd());
                        }
                    }
                }
            }
            catch (WebException)
            {
                Trace.WriteLine("Could not locate WebAnnotationMapping.XML, disabling WebAnnotations.", "WebAnnotation");
                return false;
            } 

            return GetEndpointFromXML(XMLMapping);
        }

        static bool GetEndpointFromXML(XDocument XMLMapping)
        {
            if(XMLMapping == null)
            {
                return false;
            }

            //Examine the mappings and determine if we can map the volume
            IEnumerable<XElement> VolumeElements = XMLMapping.Elements().Where(elem => elem.Name.LocalName == "Volume");

            foreach (XElement elem in VolumeElements)
            {
                //Fetch the name if we know it
                switch (elem.Name.LocalName)
                {
                    case "Volume":
                        IEnumerable<XElement> SettingsElements = elem.Elements().Where(e => e.Name.LocalName == "DefaultWebAnnotationUserSettings");
                        if (SettingsElements.Count() > 0)
                        {
                            UserSettingsElement = SettingsElements.First();
                        }

                        IEnumerable<XElement> MappingElements = elem.Elements().Where(e => e.Name.LocalName == "VolumeToEndpoint");

                        if (MappingElements.Count() == 0)
                            break;

                        Global.PopulateEndpointStateFromVolumeToEndpointElement(MappingElements.First());
                        
                        break;
                    default:
                        break;
                }

                //If we have an endpoint address then give the OK to load
                if (WebAnnotationModel.State.EndpointAddress != null)
                    return true;
            }

            //We don't have an endpoint to read/write annotations.  Do not load.
            return false;
        }

        private static void PopulateEndpointStateFromVolumeToEndpointElement(XElement MappingElement)
        {
            XAttribute NameAttribute = MappingElement.Attribute("Name");
            if (NameAttribute != null)
                Global.EndpointName = NameAttribute.Value;

            XAttribute EndpointAttribute = MappingElement.Attribute("Endpoint");
            if (EndpointAttribute != null)
            {
                #if DEBUG
                    WebAnnotationModel.State.EndpointAddress = new EndpointAddress(EndpointAttribute.Value);                       
//                        WebAnnotationModel.State.EndpointAddress = new EndpointAddress("https://connectomes.utah.edu/Services/TestBinary/Annotate.svc");
                #else
                    WebAnnotationModel.State.EndpointAddress = new EndpointAddress(EndpointAttribute.Value);                       
                #endif
            }
                
            /*
            XAttribute AuthenticationAttribute = MappingElement.Attribute("Authentication");
            if (AuthenticationAttribute != null)
            {
                Global._AuthenticationAddress = new EndpointAddress(AuthenticationAttribute.Value);
                ValidateUser(); 
            }
            */

            return;
        }

        private static void LoadUserPreferences()
        {
            try
            {
                if (false == System.IO.Directory.Exists(WebAnnotationPath))
                {
                    System.IO.Directory.CreateDirectory(WebAnnotationPath);
                    LoadServerUserSettings();
                }

                if (false == System.IO.File.Exists(UserSettingsFilePath))
                {
                    LoadServerUserSettings(); 
                }

                UserSettingsDoc = XRoot.Load(UserSettingsFilePath);
            }
            catch (Xml.Schema.Linq.LinqToXsdException )
            {
                //We found it, but could not parse it
//                HandleIncorrectXSDMessage();
//                LoadBookmarksFromBackup();
            }
            catch (System.Xml.XmlException )
            {
                //We found it, but could not parse it
 //               HandleIncorrectXSDMessage();
 //               LoadBookmarksFromBackup();
            }
            catch (Exception )
            {
                //We found it, but could not parse it
  //              HandleIncorrectXSDMessage();
   //             LoadBookmarksFromBackup();
            }
        }

        private static bool LoadServerUserSettings()
        {
            //Try to download the default user settings file
            if (UserSettingsElement != null)
            {
                XAttribute UriAttrib = UserSettingsElement.Attribute("Uri");
                if (UriAttrib != null)
                {
                    Uri uri = new Uri(UriAttrib.Value);
                    System.Net.WebRequest request = null;
                    WebResponse response = null;
                    Stream stream = null;
                    FileStream file = null;

                    try
                    {
                        request = HttpWebRequest.Create(uri);
                        response = request.GetResponse();
                        stream = response.GetResponseStream();
                        byte[] data = new Byte[response.ContentLength];
                        DateTime loopStart = DateTime.UtcNow; 
                        TimeSpan elapsed;
                        long BytesRead = 0;

                        do
                        {
                            BytesRead += stream.Read(data, (int)BytesRead, (int)data.Length - (int)BytesRead);
                            elapsed = new TimeSpan(DateTime.UtcNow.Ticks - loopStart.Ticks);
                        }
                        while (BytesRead < response.ContentLength && elapsed.TotalSeconds < 60);

                        file = File.Open(UserSettingsFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                        file.Write(data, 0, data.Length);
                    }
                    catch (Exception)
                    {
                        Trace.WriteLine("Could not load server user settings: " + uri.ToString());
                        return false; 
                    }
                    finally
                    {
                        if (response != null)
                            response.Close();

                        if (file != null)
                            file.Close();
                    }

                    return true; 

                }
            }

            return false; 
        }

        private static void CreateNewUserSettingsFile()
        {
            Global.UserSettingsDoc = new XRoot(new UserSettings());
            SaveUserSettings(); 
        }

        public static void SaveUserSettings()
        {
            Global.UserSettingsDoc.Save(UserSettingsFilePath);
        }

        #endregion
    }
}
