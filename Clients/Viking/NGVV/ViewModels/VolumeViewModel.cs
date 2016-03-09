using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq; 
using Viking.VolumeModel;
using Geometry;

namespace Viking.ViewModels
{
    public class VolumeViewModel
    {
        private Volume _Volume;
        private MappingManager _MappingManager;

        public SortedList<int, SectionViewModel> SectionViewModels;

        public string Name { get { return _Volume.Name; } }

        public bool IsLocal { get { return _Volume.IsLocal; } }

        public int DefaultSectionNumber
        {
            get
            {
                if (_Volume.DefaultSectionNumber.HasValue)
                {
                    if (SectionViewModels.ContainsKey(_Volume.DefaultSectionNumber.Value))
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

        public VolumeViewModel(Volume volume)
        {
            this._Volume = volume;
            _MappingManager = new MappingManager(volume);

            SectionViewModels = new SortedList<int, SectionViewModel>(_Volume.Sections.Length);

            foreach (Section s in _Volume.Sections)
            {
                SectionViewModel sectionViewModel = new SectionViewModel(this, s);
                SectionViewModels.Add(s.Number, sectionViewModel);
            }
        }

        public string Host { get { return _Volume.Host; } }

        public MappingBase GetMapping(string VolumeTransformName, int SectionNumber, string ChannelName, string SectionTransformName)
        {
            return _MappingManager.GetMapping(VolumeTransformName, SectionNumber, ChannelName, SectionTransformName);
        }

        public MappingBase GetMapping(int SectionNumber, string ChannelName, string SectionTransformName)
        {
            return _MappingManager.GetMapping(this.ActiveVolumeTransform, SectionNumber, ChannelName, SectionTransformName);
        }

        public IVolumeToSectionMapper GetMapping(int SectionNumber)
        {
            SectionViewModel svm = this.SectionViewModels[SectionNumber];
            return _MappingManager.GetMapping(this.ActiveVolumeTransform, SectionNumber, svm.ActiveChannel, svm.ActiveTransform);
        }

        public void ReduceCacheFootprint(object state)
        {
            _MappingManager.ReduceCacheFootprint();
        }

        #region Events


        /// <summary>
        /// Fires when the transform used to place the section into the volume changes
        /// </summary>
        public event Viking.Common.TransformChangedEventHandler TransformChanged;

        #endregion

        protected string _ActiveVolumeTransform;
        public string ActiveVolumeTransform
        {
            get { return _ActiveVolumeTransform; }
            set
            {
                bool NewValue = value != _ActiveVolumeTransform;

                if (NewValue)
                {
                    string OldTransform = _ActiveVolumeTransform;
                    _ActiveVolumeTransform = value;

                    if (TransformChanged != null)
                    {
                        TransformChanged(this, new Viking.Common.TransformChangedEventArgs(_ActiveVolumeTransform, OldTransform));
                    }
                }
            }
        }
         
        public bool UsingVolumeTransform
        {
            get
            {
                return ActiveVolumeTransform != null; 
            }
        }

        /// <summary>
        /// Get the boundaries for a section given the current transforms
        /// </summary>
        /// <param name="section"></param>
        /// <returns></returns>
        public GridRectangle SectionBounds(int SectionNumber, string ActiveChannel, string ActiveTransform)
        {
            MappingBase map = this.GetMapping(this.ActiveVolumeTransform, SectionNumber, ActiveChannel, ActiveTransform);
            if (map != null)
            {
                return map.Bounds;
            }

            throw new System.ArgumentException("Cannot find boundaries for section");
        }
    }
}
