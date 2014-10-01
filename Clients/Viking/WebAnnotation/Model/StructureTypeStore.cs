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
    class StructureTypeStore : StoreBase<AnnotateStructureTypesClient,
                                         IAnnotateStructureTypes,
                                        StructureTypeObj,
                                         StructureType>
    {
        #region Proxy

        protected override AnnotateStructureTypesClient CreateProxy()
        {
            AnnotateStructureTypesClient proxy =  new Service.AnnotateStructureTypesClient("Annotation.Service.Interfaces.IAnnotateStructureTypes-Binary", Global.EndpointAddress);
            proxy.ClientCredentials.UserName.UserName = Viking.UI.State.UserCredentials.UserName;
            proxy.ClientCredentials.UserName.Password = Viking.UI.State.UserCredentials.Password;
            return proxy; 
        }

        internal override long[] ProxyUpdate(AnnotateStructureTypesClient proxy, StructureType[] objects)
        {
            return proxy.UpdateStructureTypes(objects);
        }

        internal override StructureType ProxyGetByID(AnnotateStructureTypesClient proxy, long ID)
        {
            return proxy.GetStructureTypeByID(ID);
        }

        #endregion

        public List<StructureTypeObj> rootStructureTypes = new List<StructureTypeObj>();

        public override void  Init()
        {
 	        LoadStructureTypes();
        }

        internal override StructureTypeObj Add(StructureTypeObj newType)
        {
            return Add(newType, false); 
        }

        internal  StructureTypeObj Add(StructureTypeObj newType, bool LoadParents)
        {
            if (IDToObject.ContainsKey(newType.ID))
                return Update(newType);
            else
            {
                if (newType.ParentID.HasValue == false)
                {
                    rootStructureTypes.Add(newType);
                }
                else
                {
                    newType.Parent = GetObjectByID(newType.ParentID.Value, LoadParents);

                    //If it returns null we couldn't find the parent on the server, what the hell?
                    Debug.Assert(newType.Parent != null, "Couldn't locate parent of the structureType, Hit continue to reload all structure types in a panic");
                }
                
                //Add the new object to the table
                IDToObject[newType.ID] = newType;

                CallOnAddUpdateRemoveKey(newType, new AddUpdateRemoveKeyEventArgs(newType.ID, AddUpdateRemoveKeyEventArgs.Action.ADD));

                return newType;
            }
        }

        internal override StructureTypeObj Update(StructureTypeObj updateObj)
        {
            StructureTypeObj obj = IDToObject[updateObj.ID];

            //Remove ourselves from the root list if we have a ParentID
            if (!obj.ParentID.HasValue)
            {
                if(rootStructureTypes.Contains(obj))
                    rootStructureTypes.Remove(obj);
            }
            else
            {
                //Remove ourselves from our parent object if needed
                if (obj.ParentID != updateObj.ParentID)
                    obj.Parent = null;
            }

            //Update if the new DB object has a later modified date. 
            obj.Synch(updateObj.GetData());

            //Add ourselves from the root list if we do not have a ParentID
            if (!obj.ParentID.HasValue)
            {
                rootStructureTypes.Add(obj);
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

        internal override void Remove(long ID)
        {
            StructureTypeObj obj = null;
            bool Success = IDToObject.TryRemove(ID, out obj);

            if (Success && obj != null)
            {
                //Let consumers know this key is about to go away
                //Before concurrent collections we didn't remove from IDToOBject until the event was fired
                CallOnAddUpdateRemoveKey(obj, new AddUpdateRemoveKeyEventArgs(ID, AddUpdateRemoveKeyEventArgs.Action.REMOVE));

                if (obj.ParentID.HasValue == false && rootStructureTypes.Contains(obj))
                    rootStructureTypes.Remove(obj);
            }
            else
            {
                System.Diagnostics.Trace.WriteLine("StructureTypeStore Remove Object found no object to remove", "WebAnnotation"); 
            }
        }

        /// <summary>
        /// At startup we load the entire structure types table since it is fairly static
        /// </summary>
        public void LoadStructureTypes()
        {
            StructureType[] types  = new StructureType[0]; 
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
                if(proxy != null)
                    proxy.Close();
            }
            //Populate our cache
            //List<StructureTypeObj> objList = new List<StructureTypeObj>(types.Length); 
            //Dictionary<long, StructureType> TypesTable = new Dictionary<long, StructureType>(types.Length);
            for (int i = 0; i < types.Length; i++)
            {
                StructureTypeObj typeObj = new StructureTypeObj(types[i]);
                //Do this first so we don't ask the server for parents we've already downloaded
                //IDToObject.Add(typeObj.ID, typeObj);
                
                //objList.Add(typeObj); 
                Add(typeObj, false); 
            }

            //Populate Parent/Child relationships

            /*
            foreach (StructureTypeObj typeObj in objList)
            {
                Add(typeObj); 
            }
             */
        }
    }
}
