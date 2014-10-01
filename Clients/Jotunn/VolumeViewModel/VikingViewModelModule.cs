using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Practices.Prism.Modularity;
using Microsoft.Practices.Prism.MefExtensions;
using Microsoft.Practices.Prism.MefExtensions.Modularity;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;


namespace Viking.VolumeViewModel
{
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
            Global.Volume = new VolumeModel.Volume(HostPath, Global.CachePath, ShellParameters.GetXML, InitializeBackgroundWorker);

            this.VolumeViewModel = new Viking.VolumeViewModel.VolumeViewModel(Global.Volume, InitializeBackgroundWorker);
            this.VolumeViewModelMainPanel = new VolumeViewModelSharedView(this.VolumeViewModel);
        }

        #endregion
    }
}
