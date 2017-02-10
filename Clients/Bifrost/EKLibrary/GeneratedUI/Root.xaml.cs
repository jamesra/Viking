// -----------------------------------------------------------
//  
//  This file was generated, please do not modify.
//  
// -----------------------------------------------------------
namespace EmptyKeys.UserInterface.Generated {
    using System;
    using System.CodeDom.Compiler;
    using System.Collections.ObjectModel;
    using EmptyKeys.UserInterface;
    using EmptyKeys.UserInterface.Charts;
    using EmptyKeys.UserInterface.Data;
    using EmptyKeys.UserInterface.Controls;
    using EmptyKeys.UserInterface.Controls.Primitives;
    using EmptyKeys.UserInterface.Input;
    using EmptyKeys.UserInterface.Interactions.Core;
    using EmptyKeys.UserInterface.Interactivity;
    using EmptyKeys.UserInterface.Media;
    using EmptyKeys.UserInterface.Media.Effects;
    using EmptyKeys.UserInterface.Media.Animation;
    using EmptyKeys.UserInterface.Media.Imaging;
    using EmptyKeys.UserInterface.Shapes;
    using EmptyKeys.UserInterface.Renderers;
    using EmptyKeys.UserInterface.Themes;
    
    
    [GeneratedCodeAttribute("Empty Keys UI Generator", "2.6.0.0")]
    public partial class Root : UIRoot {
        
        private Grid e_0;
        
        private TextBlock e_1;
        
        private TabControl e_2;
        
        private Rectangle e_3;
        
        private Rectangle e_4;
        
        private Rectangle e_5;
        
        private Image e_6;
        
        public Root() : 
                base() {
            this.Initialize();
        }
        
        public Root(int width, int height) : 
                base(width, height) {
            this.Initialize();
        }
        
        private void Initialize() {
            Style style = RootStyle.CreateRootStyle();
            style.TargetType = this.GetType();
            this.Style = style;
            this.InitializeComponent();
        }
        
        private void InitializeComponent() {
            // e_0 element
            this.e_0 = new Grid();
            this.Content = this.e_0;
            this.e_0.Name = "e_0";
            RowDefinition row_e_0_0 = new RowDefinition();
            row_e_0_0.Height = new GridLength(1F, GridUnitType.Auto);
            this.e_0.RowDefinitions.Add(row_e_0_0);
            RowDefinition row_e_0_1 = new RowDefinition();
            this.e_0.RowDefinitions.Add(row_e_0_1);
            RowDefinition row_e_0_2 = new RowDefinition();
            this.e_0.RowDefinitions.Add(row_e_0_2);
            ColumnDefinition col_e_0_0 = new ColumnDefinition();
            col_e_0_0.Width = new GridLength(1F, GridUnitType.Auto);
            this.e_0.ColumnDefinitions.Add(col_e_0_0);
            ColumnDefinition col_e_0_1 = new ColumnDefinition();
            this.e_0.ColumnDefinitions.Add(col_e_0_1);
            ColumnDefinition col_e_0_2 = new ColumnDefinition();
            this.e_0.ColumnDefinitions.Add(col_e_0_2);
            // e_1 element
            this.e_1 = new TextBlock();
            this.e_0.Children.Add(this.e_1);
            this.e_1.Name = "e_1";
            this.e_1.HorizontalAlignment = HorizontalAlignment.Center;
            this.e_1.VerticalAlignment = VerticalAlignment.Top;
            this.e_1.Text = "Viking";
            Grid.SetColumn(this.e_1, 0);
            Grid.SetRow(this.e_1, 0);
            Grid.SetColumnSpan(this.e_1, 2);
            // e_2 element
            this.e_2 = new TabControl();
            this.e_0.Children.Add(this.e_2);
            this.e_2.Name = "e_2";
            this.e_2.Width = 100F;
            Grid.SetColumn(this.e_2, 0);
            Grid.SetRow(this.e_2, 1);
            Grid.SetRowSpan(this.e_2, 2);
            // e_3 element
            this.e_3 = new Rectangle();
            this.e_0.Children.Add(this.e_3);
            this.e_3.Name = "e_3";
            this.e_3.Height = 100F;
            this.e_3.Width = 200F;
            this.e_3.Margin = new Thickness(5F, 5F, 5F, 5F);
            this.e_3.Fill = new SolidColorBrush(new ColorW(255, 165, 0, 255));
            Grid.SetColumn(this.e_3, 1);
            Grid.SetRow(this.e_3, 1);
            // e_4 element
            this.e_4 = new Rectangle();
            this.e_0.Children.Add(this.e_4);
            this.e_4.Name = "e_4";
            this.e_4.Height = 100F;
            this.e_4.Width = 200F;
            this.e_4.Margin = new Thickness(5F, 5F, 5F, 5F);
            this.e_4.Fill = new SolidColorBrush(new ColorW(0, 128, 0, 255));
            Grid.SetColumn(this.e_4, 2);
            Grid.SetRow(this.e_4, 1);
            // e_5 element
            this.e_5 = new Rectangle();
            this.e_0.Children.Add(this.e_5);
            this.e_5.Name = "e_5";
            this.e_5.Height = 100F;
            this.e_5.Width = 200F;
            this.e_5.Margin = new Thickness(5F, 5F, 5F, 5F);
            this.e_5.Fill = new SolidColorBrush(new ColorW(0, 0, 255, 255));
            Grid.SetColumn(this.e_5, 1);
            Grid.SetRow(this.e_5, 2);
            // e_6 element
            this.e_6 = new Image();
            this.e_0.Children.Add(this.e_6);
            this.e_6.Name = "e_6";
            Grid.SetColumn(this.e_6, 2);
            Grid.SetRow(this.e_6, 2);
            Binding binding_e_6_Source = new Binding("RenderTargetSource");
            this.e_6.SetBinding(Image.SourceProperty, binding_e_6_Source);
        }
    }
}
