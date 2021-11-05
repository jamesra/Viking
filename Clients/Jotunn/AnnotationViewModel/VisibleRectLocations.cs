using Jotunn.Common;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using Viking.VolumeViewModel;
using WebAnnotationModel;
using WebAnnotationModel.Objects;


namespace AnnotationViewModel
{
    public class VisibleRectLocations : DependencyObject
    {        
        private readonly TileMappingViewModel TileMapping = null;

        public readonly long SectionNumber; 

        private static readonly DependencyProperty LocationsProperty;
        public ObservableCollection<LocationViewModel> Locations
        {
            get { return (ObservableCollection<LocationViewModel>)this.GetValue(LocationsProperty); }
            set { this.SetValue(LocationsProperty, value); }
        }

        private static readonly DependencyProperty VisibleRegionProperty;

        /// <summary>
        /// The visible area
        /// </summary>
        public VisibleRegionInfo VisibleRegion
        {
            get { return (VisibleRegionInfo)this.GetValue(VisibleRegionProperty); }
            set { this.SetValue(VisibleRegionProperty, value); }
        }

        private static void OnVisibleRegionChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            VisibleRectLocations vrl = o as VisibleRectLocations;
            VisibleRegionInfo visibleregion = (VisibleRegionInfo)e.NewValue;

            vrl.UpdateScreenPositionForLocations(visibleregion);
        }


        static VisibleRectLocations()
        {
            VisibleRectLocations.VisibleRegionProperty = DependencyProperty.Register("VisibleRegion",
                                                                                   typeof(VisibleRegionInfo),
                                                                                   typeof(VisibleRectLocations),
                                                                                   new FrameworkPropertyMetadata(null,
                                                                                                                 FrameworkPropertyMetadataOptions.AffectsRender,
                                                                                                                 new PropertyChangedCallback(OnVisibleRegionChanged)));

            VisibleRectLocations.LocationsProperty = DependencyProperty.Register("Tiles",
                                                                                  typeof(ObservableCollection<LocationViewModel>),
                                                                                  typeof(VisibleRectLocations),
                                                                                  new FrameworkPropertyMetadata(new ObservableCollection<LocationViewModel>(),
                                                                                                                FrameworkPropertyMetadataOptions.AffectsRender));
        }

       
         
        protected void UpdateScreenPositionForLocations(VisibleRegionInfo visibleregion)
        {
            foreach (LocationViewModel lvm in this.Locations)
            {
                double X = lvm.VolumePosition.X - visibleregion.VisibleRect.Left;
                double Y = lvm.VolumePosition.Y - visibleregion.VisibleRect.Bottom;

                lvm.ScreenPosition = new Point(X / visibleregion.Downsample, Y / visibleregion.Downsample);
            }

        }

        public VisibleRectLocations(TileMappingViewModel tileMapping, long SectionNumber)
        {
            this.TileMapping = tileMapping;
            this.SectionNumber = SectionNumber;
             
            WebAnnotationModel.Store.Locations.OnCollectionChanged += this.OnLocationCollectionChanged;
            ConcurrentDictionary<long, LocationObj> locsForSection = WebAnnotationModel.Store.Locations.GetObjectsForSection(1);
            UpdateCollectionWithLocations(locsForSection);
        }

        private void OnLocationCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        { 
            ConcurrentDictionary<long, LocationObj> locsForSection = WebAnnotationModel.Store.Locations.GetLocalObjectsForSection(1);
            WebAnnotationModel.Store.Locations.OnCollectionChanged -= this.OnLocationCollectionChanged;

            UpdateCollectionWithLocationsCaller d = new UpdateCollectionWithLocationsCaller(this.UpdateCollectionWithLocations);
            Dispatcher.BeginInvoke(d, locsForSection);            
        }

        private delegate void UpdateCollectionWithLocationsCaller(ConcurrentDictionary<long, LocationObj> locsForSection);

        private void UpdateCollectionWithLocations(ConcurrentDictionary<long, LocationObj> locsForSection)
        {
            this.Locations.Clear();
            List<LocationViewModel> listLocViewModels = new List<LocationViewModel>(locsForSection.Count);
            foreach (LocationObj locObj in locsForSection.Values)
            {
                LocationViewModel lvm = new LocationViewModel(locObj);
                 
                Point VolumePosition;
                if (this.TileMapping == null)
                {
                    lvm.VolumePosition = lvm.SectionPosition;
                }
                else
                {
                    bool MapSuccess = this.TileMapping.TrySectionToVolume(lvm.SectionPosition, out VolumePosition);
                    if (!MapSuccess)
                        continue;

                    lvm.VolumePosition = VolumePosition;
                }

                

                this.Locations.Add(lvm);
            } 
        }

    }
}
