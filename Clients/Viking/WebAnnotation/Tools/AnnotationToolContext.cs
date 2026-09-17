using System;
using Geometry;
using Viking.Input;
using Viking.VolumeModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.Tools
{
    /// <summary>
    /// Shared viewport/scene access for tool classes. The host owns the queue; tools call StartTool to replace themselves.
    /// </summary>
    public sealed class AnnotationToolContext
    {
        public IViewportHost Host { get; }
        public AnnotationScene Scene { get; }
        public VolumeTransformProvider Transforms { get; }
        public Func<long?> SelectedStructureTypeId { get; }
        public Action<IAnnotationTool> StartTool { get; }
        public Action<LocationObj> RequestGoTo { get; }
        public Action<string> SetStatus { get; }
        public AnnotationPlaceKind ArmedPlaceKind { get; set; } = AnnotationPlaceKind.Circle;

        public AnnotationToolContext(
            IViewportHost host,
            AnnotationScene scene,
            Func<long?> selectedStructureTypeId,
            Action<IAnnotationTool> startTool,
            Action<LocationObj> requestGoTo,
            Action<string> setStatus)
        {
            Host = host;
            Scene = scene;
            Transforms = scene.Transforms;
            SelectedStructureTypeId = selectedStructureTypeId;
            StartTool = startTool;
            RequestGoTo = requestGoTo;
            SetStatus = setStatus;
        }

        public int SectionNumber => Host.SectionNumber;

        public IVolumeToSectionTransform Mapper => Transforms.GetSectionToVolumeTransform(SectionNumber);

        public Vector2 ScreenToWorld(Vector2 screen) => Host.ScreenToWorld(screen);

        public void Invalidate() => Host.Invalidate();
    }
}
