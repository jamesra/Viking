using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using Viking.Common;
using Viking.Common.UI;
using WebAnnotationModel;

namespace WebAnnotation.ViewModel
{
    public class Structure : Viking.Objects.UIObjBase, IEquatable<Structure>, IEqualityComparer<Structure>, IContextMenu
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
            modelObj = data;
        }

        public Structure Parent
        {
            get
            {
                if (modelObj.Parent == null)
                {
                    return null;
                }

                return new Structure(modelObj.Parent);
            }
        }

        [Column("Label")]
        public string InfoLabel
        {
            get => modelObj.Label;
            set => modelObj.Label = value;
        }

        //        [Column("ID")] This is covered by the ToString method in UI's
        public long ID => modelObj.ID;

        [Column("ParentID")]
        public long? ParentID => modelObj.ParentID;

        [Column("Last Editor")]
        public string Username => modelObj.Username;

        [Column("Num Links")]
        public int NumLinks => modelObj.NumLinks;


        [Column("Verified")]
        public bool Verified
        {
            get => modelObj.Verified;
            set => modelObj.Verified = value;
        }

        [Column("Confidence")]
        public double Confidence
        {
            get => modelObj.Confidence;
            set => modelObj.Confidence = value;
        }

        [Column("Attributes")]
        public IEnumerable<ObjAttribute> Attributes
        {
            get => modelObj.Attributes;
            set => modelObj.Attributes = new List<ObjAttribute>(value);
        }

        [Column("Notes")]
        public string Notes
        {
            get => modelObj.Notes;
            set => modelObj.Notes = value;
        }

        [Column("Type")]
        public StructureType Type => new StructureType(modelObj.Type);



        public static void ToggleAttribute(StructureObj structObj, string tag)
        {
            ObjAttribute attrib = new ObjAttribute(tag, null);
            List<ObjAttribute> listAttributes = structObj.Attributes.ToList();
            if (listAttributes.Contains(attrib))
            {
                listAttributes.Remove(attrib);
            }
            else
            {
                listAttributes.Add(attrib);
            }

            structObj.Attributes = listAttributes;
        }

        public LocationObj Center
        {
            get
            {
                LocationObj[] locations = Store.Locations.GetLocationsForStructure(ID).ToArray<LocationObj>();

                if (locations != null && locations.Length > 0)
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

                    double meanX = (sumX) * Global.Scale.X;
                    double meanY = (sumY) * Global.Scale.Y;
                    double meanZ = (sumZ) * Global.Scale.Z;

                    Geometry.GridVector3 MeanPosition = new Geometry.GridVector3(meanX, meanY, meanZ);

                    //Find the location closest to the mean position
                    double minDistance = double.MaxValue;
                    int iClosest = 0;
                    for (int iLoc = 0; iLoc < locations.Length; iLoc++)
                    {
                        Geometry.GridVector3 locPosition = new Geometry.GridVector3(locations[iLoc].VolumePosition.X * Global.Scale.X,
                                                                                    locations[iLoc].VolumePosition.Y * Global.Scale.Y,
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
            add => modelObj.PropertyChanged += value;
            remove => modelObj.PropertyChanged -= value;
        }

        public override ContextMenuStrip ContextMenu
        {
            get
            {
                ContextMenuStrip menu = new ContextMenuStrip();
                if (Global.Export != null)
                {
                    var exportItem = new ToolStripMenuItem("Export Morphology To Tulip");
                    exportItem.Click += ContextMenu_OnMorphology;
                    menu.Items.Add(exportItem);
                }

                var propertiesItem = new ToolStripMenuItem("Properties");
                propertiesItem.Click += ContextMenu_OnProperties;
                menu.Items.Add(propertiesItem);
                menu.Items.Add(new ToolStripSeparator());
                var deleteItem = new ToolStripMenuItem("Delete");
                deleteItem.Click += ContextMenu_OnDelete;
                menu.Items.Add(deleteItem);

                return menu;
            }
        }

        public override void Save()
        {
            try
            {
                Store.Structures.Save();
            }
            catch (System.ServiceModel.FaultException e)
            {
                AnnotationOverlay.ShowFaultExceptionMsgBox(e);
            }

        }

        public override Viking.UI.Controls.GenericTreeNode CreateNode()
        {
            return new Viking.UI.Controls.GenericTreeNode(this);
        }

        public override Type[] AssignableParentTypes => new System.Type[] { typeof(StructureObj) };

        public long[] UnfinishedBranches()
        {
            return Store.Structures.GetUnfinishedBranches(ID);
        }

        #endregion

        protected void ContextMenu_OnMorphology(object sender, EventArgs e)
        {
            Global.Export.OpenMorphology(ID);
        }


        protected void ContextMenu_OnProperties(object sender, EventArgs e)
        {
            Viking.UI.Forms.PropertySheetForm.Show(this);
        }

        protected void ContextMenu_OnDelete(object sender, EventArgs e)
        {
            Delete();
        }

        public ContextMenuStrip ContextMenu_AddUnverifiedBranchTerminals(ContextMenuStrip menu)
        {
            ToolStripMenuItem menuUnverifiedBranchTerminals = new ToolStripMenuItem("Unmarked process terminals");
            menuUnverifiedBranchTerminals.DropDownOpening += OnDropDownOpeningUnverifiedBranchTerminals;
            menu.Items.Add(menuUnverifiedBranchTerminals);


            return menu;
        }

        private void OnDropDownOpeningUnverifiedBranchTerminals(object sender, EventArgs e)
        {
            ToolStripMenuItem menuUnverifiedBranchTerminals = sender as ToolStripMenuItem;
            menuUnverifiedBranchTerminals.DropDownItems.Clear();
            bool HasMenuItems = _PopulateUnverifiedBranchTerminalsContextMenu(menuUnverifiedBranchTerminals);

            menuUnverifiedBranchTerminals.Enabled = HasMenuItems;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rootMenuItem"></param>
        /// <returns>True if the menu was populated, otherwise false.</returns>
        protected bool _PopulateUnverifiedBranchTerminalsContextMenu(ToolStripMenuItem rootMenuItem)
        {
            //            long[] Loc_Ids = Store.Structures.GetUnfinishedBranches(this.ID);
            //            List<LocationObj> listLocations = Store.Locations.GetObjectsByIDs(Loc_Ids, true);

            AnnotationService.Types.LocationPositionOnly[] LocationArray = Store.Structures.GetUnfinishedBranchesWithPosition(ID);

            Dictionary<double, List<AnnotationService.Types.LocationPositionOnly>> dictSectionToLocations = MapLocationsToSections(LocationArray);

            List<double> levels = new List<double>(dictSectionToLocations.Keys);
            levels.Sort();
            foreach (double level in levels)
            {
                ToolStripMenuItem levelMenus = BuildContextMenusForLevel((long)level, dictSectionToLocations[level]);
                rootMenuItem.DropDownItems.Add(levelMenus);
            }

            return levels.Count > 0;
        }

        private string _LocationToString(AnnotationService.Types.LocationPositionOnly loc)
        {
            return "Radius: " + loc.Radius.ToString("F1") + " X: " + loc.Position.X.ToString("F0") + " Y: " + loc.Position.Y.ToString("F0");
        }

        private ToolStripMenuItem BuildContextMenusForLevel(long level, List<AnnotationService.Types.LocationPositionOnly> listObjs)
        {
            ToolStripMenuItem rootMenuItem = null;
            if (listObjs.Count == 1)
            {
                AnnotationService.Types.LocationPositionOnly locObj = listObjs[0];
                //For a single item do not create a submenu
                string locString = _LocationToString(locObj);
                rootMenuItem = new ToolStripMenuItem(level.ToString("D4") + " - " + locString);
                rootMenuItem.Tag = locObj.ID;
                rootMenuItem.Click += ContextMenu_SelectUnbranchedLocation;
            }
            else
            {
                rootMenuItem = new ToolStripMenuItem(level.ToString("D4"));
                foreach (AnnotationService.Types.LocationPositionOnly locObj in listObjs)
                {
                    string locString = _LocationToString(locObj);
                    ToolStripMenuItem subItem = new ToolStripMenuItem(locString);
                    subItem.Tag = locObj.ID;
                    subItem.Click += ContextMenu_SelectUnbranchedLocation;
                    rootMenuItem.DropDownItems.Add(subItem);
                }
            }

            return rootMenuItem;
        }

        private Dictionary<double, List<AnnotationService.Types.LocationPositionOnly>> MapLocationsToSections(IEnumerable<AnnotationService.Types.LocationPositionOnly> locations)
        {
            Dictionary<double, List<AnnotationService.Types.LocationPositionOnly>> dictSectionToLocations = new Dictionary<double, List<AnnotationService.Types.LocationPositionOnly>>();
            foreach (AnnotationService.Types.LocationPositionOnly loc in locations)
            {
                if (!dictSectionToLocations.ContainsKey(loc.Position.Z))
                {
                    dictSectionToLocations[loc.Position.Z] = new List<AnnotationService.Types.LocationPositionOnly>();
                }

                dictSectionToLocations[loc.Position.Z].Add(loc);
            }

            return dictSectionToLocations;
        }

        protected void ContextMenu_SelectUnbranchedLocation(object sender, EventArgs e)
        {
            ToolStripMenuItem menu = sender as ToolStripMenuItem;
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
            return modelObj.ID == other.modelObj.ID;
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
