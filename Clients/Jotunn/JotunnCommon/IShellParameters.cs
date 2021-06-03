using System.Xml.Linq;

namespace Jotunn
{
    public interface IShellParameters
    {
        /// <summary>
        /// This should always have the following properties
        /// Host = The URL of the server hosting the volume, no file name or path
        /// HostPath = The URL of the server and local path hosting the volume.  No filename
        /// </summary>
        System.Collections.Specialized.NameValueCollection GetArgTable { get; }

        /// <summary>
        /// The XDocument representing the VikingXML file defining the volume
        /// </summary>
        XDocument GetXML { get; }
    }
}
