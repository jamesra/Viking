using AnnotationService.Interfaces;
using AnnotationService.Types;
using ConnectomeDataModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security;
using System.Security.Permissions;
using System.ServiceModel;
using System.ServiceModel.Activation;
using System.Threading.Tasks;
using System.Web;
using Location = AnnotationService.Types.Location;
using LocationLink = AnnotationService.Types.LocationLink;
using Structure = AnnotationService.Types.Structure;
using StructureLink = AnnotationService.Types.StructureLink;
using StructureType = AnnotationService.Types.StructureType;

namespace Annotation
{
    public static class Roles
    {
        public static string Read = nameof(Roles.Read);
        public static string Write = nameof(Roles.Write);
        /// <summary>
        /// A deprecated role that is no longer used, but kept for backwards compatibility.  Equivalent to Write role
        /// </summary>
        public static string Modify = nameof(Roles.Modify);
        public static string Annotate = nameof(Roles.Annotate);
        public static string Review = nameof(Roles.Review);
    }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    [AspNetCompatibilityRequirements(RequirementsMode = AspNetCompatibilityRequirementsMode.Required)]
    public class AnnotateService : IAnnotateStructureTypes,
        IAnnotatePermittedStructureLinks,
        IAnnotateStructures,
        IAnnotateLocations, ICircuit, ICredentials, IVolumeMeta
    {
        static bool _isSqlTypesLoaded = false;

        static readonly object lockObject = new();

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
                    SqlServerTypesLoader.Loader.LoadNativeAssemblies(System.Web.HttpContext.Current.Server.MapPath("~"));
                    _isSqlTypesLoaded = true;
                    return;
                }
                catch (NullReferenceException)
                {
                    SqlServerTypesLoader.Loader.LoadNativeAssemblies(AppDomain.CurrentDomain.BaseDirectory);
                    _isSqlTypesLoaded = true;
                    return;
                }
            }
        }

        static AnnotateService()
        {
            TryLoadSqlServerTypes();
            Settings.PrepareSerializers();
        }

        /// <summary>
        /// Configure database context with appropriate timeout settings
        /// </summary>
        /// <param name="db">The database context to configure</param>
        /// <param name="timeoutSeconds">Timeout in seconds (default: 300)</param>
        private static void ConfigureDatabaseTimeout(ConnectomeEntities db, int timeoutSeconds = 300) => db.Database.CommandTimeout = timeoutSeconds;

        public AnnotateService()
        {
        }

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        bool ICredentials.CanRead() => true;

        bool ICredentials.CanWrite()
        {
            DemandWritePermissions();
            return true;
        }

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Review))]
        bool ICredentials.CanAdmin() => true;

        string ICredentials.Roles()
        {
            if (false == ServiceSecurityContext.Current.PrimaryIdentity.IsAuthenticated)
                return "";

            var user = HttpContext.Current.User;

            string roles = "";
            if (user.IsInRole(nameof(Roles.Read)))
                roles += nameof(Roles.Read) + ' ';
            if (user.IsInRole(nameof(Roles.Write)))
                roles += nameof(Roles.Write) + ' ';
            else if (user.IsInRole(nameof(Roles.Modify)))
                roles += nameof(Roles.Write) + ' ';
            else if (user.IsInRole(nameof(Roles.Annotate)))
                roles += nameof(Roles.Annotate) + ' ';
            if (user.IsInRole(nameof(Roles.Review)))
                roles += nameof(Roles.Review) + ' ';

            return roles.Trim();
        }


        public static void ConfigureContextAsReadOnly(ConnectomeEntities db) => db.ConfigureAsReadOnly();

        public static void ConfigureContextAsReadOnlyWithLazyLoading(ConnectomeEntities db) =>
            //Note, disabling LazyLoading breaks loading of children and links unless they have been populated previously.
            db.ConfigureAsReadOnlyWithLazyLoading();

        static ConnectomeDataModel.ConnectomeEntities GetOrCreateDatabaseContext() => new ConnectomeEntities();/*
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

            if (_db is null)
            { 
                _db = new ConnectomeEntities();
            } 

            return _db;*/

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


        protected string ConnectomeEntities() => VikingWebAppSettings.AppSettings.GetDefaultConnectionString();

        #region IVolumeMeta Members

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public Scale GetScale()
        {
            using ConnectomeEntities db = GetOrCreateDatabaseContext();
            AxisUnits X = new(db.GetXYScale(), db.GetXYUnits());
            AxisUnits Y = new(X.Value, X.Units);
            AxisUnits Z = new(db.GetZScale(), db.GetZUnits());

            Scale scale = new(X, Y, Z);

            return scale;
        }

        #endregion

        #region IAnnotateStructureTypes Members

        public AnnotationService.Types.StructureType CreateStructureType(AnnotationService.Types.StructureType new_structureType)
        {
            DemandWritePermissions();
            using ConnectomeEntities db = GetOrCreateDatabaseContext();
            ConnectomeDataModel.StructureType db_obj = new();
            //Create the object to get the ID
            new_structureType.Sync(db_obj);
            db.StructureTypes.Add(db_obj);

            //db.Log = Console.Out;
            db.SaveChanges();
            Console.Out.Flush();

            AnnotationService.Types.StructureType output_obj = db_obj.Create();
            return output_obj;
        }

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public AnnotationService.Types.StructureType[] GetStructureTypes()
        {
            using ConnectomeEntities db = GetOrCreateReadOnlyContextWithLazyLoading();
            IQueryable<ConnectomeDataModel.StructureType> queryResults = from t in db.StructureTypes select t;
            return [.. queryResults.ToArray().Select(st => st.Create())];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public AnnotationService.Types.PermittedStructureLink[] GetPermittedStructureLinks()
        {
            using ConnectomeEntities db = GetOrCreateReadOnlyContext();
            IQueryable<ConnectomeDataModel.PermittedStructureLink> queryResults = from psl in db.PermittedStructureLink select psl;
            return [.. queryResults.ToArray().Select(psl => psl.Create())];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public AnnotationService.Types.StructureType GetStructureTypeByID(long ID)
        {
            using var db = GetOrCreateReadOnlyContextWithLazyLoading();
            try
            {
                ConnectomeDataModel.StructureType type = db.StructureTypes.Find(ID);
                if (type is null)
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

            return null;
        }

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public AnnotationService.Types.Structure[] GetStructuresForType(long TypeID) => GetStructuresOfType(TypeID);


        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public AnnotationService.Types.Structure[] GetStructuresOfType(long TypeID)
        {
            using ConnectomeEntities db = GetOrCreateReadOnlyContext();
            try
            {
                IQueryable<ConnectomeDataModel.Structure> structObjs = from s in db.Structures
                                                                       where s.TypeID == TypeID
                                                                       select s;

                if (structObjs is null)
                    return [];

                List<ConnectomeDataModel.Structure> structObjList = [.. structObjs];

                return [.. structObjs.ToList().Select(s => s.Create(false))];
            }
            catch (Exception)
            {
                return [];
            }
        }

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public AnnotationService.Types.StructureType[] GetStructureTypesByIDs(long[] IDs)
        {

            List<long> ListIDs = [.. IDs];

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

                    List<long> ShorterListIDs = [.. ShorterIDArray];

                    try
                    {
                        IQueryable<ConnectomeDataModel.StructureType> structTypeObjs = from s in db.StructureTypes
                                                                                       where s.ID >= minIDValue &&
                                                                                             s.ID <= maxIDValue &&
                                                                                             ShorterListIDs.Contains(s.ID)
                                                                                       select s;
                        if (structTypeObjs is null)
                            return null;

                        return [.. structTypeObjs.ToList().Select(stype => stype.Create())];
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

            return [];
        }

        public long[] UpdateStructureTypes(AnnotationService.Types.StructureType[] structTypes)
        {
            DemandWritePermissions();
            return Update(structTypes);
        }

        /// <summary>
        /// Raise a SecurityException if the caller is not in the review role
        /// </summary>
        protected void DemandAdminPermissions() => new PrincipalPermission(null, nameof(Roles.Review)).Demand();

        /// <summary>
        /// Raise a SecurityException if the caller is not in the review role
        /// </summary>
        protected void DemandUser(string username) => new PrincipalPermission(username, null).Demand();

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
        /// Supports the legacy "modify" role in addition to "write" role.
        /// </summary>
        protected void DemandWritePermissions()
        {
            try
            {
                new PrincipalPermission(null, nameof(Roles.Write)).Demand();
            }
            catch (SecurityException)
            {
                try
                {
                    new PrincipalPermission(null, nameof(Roles.Annotate)).Demand();
                }
                catch (SecurityException)
                {
                    try
                    {
                        new PrincipalPermission(null, nameof(Roles.Modify)).Demand();
                    }
                    catch (SecurityException)
                    {
                        new PrincipalPermission(null, nameof(Roles.Review)).Demand();
                    }

                }

            }
        }

        /// <summary>
        /// Submits passed structure types to the database
        /// </summary>
        /// <param name="structTypes"></param>
        /// <returns>Returns ID's of each object in the order they were passed. Used to recover ID's of inserted rows</returns>
        public long[] Update(AnnotationService.Types.StructureType[] structTypes)
        {
            DemandWritePermissions();
            Dictionary<ConnectomeDataModel.StructureType, int> mapNewTypeToIndex = new(structTypes.Length);
            //Stores the ID of each object manipulated for the return value
            long[] listID = new long[structTypes.Length];

            using (var db = GetOrCreateDatabaseContext())
            {
                // Batch-load structure types for UPDATE and DELETE to avoid N round-trips
                long[] updateAndDeleteIds = structTypes
                    .Where(t => t.DBAction == DBACTION.UPDATE || t.DBAction == DBACTION.DELETE)
                    .Select(t => t.ID)
                    .Distinct()
                    .ToArray();
                Dictionary<long, ConnectomeDataModel.StructureType> dictStructureTypes = new(updateAndDeleteIds.Length);
                if (updateAndDeleteIds.Length > 0)
                {
                    const int QueryChunkSize = 2000;
                    var chunks = updateAndDeleteIds.Length <= QueryChunkSize
                        ? new List<long[]> { updateAndDeleteIds }
                        : ((ICollection<long>)updateAndDeleteIds).SortAndChunk((uint)QueryChunkSize);
                    foreach (long[] chunk in chunks)
                    {
                        var batch = db.StructureTypes.Where(st => chunk.Contains(st.ID)).ToList();
                        foreach (var st in batch)
                            dictStructureTypes[st.ID] = st;
                    }
                }

                try
                {

                    for (int iObj = 0; iObj < structTypes.Length; iObj++)
                    {
                        AnnotationService.Types.StructureType t = structTypes[iObj];

                        switch (t.DBAction)
                        {
                            case DBACTION.INSERT:
                                ConnectomeDataModel.StructureType newType = new();
                                t.Sync(newType);
                                db.StructureTypes.Add(newType);
                                mapNewTypeToIndex.Add(newType, iObj);
                                break;
                            case DBACTION.UPDATE:
                                if (!dictStructureTypes.TryGetValue(t.ID, out ConnectomeDataModel.StructureType updateType))
                                {
                                    Debug.WriteLine("Could not find structuretype to update: " + t.ID.ToString());
                                    break;
                                }

                                t.Sync(updateType);
                                listID[iObj] = updateType.ID;
                                break;
                            case DBACTION.DELETE:

                                DemandAdminPermissions();

                                if (!dictStructureTypes.TryGetValue(t.ID, out ConnectomeDataModel.StructureType deleteType))
                                {
                                    Debug.WriteLine("Could not find structuretype to delete: " + t.ID.ToString());
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
                    throw;

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

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public string TestMethod() => "Test OK";

        #endregion

        #region IAnnotateStructures Members

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public AnnotationService.Types.Structure[] GetStructures()
        {
            using (ConnectomeEntities db = GetOrCreateReadOnlyContext())
            {
                try
                {
                    //IQueryable<ConnectomeDataModel.Structure> queryStructures = from s in db.ConnectomeDataModel.Structures select s;
                    List<ConnectomeDataModel.Structure> listStructs = [.. db.Structures.AsNoTracking()];

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

            return [];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public AnnotationService.Types.Structure[] GetStructuresForSection(long SectionNumber, long ModifiedAfterThisUtcTime, out long QueryExecutedTime, out long[] DeletedIDs)
        {
            DeletedIDs = [];

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

                    AnnotationService.Types.Structure[] retList = [.. db.ReadSectionStructuresAndLinks(SectionNumber, ModifiedAfter).Select(s => s.Create(false))];

                    return retList;
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.ToString());
                }
            }

            return [];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public AnnotationService.Types.Structure[] GetStructuresForSectionInMosaicRegion(long section, BoundingRectangle bbox, double MinRadius, long ModifiedAfterThisUtcTime, out long QueryExecutedTime, out long[] DeletedIDs)
        {
            DateTime start = DateTime.UtcNow;
            TimeSpan elapsed;

            DeletedIDs = [];

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

                    AnnotationService.Types.Structure[] retList = [.. db.ReadSectionStructuresAndLinksInMosaicRegion(section, bbox.ToGeometry(), MinRadius, ModifiedAfter).Select(s => s.Create(false))];

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

            return [];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public AnnotationService.Types.Structure[] GetStructuresForSectionInVolumeRegion(long section, BoundingRectangle bbox, double MinRadius, long ModifiedAfterThisUtcTime, out long QueryExecutedTime, out long[] DeletedIDs)
        {
            DateTime start = DateTime.UtcNow;
            TimeSpan elapsed;

            DeletedIDs = [];

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

                    AnnotationService.Types.Structure[] retList = [.. db.ReadSectionStructuresAndLinksInVolumeRegion(section, bbox.ToGeometry(), MinRadius, ModifiedAfter).Select(s => s.Create(false))];

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

            return [];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public AnnotationService.Types.Structure GetStructureByID(long ID, bool IncludeChildren)
        {
            using var db = GetOrCreateReadOnlyContextWithLazyLoading();
            try
            {
                ConnectomeDataModel.Structure structObj = db.Structures.Find(ID);
                if (structObj is null)
                    return null;

                AnnotationService.Types.Structure newStruct = structObj.Create(IncludeChildren);

                if (IncludeChildren)
                {
                    var childStructures = (from s in db.Structures.AsNoTracking()
                                           where s.ParentID == structObj.ID
                                           select s.ID);

                    newStruct.ChildIDs = [.. childStructures];
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
            List<AnnotationService.Types.Structure> ListStructures = new(IDs.Length);
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
                    db.AppendLinksToStructures(dictStructures, [.. structLinks]);

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
                                selected_structures[ParentStructure.Key].ChildIDs = [.. ParentStructure];
                            }
                        }
                    }

                    if (structObjs is null)
                        return [];

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


        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public AnnotationService.Types.Structure[] GetStructuresByIDs(long[] IDs, bool IncludeChildren)
        {
            return GetStructuresByIDsAsync(IDs, IncludeChildren).GetAwaiter().GetResult();
        }

        private async Task<AnnotationService.Types.Structure[]> GetStructuresByIDsAsync(long[] IDs, bool IncludeChildren)
        {
            List<AnnotationService.Types.Structure> ListStructures = new(IDs.Length);
            uint QueryChunkSize = 1024;
            var chunks = IDs.SortAndChunk(QueryChunkSize, CanSortIDsInPlace: true);

            if (chunks.Count > 1)
                Trace.WriteLine(string.Format("Dividing GetStructuresByIDs for {0} keys in {1} chunks", IDs.Length, chunks.Count));

            Task<List<AnnotationService.Types.Structure>>[] tasks = new Task<List<AnnotationService.Types.Structure>>[chunks.Count];

            for (int iChunk = 1; iChunk < chunks.Count; iChunk++)
            {
                long[] chunk = chunks[iChunk];
                tasks[iChunk] = Task.Run(() => GetStructureByIDsChunk(chunk, IncludeChildren));
            }

            ListStructures = GetStructureByIDsChunk(chunks[0], IncludeChildren);

            if (chunks.Count > 1)
            {
                List<List<AnnotationService.Types.Structure>> tailResults = [.. await Task.WhenAll(tasks.Skip(1).Where(t => t != null))];
                foreach (var list in tailResults)
                    ListStructures.AddRange(list);
            }

            return [.. ListStructures];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public void ApproximateStructureLocation(long ID)
        {
            using ConnectomeEntities db = GetOrCreateDatabaseContext();
            db.ApproximateStructureLocation(new int?((int)ID));
        }

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Review))]
        public AnnotationService.Types.PermittedStructureLink CreatePermittedStructureLink(AnnotationService.Types.PermittedStructureLink link)
        {
            ConnectomeDataModel.PermittedStructureLink newRow = new();
            link.Sync(newRow);
            using (ConnectomeEntities db = GetOrCreateDatabaseContext())
            {
                db.PermittedStructureLink.Add(newRow);
                db.SaveChanges();
            }

            AnnotationService.Types.PermittedStructureLink newLink = newRow.Create();
            return newLink;
        }

        private static void ApplyOnePermittedStructureLink(ConnectomeEntities db, AnnotationService.Types.PermittedStructureLink obj)
        {
            switch (obj.DBAction)
            {
                case DBACTION.INSERT:
                    var newRow = new ConnectomeDataModel.PermittedStructureLink();
                    obj.Sync(newRow);
                    db.PermittedStructureLink.Add(newRow);
                    break;
                case DBACTION.UPDATE:
                    ConnectomeDataModel.PermittedStructureLink updateRow;
                    try
                    {
                        updateRow = (from u in db.PermittedStructureLink
                                    where u.SourceTypeID == obj.SourceTypeID &&
                                          u.TargetTypeID == obj.TargetTypeID
                                    select u).Single();
                    }
                    catch (System.ArgumentNullException)
                    {
                        Debug.WriteLine("Could not find structuretype to update: " + obj.ToString());
                        return;
                    }
                    catch (System.InvalidOperationException)
                    {
                        Debug.WriteLine("Could not find structuretype to update: " + obj.ToString());
                        return;
                    }
                    obj.Sync(updateRow);
                    break;
                case DBACTION.DELETE:
                    ConnectomeDataModel.PermittedStructureLink deleteRow;
                    try
                    {
                        deleteRow = (from u in db.PermittedStructureLink
                                    where u.SourceTypeID == obj.SourceTypeID &&
                                          u.TargetTypeID == obj.TargetTypeID
                                    select u).Single();
                    }
                    catch (System.ArgumentNullException)
                    {
                        Debug.WriteLine("Could not find structuretype to delete: " + obj.ToString());
                        return;
                    }
                    catch (System.InvalidOperationException)
                    {
                        Debug.WriteLine("Could not find structuretype to update: " + obj.ToString());
                        return;
                    }
                    db.PermittedStructureLink.Remove(deleteRow);
                    break;
            }
        }

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Review))]
        public void UpdatePermittedStructureLinks(AnnotationService.Types.PermittedStructureLink[] links)
        {
            using ConnectomeEntities db = GetOrCreateDatabaseContext();
            try
            {
                for (int i = 0; i < links.Length; i++)
                    ApplyOnePermittedStructureLink(db, links[i]);
                db.SaveChanges();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
                throw;
            }
        }

        public AnnotationService.Types.StructureLink CreateStructureLink(AnnotationService.Types.StructureLink link)
        {
            DemandWritePermissions();
            ConnectomeDataModel.StructureLink newRow = new();
            link.Sync(newRow);
            using (ConnectomeEntities db = GetOrCreateDatabaseContext())
            {
                db.StructureLinks.Add(newRow);
                db.SaveChanges();
            }

            AnnotationService.Types.StructureLink newLink = newRow.Create();
            return newLink;
        }

        private static void ApplyOneStructureLink(ConnectomeEntities db, AnnotationService.Types.StructureLink obj)
        {
            switch (obj.DBAction)
            {
                case DBACTION.INSERT:
                    var newRow = new ConnectomeDataModel.StructureLink();
                    obj.Sync(newRow);
                    db.StructureLinks.Add(newRow);
                    break;
                case DBACTION.UPDATE:
                    ConnectomeDataModel.StructureLink updateRow;
                    try
                    {
                        updateRow = (from u in db.StructureLinks
                                    where u.SourceID == obj.SourceID &&
                                          u.TargetID == obj.TargetID
                                    select u).Single();
                    }
                    catch (System.ArgumentNullException)
                    {
                        Debug.WriteLine("Could not find structuretype to update: " + obj.ToString());
                        return;
                    }
                    catch (System.InvalidOperationException)
                    {
                        Debug.WriteLine("Could not find structuretype to update: " + obj.ToString());
                        return;
                    }
                    obj.Sync(updateRow);
                    break;
                case DBACTION.DELETE:
                    ConnectomeDataModel.StructureLink deleteRow;
                    try
                    {
                        deleteRow = (from u in db.StructureLinks
                                    where u.SourceID == obj.SourceID &&
                                          u.TargetID == obj.TargetID
                                    select u).Single();
                    }
                    catch (System.ArgumentNullException)
                    {
                        Debug.WriteLine("Could not find structuretype to delete: " + obj.ToString());
                        return;
                    }
                    catch (System.InvalidOperationException)
                    {
                        Debug.WriteLine("Could not find structuretype to update: " + obj.ToString());
                        return;
                    }
                    db.StructureLinks.Remove(deleteRow);
                    break;
            }
        }

        public void UpdateStructureLinks(AnnotationService.Types.StructureLink[] links)
        {
            DemandWritePermissions();
            using ConnectomeEntities db = GetOrCreateDatabaseContext();
            try
            {
                for (int i = 0; i < links.Length; i++)
                    ApplyOneStructureLink(db, links[i]);
                db.SaveChanges();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
                throw;
            }
        }

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public long[] GetUnfinishedLocations(long structureID)
        {
            using ConnectomeEntities db = GetOrCreateReadOnlyContext();
            return [.. (from id in db.SelectUnfinishedStructureBranches(structureID) select id.Value)];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public LocationPositionOnly[] GetUnfinishedLocationsWithPosition(long structureID)
        {
            using ConnectomeEntities db = GetOrCreateReadOnlyContext();
            return [.. db.SelectUnfinishedStructureBranchesWithPosition(structureID).ToList().Select(row => row.Create())];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public AnnotationService.Types.StructureLink[] GetLinkedStructures()
        {
            using (var db = GetOrCreateDatabaseContext())
            {
                try
                {
                    IQueryable<ConnectomeDataModel.StructureLink> queryResults = from l in db.StructureLinks.AsNoTracking() select l;
                    List<AnnotationService.Types.StructureLink> retList = new(queryResults.Count());
                    foreach (ConnectomeDataModel.StructureLink dbl in queryResults)
                    {
                        AnnotationService.Types.StructureLink link = dbl.Create();
                        retList.Add(link);
                    }
                    return [.. retList];
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
            return [];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public AnnotationService.Types.StructureLink[] GetLinkedStructuresByID(long ID)
        {
            using ConnectomeEntities db = GetOrCreateDatabaseContext();
            try
            {
                IQueryable<ConnectomeDataModel.StructureLink> queryResults = from l in db.StructureLinks.AsNoTracking() where (l.SourceID == ID || l.TargetID == ID) select l;
                List<AnnotationService.Types.StructureLink> retList = new(queryResults.Count());
                foreach (ConnectomeDataModel.StructureLink dbl in queryResults)
                {
                    AnnotationService.Types.StructureLink link = dbl.Create();
                    retList.Add(link);
                }
                return [.. retList];
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

            return [];
        }

        public long[] GetNetworkedStructures(long[] IDs, int numHops)
        {
            using var db = GetOrCreateReadOnlyContext();

            return [.. db.SelectNetworkStructureIDs(IDs, numHops)];
        }

        public AnnotationService.Types.Structure[] GetChildStructuresInNetwork(long[] IDs, int numHops)
        {
            using var db = GetOrCreateReadOnlyContext();
            var child_structs = db.SelectNetworkChildStructures(IDs, numHops);
            return [.. child_structs.ToList().Select(s => s.Create(false))];
        }

        public AnnotationService.Types.StructureLink[] GetStructureLinksInNetwork(long[] IDs, int numHops)
        {
            using var db = GetOrCreateReadOnlyContext();
            var structure_links = db.SelectNetworkStructureLinks(IDs, numHops);
            return [.. structure_links.ToList().Select(sl => sl.Create())];
        }


        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public AnnotationService.Types.Location[] GetLocationsForStructure(long structureID)
        {
            using (var db = GetOrCreateReadOnlyContext())
            {
                try
                {
                    IList<ConnectomeDataModel.Location> queryResults = db.ReadStructureLocationsAndLinks(structureID);
                    return [.. queryResults.Select(loc => loc.Create(true))];
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

            return [];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public long NumberOfLocationsForStructure(long structureID)
        {
            using ConnectomeEntities db = GetOrCreateReadOnlyContext();
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

            return 0;
        }



        public long[] UpdateStructures(AnnotationService.Types.Structure[] structures)
        {
            DemandWritePermissions();
            return Update(structures);
        }

        public long[] Update(AnnotationService.Types.Structure[] structures)
        {
            DemandWritePermissions();
            using ConnectomeEntities db = GetOrCreateDatabaseContext();
            Dictionary<ConnectomeDataModel.Structure, int> mapNewObjToIndex = new(structures.Length);

            // Batch-load structures for UPDATE and DELETE to avoid N round-trips (SQL param limit 2100)
            long[] updateAndDeleteIds = structures
                .Where(t => t.DBAction == DBACTION.UPDATE || t.DBAction == DBACTION.DELETE)
                .Select(t => t.ID)
                .Distinct()
                .ToArray();
            Dictionary<long, ConnectomeDataModel.Structure> dictStructures = new(updateAndDeleteIds.Length);
            if (updateAndDeleteIds.Length > 0)
            {
                const int QueryChunkSize = 2000;
                var chunks = updateAndDeleteIds.Length <= QueryChunkSize
                    ? new List<long[]> { updateAndDeleteIds }
                    : ((ICollection<long>)updateAndDeleteIds).SortAndChunk((uint)QueryChunkSize);
                foreach (long[] chunk in chunks)
                {
                    var batch = db.Structures
                        .Include("SourceOfLinks")
                        .Include("TargetOfLinks")
                        .Where(s => chunk.Contains(s.ID))
                        .ToList();
                    foreach (var s in batch)
                        dictStructures[s.ID] = s;
                }
            }

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
                            ConnectomeDataModel.Structure newRow = new();
                            t.Sync(newRow);
                            db.Structures.Add(newRow);
                            mapNewObjToIndex.Add(newRow, iObj);
                            break;
                        case DBACTION.UPDATE:

                            if (!dictStructures.TryGetValue(t.ID, out ConnectomeDataModel.Structure updateRow))
                            {
                                Debug.WriteLine("Could not find structure to update: " + t.ID.ToString());
                                break;
                            }

                            t.Sync(updateRow);
                            listID[iObj] = updateRow.ID;
                            break;
                        case DBACTION.DELETE:

                            if (!dictStructures.TryGetValue(t.ID, out ConnectomeDataModel.Structure deleteRow))
                            {
                                Debug.WriteLine("Could not find structure to delete: " + t.ID.ToString());
                                break;
                            }

                            t.Sync(deleteRow);
                            deleteRow.ID = t.ID;
                            listID[iObj] = deleteRow.ID;

                            //Remove any links that exist before calling delete
                            db.StructureLinks.RemoveRange([.. deleteRow.SourceOfLinks]);
                            db.StructureLinks.RemoveRange([.. deleteRow.TargetOfLinks]);

                            db.Structures.Remove(deleteRow);
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
                throw;

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

        public CreateStructureRetval CreateStructure(AnnotationService.Types.Structure structure, AnnotationService.Types.Location location)
        {
            DemandWritePermissions();
            using var db = GetOrCreateDatabaseContext();

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
                CreateStructureRetval retval = new(DBStruct.Create(false), DBLoc.Create());
                return retval;
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException e)
            {
                foreach (var error in e.EntityValidationErrors)
                {
                    Console.WriteLine(error);
                }
            }

            return null;
        }

        /*
        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Write))]
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
        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Review))]
        public long Merge(long KeepID, long MergeID)
        {
            using ConnectomeEntities db = GetOrCreateDatabaseContext();
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
        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Review))]
        public long Split(long KeepStructureID, long LocationIDInSplitStructure)
        {
            using ConnectomeEntities db = GetOrCreateDatabaseContext();
            int retval = db.SplitStructure(KeepStructureID, LocationIDInSplitStructure, out long NewStructureID);
            return NewStructureID;
        }

        /// <summary>
        /// Split the specified structure into two new structures at the specified link
        /// return an exception if the structure has a cycle in the graph.
        /// Child objects are assigned to the nearest location on the same section
        /// </summary>
        /// <param name="StructureA">Structure to split</param>
        /// <param name="locLink">Location Link to split structure at</param>
        /// <returns>ID of new structure</returns>
        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Review))]
        public long SplitAtLocationLink(long LocationIDOfKeepStructure, long LocationIDOfSplitStructure)
        {
            using ConnectomeEntities db = GetOrCreateDatabaseContext();
            int retval = db.SplitStructureAtLocationLink(LocationIDOfKeepStructure, LocationIDOfSplitStructure, out long NewStructureID);
            return NewStructureID;
        }

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public AnnotationService.Types.Structure[] GetStructureChangeLog(long? structure_id, DateTime? begin_time, DateTime? end_time) =>
            []; // Stub: was implemented via SelectStructureChangeLog; re-enable when needed.



        #endregion

        #region IAnnotateLocations Members

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public AnnotationService.Types.Location GetLocationByID(long ID)
        {
            try
            {
                using ConnectomeEntities db = GetOrCreateReadOnlyContext();
                ConnectomeDataModel.Location obj = db.Locations.Find(ID);
                if (obj is null)
                    return null;
                AnnotationService.Types.Location retLoc = obj.Create();
                return retLoc;
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
            List<AnnotationService.Types.Location> ListLocations = new(IDs.Length);

            using (var db = GetOrCreateReadOnlyContext())
            {

                try
                {
                    IQueryable<ConnectomeDataModel.Location> locObjs;
                    if (IncludeLinks)
                    {
                        locObjs = (from s in db.Locations.Include("LocationLinksA").Include("LocationLinksB").AsNoTracking()
                                   where s.ID >= minIDValue &&
                                           s.ID <= maxIDValue &&
                                           IDs.Contains(s.ID)
                                   select s);
                    }
                    else
                    {
                        locObjs = from s in db.Locations.AsNoTracking()
                                  where s.ID >= minIDValue &&
                                          s.ID <= maxIDValue &&
                                          IDs.Contains(s.ID)
                                  select s;
                    }


                    if (locObjs is null)
                        return null;

                    ListLocations.AddRange(locObjs.ToList().Select(l => l.Create(IncludeLinks)));
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
        private static List<ConnectomeDataModel.Location> _GetLocationsByID(ConnectomeEntities db, long[] IDs, bool IncludeLinks)
        {
            List<long> ListIDs = [.. IDs];
            ListIDs.Sort();  //Sort the list to slightly optimize the query

            const int QueryChunkSize = 2000;
            List<ConnectomeDataModel.Location> ListLocations = new(IDs.Length);

            while (ListIDs.Count > 0)
            {
                int NumIDs = ListIDs.Count < QueryChunkSize ? ListIDs.Count : QueryChunkSize;

                long[] ShorterIDArray = new long[NumIDs];

                ListIDs.CopyTo(0, ShorterIDArray, 0, NumIDs);
                ListIDs.RemoveRange(0, NumIDs);

                //I do this hoping that it will allow SQL to not check the entire table for each chunk
                long minIDValue = ShorterIDArray[0];
                long maxIDValue = ShorterIDArray[ShorterIDArray.Length - 1];

                List<long> ShorterListIDs = [.. ShorterIDArray];

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


                    if (locObjs is null)
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

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public AnnotationService.Types.Location[] GetLocationsByID(long[] IDs)
        {
            return GetLocationsByIDAsync(IDs).GetAwaiter().GetResult();
        }

        private async Task<AnnotationService.Types.Location[]> GetLocationsByIDAsync(long[] IDs)
        {
            List<AnnotationService.Types.Location> listObjs;
            uint QueryChunkSize = 2000;
            var chunks = IDs.SortAndChunk(QueryChunkSize, CanSortIDsInPlace: true);

            if (chunks.Count > 1)
                Trace.WriteLine(string.Format("Dividing GetLocationsByID for {0} keys in {1} chunks", IDs.Length, chunks.Count));

            Task<List<AnnotationService.Types.Location>>[] tasks = new Task<List<AnnotationService.Types.Location>>[chunks.Count];

            for (int iChunk = 1; iChunk < chunks.Count; iChunk++)
            {
                long[] chunk = chunks[iChunk];
                tasks[iChunk] = Task.Run(() => _GetReadOnlyLocationsByIDChunked(chunk, true));
            }

            listObjs = _GetReadOnlyLocationsByIDChunked(chunks[0], true);

            if (chunks.Count > 1)
            {
                List<List<AnnotationService.Types.Location>> tailResults = [.. await Task.WhenAll(tasks.Skip(1).Where(t => t != null))];
                foreach (var list in tailResults)
                    listObjs.AddRange(list);
            }

            return [.. listObjs];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public AnnotationService.Types.Location GetLastModifiedLocation()
        {
            using var db = GetOrCreateReadOnlyContext();
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

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public AnnotationService.Types.Location[] GetLocationsForSection(long section, out long QueryExecutedTime)
        {
            QueryExecutedTime = DateTime.Now.ToUniversalTime().Ticks;

            using (var db = GetOrCreateReadOnlyContext())
            {
                DateTime start = DateTime.UtcNow;

                db.Database.CommandTimeout = 90; // Increased from 30 to 300 seconds (5 minutes)

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
                    return [.. Locations];
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

            return [];

        }

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
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

                    AnnotationService.Types.Location[] retList = [.. locations.Select(l => l.Create(true))];

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

            return [];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
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

                    AnnotationService.Types.Location[] retList = [.. locations.Select(l => l.Create(true))];

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

            return [];
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
        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public AnnotationService.Types.Location[] GetLocationChangesInMosaicRegion(long section, BoundingRectangle bbox, double MinRadius, long? ModifiedAfterThisUtcTime, out long QueryExecutedTime, out long[] DeletedIDs)
        {
            using var db = GetOrCreateReadOnlyContext();
            DateTime start = DateTime.UtcNow;
            TimeSpan elapsed;

            DateTime? ModifiedAfterThisTime = new DateTime?();

            if (ModifiedAfterThisUtcTime.HasValue)
                ModifiedAfterThisTime = new DateTime?(new DateTime(ModifiedAfterThisUtcTime.Value, DateTimeKind.Utc));

            ModifiedAfterThisTime = ConnectomeDataModel.ConnectomeEntities.ValidateDate(ModifiedAfterThisTime);

            DeletedIDs = [];

            AnnotationService.Types.Location[] retList = [];

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
                retList = [.. Locations];
            }
            //TODO: Optimize this function to only return locations from the section we specify.  It currently returns all sections
            DeletedIDs = GetDeletedLocations(ModifiedAfterThisTime);

            return retList;
        }

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public AnnotationSet GetAnnotationsInMosaicRegion(long section, BoundingRectangle bbox, double MinRadius, long? ModifiedAfterThisUtcTime, out long QueryExecutedTime, out long[] DeletedIDs)
        {
            if (bbox.Width == 0 || bbox.Height == 0)
            {
                throw new ArgumentException("Bounding box must have non-zero dimensions");
            }

            using var db = GetOrCreateReadOnlyContext();
            DateTime start = DateTime.UtcNow;
            TimeSpan elapsed;

            DateTime? ModifiedAfterThisTime = new DateTime?();

            if (ModifiedAfterThisUtcTime.HasValue)
                ModifiedAfterThisTime = new DateTime?(new DateTime(ModifiedAfterThisUtcTime.Value, DateTimeKind.Utc));

            ModifiedAfterThisTime = ConnectomeDataModel.ConnectomeEntities.ValidateDate(ModifiedAfterThisTime);

            DeletedIDs = [];

            AnnotationSet results = null;

            QueryExecutedTime = start.Ticks;
            //try
            {
                AnnotationCollection dbAnnotations = db.ReadSectionAnnotationsInMosaicRegion(section, bbox.ToGeometry(), MinRadius, ModifiedAfterThisTime);
                elapsed = new TimeSpan(DateTime.UtcNow.Ticks - start.Ticks);
                Debug.WriteLine(section.ToString() + ": Query Section Annotations: " + elapsed.TotalMilliseconds);

                Task<long[]> deletedTask = Task.Run(() => GetDeletedLocations(ModifiedAfterThisTime));
                Task<AnnotationService.Types.Structure[]> structConvTask = Task.Run(() => dbAnnotations.Structures.Values.Select(s => s.Create(false)).ToArray());
                Task<AnnotationService.Types.Location[]> locConvTask = Task.Run(() => dbAnnotations.Locations.Values.Select(l => l.Create(true)).ToArray());

                Task.WhenAll(deletedTask, structConvTask, locConvTask).GetAwaiter().GetResult();

                DeletedIDs = deletedTask.Result;
                AnnotationService.Types.Structure[] structs = structConvTask.Result;
                AnnotationService.Types.Location[] locs = locConvTask.Result;

                results = new AnnotationSet(structs, locs);

                elapsed = new TimeSpan(DateTime.UtcNow.Ticks - start.Ticks);
                Debug.WriteLine(section.ToString() + ": Convert to Objects: " + elapsed.TotalMilliseconds);

                //elapsed = new TimeSpan(DateTime.UtcNow.Ticks - start.Ticks);
                //Debug.WriteLine(section.ToString() + ": Add Links: " + elapsed.TotalMilliseconds);

            }

            return results;
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
        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public AnnotationService.Types.Location[] GetLocationChangesInVolumeRegion(long section, BoundingRectangle bbox, double MinRadius, long ModifiedAfterThisUtcTime, out long QueryExecutedTime, out long[] DeletedIDs)
        {
            using var db = GetOrCreateReadOnlyContext();
            DateTime start = DateTime.UtcNow;
            TimeSpan elapsed;

            DateTime? ModifiedAfterThisTime = new DateTime?();
            if (ModifiedAfterThisUtcTime > 0)
                ModifiedAfterThisTime = new DateTime?(new DateTime(ModifiedAfterThisUtcTime, DateTimeKind.Utc));
            ModifiedAfterThisTime = ConnectomeDataModel.ConnectomeEntities.ValidateDate(ModifiedAfterThisTime);

            DeletedIDs = [];

            AnnotationService.Types.Location[] retList = [];

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
                retList = [.. Locations];
            }
            //TODO: Optimize this function to only return locations from the section we specify.  It currently returns all sections
            DeletedIDs = GetDeletedLocations(ModifiedAfterThisTime);

            return retList;
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
        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public AnnotationService.Types.Location[] GetLocationChanges(long section, long ModifiedAfterThisUtcTime, out long QueryExecutedTime, out long[] DeletedIDs)
        {
            using var db = GetOrCreateReadOnlyContext();
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

            DeletedIDs = [];

            AnnotationService.Types.Location[] retList = [];

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

                retList = [.. Locations];
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


        /// <summary>
        /// TODO: Optimize this function to use the new change tracking tables
        /// </summary>
        /// <param name="DeletedAfterThisTime"></param>
        /// <returns>An array, may be zero length if no locations were deleted</returns>
        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public long[] GetDeletedLocations(DateTime? DeletedAfterThisTime)
        {
            //Try to find if any rows were deleted from the passed list of IDs
            DateTime start = DateTime.UtcNow;

            if (!DeletedAfterThisTime.HasValue)
            {
                return [];
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

                    TimeSpan elapsed = new(DateTime.UtcNow.Ticks - start.Ticks);
                    Debug.WriteLine("\tDeleted Query: " + elapsed.TotalMilliseconds);

                    //Figure out which IDs are not in the returned list
                    return [.. queryResults];
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

            return [];
        }

        public AnnotationService.Types.Location CreateLocation(AnnotationService.Types.Location new_location, long[] links)
        {
            DemandWritePermissions();
            using var db = GetOrCreateDatabaseContext();

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
                    List<ConnectomeDataModel.LocationLink> listLinks = new(links.Length);
                    foreach (long linked_locationID in links)
                    {
                        ConnectomeDataModel.LocationLink created_link = _CreateLocationLink(db, db_obj.ID, linked_locationID, username);
                        listLinks.Add(created_link);
                    }

                    db.LocationLinks.AddRange(listLinks);
                    db.SaveChanges();
                    transaction.Commit();
                }
                catch (Exception)
                {
                    //transaction.Rollback();
                    throw;
                }
            }

            AnnotationService.Types.Location output_loc = db_obj.Create();
            output_loc.Links = links;
            return output_loc;
        }

        public long[] Update(AnnotationService.Types.Location[] locations)
        {
            if (locations is null)
                throw new ArgumentNullException(nameof(locations));

            DemandWritePermissions();
            Dictionary<ConnectomeDataModel.Location, int> mapNewTypeToIndex = new(locations.Length);
            long[] listID = new long[locations.Length];

            using (ConnectomeEntities db = GetOrCreateDatabaseContext())
            {
                //For performance pre-load all of the database objects.  A loop was very slow for batch updates
                List<ConnectomeDataModel.Location> dbLocations = _GetLocationsByID(db, [.. locations.Where(l => l.DBAction == DBACTION.UPDATE).Select(l => l.ID)], false);
                Dictionary<long, ConnectomeDataModel.Location> dictObjs = dbLocations.ToDictionary(obj => obj.ID);

                // Batch-load locations to DELETE with their links to avoid N round-trips
                long[] deleteIds = locations.Where(l => l.DBAction == DBACTION.DELETE).Select(l => l.ID).ToArray();
                Dictionary<long, ConnectomeDataModel.Location> dictDeleteObjs = new(deleteIds.Length);
                if (deleteIds.Length > 0)
                {
                    List<ConnectomeDataModel.Location> dbLocationsToDelete = _GetLocationsByID(db, deleteIds, true);
                    foreach (var loc in dbLocationsToDelete)
                        dictDeleteObjs[loc.ID] = loc;
                }

                try
                {

                    for (int iObj = 0; iObj < locations.Length; iObj++)
                    {
                        AnnotationService.Types.Location t = locations[iObj];
                        if (t is null)
                        {
                            Debug.WriteLine("Null passed to location update.");
                            continue;
                        }

                        switch (t.DBAction)
                        {
                            case DBACTION.INSERT:

                                ConnectomeDataModel.Location newObj = new();
                                t.Sync(newObj);
                                db.Locations.Add(newObj);
                                mapNewTypeToIndex.Add(newObj, iObj);
                                break;
                            case DBACTION.UPDATE:
                                if (dictObjs.TryGetValue(t.ID, out ConnectomeDataModel.Location updateRow))
                                {
                                    t.Sync(updateRow);
                                    listID[iObj] = updateRow.ID;
                                }
                                else
                                {
                                    Debug.WriteLine("Could not find location to update: " + t.ID.ToString());
                                }

                                break;
                            case DBACTION.DELETE:

                                if (dictDeleteObjs.TryGetValue(t.ID, out ConnectomeDataModel.Location deleteRow))
                                {
                                    db.LocationLinks.RemoveRange(deleteRow.LocationLinksA);
                                    db.LocationLinks.RemoveRange(deleteRow.LocationLinksB);
                                    t.Sync(deleteRow);
                                    deleteRow.ID = t.ID;
                                    listID[iObj] = deleteRow.ID;
                                    db.Locations.Remove(deleteRow);
                                }
                                else
                                {
                                    Debug.WriteLine("Could not find location to delete: " + t.ID.ToString());
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
                        Debug.WriteLine($"Validation error: {error}");
                    }
                    throw; // Re-throw to indicate failure
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Update failed: {ex.Message}");
                    throw;
                }
            }

            // Recover IDs for new objects
            foreach (ConnectomeDataModel.Location newObj in mapNewTypeToIndex.Keys)
            {
                int iIndex = mapNewTypeToIndex[newObj];
                listID[iIndex] = newObj.ID;
            }

            return listID;
        }

        private ConnectomeDataModel.LocationLink _CreateLocationLink(ConnectomeEntities db, long SourceID, long TargetID, string username)
        {
            username ??= ServiceModelUtil.GetUserForCall();

            ConnectomeDataModel.LocationLink newLink = db.LocationLinks.Create();
            ConnectomeDataModel.Location Source;
            ConnectomeDataModel.Location Target;

            try
            {
                Source = db.Locations.Find(SourceID);
                Target = db.Locations.Find(TargetID);
            }
            catch (InvalidOperationException e)
            {
                throw new ArgumentException("CreateLocationLink: The specified source or target does not exist", e);
            }

            if (Source is null || Target is null)
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

        public void CreateLocationLink(long SourceID, long TargetID)
        {
            DemandWritePermissions();
            using ConnectomeEntities db = new();
            ConnectomeDataModel.LocationLink newLink = _CreateLocationLink(db, SourceID, TargetID, null);
            db.LocationLinks.Add(newLink);
            db.SaveChanges();

            return;
        }

        public void DeleteLocationLink(long SourceID, long TargetID)
        {
            DemandWritePermissions();
            using ConnectomeEntities db = GetOrCreateDatabaseContext();
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

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public AnnotationService.Types.LocationLink[] GetLocationLinksForSection(long section, long ModifiedAfterThisUtcTime, out long QueryExecutedTime, out AnnotationService.Types.LocationLink[] DeletedLinks)
        {
            using (ConnectomeEntities db = GetOrCreateReadOnlyContext())
            {
                //TODO: This needs a real assignment, but I haven't created the table yet
                DeletedLinks = [];
                DateTime start = DateTime.Now;
                DateTime? ModifiedAfter = ModifiedAfterThisUtcTime == 0
                    ? new DateTime?()
                    : new DateTime?(new DateTime(ModifiedAfterThisUtcTime, DateTimeKind.Unspecified));
                QueryExecutedTime = DateTime.Now.ToUniversalTime().Ticks;
                try
                {
                    //IQueryable<ConnectomeDataModel.Location> queryResults = from l in db.ConnectomeDataModel.Locations where ((double)section) == l.Z select l;
                    var locationLinks = db.SelectSectionLocationLinks(section, ModifiedAfter);

                    Debug.WriteLine(section.ToString() + ": Query: " + new TimeSpan(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds);

                    //AnnotationService.Types.LocationLink[] retList = new AnnotationService.Types.LocationLink[locationLinks.Count];

                    //Debug.WriteLine(section.ToString() + ": To list: " + new TimeSpan(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds);

                    AnnotationService.Types.LocationLink[] retList = [.. locationLinks.Select(link => link.Create())];
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

            return [];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public AnnotationService.Types.LocationLink[] GetLocationLinksForSectionInMosaicRegion(long section, BoundingRectangle bbox, double MinRadius, long ModifiedAfterThisUtcTime, out long QueryExecutedTime, out AnnotationService.Types.LocationLink[] DeletedLinks)
        {
            using (ConnectomeEntities db = GetOrCreateReadOnlyContext())
            {
                //TODO: This needs a real assignment, but I haven't created the table yet
                DeletedLinks = [];
                DateTime start = DateTime.Now;
                DateTime? ModifiedAfter = ModifiedAfterThisUtcTime == 0
                    ? new DateTime?()
                    : new DateTime?(new DateTime(ModifiedAfterThisUtcTime, DateTimeKind.Unspecified));
                QueryExecutedTime = DateTime.Now.ToUniversalTime().Ticks;
                try
                {
                    //IQueryable<ConnectomeDataModel.Location> queryResults = from l in db.ConnectomeDataModel.Locations where ((double)section) == l.Z select l;
                    var locationLinks = db.SelectSectionLocationLinksInMosaicBounds((double)section, bbox.ToGeometry(), MinRadius, ModifiedAfter);// (section, ModifiedAfter).ToList();

                    Debug.WriteLine(section.ToString() + ": Query: " + new TimeSpan(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds);

                    AnnotationService.Types.LocationLink[] retList = [.. locationLinks.Select(link => link.Create())];
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

            return [];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public AnnotationService.Types.LocationLink[] GetLocationLinksForSectionInVolumeRegion(long section, BoundingRectangle bbox, double MinRadius, long ModifiedAfterThisUtcTime, out long QueryExecutedTime, out AnnotationService.Types.LocationLink[] DeletedLinks)
        {
            using (ConnectomeEntities db = GetOrCreateReadOnlyContext())
            {
                //TODO: This needs a real assignment, but I haven't created the table yet
                DeletedLinks = [];
                DateTime start = DateTime.Now;
                DateTime? ModifiedAfter = ModifiedAfterThisUtcTime == 0
                    ? new DateTime?()
                    : new DateTime?(new DateTime(ModifiedAfterThisUtcTime, DateTimeKind.Unspecified));
                QueryExecutedTime = DateTime.Now.ToUniversalTime().Ticks;
                try
                {
                    //IQueryable<ConnectomeDataModel.Location> queryResults = from l in db.ConnectomeDataModel.Locations where ((double)section) == l.Z select l;
                    var locationLinks = db.SelectSectionLocationLinksInVolumeBounds((double)section, bbox.ToGeometry(), MinRadius, ModifiedAfter);// (section, ModifiedAfter).ToList();

                    Debug.WriteLine(section.ToString() + ": Query: " + new TimeSpan(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds);

                    AnnotationService.Types.LocationLink[] retList = [.. locationLinks.Select(link => link.Create())];
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

            return [];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public long[] GetLinkedLocations(long ID)
        {
            using ConnectomeEntities db = GetOrCreateDatabaseContext();
            var links = (from u in db.LocationLinks.AsNoTracking() where u.A == ID select u.B).Union(from u in db.LocationLinks.AsNoTracking() where u.B == ID select u.A);
            return [.. links];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public LocationHistory[] GetLocationChangeLog(long? structure_id, DateTime? begin_time, DateTime? end_time)
        {
            using ConnectomeEntities db = GetOrCreateReadOnlyContext();
            var result = db.SelectStructureLocationChangeLog(structure_id, begin_time, end_time);
            List<SelectStructureLocationChangeLog_Result> listChanges = [.. result];

            return [.. listChanges.Select(loc => loc.Create())];
        }


        #endregion


        #region ICircuit Members

        public SortedDictionary<long, AnnotationService.Types.StructureType> StructureTypesDictionary = [];

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public Graphx getGraph(int cellID, int numHops)
        {
            using ConnectomeEntities db = GetOrCreateDatabaseContext();
            // Create a new graph
            Graphx graph = new();

            // Get all the missing nodes
            List<long> MissingNodes = [cellID];

            // Get the nodes and build graph for numHops
            for (int i = 0; i < numHops; i++)
            {
                MissingNodes = webService_GetHop(graph, [.. MissingNodes]);
            }

            //Tell the graph which cells are not fully populated
            graph.FrontierNodes = MissingNodes;


            var structLocations = db.ApproximateStructureLocations();

            foreach (var result in structLocations)
            {
                if (result is null)
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


        public AnnotationService.Types.Structure[] webService_GetStructures(Graphx graph, long[] ids)
        {
            if (ids.Length == 0)
                return [];

            // connect to the AnnotationService.Types.Structure webservice 
            AnnotationService.Types.Structure[] FoundStructures = GetStructuresByIDs(ids, true);

            //List<long> ListMissingChildrenIDs = new List<long>();

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
                return [];
            }

            // Store all them missing structure ids and call webservice
            List<long> MissingRootStructureIds = [];

            foreach (long id in cellids)
            {
                // Test to see if the RootStructure is already in the nodelist            
                if (!graph.NodeList.ContainsKey(id))
                {
                    MissingRootStructureIds.Add(id);
                }
            }

            AnnotationService.Types.Structure[] MissingStructures = webService_GetStructures(graph, [.. MissingRootStructureIds]);

            List<long> ListMissingChildrenIDs = [];

            foreach (AnnotationService.Types.Structure structure in MissingStructures)
            {
                if (structure.ChildIDs is null)
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
            AnnotationService.Types.Structure[] ChildStructObjs = webService_GetStructures(graph, [.. ListMissingChildrenIDs]);

            List<long> ListAbsentSiblings = [];

            //Find missing structures and populate the list
            foreach (AnnotationService.Types.Structure child in ChildStructObjs)
            {
                //Temp Hack to skip desmosomes
                if (child.Links is null)
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

            AnnotationService.Types.Structure[] SiblingStructures = webService_GetStructures(graph, [.. ListAbsentSiblings]);

            //Find missing structures and populate the list
            foreach (AnnotationService.Types.Structure child in ChildStructObjs)
            {
                if (child.Links is null)
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

                        Edgex E = new(SourceCell.ParentID.Value, TargetCell.ParentID.Value, link, SourceTypeName);
                        graph.EdgeList.Add(E);
                    }
                }
            }

            List<long> ListAbsentParents = new(SiblingStructures.Length);

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

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public long[] getStructuresByTypeID(int typeID)
        {
            long[] structuresList;

            using (ConnectomeEntities db = GetOrCreateReadOnlyContext())
            {

                IQueryable<long> res = from a in db.Structures.AsNoTracking() where a.TypeID == typeID select a.ID;

                structuresList = [.. res];
            }

            return structuresList;
        }

        // num=1 structures
        // num=0 locations
        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public string[] getTopConnectedStructures(int num)
        {
            using ConnectomeEntities db = GetOrCreateDatabaseContext();
            SortedDictionary<long, long> topConnections = [];

            List<String> answer = [];

            if (num == 1)
            {
                var results = db.SelectNumConnectionsPerStructure();

                foreach (var row in results)
                {
                    string type = (row.Label is null || String.IsNullOrEmpty(row.Label)) ? "[None]" : "[" + row.Label + "]";
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
                    string type = (row.label is null || String.IsNullOrEmpty(row.label)) ? "[None]" : "[" + row.label + "]";
                    answer.Add(row.id + "~" + type + "~" + row.count);
                }



            }
            return [.. answer];
        }

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public string[] getTopConnectedCells()
        {
            List<string> result = [];

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

            return [.. result];

            /*
             * 
        var res = from s in db.ConnectomeDataModel.Structures where s.ParentID is null select s.ID;
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
            Dictionary<long, string> structureTypes = [];

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
            Dictionary<long, string> labelDictionary = [];

            using (ConnectomeEntities db = GetOrCreateDatabaseContext())
            {
                foreach (var row in db.SelectStructureLabels())
                {
                    labelDictionary[row.ID] = row.Label;
                }
            }

            return labelDictionary;
        }

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public SynapseObject getSynapseStats()
        {
            SortedDictionary<long, long> topConnections = [];

            using ConnectomeEntities db = GetOrCreateDatabaseContext();

            List<long> structureIDs = [.. (from s in db.Structures where s.ParentID == null select s.ID)];

            Dictionary<long, string[]> result = [];

            Dictionary<long, string> structureTypeName = _CreateStructureTypeIDToNameDict();

            foreach (int id in structureIDs.Select(v => (int)v))
            {
                List<string> childCountList = [];
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
                result[id] = [.. childCountList];
            }

            SynapseObject retObj = new();
            Dictionary<long, string> labelDictionary = _CreateStructureIDToLabelDict();

            foreach (var row in result)
            {
                SynapseStats temp = new()
                {
                    id = row.Key.ToString()
                };
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

        [PrincipalPermission(SecurityAction.Demand, Role = nameof(Roles.Read))]
        public string[] getSynapses(int cellID)
        {
            AnnotationService.Types.Structure mainStructure = GetStructureByID(cellID, true);
            if (mainStructure.ChildIDs is null)
            {
                return [];
            }

            AnnotationService.Types.Structure[] synapses = GetStructuresByIDs(mainStructure.ChildIDs, false);
            SortedDictionary<long, long> temp = new()
            {
                [1] = synapses.Count()
            };

            foreach (AnnotationService.Types.Structure child in synapses)
            {
                if (temp.Keys.Contains(child.TypeID))
                    temp[child.TypeID]++;
                else
                    temp[child.TypeID] = 1;
            }

            var temp2 = (from entry in temp orderby entry.Value ascending select entry);

            Dictionary<string, long> result = [];

            foreach (var tuple in temp2)
            {
                string name = GetStructureTypeByID(tuple.Key).Name;
                if (name == "Cell")
                    name = "Total Count";
                result[name] = tuple.Value;
            }

            List<string> ans = [];
            foreach (var row in result)
                ans.Add(row.Key + "," + row.Value);


            return [.. ans];

        }
        #endregion
    }
}
