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
using Viking.VolumeModel;
using UnitsAndScale;

namespace Viking.VolumeModel
{
    public class OCPChannelInfo
    {
        public string Name;
        public string Path;

        public OCPChannelInfo(string Name, string Path)
        {
            this.Name = Name;
            this.Path = Path;
        }

        public OCPChannelInfo(XElement elem)
        {
            this.Name = elem.GetAttributeCaseInsensitive("name").Value;
            this.Path = elem.HasAttributeCaseInsensitive("path") ? elem.GetAttributeCaseInsensitive("path").Value : this.Name;
        }
    }

    public class EndpointInformation
    {
        public readonly Uri AuthenticationURL;
        public readonly Uri EndpointURL;
        public readonly Uri ExportURL;

        public EndpointInformation(string Authentication, string Endpoint, string exportURL)
        {
            AuthenticationURL = new Uri(Authentication);
            EndpointURL = new Uri(Endpoint);
            this.ExportURL = new Uri(exportURL);
        }

        internal static EndpointInformation CreateFromElement(XElement elem)
        {
            return new EndpointInformation(elem.GetAttributeCaseInsensitive("authentication").Value,
                                           elem.GetAttributeCaseInsensitive("endpoint").Value, 
                                           elem.GetAttributeCaseInsensitive("exporturl").Value);
        }
    }

    public class TileServerInfo
    {
        public string Host { get; private set; }
        public string CoordSpaceName { get; private set; }
        public int TileXDim { get; private set; }
        public int TileYDim { get; private set; }
        public int GridXDim { get; private set; }
        public int GridYDim { get; private set; }
        public int MaxLevel { get; private set; }
        public string FilePrefix { get; private set; }
        public string FilePostfix { get; private set; }

        public List<OCPChannelInfo> Channels { get; private set; }

        public static TileServerInfo CreateFromElement(XElement node)
        {
            TileServerInfo info = new TileServerInfo();

            info.TileXDim = System.Convert.ToInt32(IO.GetAttributeCaseInsensitive(node, "TileXDim").Value);
            info.TileYDim = System.Convert.ToInt32(IO.GetAttributeCaseInsensitive(node, "TileYDim").Value);
            info.GridXDim = System.Convert.ToInt32(IO.GetAttributeCaseInsensitive(node, "GridXDim").Value);
            info.GridYDim = System.Convert.ToInt32(IO.GetAttributeCaseInsensitive(node, "GridYDim").Value);
            info.MaxLevel = System.Convert.ToInt32(IO.GetAttributeCaseInsensitive(node, "MaxLevel").Value);
            info.FilePrefix = IO.GetAttributeCaseInsensitive(node, "FilePrefix").Value;
            info.FilePostfix = IO.GetAttributeCaseInsensitive(node, "FilePostfix").Value;
            info.Host = IO.GetAttributeCaseInsensitive(node, "host").Value;
            info.CoordSpaceName = IO.GetAttributeCaseInsensitive(node, "coordspacename").Value;

            info.Channels = node.Elements().Where(e => e.Name == "Channel").Select(e => new OCPChannelInfo(e)).ToList();

            return info;
        }

        protected TileServerInfo()
        {

        }
    }



    /// <summary>
    /// Collection of volumes, sections and tiles. There is only one dataset loaded at a time.
    /// TODO: Split parsing the VikingXML into a separate class
    /// </summary>
    public class Volume
    {
        /// <summary>
        /// Friendly name for the volume
        /// </summary>
        public string Name = "";

        /// <summary>
        /// Name of the volume transform to use by default
        /// </summary>
        public string DefaultVolumeTransform = null;

        /// <summary>
        /// Name of the default stos group
        /// </summary>
        public string DefaultStosGroup = null;

        /// <summary>
        /// Name of the default image pyramid
        /// </summary>
        public string DefaultImagePyramid = null;

        /// <summary>
        /// Name of the default tile-to-mosaic transform when using pyramids
        /// </summary>
        public string DefaultMosaicTransform = null;

        /// <summary>
        /// The starting section number read from meta-data
        /// </summary>
        public int? DefaultSectionNumber = new int?();

        /// <summary>
        /// If true the VikingXML requests the client update the server volume positions if they are noticeably different.
        /// </summary>
        public bool UpdateServerVolumePositions = false;

        public EndpointInformation Endpoint = null;

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

        private XElement _VolumeElement;

        /// <summary>
        /// The XML document used to initialize the volume.  Contains all configuration settings from the server.
        /// </summary>
        public XElement VolumeElement
        {
            get
            {
                return _VolumeElement;
            }
        }

        /// <summary>
        /// Names of transform groups that can be used to register images into the volume
        /// </summary>
        public List<string> VolumeTransformNames = new List<string>(new string[] { "None" });

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
        internal readonly VolumePaths Paths;

        /// <summary>
        /// Maps a section number to its section object
        /// </summary>
        public SortedList<int, Section> Sections = new SortedList<int, Section>();

        /// <summary>
        /// Sorted list containing the transforms for each volume transform we find
        /// Key = Downsample level
        /// Value = Dictionary mapping each section number to a stos transform.  This is because section numbers may not be continuos
        /// </summary>
        public SortedList<string, SortedList<int, ITransform>> Transforms = new SortedList<string, SortedList<int, ITransform>>();

        public int NumSections
        {
            get { return Sections.Count; }
        }

        private IAxisUnits _DefaultXYScale;

        public IAxisUnits DefaultXYScale
        {
            get { return _DefaultXYScale;}
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
            int minSectionNumber = Sections.Keys.Min();
            while (refnumber >= minSectionNumber)
            {
                if (Sections.ContainsKey(refnumber))
                    return Sections[refnumber];
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
            int maxSectionNumber = Sections.Keys.Max();
            while (refnumber <= maxSectionNumber)
            {
                if (Sections.ContainsKey(refnumber))
                    return Sections[refnumber];
                refnumber++;
            }

            return null;
        }

        private List<TileServerInfo> TileServerList = new List<TileServerInfo>();

        

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path">The host and path to the volume, no filenames</param>
        /// <param name="localCachePath">LocaL cache path corresponding to the path</param>
        /// <param name="workerThread">optional worker thread to report progress</param>
        public Volume(string path, string localCachePath, Viking.Common.IProgressReporter workerThread)
        {
            //Load the default settings from user preferences
            //            ChannelInfo DefaultChannel = new ChannelInfo();
            DefaultChannels = new ChannelInfo[0];

            XDocument VolumeXML = LoadXDocument(path, null, workerThread);
            if(IsVolumePathLocal(path))
            {
                //This code remains, but the value is replaced if a value is found in the XML file
                this._Host = RemoveXMLExtension(path);
                this._IsLocal = false;
            }

            this._VolumeElement = GetVolumeElement(VolumeXML);
            LoadDefaultsFromVolumeElement(_VolumeElement);

            this._Host = path;
            this.Paths = new VolumePaths(localCachePath, this.Name);

            Initialize(VolumeXML, workerThread);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path">The host and path to the volume, no filenames</param>
        /// <param name="localCachePath">LocaL cache path corresponding to the path</param>
        /// <param name="workerThread">optional worker thread to report progress</param>
        public Volume(string path, string localCachePath, XDocument VolumeXML, Viking.Common.IProgressReporter workerThread)
        {
            //Load the default settings from user preferences
            //ChannelInfo DefaultChannel = new ChannelInfo();
            DefaultChannels = new ChannelInfo[0];

            this._Host = path;
            this._VolumeElement = GetVolumeElement(VolumeXML);
            LoadDefaultsFromVolumeElement(_VolumeElement);

            this.Paths = new VolumePaths(localCachePath, this.Name);

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

        public static bool IsVolumePathLocal(string path)
        {
            Uri uri = new Uri(path);
            if (uri.Scheme == "http" || uri.Scheme == "https")
                return false;

            return true; 
        }

        /// <summary>
        /// Loads a path, determines whether path refers to XML file or a local directory
        /// </summary>
        /// <param name="path"></param>
        public static XDocument LoadXDocument(string path, System.Net.NetworkCredential UserCredentials = null, Viking.Common.IProgressReporter workerThread = null)
        {
            Uri uri = new Uri(path);

            if(workerThread != null)
                workerThread.ReportProgress(0, "Requesting " + path);

            XDocument XMLInitData;
            if (uri.Scheme == "http" || uri.Scheme == "https")
                XMLInitData = LoadHTTP(path, UserCredentials);
            else
                XMLInitData = LoadLocal(uri.LocalPath);

            return XMLInitData;
        }

        protected static string RemoveXMLExtension(string path)
        {
            //Remove the .xml file from the path
            int iRemove = path.LastIndexOf('/');
            string VolumePath = path;
            if (iRemove > 0)
            {
                VolumePath = VolumePath.Remove(iRemove);
            }

            return VolumePath;
        }
        

        protected static XDocument LoadHTTP(string path, System.Net.NetworkCredential UserCredentials)
        { 
            Uri pathURI = new Uri(path);

            HttpWebRequest request = WebRequest.Create(pathURI) as HttpWebRequest;
            if (pathURI.Scheme.ToLower() == "https")
                request.Credentials = UserCredentials;

            request.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.Revalidate);
             
            XDocument reader = null;
            try
            {
                using (WebResponse response = request.GetResponse())
                {

                    Stream responseStream = response.GetResponseStream();

                    using (StreamReader XMLStream = new StreamReader(responseStream))
                    {

                        reader = XDocument.Parse(XMLStream.ReadToEnd());
                    }
                }
            }
            catch (WebException e)
            {
                /*PORT: Don't have forms, throw a better exception*/
                throw new WebException("Error connecting to volume server: \n" + path + "\n" + e.Message, e);
            }
            
            return reader;
        }


        protected static XDocument LoadLocal(string path)
        {
            XDocument reader = null;
            using (FileStream f = File.OpenRead(path))
            {
                using (StreamReader XMLStreamReader = new StreamReader(f))
                {
                    string text = XMLStreamReader.ReadToEnd();
                    reader = XDocument.Parse(text);
                }
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
                                if (System.IO.Directory.Exists(LocalCachePath))
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
                Trace.WriteLine("Error connecting to volume server: \n" + StosZipPath + "\n" + e.Message, "VolumeModel");
                return false;
            }
            catch (Exception e)
            {
                Trace.WriteLine(string.Format("Could not open StosZip file: {0}", StosZipPath), "VolumeModel");
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
                    case "volumetoendpoint":
                        this.Endpoint = EndpointInformation.CreateFromElement(elem);
                        break;
                }
            }
        }

        private void LoadDefaultsFromVolumeElement(XElement volumeElement)
        {
            this.Name = IO.GetAttributeCaseInsensitive(volumeElement, "Name").Value;

            XAttribute defaulttileset = IO.GetAttributeCaseInsensitive(volumeElement, "defaulttileset");
            if (defaulttileset != null)
            {
                this.DefaultTileset = defaulttileset.Value;
            }

            XAttribute defaultimagepyramid = IO.GetAttributeCaseInsensitive(volumeElement, "defaultimagepyramid");
            if (defaultimagepyramid != null)
            {
                this.DefaultImagePyramid = defaultimagepyramid.Value;
            }

            XAttribute defaultmosaictransform = IO.GetAttributeCaseInsensitive(volumeElement, "defaultmosaictransform");
            if (defaultmosaictransform != null)
            {
                this.DefaultMosaicTransform = defaultmosaictransform.Value;
            }

            XAttribute defaultstosgroup = IO.GetAttributeCaseInsensitive(volumeElement, "defaultstosgroup");
            if (defaultstosgroup != null)
            {
                this.DefaultTileset = defaultstosgroup.Value;
            }

            XAttribute updateVolumePositions = IO.GetAttributeCaseInsensitive(volumeElement, "updateservervolumepositions");
            if (updateVolumePositions != null)
            {
                this.UpdateServerVolumePositions = Convert.ToBoolean(updateVolumePositions.Value);
            }

            XAttribute defaultsection = IO.GetAttributeCaseInsensitive(volumeElement, "defaultsection");
            if (defaultsection != null)
            {
                try
                {
                    this.DefaultSectionNumber = new int?(Convert.ToInt32(defaultsection.Value));
                }
                catch (FormatException)
                {
                    Trace.WriteLine("Unable to parse default section: " + defaultsection.Value);
                }
            }

            return;
        }

        /// <summary>
        /// Fetch the root <Volume> element from the XML
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        public static XElement GetVolumeElement(XDocument reader)
        {
            //Search for the correct node in the XML
            IEnumerable<XElement> VolumeElements = reader.Elements().Where(e => e.Name.LocalName == "Volume");
            if (VolumeElements.Count() == 0)
            {
                Trace.WriteLine("No volume node found in the XML", "VolumeModel");
                throw new InvalidDataException("No volume node found in the VikingXML");
            }

            return VolumeElements.First();
        }


        void Initialize(XDocument reader, Viking.Common.IProgressReporter workerThread)
        {
            List<CreateSectionThreadingObj> ListSectionThreadingObj = new List<CreateSectionThreadingObj>();
            List<CreateStosTransformThreadingObj> ListStosGridTransformThreadingObj = new List<CreateStosTransformThreadingObj>();
             
            //Fetch the volume information which should be the top level of the XML
            
            int NumStosFiles = System.Convert.ToInt32(IO.GetAttributeCaseInsensitive(VolumeElement, "num_stos").Value);
            int NumSections = System.Convert.ToInt32(IO.GetAttributeCaseInsensitive(VolumeElement, "num_sections").Value);

            XAttribute VolumePathAttrib = IO.GetAttributeCaseInsensitive(VolumeElement, "path");
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

            if (IO.GetAttributeCaseInsensitive(VolumeElement, "UniqueID") != null)
                this._UniqueID = IO.GetAttributeCaseInsensitive(VolumeElement, "UniqueID").Value;


            bool HaveStosZip = false;
            if (IO.GetAttributeCaseInsensitive(VolumeElement, "StosZip") != null)
            {
                string StosZipFileName = IO.GetAttributeCaseInsensitive(VolumeElement, "StosZip").Value;
                workerThread.ReportProgress(0, "Loading compressed transform file " + StosZipFileName);
                HaveStosZip = FetchStosZip(new Uri(Host + '/' + StosZipFileName), this.UserCredentials, this.Paths.ServerStosCachePath);
            }

            int countStos = 0;
            int countSections = 0;
            ListSectionThreadingObj.Capacity = NumSections;
            ListStosGridTransformThreadingObj.Capacity = NumStosFiles;

            LoadDefaultsFromXML(VolumeElement);

            foreach (XNode node in VolumeElement.Nodes().ToList<XNode>())
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
                        string StosFileName = IO.GetAttributeCaseInsensitive(elem, "path").Value;
                        Uri StosPath = new Uri(this.Host + System.IO.Path.DirectorySeparatorChar + StosFileName);

                        //      int pixelSpacing = System.Convert.ToInt32(GetAttributeCaseInsensitive(elem,"pixelSpacing").Value);
                        int ProgressPercent = (countStos * 100) / NumStosFiles;
                        countStos++;
                        workerThread.ReportProgress(ProgressPercent, "Loading " + StosPath);

                        CreateStosTransformThreadingObj CreateStosThreadObj = null;

                        if (HaveStosZip)
                        {
                            String StosFileCacheFullPath = System.IO.Path.Combine(this.Paths.ServerStosCachePath, StosFileName);
                            if (System.IO.File.Exists(StosFileCacheFullPath))
                            {
                                CreateStosThreadObj = new CreateStosTransformThreadingObj(StosFileCacheFullPath, elem, Paths.StosCacheDir);
                            }
                        }

                        //Load from server if it is not in the zip
                        if (CreateStosThreadObj == null)
                        {
                            //    Trace.WriteLine("Loading " + StosFileName + " from HTTP Server", "VolumeModel");
                            CreateStosThreadObj = new CreateStosTransformThreadingObj(StosPath, this.UserCredentials, elem, Paths.StosCacheDir);
                        }

                        ListStosGridTransformThreadingObj.Add(CreateStosThreadObj);
                        System.Threading.ThreadPool.QueueUserWorkItem(CreateStosThreadObj.ThreadPoolCallback);

                        break;
                    case "section":
                        //string SectionPath = VolumePath + '/' + GetAttributeCaseInsensitive(elem,"path").Value;
                        string SectionPath = elem.HasAttributeCaseInsensitive("path") ? IO.GetAttributeCaseInsensitive(elem, "path").Value : "";

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
                    case "ocptileserver":
                        TileServerInfo info = TileServerInfo.CreateFromElement(elem);
                        this.TileServerList.Add(info);
                        break;

                    case "scale":
                        this._DefaultXYScale = VikingXMLUtils.ParseScale(elem);
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

                            int pixelSpacing = System.Convert.ToInt32(IO.GetAttributeCaseInsensitive(elem, "pixelSpacing").Value);
                            string type = IO.GetAttributeCaseInsensitive(elem, "type").Value;
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

                            if (this.DefaultVolumeTransform == null || this.DefaultVolumeTransform == "None")
                                this.DefaultVolumeTransform = groupName;

                            if (CreateStosGridTransformObj.stosTransform != null)
                            {
                                //IContinuousTransform stosTransform = EnsureTransformIsContinuous(CreateStosGridTransformObj.stosTransform);
                                ITransform stosTransform = CreateStosGridTransformObj.stosTransform;
                                StosTransformInfo info = (stosTransform as ITransformInfo)?.Info as StosTransformInfo;
                                SortedList<int, ITransform> transformDict = null;
                                if (this.Transforms.ContainsKey(groupName))
                                {
                                    transformDict = this.Transforms[groupName];
                                }
                                else
                                {
                                    transformDict = new SortedList<int, ITransform>();
                                    Transforms.Add(groupName, transformDict);
                                }

                                if (transformDict.ContainsKey(info.MappedSection))
                                {
                                    Console.WriteLine("Volume stos mapping already contains " + info.ToString());
                                }
                                else
                                {
                                    transformDict.Add(info.MappedSection, stosTransform);
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

       
        

        private IContinuousTransform EnsureTransformIsContinuous(ITransform transform)
        {
            if(transform as IContinuousTransform == null)
            {
                Geometry.Transforms.StosTransformInfo info = ((ITransformInfo)transform).Info as Geometry.Transforms.StosTransformInfo;
                string SerializerCacheFullPath = System.IO.Path.Combine(this.Paths.StosCacheDir, info.GetCacheFilename(".stos_bin")); 
                return Serialization.LoadOrCreateContinuousTransform(SerializerCacheFullPath, transform as IDiscreteTransform);
            }

            return transform as IContinuousTransform;
        }

        private void WaitForCreateSectionThreads(List<CreateSectionThreadingObj> ListSectionThreadingObj, Viking.Common.IProgressReporter workerThread)
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
            foreach (string name in section.ChannelNames)
            {
                this.AddChannel(name);
            }

            this.AddTileServerToSectionMappings(section);

            if (this.DefaultTileset != null)
            {
                if (section.ChannelNames.Contains(DefaultTileset))
                {
                    section.DefaultTileset = DefaultTileset;
                }
            }

            this.Sections.Add(section.Number, section);
        }

        private void AddTileServerToSectionMappings(Section section)
        {
            foreach (TileServerInfo tileserver in this.TileServerList)
            {
                section.AddOCPTileserver(tileserver);
            }
        }


        private static ITransform LoadSerializedTransformFromCache(string CacheStosPath, StosTransformInfo ControlToVolumeInfo, StosTransformInfo SectionToControlInfo)
        {
            ITransform cachedTransform = null;

            try
            {
                if (Geometry.Global.IsCacheFileValid(CacheStosPath, new DateTime[] { ControlToVolumeInfo.LastModified, SectionToControlInfo.LastModified }))
                {
                    string outString = "Loading from binary cache: " + SectionToControlInfo.MappedSection + " to " + ControlToVolumeInfo.ControlSection.ToString();
                    Trace.WriteLine(outString);
                    using (Stream binFile = System.IO.File.OpenRead(CacheStosPath))
                    {
                        var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                        cachedTransform = binaryFormatter.Deserialize(binFile) as ITransform;
                    }
                }
                else
                {
                    Geometry.Global.TryDeleteCacheFile(CacheStosPath);
                }
            }
            catch (Exception)
            {
                Trace.WriteLine(string.Format("Exception loading {0}, deleting", CacheStosPath));
                Geometry.Global.TryDeleteCacheFile(CacheStosPath);

                return null;
            }

            return cachedTransform;
        }

        private static IContinuousTransform LoadStosFromCache(string CacheStosPath, StosTransformInfo ControlToVolumeInfo, StosTransformInfo SectionToControlInfo)
        {
            IDiscreteTransform cachedTransform = null;
            DiscreteTransformWithContinuousFallback continuousTransform = null;
            try
            {
                if (Geometry.Global.IsCacheFileValid(CacheStosPath, new DateTime[] { ControlToVolumeInfo.LastModified, SectionToControlInfo.LastModified }))
                {
                    string outString = "Loading from ITK string cache: " + SectionToControlInfo.MappedSection + " to " + ControlToVolumeInfo.ControlSection.ToString();
                    Trace.WriteLine(outString);
                    DateTime CacheLastModifiedUtc = System.IO.File.GetLastWriteTimeUtc(CacheStosPath);
                    StosTransformInfo stosInfo = new StosTransformInfo(ControlToVolumeInfo.ControlSection, SectionToControlInfo.MappedSection, CacheLastModifiedUtc);
                    using (Stream stostext = System.IO.File.OpenRead(CacheStosPath) as Stream)
                    {
                        cachedTransform = TransformFactory.ParseStos(stostext,
                                                                        stosInfo,
                                                                            1) as TriangulationTransform;

                        continuousTransform = new DiscreteTransformWithContinuousFallback(cachedTransform,
                                                                                            new RBFTransform(((ITransformControlPoints)cachedTransform).MapPoints, stosInfo),
                                                                                            stosInfo);
                    }
                }
                else
                {
                    Geometry.Global.TryDeleteCacheFile(CacheStosPath);
                }
            }
            catch (Exception)
            {
                Trace.WriteLine(string.Format("Exception loading {0}, deleting", CacheStosPath));
                Geometry.Global.TryDeleteCacheFile(CacheStosPath);

                return null;
            }

            return continuousTransform;
        }

        private static void SaveSerializedTransformToCache(string CacheStosPath, object itkTransform)
        {
            using (Stream binFile = System.IO.File.OpenWrite(CacheStosPath))
            {
                var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                binaryFormatter.Serialize(binFile, itkTransform);
            }
        }

        private static void SaveStosToCache(string CacheStosPath, IITKSerialization itkTransform, StosTransformInfo ControlToVolumeInfo, StosTransformInfo SectionToControlInfo)
        {
            using (StreamWriter fs = System.IO.File.CreateText(CacheStosPath))
            {
                fs.WriteLine(ControlToVolumeInfo.ToString());
                fs.WriteLine(SectionToControlInfo.ToString());

                itkTransform.WriteITKTransform(fs);
            }
        }
         

        /// <summary>
        /// Adds a transform to each section mapping it into each of the volume spaces we found
        /// </summary>
        public void CreateVolumeTransforms(Viking.Common.IProgressReporter workerThread)
        {
            int iSectionProgress = 0;
            foreach (string TransformKey in Transforms.Keys)
            {
                //The transform list is sorted by which section the transform maps from. 
                //Next we'll add transfroms so every transform maps from the mapped section to section #1
                SortedList<int, ITransform> TList = Transforms[TransformKey];

                //Create a registration chain so we know what order to register the sections in
                RegistrationTree tree = RegistrationTree.Build(TList, Sections.Keys);

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

                        ITransform ControlTrans = null;
                        IContinuousTransform ContinuousControlTransform = null;

                        //Find the section that can map our transform
                        if (TList.ContainsKey(ControlNode.SectionNumber))
                        {
                            //string outString = "Loading continuous transform for control section: " + ControlSection.ToString();
                            //workerThread.ReportProgress((iSectionProgress * 100) / TList.Count, outString);
                            ControlTrans = TList[ControlNode.SectionNumber];
                        }

                        foreach (int childSection in ControlNode.Children)
                        {
                            iSectionProgress++;
                            ITransform trans = TList[childSection];
                            StosTransformInfo info = ((ITransformInfo)trans)?.Info as StosTransformInfo;
                            if (info == null)
                                continue;

                            if (false == Sections.ContainsKey(info.MappedSection))
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
                                StosTransformInfo ControlInfo = ((ITransformInfo)ControlTrans)?.Info as StosTransformInfo;
                                TransformInfo transformInfo = ((ITransformInfo)trans)?.Info;
                                string CacheStosPath = Paths.GetITKSCacheName(info.MappedSection, ControlInfo.ControlSection);
                                string CacheSerializedPath = Paths.GetSerializerCacheName(info.MappedSection, ControlInfo.ControlSection);
                                //TList[childSection] = LoadStosFromCache(CacheStosPath, ControlInfo, info);
                                TList[childSection] = LoadSerializedTransformFromCache(CacheSerializedPath, ControlInfo, info);

                                //CalculateSliceToVolume = true; 
                                if (TList[childSection] == null)
                                {
                                    if(ContinuousControlTransform == null)
                                    {
                                        //This line creating continuous transforms can be slow.
                                        ContinuousControlTransform = EnsureTransformIsContinuous(ControlTrans);
                                        //Replace the discreet transform with the continuous version for future use
                                        TList[ControlNode.SectionNumber] = ContinuousControlTransform;
                                    }

                                    try
                                    {
                                        string outString = "Adding transforms: " + trans.ToString() + " to " + ControlTrans.ToString();
                                        workerThread.ReportProgress((iSectionProgress * 100) / TList.Count, outString);

                                        TList[childSection] = ContinuousControlTransform.TransformTransform((trans as ITransformControlPoints), trans.GetType());

                                        //(ContinuousControlTransform as IMemoryMinimization)?.MinimizeMemory();
                                        /*
                                        TList[childSection] = TriangulationTransform.Transform(ControlTrans,
                                                                                               trans,
                                                                                               new StosTransformInfo(ControlInfo.ControlSection, info.MappedSection,
                                                                                               StosTransformInfo.Merge(ControlInfo, transformInfo)));
                                                                                               */
                                    }
                                    catch (Exception)
                                    {
                                        Trace.WriteLine(string.Format("Exception adding transforms {0} to {1}", trans.ToString(), ControlTrans.ToString()));
                                        trans = TList[childSection];
                                    }
                                    
                                    IITKSerialization itkTransform = TList[childSection] as IITKSerialization;
                                    if (itkTransform != null)
                                    {
                                        SaveSerializedTransformToCache(CacheSerializedPath, itkTransform);
                                        SaveStosToCache(CacheStosPath, itkTransform, ControlInfo, info);
                                    }
                                }
                                else
                                {
                                    string outString = "Loading transforms from Cache: " + trans.ToString() + " to " + ControlTrans.ToString();
                                    workerThread.ReportProgress((iSectionProgress * 100) / TList.Count, outString);
                                }
                            }

                            SafeNodes.Enqueue(childSection);
                        }
                    }
                }
            }
        }
    }
}
