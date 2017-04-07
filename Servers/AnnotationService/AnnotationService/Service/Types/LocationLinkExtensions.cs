using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnnotationService.Types
{
    public static class LocationLinkExtensions
    {
        public static LocationLink Create(this ConnectomeDataModel.LocationLink link)
        {
            LocationLink ll = new LocationLink();
            ll.SourceID = link.A;
            ll.TargetID = link.B;
            return ll;
        }
    }
}
