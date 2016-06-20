using System;
using System.Diagnostics; 
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq; 
using Geometry;
using System.IO;
using Geometry.Transforms;

namespace Viking.VolumeModel
{
    class CreateStosTransformThreadingObj : IDisposable
    {
        public ManualResetEvent DoneEvent = new ManualResetEvent(false);

        readonly Uri ServerPath;
        readonly string LocalCachePath;
        readonly string LocalCacheDir;
        readonly System.Net.NetworkCredential UserCredentials;

        public readonly XElement element;
        private Stream stream;

        public ITransform stosTransform;

        protected DateTime? lastModified; 

        public override string ToString()
        {
            return element.ToString(); 
        }

        public CreateStosTransformThreadingObj(Stream stream, XElement reader, DateTime LastModified, string LocalCacheDir)
        {
            this.element = reader;
            this.stream = stream;
            this.lastModified = new DateTime?(LastModified);
            this.LocalCacheDir = LocalCacheDir;
        }

        public CreateStosTransformThreadingObj(String localCachePath, XElement reader, string LocalCacheDir)
        {
            this.element = reader;
            this.LocalCachePath = localCachePath;
            this.LocalCacheDir = LocalCacheDir;
        }

        public CreateStosTransformThreadingObj(Uri path, System.Net.NetworkCredential userCreds, XElement reader, string LocalCacheDir)
        {
            this.element = reader;
            this.ServerPath = path;
            UserCredentials = userCreds;
            this.LocalCacheDir = LocalCacheDir;
        }

        public void ThreadPoolCallback(Object threadContext)
        {  
            if (stream != null)
            {
                //stosTransform = new StosGridTransform(stream, element);
                int pixelSpacing = System.Convert.ToInt32(element.Attribute("pixelSpacing").Value);
                int MappedSection = System.Convert.ToInt32(element.Attribute("mappedSection").Value);
                int ControlSection = System.Convert.ToInt32(element.Attribute("controlSection").Value);

                if (!this.lastModified.HasValue)
                {
                    Trace.WriteLine("Stos stream from zip has no lastModified date.  Caching is probably not working.");
                    this.lastModified = DateTime.UtcNow;
                }

                Geometry.Transforms.StosTransformInfo info = new Geometry.Transforms.StosTransformInfo(ControlSection, MappedSection, this.lastModified.Value);

                stosTransform = TransformFactory.ParseStos(stream, info, pixelSpacing);

                if (stream != null)
                {
                    stream.Close();
                    stream.Dispose();
                    stream = null; 
                }
            }
            else if (LocalCachePath != null)
            {
                stosTransform = TransformFactory.ParseStos(LocalCachePath);
            }
            else
            {
                //stosTransform = new StosGridTransform(Path, element, UserCredentials); 
                stosTransform = TransformFactory.ParseStos(ServerPath, element, UserCredentials);
            }

            /*
            if(loadedTransform as IDiscreteTransform != null)
            {
                Geometry.Transforms.StosTransformInfo info = ((ITransformInfo)loadedTransform).Info as Geometry.Transforms.StosTransformInfo;
                string SerializerCacheFullPath = Path.Combine(this.LocalCacheDir, info.GetCacheFilename(".stos_bin"));
                if (Geometry.Global.IsCacheFileValid(SerializerCacheFullPath, info.LastModified))
                {
                    stosTransform = Serialization.LoadSerializedTransformFromCache(SerializerCacheFullPath);
                }
                else
                {
                    stosTransform = loadedTransform;
                }
            }
            else
            {
                stosTransform = loadedTransform as IContinuousTransform;
            }*/

            DoneEvent.Set();
        }

       

        #region IDisposable Members

        public void Dispose()
        {
            if (DoneEvent != null)
            {
                DoneEvent.Close();
                DoneEvent.Dispose();
                DoneEvent = null; 
            }

            if (stream != null)
            {
                stream.Close();
                stream.Dispose();
                stream = null;
            }
        }

        #endregion
    }
}
