using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Viking.VolumeModel;
using System.Xml.Linq;
using System.ComponentModel.Composition;
using Jotunn.Common;
using System.Windows;
using System.Windows.Input;
using System.Collections.ObjectModel;

namespace Viking.VolumeViewModel
{    
    /// <summary>
    /// A view of a volume and a number of sections of the same X/Y region across all visible regions
    /// </summary>
    public class VolumeViewModelSharedView : DependencyObject
    {
        public static readonly DependencyProperty VisibleRegionProperty;

        public VisibleRegionInfo VisibleRegion
        {
            get { return (VisibleRegionInfo)GetValue(VolumeViewModelSharedView.VisibleRegionProperty); }
            set { SetCurrentValue(VolumeViewModelSharedView.VisibleRegionProperty, value); }
        }

        public static readonly DependencyProperty VisibleSectionsProperty;
        public ObservableCollection<SectionViewModel> VisibleSections
        {
            get { return (ObservableCollection<SectionViewModel>)GetValue(VolumeViewModelSharedView.VisibleSectionsProperty); }
            set { SetCurrentValue(VolumeViewModelSharedView.VisibleSectionsProperty, value); }
        }


        private readonly VolumeViewModel volume;

        public SortedList<int, SectionViewModel> Sections { get { return volume.SectionViewModels; } }

        static VolumeViewModelSharedView()
        {
            VolumeViewModelSharedView.VisibleRegionProperty = DependencyProperty.Register("VisibleRegion",
                                                                                   typeof(VisibleRegionInfo),
                                                                                   typeof(VolumeViewModelSharedView),
                                                                                   new FrameworkPropertyMetadata(null,
                                                                                       FrameworkPropertyMetadataOptions.AffectsRender));

            VolumeViewModelSharedView.VisibleSectionsProperty = DependencyProperty.Register("VisibleSections",
                                                                                   typeof(ObservableCollection<int>),
                                                                                   typeof(VolumeViewModelSharedView),
                                                                                   new FrameworkPropertyMetadata(null,
                                                                                       FrameworkPropertyMetadataOptions.AffectsRender));
        }
         

        public VolumeViewModelSharedView(VolumeViewModel volume)
        {
            this.volume = volume; 
        }
    }

    public class VolumeViewModel : DependencyObject
    {
        private Volume _Volume;

        private MappingManager _MappingManager;

        public SortedList<int, SectionViewModel> SectionViewModels;

        public string Name { get { return _Volume.Name; } }

        public bool IsLocal { get { return _Volume.IsLocal; } }

        public string DefaultVolumeTransform { get { return _Volume.DefaultVolumeTransform; } }

        public ChannelInfo[] DefaultChannels { get { return _Volume.DefaultChannels; } set { _Volume.DefaultChannels = value; } }

        public string[] ChannelNames { get { return _Volume.ChannelNames; } }

        public XDocument VolumeXML { get { return _Volume.VolumeElement.Document; } }

 //       public string[] TransformNames { get { return _Volume.Transforms.Keys.ToArray(); } }
               
        public VolumeViewModel(Volume volume, System.ComponentModel.BackgroundWorker workerThread)
        {
            _Volume = volume;

            _MappingManager = new MappingManager(volume);

            SectionViewModels = new SortedList<int, SectionViewModel>(_Volume.Sections.Count);

            foreach (Section s in _Volume.Sections.Values)
            {
                SectionViewModel sectionViewModel = new SectionViewModel(volume, s, _MappingManager);
                SectionViewModels.Add(s.Number, sectionViewModel);
            }
             
        }

        public string Host { get { return _Volume.Host; } }

        public int? NextLowerSectionNumber(int sectionNumber)
        {
            int LowestKey = SectionViewModels.Keys.Min();
            while (false == SectionViewModels.ContainsKey(sectionNumber))
            {
                if (sectionNumber < LowestKey)
                    return new int?(); 

                sectionNumber--; 
            }

            return new int?(sectionNumber);
        }

        public int? NextHigherSectionNumber(int sectionNumber)
        {
            int HighestKey = SectionViewModels.Keys.Max();
            while (false == SectionViewModels.ContainsKey(sectionNumber))
            {
                if (sectionNumber < HighestKey)
                    return new int?();

                sectionNumber++;
            }

            return new int?(sectionNumber);
        }
    }
}
