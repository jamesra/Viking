#if !NETFRAMEWORK
using System;
using Viking;
using Viking.VolumeModel;
using WebAnnotation.ViewModel;

namespace WebAnnotation
{
    /// <summary>
    /// Same type name as the WinForms overlay so shared Actions compile. No input or draw.
    /// AnnotationScene must set SectionViewLookup or GetAnnotationsForSection returns null.
    /// </summary>
    internal static class AnnotationOverlay
    {
        public static OverlayHost CurrentOverlay { get; } = new();

        public static void ShowFaultExceptionMsgBox(Exception e) =>
            System.Diagnostics.Trace.WriteLine(e);

        public static Func<int, SectionAnnotationsView> SectionViewLookup { get; set; }

        public static SectionAnnotationsView GetAnnotationsForSection(int sectionNumber) =>
            SectionViewLookup?.Invoke(sectionNumber);

        public static void UpdateCacheSize(int _)
        {
        }

        internal sealed class OverlayHost
        {
            public OverlayParent Parent { get; } = new();
        }

        internal sealed class OverlayParent
        {
            public OverlaySection Section { get; } = new();

            public double Downsample => TileLoadEnvironment.GetDownsample?.Invoke() ?? 1;
        }

        internal sealed class OverlaySection
        {
            public IVolumeToSectionTransform ActiveSectionToVolumeTransform
            {
                get
                {
                    int sectionNumber = TileLoadEnvironment.GetSectionNumber?.Invoke() ?? 0;
                    if (AnnotationBootstrap.Transforms != null)
                        return AnnotationBootstrap.Transforms.GetSectionToVolumeTransform(sectionNumber);

                    throw new InvalidOperationException("Annotation store is not initialized.");
                }
            }
        }
    }
}
#endif
