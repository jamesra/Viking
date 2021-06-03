using Geometry;
using Microsoft.Xna.Framework.Graphics;

namespace Viking.Common
{
    /// <summary>
    /// This interface can be placed on a class in an extension assembly.  If the Initialize
    /// method returns false the assembly will not be loaded as an extension
    /// </summary>
    public interface IInitExtensions
    {
        bool Initialize();
    }

    /// <summary>
    /// IPropertyPage is used to expose custom property pages to PlantMap's property sheets. Custom controls that wish
    /// to be shown should implement IPropertyPage and execute the RegisterPage.exe utility. 
    /// </summary>
    public interface ITabExtension
    {
        /// <summary>
        /// GetPage is called at program load before the overview page is shown. 
        /// </summary>
        System.Windows.Forms.TabPage GetPage();
    }

    public interface ISectionOverlayExtension
    {
        /// <summary>
        /// Name of the overlay for UI purposes
        /// </summary>
        /// <returns></returns>
        string Name();

        /// <summary>
        /// Used to sort all extensions to determine draw order
        /// </summary>
        /// <returns></returns>
        int DrawOrder();

        /// <summary>
        /// Must be called before draw
        /// </summary>
        /// <param name="parent"></param>
        void SetParent(Viking.UI.Controls.SectionViewerControl parent);

        /// <summary>
        /// The UI is being asked to select an object.  The extension should respond to this method with the object if it exists 
        /// and the distance to object.  Return null if no object can be selected at the given point
        /// </summary>
        /// <param name="WorldPosition"></param>
        /// <param name="distance"></param>
        /// <returns></returns>
        object ObjectAtPosition(GridVector2 WorldPosition, out double distance); 

        /// <summary>
        /// Draw the specified overlay extension on the render target.  
        /// </summary>
        /// <param name="graphicsDevice"></param>
        /// <param name="Bounds"></param>
        /// <param name="DownSample"></param>
        /// <param name="BackgroundLuma">Texture matching size of client with Luma value of each pixel</param>
        /// <param name="BackgroundColors">Texture matching size of client window with RGB values for each pixel.  May be null of no color data available</param>
        void Draw(GraphicsDevice graphicsDevice, VikingXNA.Scene scene, Texture BackgroundLuma, Texture BackgroundColors, ref int NextStencilValue);
    }

    /// <summary>
    /// Primary interface for extensions to listen in on User interface commands and react accordingly
    /// </summary>
    public interface ICommands
    {
    }

    public interface IMenuFactory
    {
        /// <summary>
        /// Extension should create the menu item as appropriate, or return NULL if not possible
        /// </summary>
        /// <returns></returns>
        System.Windows.Forms.ToolStripItem CreateMenuItem();
    }

    /// <summary>
    /// </summary>
    public interface IToolBarButtons
    {
        void AddButtons(System.Windows.Forms.Control ToolBar);
    }

    /// <summary>
    /// </summary>
    public interface IProvideContextMenus
    {
        /// <summary>
        /// GetMenuFor returns a context menu for the passed DataObject or null
        /// </summary>
        System.Windows.Forms.ContextMenu BuildMenuFor(IContextMenu Obj, System.Windows.Forms.ContextMenu Menu);

        /// <summary>
        /// GetMenuFor returns a context menu for the passed System.Type or null
        /// </summary>
        System.Windows.Forms.ContextMenu BuildMenuFor(System.Type ObjType, System.Windows.Forms.ContextMenu Menu);
    }

    /// <summary>
    /// IPropertyPage is used to expose custom property pages to property sheets. Custom controls that wish
    /// to be shown should implement IPropertyPage
    /// </summary>
    public interface IPropertyPage
    {
        /// <summary>
        /// InitPage is called before the property page is displayed, and before the Load event is fired.
        /// </summary>
        void InitPage();

        /// <summary>
        /// The ShowObject call specifies which object the property page should be displaying
        /// </summary>
        /// <param name="Object">The object to display</param>
        void ShowObject(object Object);


        /// <summary>
        /// Reset clears each control and inititalizes it with any default values
        /// </summary>
        void Reset();

        /// <summary>
        /// Enable enables or disables the property page
        /// </summary>
        void Enable(bool Enabled);

        /// <summary>
        /// GetPage is called after InitPage. If a Null reference is returned the page is not displayed. 
        /// This allows pages to filter thier usage depending on the contents of the DataRow they are passed.
        /// </summary>
        System.Windows.Forms.TabPage GetPage();

        /// <summary>
        /// OnValidateChanges is called before an OnSaveChanges call. If the property page determines that the save
        /// cannot take place it should return false. If OnValidateChanges returns true, the page is expected to 
        /// perform OnSaveChanges without error. 
        /// </summary>
        /// <returns></returns>
        bool OnValidateChanges();

        /// <summary>
        /// OnSaveChanges is called when the user presses either the 'Apply' or 'OK' button. Any changes that the
        /// property page wishes to persist to the DataRow should be done here. Changes made to the DataRow do not 
        /// need to be saved. DBObject.Save() is called after all pages have executed thier OnSaveChanges calls. 
        /// </summary>
        void OnSaveChanges();

        /// <summary>
        /// OnCancelChanges is called when the user presses the "Cancel" button. Unsaved
        /// changes to the DataRow will be performed by the PropertySheet framework. Any other corrections should be 
        /// performed here. 
        /// </summary>
        void OnCancelChanges();
    }

    public interface IContextMenu
    {
        System.Windows.Forms.ContextMenu ContextMenu { get; }
    }

    public interface IUIObjectBasic : IContextMenu
    {
        void ShowProperties();

        string ToolTip { get; }

        /// <summary>
        /// The object should persist its current state if it needs to
        /// </summary>
        void Save();
    }

    /// <summary>
    /// This interface must be implemented by objects that want to leverage the UI infrastructure of Viking
    /// </summary>
    public interface IUIObject : IUIObjectBasic
    {
        event System.ComponentModel.PropertyChangedEventHandler ValueChanged;
        event System.EventHandler BeforeDelete;
        event System.EventHandler AfterDelete;
        event System.EventHandler BeforeSave;
        event System.EventHandler AfterSave;

        System.Drawing.Image SmallThumbnail {get;}

        #region DragDrop

        /// <summary>
        /// This returns a list of objects which we can be added to as a child, used for drag/drop, treeview
        /// </summary>
        System.Type[] AssignableParentTypes { get; }

        /// <summary>
        /// Add ourselves to the passed parent as a child object
        /// </summary>
        /// <param name="parent"></param>
        void SetParent(IUIObject parent);

        #endregion

        #region Treeview specific

        /// <summary>
        /// Creates a node suitable for placing in a treeview
        /// </summary>
        /// <returns></returns>
        Viking.UI.Controls.GenericTreeNode CreateNode();

        int TreeImageIndex { get; }
        int TreeSelectedImageIndex { get; }

        event System.Collections.Specialized.NotifyCollectionChangedEventHandler ChildChanged;

        #endregion 
    }
}