using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SqlServer.Types;

namespace AnnotationVizLib
{
    
    public interface ILocation : IGeometry
    {
        ulong ID { get; }

        ulong ParentID { get; }

        bool Terminal { get; }
        bool OffEdge { get; }
        
        bool IsVericosityCap { get; }

        bool IsUntraceable { get; }

        IDictionary<string, string> Attributes { get; }

        long UnscaledZ { get; }

        string TagsXml { get; }

        LocationType TypeCode { get; }

    }
}
