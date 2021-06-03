using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace Jotunn.Controls
{
    /// <summary>
    /// Follow steps 1a or 1b and then 2 to use this custom control in a XAML file.
    ///
    /// Step 1a) Using this custom control in a XAML file that exists in the current project.
    /// Add this XmlNamespace attribute to the root element of the markup file where it is 
    /// to be used:
    ///
    ///     xmlns:MyNamespace="clr-namespace:Viking.VolumeView"
    ///
    ///
    /// Step 1b) Using this custom control in a XAML file that exists in a different project.
    /// Add this XmlNamespace attribute to the root element of the markup file where it is 
    /// to be used:
    ///
    ///     xmlns:MyNamespace="clr-namespace:Viking.VolumeView;assembly=Viking.VolumeView"
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
    ///     <MyNamespace:NumberGrid/>
    ///
    /// </summary>
    public class NumberGrid : System.Windows.Controls.Grid
    {
        #region Dependancy Property

        public static readonly DependencyProperty CenterNumberProperty;

        /// <summary>
        /// The number assigned to the center of the grid
        /// </summary>
        public int CenterNumber
        {
            get { return (int)GetValue(NumberGrid.CenterNumberProperty); }
            set { SetValue(NumberGrid.CenterNumberProperty, value); }
        }

        private static void OnCenterNumberChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            NumberGrid c = o as NumberGrid;

            c.GridNumberArray = c.GetVisibleSections(c.CenterNumber, c.OffsetArray); 
        }

        public static readonly DependencyProperty OffsetArrayProperty;

        /// <summary>
        /// The offset array indicates which offsets are applied to the CenterNumber to populate each cell of the grid
        /// </summary>
        public int[] OffsetArray
        {
            get { return (int[])GetValue(NumberGrid.OffsetArrayProperty); }
            set { SetValue(NumberGrid.OffsetArrayProperty, value); }
        }

        private static void OnOffsetArrayChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            NumberGrid c = o as NumberGrid;

            c.GridNumberArray = c.GetVisibleSections(c.CenterNumber, c.OffsetArray); 
        }

        public static readonly DependencyProperty GridNumberArrayProperty;

        /// <summary>
        /// The offset array indicates which offsets are applied to the CenterNumber to populate each cell of the grid
        /// </summary>
        public int?[] GridNumberArray
        {
            get { return (int?[])GetValue(NumberGrid.GridNumberArrayProperty); }
            set { SetValue(NumberGrid.GridNumberArrayProperty, value); }
        }

        public static readonly DependencyProperty ValidNumberSetProperty;

        /// <summary>
        /// The sorted set of numbers which are valid for grid cells
        /// </summary>
        public List<int> ValidNumberSet
        {
            get { return (List<int>)GetValue(NumberGrid.ValidNumberSetProperty); }
            set { SetValue(NumberGrid.ValidNumberSetProperty, value); }
        }

        private static void OnValidNumberSetChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            NumberGrid c = o as NumberGrid;

            List<int> listValidNumbers = e.NewValue as List<int>;
            listValidNumbers.Sort();

            c.GridNumberArray = c.GetVisibleSections(c.CenterNumber, c.OffsetArray);
        }

        #endregion

        #region Commands

        private static RoutedUICommand incrementSectionCommand;
        private static RoutedUICommand decrementSectionCommand;

        /// <summary>
        /// Increment the center number
        /// </summary>
        public static RoutedUICommand IncrementCommand
        {
            get { return incrementSectionCommand; }
        }

        /// <summary>
        /// Decrement the center number
        /// </summary>
        public static RoutedUICommand DecrementCommand
        {
            get { return decrementSectionCommand; }
        }
        
        #endregion

        public NumberGrid() : base()
        {
            
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
        }

        static NumberGrid()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(NumberGrid), new FrameworkPropertyMetadata(typeof(NumberGrid)));

            NumberGrid.CenterNumberProperty = DependencyProperty.Register("CenterNumber",
                                                                        typeof(int),
                                                                        typeof(NumberGrid),
                                                                        new FrameworkPropertyMetadata(1, new PropertyChangedCallback(OnCenterNumberChanged)));

            NumberGrid.OffsetArrayProperty = DependencyProperty.Register("OffsetArray",
                                                                        typeof(int[]),
                                                                        typeof(NumberGrid),
                                                                        new FrameworkPropertyMetadata(new int[] {0}, new PropertyChangedCallback(OnOffsetArrayChanged)));

            NumberGrid.GridNumberArrayProperty = DependencyProperty.Register("GridNumberArray",
                                                                        typeof(int?[]),
                                                                        typeof(NumberGrid),
                                                                        new FrameworkPropertyMetadata(new int?[] {1}));

            NumberGrid.ValidNumberSetProperty = DependencyProperty.Register("ValidNumberSet",
                                                                        typeof(List<int>),
                                                                        typeof(NumberGrid),
                                                                        new FrameworkPropertyMetadata(new List<int>(), new PropertyChangedCallback(OnValidNumberSetChanged)));                        

            InputGestureCollection IncrementInputs = new InputGestureCollection();
            IncrementInputs.Add(new KeyGesture(Key.PageUp)); 
            
            InputGestureCollection DecrementInputs = new InputGestureCollection();
            DecrementInputs.Add(new KeyGesture(Key.PageDown));

            incrementSectionCommand = new RoutedUICommand("+", "IncrementSectionCommand", typeof(NumberGrid), IncrementInputs);
            decrementSectionCommand = new RoutedUICommand("-", "DecrementSectionCommand", typeof(NumberGrid), DecrementInputs);

            
        }

        

        public int? NextLowerSectionNumber(int sectionNumber)
        {
            if (ValidNumberSet == null)
                return sectionNumber; 

            int LowestKey = ValidNumberSet.Min();

            if (ValidNumberSet.Contains(sectionNumber))
                return sectionNumber;
            else
            {
                for (int i = ValidNumberSet.Count - 1; i >= 0; i--)
                {
                    if (ValidNumberSet[i] < sectionNumber)
                        return new int?(ValidNumberSet[i]);
                }
            }

            return new int?();
        }

        public int? NextHigherSectionNumber(int sectionNumber)
        {
            if (ValidNumberSet == null)
                return sectionNumber; 

            if (ValidNumberSet.Contains(sectionNumber))
                return sectionNumber;
            else
            {
                for (int i = 0; i < ValidNumberSet.Count; i++)
                {
                    if (ValidNumberSet[i] > sectionNumber)
                        return new int?(ValidNumberSet[i]);
                }
            }

            return new int?();
        }
        
        private int?[] GetVisibleSections(int CentralSection, int[] OffsetArray)
        {
            //Populate 3x3 grid of sections
            int NumVisibleSections = RowDefinitions.Count * ColumnDefinitions.Count;
            int iSection = CentralSection - (NumVisibleSections / 2);
            int iCenter = (NumVisibleSections - 1) / 2;

            if (OffsetArray == null || OffsetArray.Length < NumVisibleSections)
            {
                OffsetArray = new int[NumVisibleSections];

                for (int i = 0; i < NumVisibleSections / 2; i++)
                    OffsetArray[i] = -1;

                for (int i = NumVisibleSections / 2; i < NumVisibleSections; i++)
                    OffsetArray[i] = 1;
            }

            int?[] SectionArray = new int?[NumVisibleSections];
            SectionArray[iCenter] = CentralSection + OffsetArray[iCenter];

            iSection = iCenter - 1;
            int nextSectionNumber = CentralSection - 1;
            while (iSection >= 0)
            {
                int? SectionNumber = NextLowerSectionNumber(nextSectionNumber);
                SectionArray[iSection] = SectionNumber;

                if (SectionNumber.HasValue)
                    nextSectionNumber = SectionNumber.Value + OffsetArray[iSection];

                iSection--;
            }

            iSection = iCenter + 1;
            nextSectionNumber = CentralSection + 1;
            while (iSection < NumVisibleSections)
            {
                int? SectionNumber = NextHigherSectionNumber(nextSectionNumber);
                SectionArray[iSection] = SectionNumber;

                if (SectionNumber.HasValue)
                    nextSectionNumber = SectionNumber.Value + OffsetArray[iSection];

                iSection++;
            }

            return SectionArray;
        }

        private void OnIncrementSection(object sender, RoutedEventArgs e)
        {
            int? SectionNumber = NextHigherSectionNumber(CenterNumber + 1);
            if (SectionNumber.HasValue)
                CenterNumber = SectionNumber.Value;
        }

        private void OnDecrementSection(object sender, RoutedEventArgs e)
        {
            int? SectionNumber = NextLowerSectionNumber(CenterNumber - 1);
            if (SectionNumber.HasValue)
                CenterNumber = SectionNumber.Value;
        }

        
    }
}