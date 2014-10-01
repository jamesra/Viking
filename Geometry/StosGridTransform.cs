using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Xml.Linq;
using System.Runtime.Serialization; 


namespace Geometry
{
    [Serializable]
    public class StosGridTransform : GridTransform
    {
        

        public StosGridTransform(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        public StosGridTransform(string stosfile) : base()
        {
            string filename = Path.GetFileNameWithoutExtension(stosfile);

            int pixelSpacing = 1;

            this.LastModified = System.IO.File.GetCreationTime(stosfile); 

            //Find out if the name ends in a number indicating the pixel spacing
            //Expecting format: ####-####_grid_##.stos
            string[] fileparts = filename.Split(new char[] { '-', '_' });
            this.MappedSection = System.Convert.ToInt32(fileparts[0]);
            this.ControlSection = System.Convert.ToInt32(fileparts[1]);

            //File format may not contain downsample number, if it does record the value

            if (fileparts.Length >= 4)
            {
                pixelSpacing = System.Convert.ToInt32(fileparts[3]);
            }

            Stream transform = File.OpenRead(stosfile);

            ParseStosFile(transform, pixelSpacing);
        }

        private StosGridTransform(XElement elem)
        {
            //Loading the stos files really needs to be taken out of this module, it should be math only
            this.MappedSection = System.Convert.ToInt32(elem.Attribute("mappedSection").Value);
            this.ControlSection = System.Convert.ToInt32(elem.Attribute("controlSection").Value);
            
        }

        public StosGridTransform(string stosfile, XElement elem, System.Net.NetworkCredential UserCredentials)
            : this(elem)
        {
            Uri stosURI = new Uri(stosfile);
            int pixelSpacing = System.Convert.ToInt32(elem.Attribute("pixelSpacing").Value);

            System.Net.HttpWebRequest request = (System.Net.HttpWebRequest)System.Net.HttpWebRequest.CreateDefault(stosURI);
            if (stosURI.Scheme.ToLower() == "https")
                request.Credentials = UserCredentials; 

            request.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.Revalidate);
            request.AutomaticDecompression = System.Net.DecompressionMethods.Deflate | System.Net.DecompressionMethods.GZip;

            System.Net.WebResponse response = null;
            Stream stream = null;
            try
            {
                response = request.GetResponse();

                try
                {
                    string LastModifiedString = response.Headers["Last-Modified"];
                    this.LastModified = DateTime.Parse(LastModifiedString); 
                }
                catch(Exception e)
                {

                }

                Trace.WriteLine(stosfile + " From Cache: " + response.IsFromCache.ToString() + " Modified: " + LastModified.ToString(), "Geometry");
                stream = response.GetResponseStream();
                ParseStosFile(stream, pixelSpacing);
            }
            finally
            {
                if (stream != null)
                {
                    stream.Close();
                    stream = null;
                }

                if (response != null)
                {
                    response.Close();
                    response = null;
                }
            }
        }

        public StosGridTransform(Stream stream, XElement elem)
            : this(elem)
        {
            int pixelSpacing = System.Convert.ToInt32(elem.Attribute("pixelSpacing").Value);

            ParseStosFile(stream, pixelSpacing);
        }

        void ParseStosFile(Stream stream, int pixelSpacing)
        {
            string[] lines = Common.StreamToLines(stream); 
            string[] controlDims = lines[4].Split(new char[] { ' ','\t'}, StringSplitOptions.RemoveEmptyEntries);
            string[] mappedDims = lines[5].Split(new char[] { ' ','\t' }, StringSplitOptions.RemoveEmptyEntries);

            ControlBounds.Left = (System.Convert.ToDouble(controlDims[0]) * pixelSpacing);
            ControlBounds.Bottom = (System.Convert.ToDouble(controlDims[1]) * pixelSpacing);
            ControlBounds.Right = ControlBounds.Left + (System.Convert.ToDouble(controlDims[2]) * pixelSpacing);
            ControlBounds.Top = ControlBounds.Bottom + (System.Convert.ToDouble(controlDims[3]) * pixelSpacing);

            MappedBounds.Left = (int)(System.Convert.ToDouble(mappedDims[0]) * pixelSpacing);
            MappedBounds.Bottom = (int)(System.Convert.ToDouble(mappedDims[1]) * pixelSpacing);
            MappedBounds.Right = ControlBounds.Left + (int)(System.Convert.ToDouble(mappedDims[2]) * pixelSpacing);
            MappedBounds.Top = ControlBounds.Bottom + (int)(System.Convert.ToDouble(mappedDims[3]) * pixelSpacing);
            
            string[] parts = lines[6].Split(new char[] {' '}, StringSplitOptions.RemoveEmptyEntries);

            //Find the dimensions of the grid
            int iFixedParameters = 0;
            int iVariableParameters = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == "vp")
                {
                    iVariableParameters = i;
                    continue;
                }

                if (parts[i] == "fp")
                {
                    iFixedParameters = i;
                    break;
                }
            }

            Debug.Assert(iFixedParameters > 0 && iVariableParameters > 0, "StosGridTransform::ParseGridTransform"); 

            switch (parts[0].ToLower())
            {
                case "gridtransform_double_2_2":
                    this.mapPoints = ParseGridTransform(parts, (float)pixelSpacing, iFixedParameters, iVariableParameters, MappedBounds);
                    break;
                case "legendrepolynomialtransform_double_2_2_3":
                    this.mapPoints = ParsePolyTransform(parts, (float)pixelSpacing, iFixedParameters, iVariableParameters, MappedBounds).ToArray();
                    break;
                case "fixedcenterofrotationaffinetransform_double_2_2":
                    this.mapPoints = ParseRotateTranslateAffineTransform(parts, (float)pixelSpacing, iFixedParameters, iVariableParameters, MappedBounds, ControlBounds).ToArray();
                    break;
                default:
                    Debug.Assert(false, "Trying to read stos tranform I don't understand");
                    break; 
            }
        }

        static public List<MappingGridVector2> ParseRotateTranslateAffineTransform(string[] parts,
            float pixelSpacing,
            int iFixedParameters,
            int iVariableParameters,
            GridRectangle MappedBounds, 
            GridRectangle ControlBounds)
        {
            
            //Find the dimensions of the grid
            List<MappingGridVector2> mappings = new List<MappingGridVector2>();
            /*
            GridVector2[] Points = new GridVector2[4];
            GridVector2[] mappedPoints = new GridVector2[4];
            GridVector2[] ctrlPoints = new GridVector2[4];

            Points[0] = new GridVector2(0, 0);
            Points[1] = new GridVector2(MappedBounds.Width, 0);
            Points[2] = new GridVector2(0, MappedBounds.Height);
            Points[3] = new GridVector2(MappedBounds.Width, MappedBounds.Height);

            ctrlPoints[0] = new GridVector2(0, 0);
            ctrlPoints[1] = new GridVector2(ControlBounds.Width, 0);
            ctrlPoints[2] = new GridVector2(0, ControlBounds.Height);
            ctrlPoints[3] = new GridVector2(ControlBounds.Width, ControlBounds.Height);

            Matrix mat = Matrix.Identity;
            mat.M11 = System.Convert.ToSingle(parts[iVariableParameters + 2]);
            mat.M12 = System.Convert.ToSingle(parts[iVariableParameters + 3]);
            mat.M21 = System.Convert.ToSingle(parts[iVariableParameters + 4]);
            mat.M22 = System.Convert.ToSingle(parts[iVariableParameters + 5]); 
            
            //Cheating: since the rotation matrix is
            //[cos(t) -sin(t)]
            //[sin(t)  cos(t)]
            //we just take the asin of the parameter to find the rotation value

//            double theta = Math.Acos(System.Convert.ToSingle(parts[iVariableParameters + 2]));

            //Matrix mat = Matrix.CreateRotationZ((float)theta); 

            mappedPoints[0] = Vector2.Transform(Points[0], mat);
            mappedPoints[1] = Vector2.Transform(Points[1], mat);
            mappedPoints[2] = Vector2.Transform(Points[2], mat);
            mappedPoints[3] = Vector2.Transform(Points[3], mat);

            Triangle controlOne = new Triangle(ctrlPoints[0], ctrlPoints[1], ctrlPoints[2]);
            Triangle controlTwo = new Triangle(ctrlPoints[2], ctrlPoints[1], ctrlPoints[3]);
            Triangle mappedOne = new Triangle(mappedPoints[0], mappedPoints[1], mappedPoints[2]);
            Triangle mappedTwo = new Triangle(mappedPoints[2], mappedPoints[1], mappedPoints[3]);

            mappings.Add(new MappingTriangle(controlOne, mappedOne));
            mappings.Add(new MappingTriangle(controlTwo, mappedTwo));
            */
            return mappings; 
        }

        static public MappingGridVector2[] ParseGridTransform(string[] parts, float pixelSpacing, int iFixedParameters, int iVariableParameters, GridRectangle MappedBounds)
        {
            //Find the dimensions of the grid
            MappingGridVector2[] mappings;

            float MappedWidth = (float)MappedBounds.Width;
            float MappedHeight = (float)MappedBounds.Height; 
            
            int gridWidth = System.Convert.ToInt32(System.Convert.ToDouble(parts[iFixedParameters + 4]) + 1.0);
            int gridHeight = System.Convert.ToInt32(System.Convert.ToDouble(parts[iFixedParameters + 3]) + 1.0);
            double NumPts = gridHeight * gridWidth;

            mappings = new MappingGridVector2[gridWidth * gridHeight];
            GridVector2[] Points = new GridVector2[System.Convert.ToInt32(NumPts)];

            int iPoints = iVariableParameters + 2;

            for (int i = 0; i < NumPts; i++)
            {
                Points[i].X = System.Convert.ToDouble(parts[iPoints + (i * 2)]) * pixelSpacing;
                Points[i].Y = System.Convert.ToDouble(parts[iPoints + (i * 2) + 1]) * pixelSpacing;
            }

            for (int y = 0; y < gridHeight; y++)
            {
                int iYOffset = y * gridWidth; 
                for (int x = 0; x < gridWidth; x++)
                {
                    int i = x + iYOffset;
                    GridVector2 controlPoint = Points[i];
                    GridVector2 mappedPoint = CoordinateFromGridPos(x, y, gridWidth, gridHeight, MappedWidth, MappedHeight);

                    mappings[i] = new MappingGridVector2(controlPoint, mappedPoint);
                }
            }

            return mappings; 
        }

        const uint Dimensions = 3;
        const uint CoefficientsPerDimension = ((Dimensions + 1) * (Dimensions + 2)) / 2;

        
        static uint index_a(int j, int k)
        {
            return (uint)(j + ((j + k) * (j + k + 1)) / 2);
        }

        static uint index_b(int j, int k)
        {
            return CoefficientsPerDimension + index_a(j,k);
        }

        /// <summary>
        /// This code was reverse engineered from original stos polynomial transform source
        /// </summary>
        /// <param name="parts"></param>
        /// <param name="pixelSpacing"></param>
        /// <param name="iFixedParameters"></param>
        /// <param name="iVariableParameters"></param>
        /// <param name="MappedBounds"></param>
        /// <returns></returns>
        public static List<MappingGridVector2> ParsePolyTransform(string[] parts, float pixelSpacing, int iFixedParameters, int iVariableParameters, GridRectangle MappedBounds)
        {
            List<MappingGridVector2> mappings = new List<MappingGridVector2>();

            float MappedWidth = (float)MappedBounds.Width;
            float MappedHeight = (float)MappedBounds.Height; 
            
            int numParams = System.Convert.ToInt32(parts[iVariableParameters +1]); 
            
            //Skip two so we skip the "vp 5" part of the file and our indicies line up with Paul's code
            iFixedParameters += 2; 
            iVariableParameters += 2;

            double uc = System.Convert.ToDouble(parts[iFixedParameters]);
            double vc = System.Convert.ToDouble(parts[iFixedParameters + 1]);
            double xmax = System.Convert.ToDouble(parts[iFixedParameters + 2]);
            double ymax = System.Convert.ToDouble(parts[iFixedParameters + 3]);

            uc = xmax / 2;
            vc = ymax / 2; 

            double[] parameters = new double[numParams]; 
            for(int iVP = 0; iVP < numParams; iVP++)
            {
                parameters[iVP] = System.Convert.ToDouble(parts[iVariableParameters + iVP]); 
            }

            int gridHeight = 5;
            int gridWidth = 5;

            int NumPts = (int)(gridHeight * gridWidth);

            GridVector2[] Points = new GridVector2[NumPts];

            for (int iY = 0; iY < gridHeight; iY++)
            {
                for (int iX = 0; iX < gridWidth; iX++)
                {
                    double u = (xmax / (double)(gridWidth-1)) * (double)iX;
                    double v = (ymax / (double)(gridHeight-1)) * (double)iY;

                    double A = (u - uc) / xmax;
                    double B = (v - vc) / ymax;

                    //For some reason I am off by a factor of two:
                    A *= 2;
                    B *= 2; 

                    double[] P = new double[Dimensions + 1];
                    double[] Q = new double[Dimensions + 1];

                    for (int i = 0; i <= Dimensions; i++)
                    {
                        P[i] = Legendre.P[i](A);
                        Q[i] = Legendre.P[i](B); 
                    }

                    double Sa = 0.0;
                    double Sb = 0.0;

                    for (int i = 0; i <= Dimensions; i++)
                    {
                        for (int j = 0; j <= i; j++)
                        {
                            int k = i - j;
                            double PjQk = P[j] * Q[k];
                            Sa += parameters[index_a(j, k)] * PjQk;
                            Sb += parameters[index_b(j, k)] * PjQk;
                        }
                    }

                    

                    Points[(iY * gridWidth) + iX] = new GridVector2((xmax * Sa * pixelSpacing), (ymax * Sb * pixelSpacing)); 
                }
            }

            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    int i = x + (y * gridWidth);
                    GridVector2 controlPoint = Points[i];
                    GridVector2 mappedPoint = CoordinateFromGridPos(x, y, gridWidth, gridHeight, MappedWidth, MappedHeight);

                    mappings.Add(new MappingGridVector2(controlPoint, mappedPoint)); 
                }
            }

            return mappings;
        }
    }
}
