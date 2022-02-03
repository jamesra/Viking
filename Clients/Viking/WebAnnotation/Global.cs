using connectomes.utah.edu.XSD.WebAnnotationUserSettings.xsd;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml.Linq;
using Viking.Common;
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

        public static uint NumCurveInterpolationPoints(bool Closed)
        {
            return Geometry.Global.NumCurveInterpolationPoints(Closed);
        }

        //TODO: Choose number of points based on distance between control points
        static public uint NumOpenCurveInterpolationPoints => Geometry.Global.NumOpenCurveInterpolationPoints;
        static public uint NumClosedCurveInterpolationPoints => Geometry.Global.NumClosedCurveInterpolationPoints;

        static public uint NumClosedCurveInterpolationPointsForDisplay = 4;

        static public int PenSimplifyThreshold = 15;

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

        private static System.Collections.ObjectModel.ObservableCollection<ulong> _UserFavoriteStructureTypes;

        public static System.Collections.ObjectModel.ObservableCollection<ulong> UserFavoriteStructureTypes
        {
            get
            { 
                if (_UserFavoriteStructureTypes == null)
                {
                    _UserFavoriteStructureTypes = new System.Collections.ObjectModel.ObservableCollection<ulong>();
                    foreach (string ID_str in Properties.Settings.Default.FavoriteStructureIDs)
                    {
                        try
                        {
                            ulong ID = System.Convert.ToUInt64(ID_str);
                            if(_UserFavoriteStructureTypes.Contains(ID) == false) //Do not add accidental duplicates
                                _UserFavoriteStructureTypes.Add(ID);
                        }
                        catch (ArgumentException)
                        {
                            Trace.WriteLine(string.Format("Unable to convert Favorite StructureID to long {0}", ID_str));
                        }
                    }

                    _UserFavoriteStructureTypes.CollectionChanged += OnFavoriteStructureTypesChanged;
                } 

                return _UserFavoriteStructureTypes; 
            }
        }

        private static void OnFavoriteStructureTypesChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            switch(e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach(object item in e.NewItems)
                    {
                        Properties.Settings.Default.FavoriteStructureIDs.Add(string.Format("{0}", item));
                    }
                    break;
                case NotifyCollectionChangedAction.Remove:
                    foreach (object item in e.OldItems)
                    {
                        Properties.Settings.Default.FavoriteStructureIDs.Remove(string.Format("{0}", item));
                    }
                    break;
                case NotifyCollectionChangedAction.Reset:
                    Properties.Settings.Default.FavoriteStructureIDs.Clear();
                    break;
                case NotifyCollectionChangedAction.Move:
                    break;
                case NotifyCollectionChangedAction.Replace:
                    foreach (object item in e.OldItems)
                    {
                        Properties.Settings.Default.FavoriteStructureIDs.Remove(string.Format("{0}", item));
                    }
                    foreach (object item in e.NewItems)
                    {
                        Properties.Settings.Default.FavoriteStructureIDs.Add(string.Format("{0}", item));
                    }
                    break;
            }

            /* The brute force approach */
            /*
            StringCollection newList = new System.Collections.Specialized.StringCollection();
            foreach(long ID in _UserFavoriteStructureTypes)
            {
                newList.Add(string.Format("{0}", ID));
            }

            Properties.Settings.Default.FavoriteStructureIDs = newList;
            */
            Properties.Settings.Default.Save(); //Persist the updated list
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

        /// <summary>
        /// Return true if the last annotation can be continued on the section number. 
        /// Continuation creates a new annotation on the section and links to the last.
        /// </summary>
        /// <param name="SectionNumber"></param>
        /// <returns></returns>
        internal static bool CanContinueLastTrace(int SectionNumber)
        {
            if (!Global.LastEditedAnnotationID.HasValue)
            {
                return false;
            }

            WebAnnotationModel.LocationObj lastLoc = WebAnnotationModel.Store.Locations.GetObjectByID(Global.LastEditedAnnotationID.Value, false);
            if (lastLoc == null)
            {
                return false;
            }

            if (lastLoc.Z == SectionNumber)
            {
                return false;
            }

            return true;
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
            AnnotationService.Types.Settings.PrepareSerializers();
            //Find the server hosting the volume.  Look for an XML file mapping the volume to an endpoint.
            //return true; 


            Viking.ViewModels.VolumeViewModel volume = Viking.UI.State.volume;

            //Section Thickness is hard-coded, should be pulled from server.
            Scale = new Geometry.GridVector3(volume.DefaultXYScale.Value, volume.DefaultXYScale.Value, 90.0);

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
            HttpWebRequest request = WebRequest.CreateHttp(AboutURI);
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
            /*
            catch (Exception )
            {
                //We found it, but could not parse it
  //              HandleIncorrectXSDMessage();
   //             LoadBookmarksFromBackup();
            }*/
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

            HttpWebRequest headerRequest = HttpWebRequest.CreateHttp(uri);
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
                    request = HttpWebRequest.CreateHttp(uri);
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
