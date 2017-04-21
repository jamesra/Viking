using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using Utils;

namespace Geometry.Transforms
{
     
    public class TransformParameters
    {
        public string transform_name;
        public double[] fixedParameters = new double[0];
        public double[] variableParameters = new double[0];
         
        /// <summary>
        /// Read parameters from an enumerator of strings and return an array of numbers.  First value is number of values.  Remaining strings are values themselves.
        /// </summary>
        /// <param name="NumParameters">Space delimited string, first value is number of parameters.</param>
        /// <param name="parts"></param>
        /// <returns></returns>
        static private double[] ReadParameterValues(string[] parts, long iStart)
        {
            if(parts.Length < iStart)
            {
                throw new ArgumentException("Insufficient parameter values in transform string");
            }

            long number_of_params = Convert.ToInt64(parts[iStart]);
            iStart += 1; //Start reading at parameter values

            if(parts.Length < iStart + number_of_params)
            {
                throw new ArgumentException("Insufficient parameter values in transform string");
            }

            double[] parameter_values = new double[number_of_params]; 

            Parallel.For(iStart, iStart + number_of_params, i =>
            {
                double val = Convert.ToDouble(parts[i]);
                parameter_values[i-iStart] = val;

                Debug.Assert(!(double.IsInfinity(val) || double.IsNaN(val)));
                if (double.IsInfinity(val) || double.IsNaN(val))
                    throw new ArgumentException("Infinite or NaN found in transform parameters file");
            });

            return parameter_values;
        }

        static public TransformParameters Parse(string transform)
        {
            TransformParameters parameters = new TransformParameters();

            string[] transform_parts = transform.Split(new Char[] {' '}, StringSplitOptions.RemoveEmptyEntries);

            parameters.transform_name = transform_parts[0];

            long iWord = 1;
            
            while(iWord < transform_parts.Length)
            {
                if (transform_parts[iWord].ToLower() == "vp")
                {
                    ++iWord; 
                    parameters.variableParameters = ReadParameterValues(transform_parts, iWord);
                    iWord += parameters.variableParameters.Length+1;
                }
                else if (transform_parts[iWord].ToLower() == "fp")
                {
                    ++iWord; 
                    parameters.fixedParameters = ReadParameterValues(transform_parts, iWord);
                    iWord += parameters.fixedParameters.Length+1;
                }
                else
                {
                    iWord++; 
                }
            }

            return parameters;
        }
    }

    public static class TransformFactory
    {
        public static TransformBase TransformFromPoints(MappingGridVector2[] Points)
        {
            return null; 
        }

    #region Stos Parsing code

        public static ITransform ParseStos(string stosfile)
        {
            string filename = Path.GetFileNameWithoutExtension(stosfile);

            int pixelSpacing = 1;

            //this.LastModified = System.IO.File.GetCreationTime(stosfile); 

            //Find out if the name ends in a number indicating the pixel spacing
            //Expecting format: ####-####_grid_##.stos
            string[] fileparts = filename.Split(new char[] { '-', '_' });
            int MappedSection = System.Convert.ToInt32(fileparts[0]);
            int ControlSection = System.Convert.ToInt32(fileparts[1]);

            StosTransformInfo Info = new StosTransformInfo(ControlSection, MappedSection, System.IO.File.GetLastWriteTimeUtc(stosfile));

            //File format may not contain downsample number, if it does record the value

            if (fileparts.Length >= 4)
            {
                pixelSpacing = System.Convert.ToInt32(fileparts[3]);
            }

            using (Stream transformStream = File.OpenRead(stosfile))
            {
                ITransform transform = ParseStos(transformStream, Info, pixelSpacing);              

                return transform; 
            }
            
        }
        
        public static ITransform ParseStos(Uri stosURI, XElement elem, System.Net.NetworkCredential UserCredentials)
        {
            if (elem == null || stosURI == null)
                throw new ArgumentNullException(); 

            int pixelSpacing = System.Convert.ToInt32(Utils.IO.GetAttributeCaseInsensitive(elem,"pixelSpacing").Value);

            int MappedSection = System.Convert.ToInt32(IO.GetAttributeCaseInsensitive(elem, "mappedSection").Value);
            int ControlSection = System.Convert.ToInt32(IO.GetAttributeCaseInsensitive(elem, "controlSection").Value);

            System.Net.HttpWebRequest request = (System.Net.HttpWebRequest)System.Net.HttpWebRequest.CreateDefault(stosURI);
            if (stosURI.Scheme.ToLower() == "https")
                request.Credentials = UserCredentials; 

            request.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.Revalidate);
            request.AutomaticDecompression = System.Net.DecompressionMethods.Deflate | System.Net.DecompressionMethods.GZip;

            try
            {
                using (System.Net.HttpWebResponse response = (System.Net.HttpWebResponse)request.GetResponse())
                {
                    StosTransformInfo info = null;
                    info = new StosTransformInfo(ControlSection, MappedSection, response.LastModified.ToUniversalTime());

                    Trace.WriteLine(stosURI.ToString() + " From Cache: " + response.IsFromCache.ToString() + " Modified: " + info.LastModified.ToString(), "Geometry");
                    using (Stream stream = response.GetResponseStream())
                    {
                        return ParseStos(stream, info, pixelSpacing);
                    }
                }
            }
            catch (System.Net.WebException e)
            {
                Trace.WriteLine(stosURI.ToString() + " could not be loaded", "Geometry");
                return null;
            }
        } 

        public static ITransform ParseStos(Stream stream, StosTransformInfo info, int pixelSpacing)
        {
            string[] lines = StreamUtil.StreamToLines(stream); 
            string[] controlDims = lines[4].Split(new char[] { ' ','\t'}, StringSplitOptions.RemoveEmptyEntries);
            string[] mappedDims = lines[5].Split(new char[] { ' ','\t' }, StringSplitOptions.RemoveEmptyEntries);

            

            double ControlLeft = (System.Convert.ToDouble(controlDims[0]) * pixelSpacing);
            double ControlBottom = (System.Convert.ToDouble(controlDims[1]) * pixelSpacing);
            double ControlRight = ControlLeft + (System.Convert.ToDouble(controlDims[2]) * pixelSpacing);
            double ControlTop = ControlBottom + (System.Convert.ToDouble(controlDims[3]) * pixelSpacing);

            double MappedLeft = (int)(System.Convert.ToDouble(mappedDims[0]) * pixelSpacing);
            double MappedBottom = (int)(System.Convert.ToDouble(mappedDims[1]) * pixelSpacing);
            double MappedRight = MappedLeft + (int)(System.Convert.ToDouble(mappedDims[2]) * pixelSpacing);
            double MappedTop = MappedBottom + (int)(System.Convert.ToDouble(mappedDims[3]) * pixelSpacing);

            GridRectangle ControlBounds = new GridRectangle(ControlLeft, ControlRight, ControlBottom, ControlTop);
            GridRectangle MappedBounds = new GridRectangle(MappedLeft, MappedRight, MappedBottom, MappedTop);

            //Check the parts to make sure they are actually numbers
            TransformParameters transform_parts = TransformParameters.Parse(lines[6]);

            Debug.Assert(transform_parts.fixedParameters.Length > 0 && transform_parts.variableParameters.Length > 0, "StosGridTransform::ParseGridTransform");


            switch (transform_parts.transform_name.ToLower())
            {
                case "gridtransform_double_2_2":
                    //return ParseGridTransform(parts, info, (float)pixelSpacing, iFixedParameters, iVariableParameters, ControlBounds, MappedBounds);
                    return ParseGridTransform(transform_parts, pixelSpacing, info);
                case "legendrepolynomialtransform_double_2_2_3":
                    throw new NotImplementedException("stos transform not supported: legendrepolynomialtransform_double_2_2_3");
                    //MapPoints = ParsePolyTransform(parts, (float)pixelSpacing, iFixedParameters, iVariableParameters, MappedBounds).ToArray();
                case "fixedcenterofrotationaffinetransform_double_2_2":
                    throw new NotImplementedException("stos transform not supported: fixedcenterofrotationaffinetransform_double_2_2");
                    //MapPoints = ParseRotateTranslateAffineTransform(parts, (float)pixelSpacing, iFixedParameters, iVariableParameters, MappedBounds, ControlBounds).ToArray();
                case "meshtransform_double_2_2":
                    return ParseMeshTransform(transform_parts, info, pixelSpacing); 
                default:
                    Debug.Assert(false, "Trying to read stos tranform I don't understand");
                    return null;
            }

        }

        static public ReadOnlyCollection<MappingGridVector2> ParseRotateTranslateAffineTransform(string[] parts,
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
            return new ReadOnlyCollection<MappingGridVector2>(mappings);
        }

        static private GridTransform ParseGridTransform(TransformParameters transform,
                                                                StosTransformInfo info,
                                                                float pixelSpacing, 
                                                                int iFixedParameters,
                                                                int iVariableParameters,
                                                                GridRectangle ControlBounds,
                                                                GridRectangle MappedBounds)
        {
            //Find the dimensions of the grid
            MappingGridVector2[] mappings;

            float MappedWidth = (float)MappedBounds.Width;
            float MappedHeight = (float)MappedBounds.Height;

            int gridWidth = System.Convert.ToInt32(transform.fixedParameters[2] + 1.0);
            int gridHeight = System.Convert.ToInt32(transform.fixedParameters[1] + 1.0);
            double NumPts = gridHeight * gridWidth;

            mappings = new MappingGridVector2[gridWidth * gridHeight];
            GridVector2[] Points = new GridVector2[System.Convert.ToInt32(NumPts)];

            int iPoints = iVariableParameters + 2;

            for (int i = 0; i < NumPts; i++)
            {
                Points[i].X = transform.variableParameters[i*2] * pixelSpacing;
                Points[i].Y = transform.variableParameters[(i * 2) + 1] * pixelSpacing;
            }

            for (int y = 0; y < gridHeight; y++)
            {
                int iYOffset = y * gridWidth; 
                for (int x = 0; x < gridWidth; x++)
                {
                    int i = x + iYOffset;
                    GridVector2 controlPoint = Points[i];
                    GridVector2 mappedPoint = GridTransform.CoordinateFromGridPos(x, y, gridWidth, gridHeight, MappedWidth, MappedHeight);

                    mappings[i] = new MappingGridVector2(controlPoint, mappedPoint);
                }
            }

            return new GridTransform(mappings, MappedBounds, gridWidth, gridHeight, info); 
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
        public static List<MappingGridVector2> ParsePolyTransform(TransformParameters transform , float pixelSpacing, int iFixedParameters, int iVariableParameters, GridRectangle MappedBounds)
        {
            if (transform == null)
                throw new ArgumentNullException(); 

            List<MappingGridVector2> mappings = new List<MappingGridVector2>();

            float MappedWidth = (float)MappedBounds.Width;
            float MappedHeight = (float)MappedBounds.Height;

            int numParams = transform.variableParameters.Length;

            double uc = transform.fixedParameters[0];
            double vc = transform.fixedParameters[1];
            double xmax = transform.fixedParameters[2];
            double ymax = transform.fixedParameters[3];

            uc = xmax / 2.0;
            vc = ymax / 2.0; 
              
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
                            Sa += transform.variableParameters[index_a(j, k)] * PjQk;
                            Sb += transform.variableParameters[index_b(j, k)] * PjQk;
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
                    GridVector2 mappedPoint = GridTransform.CoordinateFromGridPos(x, y, gridWidth, gridHeight, MappedWidth, MappedHeight);

                    mappings.Add(new MappingGridVector2(controlPoint, mappedPoint)); 
                }
            }

            return mappings;
        }

        #endregion

        #region .mosaic Parsing code

        /// <summary>
        /// Load mosaic from specified file and add it to transforms list using specified key
        /// </summary>
        /// <param name="file"></param>
        /// <param name="Key"></param>
        public static ITransform[] LoadMosaic(string path, string[] mosaic, DateTime lastModified)
        {
            if (mosaic == null || path == null)
                throw new ArgumentNullException(); 

            string[] parts;
            int numTiles = 0;
 //           double PixelSpacing; 

            int formatVersion = 0;
            int iTileStart = 3;
            for (int i = 0; i < mosaic.Length; i++)
            {
                string line = mosaic[i];
                if (line.StartsWith("format_version_number:"))
                {
                    parts = line.Split(':');
                    formatVersion = System.Convert.ToInt32(parts[1]);
                }
                if (line.StartsWith("number_of_images:"))
                {
                    parts = line.Split(':');
                    numTiles = System.Convert.ToInt32(parts[1]);
                }
                if (line.StartsWith("pixel_spacing:"))
                {
                    parts = line.Split(':');
                //    PixelSpacing = System.Convert.ToDouble(parts[1]);
                }
                if (line.StartsWith("image:"))
                {
                    iTileStart = i;
                    break;
                }
            }

            Trace.WriteLine("Loading " + numTiles.ToString() + " tiles", "Geometry");
            ITransform[] tileTransforms = new ITransform[numTiles];

            int iTile = 0; 

            if (formatVersion == 0)
            {
                
                for (int i = iTileStart; i < mosaic.Length; i++)
                {
                    string Transform = mosaic[i];
                    //Trace.WriteLine(line, "Geometry");

                    //Get the second entry which is the file name
                    string[] transformParts = Transform.Split(new char[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);

                    string TileFileName = transformParts[1];
                    //Make sure we don't pull in the full path
                    TileFileName = System.IO.Path.GetFileName(TileFileName);

                    string[] TileNameParts = TileFileName.Split(new char[] { '.' }, 3, StringSplitOptions.RemoveEmptyEntries);

                    int iTileNumber = 0;
                    //Viking originally used a format with section.number.png, but Iris may write number.png instead
                    if (TileNameParts.Length == 3)
                    {
                        iTileNumber = System.Convert.ToInt32(TileNameParts[1]);
                    }
                    else
                    {
                        iTileNumber = System.Convert.ToInt32(TileNameParts[0]);
                    }



                    //Crop the tile file name fron the Transform string
                    int iFileName = Transform.IndexOf(TileFileName);
                    Transform = Transform.Remove(0, iFileName + TileFileName.Length);


                    ITransformControlPoints newTGT = ParseMosaicTileEntry(Transform, null) as ITransformControlPoints;
                    TileTransformInfo info = new TileTransformInfo(TileFileName, iTileNumber, lastModified, newTGT.MappedBounds.Width, newTGT.MappedBounds.Height);
                    ((ITransformInfo)newTGT).Info = info; 

                    

                    //GridTransform newTGT = new GridTransform(path, Transform, new TileTransformInfo(TileFileName, iTileNumber, lastModified));

                    tileTransforms[iTile++] = (ITransform)newTGT;
                    //tileTransforms.Add(newTGT);
                }
            }
            else if (formatVersion == 1)
            {
                for (int i = iTileStart; i < mosaic.Length; i += 3)
                {
                    string TileFileName = mosaic[i + 1];
                    string Transform = mosaic[i + 2];
                    //Trace.WriteLine(line, "Geometry");

                    //Make sure we don't pull in the full path
                    TileFileName = System.IO.Path.GetFileName(TileFileName);
                    string[] TileNameParts = TileFileName.Split(new char[] { '.' }, 3, StringSplitOptions.RemoveEmptyEntries);
                    int iTileNumber = 0;

                    try
                    {
                        //Viking originally used a format with section.number.png, but Iris may write number.png instead
                        if (TileNameParts.Length == 3)
                        {
                            iTileNumber = System.Convert.ToInt32(TileNameParts[1]);
                        }
                        else
                        {
                            iTileNumber = System.Convert.ToInt32(TileNameParts[0]);
                        }
                    }
                    catch (System.FormatException)
                    {
                        iTileNumber = i;
                    }


                    //Get the second entry which is the transform 
                    ITransformControlPoints newTGT = ParseMosaicTileEntry(Transform, null) as ITransformControlPoints;
                    TileTransformInfo info = new TileTransformInfo(TileFileName, iTileNumber, lastModified, newTGT.MappedBounds.Width, newTGT.MappedBounds.Height);

                    ((ITransformInfo)newTGT).Info = info;

                    //string[] transformParts = Transform.Split(new char[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
                    //TileGridTransform newTGT = new TileGridTransform(path, Transform, new TileTransformInfo(TileFileName, iTileNumber, lastModified));
                    
                    tileTransforms[iTile++] = (ITransform)newTGT;
                    //tileTransforms.Add(newTGT);
                }
            }
            else
            {
                Debug.Assert(false, "Unknown format version in mosaic file");
                return new GridTransform[0];
            }

            /*
             * Don't translate mosaics to 0,0 origin because the buildscripts do it automatically and the mosaic to volume transforms are broken by the translation
            GridRectangle R = ReferencePointBasedTransform.CalculateControlBounds(tileTransforms as ReferencePointBasedTransform[]);

            
            foreach (TransformBase T in tileTransforms)
            {
                //Adjusting to center here breaks vclume tranformation since volume transforms assume mosaic origin of 0,0
                T.Translate(new GridVector2(-R.Left, -R.Bottom));
            }
            */

            //Add transform list to our collection of transforms
            return tileTransforms;
        }


        private static ITransform ParseMosaicTileEntry(string transformString, TransformInfo info)
        {
            TransformParameters transform = TransformParameters.Parse(transformString);

            switch (transform.transform_name)
            {
                case "GridTransform_double_2_2":
                    return ParseGridTransform(transform, info);
                    
                case "LegendrePolynomialTransform_double_2_2_1":
                    return ParsePolyTransform(transform, info);
                    
                case "TranslationTransform_double_2_2":
                    return ParseTranslateTransform(transform, info);

                case "meshtransform_double_2_2":
                    return ParseMeshTransform(transform, info);

                case "MeshTransform_double_2_2":
                    return ParseMeshTransform(transform, info); 
                    
                default:
                    Debug.Assert(false, "Unexpected transform type: " + transform.transform_name);
                    break;
            }

            return null;
        }


        private static ITransform ParsePolyTransform(TransformParameters transform, TransformInfo info)
        {
            //            string filename = System.IO.Path.GetFileName(parts[1]);
            //            string[] fileparts = filename.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            //            this.Number = System.Convert.ToInt32(fileparts[1]); 

            //Figure out tile size
            int ImageWidth = System.Convert.ToInt32(transform.fixedParameters[2]) * 2;
            int ImageHeight = System.Convert.ToInt32(transform.fixedParameters[3]) * 2;

            //The poly transform parameters dictate the center of the image
            double x = transform.fixedParameters[0] - (ImageWidth / 2.0);
            double y = transform.fixedParameters[1] - (ImageHeight / 2.0);

            GridVector2 ctrlBotLeft = new GridVector2(x, y);
            GridVector2 ctrlBotRight = new GridVector2(x + ImageWidth, y);
            GridVector2 ctrlTopLeft = new GridVector2(x, y + ImageHeight);
            GridVector2 ctrlTopRight = new GridVector2(x + ImageWidth, y + ImageHeight);

            GridVector2 mapBotLeft = new GridVector2(0, 0);
            GridVector2 mapBotRight = new GridVector2(ImageWidth, 0);
            GridVector2 mapTopLeft = new GridVector2(0, ImageHeight);
            GridVector2 mapTopRight = new GridVector2(ImageWidth, ImageHeight);

            MappingGridVector2 BotLeft = new MappingGridVector2(ctrlBotLeft, mapBotLeft);
            MappingGridVector2 BotRight = new MappingGridVector2(ctrlBotRight, mapBotRight);
            MappingGridVector2 TopLeft = new MappingGridVector2(ctrlTopLeft, mapTopLeft);
            MappingGridVector2 TopRight = new MappingGridVector2(ctrlTopRight, mapTopRight);

            MappingGridVector2[] MapPoints = new MappingGridVector2[] { BotLeft, BotRight, TopLeft, TopRight };

            return new GridTransform(MapPoints, new GridRectangle(0, ImageWidth, 0, ImageHeight), 2, 2, info); 
        }

        private static ITransform ParseTranslateTransform(TransformParameters transform, TransformInfo info)
        {
            if (transform == null)
                throw new ArgumentNullException("transform"); 
              
            //string filename = System.IO.Path.GetFileName(parts[1]);

            //Figure out tile size if we haven't already
            int ImageWidth = System.Convert.ToInt32(transform.fixedParameters[2]) * 2;
            int ImageHeight = System.Convert.ToInt32(transform.fixedParameters[3]) * 2;

            double x = transform.variableParameters[0];
            double y = transform.variableParameters[1];

            GridVector2 ctrlBotLeft = new GridVector2(x, y);
            GridVector2 ctrlBotRight = new GridVector2(x + ImageWidth, y);
            GridVector2 ctrlTopLeft = new GridVector2(x, y + ImageHeight);
            GridVector2 ctrlTopRight = new GridVector2(x + ImageWidth, y + ImageHeight);

            GridVector2 mapBotLeft = new GridVector2(0, 0);
            GridVector2 mapBotRight = new GridVector2(ImageWidth, 0);
            GridVector2 mapTopLeft = new GridVector2(0, ImageHeight);
            GridVector2 mapTopRight = new GridVector2(ImageWidth, ImageHeight);

            MappingGridVector2 BotLeft = new MappingGridVector2(ctrlBotLeft, mapBotLeft);
            MappingGridVector2 BotRight = new MappingGridVector2(ctrlBotRight, mapBotRight);
            MappingGridVector2 TopLeft = new MappingGridVector2(ctrlTopLeft, mapTopLeft);
            MappingGridVector2 TopRight = new MappingGridVector2(ctrlTopRight, mapTopRight);

            MappingGridVector2[] mapPoints = new MappingGridVector2[] { BotLeft, BotRight, TopLeft, TopRight };

            return new GridTransform(mapPoints, new GridRectangle(0, ImageWidth, 0, ImageHeight), 2, 2, info);
        }

        private static GridTransform ParseGridTransform(TransformParameters transform, TransformInfo info)
        {
            return ParseGridTransform(transform, 1, info); 
        }

        private static GridTransform ParseGridTransform(TransformParameters transform, double PixelSpacing, TransformInfo info)
        { 
            //string filename = System.IO.Path.GetFileName(parts[1]);
            //string[] fileparts = filename.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            //this.Number = System.Convert.ToInt32(fileparts[1]); 

            int gridWidth = System.Convert.ToInt32(transform.fixedParameters[2] + 1.0);
            int gridHeight = System.Convert.ToInt32(transform.fixedParameters[1] + 1.0);

            int ImageWidth = System.Convert.ToInt32(transform.fixedParameters[5] * PixelSpacing);
            int ImageHeight = System.Convert.ToInt32(transform.fixedParameters[6] * PixelSpacing);
            
            GridRectangle MappedBounds = new GridRectangle(0, ImageWidth, 0, ImageHeight);

            int NumPts = transform.variableParameters.Length / 2; 
            GridVector2[] Points = new GridVector2[NumPts];

            //           verticies = new VertexPositionNormalTexture[numPts];

            Double minX = Double.MaxValue;
            Double minY = Double.MaxValue;
            Double maxX = Double.MinValue;
            Double maxY = Double.MinValue;

            //Every number in the array is separated by an empty space in the array
            for (int i = 0; i < NumPts; i++)
            {
                int iPoint = (i * 2);
                Double x = transform.variableParameters[iPoint] * PixelSpacing;
                Double y = transform.variableParameters[iPoint+1] * PixelSpacing;

                Points[i] = new GridVector2(x, y);

                //Trace.WriteLine(x.ToString() + ", " + y.ToString(), "Geometry");
                if (x < minX)
                    minX = x;
                if (x > maxX)
                    maxX = x;
                if (y < minY)
                    minY = y;
                if (y > maxY)
                    maxY = y;
            }

//            List<int> indicies = new List<int>();
            MappingGridVector2[] mapList = new MappingGridVector2[gridHeight * gridWidth];
            List<int> triangleIndicies = new List<int>((gridHeight-1) * (gridWidth-1) * 6);

            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    int i = x + (y * gridWidth);

                    GridVector2 mapPoint = GridTransform.CoordinateFromGridPos(x, y, gridWidth, gridHeight, ImageWidth,ImageHeight);
                    GridVector2 ctrlPoint = Points[i];
                     
                    mapList[i] = new MappingGridVector2(ctrlPoint, mapPoint); ; 
                }
            }

            int[] triangles = new int[6];

            for (int y = 0; y < gridHeight - 1; y++)
            {
                for (int x = 0; x < gridWidth - 1; x++)
                {
                    int botLeft = x + (y * gridWidth);
                    int botRight = (x + 1) + (y * gridWidth);
                    int topLeft = x + ((y + 1) * gridWidth);
                    int topRight = (x + 1) + ((y + 1) * gridWidth);

                    triangles[0] = botLeft;
                    triangles[1] = botRight;
                    triangles[2] = topLeft;
                    triangles[3] = botRight;
                    triangles[4] = topRight;
                    triangles[5] = topLeft;
                    
                    triangleIndicies.AddRange(triangles);
                }
            }

            return new GridTransform(mapList, MappedBounds, gridWidth, gridHeight, info);
        }

        private static DiscreteTransformWithContinuousFallback ParseMeshTransform(TransformParameters transform, TransformInfo info, double PixelSpacing= 1.0 )
        {
            int NumVariableParameters = transform.variableParameters.Length;
            Debug.Assert(NumVariableParameters % 4 == 0);
            int NumPoints = NumVariableParameters / 4;

            double Left = transform.fixedParameters[3] * PixelSpacing;
            double Bottom = transform.fixedParameters[4] * PixelSpacing;
            double ImageWidth = transform.fixedParameters[5] * PixelSpacing;
            double ImageHeight = transform.fixedParameters[6] * PixelSpacing;

            MappingGridVector2[] Points = new MappingGridVector2[NumPoints];

            for (int iP = 0; iP < NumPoints; iP++)
            {
                int iOffset = (iP * 4);
                GridVector2 Mapped = new GridVector2((transform.variableParameters[iOffset] * ImageWidth) + Left,
                                                     (transform.variableParameters[iOffset+1] * ImageHeight) + Bottom);
                GridVector2 Control = new GridVector2(transform.variableParameters[iOffset+2] * PixelSpacing,
                                                     transform.variableParameters[iOffset+3] * PixelSpacing);

                Points[iP] = new MappingGridVector2(Control, Mapped); 
            }

            MeshTransform discreteTransform = new MeshTransform(Points, info);
            RBFTransform continuousTransform = new RBFTransform(Points, info);

            return new DiscreteTransformWithContinuousFallback(discreteTransform, continuousTransform, info);
        }

    #endregion

    }
}
