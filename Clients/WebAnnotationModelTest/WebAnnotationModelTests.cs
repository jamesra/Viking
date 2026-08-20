using Viking.AnnotationServiceTypes.Interfaces;
using DBACTION = Viking.AnnotationServiceTypes.Interfaces.DBACTION;
using Duende.IdentityModel.Client;
using Microsoft.SqlServer.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Viking.Tokens;
using WebAnnotationModel;
using WebAnnotationModel.Objects;


namespace WebAnnotationModelTest
{
    [TestClass]
    public class WebAnnotationModelTests
    {
        readonly string Username = "VikingUnitTests";
        readonly string Password = "4%W%o06";
        readonly string VolumeName = "RC1Test";
        public System.Net.NetworkCredential TestCredentials;
        //static public EndpointAddress Endpoint;

        public static string Endpoint = "https://webdev.connectomes.utah.edu/RC1Test/Annotation/service.svc";
        public static string IdentityEndpoint = "https://identity.connectomes.utah.edu/";

        public static Viking.Tokens.BearerTokenHelper TokenHelper;
        public static Viking.Tokens.IdentityApiHelper ApiHelper;

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
            TokenHelper = new BearerTokenHelper()
            {
                IdentityServerURL = new Uri(IdentityEndpoint),
            };

            // Create IdentityApiHelper - need to determine IdentityApiURL (typically same host, port 6001)
            var identityApiUri = new UriBuilder(IdentityEndpoint)
            {
                Port = 6001
            }.Uri;

            ApiHelper = new IdentityApiHelper()
            {
                IdentityApiURL = identityApiUri
            };

            var token = await TokenHelper.RetrieveBearerToken(Username, Password);
            Assert.IsFalse(token.IsError, token.Error);

            var permissions = await ApiHelper.RetrieveUserVolumePermissions(token as TokenResponse, VolumeName);
            Assert.IsFalse(permissions is null || permissions.Length == 0, $"No permissions found for test user {Username} in volume {VolumeName}");

            List<string> list_permissions = new List<string>
            {
                "openid",
                "Viking.Annotation"
            };
            list_permissions.AddRange(permissions.Select(p => $"{VolumeName}.{p}"));

            var bearer_token_response = await TokenHelper.RetrieveBearerToken(Username, Password, list_permissions.ToArray());
            Assert.IsFalse(bearer_token_response.IsError, token.Error);

            TokenStore.BearerToken = bearer_token_response as TokenResponse;
            TokenStore.BearerTokenAuthority = IdentityEndpoint;
        }

        #region StructureTypes

        private LocationObj NewPopulatedLocation(StructureObj parent)
        {
            return new LocationObj(parent, SqlGeometry.Point(0, 0, 0).ToShape2D(), SqlGeometry.Point(0, 0, 0).ToShape2D(), 0, LocationType.POINT);
        }

        [TestMethod]
        public async Task TypesCreationTest()
        {
            await Store.StructureTypes.GetAll();

            foreach (StructureTypeObj type in Store.StructureTypes.RootObjects.Select(id => Store.StructureTypes[id]))
            {
                Debug.WriteLine(type.ToString()); 
            }

            StructureTypeObj test_stype = new StructureTypeObj
            {
                Name = "Test Structure"
            };

            long OriginalID = test_stype.ID; 

            EventLogger EventLog = new EventLogger(); 
            EventLog.SubscribeToCollectionChangedEvents(Store.StructureTypes);
              
            test_stype = await Store.StructureTypes.Create(test_stype);

            Assert.IsTrue(EventLog.listCollectionEvents.Count == 1);
            
            EventLog.PopObjectAddedEvent(test_stype);
            EventLog.SubscribeToPropertyChangingEvents(test_stype);
            EventLog.SubscribeToPropertyChangedEvents(test_stype);
            
            //Make sure we can fetch the new ID
            Assert.IsTrue(test_stype.ID > 0);
            StructureTypeObj queryOriginalObj = await Store.StructureTypes.GetObjectByID(OriginalID);
            Assert.IsNull(queryOriginalObj);

            //Test creating a structure with a parent
            StructureTypeObj testChildObj = new StructureTypeObj(test_stype)
            {
                Name = "Child of test structure"
            };
            testChildObj = await Store.StructureTypes.Create(testChildObj);

            EventLog.PopObjectAddedEvent(testChildObj);
            EventLog.SubscribeToPropertyChangingEvents(testChildObj);
            EventLog.SubscribeToPropertyChangedEvents(testChildObj);

            //Ensure the parent was provided the new child ID
            Assert.IsTrue(test_stype.Children.Contains(testChildObj));
            Assert.IsTrue(testChildObj.ID > 0);

            testChildObj.DBAction = DBACTION.DELETE; 
            test_stype.DBAction = DBACTION.DELETE; 
            await Store.StructureTypes.Save();

            Assert.IsTrue(EventLog.listCollectionEvents.Count == 1);
            EventLog.PopObjectRemovedEvent(new StructureTypeObj[] {test_stype, testChildObj} );

            //Make sure we can't fetch the deleted item
            StructureTypeObj queryObj = await Store.StructureTypes.GetObjectByID(test_stype.ID);
            Assert.IsNull(queryObj);

            queryObj = await Store.StructureTypes.GetObjectByID(testChildObj.ID);
            Assert.IsNull(queryObj);
        }

        [TestMethod]
        public async Task StructureChildCreationTest1()
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

            StructureTypeObj cellType = await Store.StructureTypes.GetObjectByID(1);
            StructureObj testObj = new StructureObj(cellType);
            LocationObj locObj = NewPopulatedLocation(testObj); 
            
            testObj.Label = "Test Structure";

            long OriginalID = testObj.ID;

            var createResult = await Store.Structures.Create(testObj, locObj);
            testObj = createResult.Structure;
            locObj = createResult.Location;
            StructureEventLog.PopObjectAddedEvent(testObj);
            LocationEventLog.PopObjectAddedEvent(locObj);

            //Make sure we can't fetch the new ID
            Assert.IsTrue(testObj.ID > 0);
            StructureObj queryObj = await Store.Structures.GetObjectByID(OriginalID);
            Assert.IsNull(queryObj);

            //Test creating a structure with a parent
            StructureObj testChildObj = new StructureObj(cellType)
            {
                Parent = testObj
            };
            LocationObj childLocObj = NewPopulatedLocation(testChildObj); 
            testChildObj.Label = "Child of test structure";
            var childCreateResult = await Store.Structures.Create(testChildObj, childLocObj);
            testChildObj = childCreateResult.Structure;
            childLocObj = childCreateResult.Location;
            
            Assert.IsTrue(testObj.Children.Contains(testChildObj));
            StructureEventLog.PopObjectAddedEvent(testChildObj);
            LocationEventLog.PopObjectAddedEvent(childLocObj);

            Assert.IsTrue(testChildObj.ID > 0);

            testChildObj.DBAction = DBACTION.DELETE;
            
            //Delete the objects
            await Store.Structures.Save();

            queryObj = await Store.Structures.GetObjectByID(testChildObj.ID);
            Assert.IsNull(queryObj);

            Assert.IsFalse(testObj.Children.Contains(testChildObj));

            StructureEventLog.PopObjectRemovedEvent(testChildObj);
             
            testObj.DBAction = DBACTION.DELETE;

            await Store.Structures.Save();
            StructureEventLog.PopObjectRemovedEvent(testObj);

            //Make sure we can't fetch the deleted item
            queryObj = await Store.Structures.GetObjectByID(testObj.ID);
            Assert.IsNull(queryObj);
              
            //Make sure the child objects were deleted too
            //Assert.IsNull(await Store.Locations.GetObjectByID(locObj.ID));
            //Assert.IsNull(await Store.Locations.GetObjectByID(childLocObj.ID));
        }

        [TestMethod]
        public async Task StructureLinkCreationTest1()
        { 
            EventLogger StructureEventLog = new EventLogger();
            StructureEventLog.SubscribeToCollectionChangedEvents(Store.Structures);

            EventLogger StructureLinkEventLog = new EventLogger();
            StructureLinkEventLog.SubscribeToCollectionChangedEvents(Store.StructureLinks);

            EventLogger LocationEventLog = new EventLogger();
            LocationEventLog.SubscribeToCollectionChangedEvents(Store.Locations);

            StructureTypeObj cellType = await Store.StructureTypes.GetObjectByID(1);
            StructureObj sourceStruct = new StructureObj(cellType);
            StructureObj targetStruct = new StructureObj(cellType);


            LocationObj sourceLocObj = NewPopulatedLocation(sourceStruct);
            LocationObj targetLocObj = NewPopulatedLocation(targetStruct);

            var sourceCreateResult = await Store.Structures.Create(sourceStruct, sourceLocObj);
            sourceStruct = sourceCreateResult.Structure;
            sourceLocObj = sourceCreateResult.Location;
            StructureEventLog.PopObjectAddedEvent(sourceStruct);
            LocationEventLog.PopObjectAddedEvent(sourceLocObj);
            var targetCreateResult = await Store.Structures.Create(targetStruct, targetLocObj);
            targetStruct = targetCreateResult.Structure;
            targetLocObj = targetCreateResult.Location;
            StructureEventLog.PopObjectAddedEvent(targetStruct);
            LocationEventLog.PopObjectAddedEvent(targetLocObj);

            await Store.Structures.Save();

            StructureLinkObj link = new StructureLinkObj(sourceStruct.ID, targetStruct.ID, false);
            link = await Store.StructureLinks.Create(link);
            Assert.AreEqual(link.DBAction, DBACTION.NONE);

            StructureLinkEventLog.PopObjectAddedEvent(link);

            Assert.AreEqual(sourceStruct.NumLinks, 1);
            Assert.AreEqual(targetStruct.NumLinks, 1);

            Assert.IsTrue(sourceStruct.LinksCopy.Contains(link));
            Assert.IsTrue(targetStruct.LinksCopy.Contains(link));

            //Check that we can adjust link properties
            /*We no longer toggle Bidirectional.  We delete and recreate the link.
             * link.Bidirectional = !link.Bidirectional;
            Assert.AreEqual(link.DBAction, DBACTION.UPDATE);
            await Store.StructureLinks.Save();
            */

            //Ensure our change was submitted, this should reset DBAction
            Assert.AreEqual(link.DBAction, DBACTION.NONE);
            

            //Remove the link
            await Store.StructureLinks.Remove(link);

            StructureLinkEventLog.PopObjectRemovedEvent(link); 

            Assert.AreEqual(sourceStruct.NumLinks, 0);
            Assert.AreEqual(targetStruct.NumLinks, 0);

            Assert.IsFalse(sourceStruct.LinksCopy.Contains(link));
            Assert.IsFalse(targetStruct.LinksCopy.Contains(link));

            await Store.StructureLinks.Save();

            await Store.Structures.Remove(sourceStruct);
            await Store.Structures.Remove(targetStruct);

            await Store.Structures.Save();

            StructureEventLog.PopObjectRemovedEvent(new object[] {sourceStruct, targetStruct});

            //Make sure the child objects were deleted too
            //Assert.IsNull(await Store.Locations.GetObjectByID(sourceLocObj.ID));
            //Assert.IsNull(await Store.Locations.GetObjectByID(targetLocObj.ID));
        }
        
        public async Task TestLocationPropertyEvents(LocationObj obj)
        {

            EventLogger LocationEventLog = new EventLogger();
            LocationEventLog.SubscribeToPropertyChangingEvents(obj);
            LocationEventLog.SubscribeToPropertyChangedEvents(obj);


            Assert.AreEqual(obj.DBAction, DBACTION.NONE);

            obj.OffEdge = !obj.OffEdge; 
            LocationEventLog.PopObjectPropertyChangingEvent(obj, "OffEdge");
            LocationEventLog.PopObjectPropertyChangingEvent(obj, "DBAction");
            LocationEventLog.PopObjectPropertyChangedEvent(obj, "DBAction");
            LocationEventLog.PopObjectPropertyChangedEvent(obj, "OffEdge");

            Assert.AreEqual(obj.DBAction, DBACTION.UPDATE);
            
            await Store.Locations.Save();

            Assert.AreEqual(obj.DBAction, DBACTION.NONE);
            Geometry.Vector2 oldPosition = obj.VolumePosition; 
            Geometry.Vector2 newPosition = new Geometry.Vector2(1,1);
             
            //obj.VolumeShape = newPosition;
            //LocationEventLog.PopObjectPropertyChangingEvent(obj, "VolumePosition");            
            //LocationEventLog.PopObjectPropertyChangedEvent(obj, "VolumePosition");

            //VolumePosition is special because it is not automatically updated.
            Assert.AreEqual(obj.DBAction, DBACTION.NONE);
        }
        
        [TestMethod]
        public async Task LocationCreationTest1()
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

            StructureTypeObj cellType = await Store.StructureTypes.GetObjectByID(1);
            StructureObj structObj = new StructureObj(cellType); 
            LocationObj locObj = new LocationObj(structObj, SqlGeometry.Point(0,0,0).ToShape2D(), SqlGeometry.Point(0,0,0).ToShape2D(), 1, LocationType.POINT);
            try
            {
                var structCreateResult = await Store.Structures.Create(structObj, locObj);
                structObj = structCreateResult.Structure;
                locObj = structCreateResult.Location;

                LocationEventLog.PopObjectAddedEvent(locObj);

                Assert.IsTrue(locObj.ID > 0);
                Assert.IsTrue(structObj.ID > 0);

                await TestLocationPropertyEvents(locObj); 

                //
                LocationObj linkedLoc = new LocationObj(structObj, SqlGeometry.Point(1, 1, 0).ToShape2D(), SqlGeometry.Point(1, 1, 0).ToShape2D(), 2, LocationType.POINT);
                linkedLoc = await Store.Locations.Create(linkedLoc, new long[] { locObj.ID });

                LocationEventLog.PopObjectAddedEvent(linkedLoc);
                LocationLinkEventLog.PopObjectAddedEvent(new LocationLinkObj(locObj.ID, linkedLoc.ID));

                //            Assert.IsTrue(linkedLoc.Links.Contains(locObj.ID));
                //            Assert.IsTrue(locObj.Links.Contains(linkedLoc.ID));

                Assert.IsTrue(linkedLoc.Links.Contains(locObj.ID));
                Assert.IsTrue(locObj.Links.Contains(linkedLoc.ID));

                await Store.LocationLinks.DeleteLink(locObj.ID, linkedLoc.ID);

                LocationLinkEventLog.PopObjectRemovedEvent(new LocationLinkObj(locObj.ID, linkedLoc.ID));

                Assert.IsFalse(linkedLoc.Links.Contains(locObj.ID));
                Assert.IsFalse(locObj.Links.Contains(linkedLoc.ID));
                 
                //Delete the structure
                structObj.DBAction = DBACTION.DELETE;

                bool result = await Store.Structures.Save();

                locObj.DBAction = DBACTION.DELETE;
                linkedLoc.DBAction = DBACTION.DELETE;
                await Store.Locations.Save();

                LocationEventLog.PopObjectRemovedEvent(new object[] { locObj, linkedLoc });

                structObj.DBAction = DBACTION.DELETE;
                await Store.Structures.Save();

                //Make sure we can't fetch the deleted item
                StructureObj queryStructObj = await Store.Structures.GetObjectByID(structObj.ID);
                Assert.IsNull(queryStructObj);

                LocationObj queryLocObj = await Store.Locations.GetObjectByID(locObj.ID);
                Assert.IsNull(queryLocObj);
            }
            finally
            {
                structObj.DBAction = DBACTION.DELETE; 
                bool result = await Store.Structures.Save();
            }

            //OK, check that the location objects and structure objects have no references and are GC'ed.
            System.GC.Collect();
        }

        #endregion 
    }
}
