using System;
using System.Threading;

using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.UI.Commands
{
    /// <summary>
    /// This command takes a structureObj and LocationObj defined by other commands and commits them to the database
    /// </summary>
    internal class CreateNewStructureCommand(Viking.UI.Controls.SectionViewerControl parent,
                                           StructureObj structure,
                                           LocationObj location) : AnnotationCommandBase(parent)
    {
        private readonly StructureObj newStruct = structure;
        private readonly LocationObj newLoc = location;
        int _started;

        public override void OnActivate() => Parent.BeginInvoke((Action)Execute);

        protected override void Execute()
        {
            _ = ExecuteAsync();
        }

        async System.Threading.Tasks.Task ExecuteAsync()
        {
            if (Interlocked.Exchange(ref _started, 1) != 0)
                return;

            var result = await Store.Structures.Create(newStruct, newLoc);
            if (result.Location != null)
            {
                Global.LastEditedAnnotationID = result.Location.ID;
            }

            base.Execute();
        }
    }
}
