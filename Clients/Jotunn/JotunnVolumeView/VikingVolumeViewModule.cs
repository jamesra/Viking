using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Prism.Modularity;
using Prism.Mef.Modularity;
using Microsoft.Practices.ServiceLocation;
using Prism.Regions; 
using System.ComponentModel.Composition;
using Viking.VolumeViewModel;
using Jotunn.Common;

namespace Viking.VolumeView
{

    [ModuleExport(typeof(VikingVolumeViewModule), InitializationMode = InitializationMode.WhenAvailable, DependsOnModuleNames = new string[] { "VolumeViewModelModule" })]
    public class VikingVolumeViewModule : IModule 
    {
        #region IModule Members

        delegate void InitializeUIDelegate();

        public SectionGridControl sectionGrid { get; set; } 
        
        void IModule.Initialize()
        {
            System.Diagnostics.Trace.WriteLine("VikingVolumeViewModule::Initialize()");


            InitializeUIDelegate InitUIDelegate = new InitializeUIDelegate(InitializeUI);

            System.Windows.Application.Current.Dispatcher.Invoke(InitUIDelegate, null);
        }

        /// <summary>
        /// The initialization of UI must occur on the STA thread
        /// </summary>
        void InitializeUI()
        {
            
            IRegionManager regionManager = ServiceLocator.Current.GetInstance<IRegionManager>();
            this.sectionGrid = new SectionGridControl();
                        
//            Checkerboard checkerboard = new Checkerboard();

//            regionManager.AddToRegion(Jotunn.RegionNames.View, checkerboard);


//            PyramidViewer pyramidViewer = new PyramidViewer();

//            Viking.VolumeViewModel.VolumeViewModel volume = Microsoft.Practices.ServiceLocation.ServiceLocator.Current.GetInstance<Viking.VolumeViewModel.VolumeViewModel>();
//            SectionViewModel section = volume.SectionViewModels.Values[1];

            //pyramidViewer.TileMapping = section.DefaultMapping; 

            //regionManager.AddToRegion(Jotunn.RegionNames.Navigation, sectionList);
            //regionManager.AddToRegion(Jotunn.RegionNames.View, pyramidViewer);

            regionManager.AddToRegion(Jotunn.Common.RegionNames.View, sectionGrid);

            SectionList sectionList = new SectionList();

            regionManager.AddToRegion(Jotunn.Common.RegionNames.Navigation, sectionList);

            
        }

        #endregion
    }
}
