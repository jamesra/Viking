using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq; 
using Viking.VolumeModel;
using Geometry;

namespace Viking.ViewModels
{
    public class VolumeToSectionTransform : IVolumeToSectionTransform
    {
        readonly string _Name;
        readonly Geometry.ITransform Transform;

        public VolumeToSectionTransform(string Name, ITransform transform)
        {
            this._Name = Name;
            this.Transform = transform;
        }

        public override string ToString()
        {
            return _Name;
        }

        public long ID
        {
            get
            {
                return _Name.GetHashCode();
            }
        }

        public GridRectangle? SectionBounds
        {
            get
            {
                if(Transform as IDiscreteTransform != null)
                {
                    return ((IDiscreteTransform)Transform).MappedBounds;
                }
                else
                {
                    return new GridRectangle?();
                }
            }
        }

        public GridRectangle? VolumeBounds
        {
            get
            {
                if (Transform as IDiscreteTransform != null)
                {
                    return ((IDiscreteTransform)Transform).ControlBounds;
                }
                else
                {
                    return new GridRectangle?();
                }
            }
        }

        public GridVector2[] SectionToVolume(GridVector2[] Points)
        {
            return Transform.Transform(Points);
        }

        public GridVector2 SectionToVolume(GridVector2 P)
        {
            return Transform.Transform(P);
        }

        public bool[] TrySectionToVolume(GridVector2[] Points, out GridVector2[] transformedP)
        {
            return Transform.TryTransform(Points, out transformedP);
        }

        public bool TrySectionToVolume(GridVector2 P, out GridVector2 transformedP)
        {
            return Transform.TryTransform(P, out transformedP);
        }

        public bool[] TryVolumeToSection(GridVector2[] Points, out GridVector2[] transformedP)
        {
            return Transform.TryInverseTransform(Points, out transformedP);
        }

        public bool TryVolumeToSection(GridVector2 P, out GridVector2 transformedP)
        {
            return Transform.TryInverseTransform(P, out transformedP);
        }

        public GridVector2[] VolumeToSection(GridVector2[] Points)
        {
            return Transform.InverseTransform(Points);
        }

        public GridVector2 VolumeToSection(GridVector2 P)
        {
            return Transform.InverseTransform(P);
        }
    }

    public class VolumeViewModel : IVolumeTransformProvider
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

        public XElement VolumeElement { get { return _Volume.VolumeElement; } }

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

        public MappingBase GetTileMapping(string VolumeTransformName, int SectionNumber, string ChannelName, string SectionTransformName)
        {
            return _MappingManager.GetMapping(VolumeTransformName, SectionNumber, ChannelName, SectionTransformName);
        }

        public MappingBase GetTileMapping(int SectionNumber, string ChannelName, string SectionTransformName)
        {
            return _MappingManager.GetMapping(this.ActiveVolumeTransform, SectionNumber, ChannelName, SectionTransformName);
        }

        protected static string BuildTransformKey(string VolumeTransformName, int SectionNumber)
        {
            string key = VolumeTransformName + '-' + SectionNumber.ToString("D4");
            return key;
        }

        public IVolumeToSectionTransform GetSectionToVolumeTransform(int SectionNumber)
        {
            SectionViewModel svm = this.SectionViewModels[SectionNumber];
            SortedList<int, ITransform> SectionTransforms = _Volume.Transforms[this.ActiveVolumeTransform];

            if(SectionTransforms.ContainsKey(SectionNumber))
                return new VolumeToSectionTransform(BuildTransformKey(this.ActiveVolumeTransform, SectionNumber),
                                                    SectionTransforms[SectionNumber]);
            else
                return new VolumeToSectionTransform(BuildTransformKey(this.ActiveVolumeTransform, SectionNumber),
                                                    new Geometry.Transforms.IdentityTransform());
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
    }
}
