using Microsoft.Practices.ServiceLocation;
using Prism.Mef.Modularity;
using Prism.Modularity;
using Prism.Regions;

namespace Jotunn.AnnotationView
{

    [ModuleExport(typeof(AnnotationViewModule), InitializationMode = InitializationMode.WhenAvailable, DependsOnModuleNames = new string[] { "AnnotationViewModelModule", "VikingVolumeViewModule"})]
    class AnnotationViewModule : IModule
    {
        #region IModule Members

        delegate void InitializeUIDelegate();



        void IModule.Initialize()
        {
            System.Diagnostics.Trace.WriteLine("AnnotationViewModule::Initialize()");

            InitializeUIDelegate InitUIDelegate = new InitializeUIDelegate(InitializeUI);

            System.Windows.Application.Current.Dispatcher.Invoke(InitUIDelegate, null); 
        }

        /// <summary>
        /// The initialization of UI must occur on the STA thread
        /// </summary>
        void InitializeUI()
        {

            IRegionManager regionManager = ServiceLocator.Current.GetInstance<IRegionManager>();
            AnnotationsOverlay overlay = new AnnotationsOverlay();
              
            regionManager.AddToRegion(Jotunn.Common.RegionNames.ViewOverlay, overlay);

           /* SectionGridControl sectionGrid = new SectionGridControl();

            

            //            Checkerboard checkerboard = new Checkerboard();

            //            regionManager.AddToRegion(Jotunn.RegionNames.View, checkerboard);


            //            PyramidViewer pyramidViewer = new PyramidViewer();

            //            Viking.VolumeViewModel.VolumeViewModel volume = Microsoft.Practices.ServiceLocation.ServiceLocator.Current.GetInstance<Viking.VolumeViewModel.VolumeViewModel>();
            //            SectionViewModel section = volume.SectionViewModels.Values[1];

            //pyramidViewer.TileMapping = section.DefaultMapping; 

            //regionManager.AddToRegion(Jotunn.RegionNames.Navigation, sectionList);
            //regionManager.AddToRegion(Jotunn.RegionNames.View, pyramidViewer);

            regionManager.AddToRegion(Jotunn.RegionNames.View, sectionGrid);

            SectionList sectionList = new SectionList();

            regionManager.AddToRegion(Jotunn.RegionNames.Navigation, sectionList);

            */
        }

        #endregion
    }
}
