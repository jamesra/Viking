using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using Viking.DependencyInjection;

namespace Viking.Common
{
    /// <summary>
    /// Supports adding new modules to extend the Viking UI
    /// </summary>
    public class ExtensionManager
    {
        private static readonly List<System.Type> ExtensionTabList = [];

        private static readonly SortedDictionary<VikingExtensionAttribute, Assembly> ExtensionToAssemblyTable = [];

        /// <summary>
        /// List of types that can extend the section viewer control
        /// </summary>
        private static readonly List<System.Type> SectionOverlayList = [];

        /// <summary>
        /// List of types that extend menu
        /// </summary>
        private static readonly List<System.Type> SectionMenuList = [];

        /// <summary>
        /// List of objects that can extend the context menu
        /// </summary>
        private static readonly List<System.Type> ContextMenuProviderList = [];
        private static readonly List<System.Type> ModuleRegistrarTypes = [];
        private static readonly List<System.Type> ModuleInitializerTypes = [];
        private static readonly List<System.Type> LegacyInitializerTypes = [];

        /// <summary>
        /// This maps a system.type that the user would interact with, such as a structure to a list of commands that can operate on that type 
        /// </summary>
        private static readonly Dictionary<System.Type, List<System.Type>> ObjectTypeToCommandTable = [];

        public static Assembly[] GetExtensionAssemblies() => [.. ExtensionToAssemblyTable.Values];

        #region Property Pages
        /// <summary>
        /// Maps a system.type to a set of property pages
        /// </summary>
        private static readonly Dictionary<System.Type, List<System.Type>> ObjectTypeToPropertyPageTable = [];

        public static System.Type[] GetPropertyPages(object Obj)
        {
            System.Type ObjType = Obj.GetType();
            return GetPropertyPages(ObjType);
        }


        public static System.Type[] GetPropertyPages(System.Type ObjType)
        {
            List<Type> TypeArray = [];

            //Ensure that we get all pages for both the object and types it inherits from
            while (ObjType != null && ObjType != typeof(object))
            {
                if (ObjectTypeToPropertyPageTable.TryGetValue(ObjType, out var value))
                    TypeArray.AddRange(value);

                //Start next step in the loop
                ObjType = ObjType.BaseType;
            }

            // order our pages
            TypeArray.Sort(new MyTypeComparer());

            return [.. TypeArray];
        }

        #endregion

        #region Menus

        /// <summary>
        /// Expand the passed menu with the items known by the extension manager
        /// </summary>
        /// <param name="menu"></param>
        public static void AddMenuItems(System.Windows.Forms.MenuStrip menuStrip)
        {
            //Fetch the menu item methods
            foreach (System.Type T in SectionMenuList)
            {
                if (T.GetCustomAttributes(typeof(Viking.Common.MenuAttribute), true) is not MenuAttribute[] Attribs || Attribs.Length == 0)
                {
                    continue;
                }

                System.Windows.Forms.ToolStripItem[] items = menuStrip.Items.Find(Attribs[0].ParentMenuName, false);
                System.Windows.Forms.ToolStripMenuItem ParentItem = null;
                if (items != null && items.Length > 0)
                {
                    ParentItem = items[0] as System.Windows.Forms.ToolStripMenuItem;
                }

                if (Activator.CreateInstance(T) is IMenuFactory menuObj)
                {
                    System.Windows.Forms.ToolStripItem ExtensionItem = menuObj.CreateMenuItem();

                    ParentItem = ExtensionItem as System.Windows.Forms.ToolStripMenuItem;
                    if (ParentItem != null)
                    {
                        //Trying not to stomp user extension info if it exists
                        ParentItem.Tag ??= T.ToString();

                        //Assign a name if the user did not
                        ParentItem.Text ??= Attribs[0].ParentMenuName;
                    }

                    if (ExtensionItem != null)
                        menuStrip.Items.Add(ExtensionItem);
                }

                //Create a menu item if we haven't yet
                if (ParentItem is null)
                {

                    ParentItem = new System.Windows.Forms.ToolStripMenuItem(Attribs[0].ParentMenuName);
                    menuStrip.Items.Add(ParentItem as System.Windows.Forms.ToolStripItem);
                }

                MethodInfo[] methods = T.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                for (int i = 0; i < methods.Length; i++)
                {
                    if (methods[i].GetCustomAttributes(typeof(Viking.Common.MenuItemAttribute), true) is not MenuItemAttribute[] ItemAttribs || ItemAttribs.Length == 0)
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
            if (sender is not System.Windows.Forms.ToolStripItem item)
                return;

            if (item.Tag is not MethodInfo method)
                return;

            method.Invoke(null, [sender, e]);
        }

        #endregion

        public static string[] ExtensionNames
        {
            get
            {
                List<string> Names = new(ExtensionToAssemblyTable.Keys.Count);
                foreach (VikingExtensionAttribute Extension in ExtensionToAssemblyTable.Keys)
                    Names.Add(Extension.Name);

                return [.. Names];
            }
        }

        public static string[] SectionOverlayNames
        {
            get
            {
                List<string> Names = new(ExtensionToAssemblyTable.Keys.Count);
                foreach (VikingExtensionAttribute Extension in ExtensionToAssemblyTable.Keys)
                    Names.Add(Extension.Name);

                return [.. Names];
            }

        }

        static ExtensionManager()
        {
            string AssemblyDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            AssemblyDir += System.IO.Path.DirectorySeparatorChar + "Modules";

            // Add custom assembly resolver to handle dependencies in module directories
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

            //Check our own assembly for extensions, then check the module directory if possible
            ExtensionManager.SectionOverlayList = [];

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
                    // Check if the file is a valid .NET assembly
                    if (!IsDotNetAssembly(FileName))
                    {
                        Trace.WriteLine($"Skipping non-.NET assembly file: {FileName}", "ExtMan");
                        continue;
                    }

                    Assembly A = Assembly.LoadFrom(FileName);

                    VikingExtensionAttribute Extension = GetAssemblyExtensionAttribute(A);
                    if (Extension is null)
                    {
                        continue;
                    }

                    Trace.WriteLine($"Found extension: {Extension.Name} at {FileName}", "ExtMan");
                    Debug.Assert(ExtensionToAssemblyTable.ContainsKey(Extension) == false, Extension.Name + ":" + FileName + " Extension loaded twice!");

                    ExtensionToAssemblyTable.Add(Extension, A);
                }
                catch (System.BadImageFormatException e)
                {
                    Trace.WriteLine("Bad image format loading assembly " + FileName + ". This can be OK if it is a support assembly and not an extension module.  Otherwise it usually indicates loading a 64-bit DLL from a 32-bit process.");
                    continue;
                }
            }
        }

        private static bool IsDotNetAssembly(string filePath)
        {
            try
            {
                // Attempt to load the assembly name to check if it's a valid .NET assembly
                AssemblyName.GetAssemblyName(filePath);
                return true;
            }
            catch (BadImageFormatException)
            {
                // Not a valid .NET assembly
                return false;
            }
            catch (FileNotFoundException)
            {
                // File not found, treat as invalid
                return false;
            }
        }

        internal static List<string> RecursiveGetModules(string root)
        {
            List<string> listFiles = [.. Directory.GetFiles(root, "*.DLL")];

            string[] dirs = Directory.GetDirectories(root);

            foreach (string dir in dirs)
            {
                listFiles.AddRange(RecursiveGetModules(dir));
            }

            return listFiles;
        }

        /// <summary>
        /// Checks if an extension assembly should be loaded by looking for a static ShouldLoad method.
        /// If the method exists, it is called with the provided context. If it doesn't exist, returns true for backward compatibility.
        /// </summary>
        /// <param name="assembly">The extension assembly to check</param>
        /// <param name="context">The load context to pass to the ShouldLoad method</param>
        /// <returns>True if the extension should be loaded, false otherwise</returns>
        private static bool CheckExtensionShouldLoad(Assembly assembly, IExtensionLoadContext context)
        {
            if (assembly is null)
            {
                return true;
            }

            try
            {
                // Get all types in the assembly
                Type[] types = assembly.GetTypes();

                // Look for a static method named ShouldLoad with signature: bool ShouldLoad(IExtensionLoadContext)
                foreach (Type type in types)
                {
                    if (type.IsAbstract || type.IsInterface)
                    {
                        continue;
                    }

                    MethodInfo shouldLoadMethod = type.GetMethod("ShouldLoad",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                        null,
                        [typeof(IExtensionLoadContext)],
                        null);

                    if (shouldLoadMethod != null && shouldLoadMethod.ReturnType == typeof(bool))
                    {
                        try
                        {
                            object result = shouldLoadMethod.Invoke(null, [context]);
                            bool shouldLoad = (bool)result;

                            if (!shouldLoad)
                            {
                                Trace.WriteLine($"Extension {assembly.GetName().Name} indicated it should not load via ShouldLoad method", "ExtMan");
                            }

                            return shouldLoad;
                        }
                        catch (Exception ex)
                        {
                            Trace.WriteLine($"Error calling ShouldLoad method on {type.FullName}: {ex.Message}", "ExtMan");
                            // If there's an error calling the method, default to loading (fail-safe)
                            return true;
                        }
                    }
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                // Some types couldn't be loaded, but that's OK - we'll handle it in CanAssemblyInitialize
                Trace.WriteLine($"Some types could not be loaded from {assembly.GetName().Name} during ShouldLoad check: {ex.Message}", "ExtMan");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error checking ShouldLoad for {assembly.GetName().Name}: {ex.Message}", "ExtMan");
            }

            // No ShouldLoad method found - backward compatibility: load the extension
            return true;
        }

        internal static void LoadExtensions(IProgressReporter progressReporter)
        {
            //Put in an array so we can change the collection in the loop

            // Create the extension load context once for all extensions
            ExtensionLoadContext loadContext = null;
            try
            {
                Viking.ViewModels.VolumeViewModel volume = Viking.UI.State.volume;
                if (volume != null)
                {
                    loadContext = new ExtensionLoadContext(volume);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error creating ExtensionLoadContext: {ex.Message}", "ExtMan");
            }

            int extensionCount = 0;
            IEnumerable<VikingExtensionAttribute> extensions = [.. ExtensionToAssemblyTable.Keys];
            foreach (VikingExtensionAttribute Extension in extensions)
            {
                Assembly A = ExtensionToAssemblyTable[Extension];

                progressReporter.Report($"Loading {Extension.Name}", (int)((double)extensionCount / (double)ExtensionToAssemblyTable.Count), 100);

                // Check if extension wants to conditionally prevent loading
                bool shouldLoad = CheckExtensionShouldLoad(A, loadContext);
                if (!shouldLoad)
                {
                    //Remove assembly if it indicated it should not load
                    ExtensionToAssemblyTable.Remove(Extension);
                    Trace.WriteLine($"Extension {Extension.Name} indicated it should not load", "ExtMan");
                    continue;
                }

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
                catch (System.Reflection.ReflectionTypeLoadException e)
                {
                    Trace.WriteLine($"Unable to load {A}.");
                    progressReporter.Report($"Unable to load {A}.", 100, 100);
                    foreach (var loaderException in e.LoaderExceptions)
                    {
                        Trace.WriteLine($"{loaderException}");
                    }

                    //Remove assembly if it cannot initialize
                    ExtensionToAssemblyTable.Remove(Extension);

                    continue;
                }
            }

            progressReporter.Report("Extensions loading complete", 100, 100);
        }

        internal static void RegisterModuleServices(IServiceCollection services)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            foreach (var registrarType in ModuleRegistrarTypes)
            {
                try
                {
                    if (Activator.CreateInstance(registrarType) is IModuleServiceRegistrar registrar)
                    {
                        registrar.RegisterServices(services);
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Failed to register services for module {registrarType.FullName}: {ex}", "ExtMan");
                }
            }
        }

        internal static async Task InitializeModulesAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        {
            if (serviceProvider is null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            foreach (var initializerType in ModuleInitializerTypes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (Activator.CreateInstance(initializerType) is IModuleInitializer initializer)
                    {
                        var activeProvider = ServiceLocator.IsInitialized ? ServiceLocator.ServiceProvider : serviceProvider;
                        await initializer.InitializeAsync(activeProvider, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Failed to initialize module {initializerType.FullName}: {ex}", "ExtMan");
                }
            }

            foreach (var legacyType in LegacyInitializerTypes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (Activator.CreateInstance(legacyType) is IInitExtensions legacy)
                    {
                        var activeProvider = ServiceLocator.IsInitialized ? ServiceLocator.ServiceProvider : serviceProvider;
                        legacy.Initialize(activeProvider);
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Failed to initialize legacy module {legacyType.FullName}: {ex}", "ExtMan");
                }
            }
        }

        private static bool CanAssemblyInitialize(Assembly A)
        {
            try
            {
                _ = A.GetExportedTypes();
                return true;
            }
            catch (ReflectionTypeLoadException except)
            {
                return HandleAssemblyInitializationError(A, except, null);
            }
            catch (System.TypeLoadException except)
            {
                return HandleAssemblyInitializationError(A, except, null);
            }
            catch (System.AggregateException except)
            {
                // Check if this is a FileLoadException about strongly-named assemblies
                bool isStrongNameIssue = except.InnerExceptions.OfType<System.IO.FileLoadException>()
                    .Any(e => e.Message.Contains("strongly-named assembly") || e.HResult == 0x80131044);

                string? customMessage = isStrongNameIssue
                    ? "This extension requires a strongly-named assembly that is not available."
                    : null;

                return HandleAssemblyInitializationError(A, except, customMessage);
            }
            catch (System.IO.FileLoadException except)
            {
                return HandleAssemblyInitializationError(A, except, null);
            }
        }

        /// <summary>
        /// Handles assembly initialization errors with a consistent user dialog.
        /// </summary>
        /// <param name="assembly">The assembly that failed to initialize</param>
        /// <param name="exception">The exception that occurred</param>
        /// <param name="customMessage">Optional custom message to include in the error dialog</param>
        /// <returns>True if user chose to continue without the extension, false if user chose to exit</returns>
        private static bool HandleAssemblyInitializationError(Assembly assembly, Exception exception, string? customMessage)
        {
            VikingExtensionAttribute extension = GetAssemblyExtensionAttribute(assembly);
            string extensionName = extension?.Name ?? assembly.GetName().Name ?? "Unknown";

            string message = "OK = Run Viking without the extension.\nCancel = Exit and throw exception with debug information.";
            if (!string.IsNullOrWhiteSpace(customMessage))
            {
                message += $"\n\n{customMessage}";
            }
            message += $"\n\nException:\n{exception}";

            DialogResult result = MessageBox.Show(message, $"Could not load module: {extensionName}", MessageBoxButtons.OKCancel);

            if (result == DialogResult.OK)
            {
                return false;
            }
            else
            {
                //TODO: Exit the program
                throw new Exception("User elected to cancel Viking Launch");
            }
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
            Dictionary<string, Module> LoadedModuleTable = [];
            Assembly[] LoadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly A in LoadedAssemblies)
            {
                foreach (Module M in A.GetModules(true))
                {
                    string FullyQualifiedName = null;
                    try
                    {
                        FullyQualifiedName = M.FullyQualifiedName;
                    }
                    catch (ArgumentException e)
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
                RegisterModuleInterfaces(T);
            }
        }

        /// <summary>
        /// Given a type, adds all Command attributes to the CommandTable
        /// </summary>
        /// <param name="T"></param>
        private static void FindCommands(System.Type T)
        {
            /*Find Command extensions*/
            if (T.GetCustomAttributes(typeof(CommandAttribute), true) is CommandAttribute[] Attribs && Attribs.Length > 0)
            {
                /*Add this type to the list for each table it supports*/
                foreach (CommandAttribute Attrib in Attribs)
                {
                    /*The list contains ArrayLists. If one already exists then reuse it.
                        * otherwise create a new one */

                    //The Default Command has a NULL Object Type
                    if (Attrib.ObjectType is null)
                        continue;

                    List<Type> List = ObjectTypeToCommandTable.TryGetValue(Attrib.ObjectType, out var value) ? value : [];
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
            if (T.GetCustomAttributes(typeof(Viking.Common.PropertyPageAttribute), true) is PropertyPageAttribute[] Attribs && Attribs.Length > 0)
            {
                /*Add this type to the list for each table it supports*/
                foreach (PropertyPageAttribute Attrib in Attribs)
                {
                    Type resolvedTarget = Attrib.ResolveTargetType();
                    if (resolvedTarget is null)
                    {
                        Trace.WriteLine($"Skipping property page '{T}' because target type '{Attrib.TargetTypeName}' could not be resolved.", "ExtensionManager");
                        continue;
                    }

                    /*The list contains lists. If one already exists then reuse it.
                        * otherwise create a new one */
                    List<Type> List = ObjectTypeToPropertyPageTable.TryGetValue(resolvedTarget, out var value) ? value : [];
                    List.Add(T);

                    ObjectTypeToPropertyPageTable[resolvedTarget] = List;
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
            if (T.GetCustomAttributes(typeof(Viking.Common.ExtensionTabAttribute), true) is ExtensionTabAttribute[] Attribs && Attribs.Length > 0)
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
            if (T.GetCustomAttributes(typeof(Viking.Common.MenuAttribute), true) is MenuAttribute[] Attribs && Attribs.Length > 0)
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

        public static System.Type[] GetExtensionTabCategory(TABCATEGORY Cat)
        {
            List<Type> TabList = [];
            foreach (System.Type T in ExtensionTabList)
            {
                if (T.GetCustomAttributes(typeof(Viking.Common.ExtensionTabAttribute), true) is ExtensionTabAttribute[] Attribs && Attribs.Length > 0)
                {
                    ExtensionTabAttribute Attrib = Attribs[0];

                    if (Attrib.Category == Cat)
                        TabList.Add(T);
                }
            }

            return [.. TabList];
        }


        /// <summary>
        /// Given a type, adds all ExtensionOverlay attributes to the OverviewTabTable
        /// </summary>
        /// <param name="T"></param>
        private static void FindExtensionOverlays(System.Type T)
        {
            /*Find Property Page extensions*/
            if (T.GetCustomAttributes(typeof(Viking.Common.SectionOverlayAttribute), true) is SectionOverlayAttribute[] Attribs && Attribs.Length > 0)
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

        private static void RegisterModuleInterfaces(System.Type type)
        {
            if (type.IsInterface || type.IsAbstract)
            {
                return;
            }

            if (typeof(IModuleServiceRegistrar).IsAssignableFrom(type) && !ModuleRegistrarTypes.Contains(type))
            {
                ModuleRegistrarTypes.Add(type);
            }

            if (typeof(IModuleInitializer).IsAssignableFrom(type) && !ModuleInitializerTypes.Contains(type))
            {
                ModuleInitializerTypes.Add(type);
            }

            if (typeof(IInitExtensions).IsAssignableFrom(type) && !LegacyInitializerTypes.Contains(type))
            {
                LegacyInitializerTypes.Add(type);
            }
        }

        public static System.Type[] GetCommandsForType(System.Type ObjType)
        {
            List<System.Type> CommandTypeList = [];

            //Ensure that we get all commands for both the object and types it inherits from
            while (ObjType != null)
            {
                if (ObjectTypeToCommandTable.TryGetValue(ObjType, out var value))
                {
                    CommandTypeList.AddRange(value);
                }

                //Start next step in the loop
                ObjType = ObjType.BaseType;
            }

            return [.. CommandTypeList];
        }

        private static ISectionOverlayExtension[]? _SectionOverlays = null;


        /// <summary>
        /// Returns null if CreateSectionOverlays or an empty array if there are no listeners
        /// </summary>
        public static ISectionOverlayExtension[] SectionOverlays => _SectionOverlays?.ToArray();

        public static ISectionOverlayExtension[] CreateSectionOverlays(Viking.UI.Controls.SectionViewerControl parent)
        {
            List<ISectionOverlayExtension> listOverlays = new(ExtensionManager.SectionOverlayList.Count);
            for (int i = 0; i < ExtensionManager.SectionOverlayList.Count; i++)
            {
                System.Type ObjType = SectionOverlayList[i];
                try
                {
                    ISectionOverlayExtension OverlayObj = Activator.CreateInstance(ObjType, []) as ISectionOverlayExtension;
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

            _SectionOverlays = [.. listOverlays.OrderBy(s => s.DrawOrder()).Reverse()];
            return _SectionOverlays;
        }

        public static Viking.Common.IProvideContextMenus[] CreateContextMenuProviders()
        {
            List<IProvideContextMenus> listProviders = new(ContextMenuProviderList.Count);

            for (int i = 0; i < ExtensionManager.ContextMenuProviderList.Count; i++)
            {
                System.Type ObjType = ExtensionManager.ContextMenuProviderList[i];
                try
                {
                    IProvideContextMenus OverlayObj = Activator.CreateInstance(ObjType, []) as IProvideContextMenus;
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

            return [.. listProviders];
        }

        public static ContextMenuStrip CreateContextMenuFromProviders(object Obj, ContextMenuStrip Menu)
        {
            //Create a context menu for the object
            foreach (IProvideContextMenus Provider in ExtensionManager.CreateContextMenuProviders())
            {
                try
                {

                    ContextMenuStrip NewMenu = Provider.BuildMenuFor(Obj, Menu);
                }
                catch (NotImplementedException e)
                {
                    Trace.WriteLine($"Error creating context menu from provider {Provider.GetType().Name}: {e.Message}", "ExtMan");
                    continue; // Skip this provider if it fails
                }
            }
            return Menu;
        }

        /// <summary>
        /// Custom assembly resolver to handle dependencies in module directories
        /// </summary>
        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                // Extract the assembly name from the full name
                string assemblyName = new AssemblyName(args.Name).Name;
                string assemblyFileName = assemblyName + ".dll";

                // Get the main application directory
                string appDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string modulesDir = Path.Combine(appDir, "Modules");

                // First, try to find the assembly in the main application directory
                string assemblyPath = Path.Combine(appDir, assemblyFileName);
                if (File.Exists(assemblyPath))
                {
                    Trace.WriteLine($"Loading assembly from main directory: {assemblyPath}", "ExtMan");
                    return Assembly.LoadFrom(assemblyPath);
                }

                // Then, search recursively in all module subdirectories
                if (Directory.Exists(modulesDir))
                {
                    string[] moduleDirs = Directory.GetDirectories(modulesDir, "*", SearchOption.AllDirectories);
                    foreach (string moduleDir in moduleDirs)
                    {
                        assemblyPath = Path.Combine(moduleDir, assemblyFileName);
                        if (File.Exists(assemblyPath))
                        {
                            Trace.WriteLine($"Loading assembly from module directory: {assemblyPath}", "ExtMan");
                            return Assembly.LoadFrom(assemblyPath);
                        }
                    }
                }

                // Assembly not found
                Trace.WriteLine($"Could not resolve assembly: {args.Name}", "ExtMan");
                return null;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error in assembly resolver: {ex.Message}", "ExtMan");
                return null;
            }
        }
    }

    /// <summary>
    /// Provides context information to extensions during conditional loading checks
    /// </summary>
    internal class ExtensionLoadContext : IExtensionLoadContext
    {
        private readonly XDocument? _vikingXML;
        private readonly XElement? _volumeElement;
        private readonly Viking.ViewModels.VolumeViewModel? _volume;

        public ExtensionLoadContext(Viking.ViewModels.VolumeViewModel? volume)
        {
            _volume = volume;
            if (volume?.VolumeElement?.Document != null)
            {
                _vikingXML = volume.VolumeElement.Document;
                _volumeElement = volume.VolumeElement;
            }
        }

        public XDocument VikingXML => _vikingXML ?? throw new InvalidOperationException("VikingXML is not available");

        public XElement VolumeElement => _volumeElement ?? throw new InvalidOperationException("VolumeElement is not available");

        public string VolumeName
        {
            get
            {
                if (_volumeElement != null)
                {
                    var nameAttr = _volumeElement.Attribute("Name");
                    if (nameAttr != null)
                    {
                        return nameAttr.Value;
                    }
                }
                return _volume?.Name ?? string.Empty;
            }
        }

        public string VolumeHost => _volume?.Host ?? string.Empty;
    }

    /// <summary>
    /// used to sort property pages by there priority
    /// </summary>
    class MyTypeComparer : IComparer<System.Type>
    {
        int IComparer<Type>.Compare(Type x, Type y)
        {
            if (Util.GetAttribute(x, typeof(PropertyPageAttribute)) is not PropertyPageAttribute attrib_x || Util.GetAttribute(y, typeof(PropertyPageAttribute)) is not PropertyPageAttribute attrib_y)
                return 0;

            return attrib_x.Priority.CompareTo(attrib_y.Priority);
        }
    }
}
