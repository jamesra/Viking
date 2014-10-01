using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Runtime.Serialization; 


namespace Geometry
{
    [Serializable]
    public class TileGridTransform : GridTransform
    {
        public int ImageWidth = int.MinValue;
        public int ImageHeight = int.MinValue;

        public int Number;

        public string TileFileName;

        public TileGridTransform(string SectionPath, string TileEntry, string tileFileName, int iTileNumber) : base()
        {
            this.TileFileName = tileFileName; 
            this.Number = iTileNumber; 
            ParseMosaicGridFile(SectionPath, TileEntry);
        }

#region ISerializable

        public TileGridTransform(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            ImageWidth = info.GetInt32("ImageWidth");
            ImageHeight = info.GetInt32("ImageHeight");
            Number = info.GetInt32("Number");
            TileFileName = info.GetString("TileFileName");
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);

            info.AddValue("TileFileName", TileFileName);
            info.AddValue("ImageWidth", ImageWidth);
            info.AddValue("ImageHeight", ImageHeight);
            info.AddValue("Number", Number);
        }

#endregion

        #region .mosaic Parsing code

        /// <summary>
        /// Load mosaic from specified file and add it to transforms list using specified key
        /// </summary>
        /// <param name="file"></param>
        /// <param name="Key"></param>
        public static TileGridTransform[] LoadMosaic(string path, string[] mosaic)
        {
            string[] parts;
            int numTiles = 0;

            int formatVersion = 0;
            int iTileStart = 3;
            double PixelSpacing = 0;
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
                    PixelSpacing = System.Convert.ToDouble(parts[1]);
                }
                if (line.StartsWith("image:"))
                {
                    iTileStart = i;
                    break;
                }
            }

            Trace.WriteLine("Loading " + numTiles.ToString() + " tiles", "Geometry");
            List<TileGridTransform> tileTransforms = new List<TileGridTransform>(numTiles);

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

                    TileGridTransform newTGT = new TileGridTransform(path, Transform, TileFileName, iTileNumber);

                    newTGT.ControlSection = iTileNumber;
                    newTGT.MappedSection = iTileNumber;
                    tileTransforms.Add(newTGT);
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

                    //Viking originally used a format with section.number.png, but Iris may write number.png instead
                    if (TileNameParts.Length == 3)
                    {
                         
                         iTileNumber = System.Convert.ToInt32(TileNameParts[1]);
                    }
                    else
                    {
                        iTileNumber = System.Convert.ToInt32(TileNameParts[0]);
                    }

                    

                    //Get the second entry which is the transform 
                    string[] transformParts = Transform.Split(new char[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
                    TileGridTransform newTGT = new TileGridTransform(path, Transform,TileFileName, iTileNumber);

                    newTGT.ControlSection = iTileNumber;
                    newTGT.MappedSection = iTileNumber;
                    tileTransforms.Add(newTGT);
                }
            }
            else
            {
                Debug.Assert(false, "Unknown format version in mosaic file");
                return new TileGridTransform[0];
            }

            GridRectangle R = CalculateBounds(tileTransforms.ToArray());

            foreach (GridTransform T in tileTransforms)
            {
                //Adjusting to center here breaks vclume tranformation since volume transforms assume mosaic origin of 0,0
                //T.Translate(new Vector2(-R.X - (R.Width / 2), -R.Y - (R.Height / 2))); 
                T.Translate(new GridVector2(-R.Left, -R.Bottom));
            }

            //Add transform list to our collection of transforms
            return tileTransforms.ToArray();
        }

        #endregion 

        public void ParseMosaicGridFile(string SectionPath, string transform)
        {
            string[] parts = transform.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            string transformType = parts[0];

            switch (transformType)
            {
                case "GridTransform_double_2_2":
                    ParseGridTransform(parts);
                    break; 
                case "LegendrePolynomialTransform_double_2_2_1":
                    ParsePolyTransform(SectionPath, parts); 
                    break;
                case "TranslationTransform_double_2_2":
                    ParseTranslateTransform(SectionPath, parts); 
                    break; 
                default:
                    Debug.Assert(false, "Unexpected transform type: " + transformType);
                    break; 
            }
        }


        public void ParsePolyTransform(string SectionPath, string[] parts)
        {
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

//            string filename = System.IO.Path.GetFileName(parts[1]);
//            string[] fileparts = filename.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
//            this.Number = System.Convert.ToInt32(fileparts[1]); 

            //Figure out tile size
            ImageWidth = System.Convert.ToInt32(System.Convert.ToDouble(parts[iFixedParameters + 4])) * 2;
            ImageHeight = System.Convert.ToInt32(System.Convert.ToDouble(parts[iFixedParameters + 5])) * 2;

            //The poly transform parameters dictate the center of the image
            double x = System.Convert.ToDouble(parts[iFixedParameters + 2]) - (ImageWidth / 2);
            double y = System.Convert.ToDouble(parts[iFixedParameters + 3]) - (ImageHeight / 2);

            GridVector2 ctrlBotLeft = new GridVector2(x, y);
            GridVector2 ctrlBotRight = new GridVector2(x + ImageWidth, y);
            GridVector2 ctrlTopLeft = new GridVector2(x, y + ImageHeight);
            GridVector2 ctrlTopRight = new GridVector2(x + ImageWidth, y + ImageHeight);

            GridVector2 mapBotLeft = new GridVector2(0, 0);
            GridVector2 mapBotRight = new GridVector2(ImageWidth, 0);
            GridVector2 mapTopLeft = new GridVector2(0, ImageHeight);
            GridVector2 mapTopRight = new GridVector2(ImageWidth, ImageHeight);

            int iStart = mapPoints.Length; 

            MappingGridVector2 BotLeft = new MappingGridVector2(ctrlBotLeft, mapBotLeft);
            MappingGridVector2 BotRight = new MappingGridVector2(ctrlBotRight, mapBotRight);
            MappingGridVector2 TopLeft = new MappingGridVector2(ctrlTopLeft, mapTopLeft);
            MappingGridVector2 TopRight = new MappingGridVector2(ctrlTopRight, mapTopRight);

            this.mapPoints = new MappingGridVector2[] {BotLeft, BotRight, TopLeft, TopRight};
        }

        public void ParseTranslateTransform(string SectionPath, string[] parts)
        {
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

            //string filename = System.IO.Path.GetFileName(parts[1]);
            
            //Figure out tile size if we haven't already
            ImageWidth = System.Convert.ToInt32(parts[iFixedParameters + 4]) * 2;
            ImageHeight = System.Convert.ToInt32(parts[iFixedParameters + 5]) * 2;
            
            double x = System.Convert.ToDouble(parts[iVariableParameters + 2]);
            double y = System.Convert.ToDouble(parts[iVariableParameters + 3]);

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

            this.mapPoints =  new MappingGridVector2[] { BotLeft, BotRight, TopLeft, TopRight };
        }

        public void ParseGridTransform(string[] parts)
        {
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

            //string filename = System.IO.Path.GetFileName(parts[1]);
            //string[] fileparts = filename.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            //this.Number = System.Convert.ToInt32(fileparts[1]); 

            int gridWidth = System.Convert.ToInt32(System.Convert.ToDouble(parts[iFixedParameters + 3]) + 1.0);
            int gridHeight = System.Convert.ToInt32(System.Convert.ToDouble(parts[iFixedParameters + 4]) + 1.0);

            ImageHeight = System.Convert.ToInt32(System.Convert.ToDouble(parts[iFixedParameters + 8]));
            ImageWidth = System.Convert.ToInt32(System.Convert.ToDouble(parts[iFixedParameters + 7]));

            this.MappedBounds.Left = 0;
            this.MappedBounds.Bottom = 0; 
            this.MappedBounds.Right = ImageWidth;
            this.MappedBounds.Top = ImageHeight;

            int NumPts = System.Convert.ToInt32(parts[2]) / 2;
            GridVector2[] Points = new GridVector2[System.Convert.ToInt32(NumPts)];

 //           verticies = new VertexPositionNormalTexture[numPts];

            Double minX = Double.MaxValue;
            Double minY = Double.MaxValue;
            Double maxX = Double.MinValue;
            Double maxY = Double.MinValue;

            //Every number in the array is seperated by an empty space in the array
            for (int i = 0; i < NumPts; i++)
            {
                int iPoint = 3 + (i * 2);
                Double x = System.Convert.ToDouble(parts[iPoint]);
                Double y = System.Convert.ToDouble(parts[iPoint + 1]);

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

            List<int> indicies = new List<int>();
            List<MappingGridVector2> mapList = new List<MappingGridVector2>(gridHeight * gridWidth);
            List<int> triangleIndicies = new List<int>(); 

            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    int i = x + (y * gridWidth);

                    GridVector2 mapPoint = CoordinateFromGridPos(x, y, gridWidth, gridHeight);
                    GridVector2 ctrlPoint = Points[i];

                    MappingGridVector2 gridPoint = new MappingGridVector2(ctrlPoint, mapPoint);
                    mapList.Add(gridPoint); 
                    /*
                    Triangle controlOne = new Triangle(Points[i], Points[i + 1], Points[i + gridWidth]);

                    Vector2 p1 = CoordinateFromGridPos(x, y, gridWidth, gridHeight);
                    Vector2 p2 = CoordinateFromGridPos(x + 1, y, gridWidth, gridHeight);
                    Vector2 p3 = CoordinateFromGridPos(x, y + 1, gridWidth, gridHeight);
                    Triangle mappedOne = new Triangle(p1, p2, p3);

                    mappings.Add(new MappingTriangle(controlOne, mappedOne));

                    Triangle controlTwo = new Triangle(Points[i + 1], Points[i + gridWidth + 1], Points[i + gridWidth]);

                    Vector2 p4 = CoordinateFromGridPos(x + 1, y, gridWidth, gridHeight);
                    Vector2 p5 = CoordinateFromGridPos(x + 1, y + 1, gridWidth, gridHeight);
                    Vector2 p6 = CoordinateFromGridPos(x, y + 1, gridWidth, gridHeight);
                    Triangle mappedTwo = new Triangle(p4, p5, p6);

                    mappings.Add(new MappingTriangle(controlTwo, mappedTwo));
                    */
                }
            }

            for (int y = 0; y < gridHeight-1; y++)
            {
                for (int x = 0; x < gridWidth-1; x++)
                {
                    int botLeft = x + (y * gridWidth);
                    int botRight = (x+1) + (y * gridWidth);
                    int topLeft = x + ((y+1) * gridWidth);
                    int topRight = (x+1) + ((y+1) * gridWidth);


                    int[] triangles = new int[] { botLeft, botRight, topLeft, botRight, topRight, topLeft };
                    triangleIndicies.AddRange(triangles); 
                }
            }
 
            this.mapPoints = mapList.ToArray();

            this.SetPointsAndTriangles(mapList.ToArray(), triangleIndicies.ToArray());
            
//            this.gridIndicies = indicies;
        }

        

        
    }
}
