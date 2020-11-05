using System;
using System.Diagnostics;
using System.Xml.Linq;
using Utils;

namespace Viking.VolumeModel
{
    static class VikingXMLUtils
    {
        public static UnitsAndScale.IAxisUnits ParseScale(this XElement scale_elem)
        {
            if (!(scale_elem.HasAttributeCaseInsensitive("UnitsOfMeasure") && scale_elem.HasAttributeCaseInsensitive("UnitsPerPixel")))
            {
                Trace.WriteLine("Scale element missing required elements " + scale_elem.ToString());
            }

            string UnitsOfMeasure = IO.GetAttributeCaseInsensitive(scale_elem, "UnitsOfMeasure").Value;
            double UnitsPerPixel = Convert.ToDouble(IO.GetAttributeCaseInsensitive(scale_elem, "UnitsPerPixel").Value);

            return new UnitsAndScale.AxisUnits(UnitsPerPixel, UnitsOfMeasure);
        }
    }
}
