using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace Viking.Common
{
    /// <summary>
    /// Supports adding new modules to extend the Viking UI
    /// </summary>
    public class ExtensionManager
    {
        private static List<System.Type> ExtensionTabList = new List<System.Type>();

        private static SortedDictionary<VikingExtensionAttribute, Assembly> ExtensionToAssemblyTable = new SortedDictionary<VikingExtensionAttribute, Assembly>();

        /// <summary>
        /// List of types that can extend the section viewer control
        /// </summary>
        private static List<System.Type> SectionOverlayList = new List<System.Type>();

        /// <summary>
        /// List of types that extend menu
        /// </summary>
        private static List<System.Type> SectionMenuList = new List<System.Type>();

        /// <summary>
        /// List of objects that can extend the context menu
        /// </summary>
        private static List<System.Type> ContextMenuProviderList = new List<System.Type>(); 

        /// <summary>
        /// This maps a system.type that the user would interact with, such as a structure to a list of commands that can operate on that type 
        /// </summary>
        private static Dictionary<System.Type, List<System.Type>> ObjectTypeToCommandTable = new Dictionary<System.Type, List<System.Type>>();

        static public Assembly[] GetExtensionAssemblies()
        {
            return ExtensionToAssemblyTable.Values.ToArray();
        }

        #region Property Pages
        /// <summary>
        /// Maps a system.type to a set of property pages
        /// </summary>
        private static Dictionary<System.Type, List<System.Type>> ObjectTypeToPropertyPageTable = new Dictionary<Type, List<Type>>(); 

        static public System.Type[] GetPropertyPages(object Obj)
        {
            System.Type ObjType = Obj.GetType();
            return GetPropertyPages(ObjType);
        }


        static public System.Type[] GetPropertyPages(System.Type ObjType)
        {
            List<Type> TypeArray = new List<Type>();

            //Ensure that we get all pages for both the object and types it inherits from
            while (ObjType != null && ObjType != typeof(object))
            {
                if(ObjectTypeToPropertyPageTable.ContainsKey(ObjType))
                    TypeArray.AddRange(ObjectTypeToPropertyPageTable[ObjType]);

                //Start next step in the loop
                ObjType = ObjType.BaseType;
            }

            // order our pages
            TypeArray.Sort(new MyTypeComparer());

            return TypeArray.ToArray();
        }

        #endregion

        #region Menus

        /// <summary>
        /// Expand the passed menu with the items known by the extension manager
        /// </summary>
        /// <param name="menu"></param>
        static public void AddMenuItems(System.Windows.Forms.MenuStrip menuStrip)
        {
            //Fetch the menu item methods
            foreach (System.Type T in SectionMenuList)
            {
                MenuAttribute[] Attribs = T.GetCustomAttributes(typeof(Viking.Common.MenuAttribute), true) as MenuAttribute[];
                if (Attribs == null || Attribs.Length == 0)
                {
                    continue;
                }

                System.Windows.Forms.ToolStripItem[] items = menuStrip.Items.Find(Attribs[0].ParentMenuName,false);
                System.Windows.Forms.ToolStripMenuItem ParentItem = null;
                if(items != null && items.Length > 0)
                {
                    ParentItem = items[0] as System.Windows.Forms.ToolStripMenuItem; 
                }

                IMenuFactory menuObj = Activator.CreateInstance(T) as IMenuFactory;
                if (menuObj != null)
                {
                    System.Windows.Forms.ToolStripItem ExtensionItem =  menuObj.CreateMenuItem();

                    ParentItem = ExtensionItem as System.Windows.Forms.ToolStripMenuItem;
                    if (ParentItem != null)
                    {
                        //Trying not to stomp user extension info if it exists
                        if (ParentItem.Tag == null)
                        {
                            ParentItem.Tag = T.ToString();
                        }

                        //Assign a name if the user did not
                        if (ParentItem.Text == null)
                        {
                            ParentItem.Text = Attribs[0].ParentMenuName; 
                        }
                    }

                    if(ExtensionItem != null)
                        menuStrip.Items.Add(ExtensionItem);
                }

                //Create a menu item if we haven't yet
                if (ParentItem == null)
                {
                    
                    ParentItem = new System.Windows.Forms.ToolStripMenuItem(Attribs[0].ParentMenuName);
                    menuStrip.Items.Add(ParentItem as System.Windows.Forms.ToolStripItem);
                }
                
                MethodInfo[] methods = T.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                for (int i = 0; i < methods.Length; i++)
                {
                    MenuItemAttribute[] ItemAttribs = methods[i].GetCustomAttributes(typeof(Viking.Common.MenuItemAttribute), true) as MenuItemAttribute[];
                    if (ItemAttribs == null || ItemAttribs.Length == 0)
                        continue;

                    MenuItemAttribute ItemAttribute = ItemAttribs[0];

                    System.Windows.Forms.ToolStripItem NewItem = ParentItem.DropDownItems.Add(ItemAttribute.LabelName);
                    NewItem.Tag = methods[i];
                    NewItem.Click += new EventHandler(ExtensionManager.ExtensionMenuItemCallback);
                }
            }            

        }

        static void ExtensionMenuItemCallback(object sender, EventArgs e)
        {
            System.Windows.Forms.ToolStripItem item = sender as System.Windows.Forms.ToolStripItem;
            if (item == null)
                return;

            MethodInfo method = item.Tag as MethodInfo;
            if (method == null)
                return;

            method.Invoke(null, new object[] { sender, e }); 
        }

        #endregion

        public static string[] ExtensionNames
        {
            get
            {
                List<string> Names = new List<string>(ExtensionToAssemblyTable.Keys.Count);
                foreach (VikingExtensionAttribute Extension in ExtensionToAssemblyTable.Keys)
                    Names.Add(Extension.Name);

                return Names.ToArray();
            }
        }

        public static string[] SectionOverlayNames
        {
            get
            {
                List<string> Names = new List<string>(ExtensionToAssemblyTable.Keys.Count);
                foreach (VikingExtensionAttribute Extension in ExtensionToAssemblyTable.Keys)
                    Names.Add(Extension.Name);

                return Names.ToArray();
            }

        }

        static ExtensionManager()
        {
            string AssemblyDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            AssemblyDir += System.IO.Path.DirectorySeparatorChar + "Modules";
            
            //Check our own assembly for extensions, then check the module directory if possible
            ExtensionManager.SectionOverlayList = new List<System.Type>();

            FindAssemblyExtensions(Assembly.GetExecutingAssembly());

            
            if (Directory.Exists(AssemblyDir) == false)
            {
                Trace.WriteLine("Unable to find extension directory", "ExtMan");
                return;
            }

            //Load files in module directory and all sub directories
            List<string> Files = RecursiveGetModules(AssemblyDir);

            //Check the assemblies we've already loaded for extensions. PlantMap.UI uses the extension mechanism.
            //TODO: Should the always present extensions in PlantMap.UI be moved to a basic extension assembly and this line removed?

            Dictionary<string, Module> LoadedModuleTable = GetLoadedModuleTable();

            foreach (string FileName in Files)
            {
                //Don't check this file if we already have it loaded. 
                if (LoadedModuleTable.ContainsKey(FileName))
                    continue;

                //Check the module attributes and see if it is an extension module.
                try
                {
                    Assembly A = Assembly.LoadFrom(FileName);

                    VikingExtensionAttribute Extension = GetAssemblyExtensionAttribute(A);
                    if (Extension == null)
                        continue;

                    Trace.WriteLine("Found extension: " + Extension.Name, "ExtMan");
                    Debug.Assert(ExtensionToAssemblyTable.ContainsKey(Extension) == false, Extension.Name + ":" + FileName + " Extension loaded twice!");

                    ExtensionToAssemblyTable.Add(Extension, A);
                }
                catch(System.BadImageFormatException e)
                {
                    Trace.WriteLine("Bad image format loading assembly " + FileName + ". This can be OK if it is a support assembly and not an extension module.  Otherwise it usually indicates loading a 64-bit DLL from a 32-bit process.");
                    continue;
                }
                
            }
        }

        static internal List<string> RecursiveGetModules(string root)
        {
            List<string> listFiles = new List<string>();

            listFiles.AddRange(Directory.GetFiles(root, "*.DLL"));

            string[] dirs = Directory.GetDirectories(root);

            foreach (string dir in dirs)
            {
                listFiles.AddRange(RecursiveGetModules(dir));
            }

            return listFiles;
        }

        internal static void LoadExtensions(IProgressReporter progressReporter)
        {
            //Put in an array so we can change the collection in the loop

            int extensionCount = 0;
            IEnumerable<VikingExtensionAttribute> extensions = ExtensionToAssemblyTable.Keys.ToArray();
            foreach (VikingExtensionAttribute Extension in extensions)
            {
                Assembly A = ExtensionToAssemblyTable[Extension];

                progressReporter.ReportProgress((int)((double)extensionCount / (double)ExtensionToAssemblyTable.Count), "Loading " + Extension.Name);

                //Before we agree to load an assembly we need to determine if it can initialize correctly
                bool canInit = CanAssemblyInitialize(A);
                if (canInit == false)
                {
                    //Remove assembly if it cannot initialize
                    ExtensionToAssemblyTable.Remove(Extension);
                    Trace.WriteLine("Unloading assembly due to initialization failure: " + Extension.ToString(), "ExtMan"); 
                    continue;
                }

                try
                { 
                    FindAssemblyExtensions(A);
                }
                catch(System.Reflection.ReflectionTypeLoadException e)
                {
                    Trace.WriteLine($"Unable to load {A.ToString()}.");
                    progressReporter.ReportProgress(100, $"Unable to load {A.ToString()}.");
                    foreach (var loaderException in e.LoaderExceptions)
                    {
                        Trace.WriteLine($"{loaderException.ToString()}");
                    }

                    //Remove assembly if it cannot initialize
                    ExtensionToAssemblyTable.Remove(Extension);

                    continue; 
                }
            }

            progressReporter.ReportProgress(100, "Extensions loading complete");
        }

        private static bool CanAssemblyInitialize(Assembly A)
        {
            bool OKToLoad = true; 
            System.Type[] types = null;
            try
            {
                types = A.GetExportedTypes();
            }
            catch (ReflectionTypeLoadException except)
            {
                VikingExtensionAttribute Extension = GetAssemblyExtensionAttribute(A);
                DialogResult result = MessageBox.Show("OK = Run Viking without the extension.\nCancel = Exit and throw exception with debug information.\n\nException:\n" + except.ToString(), "Could not load module: " + Extension.Name, MessageBoxButtons.OKCancel);

                if (result == DialogResult.OK)
                {
                    return false;
                }
                else
                {
                    throw;
                }
            }
            catch (System.TypeLoadException except)
            {
                VikingExtensionAttribute Extension = GetAssemblyExtensionAttribute(A);
                DialogResult result = MessageBox.Show("OK = Run Viking without the extension.\nCancel = Exit and throw exception with debug information.\n\nException:\n" + except.ToString(), "Could not load module: " + Extension.Name, MessageBoxButtons.OKCancel);

                if (result == DialogResult.OK)
                {
                    return false;
                }
                else
                {
                    throw;
                }
            }

            if (types == null || types.Length == 0)
                return false;

            foreach (System.Type type in types)
            {
                if (type.IsClass == false)
                    continue;

                System.Type interfaceType = type.GetInterface("Viking.Common.IInitExtensions");
                if (interfaceType == null)
                    continue;

                try
                {
                    Viking.Common.IInitExtensions InitObj = Activator.CreateInstance(type, new object[0]) as IInitExtensions;
                    OKToLoad = InitObj.Initialize();
                }
                catch (System.MissingMethodException except)
                {
                    VikingExtensionAttribute Extension = GetAssemblyExtensionAttribute(A);
                    DialogResult result = MessageBox.Show("OK = Run Viking without the extension.\nCancel = Exit and throw exception with debug information.\n\nIn the past this exception suggests there are duplicate .dll files accidentally shipped in both the Viking and Modules folders.\n\nException:\n" + except.ToString(), "Could not load module: " + Extension.Name, MessageBoxButtons.OKCancel);

                    if (result == DialogResult.OK)
                    {
                        return false;
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (System.TypeInitializationException except)
                {
                    var shownException = except.InnerException is null ? except : except.InnerException;
                    VikingExtensionAttribute Extension = GetAssemblyExtensionAttribute(A);
                    DialogResult result = MessageBox.Show("OK -> Run Viking without the extension.\nCancel -> Exit and throw exception with debug information.\n\nException:\n" + shownException.ToString(), "Could not load module: " + Extension.Name, MessageBoxButtons.OKCancel);

                    if (result == DialogResult.OK)
                    {
                        return false;
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (System.Exception except)
                {
                    VikingExtensionAttribute Extension = GetAssemblyExtensionAttribute(A);
                    DialogResult result = MessageBox.Show("OK -> Run Viking without the extension.\nCancel -> Exit and throw exception with debug information.\n\nException:\n" + except.ToString(), "Could not load module: " + Extension.Name, MessageBoxButtons.OKCancel);

                    if (result == DialogResult.OK)
                    {
                        return false;
                    }
                    else
                    {
                        throw;
                    }
                }

                if (OKToLoad == false)
                    return false;
            }

            return true; 
        }

        private static VikingExtensionAttribute GetAssemblyExtensionAttribute(Assembly A)
        {
            VikingExtensionAttribute[] Attribs = A.GetCustomAttributes(typeof(VikingExtensionAttribute), false) as VikingExtensionAttribute[];
            Debug.Assert(Attribs.Length < 2, A.FullName + " contained two AssemblyExtensionAttribues. Maximum number is one.");

            if (Attribs.Length != 1)
                return null;

            return Attribs[0];
        }

        private static Dictionary<string, Module> GetLoadedModuleTable()
        {
            Dictionary<string, Module> LoadedModuleTable = new Dictionary<string, Module>();
            Assembly[] LoadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly A in LoadedAssemblies)
            {
                foreach (Module M in A.GetModules(true))
                {
                    string FullyQualifiedName   = null;
                    try
                    {
                        FullyQualifiedName = M.FullyQualifiedName;
                    }
                    catch(ArgumentException e)
                    {
                        Trace.WriteLine("Could not generate FullyQualifiedName for M.ToString(), this is probably OK if it is generated code or a resource.");
                        continue;
                    }

                    if (LoadedModuleTable.ContainsKey(FullyQualifiedName))
                        continue;

                    LoadedModuleTable.Add(FullyQualifiedName, M);
                }
            }

            return LoadedModuleTable;
        }

        private static void FindAssemblyExtensions(Assembly Extension)
        {
            System.Type[] ExtensionTypes = Extension.GetTypes();
            foreach (System.Type T in ExtensionTypes)
            {
                /*Find Property Page extensions*/
                FindPropertyPages(T);

                /*Find Command extensions */
                FindCommands(T);

                /*Find Tab Extensions */
                FindTabExtensions(T);

                /*Find Menu Extensions */
                FindMenuExtensions(T);

                FindContextMenuExtensions(T); 

                /*Find Overview Tab extensions*/
                /*Find ContextMenu extensions*/
   //             FindExtensionInterfaces(Extension, T);

                FindExtensionOverlays(T); 
            }
        }

        /// <summary>
        /// Given a type, adds all Command attributes to the CommandTable
        /// </summary>
        /// <param name="T"></param>
        private static void FindCommands(System.Type T)
        {
            /*Find Command extensions*/
            CommandAttribute[] Attribs = T.GetCustomAttributes(typeof(CommandAttribute), true) as CommandAttribute[];
            if (Attribs != null && Attribs.Length > 0)
            {
                /*Add this type to the list for each table it supports*/
                foreach (CommandAttribute Attrib in Attribs)
                {
                    /*The list contains ArrayLists. If one already exists then reuse it.
                        * otherwise create a new one */

                    //The Default Command has a NULL Object Type
                    if (Attrib.ObjectType == null)
                        continue;

                    List<System.Type> List;
                    if (ObjectTypeToCommandTable.ContainsKey(Attrib.ObjectType))
                        List = ObjectTypeToCommandTable[Attrib.ObjectType];
                    else
                        List = new List<System.Type>();

                    List.Add(T);

                    ObjectTypeToCommandTable[Attrib.ObjectType] = List;
                }
            }
        }

        /// <summary>
        /// Given a type, adds all PropertyPage attributes to the OverviewTabTable
        /// </summary>
        /// <param name="T"></param>
        private static void FindPropertyPages(System.Type T)
        {
            /*Find Property Page extensions*/
            PropertyPageAttribute[] Attribs = T.GetCustomAttributes(typeof(Viking.Common.PropertyPageAttribute), true) as PropertyPageAttribute[];
            if (Attribs != null && Attribs.Length > 0)
            {
                /*Add this type to the list for each table it supports*/
                foreach (PropertyPageAttribute Attrib in Attribs)
                {
                    /*The list contains lists. If one already exists then reuse it.
                        * otherwise create a new one */
                    List<System.Type> List;
                    if (ObjectTypeToPropertyPageTable.ContainsKey(Attrib.targetType))
                        List = ObjectTypeToPropertyPageTable[Attrib.targetType];
                    else
                    {
                        List = new List<Type>();
                    }

                    List.Add(T);

                    ObjectTypeToPropertyPageTable[Attrib.targetType] = List;
                }
            }
        }

        /// <summary>
        /// Given a type, adds all PropertyPage attributes to the OverviewTabTable
        /// </summary>
        /// <param name="T"></param>
        private static void FindTabExtensions(System.Type T)
        {
            /*Find Property Page extensions*/
            ExtensionTabAttribute[] Attribs = T.GetCustomAttributes(typeof(Viking.Common.ExtensionTabAttribute), true) as ExtensionTabAttribute[];
            if (Attribs != null && Attribs.Length > 0)
            {
                ExtensionTabList.Add(T);
            }
        }

        /// <summary>
        /// Given a type, adds all PropertyPage attributes to the OverviewTabTable
        /// </summary>
        /// <param name="T"></param>
        private static void FindMenuExtensions(System.Type T)
        {
            /*Find Property Page extensions*/
            MenuAttribute[] Attribs = T.GetCustomAttributes(typeof(Viking.Common.MenuAttribute), true) as MenuAttribute[];
            if (Attribs != null && Attribs.Length > 0)
            {
                SectionMenuList.Add(T);
            }
        }

        /// <summary>
        /// Given a type, adds all PropertyPage attributes to the OverviewTabTable
        /// </summary>
        /// <param name="T"></param>
        private static void FindContextMenuExtensions(System.Type T)
        {
            /*Find Property Page extensions*/
            System.Type Interface = T.GetInterface((typeof(Viking.Common.IProvideContextMenus).ToString()));
            if (Interface != null)
            {
                ContextMenuProviderList.Add(T); 
            }
        }

        static public System.Type[] GetExtensionTabCategory(TABCATEGORY Cat)
        {
            List<Type> TabList = new List<Type>();
            foreach (System.Type T in ExtensionTabList)
            {
                ExtensionTabAttribute[] Attribs = T.GetCustomAttributes(typeof(Viking.Common.ExtensionTabAttribute), true) as ExtensionTabAttribute[];
                if (Attribs != null && Attribs.Length > 0)
                {
                    ExtensionTabAttribute Attrib = Attribs[0];

                    if (Attrib.Category == Cat)
                        TabList.Add(T);
                }
            }

            return TabList.ToArray();
        }


        /// <summary>
        /// Given a type, adds all ExtensionOverlay attributes to the OverviewTabTable
        /// </summary>
        /// <param name="T"></param>
        private static void FindExtensionOverlays(System.Type T)
        {
            

            /*Find Property Page extensions*/
            SectionOverlayAttribute[] Attribs = T.GetCustomAttributes(typeof(Viking.Common.SectionOverlayAttribute), true) as SectionOverlayAttribute[];
            if (Attribs != null && Attribs.Length > 0)
            {
                /*Add this type to the list for each table it supports*/
                /*
                foreach (SectionOverlayAttribute Attrib in Attribs)
                {
                    /*The list contains lists. If one already exists then reuse it.
                        * otherwise create a new one */
              //  }
                
                ExtensionManager.SectionOverlayList.Add(T);
            }
        }

        static public System.Type[] GetCommandsForType(System.Type ObjType)
        {
            List<System.Type> CommandTypeList = new List<System.Type>();

            //Ensure that we get all commands for both the object and types it inherits from
            while (ObjType != null)
            {
                if (ObjectTypeToCommandTable.ContainsKey(ObjType) == true)
                {
                    CommandTypeList.AddRange(ObjectTypeToCommandTable[ObjType]);
                }

                //Start next step in the loop
                ObjType = ObjType.BaseType;
            }

            return CommandTypeList.ToArray();
        }

        static private ISectionOverlayExtension[] _SectionOverlays = null;

        
        /// <summary>
        /// Returns null if CreateSectionOverlays or an empty array if there are no listeners
        /// </summary>
        static public ISectionOverlayExtension[] SectionOverlays
        {
            get
            {
                if (_SectionOverlays != null)
                    return _SectionOverlays.ToArray();

                return null; 
            }
        }

        static public ISectionOverlayExtension[] CreateSectionOverlays(Viking.UI.Controls.SectionViewerControl parent)
        {
            List<ISectionOverlayExtension> listOverlays = new List<ISectionOverlayExtension>(ExtensionManager.SectionOverlayList.Count);
            for (int i = 0; i < ExtensionManager.SectionOverlayList.Count; i++ )
            {
                System.Type ObjType = SectionOverlayList[i];
                try
                {
                    ISectionOverlayExtension OverlayObj = Activator.CreateInstance(ObjType, new object[0]) as ISectionOverlayExtension;
                    OverlayObj.SetParent(parent);
                    listOverlays.Add(OverlayObj); 
                }
                catch (Exception e)
                {
                    System.Windows.Forms.MessageBox.Show("Failed to create overlay: " + ObjType.ToString() + " Removing from overlay list. Exception: " + e.ToString(), "Error");
                    ExtensionManager.SectionOverlayList.RemoveAt(i);
                    i--;
                    throw;
                }
            }
             
            _SectionOverlays = listOverlays.OrderBy(s => s.DrawOrder()).Reverse().ToArray();
            return _SectionOverlays; 
       }

        static public Viking.Common.IProvideContextMenus[] CreateContextMenuProviders()
        {
            List<IProvideContextMenus> listProviders = new List<IProvideContextMenus>(ContextMenuProviderList.Count);

            for (int i = 0; i < ExtensionManager.ContextMenuProviderList.Count; i++)
            {
                System.Type ObjType = ExtensionManager.ContextMenuProviderList[i];
                try
                {
                    IProvideContextMenus OverlayObj = Activator.CreateInstance(ObjType, new object[0]) as IProvideContextMenus;
                    listProviders.Add(OverlayObj);
                }
                catch (Exception e)
                {
                    System.Windows.Forms.MessageBox.Show("Failed to create contect menu provider: " + ObjType.ToString() + " Removing from overlay list. Exception: " + e.ToString(), "Error");
                    ExtensionManager.ContextMenuProviderList.RemoveAt(i);
                    i--;
                    throw;
                }
            } 

            return listProviders.ToArray(); 
        }
    }

    

    /// <summary>
    /// used to sort property pages by there priority
    /// </summary>
    class MyTypeComparer : IComparer<System.Type> 
    {
        int IComparer<Type>.Compare(Type x, Type y)
        {
            PropertyPageAttribute attrib_x = Util.GetAttribute(x as Type, typeof(PropertyPageAttribute)) as PropertyPageAttribute;
            PropertyPageAttribute attrib_y = Util.GetAttribute(y as Type, typeof(PropertyPageAttribute)) as PropertyPageAttribute;

            return attrib_x.priority.CompareTo(attrib_y.priority);
        }
    }
}
