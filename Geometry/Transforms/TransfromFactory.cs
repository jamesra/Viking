using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Utils;

namespace Geometry.Transforms
{

    public readonly struct TransformParameters
    {
        public readonly string TransformName;
        public readonly double[] FixedParameters;
        public readonly double[] VariableParameters;

        private TransformParameters(string name, double[] fixedParams, double[] variableParams)
        {
            TransformName = name;
            FixedParameters = fixedParams;
            VariableParameters = variableParams;
        }
         
        /// <summary>
        /// Read parameters from an enumerator of strings and return an array of numbers.  First value is number of values.  Remaining strings are values themselves.
        /// </summary>
        /// <param name="NumParameters">Space delimited string, first value is number of parameters.</param>
        /// <param name="parts"></param>
        /// <returns></returns>
        private static double[] ReadParameterValues(string[] parts, long iStart)
        {
            if (parts.Length < iStart)
            {
                throw new ArgumentException("Insufficient parameter values in transform string");
            }

            long number_of_params = Convert.ToInt64(parts[iStart]);
            iStart += 1; //Start reading at parameter values

            if (parts.Length < iStart + number_of_params)
            {
                throw new ArgumentException("Insufficient parameter values in transform string");
            }

            double[] parameter_values = new double[number_of_params];

            /*
            Parallel.For(iStart, iStart + number_of_params, i =>
            {
                double val = Convert.ToDouble(parts[i]);
                parameter_values[i - iStart] = val;

                Debug.Assert(!(double.IsInfinity(val) || double.IsNaN(val)));
                if (double.IsInfinity(val) || double.IsNaN(val))
                    throw new ArgumentException("Infinite or NaN found in transform parameters file");
            });
            */

            for (long i = iStart; i < iStart + number_of_params; i++)
            {
                double val = Convert.ToDouble(parts[i]);
                parameter_values[i - iStart] = val;

                Debug.Assert(!(double.IsInfinity(val) || double.IsNaN(val)));
                if (double.IsInfinity(val) || double.IsNaN(val))
                    throw new ArgumentException("Infinite or NaN found in transform parameters file");
            }

            return parameter_values;
        }

        public static TransformParameters Parse(string transform)
        {
            string[] transform_parts = transform.Split(new Char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            var transform_name = transform_parts[0];
            double[] variableParameters = Array.Empty<double>();
            double[] fixedParameters = Array.Empty<double>();

            long iWord = 1;

            while (iWord < transform_parts.Length)
            {
                if (transform_parts[iWord].ToLower() == "vp")
                {
                    ++iWord;
                    variableParameters = ReadParameterValues(transform_parts, iWord);
                    iWord += variableParameters.Length + 1;
                }
                else if (transform_parts[iWord].ToLower() == "fp")
                {
                    ++iWord;
                    fixedParameters = ReadParameterValues(transform_parts, iWord);
                    iWord += fixedParameters.Length + 1;
                }
                else
                {
                    iWord++;
                }
            }

            return new TransformParameters(transform_name, fixedParameters, variableParameters);
        }
    }

    public static class TransformFactory
    {
        public static TransformBase TransformFromPoints(MappingGridVector2[] Points)
        {
            return null;
        }

        #region Stos Parsing code

        public static async Task<ITransform> ParseStos(string stosfile)
        {
            string filename = System.IO.Path.GetFileNameWithoutExtension(stosfile);

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
                ITransform transform = await ParseStos(transformStream, Info, pixelSpacing).ConfigureAwait(false);
                return transform;
            }

        }

        public static async Task<ITransform> ParseStos(Uri stosURI, XElement elem, System.Net.NetworkCredential UserCredentials)
        {
            if (elem == null) 
                throw new ArgumentNullException(nameof(elem));

            if (stosURI == null)
                throw new ArgumentNullException(nameof(stosURI));

            int pixelSpacing = System.Convert.ToInt32(elem.GetAttributeCaseInsensitive("pixelSpacing").Value);

            int MappedSection = System.Convert.ToInt32(elem.GetAttributeCaseInsensitive("mappedSection").Value);
            int ControlSection = System.Convert.ToInt32(elem.GetAttributeCaseInsensitive("controlSection").Value);
             
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(stosURI, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                if (false == response.IsSuccessStatusCode)
                {
                    Trace.WriteLine($"Failure loading .stos from server: {response.StatusCode}");
                    return null;
                }

                var lm = response.Content.Headers.LastModified ?? DateTime.MaxValue;
                var lastModified = lm.UtcDateTime;
                var info = new StosTransformInfo(ControlSection, MappedSection, lastModified);

#if DEBUG
                Trace.WriteLine($"{stosURI} Modified: {lastModified}");
#endif
                using (var memStream = new MemoryStream(await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false)))
                {
                    return await ParseStos(memStream, info, pixelSpacing).ConfigureAwait(false);
                }
            }



            /*
            System.Net.HttpWebRequest request = System.Net.WebRequest.CreateHttp(stosURI);
            if (stosURI.Scheme.ToLower() == "https")
                request.Credentials = UserCredentials;

            request.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.Revalidate);
            request.AutomaticDecompression = System.Net.DecompressionMethods.Deflate | System.Net.DecompressionMethods.GZip;

            try
            {
                using (var response = await request.GetResponseAsync().ConfigureAwait(false))
                {
                    StosTransformInfo info = null;
                    info = new StosTransformInfo(ControlSection, MappedSection, response.);

#if DEBUG
                    //Trace.WriteLine(stosURI.ToString() + " From Cache: " + response.IsFromCache.ToString() + " Modified: " + info.LastModified.ToString(), "Geometry");
#endif 
                    using (Stream stream = response.GetResponseStream())
                    {
                        return await ParseStos(stream, info, pixelSpacing);
                    }
                }
            }
            catch (System.Net.WebException)
            {
                Trace.WriteLine(stosURI.ToString() + " could not be loaded", "Geometry");
                return null;
            }
            */
        }

        public static async Task<ITransform> ParseStos(Stream stream, StosTransformInfo info, int pixelSpacing)
        {
            string[] lines = await stream.ToLinesAsync(); ;
            string[] controlDims = lines[4].Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            string[] mappedDims = lines[5].Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
              
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

            Debug.Assert(transform_parts.FixedParameters.Length > 0 && transform_parts.VariableParameters.Length > 0, "StosGridTransform::ParseGridTransform");


            switch (transform_parts.TransformName.ToLower())
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

        public static ReadOnlyCollection<MappingGridVector2> ParseRotateTranslateAffineTransform(string[] parts,
            float pixelSpacing,
            int iFixedParameters,
            int iVariableParameters,
            in GridRectangle MappedBounds,
            in GridRectangle ControlBounds)
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

        private static GridTransform ParseGridTransform(TransformParameters transform,
                                                                StosTransformInfo info,
                                                                float pixelSpacing,
                                                                int iFixedParameters,
                                                                int iVariableParameters,
                                                                in GridRectangle ControlBounds,
                                                                in GridRectangle MappedBounds)
        {
            //Find the dimensions of the grid
            MappingGridVector2[] mappings;

            float MappedWidth = (float)MappedBounds.Width;
            float MappedHeight = (float)MappedBounds.Height;

            int gridWidth;
            try
            {
                gridWidth = System.Convert.ToInt32(transform.FixedParameters[2]) + 1;
            }
            catch(System.OverflowException)
            {
                try { 
                    gridWidth = (int)System.Convert.ToDouble(transform.FixedParameters[2]) + 1;
                }
                catch(System.OverflowException e)
                {
                    throw new ArgumentException($"Could not parse value: {transform.FixedParameters[2]}.", e);
                }
            }

            int gridHeight;
            try
            {
                gridHeight = System.Convert.ToInt32(transform.FixedParameters[1]) + 1;
            }
            catch (System.OverflowException)
            {
                try
                {
                    gridHeight = (int)System.Convert.ToDouble(transform.FixedParameters[1]) + 1;
                }
                catch (System.OverflowException e)
                {
                    throw new ArgumentException($"Could not parse value: {transform.FixedParameters[2]}.", e);
                }
            } 

            int NumPts = gridHeight * gridWidth;

            mappings = new MappingGridVector2[gridWidth * gridHeight];
            GridVector2[] Points = new GridVector2[NumPts];

            int iPoints = iVariableParameters + 2;

            for (int i = 0; i < NumPts; i++)
            {
                Points[i].X = transform.VariableParameters[i * 2] * pixelSpacing;
                Points[i].Y = transform.VariableParameters[(i * 2) + 1] * pixelSpacing;
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
            return CoefficientsPerDimension + index_a(j, k);
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
        public static List<MappingGridVector2> ParsePolyTransform(TransformParameters transform, float pixelSpacing, int iFixedParameters, int iVariableParameters, in GridRectangle MappedBounds)
        { 
            List<MappingGridVector2> mappings = new List<MappingGridVector2>();

            float MappedWidth = (float)MappedBounds.Width;
            float MappedHeight = (float)MappedBounds.Height;

            int numParams = transform.VariableParameters.Length;

            double uc = transform.FixedParameters[0];
            double vc = transform.FixedParameters[1];
            double xmax = transform.FixedParameters[2];
            double ymax = transform.FixedParameters[3];

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
                    double u = (xmax / (double)(gridWidth - 1)) * (double)iX;
                    double v = (ymax / (double)(gridHeight - 1)) * (double)iY;

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
                            Sa += transform.VariableParameters[index_a(j, k)] * PjQk;
                            Sb += transform.VariableParameters[index_b(j, k)] * PjQk;
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

        private static readonly Regex TileNumberRegex = new Regex(@"[^\d]*(?<number>\d+)[^\.]*(?<ext>\..+)?", RegexOptions.Compiled);
        /// <summary>
        /// Load mosaic from specified file and add it to transforms list using specified key
        /// </summary>
        /// <param name="file"></param>
        /// <param name="Key"></param>
        public static ITransform[] LoadMosaic(string path, string[] mosaic, DateTime lastModified)
        {
            if (mosaic == null)
                throw new ArgumentNullException(nameof(mosaic));

            if (path == null)
                throw new ArgumentNullException(nameof(path));

            int numTiles = 0;
            //           double PixelSpacing; 

            int formatVersion = 0;
            int iTileStart = 3;
            for (int i = 0; i < mosaic.Length; i++)
            {
                string[] parts;
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

            //Trace.WriteLine("Loading " + numTiles.ToString() + " tiles", "Geometry");
            ITransform[] tileTransforms = new ITransform[numTiles];

            int iTile = 0;

            if (formatVersion == 0)
            { 
                for (int i = iTileStart; i < mosaic.Length; i++)
                {
                    string Transform = mosaic[i];
                    if (string.IsNullOrWhiteSpace(Transform))
                        continue;

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
                for (int i = iTileStart; i < mosaic.Length-1; i += 3)
                {
                    string TileFileName = mosaic[i + 1];
                    string Transform = mosaic[i + 2];
                    //Trace.WriteLine(line, "Geometry");

                    //Make sure we don't pull in the full path
                    TileFileName = System.IO.Path.GetFileName(TileFileName);

                    var m = TileNumberRegex.Match(TileFileName);
                    if (false == m.Success)
                        throw new FormatException($"Unable to parse tile file name {TileFileName}");

                    string tilenumber = m.Groups["number"].Value;
                    int iTileNumber = 0;
                    try
                    {
                        iTileNumber = System.Convert.ToInt32(tilenumber);
                    }
                    catch (System.FormatException)
                    {
                        iTileNumber = i;
                    }

                    /*
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
                    }*/


                    //Get the second entry which is the transform 
                    ITransform newTGT = ParseMosaicTileEntry(Transform, null);
                    TileTransformInfo info;
                    if (newTGT is ITransformControlPoints tcp)
                        info = new TileTransformInfo(TileFileName, iTileNumber, lastModified, tcp.MappedBounds.Width, tcp.MappedBounds.Height);
                    else if (newTGT is IContinuousTransform tcont)
                        info = new TileTransformInfo(TileFileName, iTileNumber, lastModified, 4080, 4080);
                    else
                        throw new NotImplementedException("Unsupported transform type");

                    if (newTGT is Geometry.ITransformInfo TInfo)
                        TInfo.Info = info;
                    else
                        throw new NotImplementedException(
                            "Transform should implement ITransformInfo if used in mosaics");

                    //string[] transformParts = Transform.Split(new char[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
                    //TileGridTransform newTGT = new TileGridTransform(path, Transform, new TileTransformInfo(TileFileName, iTileNumber, lastModified));

                    tileTransforms[iTile++] = (ITransform)newTGT;
                    //tileTransforms.Add(newTGT);
                }
            }
            else
            {
                Debug.Assert(false, "Unknown format version in mosaic file");
                return Array.Empty<GridTransform>();
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


        private static ITransform ParseMosaicTileEntry(string transformString, TransformBasicInfo info)
        {
            TransformParameters transform = TransformParameters.Parse(transformString);

            switch (transform.TransformName)
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

                case "Rigid2DTransform_double_2_2":
                    return ParseRigidTransform(transform, info);

                default:
                    Debug.Assert(false, "Unexpected transform type: " + transform.TransformName);
                    break;
            }

            return null;
        }


        private static ITransform ParsePolyTransform(TransformParameters transform, TransformBasicInfo info)
        {
            //            string filename = System.IO.Path.GetFileName(parts[1]);
            //            string[] fileparts = filename.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            //            this.Number = System.Convert.ToInt32(fileparts[1]); 

            //Figure out tile size
            int ImageWidth = System.Convert.ToInt32(transform.FixedParameters[2]) * 2;
            int ImageHeight = System.Convert.ToInt32(transform.FixedParameters[3]) * 2;

            //The poly transform parameters dictate the center of the image
            double x = transform.FixedParameters[0] - (ImageWidth / 2.0);
            double y = transform.FixedParameters[1] - (ImageHeight / 2.0);

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

        private static ITransform ParseTranslateTransform(TransformParameters transform, TransformBasicInfo info)
        {  
            //string filename = System.IO.Path.GetFileName(parts[1]);

            //Figure out tile size if we haven't already
            int ImageWidth = System.Convert.ToInt32(transform.FixedParameters[2]) * 2;
            int ImageHeight = System.Convert.ToInt32(transform.FixedParameters[3]) * 2;

            double x = transform.VariableParameters[0];
            double y = transform.VariableParameters[1];

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

        private static ITransform ParseRigidTransform(TransformParameters transform, TransformBasicInfo info)
        {
            //string filename = System.IO.Path.GetFileName(parts[1]);
            var angle = transform.VariableParameters[0];
            var sourceToTargetOffset =
                new GridVector2(transform.VariableParameters[1], transform.VariableParameters[2]);

            if (angle != 0)
            {
                var sourceSpaceCenterOfRotation = new GridVector2(transform.FixedParameters[0], transform.FixedParameters[1]);
                throw new NotImplementedException("Rotation by an angle not supported yet");
            }

            return new RigidNoRotationTransform(sourceToTargetOffset, info);
        }

        private static GridTransform ParseGridTransform(TransformParameters transform, TransformBasicInfo info)
        {
            return ParseGridTransform(transform, 1, info);
        }

        private static GridTransform ParseGridTransform(TransformParameters transform, double PixelSpacing, TransformBasicInfo info)
        {
            //string filename = System.IO.Path.GetFileName(parts[1]);
            //string[] fileparts = filename.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            //this.Number = System.Convert.ToInt32(fileparts[1]); 

            int gridWidth = System.Convert.ToInt32(transform.FixedParameters[2] + 1.0);
            int gridHeight = System.Convert.ToInt32(transform.FixedParameters[1] + 1.0);

            int ImageWidth = System.Convert.ToInt32(transform.FixedParameters[5] * PixelSpacing);
            int ImageHeight = System.Convert.ToInt32(transform.FixedParameters[6] * PixelSpacing);

            GridRectangle MappedBounds = new GridRectangle(0, ImageWidth, 0, ImageHeight);

            int NumPts = transform.VariableParameters.Length / 2;
            GridVector2[] Points = new GridVector2[NumPts];

            //           verticies = new VertexPositionNormalTexture[numPts];
            /*
            Double minX = Double.MaxValue;
            Double minY = Double.MaxValue;
            Double maxX = Double.MinValue;
            Double maxY = Double.MinValue;
            */
            //Every number in the array is separated by an empty space in the array
            for (int i = 0; i < NumPts; i++)
            {
                int iPoint = (i * 2);
                double x = transform.VariableParameters[iPoint] * PixelSpacing;
                double y = transform.VariableParameters[iPoint + 1] * PixelSpacing;

                Points[i] = new GridVector2(x, y);

                //Trace.WriteLine(x.ToString() + ", " + y.ToString(), "Geometry");
                /*
                if (x < minX)
                    minX = x;
                if (x > maxX)
                    maxX = x;
                if (y < minY)
                    minY = y;
                if (y > maxY)
                    maxY = y;
                */
            }

            //            List<int> indicies = new List<int>();
            MappingGridVector2[] mapList = new MappingGridVector2[gridHeight * gridWidth];
            List<int> triangleIndicies = new List<int>((gridHeight - 1) * (gridWidth - 1) * 6);

            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    int i = x + (y * gridWidth);

                    GridVector2 mapPoint = GridTransform.CoordinateFromGridPos(x, y, gridWidth, gridHeight, ImageWidth, ImageHeight);
                    GridVector2 ctrlPoint = Points[i];

                    mapList[i] = new MappingGridVector2(ctrlPoint.Round(Global.TransformSignificantDigits), mapPoint.Round(Global.TransformSignificantDigits));
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

        private static DiscreteTransformWithContinuousFallback ParseMeshTransform(TransformParameters transform, TransformBasicInfo info, double PixelSpacing = 1.0)
        {
            int NumVariableParameters = transform.VariableParameters.Length;
            Debug.Assert(NumVariableParameters % 4 == 0);
            int NumPoints = NumVariableParameters / 4;

            double Left = transform.FixedParameters[3] * PixelSpacing;
            double Bottom = transform.FixedParameters[4] * PixelSpacing;
            double ImageWidth = transform.FixedParameters[5] * PixelSpacing;
            double ImageHeight = transform.FixedParameters[6] * PixelSpacing;

            MappingGridVector2[] Points = new MappingGridVector2[NumPoints];

            for (int iP = 0; iP < NumPoints; iP++)
            {
                int iOffset = (iP * 4);
                GridVector2 Mapped = new GridVector2((transform.VariableParameters[iOffset] * ImageWidth) + Left,
                                                     (transform.VariableParameters[iOffset + 1] * ImageHeight) + Bottom);
                GridVector2 Control = new GridVector2(transform.VariableParameters[iOffset + 2] * PixelSpacing,
                                                     transform.VariableParameters[iOffset + 3] * PixelSpacing);

                Points[iP] = new MappingGridVector2(Control.Round(Global.TransformSignificantDigits), Mapped.Round(Global.TransformSignificantDigits));
            }

            MeshTransform discreteTransform = new MeshTransform(Points, info);
            RBFTransform continuousTransform = new RBFTransform(Points, info);

            return new DiscreteTransformWithContinuousFallback(discreteTransform, continuousTransform, info);
        }

        #endregion

    }
}
