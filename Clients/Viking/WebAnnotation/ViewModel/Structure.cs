using System;
using System.ComponentModel; 
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common.UI; 
using Viking.Common;
using System.Windows.Forms;
using System.Drawing;
using WebAnnotationModel;

namespace WebAnnotation.ViewModel
{
    public class Structure : Viking.Objects.UIObjBase, IEquatable<Structure>, IEqualityComparer<Structure>
    {
        public StructureObj modelObj;

        public override string ToString()
        {
            return modelObj.ToString();
        }

        public override int GetHashCode()
        {
            return modelObj.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            Structure Obj = obj as Structure;
            if (Obj != null)
            {
                return modelObj.Equals(Obj.modelObj);
            }

            StructureObj Obj2 = obj as StructureObj;
            if (Obj2 != null)
            {
                return modelObj.Equals(Obj2);
            }

            return false;
        }

        public Structure(StructureObj data)
        {
            this.modelObj = data; 
        }

        public Structure Parent
        {
            get
            {
                if (modelObj.Parent == null)
                    return null;

                return new Structure(modelObj.Parent);
            }
        }

        [Column("Label")]
        public string InfoLabel
        {
            get { return modelObj.Label; }
            set
            {
                modelObj.Label = value;
            }
        }
        
//        [Column("ID")] This is covered by the ToString method in UI's
        public long ID
        {
            get { return modelObj.ID; }
        }

        [Column("ParentID")]
        public long? ParentID
        {

            get { return modelObj.ParentID; }
        }

        [Column("Last Editor")]
        public string Username
        {
            get { return modelObj.Username; }
        }

        [Column("Num Links")]
        public int NumLinks
        {
            get { return modelObj.NumLinks; }
        }


        [Column("Verified")]
        public bool Verified
        {
            get { return modelObj.Verified; }
            set
            {
                modelObj.Verified = value;
            }
        }

        [Column("Confidence")]
        public double Confidence
        {
            get { return modelObj.Confidence; }
            set
            {
                modelObj.Confidence = value;
            }
        }

        [Column("Attributes")]
        public IEnumerable<ObjAttribute> Attributes
        {
            get { return modelObj.Attributes; }
            set
            {
                modelObj.Attributes = new List<ObjAttribute>(value);
            }
        }

        [Column("Notes")]
        public string Notes
        {
            get { return modelObj.Notes; }
            set
            {
                modelObj.Notes = value;
            }
        }
        
        [Column("Type")]
        public StructureType Type
        {
            get
            {
                return new StructureType(modelObj.Type); 
            }
        }
        
        /// <summary>
        /// Add the specified name to the attributes if it does not exists, removes it 
        /// </summary>
        /// <param name="tag"></param>
        public void ToggleAttribute(string tag)
        {
            ObjAttribute attrib = new ObjAttribute(tag, null);
            List<ObjAttribute> listAttributes = this.Attributes.ToList();
            if(listAttributes.Contains(attrib))
            {
                listAttributes.Remove(attrib); 
            }
            else
            {
                listAttributes.Add(attrib);  
            }

            this.Attributes = listAttributes;
        }

        public LocationObj Center
        {
            get
            {
                LocationObj[] locations = Store.Locations.GetLocationsForStructure(ID).ToArray<LocationObj>();
                
                if (locations != null)
                {
                    double sumX = 0;
                    double sumY = 0;
                    double sumZ = 0;
                    double sumRadiusSquared = 0; 
                    foreach (LocationObj loc in locations)
                    {
                        double RadiusSquared = loc.Radius * loc.Radius;
                        sumX += loc.VolumePosition.X * RadiusSquared;
                        sumY += loc.VolumePosition.Y * RadiusSquared;
                        sumZ += loc.Z * RadiusSquared;
                        sumRadiusSquared += RadiusSquared; 
                    }

                    sumX /= sumRadiusSquared;
                    sumY /= sumRadiusSquared;
                    sumZ /= sumRadiusSquared; 

                    double meanX = (sumX ) * Global.Scale.X;
                    double meanY = (sumY) * Global.Scale.Y;
                    double meanZ = (sumZ ) * Global.Scale.Z;

                    Geometry.GridVector3 MeanPosition = new Geometry.GridVector3(meanX, meanY, meanZ); 

                    //Find the location closest to the mean position
                    double minDistance = double.MaxValue;
                    int iClosest = 0; 
                    for(int iLoc = 0; iLoc < locations.Length; iLoc++)
                    {
                        Geometry.GridVector3 locPosition = new Geometry.GridVector3(locations[iLoc].VolumePosition.X  * Global.Scale.X,
                                                                                    locations[iLoc].VolumePosition.Y  * Global.Scale.Y,
                                                                                    locations[iLoc].Z * Global.Scale.Z);

                        double distance = Geometry.GridVector3.Distance(MeanPosition, locPosition);
                        if (distance < minDistance)
                        {
                            iClosest = iLoc;
                            minDistance = distance; 
                        }
                    }

                    return locations[iClosest];
                }

                return null; 
            }
        }

        #region IUIObject Members : IUIObject

        public new event PropertyChangedEventHandler ValueChanged
        {
            add { modelObj.PropertyChanged += value; }
            remove { modelObj.PropertyChanged -= value; }
        }

        public override ContextMenu ContextMenu
        {
            get
            {
                ContextMenu menu = new ContextMenu();
                if(Global.Export != null)
                {
                    menu.MenuItems.Add("Export Morphology To Tulip", ContextMenu_OnMorphology); 
                }

                menu.MenuItems.Add("Properties", ContextMenu_OnProperties);
                menu.MenuItems.Add("");
                menu.MenuItems.Add("Delete", ContextMenu_OnDelete);

                return menu;
            }
        }
        
        public override void Save()
        {
            Store.Structures.Save();
        }

        public override Viking.UI.Controls.GenericTreeNode CreateNode()
        {
            return new Viking.UI.Controls.GenericTreeNode(this);
        }

        public override Type[] AssignableParentTypes
        {
            get { return new System.Type[] { typeof(StructureObj) }; }
        }

        public long[] UnfinishedBranches()
        {
            return Store.Structures.GetUnfinishedBranches(this.ID);
        }

        #endregion

        protected void ContextMenu_OnMorphology(object sender, EventArgs e)
        {
            Global.Export.OpenMorphology(this.ID);
        }


        protected void ContextMenu_OnProperties(object sender, EventArgs e)
        {
            Viking.UI.Forms.PropertySheetForm.Show(this);
        }

        protected void ContextMenu_OnDelete(object sender, EventArgs e)
        {
            Delete();
        }

        public ContextMenu ContextMenu_AddUnverifiedBranchTerminals(ContextMenu menu)
        { 
            MenuItem menuUnverifiedBranchTerminals = new MenuItem("Unmarked process terminals");
            menuUnverifiedBranchTerminals.MenuItems.Add(new MenuItem());
            menuUnverifiedBranchTerminals.Select += this.OnSelectUnverifiedBranchTerminals;
            menu.MenuItems.Add(menuUnverifiedBranchTerminals);


            return menu;
        }

        private void OnSelectUnverifiedBranchTerminals(object sender, EventArgs e)
        {
            MenuItem menuUnverifiedBranchTerminals = sender as MenuItem;
            menuUnverifiedBranchTerminals.MenuItems.Clear();
            menuUnverifiedBranchTerminals.Select -= this.OnSelectUnverifiedBranchTerminals;
            bool HasMenuItems = _PopulateUnverifiedBranchTerminalsContextMenu(menuUnverifiedBranchTerminals);

            menuUnverifiedBranchTerminals.Enabled = HasMenuItems;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rootMenuItem"></param>
        /// <returns>True if the menu was populated, otherwise false.</returns>
        protected bool _PopulateUnverifiedBranchTerminalsContextMenu(MenuItem rootMenuItem)
        {
//            long[] Loc_Ids = Store.Structures.GetUnfinishedBranches(this.ID);
//            List<LocationObj> listLocations = Store.Locations.GetObjectsByIDs(Loc_Ids, true);

            WebAnnotationModel.Service.LocationPositionOnly[] LocationArray = Store.Structures.GetUnfinishedBranchesWithPosition(this.ID);

            Dictionary<double, List<WebAnnotationModel.Service.LocationPositionOnly>> dictSectionToLocations = this.MapLocationsToSections(LocationArray);

            List<double> levels = new List<double>(dictSectionToLocations.Keys);
            levels.Sort();
            foreach (double level in levels)
            {
                MenuItem levelMenus = BuildContextMenusForLevel((long)level, dictSectionToLocations[level]);
                rootMenuItem.MenuItems.Add(levelMenus);
            }

            return levels.Count > 0;
        }

        private string _LocationToString(WebAnnotationModel.Service.LocationPositionOnly loc)
        {
            return "Radius: " + loc.Radius.ToString("F1") + " X: " + loc.Position.X.ToString("F0") + " Y: " + loc.Position.Y.ToString("F0");
        }

        private MenuItem BuildContextMenusForLevel(long level, List<WebAnnotationModel.Service.LocationPositionOnly> listObjs)
        {
            MenuItem rootMenuItem = null;
            if (listObjs.Count == 1)
            {
                WebAnnotationModel.Service.LocationPositionOnly locObj = listObjs[0];
                //For a single item do not create a submenu
                string locString = _LocationToString(locObj);
                rootMenuItem = new MenuItem(level.ToString("D4") + " - " + locString, ContextMenu_SelectUnbranchedLocation);
                rootMenuItem.Tag = locObj.ID;
            }
            else
            {
                rootMenuItem = new MenuItem(level.ToString("D4"));
                foreach (WebAnnotationModel.Service.LocationPositionOnly locObj in listObjs)
                {
                    string locString = _LocationToString(locObj);
                    MenuItem subItem = new MenuItem(locString, ContextMenu_SelectUnbranchedLocation);
                    subItem.Tag = locObj.ID;
                    rootMenuItem.MenuItems.Add(subItem);
                }
            }

            return rootMenuItem;
        }

        private Dictionary<double, List<WebAnnotationModel.Service.LocationPositionOnly>> MapLocationsToSections(IEnumerable<WebAnnotationModel.Service.LocationPositionOnly> locations)
        {
            Dictionary<double, List<WebAnnotationModel.Service.LocationPositionOnly>> dictSectionToLocations = new Dictionary<double, List<WebAnnotationModel.Service.LocationPositionOnly>>();
            foreach (WebAnnotationModel.Service.LocationPositionOnly loc in locations)
            {
                if(!dictSectionToLocations.ContainsKey(loc.Position.Z))
                {
                    dictSectionToLocations[loc.Position.Z] = new List<WebAnnotationModel.Service.LocationPositionOnly>();
                }

                dictSectionToLocations[loc.Position.Z].Add(loc);
            }

            return dictSectionToLocations;
        }

        protected void ContextMenu_SelectUnbranchedLocation(object sender, EventArgs e)
        {
            MenuItem menu = sender as MenuItem;
            long locationID = (long)menu.Tag;

            LocationObj loc = Store.Locations.GetObjectByID(locationID);

            AnnotationOverlay.GoToLocation(loc); 
        }

        public override void Delete()
        {
            Store.Structures.Remove(modelObj);

            /*
            Structure OriginalParent = this.Parent;
            this.Parent = null;

            DBACTION originalAction = this.DBAction;
            this.DBAction = DBACTION.DELETE;

            bool success = Store.Structures.Save();
            if (!success)
            {
                //Write straight to data since we have an assert to check whether an object is being deleted, but
                //in this case we know it is ok
                this.Data.DBAction = originalAction;
                this.Parent = OriginalParent;
            }
            */
        }

        bool IEquatable<Structure>.Equals(Structure other)
        {
            return this.modelObj.ID == other.modelObj.ID; 
        }

        public bool Equals(Structure x, Structure y)
        {
            return x.modelObj.ID == y.modelObj.ID; 
        }

        public int GetHashCode(Structure obj)
        {
            return obj.modelObj.GetHashCode(); 
        }
    }
}
