using AnnotationService.Types;
using System;
using System.Collections.Concurrent;
using WebAnnotationModel.Service;
using WebAnnotationModel.Objects;

namespace WebAnnotationModel
{
    public class StructureTypeStore : StoreBaseWithIndexKeyAndParent<AnnotateStructureTypesClient,
                                        IAnnotateStructureTypes,
                                        long,
                                        LongIndexGenerator,
                                        StructureTypeObj,
                                        StructureType>
    {
        #region Proxy

        protected override AnnotateStructureTypesClient CreateProxy()
        {
            AnnotateStructureTypesClient proxy = null;
            try
            {
                proxy = new Service.AnnotateStructureTypesClient("Annotation.Service.Interfaces.IAnnotateStructureTypes-Binary", State.EndpointAddress);
                proxy.ClientCredentials.UserName.UserName = State.UserCredentials.UserName;
                proxy.ClientCredentials.UserName.Password = State.UserCredentials.Password;
            }
            catch
            {
                if (proxy != null)
                {
                    proxy.Close();
                }
                throw;

            }
            return proxy;
        }


        /*
    public override StructureTypeObj Create()
    {
        StructureTypeObj newObj = new StructureTypeObj();
        InternalAdd(newObj); 

        AnnotateStructureTypesClient proxy = null;
        try
        {
            proxy = CreateProxy();
            proxy.Open();

            long OriginalID = newObj.ID;

            long[] newIDs = proxy.UpdateStructureTypes(new StructureType[] { newObj.GetData() });



            Store.Locations.InternalDelete(newObj.ID);

            newObj.GetData().ID = newIDs[1];
            newObj.GetData().ParentID = newIDs[0];
            newObj.DBAction = DBACTION.NONE;

            Store.StructureTypes.InternalAdd(newObj);

        }
        catch (Exception e)
        {
            ShowStandardExceptionMessage(e);
            InternalDelete(newObj.ID);
            return null;
        }
        finally
        {
            if (proxy != null)
                proxy.Close();
        }

        return newObj; 
    }
*/

        protected override long[] ProxyUpdate(AnnotateStructureTypesClient proxy, StructureType[] objects)
        {
            return proxy.UpdateStructureTypes(objects);
        }

        protected override StructureType ProxyGetByID(AnnotateStructureTypesClient proxy, long ID)
        {
            return proxy.GetStructureTypeByID(ID);
        }

        protected override StructureType[] ProxyGetByIDs(AnnotateStructureTypesClient proxy, long[] IDs)
        {
            return proxy.GetStructureTypesByIDs(IDs);
        }



        protected override StructureType[] ProxyGetBySectionCallback(out long TicksAtQueryExecute,
                                                                     out long[] DeletedLocations,
                                                                     GetObjectBySectionCallbackState<AnnotateStructureTypesClient, StructureTypeObj> state,
                                                                     IAsyncResult result)
        {
            throw new NotImplementedException();
        }

        protected override StructureType[] ProxyGetBySection(AnnotateStructureTypesClient proxy, long SectionNumber, DateTime LastQuery,
                                                                out long TicksAtQueryExecute,
                                                                out long[] deleted_objects)
        {
            deleted_objects = Array.Empty<long>();
            TicksAtQueryExecute = 0;
            return proxy.GetStructureTypes();
        }

        protected override StructureType[] ProxyGetBySectionRegion(AnnotateStructureTypesClient proxy, long SectionNumber, BoundingRectangle BBox, double MinRadius, DateTime LastQuery,
                                                                out long TicksAtQueryExecute,
                                                                out long[] deleted_objects)
        {
            deleted_objects = Array.Empty<long>();
            TicksAtQueryExecute = 0;
            return proxy.GetStructureTypes();
        }

        protected override IAsyncResult ProxyBeginGetBySection(AnnotateStructureTypesClient proxy,
                                                             long SectionNumber,
                                                             DateTime LastQuery,
                                                             AsyncCallback callback,
                                                             object asynchState)
        {
            throw new NotImplementedException();
        }

        protected override IAsyncResult ProxyBeginGetBySectionRegion(AnnotateStructureTypesClient proxy, long SectionNumber, BoundingRectangle BBox, double MinRadius, DateTime LastQuery, AsyncCallback callback, object asynchState)
        {
            throw new NotImplementedException();
        }

        public override ConcurrentDictionary<long, StructureTypeObj> GetLocalObjectsForSection(long SectionNumber)
        {
            throw new NotImplementedException();
        }

        #endregion

        public override void Init()
        {
            LoadStructureTypes();
        }


        public StructureTypeObj Create(StructureTypeObj new_type)
        {
            AnnotateStructureTypesClient proxy = null;
            StructureTypeObj created_structuretype = null;
            try
            {
                proxy = CreateProxy();
                StructureType created_db_structuretype = proxy.CreateStructureType(new_type.GetData());
                if (created_db_structuretype == null)
                    return null;

                created_structuretype = new StructureTypeObj(created_db_structuretype);

                Add(created_structuretype);

                return created_structuretype;
            }
            finally
            {
                if (proxy != null)
                    proxy.Close();
            }

        }

        /// <summary>
        /// At startup we load the entire structure types table since it is fairly static
        /// </summary>
        public void LoadStructureTypes()
        {
            StructureType[] types = Array.Empty<StructureType>();
            AnnotateStructureTypesClient proxy = null;
            try
            {
                proxy = CreateProxy();
                proxy.Open();
                types = proxy.GetStructureTypes();
            }
            catch (Exception e)
            {
                ShowStandardExceptionMessage(e);
            }
            finally
            {
                if (proxy != null)
                    proxy.Close();
            }

            //Populate our cache
            StructureTypeObj[] objList = new StructureTypeObj[types.Length];
            //Dictionary<long, StructureType> TypesTable = new Dictionary<long, StructureType>(types.Length);

            for (int i = 0; i < types.Length; i++)
            {
                objList[i] = new StructureTypeObj(types[i]);
                //Do this first so we don't ask the server for parents we've already downloaded
                //IDToObject.Add(typeObj.ID, typeObj);

                //InternalAdd(typeObj, false); 
            }

            ChangeInventory<StructureTypeObj> inventory = InternalAdd(objList, false);
            CallOnCollectionChanged(inventory);
            //Populate Parent/Child relationships

            /*
            foreach (StructureTypeObj typeObj in objList)
            {
                Add(typeObj); 
            }
             */
        }

        protected override StructureType[] ProxyGetBySectionRegionCallback(out long TicksAtQueryExecute, out long[] DeletedLocations, GetObjectBySectionCallbackState<AnnotateStructureTypesClient, StructureTypeObj> state, IAsyncResult result)
        {
            throw new NotImplementedException();
        }
    }
}
