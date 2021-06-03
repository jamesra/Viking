using Microsoft.Practices.ServiceLocation;
using Prism.Mef.Modularity;
using Prism.Modularity;
using Prism.Regions;
using System;
using System.Linq;

namespace MorphologyView
{
    [ModuleExport(typeof(MorphologyViewModule), InitializationMode = InitializationMode.WhenAvailable)]

    public class MorphologyViewModule : IModule
    {
        #region IModule Members

        delegate void InitializeUIDelegate();

        public MorphologyViewer morphologyView { get; set; }

        void IModule.Initialize()
        {
            System.Diagnostics.Trace.WriteLine("VikingVolumeViewModule::Initialize()");

            SqlServerTypes.Utilities.LoadNativeAssemblies(AppDomain.CurrentDomain.BaseDirectory);

            InitializeUIDelegate InitUIDelegate = new InitializeUIDelegate(InitializeUI);

            System.Windows.Application.Current.Dispatcher.Invoke(InitUIDelegate, null);
        }

        /// <summary>
        /// The initialization of UI must occur on the STA thread
        /// </summary>
        void InitializeUI()
        {  
            IRegionManager regionManager = ServiceLocator.Current.GetInstance<IRegionManager>();
            this.morphologyView = new MorphologyViewer(); 

            regionManager.AddToRegion(Jotunn.Common.RegionNames.View, morphologyView);
            
            System.Threading.Tasks.Task t = new System.Threading.Tasks.Task(() =>
            {
                AnnotationVizLib.MorphologyGraph graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(new ulong[] { 8883 }, true, new Uri("http://webdev.connectomes.utah.edu/RC1Test/OData"));
                //AnnotationVizLib.MorphologyGraph graph = AnnotationVizLib.SimpleODataMorphologyFactory.FromOData(new long[] { 180 }, true, new Uri("http://webdev.connectomes.utah.edu/RC1Test/OData"));
                if (graph != null)
                {
                    //System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => ((MorphologyView.ViewModels.MorphologyGraphViewModel)morphologyView.DataContext).Graph = graph.Subgraphs.Values.First()));
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => morphologyView.DataContext = graph.Subgraphs.Values.First()));
                    System.Diagnostics.Trace.WriteLine("Loaded the morphology graph!");
                }
                else
                {
                    throw new ArgumentException("NO graph found on module initialize");
                }
            });
              

            t.Start();
        }

        #endregion
    }
}
