using Geometry;
using Geometry.Transforms;
//using System.IO.Compression;
using System.IO.Compression;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using UnitsAndScale;
using Utils;
using VolumeModel;
using Viking.Common;

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

    public class EndpointInformation(string Authentication, string Endpoint, string exportURL)
    {
        public readonly Uri AuthenticationURL = new(Authentication);
        public readonly Uri EndpointURL = new(Endpoint);
        public readonly Uri ExportURL = exportURL is null ? null : new Uri(exportURL);

        internal static EndpointInformation CreateFromElement(XElement elem)
        {
            return new EndpointInformation(elem.GetAttributeCaseInsensitive("authentication").Value,
                                           elem.GetAttributeCaseInsensitive("endpoint").Value,
                                           elem.GetAttributeCaseInsensitive("exporturl")?.Value);
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
            TileServerInfo info = new()
            {
                TileXDim = System.Convert.ToInt32(node.GetAttributeCaseInsensitive("TileXDim").Value),
                TileYDim = System.Convert.ToInt32(node.GetAttributeCaseInsensitive("TileYDim").Value),
                GridXDim = System.Convert.ToInt32(node.GetAttributeCaseInsensitive("GridXDim").Value),
                GridYDim = System.Convert.ToInt32(node.GetAttributeCaseInsensitive("GridYDim").Value),
                MaxLevel = System.Convert.ToInt32(node.GetAttributeCaseInsensitive("MaxLevel").Value),
                FilePrefix = node.GetAttributeCaseInsensitive("FilePrefix").Value,
                FilePostfix = node.GetAttributeCaseInsensitive("FilePostfix").Value,
                Host = node.GetAttributeCaseInsensitive("host").Value,
                CoordSpaceName = node.GetAttributeCaseInsensitive("coordspacename").Value,

                Channels = [.. node.Elements().Where(e => e.Name == "Channel").Select(e => new OCPChannelInfo(e))]
            };
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
        public string UniqueID => _UniqueID;

        /// <summary>
        /// Set to true if the volume is located on the local drive
        /// False if over a network
        /// </summary>
        private readonly bool _IsLocal;
        public bool IsLocal => _IsLocal;

        /// <summary>
        /// Credentials to use during web requests
        /// </summary>
        public System.Net.NetworkCredential UserCredentials = new("anonymous", "connectome");

        private readonly XElement _VolumeElement;

        /// <summary>
        /// The XML document used to initialize the volume.  Contains all configuration settings from the server.
        /// </summary>
        public XElement VolumeElement => _VolumeElement;

        /// <summary>
        /// Names of transform groups that can be used to register images into the volume
        /// </summary>
        public List<string> VolumeTransformNames = ["None"];

        private readonly Dictionary<int, int> SectionToReferenceSectionBelow = [];

        /// <summary>
        /// Specified during loading, if the <DefaultTileset> element exists we assign all sections containing that tileset to use it as the default transform
        /// </summary>
        private string DefaultTileset = null;

        /// <summary>
        /// The server the volume transforms and image data is located on
        /// </summary>
        private string _Host;
        public string Host => _Host;


        /// <summary>
        /// The path we use to cache data on the local drive
        /// </summary>
        internal readonly VolumePaths Paths;

        /// <summary>
        /// The local directory where volume-specific data is cached
        /// </summary>
        public string LocalVolumeDir => Paths?.LocalVolumeDir;

        /// <summary>
        /// Maps a section number to its section object
        /// </summary>
        public SortedList<int, Section> Sections = [];

        private long _Initialized = 0;
        /// <summary>
        /// Set to true if the Initialize() method has previously completed for this instance
        /// </summary>
        public bool IsInitialized => Interlocked.Read(ref _Initialized) > 0;

        /// <summary>
        /// Sorted list containing the transforms for each volume transform we find
        /// Key = Downsample level
        /// Value = Dictionary mapping each section number to a stos transform.  This is because section numbers may not be continuos
        /// </summary>
        public SortedList<string, SortedList<int, ITransform>> Transforms = [];

        public int NumSections => Sections.Count;

        private IAxisUnits _DefaultXYScale;

        public IAxisUnits DefaultXYScale => _DefaultXYScale;

        /// <summary>
        /// This task is set to completed when the volume is initialized.
        /// </summary>
        public Task InitializationTask { get; private set; }

        /// <summary>
        /// Returns the section that the passed section was registered to
        /// </summary>
        /// <param name="?"></param>
        /// <returns></returns>
        public Section GetReferenceSectionBelow(Section section)
        {
            if (section is null)
                return null;

            //Optimistic implementation that looks at section immediately above
            int refnumber = section.Number - 1;
            int minSectionNumber = Sections.Keys.Min();
            while (refnumber >= minSectionNumber)
            {
                if (Sections.TryGetValue(refnumber, out var below))
                    return below;
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
            if (section is null)
                return null;

            //Optimistic implementation that looks at section immediately above
            int refnumber = section.Number + 1;
            int maxSectionNumber = Sections.Keys.Max();
            while (refnumber <= maxSectionNumber)
            {
                if (Sections.TryGetValue(refnumber, out var above))
                    return above;
                refnumber++;
            }

            return null;
        }

        private readonly List<TileServerInfo> TileServerList = [];

        /// <summary>
        /// Tile pixel width from the first tile server, or null if no tile servers are configured.
        /// Used to compute the max concurrent texture request limit.
        /// </summary>
        public int? DefaultTileWidth => TileServerList.Count > 0 ? TileServerList[0].TileXDim : null;

        private XDocument VolumeXML;

        /// <summary>
        /// Loads the .xml file describing the volume and populates the name, number of sections, and other top-level information.
        /// Does not load details for each section or each transform until Volume.Initialize() is called.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="localCachePath"></param>
        /// <param name="workerThread"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="TaskCanceledException"></exception>
        public static async Task<Volume> CreateAsync(string path, string localCachePath, IProgress<ProgressInfo> workerThread, CancellationToken token)
        {
            if (path is null)
                throw new ArgumentNullException(nameof(path));

            var document = await LoadXDocumentAsync(path, token, null, workerThread).ConfigureAwait(false);
            Volume output = new(path, localCachePath, document);

            if (token.IsCancellationRequested)
                throw new TaskCanceledException();

            return output;
        }

        private Volume(string path, string localCachePath, XDocument VolumeXML)
        {
            DefaultChannels = [];

            if (IsVolumePathLocal(path))
            {
                //This code remains, but the value is replaced if a value is found in the XML file
                this._Host = RemoveXMLExtension(path);
                this._IsLocal = false;
            }

            this._Host = path;
            this._VolumeElement = GetVolumeElement(VolumeXML);
            LoadDefaultsFromVolumeElement(_VolumeElement);
            LoadDefaultsFromXML(_VolumeElement);
            this.Paths = new VolumePaths(localCachePath, this.Name);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path">The host and path to the volume, no filenames</param>
        /// <param name="localCachePath">LocaL cache path corresponding to the path</param>
        /// <param name="workerThread">optional worker thread to report progress</param>
        public Volume(string path, string localCachePath, XDocument VolumeXML, IProgress<ProgressInfo> workerThread)
        {
            //Load the default settings from user preferences
            //ChannelInfo DefaultChannel = new ChannelInfo();
            DefaultChannels = [];

            this._Host = path;
            this._VolumeElement = GetVolumeElement(VolumeXML);
            LoadDefaultsFromVolumeElement(_VolumeElement);
            LoadDefaultsFromXML(_VolumeElement);

            this.Paths = new VolumePaths(localCachePath, this.Name);

            //Initialize(workerThread);
        }


        #region Channels

        private ChannelInfo[] _DefaultChannels = [];

        public ChannelInfo[] DefaultChannels
        {
            get => _DefaultChannels;
            set
            {
                if (null == value)
                {
                    _DefaultChannels = [];
                    return;
                }

                _DefaultChannels = value;
            }
        }

        //A list of all channel names found in the volume
        //TODO: Modify to a per section basis?
        private static readonly List<string> _ChannelNames = [];

        /// <summary>
        /// A list of all channel names found on sections in the volume
        /// </summary>
        public static string[] ChannelNames => [.. _ChannelNames];

        private static void AddChannel(string name)
        {
            //TODO: This needs a more thorough fix.  Sections are created on threads and they race to add entries to this list.
            //We should import all sections and then build the list from the results
            if (false == _ChannelNames.Contains(name))
            {
                _ChannelNames.Add(name);
                _ChannelNames.Sort();
            }
        }

        public static void RemoveChannel(string name)
        {
            if (false == _ChannelNames.Contains(name))
                return;

            _ChannelNames.Remove(name);
        }

        #endregion

        public static bool IsVolumePathLocal(string path)
        {
            Uri uri = new(path);
            if (uri.Scheme == "http" || uri.Scheme == "https")
                return false;

            return true;
        }

        /*
        /// <summary>
        /// Loads a path, determines whether path refers to XML file or a local directory
        /// </summary>
        /// <param name="path"></param>
        public static XDocument LoadXDocument(string path, System.Net.NetworkCredential UserCredentials = null, Viking.Common.IProgressReporter workerThread = null)
        {
            if (path is null)
                throw new ArgumentNullException(nameof(path));
            Uri uri = new Uri(path);

            workerThread?.Report(0, $"Requesting {path}");

            XDocument XMLInitData;
            if (uri.Scheme == "http" || uri.Scheme == "https")
                XMLInitData = LoadHttp(path, UserCredentials);
            else
                XMLInitData = LoadLocal(uri.LocalPath);

            return XMLInitData;
        }
        */

        /// <summary>
        /// Loads a path, determines whether path refers to XML file or a local directory
        /// </summary>
        /// <param name="path"></param>
        public static Task<XDocument> LoadXDocumentAsync(string path, CancellationToken token, System.Net.NetworkCredential UserCredentials = null, IProgress<ProgressInfo> workerThread = null)
        {
            if (path is null)
                throw new ArgumentNullException(nameof(path));
            Uri uri = new(path);

            workerThread?.Report(new ProgressInfo($"Requesting {path}", 0, 100));

            XDocument XMLInitData;
            if (uri.Scheme == "http" || uri.Scheme == "https")
                return LoadHTTPAsync(path, UserCredentials, token);
            else
                return LoadLocalAsync(uri.LocalPath, token);
        }

        static string RemoveXMLExtension(string path)
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

        private const int VolumeXmlRequestTimeoutSeconds = 60;
        private const int VolumeXmlMaxRetries = 3;

        protected static async Task<XDocument> LoadHTTPAsync(string path, System.Net.NetworkCredential UserCredentials, CancellationToken token)
        {
            Uri pathURI = new(path);

            HttpClientHandler handler = pathURI.Scheme.ToLower() == "https" && UserCredentials != null
                ? new HttpClientHandler
                {
                    Credentials = UserCredentials
                }
                : new HttpClientHandler
                {
                    UseDefaultCredentials = true
                };

            Exception lastException = null;
            for (int attempt = 0; attempt <= VolumeXmlMaxRetries; attempt++)
            {
                if (token.IsCancellationRequested)
                    throw new TaskCanceledException("LoadHttpAsync cancelled by token");

                if (attempt > 0)
                {
                    Trace.WriteLine($"Volume XML load retry {attempt}/{VolumeXmlMaxRetries} for {path}");
                    await Task.Delay(1000 * attempt, token).ConfigureAwait(false);
                }

                using HttpClient httpClient = new(handler);
                httpClient.Timeout = TimeSpan.FromSeconds(VolumeXmlRequestTimeoutSeconds);
                try
                {
                    var response = await httpClient.GetAsync(pathURI, token).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();

                    if (token.IsCancellationRequested)
                        throw new TaskCanceledException("LoadHttpAsync cancelled by token");

                    var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (token.IsCancellationRequested)
                        throw new TaskCanceledException("LoadHttpAsync cancelled by token");

                    return XDocument.Parse(content);
                }
                catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
                {
                    lastException = e;
                    if (attempt == VolumeXmlMaxRetries)
                        throw new WebException($"Error connecting to volume server after {VolumeXmlMaxRetries + 1} attempts: \n{path}\n{e.Message}", e);
                }
            }

            if (lastException != null)
                throw new WebException($"Error connecting to volume server: \n{path}\n{lastException.Message}", lastException);
            throw new WebException($"Error connecting to volume server: \n{path}", null);
        }

        protected static XDocument LoadLocal(string path)
        {
            XDocument reader = null;
            using FileStream f = File.OpenRead(path);
            using StreamReader XMLStreamReader = new(f);
            string text = XMLStreamReader.ReadToEnd();
            return XDocument.Parse(text);
        }


        protected static async Task<XDocument> LoadLocalAsync(string path, CancellationToken token)
        {
            XDocument reader = null;
            using FileStream f = File.OpenRead(path);
            using StreamReader XMLStreamReader = new(f);
            string text = await XMLStreamReader.ReadToEndAsync().ConfigureAwait(false);
            if (token.IsCancellationRequested)
                throw new TaskCanceledException("LoadLocalAsync cancelled by token");
            return XDocument.Parse(text);
        }

        private static async Task<bool> FetchStosZip(Uri StosZipPath, System.Net.NetworkCredential UserCredentials, string LocalCachePath)
        {
            HttpClient request = new()
            {
                BaseAddress = StosZipPath
            };

            //HttpWebRequest request = WebRequest.Create(StosZipPath) as HttpWebRequest;
            //if (StosZipPath.Scheme.ToLower() == "https")
            //request.Credentials = UserCredentials;


            //request.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.Revalidate);

            try
            {
                using Stream responseStream = await request.GetStreamAsync(StosZipPath).ConfigureAwait(false);
                /*
                    Byte[] buffer = responseStream.ReadToBuffer(responseStream.Length);
                    using (MemoryStream memStream = new MemoryStream(buffer))
                    */
                using ZipArchive archive = new(responseStream, ZipArchiveMode.Read);
                if (false == System.IO.Directory.Exists(LocalCachePath))
                    archive.ExtractToDirectory(LocalCachePath);
                else
                {

                    foreach (var entry in archive.Entries)
                    {
                        var entryWriteTimeUTC = entry.LastWriteTime.DateTime.ToUniversalTime();
                        var expectedCachePath = System.IO.Path.Combine(LocalCachePath, entry.FullName);
                        FileInfo info = new(expectedCachePath);
                        if (info.Exists == false)
                        {
                            entry.ExtractToFile(expectedCachePath);
                        }
                        else if (info.LastWriteTimeUtc < entryWriteTimeUTC)
                        {
                            System.IO.File.Delete(expectedCachePath);
                            entry.ExtractToFile(expectedCachePath);
                        }
                    }
                }
            }
            catch (WebException e)
            {
                Trace.WriteLine($"Error connecting to volume server: \n{StosZipPath}\n{e.Message}", "VolumeModel");
                return false;
            }
            catch (Exception e)
            {
                Trace.WriteLine($"Could not open StosZip file: {StosZipPath}", "VolumeModel");
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
            foreach (XNode node in volumeElement.Nodes().Where(n => n.NodeType == System.Xml.XmlNodeType.Element).ToList<XNode>())
            {
                if (node is not XElement elem)
                    continue;

                //Fetch the name if we know it
                switch (elem.Name.LocalName.ToLower())
                {
                    case "defaulttileset":
                        this.DefaultTileset = elem.GetAttributeCaseInsensitive("name").Value;
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
            this.Name = volumeElement.GetAttributeCaseInsensitive("Name").Value;

            try
            {
                this.DefaultTileset = volumeElement.GetAttributeCaseInsensitive("defaulttileset").Value;
            }
            catch (XMLMissingDataException e) { Trace.WriteLine($"Optional {e}"); }

            try
            {
                this.DefaultImagePyramid = volumeElement.GetAttributeCaseInsensitive("defaultimagepyramid").Value;

            }
            catch (XMLMissingDataException e) { Trace.WriteLine($"Optional {e}"); }

            try
            {
                this.DefaultMosaicTransform = volumeElement.GetAttributeCaseInsensitive("defaultmosaictransform").Value;
            }
            catch (XMLMissingDataException e) { Trace.WriteLine($"Optional {e}"); }

            try
            {
                XAttribute defaultstosgroup = volumeElement.GetAttributeCaseInsensitive("defaultstosgroup");
                this.DefaultStosGroup = defaultstosgroup.Value;
            }
            catch (XMLMissingDataException e) { Trace.WriteLine($"Optional {e}"); }

            try
            {
                XAttribute updateVolumePositions = volumeElement.GetAttributeCaseInsensitive("updateservervolumepositions");
                this.UpdateServerVolumePositions = Convert.ToBoolean(updateVolumePositions.Value);
            }
            catch (XMLMissingDataException e) { Trace.WriteLine($"Optional {e}"); }

            try
            {
                XAttribute defaultsection = volumeElement.GetAttributeCaseInsensitive("defaultsection");
                if (defaultsection != null)
                {
                    try
                    {
                        this.DefaultSectionNumber = new int?(Convert.ToInt32(defaultsection.Value));
                    }
                    catch (FormatException)
                    {
                        Trace.WriteLine($"Unable to parse default section: {defaultsection.Value}");
                    }
                }
            }
            catch (XMLMissingDataException e) { Trace.WriteLine($"Optional {e}"); }

            XAttribute VolumePathAttrib = volumeElement.GetAttributeCaseInsensitive("path");
            if (VolumePathAttrib != null)
                this._Host = VolumePathAttrib.Value;
            else
            {
                /* PORT
                System.Windows.Forms.MessageBox.Show("Could locate path attribute for volume.  Chances are the XML definitation for this volume has not been updated. Contact administrator to update the VikingXML file.", "Error", System.Windows.Forms.MessageBoxButtons.OK);
                if (this._Host is null) //If we don't know a path throw an exception to kill the process
                    throw new ArgumentException("Could locate path attribute for volume.  Chances are the XML definitation for this volume has not been updated. Contact administrator to update the VikingXML file.");
                 */
            }

            //Remove a trailing slash
            if (this._Host.EndsWith("/"))
                this._Host = this._Host.TrimEnd('/');

            try
            {
                if (volumeElement.GetAttributeCaseInsensitive("UniqueID") != null)
                    this._UniqueID = volumeElement.GetAttributeCaseInsensitive("UniqueID").Value;
            }
            catch (XMLMissingDataException e) { Trace.WriteLine($"Optional {e}"); }

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

        /// <summary>
        /// Only allow one initialization at a time
        /// </summary>
        private readonly SemaphoreSlim InitializeLock = new(1);
        public async Task Initialize(CancellationToken token, IProgress<ProgressInfo> workerThread = null)
        {

            if (IsInitialized)
                return;

            try
            {
                await InitializeLock.WaitAsync(token).ConfigureAwait(false);
                if (IsInitialized || token.IsCancellationRequested)
                    return;

                XDocument reader = this.VolumeXML;

                int NumStosFiles = System.Convert.ToInt32(VolumeElement.GetAttributeCaseInsensitive("num_stos").Value);
                int NumSections = System.Convert.ToInt32(VolumeElement.GetAttributeCaseInsensitive("num_sections").Value);

                List<Task<Section>> ListSectionLoadingTasks = new(NumSections);
                List<Task<LoadStosResult>> ListStosLoadingTasks = new(NumStosFiles);

                bool HaveStosZip = false;
                try
                {
                    if (VolumeElement.HasAttributeCaseInsensitive("StosZip") != null)
                    {
                        string StosZipFileName = VolumeElement.GetAttributeCaseInsensitive("StosZip").Value;
                        workerThread?.Report(new ProgressInfo($"Loading compressed transform file {StosZipFileName}", 0));
                        HaveStosZip = await FetchStosZip(new Uri($"{Host}/{StosZipFileName}"), this.UserCredentials, this.Paths.ServerStosCachePath).ConfigureAwait(false);
                    }
                }
                catch (XMLMissingDataException e)
                {
                    Trace.WriteLine($"Optional {e.Message}");
                }
                catch (Exception e)
                {
                    Trace.WriteLine($"FetchStosZip failed, falling back to normal STOS loading: {e.Message}");
                    HaveStosZip = false;
                }

                int countStos = 0;
                int countSections = 0;

                //var stosFactory = new TaskFactory<LoadStosResult>(TaskCreationOptions.PreferFairness, TaskContinuationOptions.NotOnCanceled | TaskContinuationOptions.NotOnFaulted);
                //var sectionFactory = new TaskFactory<Section>(TaskCreationOptions.PreferFairness, TaskContinuationOptions.NotOnCanceled | TaskContinuationOptions.NotOnFaulted);

                //LoadDefaultsFromXML(VolumeElement);

                foreach (XNode node in VolumeElement.Nodes().ToList<XNode>())
                {
                    if (node.NodeType == System.Xml.XmlNodeType.Whitespace)
                        continue;

                    if (node is not XElement elem)
                        continue;

                    //Fetch the name if we know it
                    switch (elem.Name.LocalName.ToLower())
                    {
                        case "stos":

                            string stosFileName = elem.GetAttributeCaseInsensitive("path").Value;
                            Uri stosPath = new(this.Host + System.IO.Path.DirectorySeparatorChar + stosFileName);
                            //      int pixelSpacing = System.Convert.ToInt32(GetAttributeCaseInsensitive(elem,"pixelSpacing").Value);
                            int ProgressPercent = (countStos * 100) / NumStosFiles;
                            countStos++;
                            workerThread?.Report(new ProgressInfo($"Loading {stosFileName}", ProgressPercent));

                            ListStosLoadingTasks.Add(LoadStos(elem, HaveStosZip));

                            break;
                        case "section":
                            //string SectionPath = VolumePath + '/' + GetAttributeCaseInsensitive(elem,"path").Value;
                            string SectionPath = elem.HasAttributeCaseInsensitive("path") ? elem.GetAttributeCaseInsensitive("path").Value : "";

                            ProgressPercent = NumSections > 0 ? (countSections * 100) / NumSections : 100;

                            countSections++;
                            workerThread?.Report(new ProgressInfo($"Queueing {SectionPath}", ProgressPercent));

                            Section newSection = new(this, SectionPath, elem);
                            var task = newSection.InitializeFromXML(elem, token);
                            ListSectionLoadingTasks.Add(task);
                            //await task;
                            break;
                        case "ocptileserver":
                            TileServerInfo info = TileServerInfo.CreateFromElement(elem);
                            this.TileServerList.Add(info);
                            break;

                        case "scale":
                            this._DefaultXYScale = elem.ParseScale();
                            break;

                        default:
                            break;
                    }
                }

                await WaitForCreateSectionThreads(ListSectionLoadingTasks, workerThread, token).ConfigureAwait(false);

                await WaitForLoadStosTransformThreads(ListStosLoadingTasks, workerThread, token).ConfigureAwait(false);

                CreateVolumeTransforms(workerThread);

                workerThread?.Report(new ProgressInfo("Done!", 100, 100));

                Interlocked.Exchange(ref _Initialized, 1);
            }
            finally
            {
                VolumeXML = null;
                InitializeLock.Release();
            }
        }

        private async Task<LoadStosResult> LoadStos(XElement elem, bool HaveStosCache)
        {
            LoadStosResult result = null;
            string stosFileName = elem.GetAttributeCaseInsensitive("path").Value;
            Uri stosPath = new(this.Host + System.IO.Path.DirectorySeparatorChar + stosFileName);

            try
            {
                if (HaveStosCache)
                {
                    var stosFileCacheFullPath = System.IO.Path.Combine(this.Paths.ServerStosCachePath, stosFileName);
                    if (System.IO.File.Exists(stosFileCacheFullPath))
                    {
                        try
                        {
                            result = await LoadStosResult.LoadAsync(stosFileCacheFullPath, elem).ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Exception loading {stosFileCacheFullPath}.\n{e?.InnerException}");
                            Trace.WriteLine($"Exception loading {stosFileCacheFullPath}.\n{e?.InnerException}");
                            throw;
                        }
                    }
                }

                //Load from server if it is not in the zip
                if (result is null)
                {
                    //    Trace.WriteLine("Loading " + StosFileName + " from HTTP Server", "VolumeModel");
                    try
                    {
                        result = await LoadStosResult.LoadAsync(stosPath, this.UserCredentials, elem).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Exception loading {stosPath}.\n{e?.InnerException}");
                        Trace.WriteLine($"Exception loading {stosPath}.\n{e?.InnerException}");
                        throw;
                    }
                }
            }
            finally
            {
            }

            if (result != null)
            {
                try
                {
                    await OnStosTransformLoadComplete(result.Transform, result.element).ConfigureAwait(false);
                }
                finally
                {
                }
            }

            return result;
        }

        private readonly SemaphoreSlim StosTransformLoadSemaphore = new(1);
        private async Task OnStosTransformLoadComplete(ITransform Transform, XElement element)
        {
            try
            {
                await StosTransformLoadSemaphore.WaitAsync().ConfigureAwait(false);
                int pixelSpacing =
                    System.Convert.ToInt32(element.GetAttributeCaseInsensitive("pixelSpacing").Value);
                string type = element.GetAttributeCaseInsensitive("type").Value;
                string groupName = $"{type} {pixelSpacing}";

                XAttribute GroupNameAttribute = element.Attribute("GroupName");
                if (GroupNameAttribute != null)
                {
                    groupName = GroupNameAttribute.Value;
                }

                if (false == VolumeTransformNames.Contains(groupName))
                {
                    VolumeTransformNames.Add(groupName);
                }

                if (this.DefaultVolumeTransform is null || this.DefaultVolumeTransform == "None")
                    this.DefaultVolumeTransform = groupName;

                if (Transform != null)
                {
                    //IContinuousTransform stosTransform = EnsureTransformIsContinuous(CreateStosGridTransformObj.stosTransform);
                    ITransform stosTransform = Transform;
                    StosTransformInfo info = (stosTransform as ITransformInfo)?.Info as StosTransformInfo;
                    SortedList<int, ITransform> transformDict = null;
                    if (this.Transforms.TryGetValue(groupName, out var transform))
                    {
                        transformDict = transform;
                    }
                    else
                    {
                        transformDict = [];
                        Transforms.Add(groupName, transformDict);
                    }

                    if (transformDict.ContainsKey(info.MappedSection))
                    {
                        Console.WriteLine($"Volume stos mapping already contains {info}");
                    }
                    else
                    {
                        transformDict.Add(info.MappedSection, stosTransform);
                    }
                }
                else
                {
                    Trace.WriteLine($"Could not load stos file: {element}");
                }
            }
            finally
            {
                StosTransformLoadSemaphore.Release();
            }
        }


        private IContinuousTransform EnsureTransformIsContinuous(ITransform transform)
        {
            if (transform as IContinuousTransform is null)
            {
                Geometry.Transforms.StosTransformInfo info = ((ITransformInfo)transform).Info as Geometry.Transforms.StosTransformInfo;
                string SerializerCacheFullPath = System.IO.Path.Combine(this.Paths.StosCacheDir, info.GetCacheFilename(".stos_bin"));
                return Serialization.LoadOrCreateContinuousTransform(SerializerCacheFullPath, transform as IDiscreteTransform);
            }

            return transform as IContinuousTransform;
        }

        /// <summary>
        /// Displays a string in the UI as sections load
        /// </summary>
        /// <param name="ListSectionThreadingObj"></param>
        /// <param name="workerThread"></param>
        private static async Task WaitForLoadStosTransformThreads(List<Task<LoadStosResult>> ListStosTransformTasks, IProgress<ProgressInfo> workerThread, CancellationToken token)
        {
            workerThread?.Report(new ProgressInfo("Waiting for Stos Transform Loading Threads", 0));
            int countFinished = 0;
            int NumStosFiles = ListStosTransformTasks.Count;

            while (ListStosTransformTasks.Count > 0)
            {
                Task<LoadStosResult>[] stosTasks = [.. ListStosTransformTasks];

                var completedTask = await System.Threading.Tasks.Task.WhenAny(stosTasks).ConfigureAwait(false);

                if (token.IsCancellationRequested)
                    throw new TaskCanceledException("WaitForLoadStosTransformThreads cancelled by token");

                LoadStosResult result = completedTask.Result;
                ListStosTransformTasks.Remove(completedTask);

                /*
                LoadStosResult result = await stosTasks[0];
                ListStosTransformTasks.RemoveAt(0);
                */

                //Test to see if the wait state is set 
                countFinished++;
                int Progress = NumStosFiles > 0 ? (countFinished * 100) / NumStosFiles : 100;
                if (result.Transform is null)
                {
                    workerThread?.Report(new ProgressInfo($"Failed Loading {result.element}", Progress, 100));
                    continue;
                }
                else
                {
                    workerThread?.Report(new ProgressInfo($"Loaded {result.Transform}", Progress, 100));
                }
            }
        }


        /// <summary>
        /// Displays a string in the UI as sections load
        /// </summary>
        /// <param name="ListSectionThreadingObj"></param>
        /// <param name="workerThread"></param>
        private async Task WaitForCreateSectionThreads(List<Task<Section>> ListSectionThreadingObj, IProgress<ProgressInfo> workerThread, CancellationToken token)
        {
            workerThread.Report(new ProgressInfo("Waiting for Section Loading Threads", 0));

            var taskArray = ListSectionThreadingObj.ToArray();
            int countFinished = 0;
            int NumSections = taskArray.Length;
            while (taskArray.Length > 0)
            {
                int iCompleted = Task.WaitAny(taskArray, token);
                var Section = taskArray[iCompleted].Result;
                taskArray = taskArray.RemoveAt(iCompleted);

                //var Section = await taskArray[0];
                //taskArray = taskArray.RemoveAt(0);

                countFinished++;
                int Progress = NumSections > 0 ? (countFinished * 100) / NumSections : 100;

                OnSectionLoadComplete(Section);
                workerThread?.Report(new ProgressInfo($"Loaded {Section}", Progress));
            }
        }

        //readonly SemaphoreSlim loadSectionSemaphore = new SemaphoreSlim(1);

        /// <summary>
        /// Called on the main thread after a section has loaded on a worker thread
        /// </summary>
        private void OnSectionLoadComplete(Section section)
        {
            try
            {
                //await loadSectionSemaphore.WaitAsync();

                foreach (string name in section.ChannelNames)
                {
                    AddChannel(name);
                    Volume.AddChannel(name);
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
            finally
            {
                //loadSectionSemaphore.Release();
            }

            //return section;
        }

        private void AddTileServerToSectionMappings(Section section)
        {
            foreach (TileServerInfo tileserver in this.TileServerList)
            {
                section.AddOCPTileserver(tileserver);
            }
        }


        private static ITransform LoadSerializedTransformFromCache(string CacheStosPath, StosTransformInfo _ControlToVolumeInfo, StosTransformInfo _SectionToControlInfo)
        {
            ITransform cachedTransform = null;

            throw new NotImplementedException("This path needs to be updated so binary encoded transforms are written and read");

            try
            {
                if (Geometry.Global.IsCacheFileValid(CacheStosPath, [_ControlToVolumeInfo.LastModified, _SectionToControlInfo.LastModified, Global.OldestValidCachedTransform]))
                {
                    string outString = $"Loading from JSON cache: {_SectionToControlInfo.MappedSection} to {_ControlToVolumeInfo.ControlSection}";
                    Trace.WriteLine(outString);
                    using Stream binFile = System.IO.File.OpenRead(CacheStosPath);
                    //cachedTransform = JsonTransformSerializer.Deserialize(binFile); 
                }
                else
                {
                    Geometry.Global.TryDeleteCacheFile(CacheStosPath);
                }
            }
            catch (Exception)
            {
                Trace.WriteLine($"Exception loading {CacheStosPath}, deleting");
                Geometry.Global.TryDeleteCacheFile(CacheStosPath);

                return null;
            }

            return cachedTransform;
        }

        private static async Task<IContinuousTransform> LoadStosFromCache(string CacheStosPath, StosTransformInfo ControlToVolumeInfo, StosTransformInfo SectionToControlInfo)
        {
            DiscreteTransformWithContinuousFallback continuousTransform = null;
            try
            {
                if (Geometry.Global.IsCacheFileValid(CacheStosPath, [ControlToVolumeInfo.LastModified, SectionToControlInfo.LastModified, Global.OldestValidCachedTransform]))
                {
                    string outString =
                        $"Loading from ITK string cache: {SectionToControlInfo.MappedSection} to {ControlToVolumeInfo.ControlSection}";
                    Trace.WriteLine(outString);
                    DateTime CacheLastModifiedUtc = System.IO.File.GetLastWriteTimeUtc(CacheStosPath);
                    StosTransformInfo stosInfo = new(ControlToVolumeInfo.ControlSection, SectionToControlInfo.MappedSection, CacheLastModifiedUtc);
                    using Stream stostext = System.IO.File.OpenRead(CacheStosPath) as Stream;
                    var cachedTransform = await TransformFactory.ParseStos(stostext,
                        stosInfo,
                        1).ConfigureAwait(false);

                    if (cachedTransform is IContinuousTransform transform)
                        return transform;

                    if (cachedTransform is not IDiscreteTransform)
                        throw new NullReferenceException($"Unable to load {stostext} for {stosInfo}");

                    continuousTransform = new DiscreteTransformWithContinuousFallback(cachedTransform as IDiscreteTransform,
                        new RBFTransform(((ITransformControlPoints)cachedTransform).MapPoints, stosInfo),
                        stosInfo);
                }
                else
                {
                    Geometry.Global.TryDeleteCacheFile(CacheStosPath);
                }
            }
            catch (Exception)
            {
                Trace.WriteLine($"Exception loading {CacheStosPath}, deleting");
                Geometry.Global.TryDeleteCacheFile(CacheStosPath);

                return null;
            }

            return continuousTransform;
        }

        /// <summary>
        /// Write the straight ITK format transform to the cache file
        /// </summary>
        /// <param name="CacheStosPath"></param>
        /// <param name="itkTransform"></param>
        /// <returns></returns>
        private static void SaveSerializedTransformToCache(string CacheStosPath, IITKSerialization itkTransform)
        {
            //TODO: This was a binary formatted file before the port to being a modern SDK project.  It should be converted to a binary format again, or the serialization should be updated to use a more efficient format.

            using Stream binFile = System.IO.File.OpenWrite(CacheStosPath);
            using StreamWriter streamWriter = new(binFile, System.Text.Encoding.UTF8, 1024, true)
            {
                AutoFlush = true
            };

            string itk = itkTransform.GetITKTransform();
            streamWriter.Write(itk);
        }

        private static void SaveStosToCache(string CacheStosPath, IITKSerialization itkTransform, StosTransformInfo ControlToVolumeInfo, StosTransformInfo SectionToControlInfo)
        {
            using StreamWriter fs = System.IO.File.CreateText(CacheStosPath);
            fs.WriteLine(ControlToVolumeInfo.ToString());
            fs.WriteLine(SectionToControlInfo.ToString());

            string itk = itkTransform.GetITKTransform();
            fs.WriteLine(itk);
        }


        /// <summary>
        /// Adds a transform to each section mapping it into each of the volume spaces we found
        /// </summary>
        public void CreateVolumeTransforms(IProgress<ProgressInfo> workerThread)
        {
            foreach (string transformKey in Transforms.Keys)
            {
                //The transform list is sorted by which section the transform maps from. 
                //Next we'll add transfroms so every transform maps from the mapped section to section #1
                SortedList<int, ITransform> TList = Transforms[transformKey];

                //Create a registration chain so we know what order to register the sections in
                RegistrationTree tree = RegistrationTree.Build(TList, Sections.Keys);

                int iSectionProgress = 0;
                //OK, walk the tree, adding from the root nodes down
                foreach (RegistrationTreeNode rootnode in tree.RootNodes.Values)
                {
                    Queue<int> SafeNodes = new();
                    SafeNodes.Enqueue(rootnode.SectionNumber);

                    while (SafeNodes.Count > 0)
                    {
                        int ControlSection = SafeNodes.Dequeue();
                        RegistrationTreeNode ControlNode = tree.Nodes[ControlSection];

                        ITransform ControlTrans = null;
                        IContinuousTransform ContinuousControlTransform = null;

                        //Find the section that can map our transform
                        if (TList.TryGetValue(ControlNode.SectionNumber, out var value))
                        {
                            //string outString = "Loading continuous transform for control section: " + ControlSection.ToString();
                            //workerThread.Report((iSectionProgress * 100) / TList.Count, outString);
                            ControlTrans = value;
                        }

                        foreach (int childSection in ControlNode.Children)
                        {
                            iSectionProgress++;
                            ITransform trans = TList[childSection];
                            if (((ITransformInfo)trans)?.Info is not StosTransformInfo info)
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
                                var transformInfo = ((ITransformInfo)trans)?.Info;
                                string CacheStosPath = Paths.GetITKSCacheName(info.MappedSection, ControlInfo.ControlSection);
                                string CacheSerializedPath = Paths.GetSerializerCacheName(info.MappedSection, ControlInfo.ControlSection);
                                TList[childSection] = LoadStosFromCache(CacheStosPath, ControlInfo, info).GetAwaiter().GetResult();
                                //TList[childSection] = LoadSerializedTransformFromCache(CacheSerializedPath, ControlInfo, info);

                                //CalculateSliceToVolume = true; 
                                if (TList[childSection] is null)
                                {
                                    if (ContinuousControlTransform is null)
                                    {
                                        //This line creating continuous transforms can be slow.
                                        ContinuousControlTransform = EnsureTransformIsContinuous(ControlTrans);
                                        //Replace the discreet transform with the continuous version for future use
                                        TList[ControlNode.SectionNumber] = ContinuousControlTransform;
                                    }

                                    try
                                    {
                                        string outString = $"Adding transforms: {trans} to {ControlTrans}";
                                        workerThread.Report(new ProgressInfo(outString, (iSectionProgress * 100) / TList.Count, 100));

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
                                        Trace.WriteLine(
                                            $"Exception adding transforms {trans} to {ControlTrans}");
                                        trans = TList[childSection];
                                    }

                                    if (TList[childSection] is IITKSerialization itkTransform)
                                    {
                                        try
                                        {
                                            SaveSerializedTransformToCache(CacheSerializedPath, itkTransform);
                                        }
                                        catch (System.Text.Json.JsonException e)
                                        {

                                            System.Diagnostics.Debugger.Break();
                                        }
                                        SaveStosToCache(CacheStosPath, itkTransform, ControlInfo, info);
                                    }
                                }
                                else
                                {
                                    string outString = $"Loading transforms from Cache: {trans} to {ControlTrans}";
                                    workerThread.Report(new ProgressInfo(outString, (iSectionProgress * 100) / TList.Count, 100));
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
