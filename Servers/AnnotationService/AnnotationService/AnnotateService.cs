using System;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Activation;
using Annotation.Database;
using System.Linq;
using System.Data.Linq; 
using System.Collections.Generic;
using System.Diagnostics;
using System.Web.Configuration;
using System.Transactions;
using System.Security.Permissions;

using Annotation.Service.Interfaces;
using Annotation.Service.GraphClasses;

namespace Annotation
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall)]
    [AspNetCompatibilityRequirements(RequirementsMode= AspNetCompatibilityRequirementsMode.Required)]
    public class AnnotateService : IAnnotateStructureTypes, IAnnotateStructures, IAnnotateLocations, IDisposable, ICircuit
    {

        public string DefaultVolumeName = null;

        public AnnotateService(string DefaultVolumeName)
        {
            this.DefaultVolumeName = DefaultVolumeName;
        }

        public AnnotateService()
        {
            
        }

        Annotation.Database.AnnotationDataContext _db;
        Annotation.Database.AnnotationDataContext db
        {
            get
            {
                if (_db != null)
                {
                    switch(_db.Connection.State)
                    {
                        case  System.Data.ConnectionState.Closed:
                            try
                            {
                                _db.Connection.Open();
                            }
                            catch (InvalidOperationException e)
                            {
                                _db = null; 
                            }
                            break;
                        case System.Data.ConnectionState.Open:
                            return _db; 
                        case System.Data.ConnectionState.Broken:
                            _db = null;
                            break; 
                        default:
                            return _db;
                    }
                }

                if(_db != null)
                    return _db;

                //Look at the application path name, attempt to use that path to set the database for the connection

                string FormattedConnString = BuildConnectionString(); 

                _db = new Annotation.Database.AnnotationDataContext(FormattedConnString);

                DataLoadOptions options = new DataLoadOptions();
          //      options.LoadWith<DBLocation>(l => l.IsLinkedFrom);
          //      options.LoadWith<DBLocation>(l => l.IsLinkedTo);
          //      options.LoadWith<DBStructure>(s => s.IsSourceOf);
          //      options.LoadWith<DBStructure>(s => s.IsTargetOf);
                
                _db.LoadOptions = options;
                
//                _db.DeferredLoadingEnabled = false; 
                return _db;
            }
        }

        protected string BuildConnectionString()
        {
            System.Configuration.ConnectionStringSettingsCollection connStrings = WebConfigurationManager.ConnectionStrings;
            string UnformattedConnstring = connStrings["VikingGenericConnection"].ConnectionString;

            string VolumeName = TryGetVolumeFromRequest(); 

            if(VolumeName == null)
                VolumeName = this.DefaultVolumeName;
            else if (VolumeName.Length == 0)
                VolumeName = this.DefaultVolumeName;

            //VolumeName = "DebelloOB1";
            //Magic: The volumeName should match the database name
            string FormattedConnString = string.Format(UnformattedConnstring, VolumeName);

            return FormattedConnString;
        }

        /// <summary>
        /// If there is an HTTP context extract the volume name from the application path
        /// </summary>
        /// <returns></returns>
        protected string TryGetVolumeFromRequest()
        {
            if (System.Web.HttpContext.Current == null)
                return null; 

            string AppPath = System.Web.HttpContext.Current.Request.ApplicationPath;

            //Take only the last directory from the name
            string VolumeName = System.IO.Path.GetFileNameWithoutExtension(AppPath);

            VolumeName = VolumeName.ToLower();

            //Remove terms Text and Binary from path
            VolumeName = VolumeName.Replace("binary", "");
            VolumeName = VolumeName.Replace("text", "");
            VolumeName = VolumeName.Replace("debug", "");

            return VolumeName;
        }

        #region IAnnotateStructureTypes Members

        [PrincipalPermission(SecurityAction.Demand, Role = "Modify")]
        public StructureType CreateStructureType(StructureType new_structureType)
        {
            try
            {
                DBStructureType db_obj = new DBStructureType();
                //Create the object to get the ID
                new_structureType.Sync(db_obj);
                db.DBStructureTypes.InsertOnSubmit(db_obj);

                db.Log = Console.Out;
                db.SubmitChanges();
                Console.Out.Flush();

                StructureType output_obj = new StructureType(db_obj);
                return output_obj;
            }
            finally
            {
                if (db != null)
                    db.Connection.Close();
            }

            return null;
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public StructureType[] GetStructureTypes()
        {
            try
            {
                IQueryable<DBStructureType> queryResults = from t in db.DBStructureTypes select t;
                List<StructureType> retList = new List<StructureType>(queryResults.Count());
                foreach (DBStructureType dbt in queryResults)
                {
                    StructureType newType = new StructureType(dbt);
                    retList.Add(newType);
                }
                return retList.ToArray();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }

            return new StructureType[0];
        }

        /*
        public StructureTemplate[] GetStructureTemplates()
        {
            try
            {
                IQueryable<DBStructureTemplates> queryResults = from t in db.StructureTemplates select t;
                List<StructureType> retList = new List<StructureType>(queryResults.Count());
                foreach (DBStructureType dbt in queryResults)
                {
                    StructureType newType = new StructureType(dbt);
                    retList.Add(newType);
                }
                return retList.ToArray();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }

            return null;
        }
         */

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public StructureType GetStructureTypeByID(long ID)
        {
            try
            {
                DBStructureType type = (from t in db.DBStructureTypes where t.ID == ID select t).Single();
                if (type == null)
                    return null;

                StructureType newType = new StructureType(type);
                return newType;
            }
            catch (System.ArgumentNullException)
            {
                //This means there was no row with that ID; 
                Debug.WriteLine("Could not find requested type ID: " + ID.ToString());
            }
            catch (System.InvalidOperationException e)
            {
                //This means there was no row with that ID; 
                Debug.WriteLine("Could not find requested location ID: " + ID.ToString());
            }

            return null;
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public Structure[] GetStructuresForType(long TypeID)
        {
            return GetStructuresOfType(TypeID);
        }


        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public Structure[] GetStructuresOfType(long TypeID)
        {
            try
            {
                IQueryable<DBStructure> structObjs = from s in db.DBStructures
                                                     where s.TypeID == TypeID
                                                     select s;

                if (structObjs == null)
                    return new Structure[0];

                List<DBStructure> structObjList = structObjs.ToList<DBStructure>();

                List<Structure> ListStructures = new List<Structure>(structObjList.Count);
                foreach (DBStructure structObj in structObjList)
                {
                    Structure newStruct = new Structure(structObj, false);
                    ListStructures.Add(newStruct);
                }

                return ListStructures.ToArray();
            }
            catch (Exception e)
            {
                return new Structure[0];
            }
            finally
            {
                db.Connection.Close();
            }
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public StructureType[] GetStructureTypesByIDs(long[] IDs)
        {

            List<long> ListIDs = new List<long>(IDs);
            List<StructureType> ListStructureTypes = new List<StructureType>(IDs.Length);

            //LINQ creates a SQL query with parameters when using contains, and there is a 2100 parameter limit.  So we cut the query into smaller chunks and run 
            //multiple queries
            ListIDs.Sort();  //Sort the list to slightly optimize the query

            int QueryChunkSize = 2000;

            while (ListIDs.Count > 0)
            {
                int NumIDs = ListIDs.Count < QueryChunkSize ? ListIDs.Count : QueryChunkSize;

                long[] ShorterIDArray = new long[NumIDs];

                ListIDs.CopyTo(0, ShorterIDArray, 0, NumIDs);
                ListIDs.RemoveRange(0, NumIDs);

                //I do this hoping that it will allow SQL to not check the entire table for each chunk
                long minIDValue = ShorterIDArray[0];
                long maxIDValue = ShorterIDArray[ShorterIDArray.Length - 1];

                List<long> ShorterListIDs = new List<long>(ShorterIDArray);

                try
                {
                    IQueryable<DBStructureType> structTypeObjs = from s in db.DBStructureTypes
                                                                 where s.ID >= minIDValue &&
                                                                       s.ID <= maxIDValue &&
                                                                       ShorterListIDs.Contains(s.ID)
                                                                 select s;
                    if (structTypeObjs == null)
                        return null;

                    foreach (DBStructureType structTypeObj in structTypeObjs)
                    {
                        StructureType newStructType = new StructureType(structTypeObj);
                        ListStructureTypes.Add(newStructType);
                    }

                    
                }
                catch (System.ArgumentNullException)
                {
                    //This means there was no row with that ID; 
                    Debug.WriteLine("Could not find requested structure type IDs: " + IDs.ToString());
                }
                catch (System.InvalidOperationException e)
                {
                    //This means there was no row with that ID; 
                    Debug.WriteLine("Could not find requested structure type IDs: " + IDs.ToString());
                }
                finally
                {
                    if(db != null)
                        db.Connection.Close();
                }
            }

            return ListStructureTypes.ToArray();
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Modify")]
        public long[] UpdateStructureTypes(StructureType[] structTypes)
        {
            return Update(structTypes);
        }

        /// <summary>
        /// Submits passed structure types to the database
        /// </summary>
        /// <param name="structTypes"></param>
        /// <returns>Returns ID's of each object in the order they were passed. Used to recover ID's of inserted rows</returns>
        [PrincipalPermission(SecurityAction.Demand, Role = "Modify")]
        public long[] Update(StructureType[] structTypes)
        { 
            IQueryable<DBStructureType> types = from t in db.DBStructureTypes select t;

            Dictionary<DBStructureType, int> mapNewTypeToIndex = new Dictionary<DBStructureType, int>(structTypes.Length);

            //Stores the ID of each object manipulated for the return value
            long[] listID = new long[structTypes.Length];
            try
            {

                for (int iObj = 0; iObj < structTypes.Length; iObj++)
                {
                    StructureType t = structTypes[iObj];

                    switch (t.DBAction)
                    {
                        case DBACTION.INSERT:

                            DBStructureType newType = new DBStructureType();
                            t.Sync(newType); 
                            db.DBStructureTypes.InsertOnSubmit(newType);
                            mapNewTypeToIndex.Add(newType, iObj);
                            break;
                        case DBACTION.UPDATE:
                            DBStructureType updateType;
                            try
                            {
                                updateType = (from u in types where u.ID == t.ID select u).Single();
                            }
                            catch (System.ArgumentNullException e)
                            {
                                Debug.WriteLine("Could not find structuretype to update: " + t.ID.ToString());
                                break;
                            }
                            catch (System.InvalidOperationException e)
                            {
                                Debug.WriteLine("Could not find structuretype to update: " + t.ID.ToString());
                                break;
                            }

                            t.Sync(updateType);
                            listID[iObj] = updateType.ID;
                            //  db.DBStructureTypes.(updateType);
                            break;
                        case DBACTION.DELETE:
                            DBStructureType deleteType;
                            try
                            {
                                deleteType = (from u in types where u.ID == t.ID select u).Single();
                            }
                            catch (System.ArgumentNullException e)
                            {
                                Debug.WriteLine("Could not find structuretype to delete: " + t.ID.ToString());
                                break;
                            }
                            catch (System.InvalidOperationException e)
                            {
                                Debug.WriteLine("Could not find structuretype to update: " + t.ID.ToString());
                                break;
                            }

                            deleteType.ID = t.ID;
                            listID[iObj] = deleteType.ID;
                            db.DBStructureTypes.DeleteOnSubmit(deleteType);

                            break;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
                throw e;

            }

            db.SubmitChanges();

            //Recover the ID's for new objects


            foreach (DBStructureType newType in mapNewTypeToIndex.Keys)
            {
                int iIndex = mapNewTypeToIndex[newType];
                listID[iIndex] = newType.ID;
            }

            return listID;
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public string TestMethod()
        {
            return "Test OK";
        }

        #endregion

        #region IAnnotateStructures Members

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public Structure[] GetStructures()
        {
            try
            {
                //IQueryable<DBStructure> queryStructures = from s in db.DBStructures select s;
                List<DBStructure> listStructs = db.SelectAllStructuresAndLinks();

                Structure[] retList = new Structure[listStructs.Count()];

                for(int iStruct = 0; iStruct < listStructs.Count(); iStruct++)
                {
                    //Get structures does not include children because 
                    //if you have all the structures you can create the
                    //graph yourself by looking at ParentIDs without 
                    //sending duplicate information over the wire
                    retList[iStruct] = new Structure(listStructs[iStruct], false);
                }

                return retList;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }

            return new Structure[0];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public Structure[] GetStructuresForSection(long SectionNumber, long ModifiedAfterThisUtcTime, out long QueryExecutedTime, out long[] DeletedIDs)
        {
            DeletedIDs = new long[0];

            QueryExecutedTime = DateTime.Now.ToUniversalTime().Ticks;

            try
            {

                List<DBStructure> listStructs;
                DateTime? ModifiedAfter = new DateTime?();
                if (ModifiedAfterThisUtcTime > 0)
                    ModifiedAfter = new DateTime?(new DateTime(ModifiedAfterThisUtcTime, DateTimeKind.Unspecified));

                ModifiedAfter = AnnotationDataContext.ValidateDate(ModifiedAfter);

                listStructs = db.SelectStructuresAndLinks(SectionNumber, ModifiedAfter);

                Structure[] retList = new Structure[listStructs.Count()];

                for (int iStruct = 0; iStruct < listStructs.Count(); iStruct++)
                {
                    //Get structures does not include children because 
                    //if you have all the structures you can create the
                    //graph yourself by looking at ParentIDs without 
                    //sending duplicate information over the wire
                    retList[iStruct] = new Structure(listStructs[iStruct], false);
                }

                return retList;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }
              
            return new Structure[0];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public Structure GetStructureByID(long ID, bool IncludeChildren)
        {
            try
            {
                DBStructure structObj = (from s in db.DBStructures where s.ID == ID select s).Single();
                if (structObj == null)
                    return null;

                Structure newStruct = new Structure(structObj, IncludeChildren);
                return newStruct;
            }
            catch (System.ArgumentNullException)
            {
                //This means there was no row with that ID; 
                Debug.WriteLine("Could not find requested structure ID: " + ID.ToString());
            }
            catch (System.InvalidOperationException e)
            {
                //This means there was no row with that ID; 
                Debug.WriteLine("Could not find requested structure ID: " + ID.ToString());
            }

            return null;
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public Structure[] GetStructuresByIDs(long[] IDs, bool IncludeChildren)
        {

            List<long> ListIDs = new List<long>(IDs);
            List<Structure> ListStructures = new List<Structure>(IDs.Length);

            //LINQ creates a SQL query with parameters when using contains, and there is a 2100 parameter limit.  So we cut the query into smaller chunks and run 
            //multiple queries
            ListIDs.Sort();  //Sort the list to slightly optimize the query

            int QueryChunkSize = 2000;

            while (ListIDs.Count > 0)
            {
                int NumIDs = ListIDs.Count < QueryChunkSize ? ListIDs.Count : QueryChunkSize;

                long[] ShorterIDArray = new long[NumIDs];
                
                ListIDs.CopyTo(0, ShorterIDArray, 0, NumIDs);
                ListIDs.RemoveRange(0, NumIDs);

                //I do this hoping that it will allow SQL to not check the entire table for each chunk
                long minIDValue = ShorterIDArray[0];
                long maxIDValue = ShorterIDArray[ShorterIDArray.Length-1];

                List<long> ShorterListIDs = new List<long>(ShorterIDArray);

                try
                {
                    IQueryable<DBStructure> structObjs = from s in db.DBStructures
                                                         where s.ID >= minIDValue &&
                                                               s.ID <= maxIDValue && 
                                                               ShorterListIDs.Contains(s.ID)
                                                         select s;
                    if (structObjs == null)
                        return new Structure[0];

                    foreach (DBStructure structObj in structObjs)
                    {
                        Structure newStruct = new Structure(structObj, IncludeChildren);
                        ListStructures.Add(newStruct);
                    }  
                }
                catch (System.ArgumentNullException)
                {
                    //This means there was no row with that ID; 
                    Debug.WriteLine("Could not find requested structure IDs: " + IDs.ToString());
                }
                catch (System.InvalidOperationException e)
                {
                    //This means there was no row with that ID; 
                    Debug.WriteLine("Could not find requested structure IDs: " + IDs.ToString());
                }

            }

            if (ListStructures.Count > QueryChunkSize)
            {
                Trace.Write("GetStructuresByID count > 2000.  Would have been affected by truncation bug in the past.");
            }

            return ListStructures.ToArray();
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public void ApproximateStructureLocation(long ID)
        {
            db.ApproximateStructureLocation(new int?((int)ID));  
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Modify")]
        public StructureLink CreateStructureLink(StructureLink link)
        {
            DBStructureLink newRow = new DBStructureLink();
            link.Sync(newRow);    
            db.DBStructureLinks.InsertOnSubmit(newRow);
            db.SubmitChanges();

            StructureLink newLink = new StructureLink(newRow);
            return newLink; 
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Modify")]
        public void UpdateStructureLinks(StructureLink[] links)
        {
            //Stores the ID of each object manipulated for the return value
            try
            {
                for (int iObj = 0; iObj < links.Length; iObj++)
                {
                    StructureLink obj = links[iObj];
                    DBStructureLink DBObj = null;

                    switch (obj.DBAction)
                    {
                        case DBACTION.INSERT:

                            DBObj = new DBStructureLink();
                            obj.Sync(DBObj);
                            db.DBStructureLinks.InsertOnSubmit(DBObj);
                            break;
                        case DBACTION.UPDATE:

                            try
                            {
                                DBObj = (from u in db.DBStructureLinks
                                         where u.SourceID == obj.SourceID &&
                                               u.TargetID == obj.TargetID
                                         select u).Single();
                            }
                            catch (System.ArgumentNullException e)
                            {
                                Debug.WriteLine("Could not find structuretype to update: " + obj.ToString());
                                break;
                            }
                            catch (System.InvalidOperationException e)
                            {
                                Debug.WriteLine("Could not find structuretype to update: " + obj.ToString());
                                break;
                            }

                            obj.Sync(DBObj);
                            //  db.DBStructureTypes.(updateType);
                            break;
                        case DBACTION.DELETE:
                            try
                            {
                                DBObj = (from u in db.DBStructureLinks
                                         where u.SourceID == obj.SourceID &&
                                               u.TargetID == obj.TargetID
                                         select u).Single();
                            }
                            catch (System.ArgumentNullException e)
                            {
                                Debug.WriteLine("Could not find structuretype to delete: " + obj.ToString());
                                break;
                            }
                            catch (System.InvalidOperationException e)
                            {
                                Debug.WriteLine("Could not find structuretype to update: " + obj.ToString());
                                break;
                            }

                            db.DBStructureLinks.DeleteOnSubmit(DBObj);

                            break;
                    }
                }

                db.SubmitChanges();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
                throw e;
            }


            //Recover the ID's for new objects
            return;
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public long[] GetUnfinishedLocations(long structureID)
        {
            return db.SelectUnfinishedStructureBranches(structureID).ToArray<long>();
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public LocationPositionOnly[] GetUnfinishedLocationsWithPosition(long structureID)
        {
            List<LocationPositionOnly> listLocs = new List<LocationPositionOnly>();

            foreach (SelectUnfinishedStructureBranchesWithPositionResult row in db.SelectUnfinishedStructureBranchesWithPosition(structureID))
            {
                listLocs.Add(new LocationPositionOnly(row));

            }

            return listLocs.ToArray<LocationPositionOnly>();
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public StructureLink[] GetLinkedStructures()
        {
            try
            {
                IQueryable<DBStructureLink> queryResults = from l in db.DBStructureLinks select l;
                List<StructureLink> retList = new List<StructureLink>(queryResults.Count());
                foreach (DBStructureLink dbl in queryResults)
                {
                    StructureLink link = new StructureLink(dbl);
                    retList.Add(link);
                }
                return retList.ToArray();
            }
            catch (System.ArgumentNullException)
            {
                //This means there was no row with that ID; 
                Debug.WriteLine("Could not find StructureLinks");
            }
            catch (System.InvalidOperationException e)
            {
                //This means there was no row with that ID; 
                Debug.WriteLine("Could not find StructureLinks");
            }

            return new StructureLink[0];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public StructureLink[] GetLinkedStructuresByID(long ID)
        {
            try
            {
                IQueryable<DBStructureLink> queryResults = from l in db.DBStructureLinks where (l.SourceID == ID || l.TargetID == ID) select l;
                List<StructureLink> retList = new List<StructureLink>(queryResults.Count());
                foreach (DBStructureLink dbl in queryResults)
                {
                    StructureLink link = new StructureLink(dbl);
                    retList.Add(link);
                }
                return retList.ToArray();
            }
            catch (System.ArgumentNullException)
            {
                //This means there was no row with that ID; 
                Debug.WriteLine("Could not find StructureLinks for ID: " + ID.ToString());
            }
            catch (System.InvalidOperationException e)
            {
                //This means there was no row with that ID; 
                Debug.WriteLine("Could not find StructureLinks for ID: " + ID.ToString());
            }

            return new StructureLink[0];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public Location[] GetLocationsForStructure(long structureID)
        {
            try
            {
                IQueryable<DBLocation> queryResults = from l in db.DBLocations where (l.ParentID == structureID) select l;
                List<Location> retList = new List<Location>(queryResults.Count());
                foreach (DBLocation dbl in queryResults)
                {
                    Location loc = new Location(dbl);
                    retList.Add(loc);
                }
                return retList.ToArray();
            }
            catch (System.ArgumentNullException)
            {
                //This means there was no row with that ID; 
                Debug.WriteLine("Could not find locations for ID: " + structureID.ToString());
            }
            catch (System.InvalidOperationException e)
            {
                //This means there was no row with that ID; 
                Debug.WriteLine("Could not find locations for ID: " + structureID.ToString());
            }

            return new Location[0];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public long NumberOfLocationsForStructure(long structureID)
        {
            try
            {
                IQueryable<DBLocation> queryResults = from l in db.DBLocations where (l.ParentID == structureID) select l;
                return queryResults.Count();
            }
            catch (System.ArgumentNullException)
            {
                //This means there was no row with that ID; 
                Debug.WriteLine("Could not find locations for ID: " + structureID.ToString());
            }
            catch (System.InvalidOperationException e)
            {
                //This means there was no row with that ID; 
                Debug.WriteLine("Could not find locations for ID: " + structureID.ToString());
            }

            return 0;
        }

        

        [PrincipalPermission(SecurityAction.Demand, Role = "Modify")]
        public long[] UpdateStructures(Structure[] structures)
        {
            return Update(structures);
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Modify")]
        public long[] Update(Structure[] structures)
        {
            Dictionary<DBStructure, int> mapNewObjToIndex = new Dictionary<DBStructure, int>(structures.Length);

            //Stores the ID of each object manipulated for the return value
            long[] listID = new long[structures.Length];
            try
            {

                for (int iObj = 0; iObj < structures.Length; iObj++)
                {
                    Structure t = structures[iObj];

                    switch (t.DBAction)
                    {
                        case DBACTION.INSERT:
                            DBStructure newRow = new DBStructure();
                            t.Sync(newRow);
                            db.DBStructures.InsertOnSubmit(newRow);
                            mapNewObjToIndex.Add(newRow, iObj);
                            break;
                        case DBACTION.UPDATE:

                            DBStructure updateRow;
                            try
                            {
                                updateRow = (from u in db.DBStructures where u.ID == t.ID select u).Single();
                            }
                            catch (System.ArgumentNullException e)
                            {
                                Debug.WriteLine("Could not find structuretype to update: " + t.ID.ToString());
                                break;
                            }
                            catch (System.InvalidOperationException e)
                            {
                                Debug.WriteLine("Could not find structuretype to update: " + t.ID.ToString());
                                break;
                            }

                            t.Sync(updateRow);
                            listID[iObj] = updateRow.ID;
                            //  db.DBStructureTypes.(updateType);
                            break;
                        case DBACTION.DELETE:
                            DBStructure deleteRow = new DBStructure();
                            try
                            {
                                deleteRow = (from u in db.DBStructures where u.ID == t.ID select u).Single();
                            }
                            catch (System.ArgumentNullException e)
                            {
                                Debug.WriteLine("Could not find structuretype to update: " + t.ID.ToString());
                                break;
                            }
                            catch (System.InvalidOperationException e)
                            {
                                Debug.WriteLine("Could not find structuretype to update: " + t.ID.ToString());
                                break;
                            }


                            t.Sync(deleteRow);
                            deleteRow.ID = t.ID;
                            listID[iObj] = deleteRow.ID;

                            db.DBStructures.DeleteOnSubmit(deleteRow);

                            //Remove any links that exist before calling delete
                            foreach (DBStructureLink link in deleteRow.IsSourceOf)
                            {
                                db.DBStructureLinks.DeleteOnSubmit(link);
                            }

                            foreach (DBStructureLink link in deleteRow.IsTargetOf)
                            {
                                db.DBStructureLinks.DeleteOnSubmit(link);
                            }

                            break;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
                throw e;

            }

            db.SubmitChanges();

            //Recover the ID's for new objects
            foreach (DBStructure newObj in mapNewObjToIndex.Keys)
            {
                int iIndex = mapNewObjToIndex[newObj];
                listID[iIndex] = newObj.ID;
            }

            return listID;
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Modify")]
        public CreateStructureRetval CreateStructure(Structure structure, Location location)
        {
            try
            {
                DBStructure DBStruct = new DBStructure();
                structure.Sync(DBStruct);

                db.DBStructures.InsertOnSubmit(DBStruct);

                DBLocation DBLoc = new DBLocation();
                location.Sync(DBLoc);
                DBLoc.DBStructure = DBStruct;

                db.DBLocations.InsertOnSubmit(DBLoc);

                db.SubmitChanges();

                //Return new ID's to the caller
                CreateStructureRetval retval = new CreateStructureRetval(new Structure(DBStruct, false), new Location(DBLoc));
                return retval; 
            }
            finally
            {
                if (db != null)
                    db.Connection.Close();
            }

            return null;
        }

        /*
        [PrincipalPermission(SecurityAction.Demand, Role = "Modify")]
        public long[] CreateStructure(Structure structure, Location location)
        {
            DBStructure DBStruct = new DBStructure();
            structure.Sync(DBStruct);

            db.DBStructures.InsertOnSubmit(DBStruct);

            DBLocation DBLoc = new DBLocation();
            location.Sync(DBLoc);
            DBLoc.DBStructure = DBStruct;

            db.DBLocations.InsertOnSubmit(DBLoc);

            db.SubmitChanges();

            //Return new ID's to the caller
            return new long[] { DBStruct.ID, DBLoc.ID };
        }
         */

        /// <summary>
        /// Merges the specified structures into a single structure. Structures must be of the same type.
        /// </summary>
        /// <param name="StructureA"></param>
        /// <param name="StructureB"></param>
        /// <returns>ID of new structure</returns>
        [PrincipalPermission(SecurityAction.Demand, Role = "Admin")]
        public long Merge(long KeepID, long MergeID)
        {
            return db.MergeStructures(KeepID, MergeID); 
        }

        /// <summary>
        /// Split the specified structure into two new structures at the specified link
        /// return an exception if the structure has a cycle in the graph.
        /// Child objects are assigned to the nearest location on the same section
        /// </summary>
        /// <param name="StructureA">Structure to split</param>
        /// <param name="locLink">Location Link to split structure at</param>
        /// <returns>ID of new structure</returns>
        [PrincipalPermission(SecurityAction.Demand, Role = "Admin")]
        public long Split(long StructureA, LocationLink locLink)
        {
            return 0;
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public StructureHistory[] GetStructureChangeLog(long? structure_id, DateTime? begin_time, DateTime? end_time)
        {
            ISingleResult<SelectStructureChangeLogResult> result = db.SelectStructureChangeLog(structure_id, begin_time, end_time);
            List<SelectStructureChangeLogResult> listChanges = new List<SelectStructureChangeLogResult>(result);
            List<StructureHistory> structures = new List<StructureHistory>(listChanges.Count);
            foreach (SelectStructureChangeLogResult row in listChanges)
            {
                structures.Add(new StructureHistory(row));
            }

            return structures.ToArray();
        }



        #endregion

        #region IAnnotateLocations Members

        [PrincipalPermission(SecurityAction.Demand, Role="Read")]
        public Location GetLocationByID(long ID)
        {
            try
            {
                DBLocation obj = (from t in db.DBLocations where t.ID == ID select t).Single();
                if (obj == null)
                    return null;
                Location retLoc = new Location(obj);
                return retLoc;
            }
            catch (System.ArgumentNullException)
            {
                //This means there was no row with that ID; 
                Debug.WriteLine("Could not find requested location ID: " + ID.ToString());
            }
            catch (System.InvalidOperationException e)
            {
                //This means there was no row with that ID; 
                Debug.WriteLine("Could not find requested location ID: " + ID.ToString());
            }

            return null;
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public Location[] GetLocationsByID(long[] IDs)
        {
            List<long> ListIDs = new List<long>(IDs);
            List<Location> ListLocations = new List<Location>(IDs.Length);

            //LINQ creates a SQL query with parameters when using contains, and there is a 2100 parameter limit.  So we cut the query into smaller chunks and run 
            //multiple queries
            ListIDs.Sort();  //Sort the list to slightly optimize the query

            int QueryChunkSize = 2000;

            while (ListIDs.Count > 0)
            {
                int NumIDs = ListIDs.Count < QueryChunkSize ? ListIDs.Count : QueryChunkSize;

                long[] ShorterIDArray = new long[NumIDs];

                ListIDs.CopyTo(0, ShorterIDArray, 0, NumIDs);
                ListIDs.RemoveRange(0, NumIDs);

                //I do this hoping that it will allow SQL to not check the entire table for each chunk
                long minIDValue = ShorterIDArray[0];
                long maxIDValue = ShorterIDArray[ShorterIDArray.Length - 1];

                List<long> ShorterListIDs = new List<long>(ShorterIDArray);

                try
                {
                    IQueryable<DBLocation> locObjs = from s in db.DBLocations
                                                                 where s.ID >= minIDValue &&
                                                                       s.ID <= maxIDValue &&
                                                                       ShorterListIDs.Contains(s.ID)
                                                                 select s;
                    if (locObjs == null)
                        return null;

                    foreach (DBLocation locObj in locObjs)
                    {
                        Location newLocation = new Location(locObj);
                        ListLocations.Add(newLocation);
                    }


                }
                catch (System.ArgumentNullException)
                {
                    //This means there was no row with that ID; 
                    Debug.WriteLine("Could not find requested location IDs: " + IDs.ToString());
                }
                catch (System.InvalidOperationException e)
                {
                    //This means there was no row with that ID; 
                    Debug.WriteLine("Could not find requested location IDs: " + IDs.ToString());
                }
                finally
                {
                    if (db != null)
                        db.Connection.Close();
                }
            }

            return ListLocations.ToArray(); 

        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public Location GetLastModifiedLocation()
        {
            try
            {
                string callingUser = ServiceModelUtil.GetUserForCall().Trim();
                ISingleResult<DBLocation> LocationsByUser = db.SelectLastModifiedLocationByUsers();
                DBLocation lastLocation = (from l in LocationsByUser where l.Username.Trim() == callingUser select l).FirstOrDefault<DBLocation>();
                return new Location(lastLocation);
            }
            catch (Exception e)
            {
                return null;
            }
            finally
            {
                if (db != null)
                    db.Connection.Close(); 
            }
        }
        
        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public Location[] GetLocationsForSection(long section, out long QueryExecutedTime)
        {
            DateTime start = DateTime.Now;
            QueryExecutedTime = DateTime.Now.ToUniversalTime().Ticks;
            try
            {

                //IQueryable<DBLocation> queryResults = from l in db.DBLocations where ((double)section) == l.Z select l;
                IList<DBLocation> locations = db.SectionLocationsAndLinks(section);
                //List<DBLocation> locations = queryResults.ToList<DBLocation>();

                Debug.WriteLine(section.ToString() + ": Query: " + new TimeSpan(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds);

                Location[] retList = new Location[locations.Count];

                Debug.WriteLine(section.ToString() + ": To list: " + new TimeSpan(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds);
                for(int i = 0; i < locations.Count; i++)
                {
                    retList[i] = new Location(locations[i]);
                }

                Debug.WriteLine(section.ToString() + ": Loop: " + new TimeSpan(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds);

                return retList;
            }
            catch (System.ArgumentNullException)
            {
                //This means there was no row with that ID; 
                Debug.WriteLine("Could not find locations for section: " + section.ToString());
            }
            catch (System.InvalidOperationException e)
            {
                //This means there was no row with that ID; 
                Debug.WriteLine("Could not find locations for section: " + section.ToString());
            }


            return new Location[0];
        }

        /// <summary>
        /// Return all locations that have changed and an int array of deleted sections.
        /// The passed time has to be in UTC.  
        /// 
        /// in the UTC timezone
        /// </summary>
        /// <param name="time">UTC Datetime object passed using "ticks"</param>
        /// <param name="?"></param>
        /// <returns></returns>
        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public Location[] GetLocationChanges(long section, long ModifiedAfterThisUtcTime,  out long QueryExecutedTime, out long[] DeletedIDs)
        {
            DateTime start = DateTime.Now;
            TimeSpan elapsed;

            DateTime? ModifiedAfterThisTime = new DateTime?();
            if(ModifiedAfterThisUtcTime > 0)
                ModifiedAfterThisTime = new DateTime?(new DateTime(ModifiedAfterThisUtcTime, DateTimeKind.Unspecified));

            ModifiedAfterThisTime = AnnotationDataContext.ValidateDate(ModifiedAfterThisTime); 

            DeletedIDs = new long[0];

            Location[] retList = new Location[0]; 

            QueryExecutedTime = DateTime.Now.ToUniversalTime().Ticks;
            try
            {
                //// Find all the IDs that still exist
                //IQueryable<DateTime> queryDebug2 = from l in db.DBLocations
                //                                   where ((double)section) == l.Z
                //                                   select l.LastModified;


                //foreach (DateTime date in queryDebug2)
                //{
                //    System.Diagnostics.Debug.WriteLine(date.ToString());

                //    if (date > ModifiedAfterThisTime)
                //        System.Diagnostics.Debug.WriteLine("*******MATCH*******");
                //}


                /*
                IQueryable<DBLocation> queryResults = from l in db.DBLocations
                                                      where ((double)section) == l.Z &&
                                                            (ModifiedAfterThisTime <= l.LastModified)
                                                      select l;
                */
                IList<DBLocation> listLocations = db.SectionLocationsAndLinks((double)section, ModifiedAfterThisTime);

                elapsed = new TimeSpan(DateTime.Now.Ticks - start.Ticks);
                Debug.WriteLine(section.ToString() + ": Query: " + elapsed.TotalMilliseconds);

                //List<DBLocation> listLocations = queryResults.ToList<DBLocation>();

                
                retList = new Location[listLocations.Count];

 //               elapsed = new TimeSpan(DateTime.Now.Ticks - start.Ticks);
 //               Debug.WriteLine(section.ToString() + ": To list: " + elapsed.TotalMilliseconds);

                for (int i = 0; i < listLocations.Count; i++)
                {
                    retList[i] = new Location(listLocations[i]);
                }
                
                elapsed = new TimeSpan(DateTime.Now.Ticks - start.Ticks);
                Debug.WriteLine(section.ToString() + ": Loop: " + elapsed.TotalMilliseconds);
            }
            catch (System.ArgumentNullException)
            {
                //This means there was no row with that ID; 
                Debug.WriteLine("Could not find locations for section: " + section.ToString());
            }
            catch (System.InvalidOperationException e)
            {
                //This means there was no row with that ID; 
                Debug.WriteLine("Could not find locations for section: " + section.ToString());
            }

            //Try to find if any rows were deleted from the passed list of IDs
            try
            {
                if (ModifiedAfterThisTime.HasValue)
                {
                    //// Find all the IDs that still exist
                    //IQueryable<DateTime> queryDebug = from l in db.DBDeletedLocations
                    //                                select l.DeletedOn;

                    //foreach (DateTime date in queryDebug)
                    //{
                    //    System.Diagnostics.Debug.WriteLine(date.ToString()); 

                    //    if(date > ModifiedAfterThisTime)
                    //        System.Diagnostics.Debug.WriteLine("*******MATCH*******");
                    //}

                    // Find all the IDs that still exist
                    IQueryable<long> queryResults = from l in db.DBDeletedLocations
                                                    where (l.DeletedOn > ModifiedAfterThisTime)
                                                    select l.ID;

                    elapsed = new TimeSpan(DateTime.Now.Ticks - start.Ticks);
                    Debug.WriteLine(section.ToString() + ": Deleted Query: " + elapsed.TotalMilliseconds);

                    //Figure out which IDs are not in the returned list
                    DeletedIDs = queryResults.ToArray();
                }
                else
                {
                    DeletedIDs = new long[0];
                }

                

            }
            catch (System.ArgumentNullException)
            {
                //This means there was no row with that ID; 
                Debug.WriteLine("Could not find locations for section: " + section.ToString());
            }
            catch (System.InvalidOperationException e)
            {
                //This means there was no row with that ID; 
                Debug.WriteLine("Could not find locations for section: " + section.ToString());
            }
            finally
            {
                if (db != null)
                    db.Connection.Close();
            }


            return retList;
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Modify")]
        public Location CreateLocation(Location new_location, long[] links)
        {
            try
            {
                DBLocation db_obj = new DBLocation();
                string username = ServiceModelUtil.GetUserForCall();
                using (var transaction = new TransactionScope())
                {
                    //Create the object to get the ID
                    new_location.Sync(db_obj);
                    new_location.Username = username;
                    db.DBLocations.InsertOnSubmit(db_obj);

                    db.Log = Console.Out;
                    db.SubmitChanges();
                    Console.Out.Flush();

                    //Build a new location link for every link in the array
                    List<DBLocationLink> listLinks = new List<DBLocationLink>(links.Length);
                    foreach (long linked_locationID in links)
                    {
                        DBLocationLink created_link = _CreateLocationLink(db_obj.ID, linked_locationID, username);
                        listLinks.Add(created_link); 
                    }

                    db.DBLocationLinks.InsertAllOnSubmit(listLinks);
                    db.SubmitChanges(); 
                    transaction.Complete();
                }

                Location output_loc = new Location(db_obj);
                output_loc.Links = links;
                return output_loc;
            }
            finally
            {
                if (db != null)
                    db.Connection.Close();
            }
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Modify")]
        public long[] Update(Location[] locations)
        {
            Dictionary<DBLocation, int> mapNewTypeToIndex = new Dictionary<DBLocation, int>(locations.Length);

            //Stores the ID of each object manipulated for the return value
            long[] listID = new long[locations.Length];
            try
            {

                for (int iObj = 0; iObj < locations.Length; iObj++)
                {
                    Location t = locations[iObj];

                    switch (t.DBAction)
                    {
                        case DBACTION.INSERT:

                            DBLocation newObj = new DBLocation();
                            t.Sync(newObj);
                            db.DBLocations.InsertOnSubmit(newObj);
                            mapNewTypeToIndex.Add(newObj, iObj);
                            break;
                        case DBACTION.UPDATE:
                            DBLocation updateRow;
                            try
                            {
                                updateRow = (from u in db.DBLocations where u.ID == t.ID select u).Single();
                            }
                            catch (System.ArgumentNullException e)
                            {
                                Debug.WriteLine("Could not find structuretype to update: " + t.ID.ToString());
                                break;
                            }
                            catch (System.InvalidOperationException e)
                            {
                                Debug.WriteLine("Could not find structuretype to update: " + t.ID.ToString());
                                break;
                            }

                            t.Sync(updateRow);
                            listID[iObj] = updateRow.ID;
                            //  db.DBStructureTypes.(updateType);
                            break;
                        case DBACTION.DELETE:
                            DBLocation deleteRow;
                            try
                            {
                                deleteRow = (from u in db.DBLocations where u.ID == t.ID select u).Single();
                            }
                            catch (System.ArgumentNullException e)
                            {
                                Debug.WriteLine("Could not find structuretype to update: " + t.ID.ToString());
                                break;
                            }
                            catch (System.InvalidOperationException e)
                            {
                                Debug.WriteLine("Could not find structuretype to update: " + t.ID.ToString());
                                break;
                            }

                            //Remove any links that exist before calling delete
                            foreach (DBLocationLink link in deleteRow.IsLinkedFrom)
                            {
                                db.DBLocationLinks.DeleteOnSubmit(link);
                            }

                            foreach (DBLocationLink link in deleteRow.IsLinkedTo)
                            {
                                db.DBLocationLinks.DeleteOnSubmit(link);
                            }

                            t.Sync(deleteRow);
                            deleteRow.ID = t.ID;
                            listID[iObj] = deleteRow.ID;
                            db.DBLocations.DeleteOnSubmit(deleteRow);

                            break;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
                throw e;
            }
            

            db.Log = Console.Out;
            db.SubmitChanges();
            Console.Out.Flush();

            //Recover the ID's for new objects
            foreach (DBLocation newObj in mapNewTypeToIndex.Keys)
            {
                int iIndex = mapNewTypeToIndex[newObj];
                listID[iIndex] = newObj.ID;
            }

            if (db != null)
                db.Connection.Close();

            return listID;
        }

        private DBLocationLink _CreateLocationLink(long SourceID, long TargetID, string username)
        {
            if(username == null)
                username = ServiceModelUtil.GetUserForCall();

            DBLocationLink newLink = new DBLocationLink();
            DBLocation Source = null;
            DBLocation Target = null;
            try
            {
                Source = (from u in db.DBLocations where u.ID == SourceID select u).Single();
                Target = (from u in db.DBLocations where u.ID == TargetID select u).Single();
            }
            catch (InvalidOperationException e)
            {
                throw new ArgumentException("CreateLocationLink: The specified source or target does not exist", e);
            }

            if (Source == null || Target == null)
            {
                throw new ArgumentException("CreateLocationLink: The specified source or target does not exist");
            }

            if (Source.ParentID != Target.ParentID)
            {
                throw new ArgumentException("Location links can only be created between locations belonging to the same structure");
            }

            newLink.Username = ServiceModelUtil.GetUserForCall();

            //Source and target are poorly named.  Right now source is always the smaller ID value, links are unidirectional
            if (SourceID < TargetID)
            {
                newLink.SourceLocation = Source;
                newLink.TargetLocation = Target;
            }
            else if (SourceID > TargetID)
            {
                newLink.SourceLocation = Target;
                newLink.TargetLocation = Source;
            }

            newLink.Username = username;

            return newLink; 
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Modify")]
        public void CreateLocationLink(long SourceID, long TargetID)
        {
            DBLocationLink newLink = _CreateLocationLink(SourceID, TargetID, null); 
            db.DBLocationLinks.InsertOnSubmit(newLink);
            db.SubmitChanges();

            return;
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Modify")]
        public void DeleteLocationLink(long SourceID, long TargetID)
        {
            DBLocationLink link;
            bool LinkFound = false;
            try
            {
                link = (from u in db.DBLocationLinks where u.LinkedFrom == SourceID && u.LinkedTo == TargetID select u).Single();
            }
            catch (InvalidOperationException except)
            {
                //No link found
                link = null;
            }

            if (link != null)
            {
                db.DBLocationLinks.DeleteOnSubmit(link);
                LinkFound = true;
            }

            try
            {
                link = (from u in db.DBLocationLinks where u.LinkedFrom == TargetID && u.LinkedTo == SourceID select u).Single();
            }
            catch (InvalidOperationException except)
            {
                link = null;
            }

            if (link != null)
            {
                db.DBLocationLinks.DeleteOnSubmit(link);
                LinkFound = true;
            }

            if (!LinkFound)
            {
                throw new ArgumentException("DeleteLocationLink: The specified source or target does not exist");
            }

            db.SubmitChanges();
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public LocationLink[] LocationLinksForSection(long section, long ModifiedAfterThisUtcTime, out long QueryExecutedTime, out LocationLink[] DeletedLinks)
        {
            //TODO: This needs a real assignment, but I haven't created the table yet
            DeletedLinks = new LocationLink[0]; 
            DateTime start = DateTime.Now;
            DateTime? ModifiedAfter;
            if (ModifiedAfterThisUtcTime == 0)
            {
                ModifiedAfter = new DateTime?(); 
            }
            else
            {
                ModifiedAfter = new DateTime?(new DateTime(ModifiedAfterThisUtcTime, DateTimeKind.Unspecified));
            }
            QueryExecutedTime = DateTime.Now.ToUniversalTime().Ticks;
            try
            {
                //IQueryable<DBLocation> queryResults = from l in db.DBLocations where ((double)section) == l.Z select l;
                List<DBLocationLink> locationLinks;
                locationLinks = db.SelectSectionLocationLinks(new double?(section), ModifiedAfter).ToList<DBLocationLink>();

                Debug.WriteLine(section.ToString() + ": Query: " + new TimeSpan(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds);

                LocationLink[] retList = new LocationLink[locationLinks.Count];

                Debug.WriteLine(section.ToString() + ": To list: " + new TimeSpan(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds);
                for (int i = 0; i < locationLinks.Count; i++)
                {
                    retList[i] = new LocationLink(locationLinks[i]);
                }

                Debug.WriteLine(section.ToString() + ": Loop: " + new TimeSpan(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds);

                return retList;
            }
            catch (System.ArgumentNullException)
            {
                //This means there was no row with that ID; 
                Debug.WriteLine("Could not find locatioWat>cns for section: " + section.ToString());
            }
            catch (System.InvalidOperationException e)
            {
                //This means there was no row with that ID; 
                Debug.WriteLine("Could not find locations for section: " + section.ToString());
            }

            return new LocationLink[0];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public long[] GetLinkedLocations(long ID)
        {
            var links = (from u in db.DBLocationLinks where u.LinkedTo == ID select u.LinkedFrom).Union(from u in db.DBLocationLinks where u.LinkedFrom == ID select u.LinkedTo);
            return links.ToArray();
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public LocationHistory[] GetLocationChangeLog(long? structure_id, DateTime? begin_time, DateTime? end_time)
        {
            ISingleResult<SelectStructureLocationChangeLogResult> result = db.SelectStructureLocationChangeLog(structure_id, begin_time, end_time);
            List<SelectStructureLocationChangeLogResult> listChanges = new List<SelectStructureLocationChangeLogResult>(result);
            List<LocationHistory> locations = new List<LocationHistory>(listChanges.Count);
            foreach (SelectStructureLocationChangeLogResult row in listChanges)
            {
                locations.Add(new LocationHistory(row));
            }

            return locations.ToArray();
        }


        #endregion


        #region IDisposable Members

        void IDisposable.Dispose()
        {
            if (_db != null)
            {
                _db.Dispose();
                _db = null;
            }
        }

        #endregion

        #region ICircuit Members

        public SortedDictionary<long, StructureType> StructureTypesDictionary = new SortedDictionary<long, StructureType>();

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public Graphx getGraph(int cellID, int numHops)
        {
            // Create a new graph
            Graphx graph = new Graphx();

            // Get all the missing nodes
            List<long> MissingNodes = new List<long>(new long[] { cellID });

            // Get the nodes and build graph for numHops
            for (int i = 0; i < numHops; i++)
            {
                MissingNodes = webService_GetHop(graph, MissingNodes.ToArray());
            }

            //Tell the graph which cells are not fully populated
            graph.FrontierNodes = MissingNodes;

            var structLocations = db.ApproximateStructureLocations();

            foreach (ApproximateStructureLocationsResult result in structLocations)
            {
                if (result == null)
                    continue;

                if (graph.NodeList.ContainsKey(result.ParentID))
                {
                    Structure structure = graph.NodeList[result.ParentID];

                    if (structure.ParentID.HasValue)
                        graph.zLocationForSynapses.Add(result.ParentID, (long)Math.Round((double)result.Z));
                    else
                    {
                        graph.locationInfo.Add(result.ParentID, new LocationInfo((double)result.X, (double)result.Y, (double)result.Z, (double)result.Radius));
                        graph.InvolvedCells.Add(result.ParentID);
                    }
                }

                if (graph.FrontierNodes.Contains(result.ParentID))
                {
                    graph.locationInfo.Add(result.ParentID, new LocationInfo((double)result.X, (double)result.Y, (double)result.Z, (double)result.Radius));
                }
            }
           
            return graph;
        }

        
        public Structure[] webService_GetStructures(Graphx graph, long[] ids)
        {
            if (ids.Length == 0)
                return new Structure[0]; 

            // connect to the structure webservice 
            Structure[] FoundStructures = GetStructuresByIDs(ids, true);

            List<long> ListMissingChildrenIDs = new List<long>();

            //Add the root structure to nodelist if it not already added
            foreach (Structure structure in FoundStructures)
            {
                if (!graph.NodeList.ContainsKey(structure.ID))
                {
                    graph.NodeList.Add(structure.ID, structure);
                }
            }

            return FoundStructures;
        }

        public List<long> webService_GetHop(Graphx graph, long[] cellids)
        {
            if (cellids.Length == 0)
            {
                return new List<long>();
            }
            
            // Store all them missing structure ids and call webservice
            List<long> MissingRootStructureIds = new List<long>();

            foreach (long id in cellids)
            {
                // Test to see if the RootStructure is already in the nodelist            
                if (!graph.NodeList.ContainsKey(id))
                {
                    MissingRootStructureIds.Add(id);
                }
            }

            Structure[] MissingStructures = webService_GetStructures(graph, MissingRootStructureIds.ToArray());

            List<long> ListMissingChildrenIDs = new List<long>();

            foreach (Structure structure in MissingStructures)
            {
                if (structure.ChildIDs == null)
                    continue; 

                foreach (long childID in structure.ChildIDs)
                {
                    if (graph.NodeList.ContainsKey(childID) == false)
                    {
                        ListMissingChildrenIDs.Add(childID);
                    }
                }
            }

            //Find all synapses and gap junctions
            Structure[] ChildStructObjs = webService_GetStructures(graph, ListMissingChildrenIDs.ToArray());

            List<long> ListAbsentSiblings = new List<long>();

            //Find missing structures and populate the list
            foreach (Structure child in ChildStructObjs)
            {
                //Temp Hack to skip desmosomes
                if (child.Links == null)
                    continue; 

                foreach (StructureLink link in child.Links)
                {

                    if (!graph.NodeList.ContainsKey(link.SourceID))
                    {
                        ListAbsentSiblings.Add(link.SourceID);

                    }

                    if (!graph.NodeList.ContainsKey(link.TargetID))
                    {
                        ListAbsentSiblings.Add(link.TargetID);

                    }
                }
            }

            Structure[] SiblingStructures = webService_GetStructures(graph, ListAbsentSiblings.ToArray());

            //Find missing structures and populate the list
            foreach (Structure child in ChildStructObjs)
            {
                if (child.Links == null)
                    continue; 

                foreach (StructureLink link in child.Links)
                {
                    if (!graph.NodeList.ContainsKey(link.SourceID))
                    {
                        continue;
                    }

                    if (!graph.NodeList.ContainsKey(link.TargetID))
                    {
                        continue;
                    }

                    //After this point both nodes are already in the graph and we can create an edge
                    Structure SourceCell = graph.NodeList[link.SourceID];
                    Structure TargetCell = graph.NodeList[link.TargetID];

                    if (TargetCell.ParentID != null && SourceCell.ParentID != null)
                    {
                        string SourceTypeName = "";
                        if (StructureTypesDictionary.ContainsKey(SourceCell.TypeID))
                        {
                            SourceTypeName = StructureTypesDictionary[SourceCell.TypeID].Name;
                        }

                        Edgex E = new Edgex(SourceCell.ParentID.Value, TargetCell.ParentID.Value, link, SourceTypeName);
                        graph.EdgeList.Add(E);
                    }
                }
            }

            List<long> ListAbsentParents = new List<long>(SiblingStructures.Length);

            //Find a list of the parentIDs we are missing, and add them to the graph, and return them
            //so we can easily make another hop later
            foreach (Structure sibling in SiblingStructures)
            {
                if (sibling.ParentID.HasValue == false)
                    continue;

                if (graph.NodeList.ContainsKey(sibling.ParentID.Value))
                    continue;

                if (ListAbsentParents.Contains(sibling.ParentID.Value) == false)
                    ListAbsentParents.Add(sibling.ParentID.Value);
            }



            return ListAbsentParents;
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public long[] getStructuresByTypeID(int typeID)
        {
            long[] structuresList;

            IQueryable<long> res = from a in db.DBStructures where a.TypeID == typeID select a.ID;

            structuresList = res.ToArray();

            return structuresList;
        }

        // num=1 structures
        // num=0 locations
        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public string[] getTopConnectedStructures(int num)
        {

            SortedDictionary<long, long> topConnections = new SortedDictionary<long, long>();
            
            List<String> answer = new List<string>();

            if (num == 1)
            {
                var results = db.SelectNumConnectionsPerStructure();                

                foreach (var row in results)
                {
                    string type = (row.Label == null || String.IsNullOrEmpty(row.Label)) ? "[None]" : "[" + row.Label + "]";
                    answer.Add(type + "~" + row.StructureID + "~" + row.NumConnections);
                } 
            }


            else
            {
                var res = from t0 in db.DBLocations
                          from t1 in db.DBStructures
                          where
                            t1.ID == t0.ParentID &&
                            t1.ParentID == null
                          group t0 by new
                          {
                              t0.ParentID,
                              t1.Label
                          } into g
                          orderby
                             g.Count() descending
                          select new
                          {
                              id = g.Key.ParentID,
                              label = g.Key.Label,
                              count = g.Count()
                          };


                foreach (var row in res)
                {
                    string type = (row.label == null || String.IsNullOrEmpty(row.label)) ? "[None]" : "[" + row.label + "]";
                    answer.Add(row.id + "~" + type + "~" + row.count);
                }

                

            }
             
            return answer.ToArray();

        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public string[] getTopConnectedCells()
        {
            List<string> result = new List<string>();

            Dictionary<long, string> dictStructureLabels = _CreateStructureIDToLabelDict();

            foreach(DBStructure s in db.SelectRootStructures())
            {
                if(dictStructureLabels.ContainsKey(s.ID))
                {
                    result.Add(dictStructureLabels[s.ID] + "-" + s.ID.ToString());
                }
                else
                {
                    result.Add("Unlabeled-" + s.ID.ToString());
                }
            }

            return result.ToArray();

                /*
                 * 
            var res = from s in db.DBStructures where s.ParentID == null select s.ID;
            var res2 = from a in db.DBStructures where res.Contains(a.ID) select new { label = a.Label, id = a.ID };

            foreach (var item in res2)
            {
                result.Add(item.label.ToString() + "-" + item.id.ToString());
            }

            return result.ToArray();
                 */
        }


        private Dictionary<long, string> _CreateStructureTypeIDToNameDict()
        {
            Dictionary<long, string> structureTypes = new Dictionary<long, string>();

            var res = (from k in db.DBStructureTypes select new { id = k.ID, name = k.Name });

            foreach (var row in res)
                structureTypes[row.id] = row.name;

            return structureTypes;
        }

        private Dictionary<long, string> _CreateStructureIDToLabelDict()
        {
            Dictionary<long, string> labelDictionary = new Dictionary<long, string>();

            foreach (var row in db.SelectStructureLabels())
            {
                labelDictionary[row.ID] = row.Label; 
            }

            return labelDictionary; 
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public SynapseObject getSynapseStats()
        {
            SortedDictionary<long, long> topConnections = new SortedDictionary<long, long>();

            List<long> structureIDs = (from s in db.DBStructures where s.ParentID == null select s.ID).ToList<long>();

            Dictionary<long, string[]> result = new Dictionary<long, string[]>();

            Dictionary<long, string> structureTypeName = _CreateStructureTypeIDToNameDict();

            foreach (int id in structureIDs)
            {
                List<string> childCountList = new List<string>();
                int total_children = 0;

                foreach (var type_count_row in db.CountChildStructuresByType(id))
                {
                    total_children += type_count_row.Count.Value;
                    string output_val = structureTypeName[type_count_row.TypeID].Trim() + "," + type_count_row.Count.ToString();
                    childCountList.Add(output_val);
                }

                if(total_children == 0)
                    continue;

                childCountList.Insert(0, "Total," + total_children.ToString());
                result[id] = childCountList.ToArray();
            }

            SynapseObject retObj = new SynapseObject();
            Dictionary<long, string> labelDictionary = _CreateStructureIDToLabelDict();
            
            foreach (var row in result)
            {
                SynapseStats temp = new SynapseStats();
                temp.id = row.Key.ToString();
                if (labelDictionary.ContainsKey(row.Key))
                {
                   temp.id += "[" + labelDictionary[row.Key] + "]";
                }
                else
                {
                    temp.id += "[]";
                }
                temp.synapses = row.Value;
                retObj.objList.Add(temp);

            }


            return retObj;

        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public string[] getSynapses(int cellID)
        {
            Structure mainStructure = GetStructureByID(cellID, true);
            if(mainStructure.ChildIDs == null)
            {
                return new string[0];
            }

            Structure[] synapses = GetStructuresByIDs(mainStructure.ChildIDs, false);
            SortedDictionary<long, long> temp = new SortedDictionary<long, long>();

            temp[1] = synapses.Count();

            foreach (Structure child in synapses)
            {
                if (temp.Keys.Contains(child.TypeID))
                    temp[child.TypeID]++;
                else
                    temp[child.TypeID] = 1;
            }

            var temp2 = (from entry in temp orderby entry.Value ascending select entry);

            Dictionary<string, long> result = new Dictionary<string, long>();

            foreach (var tuple in temp2)
            {
                string name = GetStructureTypeByID(tuple.Key).Name;
                if (name == "Cell")
                    name = "Total Count";
                result[name] = tuple.Value;
            }

            List<string> ans = new List<string>();
            foreach (var row in result)
                ans.Add(row.Key + "," + row.Value);


            return ans.ToArray();

        }

        #endregion




    }
}