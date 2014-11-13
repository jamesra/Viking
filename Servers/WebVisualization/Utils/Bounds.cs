using AnnotationUtils.AnnotationService;

namespace ConnectomeViz.Utils
{
    class BoundsAdjustment
    {
        public readonly double XScale=1;
        public readonly double YScale = 1;
        public readonly double XOffset = 0;
        public readonly double YOffset = 0;

        public readonly double XMax = double.MinValue;
        public readonly double YMax = double.MinValue;
        public readonly double XMin = double.MaxValue;
        public readonly double YMin = double.MaxValue;

        public BoundsAdjustment(double DesiredMaxWidth, double DesiredMaxHeight, double Xmax, double Xmin, double Ymax, double Ymin)
        {
            this.XMax = Xmax;
            this.YMax = Ymax;
            this.XMin = Xmin;
            this.YMin = Ymin;

            XOffset = XMin;
            YOffset = YMin;

            XScale = DesiredMaxWidth / (XMax - XMin);
            YScale = DesiredMaxHeight /(YMax - YMin);

            //We want to use the same ratio for scales, so figure out if desiredMaxWidth or DesiredMaxHeight has the smaller scalar
            if (XScale < YScale)
            {
                YScale = XScale;
            }
            else if (YScale < XScale)
            {
                XScale = YScale;
            }
        }

        public double AdjustX(double X)
        {
            return (X-XOffset) * XScale;
        }

        public double AdjustY(double Y)
        {
            return (Y-YOffset) * YScale; 
        }

        /// <summary>
        /// A constructor returning a bounds adjustment object for the given array of locationInfo objects
        /// </summary>
        /// <param name="locationInfoArray"></param>
        /// <param name="DesiredWidth">Desired Width of Bounding rectangle</param>
        /// <param name="DesiredHeight">Desired Height of Bounding rectangle</param>
        /// <returns></returns>
        static public BoundsAdjustment CalculateBounds(LocationInfo[] locationInfoArray, double DesiredMaxWidth, double DesiredMaxHeight)
        {
            double XMax = double.MinValue;
            double YMax = double.MinValue;
            double XMin = double.MaxValue;
            double YMin = double.MaxValue;

            foreach(LocationInfo locInfo in locationInfoArray)
            {
                if(locInfo.X > XMax)
                    XMax = locInfo.X;

                if(locInfo.X < XMin)
                    XMin = locInfo.X;

                if(locInfo.Y > YMax)
                    YMax = locInfo.Y;

                if(locInfo.Y < YMin)
                    YMin = locInfo.Y;
            }

            
            BoundsAdjustment bounds = new BoundsAdjustment(DesiredMaxWidth, DesiredMaxHeight, XMax,XMin, YMax, YMin);

            return bounds;
        }
    }

}