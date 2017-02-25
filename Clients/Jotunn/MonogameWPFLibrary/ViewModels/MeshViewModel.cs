using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoGame;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Windows;

namespace MonogameWPFLibrary.ViewModels
{     
    /// <summary>
    /// Describes a mesh
    /// </summary>
    public class MeshViewModel : DependencyObject
    {
        public VertexPositionColor[] Verticies
        {
            get { return (VertexPositionColor[])GetValue(VerticiesProperty); }
            set { SetValue(VerticiesProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Verticies.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty VerticiesProperty =
            DependencyProperty.Register("Verticies", typeof(VertexPositionColor[]), typeof(MeshViewModel), new PropertyMetadata( new VertexPositionColor[0]));


        public int[] Edges
        {
            get { return (int[])GetValue(EdgesProperty); }
            set { SetValue(EdgesProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Edges.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty EdgesProperty =
            DependencyProperty.Register("Edges", typeof(int[]), typeof(MeshViewModel), new PropertyMetadata(new int[0]));


        /*
        public Matrix World
        {
            get { return (Matrix)GetValue(WorldProperty); }
            set { SetValue(WorldProperty, value); }
        }

        // Using a DependencyProperty as the backing store for World.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty WorldProperty =
            DependencyProperty.Register("World", typeof(Matrix), typeof(MeshViewModel), new PropertyMetadata(Matrix.Identity));
        */

        private Matrix _World = Matrix.Identity;

        public Matrix World
        {
            get { return _World; }
        }

        public Matrix Translate
        {
            get { return (Matrix)GetValue(TranslateProperty); }
            set { SetValue(TranslateProperty, value); }
        }

        // Using a DependencyProperty as the backing store for World.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TranslateProperty =
            DependencyProperty.Register("Translate", typeof(Matrix), typeof(MeshViewModel), new PropertyMetadata(Matrix.Identity, OnMatrixChanged));

        public Matrix Rotation
        {
            get { return (Matrix)GetValue(RotationProperty); }
            set { SetValue(RotationProperty, value); }
        }

        // Using a DependencyProperty as the backing store for World.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty RotationProperty =
            DependencyProperty.Register("Rotation", typeof(Matrix), typeof(MeshViewModel), new PropertyMetadata(Matrix.CreateFromYawPitchRoll(0,0,0), OnMatrixChanged));

        public MeshViewModel()
        {
        }

        public MeshViewModel(VertexPositionColor[] verts, int[] edges)
        {
            this.Verticies = verts;
            this.Edges = edges;
        }
        public MeshViewModel(VertexPositionColor[] verts, int[] edges, Vector3 translate) : this(verts, edges)
        {
            this.Translate = Matrix.CreateTranslation(translate);   
        }

        private static void OnMatrixChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            MeshViewModel model = o as MeshViewModel; 
            if(model != null)
            {
                model.UpdateWorldMatrix();
            }
        }
        private void UpdateWorldMatrix()
        {
            this._World = this.Translate * this.Rotation; 
        }
    }
}
