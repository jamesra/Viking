using Prism.Mef.Modularity;
using Prism.Modularity;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.ServiceModel;
using System.Xml.Linq;

namespace Viking.VolumeView
{
    [ModuleExport(typeof(AnnotationViewModelModule), InitializationMode = InitializationMode.WhenAvailable, DependsOnModuleNames = new string[] { "VikingVolumeViewModule" })]
    class AnnotationViewModelModule : IModule
    {
        public static EndpointAddress Endpoint = null;

        #region IModule Members

        delegate void InitializeUIDelegate();

        void IModule.Initialize()
        {
            System.Diagnostics.Trace.WriteLine("AnnotationViewModelModule::Initialize()");

            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            Viking.VolumeViewModel.VolumeViewModel volume = Microsoft.Practices.ServiceLocation.ServiceLocator.Current.GetInstance<Viking.VolumeViewModel.VolumeViewModel>();

            WebAnnotationModel.State.Endpoint = GetEndpointFromXML(volume.VolumeXML).Uri;
            WebAnnotationModel.State.UserCredentials = new System.Net.NetworkCredential("anonymous", "connectome"); 
        }

        /// <summary>
        /// The initialization of UI must occur on the STA thread
        /// </summary>
        void InitializeUI()
        {
         
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

        static EndpointAddress GetEndpointFromXML(XDocument XMLMapping)
        {

            EndpointAddress endpoint = null;
            //Examine the mappings and determine if we can map the volume
            IEnumerable<XElement> VolumeElements = XMLMapping.Elements().Where(elem => elem.Name.LocalName == "Volume");

            foreach (XElement elem in VolumeElements)
            {
                //Fetch the name if we know it
                switch (elem.Name.LocalName)
                {
                    case "Volume":
                        IEnumerable<XElement> SettingsElements = elem.Elements().Where(e => e.Name.LocalName == "DefaultWebAnnotationUserSettings");
                        if (SettingsElements.Count() > 0)
                        {
                            //UserSettingsElement = SettingsElements.First();
                        }

                        IEnumerable<XElement> MappingElements = elem.Elements().Where(e => e.Name.LocalName == "VolumeToEndpoint");

                        if (MappingElements.Count() == 0)
                            break;

                        XElement MappingElement = MappingElements.First();

                        XAttribute EndpointAttribute = MappingElement.Attribute("Endpoint");
                        if (EndpointAttribute == null)
                            break;

                        /*
                        XAttribute AuthenticationAttribute = MappingElement.Attribute("Authentication");
                        if (AuthenticationAttribute != null)
                        {
                            Global._AuthenticationAddress = new EndpointAddress(AuthenticationAttribute.Value);
                            ValidateUser(); 
                        }
                        */
#if DEBUG
                        endpoint = new EndpointAddress(EndpointAttribute.Value);
                        //                        WebAnnotationModel.State.EndpointAddress = new EndpointAddress("https://connectomes.utah.edu/Services/TestBinary/Annotate.svc");
#else
                        endpoint = new EndpointAddress(EndpointAttribute.Value);                       
#endif
                        return endpoint;
                    default:
                        break;
                }
            }

            return endpoint;
        }

       


        #endregion
    }
}
