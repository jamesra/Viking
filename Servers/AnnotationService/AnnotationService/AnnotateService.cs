using System;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Activation;
using ConnectomeDataModel;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Web.Configuration;
using System.Transactions;
using System.Security.Permissions;
using System.Security;
using System.Threading.Tasks;

using Annotation.Service.Interfaces;
using Annotation.Service.GraphClasses;

namespace Annotation
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall)]
    [AspNetCompatibilityRequirements(RequirementsMode= AspNetCompatibilityRequirementsMode.Required)]
    public class AnnotateService : IAnnotateStructureTypes, IAnnotateStructures, IAnnotateLocations, IDisposable, ICircuit, ICredentials
    {
 
        public AnnotateService()
        {
            
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        bool ICredentials.CanRead()
        {
            return true;
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Modify")]
        bool ICredentials.CanWrite()
        {
            return true;
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Admin")]
        bool ICredentials.CanAdmin()
        {
            return true;
        }

        public static void ConfigureContextAsReadOnly(ConnectomeEntities db)
        {
            db.ConfigureAsReadOnly();
        }

        public static void ConfigureContextAsReadOnlyWithLazyLoading(ConnectomeEntities db)
        {
            //Note, disabling LazyLoading breaks loading of children and links unless they have been populated previously.
            db.ConfigureAsReadOnlyWithLazyLoading();
        }

        ConnectomeDataModel.ConnectomeEntities GetOrCreateDatabaseContext()
        {
            return new ConnectomeEntities(); 
            /*
            if (_db != null)
            {
                switch (_db.Database.Connection.State)
                {
                    case System.Data.ConnectionState.Open:
                        return _db;
                    case System.Data.ConnectionState.Closed:
                        try
                        {
                            _db.Database.Connection.Open();
                            return _db;
                        }
                        catch (InvalidOperationException e)
                        {
                            _db = null;
                        }
                        break;
                    case System.Data.ConnectionState.Broken:
                        _db = null;
                        break;
                    case System.Data.ConnectionState.Connecting: 
                    case System.Data.ConnectionState.Executing: 
                    case System.Data.ConnectionState.Fetching:
                        break; 
                    default:
                        _db = null;
                        break; 
                } 
            }

            if (_db == null)
            { 
                _db = new ConnectomeEntities();
            } 

            return _db;*/
        }

        ConnectomeDataModel.ConnectomeEntities _db;

        public ConnectomeDataModel.ConnectomeEntities GetOrCreateReadOnlyContext()
        {
            ConnectomeEntities db = GetOrCreateDatabaseContext();
            ConfigureContextAsReadOnly(db);
            return db; 
        }

        public ConnectomeDataModel.ConnectomeEntities GetOrCreateReadOnlyContextWithLazyLoading()
        {
            ConnectomeEntities db = GetOrCreateDatabaseContext();
            ConfigureContextAsReadOnlyWithLazyLoading(db);
            return db;
        }


        protected string ConnectomeEntities()
        {
            return VikingWebAppSettings.AppSettings.GetDefaultConnectionString();
        }

        #region IAnnotateStructureTypes Members

        [PrincipalPermission(SecurityAction.Demand, Role = "Modify")]
        public StructureType CreateStructureType(Annotation.StructureType new_structureType)
        {
            using (ConnectomeEntities db = GetOrCreateDatabaseContext())
            {
                ConnectomeDataModel.StructureType db_obj = new ConnectomeDataModel.StructureType();
                //Create the object to get the ID
                new_structureType.Sync(db_obj);
                db.StructureTypes.Add(db_obj);

                //db.Log = Console.Out;
                db.SaveChanges();
                Console.Out.Flush();

                StructureType output_obj = new StructureType(db_obj);
                return output_obj;
            }
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public StructureType[] GetStructureTypes()
        {
            using (ConnectomeEntities db = GetOrCreateReadOnlyContext())
            {
                IQueryable<ConnectomeDataModel.StructureType> queryResults = from t in db.StructureTypes select t;
                return queryResults.ToArray().Select(st => new StructureType(st)).ToArray();
            } 
        }

        /*
        public StructureTemplate[] GetStructureTemplates()
        {
            try
            {
                IQueryable<ConnectomeDataModel.StructureTemplates> queryResults = from t in db.StructureTemplates select t;
                List<StructureType> retList = new List<StructureType>(queryResults.Count());
                foreach (ConnectomeDataModel.StructureType dbt in queryResults)
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
            using (var db = GetOrCreateReadOnlyContext())
            {
                try
                {
                    ConnectomeDataModel.StructureType type = db.StructureTypes.Find(ID);
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
                catch (System.InvalidOperationException)
                {
                    //This means there was no row with that ID; 
                    Debug.WriteLine("Could not find requested location ID: " + ID.ToString());
                }
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
            using (ConnectomeEntities db = GetOrCreateReadOnlyContext())
            {
                try
                {
                    IQueryable<ConnectomeDataModel.Structure> structObjs = from s in db.Structures
                                                                           where s.TypeID == TypeID
                                                                           select s;

                    if (structObjs == null)
                        return new Structure[0];

                    var structObjList = structObjs.ToList<ConnectomeDataModel.Structure>();

                    return structObjs.ToList().Select(s => new Structure(s, false)).ToArray();
                }
                catch (Exception)
                {
                    return new Structure[0];
                }
            }
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public StructureType[] GetStructureTypesByIDs(long[] IDs)
        {

            List<long> ListIDs = new List<long>(IDs);
            
            //LINQ creates a SQL query with parameters when using contains, and there is a 2100 parameter limit.  So we cut the query into smaller chunks and run 
            //multiple queries
            ListIDs.Sort();  //Sort the list to slightly optimize the query

            int QueryChunkSize = 2000;

            using (var db = GetOrCreateReadOnlyContext())
            {

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
                        IQueryable<ConnectomeDataModel.StructureType> structTypeObjs = from s in db.StructureTypes
                                                                                       where s.ID >= minIDValue &&
                                                                                             s.ID <= maxIDValue &&
                                                                                             ShorterListIDs.Contains(s.ID)
                                                                                       select s;
                        if (structTypeObjs == null)
                            return null;

                        return structTypeObjs.ToList().Select(stype => new StructureType(stype)).ToArray();
                    }
                    catch (System.ArgumentNullException)
                    {
                        //This means there was no row with that ID; 
                        Debug.WriteLine("Could not find requested structure type IDs: " + IDs.ToString());
                    }
                    catch (System.InvalidOperationException)
                    {
                        //This means there was no row with that ID; 
                        Debug.WriteLine("Could not find requested structure type IDs: " + IDs.ToString());
                    } 
                }
            }

            return new StructureType[0];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Modify")]
        public long[] UpdateStructureTypes(StructureType[] structTypes)
        {
            return Update(structTypes);
        }

        /// <summary>
        /// Raise a SecurityException if the caller is not in the admin role
        /// </summary>
        protected void DemandAdminPermissions()
        {
            PrincipalPermission permission = new PrincipalPermission(null, "Admin");

            permission.Demand();
        }

        /// <summary>
        /// Raise a SecurityException if the caller is not in the admin role
        /// </summary>
        protected void DemandUser(string username)
        {
            PrincipalPermission permission = new PrincipalPermission(username, null);

            permission.Demand();
        }

        protected void DemandAdminOrUser(string username)
        {
            try{
                DemandAdminPermissions();
            }
            catch(SecurityException)
            {
                DemandUser(username);
            }
            
        }

        /// <summary>
        /// Submits passed structure types to the database
        /// </summary>
        /// <param name="structTypes"></param>
        /// <returns>Returns ID's of each object in the order they were passed. Used to recover ID's of inserted rows</returns>
        [PrincipalPermission(SecurityAction.Demand, Role = "Modify")]
        public long[] Update(StructureType[] structTypes)
        { 
            Dictionary<ConnectomeDataModel.StructureType, int> mapNewTypeToIndex = new Dictionary<ConnectomeDataModel.StructureType, int>(structTypes.Length);
            //Stores the ID of each object manipulated for the return value
            long[] listID = new long[structTypes.Length];

            using (var db = GetOrCreateDatabaseContext())
            { 
                try
                {

                    for (int iObj = 0; iObj < structTypes.Length; iObj++)
                    {
                        StructureType t = structTypes[iObj];

                        switch (t.DBAction)
                        {
                            case DBACTION.INSERT: 
                                ConnectomeDataModel.StructureType newType = new ConnectomeDataModel.StructureType();
                                t.Sync(newType);
                                db.StructureTypes.Add(newType);
                                mapNewTypeToIndex.Add(newType, iObj);
                                break;
                            case DBACTION.UPDATE:
                                ConnectomeDataModel.StructureType updateType;
                                try
                                {
                                    updateType = db.StructureTypes.Find(t.ID);
                                }
                                catch (System.ArgumentNullException)
                                {
                                    Debug.WriteLine("Could not find structuretype to update: " + t.ID.ToString());
                                    break;
                                }
                                catch (System.InvalidOperationException)
                                {
                                    Debug.WriteLine("Could not find structuretype to update: " + t.ID.ToString());
                                    break;
                                }

                                t.Sync(updateType);
                                listID[iObj] = updateType.ID;
                                //  db.ConnectomeDataModel.StructureTypes.(updateType);
                                break;
                            case DBACTION.DELETE:

                                DemandAdminPermissions();

                                ConnectomeDataModel.StructureType deleteType;
                                try
                                {
                                    deleteType = db.StructureTypes.Find(t.ID);
                                }
                                catch (System.ArgumentNullException)
                                {
                                    Debug.WriteLine("Could not find structuretype to delete: " + t.ID.ToString());
                                    break;
                                }
                                catch (System.InvalidOperationException)
                                {
                                    Debug.WriteLine("Could not find structuretype to update: " + t.ID.ToString());
                                    break;
                                }

                                deleteType.ID = t.ID;
                                listID[iObj] = deleteType.ID;
                                db.StructureTypes.Remove(deleteType);

                                break;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.ToString());
                    throw e;

                }

                db.SaveChanges();

                //Recover the ID's for new objects
                foreach (ConnectomeDataModel.StructureType newType in mapNewTypeToIndex.Keys)
                {
                    int iIndex = mapNewTypeToIndex[newType];
                    listID[iIndex] = newType.ID;
                }
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
            using (ConnectomeEntities db = GetOrCreateReadOnlyContext())
            {
                try
                {
                    //IQueryable<ConnectomeDataModel.Structure> queryStructures = from s in db.ConnectomeDataModel.Structures select s;
                    List<ConnectomeDataModel.Structure> listStructs = db.Structures.ToList();

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
            }

            return new Structure[0];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public Annotation.Structure[] GetStructuresForSection(long SectionNumber, long ModifiedAfterThisUtcTime, out long QueryExecutedTime, out long[] DeletedIDs)
        {
            DeletedIDs = new long[0];

            QueryExecutedTime = DateTime.Now.ToUniversalTime().Ticks;

            using (var db = GetOrCreateReadOnlyContext())
            {
                db.Configuration.LazyLoadingEnabled = false;
                db.Configuration.AutoDetectChangesEnabled = false;

                try
                { 
                    DateTime? ModifiedAfter = new DateTime?();
                    if (ModifiedAfterThisUtcTime > 0)
                        ModifiedAfter = new DateTime?(new DateTime(ModifiedAfterThisUtcTime, DateTimeKind.Unspecified));

                    ModifiedAfter = ConnectomeDataModel.ConnectomeEntities.ValidateDate(ModifiedAfter);
                    
                    Annotation.Structure[] retList = db.ReadSectionStructuresAndLinks(SectionNumber, ModifiedAfter).Select(s => new Annotation.Structure(s, false)).ToArray();

                    return retList;
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.ToString());
                }
            }
              
            return new Structure[0];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public Annotation.Structure[] GetStructuresForSectionInMosaicRegion(long section, BoundingRectangle bbox, double MinRadius, long ModifiedAfterThisUtcTime, out long QueryExecutedTime, out long[] DeletedIDs)
        {
            DateTime start = DateTime.UtcNow;
            TimeSpan elapsed;

            DeletedIDs = new long[0];

            QueryExecutedTime = DateTime.Now.ToUniversalTime().Ticks;

            using (var db = GetOrCreateReadOnlyContext())
            {
                db.Configuration.LazyLoadingEnabled = false;
                db.Configuration.AutoDetectChangesEnabled = false;

                try
                {
                    DateTime? ModifiedAfter = new DateTime?();
                    if (ModifiedAfterThisUtcTime > 0)
                        ModifiedAfter = new DateTime?(new DateTime(ModifiedAfterThisUtcTime, DateTimeKind.Unspecified));

                    ModifiedAfter = ConnectomeDataModel.ConnectomeEntities.ValidateDate(ModifiedAfter);

                    //Annotation.Structure[] retList = db.BoundedSectionStructures(bbox.ToGeometry(), (double)section).ToList().Select(s => new Annotation.Structure(s, false)).ToArray();

                    Annotation.Structure[] retList = db.ReadSectionStructuresAndLinksInMosaicRegion(section, bbox.ToGeometry(), MinRadius, ModifiedAfter).Select(s => new Annotation.Structure(s, false)).ToArray();

                    elapsed = new TimeSpan(DateTime.UtcNow.Ticks - start.Ticks);
                    Debug.WriteLine(section.ToString() + ": Query Structures: " + elapsed.TotalMilliseconds);


                    //Annotation.Structure[] retList = db.ReadSectionStructuresAndLinks(SectionNumber, ModifiedAfter).Select(s => new Annotation.Structure(s, false)).ToArray();

                    return retList;
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.ToString());
                }
            }

            return new Structure[0];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public Annotation.Structure[] GetStructuresForSectionInVolumeRegion(long section, BoundingRectangle bbox, double MinRadius, long ModifiedAfterThisUtcTime, out long QueryExecutedTime, out long[] DeletedIDs)
        {
            DateTime start = DateTime.UtcNow;
            TimeSpan elapsed;

            DeletedIDs = new long[0];

            QueryExecutedTime = DateTime.Now.ToUniversalTime().Ticks;

            using (var db = GetOrCreateReadOnlyContext())
            {
                db.Configuration.LazyLoadingEnabled = false;
                db.Configuration.AutoDetectChangesEnabled = false;

                try
                {
                    DateTime? ModifiedAfter = new DateTime?();
                    if (ModifiedAfterThisUtcTime > 0)
                        ModifiedAfter = new DateTime?(new DateTime(ModifiedAfterThisUtcTime, DateTimeKind.Unspecified));

                    ModifiedAfter = ConnectomeDataModel.ConnectomeEntities.ValidateDate(ModifiedAfter);

                    //Annotation.Structure[] retList = db.BoundedSectionStructures(bbox.ToGeometry(), (double)section).ToList().Select(s => new Annotation.Structure(s, false)).ToArray();

                    Annotation.Structure[] retList = db.ReadSectionStructuresAndLinksInVolumeRegion(section, bbox.ToGeometry(), MinRadius, ModifiedAfter).Select(s => new Annotation.Structure(s, false)).ToArray();

                    elapsed = new TimeSpan(DateTime.UtcNow.Ticks - start.Ticks);
                    Debug.WriteLine(section.ToString() + ": Query Structures: " + elapsed.TotalMilliseconds);


                    //Annotation.Structure[] retList = db.ReadSectionStructuresAndLinks(SectionNumber, ModifiedAfter).Select(s => new Annotation.Structure(s, false)).ToArray();

                    return retList;
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.ToString());
                }
            }

            return new Structure[0];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public Structure GetStructureByID(long ID, bool IncludeChildren)
        {
            using (var db = GetOrCreateReadOnlyContextWithLazyLoading())
            {
                try
                {
                    ConnectomeDataModel.Structure structObj = db.Structures.Find(ID);
                    if (structObj == null)
                        return null;

                    Structure newStruct = new Structure(structObj, IncludeChildren);

                    if (IncludeChildren)
                    {
                        var childStructures = (from s in db.Structures
                                                 where s.ParentID == structObj.ID
                                                 select s.ID);

                        newStruct.ChildIDs = childStructures.ToArray();
                    }
                    
                    return newStruct;
                }
                catch (System.ArgumentNullException)
                {
                    //This means there was no row with that ID; 
                    Debug.WriteLine("Could not find requested structure ID: " + ID.ToString());
                }
                catch (System.InvalidOperationException)
                {
                    //This means there was no row with that ID; 
                    Debug.WriteLine("Could not find requested structure ID: " + ID.ToString());
                }
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

            using (var db = GetOrCreateReadOnlyContext())
            {

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
                        IQueryable<ConnectomeDataModel.Structure> structObjs = from s in db.Structures
                                                                                            where s.ID >= minIDValue &&
                                                                                                  s.ID <= maxIDValue &&
                                                                                                  ShorterListIDs.Contains(s.ID)
                                                                                            select s;

                        IQueryable<ConnectomeDataModel.StructureLink> structLinks = from sl in db.StructureLinks
                                                                                    where ShorterListIDs.Contains(sl.SourceID) ||
                                                                                          ShorterListIDs.Contains(sl.TargetID)
                                                                                    select sl;

                        Dictionary<long, ConnectomeDataModel.Structure> dictStructures = structObjs.ToDictionary(s => s.ID);
                        db.AppendLinksToStructures(dictStructures, structLinks.ToList());

                        Dictionary<long, Structure> selected_structures = structObjs.ToList().Select(s => new Structure(s, false)).ToDictionary(s => s.ID);

                        if (IncludeChildren)
                        {
                            var childStructGroups = (from s in db.Structures
                                                     where s.ParentID.HasValue && ShorterListIDs.Contains(s.ParentID.Value)
                                                     group s.ID by s.ParentID.Value into ParentIDGroup
                                                     select ParentIDGroup);

                            foreach (var ParentStructure in childStructGroups)
                            {
                                if (selected_structures.ContainsKey(ParentStructure.Key))
                                {
                                    selected_structures[ParentStructure.Key].ChildIDs = ParentStructure.ToArray();
                                } 
                            } 
                        }

                        if (structObjs == null)
                            return new Structure[0];
                        
                        ListStructures.AddRange(selected_structures.Values);
                    }
                    catch (System.ArgumentNullException)
                    {
                        //This means there was no row with that ID; 
                        Debug.WriteLine("Could not find requested structure IDs: " + IDs.ToString());
                    }
                    catch (System.InvalidOperationException)
                    {
                        //This means there was no row with that ID; 
                        Debug.WriteLine("Could not find requested structure IDs: " + IDs.ToString());
                    }

                }

                if (ListStructures.Count > QueryChunkSize)
                {
                    Trace.Write("GetStructuresByID count > 2000.  Would have been affected by truncation bug in the past.");
                }
            }

            return ListStructures.ToArray();
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public void ApproximateStructureLocation(long ID)
        {
            using (ConnectomeEntities db = GetOrCreateDatabaseContext())
            {
                db.ApproximateStructureLocation(new int?((int)ID));
            }
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Admin")]
        public PermittedStructureLink CreatePermittedStructureLink(PermittedStructureLink link)
        {
            ConnectomeDataModel.PermittedStructureLink newRow = new ConnectomeDataModel.PermittedStructureLink();
            link.Sync(newRow);
            using (ConnectomeEntities db = GetOrCreateDatabaseContext())
            {
                db.PermittedStructureLink.Add(newRow);
                db.SaveChanges();
            }

            PermittedStructureLink newLink = new PermittedStructureLink(newRow);
            return newLink;
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Admin")]
        public void UpdatePermittedStructureLinks(PermittedStructureLink[] links)
        {
            //Stores the ID of each object manipulated for the return value
            using (ConnectomeEntities db = GetOrCreateDatabaseContext())
            {
                try
                {
                    for (int iObj = 0; iObj < links.Length; iObj++)
                    {
                        PermittedStructureLink obj = links[iObj];
                        ConnectomeDataModel.PermittedStructureLink DBObj = null;

                        switch (obj.DBAction)
                        {
                            case DBACTION.INSERT:

                                DBObj = new ConnectomeDataModel.PermittedStructureLink();
                                obj.Sync(DBObj);
                                db.PermittedStructureLink.Add(DBObj);
                                break;
                            case DBACTION.UPDATE:

                                try
                                {
                                    DBObj = (from u in db.PermittedStructureLink
                                             where u.SourceTypeID == obj.SourceTypeID &&
                                                   u.TargetTypeID == obj.TargetTypeID
                                             select u).Single();
                                }
                                catch (System.ArgumentNullException)
                                {
                                    Debug.WriteLine("Could not find structuretype to update: " + obj.ToString());
                                    break;
                                }
                                catch (System.InvalidOperationException)
                                {
                                    Debug.WriteLine("Could not find structuretype to update: " + obj.ToString());
                                    break;
                                }

                                obj.Sync(DBObj);
                                //  db.ConnectomeDataModel.StructureTypes.(updateType);
                                break;
                            case DBACTION.DELETE:
                                try
                                {
                                    DBObj = (from u in db.PermittedStructureLink
                                             where u.SourceTypeID == obj.SourceTypeID &&
                                                   u.TargetTypeID == obj.TargetTypeID
                                             select u).Single();
                                }
                                catch (System.ArgumentNullException)
                                {
                                    Debug.WriteLine("Could not find structuretype to delete: " + obj.ToString());
                                    break;
                                }
                                catch (System.InvalidOperationException)
                                {
                                    Debug.WriteLine("Could not find structuretype to update: " + obj.ToString());
                                    break;
                                }

                                db.PermittedStructureLink.Remove(DBObj);

                                break;
                        }

                        db.SaveChanges();
                    }


                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.ToString());
                    throw e;
                }
            }

            //Recover the ID's for new objects
            return;
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Modify")]
        public StructureLink CreateStructureLink(StructureLink link)
        {
            ConnectomeDataModel.StructureLink newRow = new ConnectomeDataModel.StructureLink();
            link.Sync(newRow);
            using (ConnectomeEntities db = GetOrCreateDatabaseContext())
            {
                db.StructureLinks.Add(newRow);
                db.SaveChanges();
            }

            StructureLink newLink = new StructureLink(newRow);
            return newLink; 
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Modify")]
        public void UpdateStructureLinks(StructureLink[] links)
        {
            //Stores the ID of each object manipulated for the return value
            using (ConnectomeEntities db = GetOrCreateDatabaseContext())
            {
                try
                {
                    for (int iObj = 0; iObj < links.Length; iObj++)
                    {
                        StructureLink obj = links[iObj];
                        ConnectomeDataModel.StructureLink DBObj = null;

                        switch (obj.DBAction)
                        {
                            case DBACTION.INSERT:

                                DBObj = new ConnectomeDataModel.StructureLink();
                                obj.Sync(DBObj);
                                db.StructureLinks.Add(DBObj);
                                break;
                            case DBACTION.UPDATE:

                                try
                                {
                                    DBObj = (from u in db.StructureLinks
                                             where u.SourceID == obj.SourceID &&
                                                   u.TargetID == obj.TargetID
                                             select u).Single();
                                }
                                catch (System.ArgumentNullException)
                                {
                                    Debug.WriteLine("Could not find structuretype to update: " + obj.ToString());
                                    break;
                                }
                                catch (System.InvalidOperationException)
                                {
                                    Debug.WriteLine("Could not find structuretype to update: " + obj.ToString());
                                    break;
                                }

                                obj.Sync(DBObj);
                                //  db.ConnectomeDataModel.StructureTypes.(updateType);
                                break;
                            case DBACTION.DELETE:
                                try
                                {
                                    DBObj = (from u in db.StructureLinks
                                             where u.SourceID == obj.SourceID &&
                                                   u.TargetID == obj.TargetID
                                             select u).Single();
                                }
                                catch (System.ArgumentNullException)
                                {
                                    Debug.WriteLine("Could not find structuretype to delete: " + obj.ToString());
                                    break;
                                }
                                catch (System.InvalidOperationException)
                                {
                                    Debug.WriteLine("Could not find structuretype to update: " + obj.ToString());
                                    break;
                                }

                                db.StructureLinks.Remove(DBObj);

                                break;
                        }

                        db.SaveChanges();
                    }


                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.ToString());
                    throw e;
                }
            }

            //Recover the ID's for new objects
            return;
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public long[] GetUnfinishedLocations(long structureID)
        {
            using (ConnectomeEntities db = GetOrCreateReadOnlyContext())
            {
                return (from id in db.SelectUnfinishedStructureBranches(structureID) select id.Value).ToArray<long>();
            }
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public LocationPositionOnly[] GetUnfinishedLocationsWithPosition(long structureID)
        {
            using (ConnectomeEntities db = GetOrCreateReadOnlyContext())
            {
                return db.SelectUnfinishedStructureBranchesWithPosition(structureID).ToList().Select(row => new LocationPositionOnly(row)).ToArray();
            }
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public StructureLink[] GetLinkedStructures()
        {
            using (var db = GetOrCreateDatabaseContext())
            {
                try
                {
                    IQueryable<ConnectomeDataModel.StructureLink> queryResults = from l in db.StructureLinks select l;
                    List<StructureLink> retList = new List<StructureLink>(queryResults.Count());
                    foreach (ConnectomeDataModel.StructureLink dbl in queryResults)
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
                catch (System.InvalidOperationException)
                {
                    //This means there was no row with that ID; 
                    Debug.WriteLine("Could not find StructureLinks");
                }
            }
            return new StructureLink[0];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public StructureLink[] GetLinkedStructuresByID(long ID)
        {
            using (ConnectomeEntities db = GetOrCreateDatabaseContext())
            {
                try
                {
                    IQueryable<ConnectomeDataModel.StructureLink> queryResults = from l in db.StructureLinks where (l.SourceID == ID || l.TargetID == ID) select l;
                    List<StructureLink> retList = new List<StructureLink>(queryResults.Count());
                    foreach (ConnectomeDataModel.StructureLink dbl in queryResults)
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
                catch (System.InvalidOperationException)
                {
                    //This means there was no row with that ID; 
                    Debug.WriteLine("Could not find StructureLinks for ID: " + ID.ToString());
                }

                return new StructureLink[0];
            }
        }

        public long[] GetNetworkedStructures(long[] IDs, int numHops)
        {
            using(var db = GetOrCreateReadOnlyContext())
            {
                
                return db.SelectNetworkStructureIDs(IDs, numHops).ToArray();
            } 
        }

        public Structure[] GetChildStructuresInNetwork(long[] IDs, int numHops)
        {
            using(var db = GetOrCreateReadOnlyContext())
            {
                var child_structs = db.SelectNetworkChildStructures(IDs, numHops);
                return child_structs.ToList().Select(s => new Structure(s, false)).ToArray();
            }
        }

        public StructureLink[] GetStructureLinksInNetwork(long[] IDs, int numHops)
        {
            using (var db = GetOrCreateReadOnlyContext())
            {
                var structure_links = db.SelectNetworkStructureLinks(IDs, numHops);
                return structure_links.ToList().Select(sl => new StructureLink(sl)).ToArray();
            }
        }


        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public Location[] GetLocationsForStructure(long structureID)
        {
            using (var db = GetOrCreateReadOnlyContext())
            {
                try
                {
                    IList<ConnectomeDataModel.Location> queryResults = db.ReadStructureLocationsAndLinks(structureID);
                    return queryResults.Select(loc => new Location(loc, true)).ToArray();
                }
                catch (System.ArgumentNullException)
                {
                    //This means there was no row with that ID; 
                    Debug.WriteLine("Could not find locations for ID: " + structureID.ToString());
                }
                catch (System.InvalidOperationException)
                {
                    //This means there was no row with that ID; 
                    Debug.WriteLine("Could not find locations for ID: " + structureID.ToString());
                }
            }

            return new Location[0];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public long NumberOfLocationsForStructure(long structureID)
        {
            using (ConnectomeEntities db = GetOrCreateReadOnlyContext())
            {
                try
                {
                    IQueryable<ConnectomeDataModel.Location> queryResults = from l in db.Locations where (l.ParentID == structureID) select l;
                    return queryResults.Count();
                }
                catch (System.ArgumentNullException)
                {
                    //This means there was no row with that ID; 
                    Debug.WriteLine("Could not find locations for ID: " + structureID.ToString());
                }
                catch (System.InvalidOperationException)
                {
                    //This means there was no row with that ID; 
                    Debug.WriteLine("Could not find locations for ID: " + structureID.ToString());
                }
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
            using (ConnectomeEntities db = GetOrCreateDatabaseContext())
            {
                Dictionary<ConnectomeDataModel.Structure, int> mapNewObjToIndex = new Dictionary<ConnectomeDataModel.Structure, int>(structures.Length);

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
                                ConnectomeDataModel.Structure newRow = new ConnectomeDataModel.Structure();
                                t.Sync(newRow);
                                db.Structures.Add(newRow);
                                mapNewObjToIndex.Add(newRow, iObj);
                                break;
                            case DBACTION.UPDATE:

                                ConnectomeDataModel.Structure updateRow;
                                try
                                {
                                    updateRow = db.Structures.Find(t.ID);
                                }
                                catch (System.ArgumentNullException)
                                {
                                    Debug.WriteLine("Could not find structuretype to update: " + t.ID.ToString());
                                    break;
                                }
                                catch (System.InvalidOperationException)
                                {
                                    Debug.WriteLine("Could not find structuretype to update: " + t.ID.ToString());
                                    break;
                                }

                                t.Sync(updateRow);
                                listID[iObj] = updateRow.ID;
                                //  db.ConnectomeDataModel.StructureTypes.(updateType);
                                break;
                            case DBACTION.DELETE:
                                ConnectomeDataModel.Structure deleteRow = new ConnectomeDataModel.Structure();
                                try
                                {
                                    deleteRow = db.Structures.Find(t.ID);
                                }
                                catch (System.ArgumentNullException)
                                {
                                    Debug.WriteLine("Could not find structuretype to update: " + t.ID.ToString());
                                    break;
                                }
                                catch (System.InvalidOperationException)
                                {
                                    Debug.WriteLine("Could not find structuretype to update: " + t.ID.ToString());
                                    break;
                                }


                                t.Sync(deleteRow);
                                deleteRow.ID = t.ID;
                                listID[iObj] = deleteRow.ID;


                                //Remove any links that exist before calling delete
                                db.StructureLinks.RemoveRange(deleteRow.SourceOfLinks.ToList());
                                db.StructureLinks.RemoveRange(deleteRow.TargetOfLinks.ToList());


                                db.Structures.Remove(deleteRow);
                                break;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.ToString());
                    throw e;

                }

                db.SaveChanges();

                //Recover the ID's for new objects
                foreach (ConnectomeDataModel.Structure newObj in mapNewObjToIndex.Keys)
                {
                    int iIndex = mapNewObjToIndex[newObj];
                    listID[iIndex] = newObj.ID;
                }

                return listID;
            }
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Modify")]
        public CreateStructureRetval CreateStructure(Structure structure, Location location)
        {
            using(var db = GetOrCreateDatabaseContext())
            {

                try
                {
                    ConnectomeDataModel.Structure DBStruct = db.Structures.Create();
                    structure.Sync(DBStruct);
                    db.Structures.Add(DBStruct);


                    ConnectomeDataModel.Location DBLoc = db.Locations.Create();
                    location.Sync(DBLoc);
                    db.Locations.Add(DBLoc);
                    DBLoc.Parent = DBStruct;
                    
                    db.SaveChanges();

                    //Return new ID's to the caller
                    CreateStructureRetval retval = new CreateStructureRetval(new Structure(DBStruct, false), new Location(DBLoc));
                    return retval; 
                }
                catch (System.Data.Entity.Validation.DbEntityValidationException e)
                {
                    foreach( var error in e.EntityValidationErrors)
                    {
                        Console.WriteLine(error);
                    }
                } 
            }

            return null;
        }

        /*
        [PrincipalPermission(SecurityAction.Demand, Role = "Modify")]
        public long[] CreateStructure(Structure structure, Location location)
        {
            ConnectomeDataModel.Structure DBStruct = new ConnectomeDataModel.Structure();
            structure.Sync(DBStruct);

            db.ConnectomeDataModel.Structures.InsertOnSubmit(DBStruct);

            ConnectomeDataModel.Location DBLoc = new ConnectomeDataModel.Location();
            location.Sync(DBLoc);
            DBLoc.ConnectomeDataModel.Structure = DBStruct;

            db.ConnectomeDataModel.Locations.InsertOnSubmit(DBLoc);

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
            using (ConnectomeEntities db = GetOrCreateDatabaseContext())
            {
                return db.MergeStructures(KeepID, MergeID);
            }
            
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
            /*
            SelectStructureChangeLog_Result result = db.SelectStructureChangeLog(structure_id, begin_time, end_time);
            List<SelectStructureChangeLog_Result> listChanges = new List<SelectStructureChangeLog_Result>(result);
            List<StructureHistory> structures = new List<StructureHistory>(listChanges.Count);
            foreach (SelectStructureChangeLog_Result row in listChanges)
            {
                structures.Add(new StructureHistory(row));
            }

            return structures.ToArray();
             */
            return new StructureHistory[0];
        }



        #endregion

        #region IAnnotateLocations Members

        [PrincipalPermission(SecurityAction.Demand, Role="Read")]
        public Location GetLocationByID(long ID)
        {
            try
            {
                using (ConnectomeEntities db = GetOrCreateReadOnlyContext())
                {
                    ConnectomeDataModel.Location obj = db.Locations.Find(ID);
                    if (obj == null)
                        return null;
                    Location retLoc = new Location(obj);
                    return retLoc;
                }
            }
            catch (System.ArgumentNullException)
            {
                //This means there was no row with that ID; 
                Debug.WriteLine("Could not find requested location ID: " + ID.ToString());
            }
            catch (System.InvalidOperationException)
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

            using (var db = GetOrCreateReadOnlyContext())
            {

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
                        IQueryable<ConnectomeDataModel.Location> locObjs = from s in db.Locations
                                                                            where s.ID >= minIDValue &&
                                                                                    s.ID <= maxIDValue &&
                                                                                    ShorterListIDs.Contains(s.ID)
                                                                            select s;
                        if (locObjs == null)
                            return null;

                        ListLocations.AddRange(locObjs.ToList().Select(loc => new Location(loc))); 
                    }
                    catch (System.ArgumentNullException)
                    {
                        //This means there was no row with that ID; 
                        Debug.WriteLine("Could not find requested location IDs: " + IDs.ToString());
                    }
                    catch (System.InvalidOperationException)
                    {
                        //This means there was no row with that ID; 
                        Debug.WriteLine("Could not find requested location IDs: " + IDs.ToString());
                    }
                }
            }

            return ListLocations.ToArray(); 

        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public Location GetLastModifiedLocation()
        {
            using (var db = GetOrCreateReadOnlyContext())
            {
                try
                {
                    string callingUser = ServiceModelUtil.GetUserForCall().Trim();
                    var LocationsByUser = db.SelectLastModifiedLocationByUsers(mergeOption:System.Data.Entity.Core.Objects.MergeOption.NoTracking);
                    ConnectomeDataModel.Location lastLocation = (from l in LocationsByUser where l.Username.Trim() == callingUser select l).FirstOrDefault<ConnectomeDataModel.Location>();
                    return new Location(lastLocation);
                }
                catch (Exception)
                {
                    return null;
                } 
            }
        }
        
        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public Location[] GetLocationsForSection(long section, out long QueryExecutedTime)
        {
            QueryExecutedTime = DateTime.Now.ToUniversalTime().Ticks;
            
            using (var db = GetOrCreateReadOnlyContext())
            {
                DateTime start = DateTime.UtcNow;

                db.Database.CommandTimeout = 30;
                
                try
                {
                    TimeSpan elapsed; 

                    /*
                    var dbLocLinks = db.ReadSectionLocationLinks(section, new DateTime?());
                    var dbLocs = db.ReadSectionLocations(section, new DateTime?());
                    */

                    var dbLocs = db.ReadSectionLocationsAndLinks(section, new DateTime?());
                    elapsed = new TimeSpan(DateTime.UtcNow.Ticks - start.Ticks);
                    Debug.WriteLine(section.ToString() + ": Query Locations: " + elapsed.TotalMilliseconds);

                    var Locations = dbLocs.Select(l => new Location(l, true));

                    elapsed = new TimeSpan(DateTime.UtcNow.Ticks - start.Ticks);
                    Debug.WriteLine(section.ToString() + ": Convert to Objects: " + elapsed.TotalMilliseconds);

                    //Dictionary<long, Location> dictLocations = Locations.ToDictionary(l => l.ID);
                    //Location.PopulateLinks(dictLocations, dbLocLinks.ToList());

                    //Location[] retList = dictLocations.Values.ToArray();
                    //elapsed = new TimeSpan(DateTime.UtcNow.Ticks - start.Ticks);
                    //Debug.WriteLine(section.ToString() + ": Add Links: " + elapsed.TotalMilliseconds);
                    return Locations.ToArray();
                }
                catch (System.ArgumentNullException)
                {
                    //This means there was no row with that ID; 
                    Debug.WriteLine("Could not find locations for section: " + section.ToString());
                }
                catch (System.InvalidOperationException)
                {
                    //This means there was no row with that ID; 
                    Debug.WriteLine("Could not find locations for section: " + section.ToString());
                }
            }

            return new Location[0];
             
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public Location[] GetLocationsForSectionMosaicRegion(long section, BoundingRectangle bbox, double MinRadius, out long QueryExecutedTime)
        {
            QueryExecutedTime = DateTime.Now.ToUniversalTime().Ticks;

            using (var db = GetOrCreateReadOnlyContext())
            {
                DateTime start = DateTime.Now;
                 

                try
                { 
                    IList<ConnectomeDataModel.Location> locations = db.ReadSectionLocationsAndLinksInMosaicRegion(section, bbox.ToGeometry(), MinRadius, new DateTime ?());
                    
                    Debug.WriteLine(section.ToString() + ": Query: " + new TimeSpan(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds);

                    Location[] retList = locations.Select(l => new Location(l, true)).ToArray();

                    Debug.WriteLine(section.ToString() + ": To list: " + new TimeSpan(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds);

                    return retList;
                }
                catch (System.ArgumentNullException)
                {
                    //This means there was no row with that ID; 
                    Debug.WriteLine("Could not find locations for section: " + section.ToString());
                }
                catch (System.InvalidOperationException)
                {
                    //This means there was no row with that ID; 
                    Debug.WriteLine("Could not find locations for section: " + section.ToString());
                }
            }

            return new Location[0]; 
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public Location[] GetLocationsForSectionVolumeRegion(long section, BoundingRectangle bbox, double MinRadius, out long QueryExecutedTime)
        {
            QueryExecutedTime = DateTime.Now.ToUniversalTime().Ticks;

            using (var db = GetOrCreateReadOnlyContext())
            {
                DateTime start = DateTime.Now;


                try
                {
                    IList<ConnectomeDataModel.Location> locations = db.ReadSectionLocationsAndLinksInVolumeRegion(section, bbox.ToGeometry(), MinRadius, new DateTime?());

                    Debug.WriteLine(section.ToString() + ": Query: " + new TimeSpan(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds);

                    Location[] retList = locations.Select(l => new Location(l, true)).ToArray();

                    Debug.WriteLine(section.ToString() + ": To list: " + new TimeSpan(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds);

                    return retList;
                }
                catch (System.ArgumentNullException)
                {
                    //This means there was no row with that ID; 
                    Debug.WriteLine("Could not find locations for section: " + section.ToString());
                }
                catch (System.InvalidOperationException)
                {
                    //This means there was no row with that ID; 
                    Debug.WriteLine("Could not find locations for section: " + section.ToString());
                }
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
        public Location[] GetLocationChangesInMosaicRegion(long section, BoundingRectangle bbox, double MinRadius, long? ModifiedAfterThisUtcTime, out long QueryExecutedTime, out long[] DeletedIDs)
        {
            using (var db = GetOrCreateReadOnlyContext())
            {
                DateTime start = DateTime.UtcNow;
                TimeSpan elapsed;

                DateTime? ModifiedAfterThisTime = new DateTime?();

                if(ModifiedAfterThisUtcTime.HasValue)
                    ModifiedAfterThisTime = new DateTime?(new DateTime(ModifiedAfterThisUtcTime.Value, DateTimeKind.Utc));

                ModifiedAfterThisTime = ConnectomeDataModel.ConnectomeEntities.ValidateDate(ModifiedAfterThisTime);

                DeletedIDs = new long[0];

                Location[] retList = new Location[0];

                QueryExecutedTime = start.Ticks;
                //try
                {

                    //var dbLocLinks = db.ReadSectionLocationsAndLinksInBounds(section, bbox.ToGeometry(), ModifiedAfterThisTime).ToList();
                    /*
                    elapsed = new TimeSpan(DateTime.UtcNow.Ticks - start.Ticks);
                    Debug.WriteLine(section.ToString() + ": Query Locations: " + elapsed.TotalMilliseconds);

                    var dbLocs = db.ReadSectionLocations(section, ModifiedAfterThisTime).ToList();
                    elapsed = new TimeSpan(DateTime.UtcNow.Ticks - start.Ticks);
                    Debug.WriteLine(section.ToString() + ": Query Locations: " + elapsed.TotalMilliseconds);
                    */
                    var dbLocs = db.ReadSectionLocationsAndLinksInMosaicRegion(section, bbox.ToGeometry(), MinRadius, ModifiedAfterThisTime);
                    elapsed = new TimeSpan(DateTime.UtcNow.Ticks - start.Ticks);
                    Debug.WriteLine(section.ToString() + ": Query Locations: " + elapsed.TotalMilliseconds);

                    var Locations = dbLocs.Select(l => new Location(l, true));

                    elapsed = new TimeSpan(DateTime.UtcNow.Ticks - start.Ticks);
                    Debug.WriteLine(section.ToString() + ": Convert to Objects: " + elapsed.TotalMilliseconds);

                    //Dictionary<long, Location> dictLocations = Locations.ToDictionary(l => l.ID);

                    //Location.PopulateLinks(dictLocations, dbLocLinks.ToList());

                    //elapsed = new TimeSpan(DateTime.UtcNow.Ticks - start.Ticks);
                    //Debug.WriteLine(section.ToString() + ": Add Links: " + elapsed.TotalMilliseconds);
                    retList = Locations.ToArray();
                }
                //TODO: Optimize this function to only return locations from the section we specify.  It currently returns all sections
                DeletedIDs = GetDeletedLocations(ModifiedAfterThisTime);

                return retList;
            }
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public AnnotationSet GetAnnotationsInMosaicRegion(long section, BoundingRectangle bbox, double MinRadius, long? ModifiedAfterThisUtcTime, out long QueryExecutedTime, out long[] DeletedIDs)
        {
            using (var db = GetOrCreateReadOnlyContext())
            {
                DateTime start = DateTime.UtcNow;
                TimeSpan elapsed;

                DateTime? ModifiedAfterThisTime = new DateTime?();

                if (ModifiedAfterThisUtcTime.HasValue)
                    ModifiedAfterThisTime = new DateTime?(new DateTime(ModifiedAfterThisUtcTime.Value, DateTimeKind.Utc));

                ModifiedAfterThisTime = ConnectomeDataModel.ConnectomeEntities.ValidateDate(ModifiedAfterThisTime);

                DeletedIDs = new long[0];

                AnnotationSet results = null;

                QueryExecutedTime = start.Ticks;
                //try
                {
                    AnnotationCollection dbAnnotations = db.ReadSectionAnnotationsInMosaicRegion(section, bbox.ToGeometry(), MinRadius, ModifiedAfterThisTime);
                    elapsed = new TimeSpan(DateTime.UtcNow.Ticks - start.Ticks);
                    Debug.WriteLine(section.ToString() + ": Query Section Annotations: " + elapsed.TotalMilliseconds);

                    Task<Structure[]> structConvTask = Task<Structure[]>.Run(() => { return dbAnnotations.Structures.Values.Select(s => new Structure(s, false)).ToArray(); });
                    Task<Location[]> locConvTask = Task<Location[]>.Run(() => { return dbAnnotations.Locations.Values.Select(l => new Location(l, true)).ToArray(); });

                    Task.WaitAll(structConvTask, locConvTask);

                    Structure[] structs = structConvTask.Result;
                    Location[] locs = locConvTask.Result;

                    results = new AnnotationSet(structs, locs);

                    elapsed = new TimeSpan(DateTime.UtcNow.Ticks - start.Ticks);
                    Debug.WriteLine(section.ToString() + ": Convert to Objects: " + elapsed.TotalMilliseconds);
                    
                    //elapsed = new TimeSpan(DateTime.UtcNow.Ticks - start.Ticks);
                    //Debug.WriteLine(section.ToString() + ": Add Links: " + elapsed.TotalMilliseconds);
                    
                }

                //TODO: Optimize this function to only return locations from the region we specify.  It currently returns all sections
                DeletedIDs = GetDeletedLocations(ModifiedAfterThisTime);

                return results;
            }
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
        public Location[] GetLocationChangesInVolumeRegion(long section, BoundingRectangle bbox, double MinRadius, long ModifiedAfterThisUtcTime, out long QueryExecutedTime, out long[] DeletedIDs)
        {
            using (var db = GetOrCreateReadOnlyContext())
            {
                DateTime start = DateTime.UtcNow;
                TimeSpan elapsed;

                DateTime? ModifiedAfterThisTime = new DateTime?();
                if (ModifiedAfterThisUtcTime > 0)
                    ModifiedAfterThisTime = new DateTime?(new DateTime(ModifiedAfterThisUtcTime, DateTimeKind.Utc));
                ModifiedAfterThisTime = ConnectomeDataModel.ConnectomeEntities.ValidateDate(ModifiedAfterThisTime);

                DeletedIDs = new long[0];

                Location[] retList = new Location[0];

                QueryExecutedTime = start.Ticks;
                //try
                {

                    //var dbLocLinks = db.ReadSectionLocationsAndLinksInBounds(section, bbox.ToGeometry(), ModifiedAfterThisTime).ToList();
                    /*
                    elapsed = new TimeSpan(DateTime.UtcNow.Ticks - start.Ticks);
                    Debug.WriteLine(section.ToString() + ": Query Locations: " + elapsed.TotalMilliseconds);

                    var dbLocs = db.ReadSectionLocations(section, ModifiedAfterThisTime).ToList();
                    elapsed = new TimeSpan(DateTime.UtcNow.Ticks - start.Ticks);
                    Debug.WriteLine(section.ToString() + ": Query Locations: " + elapsed.TotalMilliseconds);
                    */
                    var dbLocs = db.ReadSectionLocationsAndLinksInVolumeRegion(section, bbox.ToGeometry(), MinRadius, ModifiedAfterThisTime).Where(l => l.Radius > MinRadius);
                    elapsed = new TimeSpan(DateTime.UtcNow.Ticks - start.Ticks);
                    Debug.WriteLine(section.ToString() + ": Query Locations: " + elapsed.TotalMilliseconds);

                    var Locations = dbLocs.Select(l => new Location(l, true));

                    elapsed = new TimeSpan(DateTime.UtcNow.Ticks - start.Ticks);
                    Debug.WriteLine(section.ToString() + ": Convert to Objects: " + elapsed.TotalMilliseconds);

                    //Dictionary<long, Location> dictLocations = Locations.ToDictionary(l => l.ID);

                    //Location.PopulateLinks(dictLocations, dbLocLinks.ToList());

                    //elapsed = new TimeSpan(DateTime.UtcNow.Ticks - start.Ticks);
                    //Debug.WriteLine(section.ToString() + ": Add Links: " + elapsed.TotalMilliseconds);
                    retList = Locations.ToArray();
                }
                //TODO: Optimize this function to only return locations from the section we specify.  It currently returns all sections
                DeletedIDs = GetDeletedLocations(ModifiedAfterThisTime);

                return retList;
            }
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
            using (var db = GetOrCreateReadOnlyContext())
            {
                db.Database.CommandTimeout = 90;
                db.Configuration.LazyLoadingEnabled = false;
                db.Configuration.UseDatabaseNullSemantics = true;
                db.Configuration.AutoDetectChangesEnabled = false; 

                DateTime start = DateTime.UtcNow;
                TimeSpan elapsed;

                DateTime? ModifiedAfterThisTime = new DateTime?();
                if (ModifiedAfterThisUtcTime > 0)
                    ModifiedAfterThisTime = new DateTime?(new DateTime(ModifiedAfterThisUtcTime, DateTimeKind.Utc));
                    ModifiedAfterThisTime = ConnectomeDataModel.ConnectomeEntities.ValidateDate(ModifiedAfterThisTime);

                DeletedIDs = new long[0];

                Location[] retList = new Location[0];

                QueryExecutedTime = start.Ticks;
                //try
                {
                    db.Configuration.AutoDetectChangesEnabled = false;

                    /*var dbLocLinks = db.SelectSectionLocationLinks(section, ModifiedAfterThisTime).ToList() ;
                    elapsed = new TimeSpan(DateTime.UtcNow.Ticks - start.Ticks);
                    Debug.WriteLine(section.ToString() + ": Query Locations: " + elapsed.TotalMilliseconds);

                    var dbLocs = db.ReadSectionLocations(section, ModifiedAfterThisTime).ToList();
                    */
                    var dbLocs = db.ReadSectionLocationsAndLinks(section, ModifiedAfterThisTime);
                    elapsed = new TimeSpan(DateTime.UtcNow.Ticks - start.Ticks);
                    Debug.WriteLine(section.ToString() + ": Query Locations: " + elapsed.TotalMilliseconds);

                    var Locations = dbLocs.Select(l => new Location(l, true));

                    elapsed = new TimeSpan(DateTime.UtcNow.Ticks - start.Ticks);
                    Debug.WriteLine(section.ToString() + ": Convert to Objects: " + elapsed.TotalMilliseconds);
                    /*
                    Dictionary<long, Location> dictLocations = Locations.ToDictionary(l => l.ID);

                    Location.PopulateLinks(dictLocations, dbLocLinks.ToList());

                    elapsed = new TimeSpan(DateTime.UtcNow.Ticks - start.Ticks);
                    Debug.WriteLine(section.ToString() + ": Add Links: " + elapsed.TotalMilliseconds);
                    */

                    retList = Locations.ToArray();
                }
                /*
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
                */
                //TODO: Optimize this function to only return locations from the section we specify.  It currently returns all sections
                DeletedIDs = GetDeletedLocations(ModifiedAfterThisTime);

                return retList;
            }
        }


        /// <summary>
        /// TODO: Optimize this function to use the new change tracking tables
        /// </summary>
        /// <param name="DeletedAfterThisTime"></param>
        /// <returns>An array, may be zero length if no locations were deleted</returns>
        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public long[] GetDeletedLocations(DateTime? DeletedAfterThisTime)
        {
            //Try to find if any rows were deleted from the passed list of IDs
            DateTime start = DateTime.UtcNow; 

            if (!DeletedAfterThisTime.HasValue)
            {
                return new long[0];
            }

            using (ConnectomeEntities db = GetOrCreateReadOnlyContext())
            {
                try
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
                    IQueryable<long> queryResults = from l in db.DeletedLocations
                                                    where (l.DeletedOn > DeletedAfterThisTime)
                                                    select l.ID;

                    TimeSpan elapsed = new TimeSpan(DateTime.UtcNow.Ticks - start.Ticks);
                    Debug.WriteLine("\tDeleted Query: " + elapsed.TotalMilliseconds);

                    //Figure out which IDs are not in the returned list
                    return queryResults.ToArray();
                }
                catch (System.ArgumentNullException)
                {
                    //This means there was no row with that ID; 
                    Debug.WriteLine("Could not find deleted locations after " + DeletedAfterThisTime.ToString());
                }
                catch (System.InvalidOperationException)
                {
                    //This means there was no row with that ID; 
                    Debug.WriteLine("Could not find deleted locations after " + DeletedAfterThisTime.ToString());
                }
            }
             
            return new long[0]; 
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Modify")]
        public Location CreateLocation(Location new_location, long[] links)
        {

            using (var db = GetOrCreateDatabaseContext())
            {

                ConnectomeDataModel.Location db_obj = db.Locations.Create();
                string username = ServiceModelUtil.GetUserForCall();
                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        //Create the object to get the ID 
                        new_location.Sync(db_obj);
                        new_location.Username = username;
                        db.Locations.Add(db_obj);
                        db.SaveChanges();

                        //Build a new location link for every link in the array
                        List<ConnectomeDataModel.LocationLink> listLinks = new List<ConnectomeDataModel.LocationLink>(links.Length);
                        foreach (long linked_locationID in links)
                        {
                            ConnectomeDataModel.LocationLink created_link = _CreateLocationLink(db, db_obj.ID, linked_locationID, username);
                            listLinks.Add(created_link);
                        }

                        db.LocationLinks.AddRange(listLinks);
                        db.SaveChanges();
                        transaction.Commit();
                    }
                    catch (Exception e)
                    {
                        //transaction.Rollback();
                        throw e;
                    }
                }

                Location output_loc = new Location(db_obj);
                output_loc.Links = links;
                return output_loc;

            }
        }
         
        [PrincipalPermission(SecurityAction.Demand, Role = "Modify")]
        public long[] Update(Location[] locations)
        {
            Dictionary<ConnectomeDataModel.Location, int> mapNewTypeToIndex = new Dictionary<ConnectomeDataModel.Location, int>(locations.Length);

            //Stores the ID of each object manipulated for the return value
            long[] listID = new long[locations.Length];

            using (ConnectomeEntities db = GetOrCreateDatabaseContext())
            {
                try
                {
                    for (int iObj = 0; iObj < locations.Length; iObj++)
                    {
                        Location t = locations[iObj];

                        switch (t.DBAction)
                        {
                            case DBACTION.INSERT:

                                ConnectomeDataModel.Location newObj = new ConnectomeDataModel.Location();
                                t.Sync(newObj);
                                db.Locations.Add(newObj);
                                mapNewTypeToIndex.Add(newObj, iObj);
                                break;
                            case DBACTION.UPDATE:
                                ConnectomeDataModel.Location updateRow;
                                try
                                {
                                    updateRow = db.Locations.Find(t.ID);
                                }
                                catch (System.ArgumentNullException)
                                {
                                    Debug.WriteLine("Could not find structuretype to update: " + t.ID.ToString());
                                    break;
                                }
                                catch (System.InvalidOperationException)
                                {
                                    Debug.WriteLine("Could not find structuretype to update: " + t.ID.ToString());
                                    break;
                                }

                                t.Sync(updateRow);
                                listID[iObj] = updateRow.ID;
                                //  db.ConnectomeDataModel.StructureTypes.(updateType);
                                break;
                            case DBACTION.DELETE:
                                ConnectomeDataModel.Location deleteRow;
                                try
                                {
                                    deleteRow = db.Locations.Find(t.ID);
                                }
                                catch (System.ArgumentNullException)
                                {
                                    Debug.WriteLine("Could not find structuretype to update: " + t.ID.ToString());
                                    break;
                                }
                                catch (System.InvalidOperationException)
                                {
                                    Debug.WriteLine("Could not find structuretype to update: " + t.ID.ToString());
                                    break;
                                }

                                //Remove any links that exist before calling delete
                                db.LocationLinks.RemoveRange(deleteRow.LocationLinksA);
                                db.LocationLinks.RemoveRange(deleteRow.LocationLinksB);

                                t.Sync(deleteRow);
                                deleteRow.ID = t.ID;
                                listID[iObj] = deleteRow.ID;
                                db.Locations.Remove(deleteRow);

                                break;
                        }
                    }

                    db.SaveChanges();
                }
                catch (System.Data.Entity.Validation.DbEntityValidationException e)
                {
                    foreach (var error in e.EntityValidationErrors)
                    {
                        Console.WriteLine(error.ToString());
                    }
                }
            }

            //Recover the ID's for new objects
            foreach (ConnectomeDataModel.Location newObj in mapNewTypeToIndex.Keys)
            {
                int iIndex = mapNewTypeToIndex[newObj];
                listID[iIndex] = newObj.ID;
            }
             
            return listID;
        }

        private ConnectomeDataModel.LocationLink _CreateLocationLink(ConnectomeEntities db, long SourceID, long TargetID, string username)
        {
            if(username == null)
                username = ServiceModelUtil.GetUserForCall();

            ConnectomeDataModel.LocationLink newLink = db.LocationLinks.Create();
            ConnectomeDataModel.Location Source = null;
            ConnectomeDataModel.Location Target = null;

            try
            {
                Source = db.Locations.Find(SourceID);
                Target = db.Locations.Find(TargetID);                
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
                newLink.LocationA = Source;
                newLink.LocationB = Target;
            }
            else if (SourceID > TargetID)
            {
                newLink.LocationA = Target;
                newLink.LocationB = Source;
            }

            newLink.Username = username;

            return newLink; 
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Modify")]
        public void CreateLocationLink(long SourceID, long TargetID)
        {
            using (ConnectomeEntities db = new ConnectomeDataModel.ConnectomeEntities())
            {
                ConnectomeDataModel.LocationLink newLink = _CreateLocationLink(db, SourceID, TargetID, null);
                db.LocationLinks.Add(newLink);
                db.SaveChanges();
            }

            return;
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Modify")]
        public void DeleteLocationLink(long SourceID, long TargetID)
        {
            using (ConnectomeEntities db = GetOrCreateDatabaseContext())
            {
                ConnectomeDataModel.LocationLink link;
                bool LinkFound = false;
                try
                {
                    link = (from u in db.LocationLinks where u.A == SourceID && u.B == TargetID select u).Single();
                }
                catch (InvalidOperationException)
                {
                    //No link found
                    link = null;
                }

                if (link != null)
                {
                    db.LocationLinks.Remove(link);
                    LinkFound = true;
                }

                try
                {
                    link = (from u in db.LocationLinks where u.A == TargetID && u.B == SourceID select u).Single();
                }
                catch (InvalidOperationException)
                {
                    link = null;
                }

                if (link != null)
                {
                    db.LocationLinks.Remove(link);
                    LinkFound = true;
                }

                if (!LinkFound)
                {
                    throw new ArgumentException("DeleteLocationLink: The specified source or target does not exist");
                }

                db.SaveChanges();
            }
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public LocationLink[] GetLocationLinksForSection(long section, long ModifiedAfterThisUtcTime, out long QueryExecutedTime, out LocationLink[] DeletedLinks)
        {
            using (ConnectomeEntities db = GetOrCreateReadOnlyContext())
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
                    //IQueryable<ConnectomeDataModel.Location> queryResults = from l in db.ConnectomeDataModel.Locations where ((double)section) == l.Z select l;
                    var locationLinks = db.SelectSectionLocationLinks(section, ModifiedAfter);

                    Debug.WriteLine(section.ToString() + ": Query: " + new TimeSpan(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds);

                    //LocationLink[] retList = new LocationLink[locationLinks.Count];

                    //Debug.WriteLine(section.ToString() + ": To list: " + new TimeSpan(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds);

                    LocationLink[] retList = locationLinks.Select(link => new LocationLink(link)).ToArray();
                    Debug.WriteLine(section.ToString() + ": Loop: " + new TimeSpan(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds);

                    return retList;
                }
                catch (System.ArgumentNullException)
                {
                    //This means there was no row with that ID; 
                    Debug.WriteLine("Could not find locatioWat>cns for section: " + section.ToString());
                }
                catch (System.InvalidOperationException)
                {
                    //This means there was no row with that ID; 
                    Debug.WriteLine("Could not find locations for section: " + section.ToString());
                }
            }

            return new LocationLink[0];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public LocationLink[] GetLocationLinksForSectionInMosaicRegion(long section, BoundingRectangle bbox, double MinRadius, long ModifiedAfterThisUtcTime, out long QueryExecutedTime, out LocationLink[] DeletedLinks)
        {
            using (ConnectomeEntities db = GetOrCreateReadOnlyContext())
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
                    //IQueryable<ConnectomeDataModel.Location> queryResults = from l in db.ConnectomeDataModel.Locations where ((double)section) == l.Z select l;
                    var locationLinks = db.SelectSectionLocationLinksInMosaicBounds((double)section, bbox.ToGeometry(), MinRadius, ModifiedAfter);// (section, ModifiedAfter).ToList();

                    Debug.WriteLine(section.ToString() + ": Query: " + new TimeSpan(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds);
                    
                    LocationLink[] retList = locationLinks.Select(link => new LocationLink(link)).ToArray();
                    Debug.WriteLine(section.ToString() + ": Loop: " + new TimeSpan(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds);

                    return retList;
                }
                catch (System.ArgumentNullException)
                {
                    //This means there was no row with that ID; 
                    Debug.WriteLine("Could not find locatioWat>cns for section: " + section.ToString());
                }
                catch (System.InvalidOperationException)
                {
                    //This means there was no row with that ID; 
                    Debug.WriteLine("Could not find locations for section: " + section.ToString());
                }
            }

            return new LocationLink[0];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public LocationLink[] GetLocationLinksForSectionInVolumeRegion(long section, BoundingRectangle bbox, double MinRadius, long ModifiedAfterThisUtcTime, out long QueryExecutedTime, out LocationLink[] DeletedLinks)
        {
            using (ConnectomeEntities db = GetOrCreateReadOnlyContext())
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
                    //IQueryable<ConnectomeDataModel.Location> queryResults = from l in db.ConnectomeDataModel.Locations where ((double)section) == l.Z select l;
                    var locationLinks = db.SelectSectionLocationLinksInVolumeBounds((double)section, bbox.ToGeometry(), MinRadius, ModifiedAfter);// (section, ModifiedAfter).ToList();

                    Debug.WriteLine(section.ToString() + ": Query: " + new TimeSpan(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds);

                    LocationLink[] retList = locationLinks.Select(link => new LocationLink(link)).ToArray();
                    Debug.WriteLine(section.ToString() + ": Loop: " + new TimeSpan(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds);

                    return retList;
                }
                catch (System.ArgumentNullException)
                {
                    //This means there was no row with that ID; 
                    Debug.WriteLine("Could not find locatioWat>cns for section: " + section.ToString());
                }
                catch (System.InvalidOperationException)
                {
                    //This means there was no row with that ID; 
                    Debug.WriteLine("Could not find locations for section: " + section.ToString());
                }
            }

            return new LocationLink[0];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public long[] GetLinkedLocations(long ID)
        {
            using (ConnectomeEntities db = GetOrCreateDatabaseContext())
            {
                var links = (from u in db.LocationLinks where u.A == ID select u.B).Union(from u in db.LocationLinks where u.B == ID select u.A);
                return links.ToArray();
            }
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public LocationHistory[] GetLocationChangeLog(long? structure_id, DateTime? begin_time, DateTime? end_time)
        {
            using (ConnectomeEntities db = GetOrCreateReadOnlyContext())
            {
                var result = db.SelectStructureLocationChangeLog(structure_id, begin_time, end_time);
                List<SelectStructureLocationChangeLog_Result> listChanges = result.ToList();

                return listChanges.Select(loc => new LocationHistory(loc)).ToArray();
            }
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
            using (ConnectomeEntities db = GetOrCreateDatabaseContext())
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
           
                foreach (var result in structLocations)
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

            using (ConnectomeEntities db = GetOrCreateReadOnlyContext())
            {

                IQueryable<long> res = from a in db.Structures where a.TypeID == typeID select a.ID;

                structuresList = res.ToArray();
            }

            return structuresList;
        }

        // num=1 structures
        // num=0 locations
        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public string[] getTopConnectedStructures(int num)
        {
            using (ConnectomeEntities db = GetOrCreateDatabaseContext())
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
                    var res = from t0 in db.Locations
                              from t1 in db.Structures
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
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public string[] getTopConnectedCells()
        {
            List<string> result = new List<string>();

            using (ConnectomeEntities db = GetOrCreateReadOnlyContext())
            {

                Dictionary<long, string> dictStructureLabels = _CreateStructureIDToLabelDict();

                foreach (ConnectomeDataModel.Structure s in db.SelectRootStructures())
                {
                    if (dictStructureLabels.ContainsKey(s.ID))
                    {
                        result.Add(dictStructureLabels[s.ID] + "-" + s.ID.ToString());
                    }
                    else
                    {
                        result.Add("Unlabeled-" + s.ID.ToString());
                    }
                }
            }

            return result.ToArray();

                /*
                 * 
            var res = from s in db.ConnectomeDataModel.Structures where s.ParentID == null select s.ID;
            var res2 = from a in db.ConnectomeDataModel.Structures where res.Contains(a.ID) select new { label = a.Label, id = a.ID };

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

            using (ConnectomeEntities db = GetOrCreateDatabaseContext())
            {

                var res = (from k in db.StructureTypes select new { id = k.ID, name = k.Name });

                foreach (var row in res)
                    structureTypes[row.id] = row.name;
            }

            return structureTypes;
        }

        private Dictionary<long, string> _CreateStructureIDToLabelDict()
        {
            Dictionary<long, string> labelDictionary = new Dictionary<long, string>();

            using (ConnectomeEntities db = GetOrCreateDatabaseContext())
            {
                foreach (var row in db.SelectStructureLabels())
                {
                    labelDictionary[row.ID] = row.Label;
                }
            }

            return labelDictionary; 
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Read")]
        public SynapseObject getSynapseStats()
        {
            SortedDictionary<long, long> topConnections = new SortedDictionary<long, long>();

            using (ConnectomeEntities db = GetOrCreateDatabaseContext())
            {

                List<long> structureIDs = (from s in db.Structures where s.ParentID == null select s.ID).ToList<long>();

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

                    if (total_children == 0)
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