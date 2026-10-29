using Geometry;
using Microsoft.Xna.Framework;
using VikingXNAGraphics;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.View
{
    /// <summary>
    /// Renders labels for a Structure inside a circle
    /// </summary>
    public class StructureCircleLabels : StructureLabelsBase
    {
        private Circle VolumeCircle;

        public double DesiredRowsOfText { get; set; } = 4.0;

        public override double DefaultFontSize => (Radius * 2.0) / DesiredRowsOfText;

        protected override double IDLineWidth => Radius * 2.0;

        public double Radius
        {
            get => VolumeCircle.Radius;
            set
            {
                VolumeCircle = new Circle(VolumeCircle.Center, value);
                CreateLabelObjects();
            }
        }

        public StructureCircleLabels(LocationObj obj, Circle circle, bool ShowAttributeLabels = true) : base(obj, ShowAttributeLabels)
        {
            VolumeCircle = circle;
            CreateLabelObjects();
        }

        private void CreateLabelObjects()
        {
            {
                double Height = VolumeCircle.Radius / 3.0f;
                StructureIDLabelView = new LabelView(StructureIDLabelWithTypeCode(locationObj.Parent), VolumeCircle.Center - new Geometry.Vector2(0, Height), fontSize: DefaultFontSize)
                {
                    MaxLineWidth = Circle.WidthAtHeight(Height / Radius) * (Radius * 2.0),
                    _Color = IDColor
                };
            }

            if (ShowAttributeLabels)
            {
                string Label = StructureLabel(locationObj);
                if (Label is null || Label?.Length == 0)
                {
                    StructureLabelView = null;
                }
                else
                {
                    double height = Radius / 2.0f;
                    StructureLabelView = new LabelView(Label, VolumeCircle.Center + new Geometry.Vector2(0, height))
                    {
                        MaxLineWidth = Circle.WidthAtHeight(height / Radius) * (Radius * 2.0)
                    };
                }

                string Tags = TagLabel(locationObj);
                if (Tags is null || Tags?.Length == 0)
                {
                    StructureAttributeView = null;
                }
                else
                {
                    double height = Radius / 4.0f;
                    StructureAttributeView = new LabelView(Tags, VolumeCircle.Center + new Geometry.Vector2(0, height))
                    {
                        MaxLineWidth = Circle.WidthAtHeight(height / Radius) * (Radius * 2.0)
                    };
                }
            }

            if (locationObj.Parent != null && locationObj.Parent.ParentID.HasValue)
            {
                double height = Radius / 2.0f;
                ParentStructureLabelView = new LabelView(locationObj.Parent.ParentID.ToString(), VolumeCircle.Center + new Geometry.Vector2(0, height))
                {
                    _Color = Color.Red, //locationObj.Parent.Parent.Type.Color.ToXNAColor(0.75f);
                    MaxLineWidth = Circle.WidthAtHeight(height / Radius) * (Radius * 2.0)
                };
            }
            else
            {
                ParentStructureLabelView = null;
            }

            RecordTargets();
        }
    }
}
