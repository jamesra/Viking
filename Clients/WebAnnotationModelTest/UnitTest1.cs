using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.ServiceModel;
using WebAnnotationModel;
using WebAnnotationModel.Objects;
using WebAnnotationModel.Service; 
using System.Diagnostics; 


namespace WebAnnotationModelTest
{
    [TestClass]
    public class TestWebAnnotation
    {
        static public System.Net.NetworkCredential TestCredentials = new System.Net.NetworkCredential("VikingUnitTests", "4%w%o06");
        static public EndpointAddress Endpoint;

        [TestInitialize]
        public void Init()
        {
            WebAnnotationModel.State.EndpointAddress = new EndpointAddress("https://connectomes.utah.edu/Services/TestBinary/Annotate.svc");
            WebAnnotationModel.State.UserCredentials = TestCredentials; 
        }

        #region StructureTypes

        [TestMethod]
        public void TypesCreationTest1()
        {
            Store.StructureTypes.LoadStructureTypes();

            foreach (StructureTypeObj type in Store.StructureTypes.rootObjects.Values)
            {
                Debug.WriteLine(type.ToString()); 
            }
            
            StructureTypeObj testObj = new StructureTypeObj();
            testObj.Name = "Test Structure";

            long OriginalID = testObj.ID; 

            Store.StructureTypes.Add(testObj);

            Store.StructureTypes.Save();

            //Make sure we can't fetch the new ID
            Assert.IsTrue(testObj.ID > 0);
            StructureTypeObj queryObj = Store.StructureTypes.GetObjectByID(OriginalID);
            Assert.IsNull(queryObj);

            //Test creating a structure with a parent
            StructureTypeObj testChildObj = new StructureTypeObj(testObj);
            testChildObj.Name = "Child of test structure";
            Store.StructureTypes.Add(testChildObj);

            Store.StructureTypes.Save();

            Assert.IsTrue(testChildObj.ID > 0);

            testChildObj.DBAction = DBACTION.DELETE; 
            testObj.DBAction = DBACTION.DELETE; 
            Store.StructureTypes.Save(); 

            //Make sure we can't fetch the deleted item
            queryObj = Store.StructureTypes.GetObjectByID(testObj.ID);
            Assert.IsNull(queryObj);

            queryObj = Store.StructureTypes.GetObjectByID(testChildObj.ID);
            Assert.IsNull(queryObj);
        }

        [TestMethod]
        public void StructureCreationTest1()
        {
            /*
            foreach (StructureTypeObj type in Store.StructureTypes.rootObjects.Values)
            {
                Debug.WriteLine(type.ToString());
            }
            */
            StructureTypeObj cellType = Store.StructureTypes.GetObjectByID(1);
            StructureObj testObj = new StructureObj(cellType);
            testObj.Label = "Test Structure";

            long OriginalID = testObj.ID;

            Store.Structures.Add(testObj);

            Store.Structures.Save();

            //Make sure we can't fetch the new ID
            Assert.IsTrue(testObj.ID > 0);
            StructureObj queryObj = Store.Structures.GetObjectByID(OriginalID);
            Assert.IsNull(queryObj);

            //Test creating a structure with a parent
            StructureObj testChildObj = new StructureObj(cellType);
            testChildObj.Label = "Child of test structure";
            Store.Structures.Add(testChildObj);

            Store.Structures.Save();

            Assert.IsTrue(testChildObj.ID > 0);

            testChildObj.DBAction = DBACTION.DELETE;
            testObj.DBAction = DBACTION.DELETE;
            Store.Structures.Save();

            //Make sure we can't fetch the deleted item
            queryObj = Store.Structures.GetObjectByID(testObj.ID);
            Assert.IsNull(queryObj);

            queryObj = Store.Structures.GetObjectByID(testChildObj.ID);
            Assert.IsNull(queryObj);
        }

        [TestMethod]
        public void StructureLinkCreationTest1()
        {
            StructureTypeObj cellType = Store.StructureTypes.GetObjectByID(1);
            StructureObj sourceStruct = new StructureObj(cellType);
            StructureObj targetStruct = new StructureObj(cellType);

            Store.Structures.Add(sourceStruct);
            Store.Structures.Add(targetStruct);
            Store.Structures.Save();

            StructureLinkObj link = new StructureLinkObj(sourceStruct.ID, targetStruct.ID, false);
            Store.StructureLinks.Add(link);

            Store.StructureLinks.Save();

            Store.StructureLinks.Remove(link);

            Store.StructureLinks.Save();

            Store.Structures.Remove(sourceStruct);
            Store.Structures.Remove(targetStruct);

            Store.Structures.Save(); 
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
            StructureTypeObj cellType = Store.StructureTypes.GetObjectByID(1);
            StructureObj structObj = new StructureObj(cellType); 
            LocationObj locObj = new LocationObj(structObj, new Geometry.GridVector2(0,0), new Geometry.GridVector2(0,0), 1);
            Store.Structures.Create(structObj, locObj);

            Assert.IsTrue(locObj.ID > 0);
            Assert.IsTrue(structObj.ID > 0);

            //
            LocationObj linkedLoc = new LocationObj(structObj, new Geometry.GridVector2(1, 1), new Geometry.GridVector2(1, 1), 2);
            Store.Locations.Add(linkedLoc); 

            Store.Locations.Save();

//            Assert.IsTrue(linkedLoc.Links.Contains(locObj.ID));
//            Assert.IsTrue(locObj.Links.Contains(linkedLoc.ID));

            Store.LocationLinks.CreateLink(locObj.ID, linkedLoc.ID);

            Assert.IsTrue(linkedLoc.Links.Contains(locObj.ID));
            Assert.IsTrue(locObj.Links.Contains(linkedLoc.ID));

            Store.LocationLinks.DeleteLink(locObj.ID, linkedLoc.ID);

            Assert.IsFalse(linkedLoc.Links.Contains(locObj.ID));
            Assert.IsFalse(locObj.Links.Contains(linkedLoc.ID));

            //Delete the structure
            structObj.DBAction = DBACTION.DELETE; 

            bool result = Store.Structures.Save();
            
            locObj.DBAction = DBACTION.DELETE;
            Store.Locations.Save();

            structObj.DBAction = DBACTION.DELETE; 
            Store.Structures.Save();

            //Make sure we can't fetch the deleted item
            StructureObj queryStructObj = Store.Structures.GetObjectByID(structObj.ID);
            Assert.IsNull(queryStructObj);

            LocationObj queryLocObj = Store.Locations.GetObjectByID(locObj.ID);
            Assert.IsNull(queryLocObj);
        }

        #endregion 
    }
}
