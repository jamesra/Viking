using Geometry;
using Geometry.Meshing;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using VikingXNA;
using Microsoft.Xna.Framework.Graphics;

namespace VikingXNAGraphics
{
    [Flags]
    public enum PointLabelType
    {
        NONE = 0b0000_0000,
        INDEX = 0b0000_0001, //The index of the point in the collection
        POSITION = 0b0000_0010 //The position of the point
    }

    /// <summary>
    /// Draw a collection of points, optionally labeling by position, index, or both
    /// </summary>
    public class PointSetView : PointViewBase
    {
        public CircleView[] PointViews = [];
        public LabelView[] LabelViews = [];
        private double _PointRadius = 1.0;

        private PointLabelType _LabelType = PointLabelType.NONE;
        public PointLabelType LabelType
        {
            get => _LabelType;
            set
            {
                _LabelType = value;
                UpdateViews();
            }
        }

        public bool LabelIndex
        {
            get => (_LabelType & PointLabelType.INDEX) > 0;
            set
            {
                LabelType = value ? _LabelType | PointLabelType.INDEX : _LabelType & ~PointLabelType.INDEX;

            }
        }

        public bool LabelPosition
        {
            get => (_LabelType & PointLabelType.POSITION) > 0;
            set
            {
                LabelType = value ? _LabelType | PointLabelType.POSITION : _LabelType & ~PointLabelType.POSITION;

            }
        }

        private Color _LabelColor = Color.Black;

        public Color LabelColor
        {
            get => _LabelColor;

            set
            {
                _LabelColor = value;
                UpdateViews();
            }
        }


        public double PointRadius
        {
            get => _PointRadius;
            set
            {
                _PointRadius = value;
                UpdateViews();
            }
        }

        public PointSetView(double defaultRadius = 1.0) : this(Color.Gold, defaultRadius)
        {
        }

        public PointSetView(Color defaultColor, double defaultRadius = 1.0)
        {
            base.Color = defaultColor;
            _PointRadius = defaultRadius;
        }

        public override void UpdateViews()
        {
            if (Points is null)
            {
                PointViews = [];
                LabelViews = [];
                return;
            }

            PointViews = [.. Points.Select(p => new CircleView(new GridCircle(p, PointRadius), Color))];
            GridVector2[] point_array = [.. Points];

            if (!LabelIndex && !LabelPosition)
            {
                //No need to adjust labels if there are no labels
                LabelViews = null;
                return;
            }
                //Figure out if we have duplicate points and offset labels as needed
                QuadTree<int> DuplicatePointsAddedCount = new(); //Track the number of times we've hit a specific duplicate point and move the label accordingly
            List<GridVector2> KnownPoints = [];
            foreach (GridVector2 p in point_array)
            {
                if (KnownPoints.Contains(p))
                {
                    if (DuplicatePointsAddedCount.TryGetValue(p, out var count))
                        DuplicatePointsAddedCount[p] = count + 1; //Increment the count
                    else
                    {
                        DuplicatePointsAddedCount.Add(p, 0); //Set the counter to 0 for when we use it later
                    }

                }
                else
                {
                    KnownPoints.Add(p);
                }
            }

            if (LabelIndex && !LabelPosition)
            {
                LabelViews = [.. point_array.Select((p, i) => new LabelView(i.ToString(), p, fontSize: this.PointRadius * 2))];
            }
            else if(LabelPosition)
            {
                LabelViews = !LabelIndex && LabelPosition
                    ? [.. point_array.Select(p => new LabelView(p.ToLabel(), p, fontSize: this.PointRadius * 2))]
                    : [.. point_array.Select((p, i) => new LabelView(i.ToString() + "\n" + p.ToLabel(), p, fontSize: this.PointRadius * 2))];
            }
            else
            {
                throw new ArgumentException("Cannot label index and position");
            }

            if (LabelViews != null)
            {
                for (int i = 0; i < LabelViews.Length; i++)
                {
                    LabelView label = LabelViews[i];
                    label.FontSize = this.PointRadius * 2.0;
                    label.Color = this.LabelColor;

                    if (DuplicatePointsAddedCount.TryGetValue(point_array[i], out var count))
                    {
                        //label.Position = label.Position + new GridVector2(0,PointRadius * (DuplicatePointsAddedCount[point_array[i]]-1));

                        //label.Position = label.Position + label.
                        string prepended_newlines = "";
                        for (int iLine = 0; iLine < count; iLine++)
                            prepended_newlines += "|\n\r";

                        label.Text = prepended_newlines + label.Text; //Prepend a line
                    }
                }
            }
        }
        /*
        public void Draw(IRenderInfo window, IScene scene, OverlayStyle overlayStyle)
        {   
            if (PointViews != null)
                CircleView.Draw(window.device, scene, overlayStyle, PointViews);

            if (LabelViews != null)
                LabelView.Draw(window.spriteBatch, window.font, scene, LabelViews);
        }*/

        public override void DrawBatch(GraphicsDevice device, IScene scene, OverlayStyle Overlay, IRenderable[] items) => throw new NotImplementedException();

        public override void Draw(GraphicsDevice device, IScene scene, OverlayStyle overlayStyle)
        {
            if (PointViews != null)
            {
                CircleView.Draw(device, scene, overlayStyle, PointViews);
            }

            if (LabelViews != null)
            {
                var fontData = DeviceFontStore.TryGet(device);
                LabelView.Draw(fontData.SpriteBatch, fontData.Font, scene, LabelViews);
            }
        }

        public static PointSetView CreateFor(IReadOnlyMesh2D<IVertex2D> mesh)
        {
            PointSetView psv = new(Color.Gray)
            {
                LabelColor = Color.White,
                PointRadius = 2,
                Points = [.. mesh.Verticies.Select(p => p.Position)],
                LabelIndex = true,
                LabelPosition = false
            };
            psv.UpdateViews();

            return psv;
        }

        public static PointSetView CreateFor(IReadOnlyMesh3D<IVertex3D> mesh)
        {
            PointSetView psv = new(Color.Gray)
            {
                LabelColor = Color.White,
                PointRadius = 2,
                Points = [.. mesh.Verticies.Select(p => p.Position.XY())],
                LabelIndex = true,
                LabelPosition = false
            };
            psv.UpdateViews();

            return psv;
        }

    }
}
