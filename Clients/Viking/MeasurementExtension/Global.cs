using SIMeasurement;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using Viking.Common;
using Viking.ViewModels;

namespace MeasurementExtension
{
    public class Global : IInitExtensions
    {
        internal static double _UnitsPerPixel = 1;
        public static double UnitsPerPixel => _UnitsPerPixel;

        internal static SILengthUnits _UnitOfMeasure;
        public static SILengthUnits UnitOfMeasure => _UnitOfMeasure;

        public static LengthMeasurement PixelWidth => new(Global.UnitOfMeasure, Global.UnitsPerPixel);

        #region IInitExtensions Members

        /// <summary>
        /// Returns true if the extension should be loaded
        /// </summary>
        /// <param name="provider"></param>
        /// <returns></returns>
        bool IInitExtensions.Initialize(IServiceProvider provider)
        {
            //This code will fetch the scale of the images from the webserver
            //If the scale can't be found we won't
            VolumeViewModel volume = Viking.UI.State.volume;

            if (volume is null)
                return false;

            if (GetScaleFromXML(volume.VolumeElement))
                return true;

            //See if we can load the about.xml file, this is for legacy support and can be removed after VikinkXML files have been regenerated with latest
            //CreateXML updates from 11/1/10

            Uri MappingURI = new(volume.Host + "/About.xml");
            // Use Task.Run to avoid blocking the thread pool when called from synchronous context
            var xmlMapping = Task.Run(async () => await GetXMLFromUriAsync(MappingURI).ConfigureAwait(false)).GetAwaiter().GetResult();

            //See if we can locate a scale tag
            GetScaleFromXML(Viking.VolumeModel.Volume.GetVolumeElement(xmlMapping));

            //Even if we couldn't load the default values, the user can set them.  Go ahead and load up.
            //If this module could not function we should return false which would tell Viking to unload it
            return true;

        }

        private static async Task<XDocument?> GetXMLFromUriAsync(Uri uri)
        {
            HttpClientHandler handler;

            handler = uri.Scheme.ToLower() == "https" ? new HttpClientHandler { Credentials = Viking.UI.State.UserCredentials } : new HttpClientHandler { UseDefaultCredentials = true };

            using HttpClient httpClient = new(handler);
            try
            {
                var response = await httpClient.GetAsync(uri).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return XDocument.Parse(content);
            }
            catch (HttpRequestException)
            {
                Trace.WriteLine("Could not locate WebAnnotationMapping.XML, disabling WebAnnotations.", "Measurement");
                return null;
            }
        }

        private static bool GetScaleFromXML(XElement elem)
        {

            //Examine the XML document and determine the scale

            //Fetch the name if we know it
            switch (elem.Name.LocalName)
            {
                case "Volume":
                    IEnumerable<XElement> MappingElements = elem.Elements().Where(e => e.Name.LocalName == "Scale");

                    if (MappingElements.Count() == 0)
                        break;

                    XElement MappingElement = MappingElements.First();

                    XAttribute EndpointAttribute = MappingElement.Attribute("UnitsPerPixel");
                    if (EndpointAttribute is null)
                        break;

                    Global._UnitsPerPixel = System.Convert.ToDouble(EndpointAttribute.Value);

                    EndpointAttribute = MappingElement.Attribute("UnitsOfMeasure");
                    if (EndpointAttribute is null)
                        break;

                    try
                    {
                        Global._UnitOfMeasure = (SIMeasurement.SILengthUnits)Enum.Parse(typeof(SIMeasurement.SILengthUnits), EndpointAttribute.Value);
                    }
                    catch (ArgumentNullException)
                    {
                        Trace.WriteLine("Null unit of measure", "Measurement");
                        return false;
                    }
                    catch (ArgumentException)
                    {
                        Trace.WriteLine(string.Format("Non SI unit of measure {0}, disabling WebAnnotations.", EndpointAttribute.Value), "Measurement");
                        return false;
                    }
                    catch (OverflowException)
                    {
                        Trace.WriteLine(string.Format("{0} is outside the range of the underlying type of SI Length Units", EndpointAttribute.Value), "Measurement");
                        return false;
                    }

                    return true;

                default:
                    break;
            }

            //Even if we couldn't load the default values, the user can set them.  Go ahead and load up.
            //If this module could not function we should return false which would tell Viking to unload it
            return false;
        }


        #endregion
    }
}

