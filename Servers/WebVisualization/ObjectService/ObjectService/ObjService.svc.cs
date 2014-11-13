using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Xml.Linq;
using System.Xml;
using System.Security;

namespace ObjectService
{

    public class ObjService : IObjService
    {
        public string GetObject(string server, string database, string cell, string type, Boolean update, string virtualUserRoot, string globalPath, string userPath) // type could be.obj or .dae
        {

            type = "." + type;

            string[] cells = cell.Trim().Split(',');
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

            StringBuilder returnPaths = new StringBuilder();
            string workingDirectory = userPath;

            string filePrefix = server + "_" + database + "_" + cell + ".dae";

            string repositoryFilePath = writePath + filePrefix;
            string globalFilePath = globalPath + filePrefix;
            string userFilePath = userPath + filePrefix ;

            // if file exists and the user has not requested for an update, copy it in his directory and send him the file
            if (!update)
            {
                foreach (string cellID in cells)
                {
                    filePrefix = server + "_" + database + "_" + cellID + ".dae";
                    repositoryFilePath = writePath + filePrefix;
                    globalFilePath = globalPath + filePrefix;
                    userFilePath = userPath + filePrefix;

                    if ((File.Exists(globalFilePath) || File.Exists(repositoryFilePath)))
                    {
                        if (!File.Exists(globalFilePath))
                            File.Copy(repositoryFilePath, globalFilePath);

                        if (File.Exists(userFilePath))
                            File.Delete(userFilePath);

                        File.Copy(globalFilePath, userFilePath);

                        if (!File.Exists(globalFilePath.Replace(".dae", ".json")))
                        {
                            string path = convertToJSON(globalFilePath, userPath, server + "_" + database + "_" + cellID);

                            if (!File.Exists(globalFilePath.Replace(".dae", ".json")))
                                File.Copy(path, globalFilePath.Replace(".dae", ".json"));
                        }
                        else
                        {
                            if (File.Exists(userFilePath.Replace(".dae", ".json")))
                                File.Delete(userFilePath.Replace(".dae", ".json"));

                            File.Copy(globalFilePath.Replace(".dae", ".json"), userFilePath.Replace(".dae", ".json"));
                        }



                        returnPaths.Append(virtualUserRoot + filePrefix.Replace(".dae", ".json") + ",");

                    }
                    else
                    {
                        missingCells.Add(cellID);
                    }
                }
            }
            else
            {
                foreach (string cellID in cells)
                {
                    missingCells.Add(cellID);
                }
            }

            if (missingCells.Count == 0)
            {
                return returnPaths.ToString().Substring(0, returnPaths.Length - 1);
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
         
            Dictionary<string, ProcessStartInfo> startInfos = new Dictionary<string, ProcessStartInfo>();

            foreach (string id in missingCells) // for each cell that needs an obj created
            {

                ProcessStartInfo startInfo = new ProcessStartInfo(@"E:\src\VikingPlot\VikingPlot.exe");


                startInfo.RedirectStandardInput = true;
                startInfo.UseShellExecute = true;
                startInfo.WorkingDirectory = @"E:\src\VikingPlot\";
                startInfo.CreateNoWindow = true;
                startInfo.ErrorDialog = true; 
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;
                startInfo.RedirectStandardInput = true;

                string outputType = "-ColladaPath";
                if (type == ".obj")
                    outputType = "-ObjPath";

                startInfo.Arguments = " " + id + " -Server " + server + " -Database " + database + " -RenderMode 0 " + outputType + " " + userPath + id + "\\";

                DeleteDirectory(userPath + id + "\\");

                Directory.CreateDirectory(userPath + id);

                startInfos[id] = startInfo;
            }

            foreach (KeyValuePair<string, ProcessStartInfo> process in startInfos)
            {

                Process p = new Process();
                p = Process.Start(process.Value);
                p.EnableRaisingEvents = true;
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

                //FindAndKillProcess("mpiexec");
            }
             

            missingCells.RemoveRange(0, missingCells.Count);

            foreach (string id in cells)
            {


                filePrefix = server + "_" + database + "_" + id + ".dae";
                repositoryFilePath = writePath + filePrefix;
                globalFilePath = globalPath + filePrefix;
                userFilePath = userPath + filePrefix;

                string filepath = writePath + id + "\\" + id + type;

                string generatedPath = userPath + id + "\\" + id + type;

                if (File.Exists(generatedPath))
                {
                    string userGen = createFinalCollada(userPath, id, filePrefix);
                    if (File.Exists(globalFilePath))
                        File.Delete(globalFilePath);

                    File.Copy(userGen, globalFilePath);

                    if (File.Exists(writePath + filePrefix + ".dae"))
                        File.Delete(writePath + filePrefix + ".dae");

                    File.Copy(globalFilePath, writePath + filePrefix + ".dae");

                    continue;
                }
                missingCells.Add(id);
            }

            if (missingCells.Count == 0)
            {
                returnPaths.Remove(0, returnPaths.Length);

                foreach (string id in cells)
                {
                    convertToJSON(globalFilePath, userPath, server + "_" + database + "_" + id);
                    returnPaths.Append(virtualUserRoot + server + "_" + database + "_" + id + ".json" + ",");
                }

                return returnPaths.ToString().Substring(0, returnPaths.Length - 1);
            }

            return "problem";
        }

        public EventHandler process_Exited { get; set; }

        static void build_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            string strMessage = e.Data;

            Console.WriteLine(e.Data);
        }


        private string createFinalCollada(string writePath, string id, string filePrefix)
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

            if (File.Exists(writePath + filePrefix))
                File.Delete(writePath + filePrefix);

            string write = sr.ReadToEnd().Replace("CommonGeometry.dae", "").Replace(id + ".dae", "").Replace("Materials.dae", "").Replace("Scene.dae", "");
            FileStream writeFile = new FileStream(writePath + filePrefix, FileMode.CreateNew, FileAccess.Write);
            StreamWriter sw = new StreamWriter(writeFile);
            sw.Write(write);
            sw.Flush();
            sw.Close();
            writeFile.Close();

            sr.Close();
            f.Close();

            return writePath + filePrefix;

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

        public static void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target)
        {
            foreach (DirectoryInfo dir in source.GetDirectories())
                CopyFilesRecursively(dir, target.CreateSubdirectory(dir.Name));
            foreach (FileInfo file in source.GetFiles())
                file.CopyTo(Path.Combine(target.FullName, file.Name));
        }

        public static string convertToJSON(string sourceFile, string destinationPath, string fileName)
        {
            string outputPath = destinationPath  + fileName;
            string destinationFile = outputPath + ".o3dtgz";

            string sourcePath = sourceFile;
           

            //if (File.Exists(outputPath + ".dae"))
            //    File.Delete(outputPath + ".dae");
            //File.Copy(sourceFile, outputPath + ".dae");

            Process p = new Process();
            p.StartInfo.FileName = @"E:\src\o3D_Converter\o3dConverter.exe";

            p.StartInfo.Arguments = "--no-condition --up-axis=0,1,0 --no-binary --no-archive --convert-dds-to-png " + outputPath + ".dae" + " " + destinationFile;
            p.StartInfo.UseShellExecute = false;


            //p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            try
            {
                p.Start();
                p.WaitForExit(100000);

            }
            catch (Exception e)
            { }

            if (File.Exists(destinationPath + fileName + ".json"))
                File.Delete(destinationPath + fileName + ".json");

            File.Move(outputPath + "\\scene.json", destinationPath + fileName + ".json");
            if(File.Exists(sourceFile.Replace(".dae", ".json")))
                File.Delete(sourceFile.Replace(".dae", ".json"));

            File.Copy(destinationPath + fileName + ".json", sourceFile.Replace(".dae", ".json"));
            Directory.Delete(outputPath);

            return destinationPath + fileName + ".json";
        }


        public bool FindAndKillProcess(string name)
        {
            //here we're going to get a list of all running processes on
            //the computer
            foreach (Process clsProcess in Process.GetProcesses())
            {
                //now we're going to see if any of the running processes
                //match the currently running processes by using the StartsWith Method,
                //this prevents us from incluing the .EXE for the process we're looking for.
                //. Be sure to not
                //add the .exe to the name you provide, i.e: NOTEPAD,
                //not NOTEPAD.EXE or false is always returned even if
                //notepad is running
                if (clsProcess.ProcessName.StartsWith(name))
                {
                    //since we found the proccess we now need to use the
                    //Kill Method to kill the process. Remember, if you have
                    //the process running more than once, say IE open 4
                    //times the loop thr way it is now will close all 4,
                    //if you want it to just close the first one it finds
                    //then add a return; after the Kill
                    clsProcess.Kill();
                    //process killed, return true
                    return true;
                }
            }
            //process not found, return false
            return false;
        }
       
    }
}
