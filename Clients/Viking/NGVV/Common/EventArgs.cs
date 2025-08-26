using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Viking.ViewModels;

namespace Viking.Common
{

    public class SectionChangedEventArgs : System.EventArgs
    {
        public SectionViewModel NewSection;
        public SectionViewModel OldSection;

        public SectionChangedEventArgs(SectionViewModel newSection, SectionViewModel oldSection)
        {
            this.NewSection = newSection;
            this.OldSection = oldSection;
        }
    }
    public delegate Task SectionChangedEventHandler(object sender, SectionChangedEventArgs e, CancellationToken token);

    public class ReferenceSectionChangedEventArgs : System.EventArgs
    {
        public SectionViewModel ChangedSection;
        public long? OldReferenceSection;
        public long? NewReferenceSection;

        public ReferenceSectionChangedEventArgs(SectionViewModel changedSection,
            long? oldReference,
            long? newReference)
        {
            this.ChangedSection = changedSection;
            this.OldReferenceSection = oldReference;
            this.NewReferenceSection = newReference;
        }
    }

    public delegate void ReferenceSectionChangedEventHandler(object sender, ReferenceSectionChangedEventArgs e);

}
