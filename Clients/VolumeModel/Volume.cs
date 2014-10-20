using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel; 
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
//using System.IO.Compression;
using Ionic.Zip;
using Utils;
using Geometry;
using Geometry.Transforms; 

namespace Viking.VolumeModel 
{
    public class TileServerInfo
    {
        public string Host { get; private set; }
        public string CoordSpaceName { get; private set; }
        public int TileXDim { get; private set; }
        public int TileYDim { get; private set; }
        public string Name { get; private set; }
        public string Path { get; private set; }
        public string FilePrefix { get; private set; }
        public string FilePostfix { get; private set; }

        public static TileServerInfo CreateFromElement(XElement node)
        {
            TileServerInfo info = new TileServerInfo();

            info.Name = IO.GetAttributeCaseInsensitive(node, "name").Value;
            info.Path = IO.GetAttributeCaseInsensitive(node, "path").Value;
            info.TileXDim = System.Convert.ToInt32(IO.GetAttributeCaseInsensitive(node, "TileXDim").Value);
            info.TileYDim = System.Convert.ToInt32(IO.GetAttributeCaseInsensitive(node, "TileYDim").Value);
            info.FilePrefix = IO.GetAttributeCaseInsensitive(node, "FilePrefix").Value;
            info.FilePostfix = IO.GetAttributeCaseInsensitive(node, "FilePostfix").Value;
            info.Host = IO.GetAttributeCaseInsensitive(node, "host").Value;
            info.CoordSpaceName = IO.GetAttributeCaseInsensitive(node, "coordspacename").Value;

            return info;
        }

        protected TileServerInfo()
        {

        }
    }

    /// <summary>
    /// Collection of volumes, sections and tiles. There is only one dataset loaded at a time
    /// </summary>
    public class Volume
    {
        

        /// <summary>z
        /// Friendly name for the volume
        /// </summary>
        public string Name = "";

        /// <summary>
        /// Name of the volume transform to use by default
        /// </summary>
        public string DefaultVolumeTransform = "None";

        /// <summary>
        /// Name of the default stos group
        /// </summary>
        public string DefaultStosGroup = null;

        /// <summary>
        /// Name of the default image pyramid
        /// </summary>
        public string DefaultImagePyramid= null;

        /// <summary>
        /// Name of the default tile-to-mosaic transform when using pyramids
        /// </summary>
        public string DefaultMosaicTransform = null;

        /// <summary>
        /// The starting section number read from meta-data
        /// </summary>
        public int? DefaultSectionNumber = new int?();

        private string _UniqueID = "";
        /// <summary>
        /// Unique ID for this volume on the server
        /// </summary>
        public string UniqueID
        {
            get { return _UniqueID; }
        }

        /// <summary>
        /// Set to true if the volume is located on the local drive
        /// False if over a network
        /// </summary>
        private bool _IsLocal;
        public bool IsLocal
        {
            get { return _IsLocal; }

        }

        /// <summary>
        /// Credentials to use during web requests
        /// </summary>
        public System.Net.NetworkCredential UserCredentials = new System.Net.NetworkCredential("anonymous", "connectome");
        
        private XDocument _VolumeXML;

        /// <summary>
        /// The XML document used to initialize the volume.  Contains all configuration settings from the server.
        /// </summary>
        public XDocument VolumeXML
        {
            get
            {
                return _VolumeXML; 
            }
        }

        public Section[] Sections
        {
            get { return SectionsTable.Values.ToArray(); }
        }

        /// <summary>
        /// Names of transform groups that can be used to register images into the volume
        /// </summary>
        public List<string> VolumeTransformNames = new List<string>(new string[] {"None"}); 
        
        private Dictionary<int, int> SectionToReferenceSectionBelow = new Dictionary<int, int>();

        /// <summary>
        /// Specified during loading, if the <DefaultTileset> element exists we assign all sections containing that tileset to use it as the default transform
        /// </summary>
        private string DefaultTileset = null;

        /// <summary>
        /// The server the volume transforms and image data is located on
        /// </summary>
        private string _Host;
        public string Host
        {
            get { return _Host; }
        }

       
        /// <summary>
        /// The path we use to cache data on the local drive
        /// </summary>
        internal readonly string LocalCachePath;

        /// <summary>
        /// Maps a section number to its section object
        /// </summary>
        public SortedList<int, Section> SectionsTable = new SortedList<int,Section>();

        /// <summary>
        /// Sorted list containing the transforms for each volume transform we find
        /// Key = Downsample level
        /// Value = Dictionary mapping each section number to a stos transform.  This is because section numbers may not be continuos
        /// </summary>
        public SortedList<string, SortedList<int, Geometry.Transforms.TriangulationTransform>> Transforms = new SortedList<string, SortedList<int, Geometry.Transforms.TriangulationTransform>>();

        public int NumSections
        {
            get { return SectionsTable.Count; }
        }

        /// <summary>
        /// Returns the section that the passed section was registered to
        /// </summary>
        /// <param name="?"></param>
        /// <returns></returns>
        public Section GetReferenceSectionBelow(Section section)
        {
            if (section == null)
                return null;

            //Optimistic implementation that looks at section immediately above
            int refnumber = section.Number - 1;
            int minSectionNumber = SectionsTable.Keys.Min();
            while (refnumber >= minSectionNumber)
            {
                if (SectionsTable.ContainsKey(refnumber))
                    return SectionsTable[refnumber];
                refnumber--;
            }

            return null; 
        }

        /// <summary>
        /// Returns the section that the passed section was registered to
        /// </summary>
        /// <param name="?"></param>
        /// <returns></returns>
        public Section GetReferenceSectionAbove(Section section)
        {
            if (section == null)
                return null;

            //Optimistic implementation that looks at section immediately above
            int refnumber = section.Number + 1;
            int maxSectionNumber = SectionsTable.Keys.Max();
            while (refnumber <= maxSectionNumber)
            {
                if (SectionsTable.ContainsKey(refnumber))
                    return SectionsTable[refnumber];
                refnumber++;
            }

            return null; 
        }

        private SortedList<string, TileServerInfo> TileServerList = new SortedList<string, TileServerInfo>();


        private string VolumeCachePath
        {
            get
            {
                return this.LocalCachePath + System.IO.Path.DirectorySeparatorChar + this.Name;
            }
        }

        private string VolumeStosZipCachePath
        {
            get
            {
                return this.LocalCachePath + System.IO.Path.DirectorySeparatorChar + this.Name +  System.IO.Path.DirectorySeparatorChar + "StosZip";
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path">The host and path to the volume, no filenames</param>
        /// <param name="localCachePath">LocaL cache path corresponding to the path</param>
        /// <param name="workerThread">optional worker thread to report progress</param>
        public Volume(string path, string localCachePath, System.ComponentModel.BackgroundWorker workerThread)
        {
            //Load the default settings from user preferences
//            ChannelInfo DefaultChannel = new ChannelInfo();
            DefaultChannels = new ChannelInfo[0];  

            this._Host = path;
            this.LocalCachePath = localCachePath; 

            XDocument VolumeXML = Load(path, workerThread);
            Initialize(VolumeXML, workerThread); 
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path">The host and path to the volume, no filenames</param>
        /// <param name="localCachePath">LocaL cache path corresponding to the path</param>
        /// <param name="workerThread">optional worker thread to report progress</param>
        public Volume(string path, string localCachePath, XDocument VolumeXML, System.ComponentModel.BackgroundWorker workerThread)
        {
            //Load the default settings from user preferences
            //ChannelInfo DefaultChannel = new ChannelInfo();
            DefaultChannels = new ChannelInfo[0];

            this._Host = path;
            this.LocalCachePath = localCachePath;

            Initialize(VolumeXML, workerThread); 
        }


        #region Channels
        
        private ChannelInfo[] _DefaultChannels = new ChannelInfo[0];

        public ChannelInfo[] DefaultChannels
        {
            get { return _DefaultChannels; }
            set
            {
                if (null == value)
                {
                    _DefaultChannels = new ChannelInfo[0];
                    return;
                }

                _DefaultChannels = value;
            }
        }

        //A list of all channel names found in the volume
        //TODO: Modify to a per section basis?
        static private List<string> _ChannelNames = new List<String>();

        /// <summary>
        /// A list of all channel names found on sections in the volume
        /// </summary>
        public string[] ChannelNames
        {
            get { return _ChannelNames.ToArray(); }
        }

        private void AddChannel(string name)
        {
            //TODO: This needs a more thorough fix.  Sections are created on threads and they race to add entries to this list.
            //We should import all sections and then build the list from the results
            if (false == _ChannelNames.Contains(name))
            {
                _ChannelNames.Add(name);
                _ChannelNames.Sort();
            }
        }

        public void RemoveChannel(string name)
        {
            if (false == _ChannelNames.Contains(name))
                return;

            _ChannelNames.Remove(name);
        }

        #endregion

        /// <summary>
        /// Loads a path, determines whether path refers to XML file or a local directory
        /// </summary>
        /// <param name="path"></param>
        protected XDocument Load(string path, BackgroundWorker workerThread)
        {
            Uri uri = new Uri(path);

            workerThread.ReportProgress(0, "Requesting " + path);

            XDocument XMLInitData; 
            if (uri.Scheme == "http" || uri.Scheme == "https")
                XMLInitData = LoadHTTP(path);
            else
                XMLInitData = LoadLocal(uri.LocalPath);

            return XMLInitData; 
        }

        /// <summary>
        /// Loads a path, determines whether path refers to XML file or a local directory
        /// </summary>
        /// <param name="path"></param>
        protected void Load(XDocument volumeXML, BackgroundWorker workerThread)
        {
            this._VolumeXML = volumeXML;

            DateTime start = DateTime.UtcNow;

            Initialize(this._VolumeXML, workerThread); 

            //Create a path for the cache
            string VolumeCachePath = this.LocalCachePath +
                System.IO.Path.DirectorySeparatorChar +
                this.Name;

            if (System.IO.Directory.Exists(VolumeCachePath) == false)
                System.IO.Directory.CreateDirectory(VolumeCachePath);

            CreateVolumeTransforms(workerThread);

            DateTime end = DateTime.UtcNow;

            Trace.WriteLine("Volume load time: " + new TimeSpan(end.Ticks - start.Ticks).TotalSeconds.ToString("D2"));

            workerThread.ReportProgress(101, "Done!"); 
        }

        protected XDocument LoadHTTP(string path)
        {
            //Remove the .xml file from the path
            int iRemove = path.LastIndexOf('/');
            string VolumePath = path;
            if (iRemove > 0)
            {
                VolumePath = VolumePath.Remove(iRemove);
            }

            //This code remains, but the value is replaced if a value is found in the XML file
            this._Host = VolumePath;

            this._IsLocal = false;
            Uri pathURI = new Uri(path);

            HttpWebRequest request = WebRequest.Create(pathURI) as HttpWebRequest;
            if (pathURI.Scheme.ToLower() == "https")
                request.Credentials = this.UserCredentials; 

            request.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.Revalidate);

            WebResponse response = null;
            XDocument reader = null;
            try
            {
                response = request.GetResponse();

                Stream responseStream = response.GetResponseStream();
                
                using (StreamReader XMLStream = new StreamReader(responseStream))
                {

                    reader = XDocument.Parse(XMLStream.ReadToEnd());
                }
            }
            catch (WebException e)
            {
                /*PORT: Don't have forms, throw a better exception*/
                throw new WebException("Error connecting to volume server: \n" + path + "\n" + e.Message, e);
            }
            finally
            {
                if(response != null)
                    response.Close();
            }

            return reader; 
        }

        
        protected static XDocument LoadLocal(string path)
        {
            XDocument reader = null; 
            using (StreamReader XMLStreamReader = new StreamReader(File.OpenRead(path)))
            { 
                string text = XMLStreamReader.ReadToEnd();
                reader = XDocument.Parse(text);
            } 

            return reader;
        }

        private static bool FetchStosZip(Uri StosZipPath, System.Net.NetworkCredential UserCredentials, string LocalCachePath)
        {
            HttpWebRequest request = WebRequest.Create(StosZipPath) as HttpWebRequest;
            if (StosZipPath.Scheme.ToLower() == "https")
                request.Credentials = UserCredentials;

            request.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.Revalidate);

            try
            {
                using (WebResponse response = request.GetResponse())
                {  
                    using (Stream responseStream = response.GetResponseStream())
                    {
                        Byte[] buffer = Global.ReadToBuffer(responseStream, response.ContentLength);

                        using (MemoryStream memStream = new MemoryStream(buffer))
                        {
                            using (ZipFile zipFile = ZipFile.Read(memStream))
                            {
                                //ZipFile takes a reference to the stream instead of copying it (which it should not do).  We cannot close the memory stream.
                                //until zipfile is closed
                                if(System.IO.Directory.Exists(LocalCachePath))
                                {
                                    System.IO.Directory.Delete(LocalCachePath, true);
                                }

                                zipFile.ExtractAll(LocalCachePath, ExtractExistingFileAction.OverwriteSilently);                                
                            }
                        }
                    }
                }
            } 
            catch (WebException e)
            {
                /*PORT: Don't have forms, throw a better exception*/
                Trace.WriteLine("Error connecting to volume server: \n" + StosZipPath + "\n" + e.Message, "VolumeModel");
                return false; 
            }
            catch (Exception e)
            {
                Trace.WriteLine("Could not open StosZip file", "VolumeModel");
            } 

            return true; 
        } 

        /// <summary>
        /// We load any default values for the volume model first.  At the time I added this section
        /// loading threads referred to these values.  We don't want a race
        /// </summary>
        /// <param name="volumeElement"></param>
        private void LoadDefaultsFromXML(XElement volumeElement)
        {
            foreach (XNode node in volumeElement.Nodes().ToList<XNode>())
            {
                if (node.NodeType == System.Xml.XmlNodeType.Whitespace)
                    continue;

                XElement elem = node as XElement;
                if (elem == null)
                    continue;

                //Fetch the name if we know it
                switch (elem.Name.LocalName.ToLower())
                {
                    case "defaulttileset":
                        this.DefaultTileset = IO.GetAttributeCaseInsensitive(elem, "name").Value;
                        break;
                    case "channelinfo":
                        this.DefaultChannels = ChannelInfo.FromXML(elem);
                        break;
                 }
            }
        } 

        private void LoadDefaultsFromVolumeElement(XElement volumeElement)
        {
            this.Name = IO.GetAttributeCaseInsensitive(volumeElement, "Name").Value;

            XAttribute defaulttileset = IO.GetAttributeCaseInsensitive(volumeElement, "defaulttileset");
            if(defaulttileset != null)
            {
                this.DefaultTileset = defaulttileset.Value; 
            }

            XAttribute defaultimagepyramid = IO.GetAttributeCaseInsensitive(volumeElement, "defaultimagepyramid");
            if(defaultimagepyramid != null)
            {
                this.DefaultImagePyramid = defaultimagepyramid.Value; 
            }

            XAttribute defaultmosaictransform = IO.GetAttributeCaseInsensitive(volumeElement, "defaultmosaictransform");
            if(defaultmosaictransform != null)
            {
                this.DefaultMosaicTransform = defaultmosaictransform.Value; 
            }

            XAttribute defaultstosgroup = IO.GetAttributeCaseInsensitive(volumeElement, "defaultstosgroup");
            if(defaultstosgroup != null)
            {
                this.DefaultTileset = defaultstosgroup.Value; 
            }

            XAttribute defaultsection = IO.GetAttributeCaseInsensitive(volumeElement, "defaultsection");
            if (defaultsection != null)
            {
                try
                {
                    this.DefaultSectionNumber = new int?(Convert.ToInt32(defaultsection.Value));
                }
                catch(FormatException e)
                {
                    Trace.WriteLine("Unable to parse default section: " + defaultsection.Value);
                } 
            }

            return; 
        }

          
        void Initialize(XDocument reader, BackgroundWorker workerThread)
        {
            List<CreateSectionThreadingObj> ListSectionThreadingObj = new List<CreateSectionThreadingObj>();
            List<CreateStosTransformThreadingObj> ListStosGridTransformThreadingObj = new List<CreateStosTransformThreadingObj>();

            this._VolumeXML = reader; 
            //Fetch the volume information which should be the top level of the XML

            //Search for the correct node in the XML
            IEnumerable<XElement> VolumeElements = reader.Elements().Where(e => e.Name.LocalName == "Volume");
            if (VolumeElements.Count() == 0)
            {
                Trace.WriteLine("No volume node found in the XML", "VolumeModel");
                throw new InvalidDataException("No volume node found in the VikingXML");
            }

            XElement volumeElement = VolumeElements.First();
            LoadDefaultsFromVolumeElement(volumeElement);

            int NumStosFiles = System.Convert.ToInt32(IO.GetAttributeCaseInsensitive(volumeElement, "num_stos").Value);
            int NumSections = System.Convert.ToInt32(IO.GetAttributeCaseInsensitive(volumeElement, "num_sections").Value);

            XAttribute VolumePathAttrib = IO.GetAttributeCaseInsensitive(volumeElement,"path");
            if (VolumePathAttrib != null)
                this._Host = VolumePathAttrib.Value;
            else
            {
                /* PORT
                System.Windows.Forms.MessageBox.Show("Could locate path attribute for volume.  Chances are the XML definitation for this volume has not been updated. Contact administrator to update the VikingXML file.", "Error", System.Windows.Forms.MessageBoxButtons.OK);
                if (this._Host == null) //If we don't know a path throw an exception to kill the process
                    throw new ArgumentException("Could locate path attribute for volume.  Chances are the XML definitation for this volume has not been updated. Contact administrator to update the VikingXML file.");
                 */
            }

            //Remove a trailing slash
            if (this._Host.EndsWith("/"))
                this._Host = this._Host.TrimEnd('/');


            if (IO.GetAttributeCaseInsensitive(volumeElement,"UniqueID") != null)
                this._UniqueID = IO.GetAttributeCaseInsensitive(volumeElement,"UniqueID").Value;

            //Create a path for the cache  
            if (System.IO.Directory.Exists(this.VolumeCachePath) == false)
                System.IO.Directory.CreateDirectory(this.VolumeCachePath);

            bool HaveStosZip = false;
            if (IO.GetAttributeCaseInsensitive(volumeElement,"StosZip") != null)
            {
                string StosZipFileName = IO.GetAttributeCaseInsensitive(volumeElement,"StosZip").Value;
                workerThread.ReportProgress(0, "Loading compressed transform file " + StosZipFileName);
                HaveStosZip = FetchStosZip(new Uri(Host + '/' + StosZipFileName), this.UserCredentials, this.VolumeStosZipCachePath);
            }

            int countStos = 0;
            int countSections = 0;
            ListSectionThreadingObj.Capacity = NumSections;
            ListStosGridTransformThreadingObj.Capacity = NumStosFiles;

            LoadDefaultsFromXML(volumeElement);

            foreach (XNode node in volumeElement.Nodes().ToList<XNode>())
            {
                if (node.NodeType == System.Xml.XmlNodeType.Whitespace)
                    continue;

                XElement elem = node as XElement;
                if (elem == null)
                    continue;

                //Fetch the name if we know it
                switch (elem.Name.LocalName.ToLower())
                {
                    case "stos":
                        string StosFileName = IO.GetAttributeCaseInsensitive(elem,"path").Value;
                        Uri StosPath = new Uri(this.Host + Path.DirectorySeparatorChar + StosFileName);
                        
                  //      int pixelSpacing = System.Convert.ToInt32(GetAttributeCaseInsensitive(elem,"pixelSpacing").Value);
                        int ProgressPercent = (countStos * 100) / NumStosFiles;
                        countStos++;
                        workerThread.ReportProgress(ProgressPercent, "Loading " + StosPath);

                        //string groupName = type + " " + pixelSpacing.ToString();
                        

                  //      XAttribute GroupNameAttribute = GetAttributeCaseInsensitive(elem,"GroupName");
                  //      if (GroupNameAttribute != null)
                  //      {
                  //          groupName = GroupNameAttribute.Value;
                  //      }

                  //      if (false == VolumeTransformNames.Contains(groupName))
                  //      {
                  //          VolumeTransformNames.Add(groupName);
                  //      }

                       

                        //StosGridTransform stosTransform = null;
                        CreateStosTransformThreadingObj CreateStosThreadObj = null;

                        if (HaveStosZip)
                        {
                            String StosFileCacheFullPath = System.IO.Path.Combine(this.VolumeStosZipCachePath, StosFileName);
                            if (System.IO.File.Exists(StosFileCacheFullPath))
                            {
                                /*
                                Trace.WriteLine("Loading " + StosFileName + " from Zip", "VolumeModel");
                                ZipEntry entry = StosZipFile[StosFileName];
                                DateTime lastModified = entry.LastModified.ToUniversalTime();
                                Byte[] buffer = new Byte[entry.UncompressedSize];

                                MemoryStream memStream = new MemoryStream(buffer);
                                entry.Extract(memStream);
                                */
                                //stosTransform = new StosGridTransform(memStream, elem);
                                CreateStosThreadObj = new CreateStosTransformThreadingObj(StosFileCacheFullPath, elem);
                                
                                //memStream.Close(); 
                            }
                        }

                        //Load from server if it is not in the zip
                        if (CreateStosThreadObj == null)
                        {
                        //    Trace.WriteLine("Loading " + StosFileName + " from HTTP Server", "VolumeModel");
                            CreateStosThreadObj = new CreateStosTransformThreadingObj(StosPath, this.UserCredentials, elem);
                            //stosTransform = new StosGridTransform(StosPath, elem, this.UserCredentials);
                        }

                        ListStosGridTransformThreadingObj.Add(CreateStosThreadObj); 
                        System.Threading.ThreadPool.QueueUserWorkItem(CreateStosThreadObj.ThreadPoolCallback);
                          
                        break;
                    case "section":
                        //string SectionPath = VolumePath + '/' + GetAttributeCaseInsensitive(elem,"path").Value;
                        string SectionPath = IO.GetAttributeCaseInsensitive(elem,"path").Value;


                        if (NumSections > 0)
                        {
                            ProgressPercent = (countSections * 100) / NumSections;
                        }
                        else
                        {
                            ProgressPercent = 100;
                        }

                        countSections++;
                        workerThread.ReportProgress(ProgressPercent, "Queueing " + SectionPath);

                        CreateSectionThreadingObj newCreateSectionThreadObj = new CreateSectionThreadingObj(this,
                            SectionPath,
                            elem);
                        ListSectionThreadingObj.Add(newCreateSectionThreadObj);

                        System.Threading.ThreadPool.QueueUserWorkItem(newCreateSectionThreadObj.ThreadPoolCallback);

                        //                        newCreateSectionThreadObj.ThreadPoolCallback(null);
                        //                        Section s = new Section(this, SectionPath, elem.CreateReader());
                        //                        this.sections.Add(s.Number, s);

                        break;
                    case "tileserver":
                        TileServerInfo info = TileServerInfo.CreateFromElement(elem);
                        this.TileServerList[info.Name] = info;
                        break;
                    
                    default:
                        break;
                } 
            }

            {
                workerThread.ReportProgress(0, "Waiting for Stos Transform Loading Threads");
                int countFinished = 0;

                while (ListStosGridTransformThreadingObj.Count > 0)
                {
                    for (int iObj = 0; iObj < ListStosGridTransformThreadingObj.Count; iObj++)
                    {
                        CreateStosTransformThreadingObj CreateStosGridTransformObj = ListStosGridTransformThreadingObj[iObj];

                        //Test to see if the wait state is set
                        bool Result = CreateStosGridTransformObj.DoneEvent.WaitOne(0);
                        if (Result == true)
                        {
                            ListStosGridTransformThreadingObj.RemoveAt(iObj);
                            iObj--;
                            countFinished++;
                            int Progress;
                            if (NumStosFiles > 0)
                                Progress = (countFinished * 100) / NumStosFiles;
                            else
                                Progress = 100;

                            if (CreateStosGridTransformObj.stosTransform == null)
                            {
                                workerThread.ReportProgress(Progress, "Failed Loading " + CreateStosGridTransformObj.ToString());
                                continue;
                            }
                            else
                            {
                                workerThread.ReportProgress(Progress, "Loaded " + CreateStosGridTransformObj.stosTransform.ToString());
                            }
                            

                            XElement elem = CreateStosGridTransformObj.element; 

                            int pixelSpacing = System.Convert.ToInt32(IO.GetAttributeCaseInsensitive(elem,"pixelSpacing").Value);
                            string type = IO.GetAttributeCaseInsensitive(elem,"type").Value;
                            string groupName = type + " " + pixelSpacing.ToString();
                            
                            XAttribute GroupNameAttribute = CreateStosGridTransformObj.element.Attribute("GroupName");
                            if (GroupNameAttribute != null)
                            {
                                groupName = GroupNameAttribute.Value;
                            }

                            if (false == VolumeTransformNames.Contains(groupName))
                            {
                                VolumeTransformNames.Add(groupName);
                            }

                            if (this.DefaultVolumeTransform == "None")
                                this.DefaultVolumeTransform = groupName;

                            if (CreateStosGridTransformObj.stosTransform != null)
                            {
                                StosTransformInfo info = CreateStosGridTransformObj.stosTransform.Info as StosTransformInfo;
                                SortedList<int, TriangulationTransform> transformDict = null;
                                if (this.Transforms.ContainsKey(groupName))
                                {
                                    transformDict = this.Transforms[groupName];
                                }
                                else
                                {
                                    transformDict = new SortedList<int, TriangulationTransform>();                                    
                                    Transforms.Add(groupName, transformDict); 
                                }

                                if(transformDict.ContainsKey(info.MappedSection))
                                {
                                    Console.WriteLine("Volume stos mapping already contains " + info.ToString());
                                }
                                else
                                {
                                    transformDict.Add(info.MappedSection, CreateStosGridTransformObj.stosTransform as TriangulationTransform);
                                }
                            }
                            else
                            {
                                Trace.WriteLine("Could not load stos file: " + CreateStosGridTransformObj.element.ToString());
                            }
                        }
                    }

                    System.Threading.Thread.Sleep(100);
                }

            }


            WaitForCreateSectionThreads(ListSectionThreadingObj, workerThread);

            CreateVolumeTransforms(workerThread); 
            workerThread.ReportProgress(101, "Done!");
        }

        private void WaitForCreateSectionThreads(List<CreateSectionThreadingObj> ListSectionThreadingObj, BackgroundWorker workerThread)
        {
            //            XMLStream.Close();
            //            response.Close();
            workerThread.ReportProgress(0, "Waiting for Section Loading Threads");

            int countFinished = 0;
            while (ListSectionThreadingObj.Count > 0)
            {
                for (int iObj = 0; iObj < ListSectionThreadingObj.Count; iObj++)
                {
                    CreateSectionThreadingObj CreateSectionObj = ListSectionThreadingObj[iObj];

                    //Test to see if the wait state is set
                    bool Result = CreateSectionObj.DoneEvent.WaitOne(0);
                    if (Result == true)
                    {
                        ListSectionThreadingObj.RemoveAt(iObj);
                        iObj--;
                        countFinished++;
                        int Progress;
                        if (NumSections > 0)
                            Progress = (countFinished * 100) / NumSections;
                        else
                            Progress = 100;

                        workerThread.ReportProgress(Progress, "Loaded " + CreateSectionObj.newSection.ToString());

                        this.OnSectionLoadComplete(CreateSectionObj.newSection);
                    }
                }

                System.Threading.Thread.Sleep(100);
            }
        }

        /// <summary>
        /// Called on the main thread after a section has loaded on a worker thread
        /// </summary>
        private void OnSectionLoadComplete(Section section)
        {
            foreach(string name in section.ChannelNames)
            {
                this.AddChannel(name);
            }

            this.AddTileServerToSectionMappings(section);

            if(this.DefaultTileset != null)
            {
                if(section.ChannelNames.Contains(DefaultTileset))
                {
                    section.DefaultTileset = DefaultTileset;
                }
            }

            this.SectionsTable.Add(section.Number, section); 
        }

        private void AddTileServerToSectionMappings(Section section)
        {
            foreach(TileServerInfo tileserver in this.TileServerList.Values)
            {
                section.AddTileserver(tileserver);
            } 
        }
         
        /// <summary>
        /// Adds a transform to each section mapping it into each of the volume spaces we found
        /// </summary>
        public void CreateVolumeTransforms(BackgroundWorker workerThread)
        {
            int iSectionProgress = 0; 
            foreach (string TransformKey in Transforms.Keys)
            {
                //The transform list is sorted by which section the transform maps from. 
                //Next we'll add transfroms so every transform maps from the mapped section to section #1
                SortedList<int, TriangulationTransform> TList = Transforms[TransformKey];

                //Create a registration chain so we know what order to register the sections in
                RegistrationTree tree = new RegistrationTree();
                foreach (int iSection in TList.Keys)
                {
                    TriangulationTransform trans = TList[iSection];
                    StosTransformInfo info = trans.Info as StosTransformInfo;
                    if (info == null)
                        continue;

                    if (false == SectionsTable.ContainsKey(info.MappedSection))
                        continue;

                    tree.AddPair(info.ControlSection, info.MappedSection); 
                }

                iSectionProgress = 0;
                //OK, walk the tree, adding from the root nodes down
                foreach (RegistrationTreeNode rootnode in tree.RootNodes.Values)
                {
                    Queue<int> SafeNodes = new Queue<int>();
                    SafeNodes.Enqueue(rootnode.SectionNumber);

                    while (SafeNodes.Count > 0)
                    {
                        int ControlSection = SafeNodes.Dequeue();
                        RegistrationTreeNode ControlNode = tree.Nodes[ControlSection];

                        TriangulationTransform ControlTrans = null;
                        //Find the section that can map our transform
                        if (TList.ContainsKey(ControlNode.SectionNumber))
                            ControlTrans = TList[ControlNode.SectionNumber];

                        foreach (int childSection in ControlNode.Children)
                        {
                            iSectionProgress++;
                            TriangulationTransform trans = TList[childSection];
                            StosTransformInfo info = trans.Info as StosTransformInfo;
                            if (info == null)
                                continue;

                            if (false == SectionsTable.ContainsKey(info.MappedSection))
                                continue;

                            //Add this mapping to our dictionary:
                            if (SectionToReferenceSectionBelow.ContainsKey(info.MappedSection) == false)
                            {
                                SectionToReferenceSectionBelow.Add(info.MappedSection, info.ControlSection);
                            }

                            //NOTE: Assumes volumes use the same mappings across all downsamplings
                            //Sections should register to section 1, but if a volume hasn't finished registration or was done in parts we may register to a section other than 1
                            //Debug.Assert(addTrans.ControlSection == 1);
                            
                            //   Trace.WriteLine(outString, "VolumeModel");

                            if (ControlTrans != null)
                            {
                                StosTransformInfo ControlInfo = ControlTrans.Info as StosTransformInfo;

                                string CacheStosPath = this.LocalCachePath +
                                                      System.IO.Path.DirectorySeparatorChar +
                                                      this.Name +
                                                      System.IO.Path.DirectorySeparatorChar +
                                                      info.MappedSection.ToString() + "-" + ControlInfo.ControlSection.ToString() + ".stos"; 

                               

                                
                                bool CalculateSliceToVolume = true;
                                
                                Stream stostext = null;
                                try
                                {
                                    if (System.IO.File.Exists(CacheStosPath))
                                    {
                                        DateTime CacheLastModifiedUtc = System.IO.File.GetLastWriteTimeUtc(CacheStosPath);
                                        if (ControlTrans.Info.LastModified < CacheLastModifiedUtc)
                                        {

                                            string outString = "Loading from cache: " + trans.ToString() + " to " + ControlTrans.ToString();

                                            stostext = System.IO.File.OpenRead(CacheStosPath) as Stream;
                                            TList[childSection] = TransformFactory.ParseStos(stostext,
                                                                                            new StosTransformInfo(ControlInfo.ControlSection, info.MappedSection, CacheLastModifiedUtc),
                                                                                             1) as TriangulationTransform;

                                            if (TList[childSection] != null)
                                                CalculateSliceToVolume = false;
                                        }
                                    }
                                }
                                catch (Exception)
                                {

                                }
                                finally
                                {
                                    if (stostext != null)
                                    {
                                        stostext.Close();
                                        stostext = null; 
                                    }
                                }

                                //CalculateSliceToVolume = true; 
                                if (CalculateSliceToVolume)
                                {
                                    try
                                    {
                                        string outString = "Adding transforms: " + trans.ToString() + " to " + ControlTrans.ToString();
                                        workerThread.ReportProgress((iSectionProgress * 100) / TList.Count, outString);
                                        TList[childSection] = TriangulationTransform.Transform(ControlTrans,
                                                                                               trans,
                                                                                               new StosTransformInfo(ControlInfo.ControlSection, info.MappedSection, 
                                                                                               ControlTrans.Info.LastModified > trans.Info.LastModified ? ControlTrans.Info.LastModified : trans.Info.LastModified));
                                    }
                                    catch (Exception)
                                    {
                                        trans = TList[childSection];
                                    }

                                    StreamWriter fs = null;
                                    
                                    MeshTransform meshTransform = TList[childSection] as MeshTransform;
                                    if (meshTransform != null)
                                    { 
                                        using (fs = System.IO.File.CreateText(CacheStosPath))
                                        {

                                            fs.WriteLine(ControlTrans.ToString());
                                            fs.WriteLine(trans.ToString());

                                            meshTransform.SaveMosaic(fs);

                                            fs.Flush();
                                        }
                                    }                             
                                   
                                }
                            }



                            SafeNodes.Enqueue(childSection);
                        }

                    }
                }
                
                /*
                foreach (int iSection in TList.Keys)
                {
                    StosGridTransform trans = TList[iSection];
                    if (false == SectionsTable.ContainsKey(trans.MappedSection))
                        continue; 

                    Section S = SectionsTable[trans.MappedSection];
 //                   S.AddVolumeTransform(trans, "slice-to-slice " + TransformKey.ToString());

                    if (trans.ControlSection == 1)
                        continue;

                    if(false == TList.ContainsKey(trans.ControlSection))
                        continue;

                    //Find the section that can map our transform
                    StosGridTransform addTrans = TList[trans.ControlSection];

                    //Add this mapping to our dictionary:
                    if (SectionToReferenceSectionBelow.ContainsKey(trans.MappedSection) == false)
                    {
                        SectionToReferenceSectionBelow.Add(trans.MappedSection, trans.ControlSection);
                    }

                    //NOTE: Assumes volumes use the same mappings across all downsamplings
                    //Sections should register to section 1, but if a volume hasn't finished registration or was done in parts we may register to a section other than 1
                    //Debug.Assert(addTrans.ControlSection == 1);
                    string outString = "Adding transforms: " + trans.ToString() + " to " + addTrans.ToString();
                 //   Trace.WriteLine(outString, "VolumeModel");
                    workerThread.ReportProgress((iSection * 100) / TList.Keys[TList.Count - 1], outString);
                    trans.Add(addTrans);

                    //Let windows process some events
                    //PORT, can't do this without referencing System.Windows
                    //System.Windows.Forms.Application.DoEvents();
                }
                */
            }
        }
    }
}
