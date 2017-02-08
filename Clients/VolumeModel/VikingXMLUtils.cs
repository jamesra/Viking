using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Diagnostics;
using Utils;

namespace Viking.VolumeModel
{
    static class VikingXMLUtils
    {
        public static Geometry.AxisUnits ParseScale(this XElement scale_elem)
        {
            if (!(scale_elem.HasAttributeCaseInsensitive("UnitsOfMeasure") && scale_elem.HasAttributeCaseInsensitive("UnitsPerPixel")))
            {
                Trace.WriteLine("Scale element missing required elements " + scale_elem.ToString());
            }

            string UnitsOfMeasure = IO.GetAttributeCaseInsensitive(scale_elem, "UnitsOfMeasure").Value;
            double UnitsPerPixel = Convert.ToDouble(IO.GetAttributeCaseInsensitive(scale_elem, "UnitsPerPixel").Value);

            return new Geometry.AxisUnits(UnitsPerPixel, UnitsOfMeasure);
        }
    }
}
