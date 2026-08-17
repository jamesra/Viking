using System;
using System.Threading.Tasks;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.UI.Commands
{
    internal class ToggleStructureTag(Viking.UI.Controls.SectionViewerControl parent,
        StructureObj structure,
        string tag, string value) : Viking.UI.Commands.Command(parent)
    {
        private readonly StructureObj target = structure;
        private readonly string tag = tag;
        private readonly string value = value;

        public override void OnActivate() => Parent.BeginInvoke((Action)Execute);

        protected override void Execute()
        {
            _ = ExecuteAsync();
        }

        async Task ExecuteAsync()
        {
            await target.ToggleAttribute(tag, value);

            if (!await AnnotationOverlay.SaveStructuresWithMessageBoxOnError())
                await target.ToggleAttribute(tag, value);

            base.Execute();
        }
    }

    internal class ToggleLocationTag(Viking.UI.Controls.SectionViewerControl parent,
        LocationObj loc,
        string tag, string value) : Viking.UI.Commands.Command(parent)
    {
        private readonly LocationObj target = loc;
        private readonly string tag = tag;
        private readonly string value = value;

        public override void OnActivate() => Parent.BeginInvoke((Action)Execute);

        protected override void Execute()
        {
            _ = ExecuteAsync();
        }

        async Task ExecuteAsync()
        {
            await target.ToggleAttribute(tag, value);

            if (!await AnnotationOverlay.SaveLocationsWithMessageBoxOnError())
                await target.ToggleAttribute(tag, value);

            base.Execute();
        }
    }
}
