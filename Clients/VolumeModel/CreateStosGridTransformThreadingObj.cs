using Geometry;
using Geometry.Transforms;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Xml.Linq;
using System.Threading.Tasks;

namespace Viking.VolumeModel
{
    internal class LoadStosResult 
    {
        public ITransform Transform; 
        public XElement element;

        public static async Task<LoadStosResult> LoadAsync(Stream stream, XElement element, DateTime? lastModified = null)
        {
            var result = new LoadStosResult();

            //stosTransform = new StosGridTransform(stream, element);
            int pixelSpacing = System.Convert.ToInt32(element.Attribute("pixelSpacing").Value);
            int MappedSection = System.Convert.ToInt32(element.Attribute("mappedSection").Value);
            int ControlSection = System.Convert.ToInt32(element.Attribute("controlSection").Value);

            if (false == lastModified.HasValue)
            {
                Trace.WriteLine("Stos stream from zip has no lastModified date.  Caching is probably not working.");
                lastModified = DateTime.UtcNow;
            }

            Geometry.Transforms.StosTransformInfo info = new Geometry.Transforms.StosTransformInfo(ControlSection, MappedSection, lastModified.Value);

            result.Transform = await TransformFactory.ParseStos(stream, info, pixelSpacing);

            if (stream != null)
            {
                //TODO: Try to get this out of here, or at least wrap in try/finally
                stream.Close();
                stream.Dispose();
                stream = null;
            }

            return result;
        }

        public static async Task<LoadStosResult> LoadAsync(String localCachePath, XElement reader)
        {
            var result = new LoadStosResult() { element = reader };
            result.Transform = await TransformFactory.ParseStos(localCachePath).ConfigureAwait(false);
            return result;
        }

        public static async Task<LoadStosResult> LoadAsync(Uri ServerPath, System.Net.NetworkCredential UserCredentials, XElement element)
        {
            var result = new LoadStosResult() { element = element };
            result.Transform = await TransformFactory.ParseStos(ServerPath, element, UserCredentials).ConfigureAwait(false);
            return result;
        }
    } 
}
