using Prism.Mef.Modularity;
using Prism.Modularity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.ServiceModel;
using System.Xml.Linq;
using Viking.Common;

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

            EndpointAddress endpointAddress = GetEndpointFromXML(volume.VolumeXML);
            if (endpointAddress != null)
                WebAnnotationModel.State.Endpoint = endpointAddress.Uri;
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

                        Uri volumeXmlEndpoint = null;
                        if (MappingElements.Count() > 0)
                        {
                            XElement MappingElement = MappingElements.First();
                            XAttribute EndpointAttribute = MappingElement.Attribute("Endpoint");
                            if (EndpointAttribute != null
                                && Uri.TryCreate(EndpointAttribute.Value, UriKind.Absolute, out Uri xmlUri))
                            {
                                volumeXmlEndpoint = xmlUri;
                            }
                        }

                        Uri? resolved = IdentityEndpoints.ResolveAnnotationServiceEndpoint(
                            volumeXmlEndpoint,
                            AccessibleVolumeSession.AnnotationServiceEndpoint?.ToString());
                        if (resolved != null)
                        {
                            endpoint = new EndpointAddress(resolved);
                            return endpoint;
                        }

                        break;
                    default:
                        break;
                }
            }

            if (AccessibleVolumeSession.AnnotationServiceEndpoint != null)
                return new EndpointAddress(AccessibleVolumeSession.AnnotationServiceEndpoint);

            return endpoint;
        }

       


        #endregion
    }
}
