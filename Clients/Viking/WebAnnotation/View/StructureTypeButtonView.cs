using Geometry;
using VikingXNAGraphics;
using WebAnnotationModel;

namespace WebAnnotation.View
{
    /// <summary>
    /// Renders the name of a structure type object over a box with the background color = Structure type color
    /// </summary>
    class StructureTypeButtonView : IViewPosition2D
    {
        StructureTypeObj model;

        VikingXNAGraphics.RectangleView BackgroundBox = null;
        VikingXNAGraphics.LabelView Label = null;

        public StructureTypeButtonView(StructureTypeObj obj)
        {
            model = obj;
        }

        public GridVector2 Position
        {
            get => ((IViewPosition2D)Label).Position;
            set
            {
                ((IViewPosition2D)Label).Position = value;
                BackgroundBox.Position = value;
            }
        }


        private void CreateVisuals(GridVector2 position)
        {
            Label = new LabelView(model.Name, position);
            BackgroundBox = new RectangleView(Label.BoundingRect, model.Color.ToXNAColor());
        }
    }
}
