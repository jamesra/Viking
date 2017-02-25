using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Prism.Modularity;
using Prism.Mef.Modularity;
using Microsoft.Practices.ServiceLocation;
using Prism.Regions;
using System.ComponentModel.Composition;  
using MonogameWPFLibrary.Views;

namespace MorphologyView
{
    [ModuleExport(typeof(MorphologyViewModule), InitializationMode = InitializationMode.WhenAvailable)]

    public class MorphologyViewModule : IModule
    {
        #region IModule Members

        delegate void InitializeUIDelegate();

        public MorphologyView morphologyView { get; set; }

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
            this.morphologyView = new MorphologyView();

            //            Checkerboard checkerboard = new Checkerboard();

            //            regionManager.AddToRegion(Jotunn.RegionNames.View, checkerboard);


            //            PyramidViewer pyramidViewer = new PyramidViewer();

            //            Viking.VolumeViewModel.VolumeViewModel volume = Microsoft.Practices.ServiceLocation.ServiceLocator.Current.GetInstance<Viking.VolumeViewModel.VolumeViewModel>();
            //            SectionViewModel section = volume.SectionViewModels.Values[1];

            //pyramidViewer.TileMapping = section.DefaultMapping; 

            //regionManager.AddToRegion(Jotunn.RegionNames.Navigation, sectionList);
            //regionManager.AddToRegion(Jotunn.RegionNames.View, pyramidViewer);

            regionManager.AddToRegion(Jotunn.Common.RegionNames.View, morphologyView);
            
        }

        #endregion

    }
}
