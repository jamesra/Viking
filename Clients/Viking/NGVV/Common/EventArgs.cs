using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Viking.ViewModels;

namespace Viking.Common
{

    public class SectionChangedEventArgs(SectionViewModel newSection, SectionViewModel oldSection) : System.EventArgs
    {
        public SectionViewModel NewSection = newSection;
        public SectionViewModel OldSection = oldSection;
    }
    public delegate Task SectionChangedEventHandler(object sender, SectionChangedEventArgs e, CancellationToken token);

    public class ReferenceSectionChangedEventArgs(SectionViewModel changedSection,
        long? oldReference,
        long? newReference) : System.EventArgs
    {
        public SectionViewModel ChangedSection = changedSection;
        public long? OldReferenceSection = oldReference;
        public long? NewReferenceSection = newReference;
    }

    public delegate void ReferenceSectionChangedEventHandler(object sender, ReferenceSectionChangedEventArgs e);

}
