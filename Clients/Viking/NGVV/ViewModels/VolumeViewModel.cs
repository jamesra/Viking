using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq; 
using Viking.VolumeModel;

namespace Viking.ViewModels
{
    public class VolumeViewModel
    {
        private Volume _Volume;

        public SortedList<int, SectionViewModel> SectionViewModels;

        public string Name { get { return _Volume.Name; } }

        public bool IsLocal { get { return _Volume.IsLocal; } }

        public int DefaultSectionNumber
        {
            get
            {
                if(_Volume.DefaultSectionNumber.HasValue)
                { 
                    if(SectionViewModels.ContainsKey(_Volume.DefaultSectionNumber.Value))
                    {
                        return _Volume.DefaultSectionNumber.Value; 
                    }
                }

                return SectionViewModels.Keys[0]; 
            }
        }

        public string DefaultVolumeTransform { get { return _Volume.DefaultVolumeTransform; } }

        public ChannelInfo[] DefaultChannels { get { return _Volume.DefaultChannels; } set { _Volume.DefaultChannels = value; } }

        public string[] ChannelNames { get { return _Volume.ChannelNames; } }

        public string[] TransformNames { get { return _Volume.Transforms.Keys.ToArray(); } }

        public XDocument VolumeXML { get { return _Volume.VolumeXML; } }

        public bool UpdateServerVolumePositions { get { return _Volume.UpdateServerVolumePositions; } }

        public VolumeViewModel(string path, string localCachePath, System.ComponentModel.BackgroundWorker workerThread)
        {
            _Volume = new Volume(path, localCachePath, workerThread);

            SectionViewModels = new SortedList<int, SectionViewModel>(_Volume.Sections.Length);

            foreach (Section s in _Volume.Sections)
            {
                SectionViewModel sectionViewModel = new SectionViewModel(this, s);
                SectionViewModels.Add(s.Number, sectionViewModel); 
            }

        }

        public string Host { get { return _Volume.Host; } }

        
    }
}
