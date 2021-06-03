
using Prism.Mef.Modularity;
using Prism.Modularity;
//using Prism.MefExtensions;
//using Prism.MefExtensions.Modularity;
using System.ComponentModel.Composition;


namespace Viking.VolumeViewModel
{
    public class BackgroundThreadProgressReporter : Viking.Common.IProgressReporter
    {
        System.ComponentModel.BackgroundWorker worker;

        public BackgroundThreadProgressReporter(System.ComponentModel.BackgroundWorker worker)
        {
            this.worker = worker;
        }

        public void ReportProgress(double PercentProgress, string message)
        {
            worker.ReportProgress((int)PercentProgress, message);
        }

        public void TaskComplete()
        {
            worker.ReportProgress(100, "Task complete");
        }
    }

    [ModuleExport(typeof(VolumeViewModelModule), InitializationMode = InitializationMode.WhenAvailable)]
    public class VolumeViewModelModule : IModule
    {
        #region IModule Members

        delegate void InitializeUIDelegate(); 

        [Import]
        Jotunn.IShellParameters ShellParameters { get; set; }

        [Import("InitializeBackgroundWorker")]
        System.ComponentModel.BackgroundWorker InitializeBackgroundWorker { get; set; }

        [Export]
        Viking.VolumeViewModel.VolumeViewModel VolumeViewModel {get;set;}

        [Export]
        Viking.VolumeViewModel.VolumeViewModelSharedView VolumeViewModelMainPanel { get; set; } 

        void IModule.Initialize()
        {
            InitializeUIDelegate InitUIDelegate = new InitializeUIDelegate(InitializeUI);

            System.Windows.Application.Current.Dispatcher.Invoke(InitUIDelegate);                                                                                    
        }

        void InitializeUI()
        {
            string HostPath = ShellParameters.GetArgTable["HostPath"];
            //Create the model for the volume
            Global.Volume = new VolumeModel.Volume(HostPath, Global.CachePath, ShellParameters.GetXML, new BackgroundThreadProgressReporter(InitializeBackgroundWorker));

            this.VolumeViewModel = new Viking.VolumeViewModel.VolumeViewModel(Global.Volume, InitializeBackgroundWorker);
            this.VolumeViewModelMainPanel = new VolumeViewModelSharedView(this.VolumeViewModel);
        }

        #endregion
    }
}
