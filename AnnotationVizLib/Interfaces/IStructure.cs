using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnnotationVizLib
{
    public interface IStructure
    {
        ulong ID { get; }

        ulong? ParentID { get; }

        ulong TypeID { get; }

        string Label { get; }

        IStructureLink[] Links
        {
            get;
        }

        IStructureType Type
        {
            get;
        }

        string TagsXML { get; }
    }
}
