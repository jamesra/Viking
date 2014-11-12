using System;
using System.Collections.Generic; 
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using WebAnnotationTest.AnnotationService;
using Annotation;
using System.Security.Principal;

namespace WebServiceTest
{


    public static class Parameters
    {
        public static string TestDatabaseName = "Empty";
    }

    /// <summary>
    ///This is a test class for AnnotationServiceImplTest and is intended
    ///to contain all AnnotationServiceImplTest Unit Tests
    ///</summary>
    [TestClass()]
    public class AnnotationServiceImplTest
    {

        private void AddPrincipalToThread()
        {
            GenericIdentity ident = new GenericIdentity("Test");
            string[] roles = new string[] { @"Admin", @"Modify", @"Read" };
            GenericPrincipal principle = new GenericPrincipal(ident, roles);

            System.Threading.Thread.CurrentPrincipal = principle;
        }

        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        //#region Additional test attributes
        //// 
        ////You can use the following additional attributes as you write your tests:
        ////
        ////Use ClassInitialize to run code before running the first test in the class
        ////[ClassInitialize()]
        ////public static void MyClassInitialize(TestContext testContext)
        ////{
        ////}
        ////
        ////Use ClassCleanup to run code after all tests in a class have run
        ////[ClassCleanup()]
        ////public static void MyClassCleanup()
        ////{
        ////}
        ////
        ////Use TestInitialize to run code before running each test
        ////[TestInitialize()]
        ////public void MyTestInitialize()
        ////{
        ////}
        ////
        ////Use TestCleanup to run code after each test has run
        ////[TestCleanup()]
        ////public void MyTestCleanup()
        ////{
        ////}
        ////
        //#endregion


        ///// <summary>
        /////A test for UpdateStructureTypes
        /////</summary>
        //[TestMethod()]
        //public void UpdateStructureTypesTest()
        //{
        //    AnnotateStructureTypesClient proxy = new WebAnnotationTest.AnnotationService.AnnotateStructureTypesClient();
        //    StructureType t = proxy.GetStructureTypeByID(1);

        //    /* Test Update */

        //    string OriginalName = t.Name;
        //    t.Name = "UpdateStructureTypesTest";
        //    t.DBAction = DBACTION.UPDATE;

        //    proxy.UpdateStructureTypes(new List<StructureType>( new StructureType[] { t }));

        //    t = proxy.GetStructureTypeByID(1);

        //    Debug.Assert(t.Name == "UpdateStructureTypesTest");
        //    t.Name = "Test";
        //    t.DBAction = DBACTION.UPDATE;

        //    proxy.UpdateStructureTypes(new List<StructureType>(new StructureType[] { t }));

        //    t = proxy.GetStructureTypeByID(1);
        //    Debug.Assert(t.Name == OriginalName);

        //    /* Test Insert */

        //    StructureType newType = new StructureType();
        //    newType.Name = "InsertTest";
        //    newType.ParentID = 1;
        //    newType.MarkupType = "Point";
        //    newType.Abstract = false;
        //    newType.DBAction = DBACTION.INSERT;

        //    proxy.UpdateStructureTypes(new List<StructureType>(new StructureType[] { newType }));

        //    List<StructureType> allTypes = proxy.GetStructureTypes();
        //    StructureType insertedType = null;
        //    foreach (StructureType s in allTypes)
        //    {
        //        if (s.Name == "InsertTest")
        //        {
        //            insertedType = s;
        //            break;
        //        }
        //    }

        //    Debug.Assert(insertedType != null, "Could not find inserted type");

        //    /* Test Delete */
        //    insertedType.DBAction = DBACTION.DELETE;

        //    proxy.UpdateStructureTypes(new List<StructureType>(new StructureType[] { insertedType }));

        //    allTypes = proxy.GetStructureTypes();
        //    StructureType deletedType = null;
        //    foreach (StructureType s in allTypes)
        //    {
        //        if (s.Name == "InsertTest")
        //        {
        //            deletedType = s;
        //            break;
        //        }
        //    }

        //    Debug.Assert(deletedType == null, "Found deleted type");

        //}

        /// <summary>
        ///A test for GetStructureTypes
        ///</summary>
        [TestMethod()]
        public void GetStructureTypesTest()
        {
            AddPrincipalToThread();

            System.Net.ServicePointManager.ServerCertificateValidationCallback =
                            ((sender, certificate, chain, sslPolicyErrors) => true);
            //CircuitClient client = new CircuitClient();
            
            //client.ClientCredentials.UserName.UserName = "anonymous";
            //client.ClientCredentials.UserName.Password = "connectome";

            AnnotateService service = new AnnotateService(Parameters.TestDatabaseName);

            service.getTopConnectedStructures(1);
            
            //Graphx graph= client.getGraph(180, 3);

            Console.WriteLine("hello");
        }

        ///// <summary>
        /////A test for GetStructureTypeByID
        /////</summary>
        //[TestMethod()]
        //public void GetStructureTypeByIDTest()
        //{
        //    AnnotateStructureTypesClient proxy = new WebAnnotationTest.AnnotationService.AnnotateStructureTypesClient();
        //    int ID = 1; // TODO: Initialize to an appropriate value
        //    StructureType t;
        //    t = proxy.GetStructureTypeByID(ID);
        //    Debug.Assert(t.Name == "Test");
        //}
    }
}
