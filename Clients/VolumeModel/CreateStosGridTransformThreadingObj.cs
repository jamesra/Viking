using System;
using System.Diagnostics; 
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq; 
using Geometry;
using System.IO; 

namespace Viking.VolumeModel
{
    class CreateStosTransformThreadingObj : IDisposable
    {
        public ManualResetEvent DoneEvent = new ManualResetEvent(false);

        readonly Uri ServerPath;
        readonly string LocalPath;
        readonly System.Net.NetworkCredential UserCredentials;

        public readonly XElement element;
        private Stream stream;

        public Geometry.Transforms.TransformBase stosTransform;

        protected DateTime? lastModified; 

        public override string ToString()
        {
            return element.ToString(); 
        }

        public CreateStosTransformThreadingObj(Stream stream, XElement reader, DateTime LastModified)
        {
            this.element = reader;
            this.stream = stream;
            this.lastModified = new DateTime?(LastModified); 
        }

        public CreateStosTransformThreadingObj(String fullpath, XElement reader)
        {
            this.element = reader;
            this.LocalPath = fullpath; 
        }

        public CreateStosTransformThreadingObj(Uri path, System.Net.NetworkCredential userCreds, XElement reader)
        {
            this.element = reader;
            this.ServerPath = path;
            UserCredentials = userCreds;
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

                stosTransform = Geometry.Transforms.TransformFactory.ParseStos(stream, info, pixelSpacing);

                if (stream != null)
                {
                    stream.Close();
                    stream.Dispose();
                    stream = null; 
                }
            }
            else if (LocalPath != null)
            {
                stosTransform = Geometry.Transforms.TransformFactory.ParseStos(LocalPath);
            }
            else
            {
                //stosTransform = new StosGridTransform(Path, element, UserCredentials); 
                stosTransform = Geometry.Transforms.TransformFactory.ParseStos(ServerPath, element, UserCredentials);
            }

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
