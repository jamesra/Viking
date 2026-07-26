using Viking.AnnotationServiceTypes.Interfaces;
using Geometry; 
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using WebAnnotationModel.Objects;

namespace WebAnnotationModel
{
    public static class Extensions
    {  
        /*
        public static SqlGeometry ToGeometry(GridVector2[] points, double Z, double radius, LocationType type)
        {
            if (points is null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            switch (type)
            {
                case LocationType.CIRCLE:
                    if (points.Length == 4)
                        return points.ToCurvePolygon();
                    else
                        return SqlGeometryUtils.Extensions.ToCircle(points[0].X, points[1].Y, Z, radius);
                case LocationType.OPENCURVE:
                    return points.ToSqlGeometry();
            }

            throw new ArgumentException(string.Format("Unknown location type {0}", type));
        }
        */

        /// <summary>
        /// A helper function to raise events on the UI thread if needed. 
        /// From: https://stackoverflow.com/questions/1698889/raise-events-in-net-on-the-main-ui-thread
        /// </summary>
        /// <param name="theEvent"></param>
        /// <param name="args"></param>
        public static void RaiseEventOnUIThread(this Delegate theEvent, object[] args)
        {
            if (theEvent == null)
                return;

            foreach (Delegate d in theEvent.GetInvocationList())
            {
                if (d.Target is ISynchronizeInvoke s) 
                    s.BeginInvoke(d, args);  // cleanup omitted 
                else 
                    d.DynamicInvoke(args); 
            }
        }

        public static void RaiseEventOnUIThread(this PropertyChangedEventHandler theEvent, PropertyChangedEventArgs args)
        {
            theEvent.RaiseEventOnUIThread(new object[] { args });
        }
    }


    public static class AttributeExtensions
    {
        /// <summary>
        /// Remove tag if it is in the attribute list or add tag if it is not
        /// </summary>
        /// <param name="listAttributes"></param>
        /// <param name="tag"></param>
        /// <param name="value"></param>
        /// <returns>True if the tag exists in the attributes after the function has completed.</returns>
        public static bool ToggleAttribute(this ICollection<ObjAttribute> listAttributes, string tag, string value = null)
        {
            var existing = listAttributes.FirstOrDefault(a => a.Equals(tag));
            bool FoundExisting = null != existing;
            if (FoundExisting)
            {
                while (null != existing)
                {
                    listAttributes.Remove(existing);
                    existing = listAttributes.FirstOrDefault(a => a.Equals(tag));
                }

                return false;
            }
            else{
                var attrib = new ObjAttribute(tag, value);
                listAttributes.Add(attrib);
                return true;
            }
        }

        public static bool Contains(this List<ObjAttribute> listAttributes, string tag)
        {
            //We cannot use the built-in contains function because ObjAttribute equality comparer also checks the value of the attribute
            return listAttributes.Exists(a => a.Name == tag);
        }

        public static int RemoveAll(this List<ObjAttribute> listAttributes, string tag)
        {
            //We cannot use the built-in contains function because ObjAttribute equality comparer also checks the value of the attribute
            return listAttributes.RemoveAll(a => a.Name == tag);
        }
    }

}
