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

using AnnotationService.Interfaces;
using AnnotationService.Types;

namespace Annotation
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    [AspNetCompatibilityRequirements(RequirementsMode = AspNetCompatibilityRequirementsMode.Required)]
    public class AnnotateService : IAnnotateStructureTypes,
        IAnnotatePermittedStructureLinks,
        IAnnotateStructures,
        IAnnotateLocations, ICircuit, ICredentials, IVolumeMeta
    {
        static bool _isSqlTypesLoaded = false;

        static object lockObject = new object();

        public static void TryLoadSqlServerTypes()
        {
            if (_isSqlTypesLoaded)
                return;

            lock (lockObject)
            {
                if (_isSqlTypesLoaded)
                    return;

                try
                {
                 //   ConnectomeDataModel.Configuration.LoadNativeAssemblies(System.Web.HttpContext.Current.Server.MapPath("~"));
                    SqlServerTypes.Utilities.LoadNativeAssemblies(System.Web.HttpContext.Current.Server.MapPath("~"));
                    _isSqlTypesLoaded = true;
                    return;
                }
                catch (NullReferenceException)
                {
                    SqlServerTypes.Utilities.LoadNativeAssemblies(AppDomain.CurrentDomain.BaseDirectory);
                    //ConnectomeDataModel.Configuration.LoadNativeAssemblies(AppDomain.CurrentDomain.BaseDirectory);
                    _isSqlTypesLoaded = true;
                    return;
                }
            }
        }

        static AnnotateService()
        {
            TryLoadSqlServerTypes(); 
        }

        public AnnotateService()
        {
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        bool ICredentials.CanRead()
        {
            return true;
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Annotator")]
        bool ICredentials.CanWrite()
        {
            return true;
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Reviewer")]
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

        #region IVolumeMeta Members

        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public Scale GetScale()
        {
            using (ConnectomeEntities db = GetOrCreateDatabaseContext())
            {
                AxisUnits X = new AnnotationService.Types.AxisUnits(db.GetXYScale(), db.GetXYUnits());
                AxisUnits Y = new AnnotationService.Types.AxisUnits(X.Value, X.Units);
                AxisUnits Z = new AnnotationService.Types.AxisUnits(db.GetZScale(), db.GetZUnits());

                Scale scale = new AnnotationService.Types.Scale(X, Y, Z);

                return scale;
            }
        }

        #endregion


        #region IAnnotateStructureTypes Members

        [PrincipalPermission(SecurityAction.Demand, Role = "Annotator")]
        public AnnotationService.Types.StructureType CreateStructureType(AnnotationService.Types.StructureType new_structureType)
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

                AnnotationService.Types.StructureType output_obj = db_obj.Create();
                return output_obj;
            }
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public AnnotationService.Types.StructureType[] GetStructureTypes()
        {
            using (ConnectomeEntities db = GetOrCreateReadOnlyContextWithLazyLoading())
            {
                IQueryable<ConnectomeDataModel.StructureType> queryResults = from t in db.StructureTypes select t;
                return queryResults.ToArray().Select(st => st.Create()).ToArray();
            }
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public AnnotationService.Types.PermittedStructureLink[] GetPermittedStructureLinks()
        {
            using (ConnectomeEntities db = GetOrCreateReadOnlyContext())
            {
                IQueryable<ConnectomeDataModel.PermittedStructureLink> queryResults = from psl in db.PermittedStructureLink select psl;
                return queryResults.ToArray().Select(psl => psl.Create()).ToArray();
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

        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public AnnotationService.Types.StructureType GetStructureTypeByID(long ID)
        {
            using (var db = GetOrCreateReadOnlyContextWithLazyLoading())
            {
                try
                {
                    ConnectomeDataModel.StructureType type = db.StructureTypes.Find(ID);
                    if (type == null)
                        return null;

                    AnnotationService.Types.StructureType newType = type.Create();
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

        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public AnnotationService.Types.Structure[] GetStructuresForType(long TypeID)
        {
            return GetStructuresOfType(TypeID);
        }


        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public AnnotationService.Types.Structure[] GetStructuresOfType(long TypeID)
        {
            using (ConnectomeEntities db = GetOrCreateReadOnlyContext())
            {
                try
                {
                    IQueryable<ConnectomeDataModel.Structure> structObjs = from s in db.Structures
                                                                           where s.TypeID == TypeID
                                                                           select s;

                    if (structObjs == null)
                        return new AnnotationService.Types.Structure[0];

                    var structObjList = structObjs.ToList<ConnectomeDataModel.Structure>();

                    return structObjs.ToList().Select(s => s.Create(false)).ToArray();
                }
                catch (Exception)
                {
                    return new AnnotationService.Types.Structure[0];
                }
            }
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public AnnotationService.Types.StructureType[] GetStructureTypesByIDs(long[] IDs)
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

                        return structTypeObjs.ToList().Select(stype => stype.Create()).ToArray();
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

            return new AnnotationService.Types.StructureType[0];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Annotator")]
        public long[] UpdateStructureTypes(AnnotationService.Types.StructureType[] structTypes)
        {
            return Update(structTypes);
        }

        /// <summary>
        /// Raise a SecurityException if the caller is not in the admin role
        /// </summary>
        protected void DemandAdminPermissions()
        {
            PrincipalPermission permission = new PrincipalPermission(null, "Reviewer");

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
            try
            {
                DemandAdminPermissions();
            }
            catch (SecurityException)
            {
                DemandUser(username);
            }

        }

        /// <summary>
        /// Submits passed structure types to the database
        /// </summary>
        /// <param name="structTypes"></param>
        /// <returns>Returns ID's of each object in the order they were passed. Used to recover ID's of inserted rows</returns>
        [PrincipalPermission(SecurityAction.Demand, Role = "Annotator")]
        public long[] Update(AnnotationService.Types.StructureType[] structTypes)
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
                        AnnotationService.Types.StructureType t = structTypes[iObj];

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

        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
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
        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public AnnotationService.Types.Structure[] GetStructures()
        {
            using (ConnectomeEntities db = GetOrCreateReadOnlyContext())
            {
                try
                {
                    //IQueryable<ConnectomeDataModel.Structure> queryStructures = from s in db.ConnectomeDataModel.Structures select s;
                    List<ConnectomeDataModel.Structure> listStructs = db.Structures.AsNoTracking().ToList();

                    AnnotationService.Types.Structure[] retList = new AnnotationService.Types.Structure[listStructs.Count()];

                    for (int iStruct = 0; iStruct < listStructs.Count(); iStruct++)
                    {
                        //Get structures does not include children because 
                        //if you have all the structures you can create the
                        //graph yourself by looking at ParentIDs without 
                        //sending duplicate information over the wire
                        retList[iStruct] = listStructs[iStruct].Create(false);
                    }

                    return retList;
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.ToString());
                }
            }

            return new AnnotationService.Types.Structure[0];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public AnnotationService.Types.Structure[] GetStructuresForSection(long SectionNumber, long ModifiedAfterThisUtcTime, out long QueryExecutedTime, out long[] DeletedIDs)
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

                    AnnotationService.Types.Structure[] retList = db.ReadSectionStructuresAndLinks(SectionNumber, ModifiedAfter).Select(s => s.Create(false)).ToArray();

                    return retList;
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.ToString());
                }
            }

            return new AnnotationService.Types.Structure[0];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public AnnotationService.Types.Structure[] GetStructuresForSectionInMosaicRegion(long section, BoundingRectangle bbox, double MinRadius, long ModifiedAfterThisUtcTime, out long QueryExecutedTime, out long[] DeletedIDs)
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

                    AnnotationService.Types.Structure[] retList = db.ReadSectionStructuresAndLinksInMosaicRegion(section, bbox.ToGeometry(), MinRadius, ModifiedAfter).Select(s => s.Create(false)).ToArray();

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

            return new AnnotationService.Types.Structure[0];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public AnnotationService.Types.Structure[] GetStructuresForSectionInVolumeRegion(long section, BoundingRectangle bbox, double MinRadius, long ModifiedAfterThisUtcTime, out long QueryExecutedTime, out long[] DeletedIDs)
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

                    AnnotationService.Types.Structure[] retList = db.ReadSectionStructuresAndLinksInVolumeRegion(section, bbox.ToGeometry(), MinRadius, ModifiedAfter).Select(s => s.Create(false)).ToArray();

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

            return new AnnotationService.Types.Structure[0];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public AnnotationService.Types.Structure GetStructureByID(long ID, bool IncludeChildren)
        {
            using (var db = GetOrCreateReadOnlyContextWithLazyLoading())
            {
                try
                {
                    ConnectomeDataModel.Structure structObj = db.Structures.Find(ID);
                    if (structObj == null)
                        return null;

                    AnnotationService.Types.Structure newStruct = structObj.Create(IncludeChildren);

                    if (IncludeChildren)
                    {
                        var childStructures = (from s in db.Structures.AsNoTracking()
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

        /// <summary>
        /// Fetch a list of IDs, the input must be smaller than our chunk size.  Used to divide requests into tasks
        /// </summary>
        /// <param name="IDs"></param>
        /// <param name="IncludeChildren"></param>
        /// <returns></returns>
        private List<AnnotationService.Types.Structure> GetStructureByIDsChunk(long[] IDs, bool IncludeChildren)
        {
            List<AnnotationService.Types.Structure> ListStructures = new List<AnnotationService.Types.Structure>(IDs.Length);
            //I do this hoping that it will allow SQL to not check the entire table for each chunk
            long minIDValue = IDs[0];
            long maxIDValue = IDs[IDs.Length - 1];

            using (var db = GetOrCreateReadOnlyContext())
            {
                try
                {
                    IQueryable<ConnectomeDataModel.Structure> structObjs = from s in db.Structures.AsNoTracking()
                                                                           where s.ID >= minIDValue &&
                                                                                 s.ID <= maxIDValue &&
                                                                                 IDs.Contains(s.ID)
                                                                           select s;

                    IQueryable<ConnectomeDataModel.StructureLink> structLinks = from sl in db.StructureLinks.AsNoTracking()
                                                                                where IDs.Contains(sl.SourceID) ||
                                                                                      IDs.Contains(sl.TargetID)
                                                                                select sl;

                    Dictionary<long, ConnectomeDataModel.Structure> dictStructures = structObjs.ToDictionary(s => s.ID);
                    db.AppendLinksToStructures(dictStructures, structLinks.ToList());

                    Dictionary<long, AnnotationService.Types.Structure> selected_structures = structObjs.ToList().Select(s => s.Create(false)).ToDictionary(s => s.ID);

                    if (IncludeChildren)
                    {
                        var childStructGroups = (from s in db.Structures.AsNoTracking()
                                                 where s.ParentID.HasValue && IDs.Contains(s.ParentID.Value)
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
                        return new List<AnnotationService.Types.Structure>();

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

            return ListStructures;
        }


        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public AnnotationService.Types.Structure[] GetStructuresByIDs(long[] IDs, bool IncludeChildren)
        {

            List<AnnotationService.Types.Structure> ListStructures = new List<AnnotationService.Types.Structure>(IDs.Length);
            //LINQ creates a SQL query with parameters when using contains, and there is a 2100 parameter limit.  So we cut the query into smaller chunks and run 
            //multiple queries.  Since we are stuck doing this I run the query in parallel
            uint QueryChunkSize = 1024;

            var chunks = IDs.SortAndChunk(QueryChunkSize);

            if(chunks.Count > 1)
                Trace.WriteLine(string.Format("Dividing GetStructuresByIDs for {0} keys in {1} chunks", IDs.Length, chunks.Count));

            //We won't spawn any tasks if we only have one chunk.
            Task<List<AnnotationService.Types.Structure>>[] tasks = new Task<List<AnnotationService.Types.Structure>>[chunks.Count];
            
            for(int iChunk = 1; iChunk < chunks.Count; iChunk++)
            {
                long[] chunk = chunks[iChunk];
                tasks[iChunk] = Task.Run(() => GetStructureByIDsChunk(chunk, IncludeChildren));
            }
             
            ListStructures = GetStructureByIDsChunk(chunks[0], IncludeChildren);

            for (int iChunk = 1; iChunk < chunks.Count; iChunk++)
            {
                ListStructures.AddRange(tasks[iChunk].Result);
            }

            return ListStructures.ToArray();
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public void ApproximateStructureLocation(long ID)
        {
            using (ConnectomeEntities db = GetOrCreateDatabaseContext())
            {
                db.ApproximateStructureLocation(new int?((int)ID));
            }
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Reviewer")]
        public AnnotationService.Types.PermittedStructureLink CreatePermittedStructureLink(AnnotationService.Types.PermittedStructureLink link)
        {
            ConnectomeDataModel.PermittedStructureLink newRow = new ConnectomeDataModel.PermittedStructureLink();
            link.Sync(newRow);
            using (ConnectomeEntities db = GetOrCreateDatabaseContext())
            {
                db.PermittedStructureLink.Add(newRow);
                db.SaveChanges();
            }

            AnnotationService.Types.PermittedStructureLink newLink = newRow.Create();
            return newLink;
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Reviewer")]
        public void UpdatePermittedStructureLinks(AnnotationService.Types.PermittedStructureLink[] links)
        {
            //Stores the ID of each object manipulated for the return value
            using (ConnectomeEntities db = GetOrCreateDatabaseContext())
            {
                try
                {
                    for (int iObj = 0; iObj < links.Length; iObj++)
                    {
                        AnnotationService.Types.PermittedStructureLink obj = links[iObj];
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

        [PrincipalPermission(SecurityAction.Demand, Role = "Annotator")]
        public AnnotationService.Types.StructureLink CreateStructureLink(AnnotationService.Types.StructureLink link)
        {
            ConnectomeDataModel.StructureLink newRow = new ConnectomeDataModel.StructureLink();
            link.Sync(newRow);
            using (ConnectomeEntities db = GetOrCreateDatabaseContext())
            {
                db.StructureLinks.Add(newRow);
                db.SaveChanges();
            }

            AnnotationService.Types.StructureLink newLink = newRow.Create();
            return newLink;
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Annotator")]
        public void UpdateStructureLinks(AnnotationService.Types.StructureLink[] links)
        {
            //Stores the ID of each object manipulated for the return value
            using (ConnectomeEntities db = GetOrCreateDatabaseContext())
            {
                try
                {
                    for (int iObj = 0; iObj < links.Length; iObj++)
                    {
                        AnnotationService.Types.StructureLink obj = links[iObj];
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

        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public long[] GetUnfinishedLocations(long structureID)
        {
            using (ConnectomeEntities db = GetOrCreateReadOnlyContext())
            {
                return (from id in db.SelectUnfinishedStructureBranches(structureID) select id.Value).ToArray<long>();
            }
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public LocationPositionOnly[] GetUnfinishedLocationsWithPosition(long structureID)
        {
            using (ConnectomeEntities db = GetOrCreateReadOnlyContext())
            {
                return db.SelectUnfinishedStructureBranchesWithPosition(structureID).ToList().Select(row => row.Create()).ToArray();
            }
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public AnnotationService.Types.StructureLink[] GetLinkedStructures()
        {
            using (var db = GetOrCreateDatabaseContext())
            {
                try
                {
                    IQueryable<ConnectomeDataModel.StructureLink> queryResults = from l in db.StructureLinks.AsNoTracking() select l;
                    List<AnnotationService.Types.StructureLink> retList = new List<AnnotationService.Types.StructureLink>(queryResults.Count());
                    foreach (ConnectomeDataModel.StructureLink dbl in queryResults)
                    {
                        AnnotationService.Types.StructureLink link = dbl.Create();
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
            return new AnnotationService.Types.StructureLink[0];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public AnnotationService.Types.StructureLink[] GetLinkedStructuresByID(long ID)
        {
            using (ConnectomeEntities db = GetOrCreateDatabaseContext())
            {
                try
                {
                    IQueryable<ConnectomeDataModel.StructureLink> queryResults = from l in db.StructureLinks.AsNoTracking() where (l.SourceID == ID || l.TargetID == ID) select l;
                    List<AnnotationService.Types.StructureLink> retList = new List<AnnotationService.Types.StructureLink>(queryResults.Count());
                    foreach (ConnectomeDataModel.StructureLink dbl in queryResults)
                    {
                        AnnotationService.Types.StructureLink link = dbl.Create();
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

                return new AnnotationService.Types.StructureLink[0];
            }
        }

        public long[] GetNetworkedStructures(long[] IDs, int numHops)
        {
            using (var db = GetOrCreateReadOnlyContext())
            {

                return db.SelectNetworkStructureIDs(IDs, numHops).ToArray();
            }
        }

        public AnnotationService.Types.Structure[] GetChildStructuresInNetwork(long[] IDs, int numHops)
        {
            using (var db = GetOrCreateReadOnlyContext())
            {
                var child_structs = db.SelectNetworkChildStructures(IDs, numHops);
                return child_structs.ToList().Select(s => s.Create(false)).ToArray();
            }
        }

        public AnnotationService.Types.StructureLink[] GetStructureLinksInNetwork(long[] IDs, int numHops)
        {
            using (var db = GetOrCreateReadOnlyContext())
            {
                var structure_links = db.SelectNetworkStructureLinks(IDs, numHops);
                return structure_links.ToList().Select(sl => sl.Create()).ToArray();
            }
        }


        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public AnnotationService.Types.Location[] GetLocationsForStructure(long structureID)
        {
            using (var db = GetOrCreateReadOnlyContext())
            {
                try
                {
                    IList<ConnectomeDataModel.Location> queryResults = db.ReadStructureLocationsAndLinks(structureID);
                    return queryResults.Select(loc => loc.Create(true)).ToArray();
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

            return new AnnotationService.Types.Location[0];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public long NumberOfLocationsForStructure(long structureID)
        {
            using (ConnectomeEntities db = GetOrCreateReadOnlyContext())
            {
                try
                {
                    IQueryable<ConnectomeDataModel.Location> queryResults = from l in db.Locations.AsNoTracking() where (l.ParentID == structureID) select l;
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



        [PrincipalPermission(SecurityAction.Demand, Role = "Annotator")]
        public long[] UpdateStructures(AnnotationService.Types.Structure[] structures)
        {
            return Update(structures);
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Annotator")]
        public long[] Update(AnnotationService.Types.Structure[] structures)
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
                        AnnotationService.Types.Structure t = structures[iObj];

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

        [PrincipalPermission(SecurityAction.Demand, Role = "Annotator")]
        public CreateStructureRetval CreateStructure(AnnotationService.Types.Structure structure, AnnotationService.Types.Location location)
        {
            using (var db = GetOrCreateDatabaseContext())
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
                    CreateStructureRetval retval = new CreateStructureRetval(DBStruct.Create(false), DBLoc.Create());
                    return retval;
                }
                catch (System.Data.Entity.Validation.DbEntityValidationException e)
                {
                    foreach (var error in e.EntityValidationErrors)
                    {
                        Console.WriteLine(error);
                    }
                }
            }

            return null;
        }

        /*
        [PrincipalPermission(SecurityAction.Demand, Role = "Annotator")]
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
        [PrincipalPermission(SecurityAction.Demand, Role = "Reviewer")]
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
        [PrincipalPermission(SecurityAction.Demand, Role = "Reviewer")]
        public long Split(long KeepStructureID, long LocationIDInSplitStructure)
        {
            using (ConnectomeEntities db = GetOrCreateDatabaseContext())
            {
                long NewStructureID;
                int retval = db.SplitStructure(KeepStructureID, LocationIDInSplitStructure,  out NewStructureID);
                return NewStructureID;
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
        [PrincipalPermission(SecurityAction.Demand, Role = "Reviewer")]
        public long SplitAtLocationLink(long LocationIDOfKeepStructure, long LocationIDOfSplitStructure)
        {
            using (ConnectomeEntities db = GetOrCreateDatabaseContext())
            {
                long NewStructureID;
                int retval = db.SplitStructureAtLocationLink(LocationIDOfKeepStructure, LocationIDOfSplitStructure, out NewStructureID);
                return NewStructureID;
            }
        }
        
        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public AnnotationService.Types.Structure[] GetStructureChangeLog(long? structure_id, DateTime? begin_time, DateTime? end_time)
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
            return new AnnotationService.Types.Structure[0];
        }



        #endregion

        #region IAnnotateLocations Members

        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public AnnotationService.Types.Location GetLocationByID(long ID)
        {
            try
            {
                using (ConnectomeEntities db = GetOrCreateReadOnlyContext())
                {
                    ConnectomeDataModel.Location obj = db.Locations.Find(ID);
                    if (obj == null)
                        return null;
                    AnnotationService.Types.Location retLoc = obj.Create();
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

        /// <summary>
        /// Used with tasks, expects the input to be a chunk size small enough the generated SQL query won't exceed size limit
        /// </summary>
        /// <param name="db"></param>
        /// <param name="IDs"></param>
        /// <param name="IncludeLinks"></param>
        /// <returns></returns>
        private List<AnnotationService.Types.Location> _GetReadOnlyLocationsByIDChunked(long[] IDs, bool IncludeLinks)
        {
            //I do this hoping that it will allow SQL to not check the entire table for each chunk
            long minIDValue = IDs[0];
            long maxIDValue = IDs[IDs.Length - 1];
            List<AnnotationService.Types.Location> ListLocations = new List<AnnotationService.Types.Location>(IDs.Length);

            using (var db = GetOrCreateReadOnlyContext())
            { 

                try
                {
                    IQueryable<ConnectomeDataModel.Location> locObjs;
                    if (IncludeLinks)
                    {
                        locObjs = from s in db.Locations.Include("LocationLinksA").Include("LocationLinksB").AsNoTracking()
                                  where s.ID >= minIDValue &&
                                          s.ID <= maxIDValue &&
                                          IDs.Contains(s.ID)
                                  select s;
                    }
                    else
                    {
                        locObjs = from s in db.Locations.AsNoTracking()
                                  where s.ID >= minIDValue &&
                                          s.ID <= maxIDValue &&
                                          IDs.Contains(s.ID)
                                  select s;
                    }


                    if (locObjs == null)
                        return null;

                    ListLocations.AddRange(locObjs.Select(l => l.Create(IncludeLinks)));
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

            return ListLocations;
        }

            /// <summary>
            /// Fetch database objects for the IDs in bulk
            /// </summary>
            /// <param name="db"></param>
            /// <param name="IDs"></param>
            /// <returns></returns>
            private List<ConnectomeDataModel.Location> _GetLocationsByID(ConnectomeEntities db, long[] IDs, bool IncludeLinks)
        {
            List<long> ListIDs = new List<long>(IDs);
            ListIDs.Sort();  //Sort the list to slightly optimize the query

            const int QueryChunkSize = 2000;
            List<ConnectomeDataModel.Location> ListLocations = new List<ConnectomeDataModel.Location>(IDs.Length);

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
                    IQueryable<ConnectomeDataModel.Location> locObjs;
                    if (IncludeLinks)
                    {
                        locObjs = from s in db.Locations.Include("LocationLinksA").Include("LocationLinksB")
                                                                           where s.ID >= minIDValue &&
                                                                                   s.ID <= maxIDValue &&
                                                                                   ShorterListIDs.Contains(s.ID)
                                                                           select s;
                    }
                    else
                    {
                        locObjs = from s in db.Locations
                                  where s.ID >= minIDValue &&
                                          s.ID <= maxIDValue &&
                                          ShorterListIDs.Contains(s.ID)
                                  select s;
                    }


                    if (locObjs == null)
                        return null;

                    ListLocations.AddRange(locObjs); 
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

            return ListLocations;
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public AnnotationService.Types.Location[] GetLocationsByID(long[] IDs)
        {

            List<AnnotationService.Types.Location> listObjs;

            //LINQ creates a SQL query with parameters when using contains, and there is a 2100 parameter limit.  So we cut the query into smaller chunks and run 
            //multiple queries.  Since we are stuck doing this I run the query in parallel
            uint QueryChunkSize = 2000;

            var chunks = IDs.SortAndChunk(QueryChunkSize);

            if (chunks.Count > 1)
                Trace.WriteLine(string.Format("Dividing GetLocationsByID for {0} keys in {1} chunks", IDs.Length, chunks.Count));

            //We won't spawn any tasks if we only have one chunk.
            Task<List<AnnotationService.Types.Location>>[] tasks = new Task<List<AnnotationService.Types.Location>>[chunks.Count];
            
            for(int iChunk = 1; iChunk < chunks.Count; iChunk++)
            {
                long[] chunk = chunks[iChunk];
                tasks[iChunk] = Task.Run(() => _GetReadOnlyLocationsByIDChunked(chunk, true));
            }

            listObjs = _GetReadOnlyLocationsByIDChunked(chunks[0], true);

            for (int iChunk = 1; iChunk < chunks.Count; iChunk++)
            {
                listObjs.AddRange(tasks[iChunk].Result);
            }

            return listObjs.ToArray();
            /*

            List<ConnectomeDataModel.Location> listObjs;

            using (var db = GetOrCreateReadOnlyContext())
            {
                listObjs = _GetLocationsByID(db, IDs, true);
            }

            return listObjs.Select(obj => obj.Create(true)).ToArray();
            */
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public AnnotationService.Types.Location GetLastModifiedLocation()
        {
            using (var db = GetOrCreateReadOnlyContext())
            {
                try
                {
                    string callingUser = ServiceModelUtil.GetUserForCall().Trim();
                    var LocationsByUser = db.SelectLastModifiedLocationByUsers(mergeOption: System.Data.Entity.Core.Objects.MergeOption.NoTracking);
                    ConnectomeDataModel.Location lastLocation = (from l in LocationsByUser where l.Username.Trim() == callingUser select l).FirstOrDefault<ConnectomeDataModel.Location>();
                    return lastLocation.Create();
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public AnnotationService.Types.Location[] GetLocationsForSection(long section, out long QueryExecutedTime)
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

                    var Locations = dbLocs.Select(l => l.Create(true));

                    elapsed = new TimeSpan(DateTime.UtcNow.Ticks - start.Ticks);
                    Debug.WriteLine(section.ToString() + ": Convert to Objects: " + elapsed.TotalMilliseconds);

                    //Dictionary<long, Location> dictLocations = Locations.ToDictionary(l => l.ID);
                    //AnnotationService.Types.Location.PopulateLinks(dictLocations, dbLocLinks.ToList());

                    //AnnotationService.Types.Location[] retList = dictLocations.Values.ToArray();
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

            return new AnnotationService.Types.Location[0];

        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public AnnotationService.Types.Location[] GetLocationsForSectionMosaicRegion(long section, BoundingRectangle bbox, double MinRadius, out long QueryExecutedTime)
        {
            QueryExecutedTime = DateTime.Now.ToUniversalTime().Ticks;

            using (var db = GetOrCreateReadOnlyContext())
            {
                DateTime start = DateTime.Now;


                try
                {
                    IList<ConnectomeDataModel.Location> locations = db.ReadSectionLocationsAndLinksInMosaicRegion(section, bbox.ToGeometry(), MinRadius, new DateTime?());

                    Debug.WriteLine(section.ToString() + ": Query: " + new TimeSpan(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds);

                    AnnotationService.Types.Location[] retList = locations.Select(l => l.Create(true)).ToArray();

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

            return new AnnotationService.Types.Location[0];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public AnnotationService.Types.Location[] GetLocationsForSectionVolumeRegion(long section, BoundingRectangle bbox, double MinRadius, out long QueryExecutedTime)
        {
            QueryExecutedTime = DateTime.Now.ToUniversalTime().Ticks;

            using (var db = GetOrCreateReadOnlyContext())
            {
                DateTime start = DateTime.Now;


                try
                {
                    IList<ConnectomeDataModel.Location> locations = db.ReadSectionLocationsAndLinksInVolumeRegion(section, bbox.ToGeometry(), MinRadius, new DateTime?());

                    Debug.WriteLine(section.ToString() + ": Query: " + new TimeSpan(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds);

                    AnnotationService.Types.Location[] retList = locations.Select(l => l.Create(true)).ToArray();

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

            return new AnnotationService.Types.Location[0];
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
        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public AnnotationService.Types.Location[] GetLocationChangesInMosaicRegion(long section, BoundingRectangle bbox, double MinRadius, long? ModifiedAfterThisUtcTime, out long QueryExecutedTime, out long[] DeletedIDs)
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

                AnnotationService.Types.Location[] retList = new AnnotationService.Types.Location[0];

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

                    var Locations = dbLocs.Select(l => l.Create(true));

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

        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public AnnotationSet GetAnnotationsInMosaicRegion(long section, BoundingRectangle bbox, double MinRadius, long? ModifiedAfterThisUtcTime, out long QueryExecutedTime, out long[] DeletedIDs)
        {
            if (bbox.Width == 0 || bbox.Height == 0)
            {
                throw new ArgumentException("Bounding box must have non-zero dimensions");
            }

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

                    Task<AnnotationService.Types.Structure[]> structConvTask = Task<AnnotationService.Types.Structure[]>.Run(() => { return dbAnnotations.Structures.Values.Select(s => s.Create(false)).ToArray(); });
                    Task<AnnotationService.Types.Location[]> locConvTask = Task<AnnotationService.Types.Location[]>.Run(() => { return dbAnnotations.Locations.Values.Select(l => l.Create(true)).ToArray(); });

                    Task.WaitAll(structConvTask, locConvTask);

                    AnnotationService.Types.Structure[] structs = structConvTask.Result;
                    AnnotationService.Types.Location[] locs = locConvTask.Result;

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
        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public AnnotationService.Types.Location[] GetLocationChangesInVolumeRegion(long section, BoundingRectangle bbox, double MinRadius, long ModifiedAfterThisUtcTime, out long QueryExecutedTime, out long[] DeletedIDs)
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

                AnnotationService.Types.Location[] retList = new AnnotationService.Types.Location[0];

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

                    var Locations = dbLocs.Select(l => l.Create(true));

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
        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public AnnotationService.Types.Location[] GetLocationChanges(long section, long ModifiedAfterThisUtcTime, out long QueryExecutedTime, out long[] DeletedIDs)
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

                AnnotationService.Types.Location[] retList = new AnnotationService.Types.Location[0];

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

                    var Locations = dbLocs.Select(l => l.Create(true));

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
        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
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
                    IQueryable<long> queryResults = from l in db.DeletedLocations.AsNoTracking()
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

        [PrincipalPermission(SecurityAction.Demand, Role = "Annotator")]
        public AnnotationService.Types.Location CreateLocation(AnnotationService.Types.Location new_location, long[] links)
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

                AnnotationService.Types.Location output_loc = db_obj.Create();
                output_loc.Links = links;
                return output_loc;

            }
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Annotator")]
        public long[] Update(AnnotationService.Types.Location[] locations)
        {
            Dictionary<ConnectomeDataModel.Location, int> mapNewTypeToIndex = new Dictionary<ConnectomeDataModel.Location, int>(locations.Length);

            //Stores the ID of each object manipulated for the return value
            long[] listID = new long[locations.Length];

            using (ConnectomeEntities db = GetOrCreateDatabaseContext())
            {
                //For performance pre-load all of the database objects.  A loop was very slow for batch updates
                List<ConnectomeDataModel.Location> dbLocations = _GetLocationsByID(db, locations.Where(l => l.DBAction == DBACTION.UPDATE).Select(l => l.ID).ToArray(), false);
                Dictionary<long, ConnectomeDataModel.Location> dictObjs = dbLocations.ToDictionary(obj => obj.ID);

                try
                {
                    
                    for (int iObj = 0; iObj < locations.Length; iObj++)
                    {
                        AnnotationService.Types.Location t = locations[iObj];
                        if (t == null)
                        {
                            Debug.WriteLine("Null passed to location update.");
                            continue;
                        }
                         
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
                                if (dictObjs.TryGetValue(t.ID, out updateRow))
                                { 
                                    t.Sync(updateRow);
                                    listID[iObj] = updateRow.ID;
                                }
                                else
                                { 
                                    Debug.WriteLine("Could not find location to update: " + t.ID.ToString()); 
                                }

                                break; 
                                /*
                                 * 
                                 * 
                                 * Remove the try/catch block for speed
                                try
                                {
                                    
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
                                */
                            case DBACTION.DELETE:
                                
                                ConnectomeDataModel.Location deleteRow;
                                /*
                                if (dictObjs.TryGetValue(t.ID, out deleteRow))
                                {
                                    //Remove any links that exist before calling delete
                                    db.LocationLinks.RemoveRange(deleteRow.LocationLinksA);
                                    db.LocationLinks.RemoveRange(deleteRow.LocationLinksB);
                                    t.Sync(deleteRow);
                                    deleteRow.ID = t.ID;
                                    listID[iObj] = deleteRow.ID;
                                    db.Locations.Remove(deleteRow);
                                }
                                */
                                try
                                {
                                    deleteRow = db.Locations.Find(t.ID);
                                    db.LocationLinks.RemoveRange(deleteRow.LocationLinksA);
                                    db.LocationLinks.RemoveRange(deleteRow.LocationLinksB);
                                    t.Sync(deleteRow);
                                    deleteRow.ID = t.ID;
                                    listID[iObj] = deleteRow.ID;
                                    db.Locations.Remove(deleteRow);
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
            if (username == null)
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

        [PrincipalPermission(SecurityAction.Demand, Role = "Annotator")]
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

        [PrincipalPermission(SecurityAction.Demand, Role = "Annotator")]
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

        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public AnnotationService.Types.LocationLink[] GetLocationLinksForSection(long section, long ModifiedAfterThisUtcTime, out long QueryExecutedTime, out AnnotationService.Types.LocationLink[] DeletedLinks)
        {
            using (ConnectomeEntities db = GetOrCreateReadOnlyContext())
            {
                //TODO: This needs a real assignment, but I haven't created the table yet
                DeletedLinks = new AnnotationService.Types.LocationLink[0];
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

                    //AnnotationService.Types.LocationLink[] retList = new AnnotationService.Types.LocationLink[locationLinks.Count];

                    //Debug.WriteLine(section.ToString() + ": To list: " + new TimeSpan(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds);

                    AnnotationService.Types.LocationLink[] retList = locationLinks.Select(link => link.Create()).ToArray();
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

            return new AnnotationService.Types.LocationLink[0];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public AnnotationService.Types.LocationLink[] GetLocationLinksForSectionInMosaicRegion(long section, BoundingRectangle bbox, double MinRadius, long ModifiedAfterThisUtcTime, out long QueryExecutedTime, out AnnotationService.Types.LocationLink[] DeletedLinks)
        {
            using (ConnectomeEntities db = GetOrCreateReadOnlyContext())
            {
                //TODO: This needs a real assignment, but I haven't created the table yet
                DeletedLinks = new AnnotationService.Types.LocationLink[0];
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

                    AnnotationService.Types.LocationLink[] retList = locationLinks.Select(link => link.Create()).ToArray();
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

            return new AnnotationService.Types.LocationLink[0];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public AnnotationService.Types.LocationLink[] GetLocationLinksForSectionInVolumeRegion(long section, BoundingRectangle bbox, double MinRadius, long ModifiedAfterThisUtcTime, out long QueryExecutedTime, out AnnotationService.Types.LocationLink[] DeletedLinks)
        {
            using (ConnectomeEntities db = GetOrCreateReadOnlyContext())
            {
                //TODO: This needs a real assignment, but I haven't created the table yet
                DeletedLinks = new AnnotationService.Types.LocationLink[0];
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

                    AnnotationService.Types.LocationLink[] retList = locationLinks.Select(link => link.Create()).ToArray();
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

            return new AnnotationService.Types.LocationLink[0];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public long[] GetLinkedLocations(long ID)
        {
            using (ConnectomeEntities db = GetOrCreateDatabaseContext())
            {
                var links = (from u in db.LocationLinks.AsNoTracking() where u.A == ID select u.B).Union(from u in db.LocationLinks.AsNoTracking() where u.B == ID select u.A);
                return links.ToArray();
            }
        }

        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public LocationHistory[] GetLocationChangeLog(long? structure_id, DateTime? begin_time, DateTime? end_time)
        {
            using (ConnectomeEntities db = GetOrCreateReadOnlyContext())
            {
                var result = db.SelectStructureLocationChangeLog(structure_id, begin_time, end_time);
                List<SelectStructureLocationChangeLog_Result> listChanges = result.ToList();

                return listChanges.Select(loc => loc.Create()).ToArray();
            }
        }


        #endregion
        

        #region ICircuit Members

        public SortedDictionary<long, AnnotationService.Types.StructureType> StructureTypesDictionary = new SortedDictionary<long, AnnotationService.Types.StructureType>();

        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
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
                        AnnotationService.Types.Structure structure = graph.NodeList[result.ParentID];

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


        public AnnotationService.Types.Structure[] webService_GetStructures(Graphx graph, long[] ids)
        {
            if (ids.Length == 0)
                return new AnnotationService.Types.Structure[0];

            // connect to the AnnotationService.Types.Structure webservice 
            AnnotationService.Types.Structure[] FoundStructures = GetStructuresByIDs(ids, true);

            List<long> ListMissingChildrenIDs = new List<long>();

            //Add the root structure to nodelist if it not already added
            foreach (AnnotationService.Types.Structure structure in FoundStructures)
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

            AnnotationService.Types.Structure[] MissingStructures = webService_GetStructures(graph, MissingRootStructureIds.ToArray());

            List<long> ListMissingChildrenIDs = new List<long>();

            foreach (AnnotationService.Types.Structure structure in MissingStructures)
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
            AnnotationService.Types.Structure[] ChildStructObjs = webService_GetStructures(graph, ListMissingChildrenIDs.ToArray());

            List<long> ListAbsentSiblings = new List<long>();

            //Find missing structures and populate the list
            foreach (AnnotationService.Types.Structure child in ChildStructObjs)
            {
                //Temp Hack to skip desmosomes
                if (child.Links == null)
                    continue;

                foreach (AnnotationService.Types.StructureLink link in child.Links)
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

            AnnotationService.Types.Structure[] SiblingStructures = webService_GetStructures(graph, ListAbsentSiblings.ToArray());

            //Find missing structures and populate the list
            foreach (AnnotationService.Types.Structure child in ChildStructObjs)
            {
                if (child.Links == null)
                    continue;

                foreach (AnnotationService.Types.StructureLink link in child.Links)
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
                    AnnotationService.Types.Structure SourceCell = graph.NodeList[link.SourceID];
                    AnnotationService.Types.Structure TargetCell = graph.NodeList[link.TargetID];

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
            foreach (AnnotationService.Types.Structure sibling in SiblingStructures)
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

        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public long[] getStructuresByTypeID(int typeID)
        {
            long[] structuresList;

            using (ConnectomeEntities db = GetOrCreateReadOnlyContext())
            {

                IQueryable<long> res = from a in db.Structures.AsNoTracking() where a.TypeID == typeID select a.ID;

                structuresList = res.ToArray();
            }

            return structuresList;
        }

        // num=1 structures
        // num=0 locations
        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
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

        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
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

        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
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

        [PrincipalPermission(SecurityAction.Demand, Role = "Reader")]
        public string[] getSynapses(int cellID)
        {
            AnnotationService.Types.Structure mainStructure = GetStructureByID(cellID, true);
            if (mainStructure.ChildIDs == null)
            {
                return new string[0];
            }

            AnnotationService.Types.Structure[] synapses = GetStructuresByIDs(mainStructure.ChildIDs, false);
            SortedDictionary<long, long> temp = new SortedDictionary<long, long>();

            temp[1] = synapses.Count();

            foreach (AnnotationService.Types.Structure child in synapses)
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