using Viking.AnnotationServiceTypes.Interfaces;
using AnnotationService.Types;
using IdentityModel.Client;
using Microsoft.SqlServer.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Viking.Tokens;
using WebAnnotationModel;
using WebAnnotationModel.Objects;


namespace WebAnnotationModelTest
{
    [TestClass]
    public class WebAnnotationModelTests
    {
        string Username = "VikingUnitTests";
        string Password = "4%W%o06";
        string VolumeName = "RC1Test";
        public System.Net.NetworkCredential TestCredentials;
        //static public EndpointAddress Endpoint;

        static public string Endpoint = "https://webdev.connectomes.utah.edu/RC1Test/Annotation/service.svc";
        static public string IdentityEndpoint = "https://identity.connectomes.utah.edu/";

        static public Viking.Tokens.IdentityServerHelper TokenHelper;

        [TestInitialize]
        public void Init()
        {
            TestCredentials = new System.Net.NetworkCredential(Username, Password);
            WebAnnotationModel.State.Endpoint = new Uri(Endpoint);
            //WebAnnotationModel.State.UserCredentials = TestCredentials;

            System.Net.ServicePointManager.ServerCertificateValidationCallback =
                            ((sender, certificate, chain, sslPolicyErrors) => true);

            WebAnnotationModel.State.UseAsynchEvents = false;

            InitIdentity().Wait();
        }

        private async System.Threading.Tasks.Task InitIdentity()
        {
            TokenHelper = new IdentityServerHelper()
            {
                IdentityServerURL = new Uri(IdentityEndpoint),
            };

            var token = await TokenHelper.RetrieveBearerToken(Username, Password);
            Assert.IsFalse(token.IsError, token.Error);

            var permissions = await TokenHelper.RetrieveUserVolumePermissions(token as TokenResponse, VolumeName);
            Assert.IsFalse(permissions == null || permissions.Length == 0, $"No permissions found for test user {Username} in volume {VolumeName}");

            List<string> list_permissions = new List<string>();
            list_permissions.Add("openid");
            list_permissions.Add("Viking.Annotation");
            list_permissions.AddRange(permissions.Select(p => $"{VolumeName}.{p}"));

            var bearer_token_response = await TokenHelper.RetrieveBearerToken(Username, Password, list_permissions.ToArray());
            Assert.IsFalse(bearer_token_response.IsError, token.Error);

            TokenInjector.BearerToken = bearer_token_response as TokenResponse;
            TokenInjector.BearerTokenAuthority = IdentityEndpoint;
        }

        #region StructureTypes

        private LocationObj NewPopulatedLocation(StructureObj parent)
        {
            return  new LocationObj(parent, SqlGeometry.Point(0, 0, 0), SqlGeometry.Point(0, 0, 0), 0, LocationType.POINT);
        }

        [TestMethod]
        public void TypesCreationTest()
        {
            Store.StructureTypes.LoadStructureTypes();

            foreach (StructureTypeObj type in Store.StructureTypes.RootObjects.Select(id => Store.StructureTypes[id]))
            {
                Debug.WriteLine(type.ToString()); 
            }
            
            StructureTypeObj test_stype = new StructureTypeObj();
            test_stype.Name = "Test Structure";

            long OriginalID = test_stype.ID; 

            EventLogger EventLog = new EventLogger(); 
            EventLog.SubscribeToCollectionChangedEvents(Store.StructureTypes);
              
            test_stype = Store.StructureTypes.Create(test_stype);

            Assert.IsTrue(EventLog.listCollectionEvents.Count == 1);
            
            EventLog.PopObjectAddedEvent(test_stype);
            EventLog.SubscribeToPropertyChangingEvents(test_stype);
            EventLog.SubscribeToPropertyChangedEvents(test_stype);
            
            //Make sure we can fetch the new ID
            Assert.IsTrue(test_stype.ID > 0);
            StructureTypeObj queryOriginalObj = Store.StructureTypes.GetObjectByID(OriginalID);
            Assert.IsNull(queryOriginalObj);
             
            //Test creating a structure with a parent
            StructureTypeObj testChildObj = new StructureTypeObj(test_stype);
            testChildObj.Name = "Child of test structure";
            testChildObj = Store.StructureTypes.Create(testChildObj);

            EventLog.PopObjectAddedEvent(testChildObj);
            EventLog.SubscribeToPropertyChangingEvents(testChildObj);
            EventLog.SubscribeToPropertyChangedEvents(testChildObj);

            //Ensure the parent was provided the new child ID
            Assert.IsTrue(test_stype.Children.Contains(testChildObj));
            Assert.IsTrue(testChildObj.ID > 0);

            testChildObj.DBAction = AnnotationService.Types.DBACTION.DELETE; 
            test_stype.DBAction = AnnotationService.Types.DBACTION.DELETE; 
            Store.StructureTypes.Save();

            Assert.IsTrue(EventLog.listCollectionEvents.Count == 1);
            EventLog.PopObjectRemovedEvent(new StructureTypeObj[] {test_stype, testChildObj} );

            //Make sure we can't fetch the deleted item
            StructureTypeObj queryObj = Store.StructureTypes.GetObjectByID(test_stype.ID);
            Assert.IsNull(queryObj);

            queryObj = Store.StructureTypes.GetObjectByID(testChildObj.ID);
            Assert.IsNull(queryObj);
        }

        [TestMethod]
        public void StructureChildCreationTest1()
        {
            /*
            foreach (StructureTypeObj type in Store.StructureTypes.rootObjects.Values)
            {
                Debug.WriteLine(type.ToString());
            }
            */ 
            EventLogger StructureEventLog = new EventLogger();
            StructureEventLog.SubscribeToCollectionChangedEvents(Store.Structures);

            EventLogger StructureLinkEventLog = new EventLogger();
            StructureLinkEventLog.SubscribeToCollectionChangedEvents(Store.StructureLinks);

            EventLogger LocationEventLog = new EventLogger();
            LocationEventLog.SubscribeToCollectionChangedEvents(Store.Locations);

            StructureTypeObj cellType = Store.StructureTypes.GetObjectByID(1);
            StructureObj testObj = new StructureObj(cellType);
            LocationObj locObj = NewPopulatedLocation(testObj); 
            
            testObj.Label = "Test Structure";

            long OriginalID = testObj.ID;
            LocationObj created_loc; 

            testObj = Store.Structures.Create(testObj, locObj, out created_loc);
            locObj = created_loc;
            StructureEventLog.PopObjectAddedEvent(testObj);
            LocationEventLog.PopObjectAddedEvent(locObj);

            //Make sure we can't fetch the new ID
            Assert.IsTrue(testObj.ID > 0);
            StructureObj queryObj = Store.Structures.GetObjectByID(OriginalID);
            Assert.IsNull(queryObj);

            //Test creating a structure with a parent
            StructureObj testChildObj = new StructureObj(cellType);
            testChildObj.Parent = testObj; 
            LocationObj childLocObj = NewPopulatedLocation(testChildObj); 
            testChildObj.Label = "Child of test structure";
            testChildObj = Store.Structures.Create(testChildObj, childLocObj, out created_loc);
            childLocObj = created_loc;
            
            Assert.IsTrue(testObj.Children.Contains(testChildObj));
            StructureEventLog.PopObjectAddedEvent(testChildObj);
            LocationEventLog.PopObjectAddedEvent(childLocObj);

            Assert.IsTrue(testChildObj.ID > 0);

            testChildObj.DBAction = AnnotationService.Types.DBACTION.DELETE;
            
            //Delete the objects
            Store.Structures.Save();

            queryObj = Store.Structures.GetObjectByID(testChildObj.ID);
            Assert.IsNull(queryObj);

            Assert.IsFalse(testObj.Children.Contains(testChildObj));

            StructureEventLog.PopObjectRemovedEvent(testChildObj);
             
            testObj.DBAction = AnnotationService.Types.DBACTION.DELETE;

            Store.Structures.Save();
            StructureEventLog.PopObjectRemovedEvent(testObj);

            //Make sure we can't fetch the deleted item
            queryObj = Store.Structures.GetObjectByID(testObj.ID);
            Assert.IsNull(queryObj);
              
            //Make sure the child objects were deleted too
            //Assert.IsNull(Store.Locations.GetObjectByID(locObj.ID, true));
            //Assert.IsNull(Store.Locations.GetObjectByID(childLocObj.ID,true));
        }

        [TestMethod]
        public void StructureLinkCreationTest1()
        { 
            EventLogger StructureEventLog = new EventLogger();
            StructureEventLog.SubscribeToCollectionChangedEvents(Store.Structures);

            EventLogger StructureLinkEventLog = new EventLogger();
            StructureLinkEventLog.SubscribeToCollectionChangedEvents(Store.StructureLinks);

            EventLogger LocationEventLog = new EventLogger();
            LocationEventLog.SubscribeToCollectionChangedEvents(Store.Locations);

            StructureTypeObj cellType = Store.StructureTypes.GetObjectByID(1);
            StructureObj sourceStruct = new StructureObj(cellType);
            StructureObj targetStruct = new StructureObj(cellType);


            LocationObj sourceLocObj = NewPopulatedLocation(sourceStruct);
            LocationObj targetLocObj = NewPopulatedLocation(targetStruct);

            sourceStruct = Store.Structures.Create(sourceStruct, sourceLocObj, out sourceLocObj);
            StructureEventLog.PopObjectAddedEvent(sourceStruct);
            LocationEventLog.PopObjectAddedEvent(sourceLocObj);
            targetStruct = Store.Structures.Create(targetStruct, targetLocObj, out targetLocObj);
            StructureEventLog.PopObjectAddedEvent(targetStruct);
            LocationEventLog.PopObjectAddedEvent(targetLocObj);

            Store.Structures.Save();

            StructureLinkObj link = new StructureLinkObj(sourceStruct.ID, targetStruct.ID, false);
            link = Store.StructureLinks.Create(link);
            Assert.AreEqual(link.DBAction, AnnotationService.Types.DBACTION.NONE);

            StructureLinkEventLog.PopObjectAddedEvent(link);

            Assert.AreEqual(sourceStruct.NumLinks, 1);
            Assert.AreEqual(targetStruct.NumLinks, 1);

            Assert.IsTrue(sourceStruct.LinksCopy.Contains(link));
            Assert.IsTrue(targetStruct.LinksCopy.Contains(link));

            //Check that we can adjust link properties
            /*We no longer toggle Bidirectional.  We delete and recreate the link.
             * link.Bidirectional = !link.Bidirectional;
            Assert.AreEqual(link.DBAction, DBACTION.UPDATE);
            Store.StructureLinks.Save();
            */

            //Ensure our change was submitted, this should reset DBAction
            Assert.AreEqual(link.DBAction, AnnotationService.Types.DBACTION.NONE);
            

            //Remove the link
            Store.StructureLinks.Remove(link);

            StructureLinkEventLog.PopObjectRemovedEvent(link); 

            Assert.AreEqual(sourceStruct.NumLinks, 0);
            Assert.AreEqual(targetStruct.NumLinks, 0);

            Assert.IsFalse(sourceStruct.LinksCopy.Contains(link));
            Assert.IsFalse(targetStruct.LinksCopy.Contains(link));

            Store.StructureLinks.Save();

            Store.Structures.Remove(sourceStruct);
            Store.Structures.Remove(targetStruct);

            Store.Structures.Save();

            StructureEventLog.PopObjectRemovedEvent(new object[] {sourceStruct, targetStruct});

            //Make sure the child objects were deleted too
            //Assert.IsNull(Store.Locations.GetObjectByID(sourceLocObj.ID, true));
            //Assert.IsNull(Store.Locations.GetObjectByID(targetLocObj.ID, true));
        }
        
        public void TestLocationPropertyEvents(LocationObj obj)
        {

            EventLogger LocationEventLog = new EventLogger();
            LocationEventLog.SubscribeToPropertyChangingEvents(obj);
            LocationEventLog.SubscribeToPropertyChangedEvents(obj);


            Assert.AreEqual(obj.DBAction, AnnotationService.Types.DBACTION.NONE);

            obj.OffEdge = !obj.OffEdge; 
            LocationEventLog.PopObjectPropertyChangingEvent(obj, "OffEdge");
            LocationEventLog.PopObjectPropertyChangingEvent(obj, "DBAction");
            LocationEventLog.PopObjectPropertyChangedEvent(obj, "DBAction");
            LocationEventLog.PopObjectPropertyChangedEvent(obj, "OffEdge");

            Assert.AreEqual(obj.DBAction, AnnotationService.Types.DBACTION.UPDATE);
            
            Store.Locations.Save();

            Assert.AreEqual(obj.DBAction, AnnotationService.Types.DBACTION.NONE);
            Geometry.GridVector2 oldPosition = obj.VolumePosition; 
            Geometry.GridVector2 newPosition = new Geometry.GridVector2(1,1);
             
            //obj.VolumeShape = newPosition;
            //LocationEventLog.PopObjectPropertyChangingEvent(obj, "VolumePosition");            
            //LocationEventLog.PopObjectPropertyChangedEvent(obj, "VolumePosition");

            //VolumePosition is special because it is not automatically updated.
            Assert.AreEqual(obj.DBAction, AnnotationService.Types.DBACTION.NONE);
        }
        
        [TestMethod]
        public void LocationCreationTest1()
        {
            /*
            foreach (StructureTypeObj type in Store.StructureTypes.rootObjects.Values)
            {
                Debug.WriteLine(type.ToString());
            }
            */
            EventLogger LocationEventLog = new EventLogger();
            LocationEventLog.SubscribeToCollectionChangedEvents(Store.Locations);
             
            EventLogger LocationLinkEventLog = new EventLogger();
            LocationLinkEventLog.SubscribeToCollectionChangedEvents(Store.LocationLinks);

            StructureTypeObj cellType = Store.StructureTypes.GetObjectByID(1);
            StructureObj structObj = new StructureObj(cellType); 
            LocationObj locObj = new LocationObj(structObj, SqlGeometry.Point(0,0,0), SqlGeometry.Point(0,0,0), 1, LocationType.POINT);
            try
            {
                structObj = Store.Structures.Create(structObj, locObj, out locObj);

                LocationEventLog.PopObjectAddedEvent(locObj);

                Assert.IsTrue(locObj.ID > 0);
                Assert.IsTrue(structObj.ID > 0);

                TestLocationPropertyEvents(locObj); 

                //
                LocationObj linkedLoc = new LocationObj(structObj, SqlGeometry.Point(1, 1, 0), SqlGeometry.Point(1, 1, 0), 2, LocationType.POINT);
                linkedLoc = Store.Locations.Create(linkedLoc, new long[] { locObj.ID });

                LocationEventLog.PopObjectAddedEvent(linkedLoc);
                LocationLinkEventLog.PopObjectAddedEvent(new LocationLinkObj(locObj.ID, linkedLoc.ID));

                //            Assert.IsTrue(linkedLoc.Links.Contains(locObj.ID));
                //            Assert.IsTrue(locObj.Links.Contains(linkedLoc.ID));

                Assert.IsTrue(linkedLoc.Links.Contains(locObj.ID));
                Assert.IsTrue(locObj.Links.Contains(linkedLoc.ID));

                Store.LocationLinks.DeleteLink(locObj.ID, linkedLoc.ID);

                LocationLinkEventLog.PopObjectRemovedEvent(new LocationLinkObj(locObj.ID, linkedLoc.ID));

                Assert.IsFalse(linkedLoc.Links.Contains(locObj.ID));
                Assert.IsFalse(locObj.Links.Contains(linkedLoc.ID));
                 
                //Delete the structure
                structObj.DBAction = AnnotationService.Types.DBACTION.DELETE;

                bool result = Store.Structures.Save();

                locObj.DBAction = AnnotationService.Types.DBACTION.DELETE;
                linkedLoc.DBAction = AnnotationService.Types.DBACTION.DELETE;
                Store.Locations.Save();

                LocationEventLog.PopObjectRemovedEvent(new object[] { locObj, linkedLoc });

                structObj.DBAction = AnnotationService.Types.DBACTION.DELETE;
                Store.Structures.Save();

                //Make sure we can't fetch the deleted item
                StructureObj queryStructObj = Store.Structures.GetObjectByID(structObj.ID);
                Assert.IsNull(queryStructObj);

                LocationObj queryLocObj = Store.Locations.GetObjectByID(locObj.ID);
                Assert.IsNull(queryLocObj);
            }
            finally
            {
                structObj.DBAction = AnnotationService.Types.DBACTION.DELETE; 
                bool result = Store.Structures.Save();
            }

            //OK, check that the location objects and structure objects have no references and are GC'ed.
            System.GC.Collect();
        }

        #endregion 
    }
}
