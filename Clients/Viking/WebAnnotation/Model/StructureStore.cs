using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Diagnostics;
using System.ServiceModel; 

using WebAnnotation.Service; 
using WebAnnotation.Objects;

namespace WebAnnotation
{
    class StructureStore : StoreBase<AnnotateStructuresClient, IAnnotateStructures, StructureObj, Structure>
    {
        #region Proxy

        protected override AnnotateStructuresClient CreateProxy()
        {
            AnnotateStructuresClient proxy = new Service.AnnotateStructuresClient("Annotation.Service.Interfaces.IAnnotateStructures-Binary", Global.EndpointAddress);
            proxy.ClientCredentials.UserName.UserName = Viking.UI.State.UserCredentials.UserName;
            proxy.ClientCredentials.UserName.Password = Viking.UI.State.UserCredentials.Password;
            return proxy; 
        }

        internal override long[] ProxyUpdate(AnnotateStructuresClient proxy, Structure[] objects)
        {
            return proxy.UpdateStructures(objects);
        }

        internal override Structure ProxyGetByID(AnnotateStructuresClient proxy, long ID)
        {
            return proxy.GetStructureByID(ID, false);
        }

        #endregion
                
        static public List<StructureObj> rootStructures = new List<StructureObj>();

        public override void Init()
        {
            
            GetAllStructures(); 
            
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

            foreach (Structure s in structures)
            {
                StructureObj obj = new StructureObj();
                obj.Synch(s);
                Add(obj, false);
            }

            Trace.WriteLine("GetAllStructures, End", "WebAnnotation");
        }

        public List<StructureObj> GetStructuresByIDs(List<long> listIDs, bool RemoveExistingIDs)
        {
            if (RemoveExistingIDs)
            {
                for (int i = 0; i < listIDs.Count; i++)
                {
                    long ID = listIDs[i];
                    if (this.IDToObject.ContainsKey(ID))
                    {
                        listIDs.RemoveAt(i);
                        i--; 
                    }
                }
            }

            if (listIDs.Count == 0)
                return new List<StructureObj>();

            System.DateTime StartTime = DateTime.Now; 
            Trace.WriteLine("Fetching Structures by ID, Begin: " + listIDs.Count.ToString() + " structures.", "WebAnnotation");
       
            Structure[] structures = new Structure[0]; 
            AnnotateStructuresClient proxy = null;
            List<StructureObj> listStructures = new List<StructureObj>(listIDs.Count); 
            try
            {
                proxy = this.CreateProxy();
                proxy.Open();

                structures = proxy.GetStructuresByIDs(listIDs.ToArray(), false);
            }
            catch (Exception e)
            {
                ShowStandardExceptionMessage(e);
                return listStructures;
            }
            finally
            {
                if (proxy != null)
                    proxy.Close();
            }

            System.TimeSpan elapsed = new TimeSpan(DateTime.Now.Ticks - StartTime.Ticks); 
            Trace.WriteLine("Fetching Structures by ID, Returned from server + " + elapsed.ToString(), "WebAnnotation");

       //     lock (LockObject)
            {

                foreach (Structure s in structures)
                {
                    StructureObj obj = new StructureObj();
                    obj.Synch(s);
                    Add(obj, false);
                    listStructures.Add(obj);
                }
            }

            elapsed = new TimeSpan(DateTime.Now.Ticks - StartTime.Ticks);
            Trace.WriteLine("Fetching Structures by ID, End " + elapsed.ToString(), "WebAnnotation");

            return listStructures; 
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
                string name; 
                if(obj.Type != null)
                    name = obj.Type.Name + " " + obj.ID.ToString();
                else
                    name = obj.ID.ToString();

                System.Windows.Forms.DialogResult result = System.Windows.Forms.MessageBox.Show("Structure " + name + " has no locations, do you wish to delete from the database?", 
                                                            "Continue Delete?",
                                                            System.Windows.Forms.MessageBoxButtons.YesNo);

                //Delete on yes
                if (result == System.Windows.Forms.DialogResult.Yes)
                {
                    try
                    {
                        obj.Delete();
                    }
                    catch (Exception e)
                    {
                        System.Windows.Forms.MessageBox.Show("Delete failed.  Structure may have had child location added or already been deleted. Exception: " + e.ToString(), "Survivable error"); 
                    }
                }
            }
        }

        internal void CreateStructure(StructureObj newStruct, LocationObj newLocation)
        {
            Add(newStruct);
            AnnotateStructuresClient proxy = null;
            try
            {

                proxy = CreateProxy();
                proxy.Open();

                long[] newIDs = proxy.CreateStructure(newStruct.GetData(), newLocation.GetData());

                newStruct.GetData().ID = newIDs[0];
                newStruct.DBAction = DBACTION.NONE;

                Store.Locations.Remove(newLocation.ID);

                newLocation.GetData().ID = newIDs[1];
                newLocation.GetData().ParentID = newIDs[0];
                newLocation.DBAction = DBACTION.NONE;

                Store.Locations.Add(newLocation);

            }
            catch (Exception e)
            {
                ShowStandardExceptionMessage(e);
                Remove(newStruct.ID);
                return;
            }
            finally
            {
                if(proxy != null)
                    proxy.Close();
            }
        }

        internal override StructureObj Update(StructureObj updateObj)
        {
            StructureObj obj = IDToObject[updateObj.ID];

            //Remove ourselves from the root list if we have a ParentID
            if (!obj.ParentID.HasValue)
            {
                rootStructures.Remove(obj);
            }
            else
            {
                //Remove ourselves from our parent object
                if (obj.ParentID != updateObj.ParentID)
                    obj.Parent = null;
            }

            //Update if the new DB object has a later modified date. 
            obj.Synch(updateObj.GetData());

            //Add ourselves from the root list if we do not have a ParentID
            if (!obj.ParentID.HasValue)
            {
                rootStructures.Add(obj);
            }
            else
            {
                //Make sure the structure object points to the correct parent
                obj.Parent = GetObjectByID(obj.ParentID.Value);

                //If it returns null we couldn't find the parent on the server, what the hell?
                Debug.Assert(obj.Parent != null, "Couldn't locate parent of the structureType, Hit continue to reload all structure types in a panic");
            }

            CallOnAddUpdateRemoveKey(updateObj, new AddUpdateRemoveKeyEventArgs(updateObj.ID, AddUpdateRemoveKeyEventArgs.Action.UPDATE));
            

            return obj;
        }

        internal override StructureObj Add(StructureObj newType)
        {
            return Add(newType, false); 
        }

        internal StructureObj Add(StructureObj newObj, bool LoadParents)
        {
            if (IDToObject.ContainsKey(newObj.ID))
            {
                return Update(newObj);
            }
            else
            {
                if (newObj.ParentID.HasValue == false)
                {
                    rootStructures.Add(newObj);
                }
                else if(LoadParents) 
                {
                    //Added the false for dynamic structure loading change, may cause bugs
                    newObj.Parent = GetObjectByID(newObj.ParentID.Value, true);

                    //If it returns null we couldn't find the parent on the server, what the hell?
                    Debug.Assert(newObj.Parent != null, "Couldn't locate parent of the structureType, Hit continue to reload all structure types in a panic");
                }

                //Add the new object to the table
                IDToObject[newObj.ID] = newObj;

                CallOnAddUpdateRemoveKey(newObj, new AddUpdateRemoveKeyEventArgs(newObj.ID, AddUpdateRemoveKeyEventArgs.Action.ADD));

                return newObj;
            }

        }

        internal override void Remove(long ID)
        {
            StructureObj obj = null;
            if (IDToObject.ContainsKey(ID) == false)
                return; 

            obj = IDToObject[ID];

            //Let consumers know this key is about to go away
            CallOnAddUpdateRemoveKey(obj, new AddUpdateRemoveKeyEventArgs(ID, AddUpdateRemoveKeyEventArgs.Action.REMOVE));
            
            if (obj.ParentID.HasValue == false && rootStructures.Contains(obj))
                rootStructures.Remove(obj);

            IDToObject.TryRemove(ID, out obj);
        }

        public LocationObj[] GetLocationsForStructure(long StructureID)
        {
            Location[] data = null;
            AnnotateStructuresClient proxy = null;
            try
            {
                proxy = CreateProxy();
                proxy.Open();

                data = proxy.GetLocationsForStructure(StructureID);
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
                return new LocationObj[0]; 
            
            List<LocationObj> listLocations = new List<LocationObj>(data.Length); 
            foreach (Location loc in data)
            {
                LocationObj newObj = new LocationObj(loc);
                newObj = Store.Locations.Add(newObj); //Add might return an existing object, which we should use instead
                listLocations.Add(newObj); 
            }

            return listLocations.ToArray(); 
        }

        public void CreateLink(long SourceID, long TargetID)
        {
            StructureLink link = new StructureLink();
            link.SourceID = SourceID;
            link.TargetID = TargetID;
            link.Bidirectional = true;
            link.DBAction = DBACTION.INSERT; 

            AnnotateStructuresClient proxy = CreateProxy();
            try
            {
                proxy.CreateStructureLink(link);
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

            StructureObj A = GetObjectByID(SourceID);
            A.AddLink(link);

            StructureObj B = GetObjectByID(TargetID);
            B.AddLink(link);

            StructureObj AObj = Update(A);
            StructureObj BObj = Update(B);

            CallOnAllUpdatesCompleted(new OnAllUpdatesCompletedEventArgs(new object[] {AObj, BObj})); 
        }

        public void SaveLinks(StructureLinkObj[] linkObjs)
        {
            StructureLink[] links = new StructureLink[linkObjs.Length];

            for(int i = 0; i < linkObjs.Length; i++)
            {
                links[i] = linkObjs[i].GetData();

                switch (links[i].DBAction)
                {
                    case DBACTION.INSERT:
                        linkObjs[i].FireBeforeSaveEvent();
                        break; 
                    case DBACTION.UPDATE:
                        linkObjs[i].FireBeforeSaveEvent();
                        break; 
                    case DBACTION.DELETE:
                        linkObjs[i].FireBeforeDeleteEvent();
                        break;
                }
            }

            AnnotateStructuresClient proxy = CreateProxy();
            try
            {
                proxy.UpdateStructureLinks(links);
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

            for (int i = 0; i < linkObjs.Length; i++)
            {
                links[i] = linkObjs[i].GetData();

                StructureObj SourceObj = GetObjectByID(linkObjs[i].SourceID); 
                StructureObj TargetObj = GetObjectByID(linkObjs[i].TargetID); 

                switch (links[i].DBAction)
                {
                    case DBACTION.INSERT:
                        SourceObj.AddLink(links[i]);
                        TargetObj.AddLink(links[i]);
                        linkObjs[i].FireAfterSaveEvent();
                        break;
                    case DBACTION.UPDATE:
                        linkObjs[i].FireAfterSaveEvent();
                        break;
                    case DBACTION.DELETE:
                        SourceObj.RemoveLink(links[i]);
                        TargetObj.RemoveLink(links[i]);
                        linkObjs[i].FireAfterDeleteEvent();
                        break;
                }

                links[i].DBAction = DBACTION.NONE;
            }
        }
    
    }
}
