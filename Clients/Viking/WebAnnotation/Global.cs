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
        internal static double DefaultLocationJumpDownsample = 4; //Jumping to a location causes it's diameter to occupy 1/8 the width of the screen

        /// <summary>
        /// Number of sections we should be attempting to load at the same time before cancelling a request
        /// </summary>
        internal static int NumSectionsLoading = 5;

        internal static Export Export = null;

#if DEBUG
        internal static int NumSectionsInMemory = 10;
#else
        internal static int NumSectionsInMemory = 10;
#endif
        public static readonly double AdjacentLocationRadiusScalar = 0.5; //Make radius of annotations on adjacent sections half of the normal value

        //TODO: Choose number of points based on distance between control points
        static public readonly uint NumOpenCurveInterpolationPoints = 3;
        static public readonly uint NumClosedCurveInterpolationPoints = 10;

        public static uint NumCurveInterpolationPoints(bool Closed)
        {
            return Closed ? NumClosedCurveInterpolationPoints : NumOpenCurveInterpolationPoints;
        }

        static public int PenSimplifyThreshold = 30;

        static public double DefaultClosedLineWidth = 24.0;

        static public double MinRadius = 0.5;

        static public WebAnnotation.UI.Forms.PenAnnotationViewForm PenAnnotationForm = null;

        /// <summary>
        /// Number of interpolations to place between curve control points, determines distance between control points
        /// </summary>
        //static public double CurveInterpolationPointSpacing = 100.0;

        //static public int NumCurveInterpolationPoints(double distance)
        //{
        //return (int)Math.Round(distance / CurveInterpolationPointSpacing);
        //}

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

        public static bool PenMode
        {
            get
            {
                return WebAnnotation.Properties.Settings.Default.PenMode;
            }
            set
            {
                WebAnnotation.Properties.Settings.Default.PenMode = value;
                WebAnnotation.Properties.Settings.Default.Save();
            }
        }

        static Uri UserSettingsUri
        {
            get
            {
                if (UserSettingsElement != null)
                {
                    XAttribute UriAttrib = UserSettingsElement.Attribute("Uri");
                    if (UriAttrib != null)
                    {
                        return new Uri(UriAttrib.Value);
                    }
                }

                return null;
            }
        }

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

        /// <summary>
        /// LastEditedAnnotationID can have no value if no location has been editted
        /// It can also have the ID of a deleted location.  Deleted locations return
        /// null objects when requested from the server.
        /// </summary>
        public static long? LastEditedAnnotationID;

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
            if (GetEndpointFromXML(volume.VolumeElement))
            {
                LoadUserPreferences();
                return true;
            }
            else
            {
                return false;
            }

            //LEGACY, check the about.xml file for the endpoint.  This needs to be removed when we have rebuilt the VikingXML files using new
            //CreateXML.py script from 11/2/10
            //LoadUserPreferences(); 
            //XDocument AboutXML = GetAboutXML(new Uri(volume.Host + "/About.xml"));
            //return GetEndpointFromXML(AboutXML);
        }

        private static XDocument GetAboutXML(Uri AboutURI)
        {
            //See if we can load the WebAnnotationMapping file
            HttpWebRequest request = WebRequest.Create(AboutURI) as HttpWebRequest;
            if (AboutURI.Scheme.ToLower() == "https")
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
                            return XDocument.Parse(XMLStream.ReadToEnd());
                        }
                    }
                }
            }
            catch (WebException)
            {
                Trace.WriteLine("Could not locate WebAnnotationMapping.XML, disabling WebAnnotations.", "WebAnnotation");
                return null; 
            } 
        }

        static bool GetEndpointFromXML(XElement elem)
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
            if (WebAnnotationModel.State.Endpoint != null)
                return true; 

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
                          WebAnnotationModel.State.Endpoint = new Uri(EndpointAttribute.Value); 
//                        WebAnnotationModel.State.EndpointAddress = new EndpointAddress("https://connectomes.utah.edu/Services/TestBinary/Annotate.svc");
                #else
                WebAnnotationModel.State.Endpoint = new Uri(EndpointAttribute.Value);
                #endif
            }

            XAttribute ExportURLAttribute = MappingElement.Attribute("ExportURL");
            if(ExportURLAttribute != null)
            {
                Global.Export = new WebAnnotation.Export(new Uri(ExportURLAttribute.Value));
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

                if (!CachedResourceIsValid(UserSettingsFilePath, UserSettingsUri))
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

        /// <summary>
        /// Validates the provide file against the last modified date of the web resource
        /// </summary>
        /// <param name="CacheFilename"></param>
        /// <param name="textureUri"></param>
        /// <returns></returns>
        private static bool CachedResourceIsValid(string CacheFilename, Uri uri)
        {
            if (uri == null)
                return true;

            if (!System.IO.File.Exists(CacheFilename))
                return false;

            HttpWebRequest headerRequest = HttpWebRequest.Create(uri) as HttpWebRequest;
            headerRequest.Method = "HEAD";
            headerRequest.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore); 
            using (HttpWebResponse headerResponse = headerRequest.GetResponse() as HttpWebResponse)
            {
                bool valid = headerResponse.LastModified.ToUniversalTime() <= System.IO.File.GetLastWriteTimeUtc(CacheFilename);
                return valid;
            }
        }

        private static bool LoadServerUserSettings()
        {
            //Try to download the default user settings file
            Uri uri = UserSettingsUri;
            if(uri != null)
            {
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

                    try
                    {
                        if(System.IO.File.Exists(UserSettingsFilePath))
                        {
                            System.IO.File.Delete(UserSettingsFilePath);
                        }
                    }
                    catch(System.IO.IOException)
                    {

                    }

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
