using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.IO;
using System.Xml;
using System.Diagnostics;
using System.Net;
using System.Web;
using System.Web.Script.Serialization;

namespace ConsoleApplication3
{
    class Program
    {

        static string server;
        static string database;
        static string Id;
        static string type;
        static bool update;

        static void Main(string[] args)
        {
            //if(args.Length == 0)
            //{
            //    Console.WriteLine("Specify Arguments, for Example: VikingPlotGenerator.exe 155.100.105.9 Rabbit 180 dae true");
            //}
            //if (args.Length >0 && args.Length< 5)
            //{
            //    Console.WriteLine("Check arguments");
            //    Console.ReadLine();
            //}
                

            //foreach (string argument in args)
            //{
               
            //    server = args[0];
            //    database = args[1];
            //    Id = args[2];
            //    type = args[3];
            //    update = args[4] ==  "true" ? true: false;

            //   string result = GetObject(server, database, Id, type, update);

            //   Console.WriteLine(result);
            //}

            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create("http://connectomes.utah.edu/Test/FormRequest/GetTopStructures?request=MarcLab(connectome.utah.edu,Rabbit(MarcLab),0");
            httpWebRequest.Method = WebRequestMethods.Http.Get;
            httpWebRequest.ContentType = "application/json; charset=utf-8";
            string jsonResponse = null;
            var response = (HttpWebResponse)httpWebRequest.GetResponse();
            using (StreamReader sr = new StreamReader(response.GetResponseStream()))
            {
                jsonResponse = sr.ReadToEnd();
            }

            JavaScriptSerializer deserialize = new JavaScriptSerializer();
            object[] sendJson = (object[])deserialize.Deserialize<object>(jsonResponse);

           //string[] cells = sendJson.ToString().Replace("\\","").Replace("/","").Split(',');
            Dictionary<long, long> cellIDs = new Dictionary<long, long>();

            foreach(object cell in sendJson)
            {
                string[] contents = cell.ToString().Split('~');
                if (contents.Length < 3)
                    continue;
                cellIDs[Convert.ToInt32(contents[0])] = Convert.ToInt32(contents[2]);

            }

            Dictionary<long,long> sortedDict = (from entry in cellIDs orderby entry.Value descending select entry).ToDictionary(pair => pair.Key, pair => pair.Value);


            server = "155.100.105.9";
            database = "Rabbit";
            Id = "180";
            type = "dae";
            update = true;
            
            int count =1;
            int length = sortedDict.Keys.Count;

            foreach (KeyValuePair<long, long> row in sortedDict)
            {
                Console.WriteLine("Executing for ID: " + row.Key + " " + count + " of " + length) ;
                count++;
                //if (row.Value < 1000)
                //    break;
                string result = GetObject(server, database, row.Key.ToString(), type, update);
                Console.WriteLine(result);
            }
            
         
        }


        public static string GetObject(string server, string database, string cell, string type, Boolean update = false) // type could be.obj or .dae
        {

            type = "." + type;

            string[] cells = cell.Trim().Split(' ');
            List<string> missingCells = new List<string>();

            string objPath = @"E:\ObjectFiles\";
            string colladaPath = @"E:\ColladaFiles\";

            string colladaLink = "http://connectomes.utah.edu/ColladaFiles/";
            string objLink = "http://connectomes.utah.edu/ObjectFiles/";



            string writePath = colladaPath;
            string returnPath = colladaLink;

            if (type == ".obj")
            {
                writePath = objPath;
                returnPath = objLink;
            }



            //foreach (string id in cells)
            //{
            //    string filepath = writePath + "\\" + id + "\\" + id + type;
            //    if (File.Exists(filepath) && update == false)
            //    {
            //        continue;
            //    }
            //    missingCells.Add(id);
            //}

            //if (missingCells.Count == 0)
            //{

            //    System.Text.StringBuilder sb = new System.Text.StringBuilder();

            //    foreach (string id in cells)
            //    {
            //        sb.Append(returnPath + "/id/" + id + type + ",");
            //    }

            //    string ans = sb.ToString();

            //    return (ans.Substring(0, ans.Length - 1));

            //}

            // There are some missing cells then, create the appropriate files

            if (File.Exists(writePath + server + "_" + database + "_" + cell + ".dae"))
                return "Collada Exists";

            missingCells.Add(cell);

            Dictionary<string, ProcessStartInfo> startInfos = new Dictionary<string, ProcessStartInfo>();

            foreach (string id in missingCells) // for each cell that needs an obj created
            {


               

                ProcessStartInfo startInfo = new ProcessStartInfo(@"E:\src\VikingPlot\VikingPlot.exe");

                Console.WriteLine("Running VikingPlot...");
               
              

                startInfo.UseShellExecute = true;

                startInfo.WorkingDirectory = @"E:\src\VikingPlot\";

                startInfo.CreateNoWindow = false;

                //startInfo.RedirectStandardOutput = true;

                //startInfo.RedirectStandardError = true;

                //startInfo.RedirectStandardInput = true;

                string outputType = "-ColladaPath";
                if (type == ".obj")
                    outputType = "-ObjPath";

                startInfo.Arguments = " " + id + " -Server " + "155.100.105.9" + " -Database " + "Rabbit" + " -RenderMode 0 " + outputType + " " + writePath + id + "\\";

                DeleteDirectory(writePath + id + "\\");

                Directory.CreateDirectory(writePath + id);

                startInfos[id] = startInfo;


            }

            foreach (KeyValuePair<string, ProcessStartInfo> process in startInfos)
            {

                Process p = new Process();

                p = Process.Start(process.Value);

                if (!p.WaitForExit(1000000))
                {
                    p.Kill();
                }

                //while (!p.HasExited)
                //{

                //    StreamWriter inputWriter = p.StandardInput;
                //    inputWriter.Write("\n");
                //    inputWriter.Flush();
                //    inputWriter.Close();

                //}


                p.Close();
            }


            missingCells.RemoveRange(0, missingCells.Count);

            foreach (string id in cells)
            {
                string filepath = writePath + id + "\\" + id + type;
                if (File.Exists(filepath))
                {
                    createFinalCollada(writePath, id);
                    continue;
                }
                missingCells.Add(id);
            }

            if (missingCells.Count == 0)
            {

                System.Text.StringBuilder sb = new System.Text.StringBuilder();

                foreach (string id in cells)
                {
                    sb.Append(returnPath + writePath + server + "_" + database + "_" + id + ".dae" + ",");
                }

                string ans = sb.ToString();

                return (ans.Substring(0, ans.Length - 1));

            }

            return "problem";
        }

        public static EventHandler process_Exited { get; set; }

        static void build_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            string strMessage = e.Data;

            Console.WriteLine(e.Data);
        }


        private static void createFinalCollada(string writePath, string id)
        {
            string pathInQuestion = writePath + id + "\\";


            //foreach (string file in Directory.GetFiles(pathInQuestion))
            //{
            //    File.Move(file, file.ToString().Replace(".xml",".dae"));
            //}

            FileStream f = new FileStream(pathInQuestion + id + ".dae", FileMode.Open, FileAccess.Read);
            StreamReader sr = new StreamReader(f);

            // Encode the XML string in a UTF-8 byte array
            byte[] encodedString = Encoding.UTF8.GetBytes(sr.ReadToEnd());

            // Put the byte array into a stream and rewind it to the beginning
            MemoryStream ms = new MemoryStream(encodedString);
            ms.Flush();
            ms.Position = 0;

            XmlDocument xdoc = new XmlDocument();
            xdoc.Load(ms);

            XmlElement documentRoot = (XmlElement)xdoc.GetElementsByTagName("COLLADA")[0];
            ms.Close();
            f.Close();

            //string stringContent = Encoding.UTF8.GetString(new StringBuilder(sr.ReadToEnd()));


            //File.Delete(pathInQuestion + id + ".dae");

            foreach (string file in Directory.GetFiles(pathInQuestion))
            {

                if (file.Equals(pathInQuestion + id + ".dae"))
                    continue;
                //string newFile= file.ToString().Replace(".dae",".xml");
                //File.Move(file, newFile);

                f = new FileStream(file, FileMode.Open, FileAccess.Read);
                sr = new StreamReader(f);

                // Encode the XML string in a UTF-8 byte array
                encodedString = Encoding.UTF8.GetBytes(sr.ReadToEnd());

                // Put the byte array into a stream and rewind it to the beginning
                ms = new MemoryStream(encodedString);
                ms.Flush();
                ms.Position = 0;

                XmlDocument tempDoc = new XmlDocument();
                tempDoc.Load(ms);

                XmlElement collada = (XmlElement)tempDoc.GetElementsByTagName("COLLADA")[0];
                XmlNodeList children = collada.ChildNodes;
                foreach (XmlNode child in children)
                {
                    XmlElement ele = (XmlElement)child;
                    if (ele.Name.Equals("asset"))
                        continue;

                    XmlNode insert = documentRoot.OwnerDocument.ImportNode(ele, true);
                    documentRoot.AppendChild(insert);
                }

                sr.Close();
                f.Close();


                //File.Delete(file);
            }

            //FileStream f = new FileStream(pathInQuestion + id + ".xml", FileMode.Open, FileAccess.Read);
            //StreamReader sr = new StreamReader(f);

            //// Encode the XML string in a UTF-8 byte array
            //byte[] encodedString = Encoding.UTF8.GetBytes(sr.ReadToEnd());

            //// Put the byte array into a stream and rewind it to the beginning
            //MemoryStream ms = new MemoryStream(encodedString);
            //ms.Flush();
            //ms.Position = 0;

            //XDocument sourceXML = XDocument.Load(pathInQuestion + id + ".xml");


            // Build the XmlDocument from the MemorySteam of UTF-8 encoded bytes



            //XmlDocument xdoc = new XmlDocument();
            //xdoc.LoadXml(sr.ToString());




            var elems = documentRoot.GetElementsByTagName("asset");
            XmlNodeList mainChildren = documentRoot.GetElementsByTagName("geometry");

            foreach (XmlNode child in mainChildren)
            {

                XmlElement mainGeom = (XmlElement)child;

                string geomName = mainGeom.GetAttribute("name").ToString() + "-material";

                XmlElement element = (XmlElement)mainGeom.GetElementsByTagName("triangles")[0];

                int numTriangles = Convert.ToInt32(element.GetAttribute("count"));
                string[] triangles = element.GetElementsByTagName("p")[0].FirstChild.Value.ToString().Split('\n');


                List<string> newTriangles = new List<string>();
                foreach (string triangle in triangles)
                {
                    if (String.IsNullOrEmpty(triangle))
                        continue;
                    string[] vertices = triangle.Trim().Replace("\r", "").Split(' ');
                    newTriangles.Add(vertices[0] + ' ' + vertices[2] + ' ' + vertices[1]);
                }

                StringBuilder finalValue = new StringBuilder();

                foreach (string newTriangle in newTriangles)
                {
                    finalValue.Append(newTriangle + System.Environment.NewLine);
                }
                element.GetElementsByTagName("p")[0].InnerText = finalValue.ToString();



                element.SetAttribute("material", geomName);

            }

            XmlNodeList geometries = documentRoot.GetElementsByTagName("node");

            foreach (XmlNode geom in geometries)
            {
                XmlElement element = (XmlElement)geom;

                string tag = "Struct-" + element.GetAttribute("name").Replace("NodeID-", "") + "-material";

                XmlNode materialElement = element.GetElementsByTagName("instance_material")[0];

                element = (XmlElement)materialElement;

                try
                {
                    element.SetAttribute("symbol", tag);
                }
                catch (Exception e)
                {
                }


            }

            XmlWriter writeXML = XmlWriter.Create(pathInQuestion + id + "big.dae");
            xdoc.WriteTo(writeXML);
            writeXML.Flush();
            writeXML.Close();

            //if (File.Exists(pathInQuestion + id + "big.dae"))
            //    File.Delete(pathInQuestion + id + "big.dae");

            f = new FileStream(pathInQuestion + id + "big.dae", FileMode.Open, FileAccess.Read);
            sr = new StreamReader(f);

            //System.IO.File.Delete(pathInQuestion + id + ".dae");

            string filePrefix = server + "_" + database + "_" + id + ".dae";

            if (File.Exists(writePath + filePrefix))
                File.Delete(writePath + filePrefix);

            string write = sr.ReadToEnd().Replace("CommonGeometry.dae", "").Replace(id + ".dae", "").Replace("Materials.dae", "").Replace("Scene.dae", "");
            FileStream writeFile = new FileStream((writePath + server + "_" + database + "_" + id + ".dae"), FileMode.CreateNew, FileAccess.Write);
            StreamWriter sw = new StreamWriter(writeFile);
            sw.Write(write);
            sw.Flush();
            sw.Close();
            writeFile.Close();

            sr.Close();
            f.Close();

            //File.Move(pathInQuestion + id + "final.dae", pathInQuestion + id + "final.xml"); 


            //MemoryStream stream = new MemoryStream();
            //byte[] data = Encoding.UTF8.GetBytes(sr.ReadToEnd());
            //stream.Write(data, 0, data.Length);
            //stream.Seek(0, SeekOrigin.Begin);

            //XmlTextReader reader = new XmlTextReader(stream);

            //// MSDN reccomends we use Load instead of LoadXml when using in memory XML payloads
            //XmlDocument doc = new XmlDocument();
            //doc.Load(reader);
            //var ans = doc.GetElementsByTagName("COLLADA");     


            ////XDocument sourceXML = XDocument.Parse(ms.ToString());

            ////var elements = sourceXML.Descendants("COLLADA").First();

            //XDocument plain = XDocument.Load(pathInQuestion + id + ".xml");

            //var secEls = plain.Descendants("COLLADA").First();



            //XDocument finalXML = new XDocument();

            //File.Move(pathInQuestion + id + ".dae", pathInQuestion + id + ".dae");


            //foreach (string file in Directory.GetFiles(pathInQuestion))
            //{
            //    sourceXML.Root.Add(XDocument.Load(file).Root.Elements());

            //    //File.Delete(file);
            //}





            //File.Move(pathInQuestion + id + ".xml", pathInQuestion + id + ".dae");

            //Combine and remove duplicates

        }

        public static bool DeleteDirectory(string target_dir)
        {
            if (!Directory.Exists(target_dir))
                return true;

            bool result = false;

            string[] files = Directory.GetFiles(target_dir);
            string[] dirs = Directory.GetDirectories(target_dir);

            foreach (string file in files)
            {
                System.IO.File.SetAttributes(file, FileAttributes.Normal);
                System.IO.File.Delete(file);
            }

            foreach (string dir in dirs)
            {
                DeleteDirectory(dir);
            }

            Directory.Delete(target_dir, false);

            return result;
        }

    }
}
