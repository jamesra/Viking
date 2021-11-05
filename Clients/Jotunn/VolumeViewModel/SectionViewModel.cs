using Jotunn.Common;
using System.Collections.Generic;
using System.Windows;
using Viking.VolumeModel;

namespace Viking.VolumeViewModel
{
    /// <summary>
    /// Encapsulates a section within the UI, including the visible region
    /// </summary>
    public class SectionViewModel : SectionViewModelBase
    {
        public static readonly DependencyProperty VisibleRegionProperty;

        public VisibleRegionInfo VisibleRegion
        {
            get { return (VisibleRegionInfo)GetValue(SectionViewModel.VisibleRegionProperty); }
            set { SetCurrentValue(SectionViewModel.VisibleRegionProperty, value); }
        }

        static SectionViewModel()
        {
            SectionViewModel.VisibleRegionProperty = DependencyProperty.Register("VisibleRegion",
                                                                                   typeof(VisibleRegionInfo),
                                                                                   typeof(SectionViewModel),
                                                                                   new FrameworkPropertyMetadata(null,
                                                                                       FrameworkPropertyMetadataOptions.AffectsRender));
        }

        public SectionViewModel(Volume Volume, Section section, MappingManager _MappingManager) : base(Volume, section, _MappingManager)
        {
        }
    }


    /// <summary>
    /// Encapsulates a section within the UI
    /// </summary>
    public class SectionViewModelBase : DependencyObject
    {
        public readonly Section section;

        public MappingManager _MappingManager;

        public string Name { get { return section.Name; } }

        public int Number { get { return section.Number; } }

        public string Notes { get { return section.Notes; } }

        public string Path { get { return section.Path; } }

        public string SubPath { get { return section.SectionSubPath; } }

        public override string ToString()
        {
            return section.ToString(); 
        }

        string _SelectedChannel = null;
        public string SelectedChannel { 
            get 
            {
                if (_SelectedChannel == null)
                    _SelectedChannel = DefaultChannel; 
                return _SelectedChannel; 
            }
            set
            {
                if(ChannelNames.Contains(value))
                    _SelectedChannel = value;
            }
        }

        public string DefaultChannel { get { return section.DefaultChannel; } }
        public IList<string> Channels { get { return section.Channels; } }

        public string DefaultPyramidTransform { get { return section.DefaultPyramidTransform; } }
        public string DefaultPyramid { get { return section.DefaultPyramid; } }
        public List<string> TilesetNames { get { return section.TilesetNames; } }
        public List<string> PyramidTransformNames { get { return section.PyramidTransformNames; } }
        public SortedList<string,Pyramid> ImagePyramids { get { return section.ImagePyramids; } }

        /// <summary>
        /// The currently displayed channels
        /// </summary>
        public ChannelInfo[] ChannelInfoArray {
            get { return section.ChannelInfoArray;}
            set { section.ChannelInfoArray = value; }
        }

        /// <summary>
        /// The names of all channels supported by this section
        /// </summary>
        public List<string> ChannelNames { get { return section.ChannelNames; } }

        private Volume _VolumeModel;
        public Volume VolumeModel { get { return _VolumeModel; } }

        
        public TileMappingViewModel DefaultMapping
        {
            get
            {  
                MappingBase mapping = _MappingManager.GetMapping(_VolumeModel.DefaultVolumeTransform, section.Number, DefaultChannel, DefaultPyramidTransform);
                TileMappingViewModel mapViewModel = new TileMappingViewModel(mapping);
                return mapViewModel;
            }
        }
        

#region Dependency Properties

        private static readonly DependencyProperty MappingProperty;

        public MappingBase Mapping
        {
            get { return (MappingBase)this.GetValue(MappingProperty); }
            set { this.SetValue(MappingProperty, value); }
        }

        static SectionViewModelBase()
        {
            SectionViewModelBase.MappingProperty = DependencyProperty.Register("Mapping",
                                                                                   typeof(MappingBase),
                                                                                   typeof(SectionViewModelBase),
                                                                                   new FrameworkPropertyMetadata(null,
                                                                                       FrameworkPropertyMetadataOptions.AffectsRender));
        }

#endregion 

        public SectionViewModelBase(Volume Volume, Section section, MappingManager _MappingManager)
        {
            this._VolumeModel = Volume;
            this.section = section;
            this._MappingManager = _MappingManager;
        }

        public async System.Threading.Tasks.Task PrepareTransform(string transform)
        {
            await this.section.PrepareTransform(transform); 
        }
        
        #region Commands
        /*
            private bool CanChangeSectionNumber(int delta)
            {
                return false;
            }

            private void ChangeSectionNumber(int delta)
            {

            }

            public Microsoft.Practices.Prism.Commands.DelegateCommand<int> IncrementZ
            {
                get
                {
                    return new Microsoft.Practices.Prism.Commands.DelegateCommand<int>(f => this.ChangeSectionNumber(1) );
                }
            }

            public Microsoft.Practices.Prism.Commands.DelegateCommand<int> DecrementZ
            {
                get
                {
                    return new Microsoft.Practices.Prism.Commands.DelegateCommand<int>(f => this.ChangeSectionNumber(-1));
                }
            }
        */
        #endregion
         
    }
}
