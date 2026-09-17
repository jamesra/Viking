using Jotunn.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Jotunn.Controls
{
    /// <summary>
    /// Follow steps 1a or 1b and then 2 to use this custom control in a XAML file.
    ///
    /// Step 1a) Using this custom control in a XAML file that exists in the current project.
    /// Add this XmlNamespace attribute to the root element of the markup file where it is 
    /// to be used:
    ///
    ///     xmlns:MyNamespace="clr-namespace:Viking.Controls"
    ///
    ///
    /// Step 1b) Using this custom control in a XAML file that exists in a different project.
    /// Add this XmlNamespace attribute to the root element of the markup file where it is 
    /// to be used:
    ///
    ///     xmlns:MyNamespace="clr-namespace:Viking.Controls;assembly=Viking.Controls"
    ///
    /// You will also need to add a project reference from the project where the XAML file lives
    /// to this project and Rebuild to avoid compilation errors:
    ///
    ///     Right click on the target project in the Solution Explorer and
    ///     "Add Reference"->"Projects"->[Browse to and select this project]
    ///
    ///
    /// Step 2)
    /// Go ahead and use your control in the XAML file.
    ///
    ///     <MyNamespace:VirtualizingGrid/>
    ///
    /// </summary>
    public class VirtualizingGrid : VirtualizingPanel
    {
        public static readonly DependencyProperty CenterNumberProperty;
        /// <summary>
        /// The number assigned to the center of the grid
        /// </summary>
        public int CenterNumber
        {
            get { return (int)GetValue(VirtualizingGrid.CenterNumberProperty); }
            set { SetValue(VirtualizingGrid.CenterNumberProperty, value); }
        }
        
        public static readonly DependencyProperty NumRowsProperty;
        /// <summary>
        /// The number assigned to the center of the grid
        /// </summary>
        public int NumRows
        {
            get { return (int)GetValue(VirtualizingGrid.NumRowsProperty); }
            set { SetValue(VirtualizingGrid.NumRowsProperty, value); }
        }

        public static readonly DependencyProperty NumColsProperty;
        /// <summary>
        /// The number assigned to the center of the grid
        /// </summary>
        public int NumCols
        {
            get { return (int)GetValue(VirtualizingGrid.NumColsProperty); }
            set { SetValue(VirtualizingGrid.NumColsProperty, value); }
        }

        public static readonly DependencyProperty VisibleRegionProperty;

        public VisibleRegionInfo VisibleRegion
        {
            get { return (VisibleRegionInfo)GetValue(VirtualizingGrid.VisibleRegionProperty); }
            set { SetValue(VirtualizingGrid.VisibleRegionProperty, value); }
        }
                
        private static RoutedUICommand incrementCenterIndexCommand;
        private static RoutedUICommand decrementCenterIndexCommand;
        private static RoutedUICommand addRowCommand;
        private static RoutedUICommand removeRowCommand;
        private static RoutedUICommand addColumnCommand;
        private static RoutedUICommand removeColumnCommand;

        /// <summary>
        /// Increment the center number
        /// </summary>
        public static RoutedUICommand IncrementCommand
        {
            get { return incrementCenterIndexCommand; }
        }

        /// <summary>
        /// Decrement the center number
        /// </summary>
        public static RoutedUICommand DecrementCommand
        {
            get { return decrementCenterIndexCommand; }
        }

        /// <summary>
        /// Increment the center number
        /// </summary>
        public static RoutedUICommand AddRowCommand
        {
            get { return addRowCommand; }
        }

        /// <summary>
        /// Decrement the center number
        /// </summary>
        public static RoutedUICommand RemoveRowCommand
        {
            get { return removeRowCommand; }
        }

        /// <summary>
        /// Increment the center number
        /// </summary>
        public static RoutedUICommand AddColumnCommand
        {
            get { return addColumnCommand; }
        }

        /// <summary>
        /// Decrement the center number
        /// </summary>
        public static RoutedUICommand RemoveColumnCommand
        {
            get { return removeColumnCommand; }
        }

        double LastValidCellHeight; 
        double LastValidCellWidth;
        
        static VirtualizingGrid()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(VirtualizingGrid), new FrameworkPropertyMetadata(typeof(VirtualizingGrid)));

            VirtualizingGrid.CenterNumberProperty = DependencyProperty.Register("CenterNumber",
                                                                        typeof(int),
                                                                        typeof(VirtualizingGrid),
                                                                        new FrameworkPropertyMetadata(0,
                                                                            FrameworkPropertyMetadataOptions.AffectsRender |
                                                                            FrameworkPropertyMetadataOptions.AffectsArrange |
                                                                            FrameworkPropertyMetadataOptions.AffectsMeasure));

            VirtualizingGrid.NumRowsProperty = DependencyProperty.Register("NumRows",
                                                                        typeof(int),
                                                                        typeof(VirtualizingGrid),
                                                                        new FrameworkPropertyMetadata(1,
                                                                            FrameworkPropertyMetadataOptions.AffectsRender |
                                                                            FrameworkPropertyMetadataOptions.AffectsArrange |
                                                                            FrameworkPropertyMetadataOptions.AffectsMeasure,
                                                                            null,
                                                                            CoerceGridDimension));

            VirtualizingGrid.NumColsProperty = DependencyProperty.Register("NumCols",
                                                                        typeof(int),
                                                                        typeof(VirtualizingGrid),
                                                                        new FrameworkPropertyMetadata(1, 
                                                                            FrameworkPropertyMetadataOptions.AffectsRender |
                                                                            FrameworkPropertyMetadataOptions.AffectsArrange |
                                                                            FrameworkPropertyMetadataOptions.AffectsMeasure,
                                                                            null,
                                                                            CoerceGridDimension));

            VirtualizingGrid.VisibleRegionProperty = DependencyProperty.Register("VisibleRegion",
                                                                        typeof(VisibleRegionInfo),
                                                                        typeof(VirtualizingGrid),
                                                                        new FrameworkPropertyMetadata(new VisibleRegionInfo(0, 0, 10000, 10000, 256), 
                                                                            FrameworkPropertyMetadataOptions.AffectsRender
                                                                            | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

            incrementCenterIndexCommand = GlobalCommands.IncrementSectionNumber;
            decrementCenterIndexCommand = GlobalCommands.DecrementSectionNumber;
            addRowCommand = GlobalCommands.AddGridRowCommand;
            removeRowCommand = GlobalCommands.RemoveGridRowCommand;
            addColumnCommand = GlobalCommands.AddGridColumnCommand;
            removeColumnCommand = GlobalCommands.RemoveGridColumnCommand;

            CommandManager.RegisterClassCommandBinding(typeof(VirtualizingGrid), new CommandBinding(IncrementCommand, OnIncrementCommand));
            CommandManager.RegisterClassCommandBinding(typeof(VirtualizingGrid), new CommandBinding(DecrementCommand, OnDecrementCommand));
            CommandManager.RegisterClassCommandBinding(typeof(VirtualizingGrid), new CommandBinding(AddRowCommand, OnAddRowCommand));
            CommandManager.RegisterClassCommandBinding(typeof(VirtualizingGrid), new CommandBinding(RemoveRowCommand, OnRemoveRowCommand, CanExecuteRemoveRowCommand));
            CommandManager.RegisterClassCommandBinding(typeof(VirtualizingGrid), new CommandBinding(AddColumnCommand, OnAddColumnCommand));
            CommandManager.RegisterClassCommandBinding(typeof(VirtualizingGrid), new CommandBinding(RemoveColumnCommand, OnRemoveColumnCommand, CanExecuteRemoveColumnCommand));
        }

        protected void MouseLeftButtonDownClassHandler(object o, RoutedEventArgs e)
        {
            //Make sure the event reaches children if the Listbox contains us.  prevent the event from being eaten by the list box.
        }

        public VirtualizingGrid()
        {
        }
                 
        /*
        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            float multiplier = ((float)e.Delta / 120.0f);

            StepCameraDistance(multiplier);

            base.OnMouseWheel(e);
        }

        protected void StepCameraDistance(float multiplier)
        {
            double ds = VisibleRegion.Downsample;
            if (multiplier > 0)
                ds *= 0.86956521739130434782608695652174;
            else
                ds *= 1.15;

            if (ds < 0.25)
                ds = 0.25;

            double Aspect = LastValidCellHeight / LastValidCellWidth;
            double visWidth = LastValidCellWidth * ds;
            double visHeight = LastValidCellWidth * Aspect * ds;

            Rect visRect = new Rect(VisibleRegion.Center.X - (visWidth / 2),
                                    VisibleRegion.Center.Y - (visHeight / 2),
                                    visWidth,
                                    visHeight); 

            VisibleRegion = new VisibleRegionInfo(visRect, ds); 
        }

        MouseEventArgs OldMouseEventArgs;
        Point OldScreenPosition;
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (OldMouseEventArgs == null)
                {
                    OldMouseEventArgs = e;
                    OldScreenPosition = e.GetPosition(this);
                    return;
                }

                Point ScreenPosition = e.GetPosition(this);

                Point newPosition = VisibleRegion.Center;
                newPosition.X += (OldScreenPosition.X - ScreenPosition.X) * VisibleRegion.Downsample;
                newPosition.Y -= (OldScreenPosition.Y - ScreenPosition.Y) * VisibleRegion.Downsample;
                Size VisibleArea = new Size(LastValidCellWidth * VisibleRegion.Downsample, LastValidCellHeight * VisibleRegion.Downsample);
                VisibleRegion = new VisibleRegionInfo(newPosition, VisibleArea, VisibleRegion.Downsample);

                OldScreenPosition = ScreenPosition;

                OldMouseEventArgs = e;
            }

            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            OldMouseEventArgs = null;
            OldScreenPosition = new Point();

            base.OnMouseUp(e);
        }
        */
        public override void EndInit()
        {
            base.EndInit();

            InvalidateMeasure(); 
        }
        
        protected override Size MeasureOverride(Size availableSize)
        {
            int numCells = this.NumRows * this.NumCols;

            Trace.WriteLine("Measure Override: " + this.NumRows.ToString() + " x " + this.NumCols.ToString());

            int ifirstVisibleItemIndex = CenterNumber - (numCells / 2);
            int ilastVisibleItemIndex = CenterNumber + (numCells / 2);

            //If the first index is negative leave some grid cells empty:
            int GridCellOffset = 0;
            if (ifirstVisibleItemIndex < 0)
            {
                GridCellOffset = -ifirstVisibleItemIndex;
                ifirstVisibleItemIndex = 0;
            }

            var necessaryChildrenTouch = this.Children; 
            IItemContainerGenerator generator = this.ItemContainerGenerator;

            if (generator == null)
                return base.MeasureOverride(availableSize);

            // Map a child index to an item index by going through a generator position
            GeneratorPosition childGeneratorPos = new GeneratorPosition(Children.Count-1, 0);
            int indexOfLastItem = generator.IndexFromGeneratorPosition(childGeneratorPos);
            
            GeneratorPosition startPos = generator.GeneratorPositionFromIndex(ifirstVisibleItemIndex);
            using (generator.StartAt(startPos, GeneratorDirection.Forward, true))
            {
                int childIndex = 0;
                for (int itemIndex = ifirstVisibleItemIndex; itemIndex <= ilastVisibleItemIndex; itemIndex++, childIndex++)
                {
                    bool newlyRealized; 
                    UIElement child = generator.GenerateNext(out newlyRealized) as UIElement;
                    if (child == null)
                        break; 

                    if(newlyRealized)
                    {
                        if (itemIndex > indexOfLastItem)
                        {
                            base.AddInternalChild(child);
                        }
                        else
                        {
                            base.InsertInternalChild(childIndex, child);
                        }

                        
                        //Binding StatusXBinding = new Binding();
                        //StatusXBinding.Source = this;
                        //StatusXBinding.Path = new PropertyPath("Downsample");
                        //this.StatusX.SetBinding(TextBlock.TextProperty, StatusXBinding);

                        generator.PrepareItemContainer(child); 
                    }
                    else
                    {
                        //Advance the index until we find the correct child index for this object
                        while (childIndex < Children.Count && child != this.Children[childIndex])
                        {
                            childIndex++; 
                        }

                        // The child has already been created, let's be sure it's in the right spot
                        //Debug.Assert(child == this.Children[childIndex], "Wrong child was generated");
                    }

                    child.Measure(new Size(availableSize.Width / NumCols, availableSize.Height / NumRows));
                }
            }

            CleanUpItems(ifirstVisibleItemIndex, ilastVisibleItemIndex);
            TrimExtraChildren(generator, numCells); 

            return base.MeasureOverride(availableSize);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            //Tell each child where they go in the grid...
            int numCells = this.NumRows * this.NumCols;
            double CellWidth = finalSize.Width / NumCols;
            double CellHeight = finalSize.Height / NumRows;

            Trace.WriteLine("Arrange Override: " + this.NumRows.ToString() + " x " + this.NumCols.ToString());

            LastValidCellHeight = CellHeight; 
            LastValidCellWidth = CellWidth; 

            int iChild = 0;

            if (CenterNumber < (numCells / 2))
            {
                iChild = CenterNumber - (numCells / 2);
            }

            for (int iY = 0; iY < NumRows; iY++)
            {
                for (int iX = 0; iX < NumCols; iX++, iChild++ )
                {
                    if (iChild < 0)
                        continue; 

                    Rect childRect = new Rect(new Point(iX * CellWidth, iY * CellHeight), new Size(CellWidth, CellHeight));
                    if(iChild < Children.Count)
                        Children[iChild].Arrange(childRect);
                }
            }

            return finalSize;
        }

        const int MaxGridDimension = 5;

        static object CoerceGridDimension(DependencyObject d, object baseValue)
        {
            int value = (int)baseValue;
            if (value < 1)
                return 1;
            if (value > MaxGridDimension)
                return MaxGridDimension;
            return value;
        }

        void TrimExtraChildren(IItemContainerGenerator generator, int maxChildren)
        {
            if (generator == null || maxChildren < 1)
                return;
            while (Children.Count > maxChildren)
            {
                int i = Children.Count - 1;
                GeneratorPosition pos = new GeneratorPosition(i, 0);
                try
                {
                    generator.Remove(pos, 1);
                }
                catch
                {
                }
                RemoveInternalChildRange(i, 1);
            }
        }

        public IReadOnlyList<GridCellLayout> GetVisibleCells()
        {
            List<GridCellLayout> cells = new List<GridCellLayout>(Children.Count);
            ItemContainerGenerator generator = ItemContainerGenerator as ItemContainerGenerator;
            for (int i = 0; i < Children.Count; i++)
            {
                UIElement child = Children[i];
                if (child == null)
                    continue;

                object item = generator?.ItemFromContainer(child);
                if (item == DependencyProperty.UnsetValue)
                    item = null;
                if (item == null && child is FrameworkElement fe)
                    item = fe.DataContext;
                GeneratorPosition pos = new GeneratorPosition(i, 0);
                int itemIndex = ItemContainerGenerator.IndexFromGeneratorPosition(pos);
                Rect bounds;
                try
                {
                    if (VisualTreeHelper.GetParent(child) == null)
                        continue;
                    GeneralTransform transform = child.TransformToAncestor(this);
                    bounds = transform.TransformBounds(new Rect(child.RenderSize));
                }
                catch (InvalidOperationException)
                {
                    continue;
                }
                cells.Add(new GridCellLayout(item, itemIndex, bounds));
            }

            return cells;
        }

        private void CleanUpItems(int firstVisibleItemIndex, int lastVisibleItemIndex)
        {

            UIElementCollection children = this.InternalChildren;
            IItemContainerGenerator generator = this.ItemContainerGenerator;

            for (int i = children.Count-1; i >= 0; i--)
            {
                // Map a child index to an item index by going through a generator position
                GeneratorPosition childGeneratorPos = new GeneratorPosition(i, 0);
                int itemIndex = generator.IndexFromGeneratorPosition(childGeneratorPos);

                if (itemIndex < firstVisibleItemIndex || itemIndex > lastVisibleItemIndex)
                {
                    if (Children[i].IsFocused)
                    {
                        Debug.WriteLine("Deleting Child with Focus");
                        if (i + 1 < Children.Count)
                        {
                            Children[i + 1].Focus();
                        }
                        else if (i - 1 >= 0)
                        {
                            Children[i - 1].Focus();
                        }
                    }
                    generator.Remove(childGeneratorPos, 1);
                    RemoveInternalChildRange(i, 1);
                }
            }
        }

        private static void OnIncrementCommand(object sender, ExecutedRoutedEventArgs e)
        {
            if (sender is VirtualizingGrid grid)
                grid.CenterNumber++;
        }

        private static void OnDecrementCommand(object sender, ExecutedRoutedEventArgs e)
        {
            if (sender is VirtualizingGrid grid)
                grid.CenterNumber--;
        }

        private static void OnAddRowCommand(object sender, ExecutedRoutedEventArgs e)
        {
            if (sender is VirtualizingGrid grid)
                grid.NumRows += 2;
        }

        private static void CanExecuteRemoveRowCommand(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = sender is VirtualizingGrid grid && grid.NumRows > 1;
        }

        private static void OnRemoveRowCommand(object sender, ExecutedRoutedEventArgs e)
        {
            if (sender is VirtualizingGrid grid)
            {
                grid.NumRows -= 2;
                if (grid.NumRows < 1)
                    grid.NumRows = 1;
            }
        }

        private static void OnAddColumnCommand(object sender, ExecutedRoutedEventArgs e)
        {
            if (sender is VirtualizingGrid grid)
                grid.NumCols += 2;
        }

        private static void CanExecuteRemoveColumnCommand(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = sender is VirtualizingGrid grid && grid.NumCols > 1;
        }

        private static void OnRemoveColumnCommand(object sender, ExecutedRoutedEventArgs e)
        {
            if (sender is VirtualizingGrid grid)
            {
                grid.NumCols -= 2;
                if (grid.NumCols < 1)
                    grid.NumCols = 1;
            }
        }

        protected void AddRows()
        {
            NumRows += 2;
        }

        protected bool CanRemoveRows()
        {
            return NumRows > 1;
        }

        protected void RemoveRows()
        {
            NumRows -= 2;
            if (NumRows < 1)
                NumRows = 1;
        }

        protected void AddColumns()
        {
            NumCols += 2;
        }

        protected bool CanRemoveColumns()
        {
            return NumCols > 1; 
        }

        protected void RemoveColumns()
        {
            NumCols -= 2;
            if (NumCols < 1)
                NumCols = 1;
        }
    }

    public sealed class GridCellLayout
    {
        public GridCellLayout(object item, int itemIndex, Rect bounds)
        {
            Item = item;
            ItemIndex = itemIndex;
            Bounds = bounds;
        }

        public object Item { get; }

        public int ItemIndex { get; }

        public Rect Bounds { get; }
    }
}
