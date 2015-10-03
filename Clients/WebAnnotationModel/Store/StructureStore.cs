using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Collections.Concurrent; 
using System.Linq;
using System.Text;

using System.Diagnostics;
using System.ServiceModel;

using WebAnnotationModel.Service;
using WebAnnotationModel.Objects;

namespace WebAnnotationModel
{
    public class CreateStructureAndLocationRetval
    {
        public readonly StructureObj structure;
        public readonly LocationObj location;

        internal CreateStructureAndLocationRetval(StructureObj s, LocationObj l)
        {
            structure = s;
            location = l;
        }
    }
    public class StructureStore : StoreBaseWithIndexKeyAndParent<AnnotateStructuresClient, IAnnotateStructures, long, LongIndexGenerator, StructureObj, Structure>
    {
        #region Proxy


        protected override AnnotateStructuresClient CreateProxy()
        {
            AnnotateStructuresClient proxy = null;
            try
            {
                proxy = new Service.AnnotateStructuresClient("Annotation.Service.Interfaces.IAnnotateStructures-Binary", State.EndpointAddress);
                proxy.ClientCredentials.UserName.UserName = State.UserCredentials.UserName;
                proxy.ClientCredentials.UserName.Password = State.UserCredentials.Password; 
            }
            catch (Exception e)
            {
                if(proxy != null)
                {
                    proxy.Close();
                    proxy = null; 
                }
                throw;
            }

            return proxy; 
        }

        protected override long[] ProxyUpdate(AnnotateStructuresClient proxy, Structure[] objects)
        {
            return proxy.UpdateStructures(objects);
        }

        protected override Structure ProxyGetByID(AnnotateStructuresClient proxy, long ID)
        {
            return proxy.GetStructureByID(ID, false);
        }

        protected override Structure[] ProxyGetByIDs(AnnotateStructuresClient proxy, long[] IDs)
        {
            Structure[] structures = proxy.GetStructuresByIDs(IDs, false);
            if (structures != null)
                return structures.ToArray<Structure>();

            return new Structure[0]; 
        }


        /// <summary>
        /// Get the location ID's for branches that are incomplete
        /// </summary>
        /// <returns></returns>
        public long[] GetUnfinishedBranches(long structureID)
        {
            using (AnnotateStructuresClient proxy = CreateProxy())
            {
                long[] ids = proxy.GetUnfinishedLocations(structureID);
                return ids;
            }
        }

        /// <summary>
        /// Get the location ID's and positions for branches that are incomplete
        /// </summary>
        /// <returns></returns>
        public LocationPositionOnly[] GetUnfinishedBranchesWithPosition(long structureID)
        {
            using (AnnotateStructuresClient proxy = CreateProxy())
            {
                return proxy.GetUnfinishedLocationsWithPosition(structureID);
            }
        }

        public override ConcurrentDictionary<long, StructureObj> GetLocalObjectsForSection(long SectionNumber)
        {
            return new ConcurrentDictionary<long, StructureObj>(); 
        }

        protected override Structure[] ProxyGetBySection(AnnotateStructuresClient proxy, long SectionNumber, DateTime LastQuery, out long TicksAtQueryExecute, out long[] DeletedLocations)
        {
            return proxy.GetStructuresForSection(out TicksAtQueryExecute, out DeletedLocations, SectionNumber, LastQuery.Ticks);
        }

        protected override Structure[] ProxyGetBySectionRegion(AnnotateStructuresClient proxy, long SectionNumber, BoundingRectangle BBox, double MinRadius, DateTime LastQuery, out long TicksAtQueryExecute, out long[] DeletedLocations)
        {
            return proxy.GetStructuresForSectionInRegion(out TicksAtQueryExecute, out DeletedLocations, SectionNumber, BBox, MinRadius, LastQuery.Ticks);
        } 

        protected override IAsyncResult ProxyBeginGetBySectionRegion(AnnotateStructuresClient proxy, long SectionNumber, BoundingRectangle BBox, double MinRadius, DateTime LastQuery, AsyncCallback callback, object asynchState)
        {
            return proxy.BeginGetStructuresForSectionInRegion(SectionNumber, BBox, MinRadius, LastQuery.Ticks, callback, asynchState);
        }
         
        protected override Structure[] ProxyGetBySectionRegionCallback(out long TicksAtQueryExecute, out long[] DeletedObjects, GetObjectBySectionCallbackState state, IAsyncResult result)
        {
            return state.Proxy.EndGetStructuresForSectionInRegion(out TicksAtQueryExecute, out DeletedObjects, result);
        }

        /// <summary>
        /// This currently always returns the empty result because its main purpose is to populate the cache so locations can determine thier type
        /// </summary>
        /// <param name="proxy"></param>
        /// <param name="SectionNumber"></param>
        /// <param name="LastQuery"></param>
        /// <param name="callback"></param>
        /// <param name="asynchState"></param>
        /// <returns></returns>
        protected override IAsyncResult ProxyBeginGetBySection(AnnotateStructuresClient proxy,
                                                                                            long SectionNumber,
                                                                                            DateTime LastQuery,
                                                                                            AsyncCallback callback,
                                                                                            object asynchState)
        {
            Debug.WriteLine("Get Structures for section: ", SectionNumber.ToString());

            return proxy.BeginGetStructuresForSection(SectionNumber, LastQuery.Ticks, callback, asynchState);
        }

        protected override Structure[] ProxyGetBySectionCallback(out long TicksAtQueryExecute, out long[] DeletedIDs, GetObjectBySectionCallbackState state, IAsyncResult result)
        {
            return state.Proxy.EndGetStructuresForSection(out TicksAtQueryExecute, out DeletedIDs, result); 
        }

        public override bool RemoveSection(int SectionNumber)
        {
            //Section store never deletes structures, but we return true so queries in flight can be aborted
            return true;
        }

        #endregion
                
        public override void Init()
        {

#if DEBUG
//            GetAllStructures(); 
#else
//            GetAllStructures(); 
#endif 
            
        }

        public void GetAllStructures()
        {
            Trace.WriteLine("GetAllStructures, Begin", "WebAnnotation");
            
            Structure[] structures = new Structure[0];

            AnnotateStructuresClient proxy = null;
            try
            {
                proxy = CreateProxy();
                proxy.Open();

                //Cache all the structures at startup
                structures = proxy.GetStructures();
            }
            catch (Exception e)
            {
                ShowStandardExceptionMessage(e);
                return;
            }
            finally
            {
                if (proxy != null)
                    proxy.Close();
            }

            ChangeInventory<StructureObj> inventory = ParseQuery(structures, new long[0], null);
            CallOnCollectionChanged(inventory);

            Trace.WriteLine("GetAllStructures, End", "WebAnnotation");
        }
        
        public void CheckForOrphan(long ID)
        {
            StructureObj obj = GetObjectByID(ID); 
            if(obj == null)
                return;

            long numLocs = 0; 

            AnnotateStructuresClient proxy = null;
            try
            {
                proxy = CreateProxy();
                proxy.Open();

                numLocs = proxy.NumberOfLocationsForStructure(ID);
            }
            catch (Exception e)
            {
                ShowStandardExceptionMessage(e);
                return;
            }
            finally
            {
                if(proxy != null)
                    proxy.Close();
            }
            
            if (numLocs == 0)
            {
                /*
                string name; 
                if(obj.Type != null)
                    name = obj.Type.Name + " " + obj.ID.ToString();
                else
                    name = obj.ID.ToString();
                */
                /*
                System.Windows.Forms.DialogResult result = System.Windows.Forms.MessageBox.Show("Structure " + name + " has no locations, do you wish to delete from the database?", 
                                                            "Continue Delete?",
                                                            System.Windows.Forms.MessageBoxButtons.YesNo);

                //Delete on yes
                if (result == System.Windows.Forms.DialogResult.Yes)
                {*/
                    try
                    {
                        //TODO
                        Remove(obj);
                        Save();
                        Trace.WriteLine("Removing childless structure: " + obj.ToString(), "WebAnnotation");

                    }
                    catch (Exception )
                    {
           //             System.Windows.Forms.MessageBox.Show("Delete failed.  Structure may have had child location added or already been deleted. Exception: " + e.ToString(), "Survivable error"); 
                    }
              //  }
            }
        }

        public StructureObj Create(StructureObj newStruct, LocationObj newLocation, out LocationObj created_loc)
        { 
            AnnotateStructuresClient proxy = null;

            created_loc = null; 
            try
            { 
                proxy = CreateProxy();
                proxy.Open();

                CreateStructureRetval retval = proxy.CreateStructure(newStruct.GetData(), newLocation.GetData());
                 
                //We should not insert created objects into the store before they are created on the server
                Debug.Assert(this.GetObjectByID(newStruct.ID, false) == null);

                StructureObj created_struct = new StructureObj(retval.structure);

                ChangeInventory<StructureObj> inventory = InternalAdd(created_struct);
                created_loc = new LocationObj(retval.location); 

                CallOnCollectionChangedForAdd(new StructureObj[] { created_struct });
                Store.Locations.AddFromFriend(new LocationObj[] { created_loc });

                return created_struct; 
            }
            catch (Exception e)
            {
                ShowStandardExceptionMessage(e);
                StructureObj deletedObj = InternalDelete(newStruct.ID);
                CallOnCollectionChangedForDelete(new StructureObj[] {deletedObj});
                return null; 
            }
            finally
            {
                if(proxy != null)
                    proxy.Close();
            }
        }

        public override bool Remove(StructureObj obj)
        {
            obj.DBAction = DBACTION.DELETE;

            return true; 
        }

        public ICollection<StructureObj> GetChildStructuresForStructure(long ID)
        {
            AnnotateStructuresClient proxy = null;
            try
            {
                proxy = CreateProxy();
                proxy.Open();

                Structure data = proxy.GetStructureByID(ID, true);
                if(data != null)
                {
                    if (data.ChildIDs.Length > 0)
                    {
                        ICollection<StructureObj> list_structures = this.GetObjectsByIDs(data.ChildIDs, true);
                        ChangeInventory<StructureObj> inventory = InternalAdd(list_structures.ToArray());
                        CallOnCollectionChanged(inventory);
                        return inventory.ObjectsInStore; 
                    }
                }
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

            return new StructureObj[0]; 
        }

        public long Merge(long KeepID, long MergeID)
        {
            AnnotateStructuresClient proxy = null;
            try
            {
                proxy = CreateProxy();
                proxy.Open();

                KeepID = proxy.Merge(KeepID, MergeID);
                return 0;
            }
            catch (Exception e)
            {
                ShowStandardExceptionMessage(e);
                throw e;
            }
            finally
            {
                if (proxy != null)
                    proxy.Close();
            }

            return 0; 
        }


        public ICollection<StructureObj> GetStructuresOfType(long StructureTypeID)
        {
            Structure[] data = null;
            AnnotateStructuresClient proxy = null;
            try
            {
                proxy = CreateProxy();
                proxy.Open();

                data = proxy.GetStructuresOfType(StructureTypeID);
            }
            catch (Exception e)
            {
                ShowStandardExceptionMessage(e);
                data = null;
            }
            finally
            {
                if (proxy != null)
                    proxy.Close();
            }

            if (null == data)
                return new StructureObj[0];

            List<StructureObj> listStructures = new List<StructureObj>(data.Length);
            foreach (Structure s in data)
            {
                Debug.Assert(s != null);

                StructureObj newObj = new StructureObj(s);
                listStructures.Add(newObj);
            }

            ChangeInventory<StructureObj> output = InternalAdd(listStructures.ToArray()); //Add might return an existing object, which we should use instead
            CallOnCollectionChanged(output);
            return output.ObjectsInStore;
        }

    }
}
