using Jotunn.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Xml.Linq;
using Viking.VolumeModel;

namespace Viking.VolumeViewModel
{
    public class VolumeViewModelSharedView : DependencyObject
    {
        public static readonly DependencyProperty VisibleRegionProperty;

        public VisibleRegionInfo VisibleRegion
        {
            get { return (VisibleRegionInfo)GetValue(VisibleRegionProperty); }
            set { SetCurrentValue(VisibleRegionProperty, value); }
        }

        public static readonly DependencyProperty VisibleSectionsProperty;
        public ObservableCollection<SectionViewModel> VisibleSections
        {
            get { return (ObservableCollection<SectionViewModel>)GetValue(VisibleSectionsProperty); }
            set { SetCurrentValue(VisibleSectionsProperty, value); }
        }

        private readonly VolumeViewModel volume;

        public SortedList<int, SectionViewModel> Sections { get { return volume.SectionViewModels; } }

        static VolumeViewModelSharedView()
        {
            VisibleRegionProperty = DependencyProperty.Register("VisibleRegion",
                                                                                   typeof(VisibleRegionInfo),
                                                                                   typeof(VolumeViewModelSharedView),
                                                                                   new FrameworkPropertyMetadata(null,
                                                                                       FrameworkPropertyMetadataOptions.AffectsRender));

            VisibleSectionsProperty = DependencyProperty.Register("VisibleSections",
                                                                                   typeof(ObservableCollection<SectionViewModel>),
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

        public Volume Volume => _Volume;

        private MappingManager _MappingManager;

        public SortedList<int, SectionViewModel> SectionViewModels;

        public static readonly DependencyProperty VisibleRegionProperty = DependencyProperty.Register(
            nameof(VisibleRegion),
            typeof(VisibleRegionInfo),
            typeof(VolumeViewModel),
            new FrameworkPropertyMetadata(new VisibleRegionInfo(0, 0, 10000, 10000, 256), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public VisibleRegionInfo VisibleRegion
        {
            get { return (VisibleRegionInfo)GetValue(VisibleRegionProperty); }
            set { SetValue(VisibleRegionProperty, value); }
        }

        public static readonly DependencyProperty CenterIndexProperty = DependencyProperty.Register(
            nameof(CenterIndex),
            typeof(int),
            typeof(VolumeViewModel),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, null, CoerceCenterIndex));

        public int CenterIndex
        {
            get { return (int)GetValue(CenterIndexProperty); }
            set { SetValue(CenterIndexProperty, value); }
        }

        private static object CoerceCenterIndex(DependencyObject d, object baseValue)
        {
            VolumeViewModel vm = (VolumeViewModel)d;
            int value = (int)baseValue;
            int max = vm.SectionViewModels == null || vm.SectionViewModels.Count == 0
                ? 0
                : vm.SectionViewModels.Count - 1;
            if (value < 0)
                return 0;
            if (value > max)
                return max;
            return value;
        }

        public string Name { get { return _Volume.Name; } }

        public bool IsLocal { get { return _Volume.IsLocal; } }

        public string DefaultVolumeTransform { get { return _Volume.DefaultVolumeTransform; } }

        public List<string> VolumeTransformNames { get { return _Volume.VolumeTransformNames; } }

        public static readonly DependencyProperty ActiveVolumeTransformProperty = DependencyProperty.Register(
            nameof(ActiveVolumeTransform),
            typeof(string),
            typeof(VolumeViewModel),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string ActiveVolumeTransform
        {
            get
            {
                string value = (string)GetValue(ActiveVolumeTransformProperty);
                return string.IsNullOrEmpty(value) ? DefaultVolumeTransform : value;
            }
            set { SetValue(ActiveVolumeTransformProperty, value); }
        }

        public ChannelInfo[] DefaultChannels { get { return _Volume.DefaultChannels; } set { _Volume.DefaultChannels = value; } }

        public string[] ChannelNames { get { return Volume.ChannelNames; } }

        public XDocument VolumeXML { get { return _Volume.VolumeElement.Document; } }

        public VolumeViewModel(Volume volume)
        {
            _Volume = volume;

            _MappingManager = new MappingManager(volume);

            SectionViewModels = new SortedList<int, SectionViewModel>(_Volume.Sections.Count);

            foreach (Section s in _Volume.Sections.Values)
            {
                SectionViewModel sectionViewModel = new SectionViewModel(volume, s, _MappingManager);
                SectionViewModels.Add(s.Number, sectionViewModel);
            }

            ActiveVolumeTransform = volume.DefaultVolumeTransform;
        }

        public string Host { get { return _Volume.Host; } }

        public int? NextLowerSectionNumber(int sectionNumber)
        {
            if (SectionViewModels == null || SectionViewModels.Count == 0)
                return null;

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
            if (SectionViewModels == null || SectionViewModels.Count == 0)
                return null;

            int HighestKey = SectionViewModels.Keys.Max();
            while (false == SectionViewModels.ContainsKey(sectionNumber))
            {
                if (sectionNumber > HighestKey)
                    return new int?();

                sectionNumber++;
            }

            return new int?(sectionNumber);
        }
    }
}
