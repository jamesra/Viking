using System;
using System.Threading;
using System.Xml.Linq;

namespace Viking.VolumeModel
{
    class CreateSectionThreadingObj : IDisposable
    {
        public ManualResetEvent DoneEvent = new ManualResetEvent(false);

        readonly Volume volume;
        readonly string SectionPath;
        readonly XElement reader;
        readonly string DescriptiveString;

        public Section newSection;

        public override string ToString()
        {
            return DescriptiveString;
        }


        public CreateSectionThreadingObj(Volume vol, string path, XElement SectionElement)
        {
            this.reader = SectionElement;
            this.volume = vol;
            this.SectionPath = path;

            this.DescriptiveString = SectionElement.ToString();
        }

        public void ThreadPoolCallback(Object threadContext)
        {
            newSection = new Section(volume, SectionPath, reader);

            DoneEvent.Set();
        }

        #region IDisposable Members

        public void Dispose()
        {
            DoneEvent.Close();
        }

        #endregion
    }
}
