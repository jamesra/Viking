using AnnotationService.Types;
using System;
using System.Collections.Concurrent;
using System.ServiceModel;
using WebAnnotationModel.Service;

namespace WebAnnotationModel
{
    public class StructureTypeStore : StoreBaseWithIndexKeyAndParent<AnnotateStructureTypesClient,
                                        IAnnotateStructureTypes,
                                        long,
                                        LongIndexGenerator,
                                        StructureTypeObj,
                                        StructureType>
    {
        public StructureTypeStore()
        {
            channelFactory =
                new ChannelFactory<IAnnotateStructureTypes>("Annotation.Service.Interfaces.IAnnotateStructureTypes-Binary");

            channelFactory.Credentials.UserName.UserName = State.UserCredentials.UserName;
            channelFactory.Credentials.UserName.Password = State.UserCredentials.Password;
        }

        #region Proxy
         

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

        protected override long[] ProxyUpdate(IAnnotateStructureTypes proxy, StructureType[] objects)
        {
            return proxy.UpdateStructureTypes(objects);
        }

        protected override StructureType ProxyGetByID(IAnnotateStructureTypes proxy, long ID)
        {
            return proxy.GetStructureTypeByID(ID);
        }

        protected override StructureType[] ProxyGetByIDs(IAnnotateStructureTypes proxy, long[] IDs)
        {
            return proxy.GetStructureTypesByIDs(IDs);
        }



        protected override StructureType[] ProxyGetBySectionCallback(out long TicksAtQueryExecute,
                                                                     out long[] DeletedLocations,
                                                                     GetObjectBySectionCallbackState<IAnnotateStructureTypes, StructureTypeObj> state,
                                                                     IAsyncResult result)
        {
            throw new NotImplementedException();
        }

        protected override StructureType[] ProxyGetBySection(IAnnotateStructureTypes proxy, long SectionNumber, DateTime LastQuery,
                                                                out long TicksAtQueryExecute,
                                                                out long[] deleted_objects)
        {
            deleted_objects = new long[0];
            TicksAtQueryExecute = 0;
            return proxy.GetStructureTypes();
        }

        protected override StructureType[] ProxyGetBySectionRegion(IAnnotateStructureTypes proxy, long SectionNumber, BoundingRectangle BBox, double MinRadius, DateTime LastQuery,
                                                                out long TicksAtQueryExecute,
                                                                out long[] deleted_objects)
        {
            deleted_objects = new long[0];
            TicksAtQueryExecute = 0;
            return proxy.GetStructureTypes();
        }

        protected override IAsyncResult ProxyBeginGetBySection(IAnnotateStructureTypes proxy,
                                                             long SectionNumber,
                                                             DateTime LastQuery,
                                                             AsyncCallback callback,
                                                             object asynchState)
        {
            throw new NotImplementedException();
        }

        protected override IAsyncResult ProxyBeginGetBySectionRegion(IAnnotateStructureTypes proxy, long SectionNumber, BoundingRectangle BBox, double MinRadius, DateTime LastQuery, AsyncCallback callback, object asynchState)
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
            IClientChannel proxy = null;
            StructureTypeObj created_structuretype = null;
            try
            {
                proxy = CreateProxy();
                StructureType created_db_structuretype = ((IAnnotateStructureTypes)proxy).CreateStructureType(new_type.GetData());
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
            StructureType[] types = new StructureType[0];
            IClientChannel proxy = null;
            try
            {
                proxy = CreateProxy();
                proxy.Open();
                types = ((IAnnotateStructureTypes)proxy).GetStructureTypes();
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

            if (types == null)
                return;


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

        protected override StructureType[] ProxyGetBySectionRegionCallback(out long TicksAtQueryExecute, out long[] DeletedLocations, GetObjectBySectionCallbackState<IAnnotateStructureTypes, StructureTypeObj> state, IAsyncResult result)
        {
            throw new NotImplementedException();
        }
    }
}
