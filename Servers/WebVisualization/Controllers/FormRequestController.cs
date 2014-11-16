using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Diagnostics;
using ConnectomeViz.Models;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Net.Mail;
using System.Web.Security;
using System.Net;
using AnnotationVizLib.AnnotationService;

using ConnectomeViz.Helpers;

namespace ConnectomeViz.Controllers
{
    public class FormRequestController : Controller
    {
        [AcceptVerbs(HttpVerbs.Get)]
        public ActionResult GetVolumes(string server)
        {
            if (server == null || !ConnectomeViz.Models.State.ServerToEndpointURLBase.ContainsKey(server))
                return Json("No volumes found on " + server, JsonRequestBehavior.AllowGet);

            return Json(ConnectomeViz.Models.State.ServerToEndpointURLBase[server].Volumes, JsonRequestBehavior.AllowGet);
        }


        [AcceptVerbs(HttpVerbs.Get)]
        public ActionResult GetTopStructures(string request)
        {   
            var res = Request;

            string[] query = request.Split(',');

            string serverName = query[0];

            string volumeName = query[1];

            int type = Int32.Parse(query[2]); 

            string applicationPath = HttpContext.Request.ApplicationPath;
            if (applicationPath == "/")
                applicationPath = "";
            State.virtualRoot = "http://" + HttpContext.Request.Url.Authority + applicationPath;

            string workingDirectory = Server.MapPath("~");
            State.filesPath = workingDirectory;

            State.ReadServices();

            ConnectomeViz.Models.State.selectedServer = serverName;

            ConnectomeViz.Models.State.selectedVolume = volumeName;

            string[] result = new string[0];

            using (AnnotationVizLib.AnnotationService.CircuitClient client = ConnectomeViz.Models.State.CreateNetworkClient())
            {

                result = client.getTopConnectedStructures(type); // 1 for structures and 0 for locations

                client.Close();
            }

            List<string> temp = new List<string>(result);

            if(type == 1) // if its structures,sort by names since the received data is sorted by connections count
                temp.Sort();    
        
            List<string> sendList = new List<string>(temp.Count + 1);
       
            sendList.Add(ConnectomeViz.Models.State.selectedVolume + " - " + "IDs Ordered by Type"); 

            int length = temp.Count;
            for (int i = 0; i < length; i++)
            {
                sendList.Add(temp[i]);
            }

            return Json(sendList, JsonRequestBehavior.AllowGet);
        }

        [AcceptVerbs(HttpVerbs.Get)]
        public ActionResult GetStructureLabelsForType(string request)
        {
            
            var res = Request;

            string[] query = request.Split(',');

            string serverName = query[0]; 
            string volumeName = query[1];

            //TODO: Hardcoded to cells for now...
            long type = 1;

            string applicationPath = HttpContext.Request.ApplicationPath;
            if (applicationPath == "/")
                applicationPath = "";
            State.virtualRoot = "http://" + HttpContext.Request.Url.Authority + applicationPath;

            string workingDirectory = Server.MapPath("~");
            State.filesPath = workingDirectory;

            State.ReadServices();

            ConnectomeViz.Models.State.selectedServer = serverName.ToString();

            ConnectomeViz.Models.State.selectedVolume = volumeName.ToString();

            SortedList<string, List<Structure>> dictLabels = AnnotationQueries.LabelToStructuresMap(type);

            List<string> sendList = new List<string>(dictLabels.Count+1);
       
            sendList.Add(ConnectomeViz.Models.State.selectedVolume + " - Unique labels");
             
            sendList.AddRange(dictLabels.Keys);

            return Json(sendList, JsonRequestBehavior.AllowGet);
            
        }

        public ActionResult UploadFile(string path)
        {
            return Content("http://connectomes.utah.edu/test/files/anonymous/155.100.105.9_Rabbit_3679.json,http://connectomes.utah.edu/test/files/anonymous/155.100.105.9_Rabbit_476.json,http://connectomes.utah.edu/test/files/anonymous/155.100.105.9_Rabbit_514.json");

            string applicationPath = HttpContext.Request.ApplicationPath;
            if (applicationPath == "/")
                applicationPath = "";
            string virtualRoot = "http://" + HttpContext.Request.Url.Authority + applicationPath;
            ViewData["virtualRoot"] = virtualRoot;

            Regex exp = new Regex(@"http://.*/(.*?).dae", RegexOptions.IgnoreCase);
            string[] fileName = exp.Split(path);
            Match match = exp.Match(path);
            string name = match.Groups[1].ToString();

            string workingDirectory = Server.MapPath("~");
            ViewData["workDirectory"] = workingDirectory;
            string userURL = virtualRoot + "/Files/" + HttpContext.User.Identity.Name;  

            string userPath = workingDirectory + "\\Files\\" + HttpContext.User.Identity.Name;

            string outputPath =userPath+"\\"+ name;
            string sourcePath = outputPath + ".dae";
            string destinationPath = outputPath + ".o3dtgz";

            if (!System.IO.Directory.Exists(userPath))
                System.IO.Directory.CreateDirectory(userPath);

            if(System.IO.File.Exists(sourcePath))
              System.IO.File.Delete(sourcePath);

            if(System.IO.Directory.Exists(outputPath))
                DeleteDirectory(outputPath);
          

            WebClient webClient = new WebClient();
            webClient.DownloadFile(path, sourcePath);

            while (!System.IO.File.Exists(sourcePath))
                continue;
          

            Process p = new Process();
            p.StartInfo.FileName = @"E:\src\o3D_Converter\o3dConverter.exe";

            p.StartInfo.Arguments = "--no-condition --up-axis=0,1,0 --no-binary --no-archive --convert-dds-to-png " + sourcePath + " " + destinationPath;
            p.StartInfo.UseShellExecute = false;
           
        
            //p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            try
            {
                p.Start();
                p.WaitForExit();
                
            }
            catch (Exception e)
            { }

            ViewData["colladaURL"] = userURL + "/" + name + ".dae";
   
            //return Content(userURL+"/"+ name +"/scene.json");
            return Content("http://connectomes.utah.edu/test/files/shoeb/155.100.105.9_Rabbit_476.json,http://connectomes.utah.edu/test/files/shoeb/155.100.105.9_Rabbit_514.json");
        }

        public static bool DeleteDirectory(string target_dir)
        {
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
         
      
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult ConvertToJPG()
        {
            var saveFile = processSVG();
            convertFile(saveFile, "jpg");

            if(System.IO.File.Exists(State.userFile + "_save.jpg"))
                return Content(State.userURL + "_save.jpg");

            return Content("error"); 
        }

        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult ConvertToPNG()
        {
            var saveFile = processSVG();
            convertFile(saveFile, "png");

            //Response.ClearHeaders();
            //Response.ClearContent();
            //Response.Clear();
            //Response.AddHeader("content-disposition", "inline;filename=" + State.userFileName +".png");
            //Response.ContentType = "image";
            //Response.WriteFile(saveFile + ".png");
            //Response.Flush();
            //Response.End();

            //return Content("x");


            //Response.ClearContent();
            //Response.AppendHeader("Content-Disposition", "attachment; filename=graph.png");
            //Response.TransmitFile(State.userFile+ "_save.png");
            //Response.ContentType = "image/png";     
            //Response.End();

            //return View("Index");

            if (System.IO.File.Exists(State.userFile + "_save.png"))
                return Content(State.userURL + "_save.png");

            return Content("error");

         }
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult ConvertToPDF()
        {
            var saveFile = processSVG();
            convertFile(saveFile, "pdf");

            //Response.ClearHeaders();
            //Response.ClearContent();
            //Response.Clear();
            //Response.AddHeader("content-disposition", "inline;filename=" + State.userFileName + ".pdf");
            //Response.ContentType = "image/svg+xml";
            //Response.WriteFile(saveFile + ".pdf");
            //Response.Flush();
            //Response.End();

            //return Content("x");

           if(System.IO.File.Exists(State.userFile + "_save.pdf"))
                return Content(State.userURL + "_save.pdf");

            return Content("error");

        }

        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult ConvertToDOT()
        {
            var saveFile = State.userURL + ".dot";
        
            //Response.ClearHeaders();
            //Response.ClearContent();
            //Response.Clear();
            //Response.AddHeader("content-disposition", "inline;filename=" + State.userFileName + ".pdf");
            //Response.ContentType = "image/svg+xml";
            //Response.WriteFile(saveFile + ".pdf");
            //Response.Flush();
            //Response.End();

            //return Content("x");

            if (System.IO.File.Exists(State.userFile + ".dot"))
                return Content(saveFile);

            return Content("error");
        }

        
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult ConvertToSVG()
        {
            var saveFile = processSVG();
            //convertFile(saveFile, "svg");

            //Response.ClearHeaders();
            //Response.ClearContent();
            //Response.Clear();
            //Response.AddHeader("content-disposition", "inline;filename=" + State.userFileName + ".svg");
            //Response.ContentType = "image/svg+xml";
            //Response.WriteFile(saveFile + ".svg");
            //Response.Flush();
            //Response.End();
            //return Content("x");

            //Response.ClearContent();
            //Response.AddHeader("content-disposition", "attachment; filename=" + "graph.svg");
            //Response.ContentType = "image/svg+xml";
            //Response.Write(State.userFile + "_save.svg");
            //Response.End();



          if(System.IO.File.Exists(State.userFile + "_save.svg")) 
                return Content(State.userURL + ".svg", "image/svg+xml");

            return Content("error");
        }

        

        string processSVG()
        {
            HttpContextBase httpContext = HttpContext;

            int scale = 1;
            
            if (!httpContext.IsPostNotification)
            {
                throw new InvalidOperationException("Only POST messages allowed on this resource");
            }
            Stream httpBodyStream = httpContext.Request.InputStream;

            if (httpBodyStream.Length > int.MaxValue)
            {
                throw new ArgumentException("HTTP InputStream too large.");
            }

            int streamLength = Convert.ToInt32(httpBodyStream.Length);
            byte[] byteArray = new byte[streamLength];
            const int startAt = 0;

            /*
             * Copies the stream into a byte array
             */
            httpBodyStream.Read(byteArray, startAt, streamLength);

            /*
             * Convert the byte array into a string
             */
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < streamLength; i++)
            {
                sb.Append(Convert.ToChar(byteArray[i]));
            }

            string xmlBody = sb.ToString();

            //Sends XML Data To Model so it could be available on the ActionResult

            //Map working directory


            var fileLocation = State.userFile;

            //FileStream storedFile = new FileStream(fileLocation + ".svg", FileMode.Open);
            //StreamReader sr = new StreamReader(storedFile);
            //StringBuilder contents = new StringBuilder(sr.ReadToEnd());
            //sr.Close();
            //storedFile.Close();

            ////Regex regex = new Regex("svg width=\"(.*?)pt\" height=\"(.*?)pt\"");

            //Regex regex = new Regex("viewBox=\"0.00 0.00 (.*?) (.*?)\"");
            //Match match = regex.Match(contents.ToString());
            //Double  w= 0, h = 0;
            //// Here we check the Match instance.
            //if (match.Success)
            //{
            //    // Finally, we get the Group value and display it.
            //    string width = match.Groups[1].Value;
            //    w = Double.Parse(width.Replace("\"",""));
            //    w = w /scale;
            //    string height = match.Groups[2].Value;
            //    h = Double.Parse(height.Replace("\"", ""));
            //    h = h /scale;

            //}

            //xmlBody = xmlBody.Replace("<svg", "<svg width=\""+ w +"pt\" height=\"" + h +"pt\"");

            //xmlBody = xmlBody.Replace("<svg", "<svg viewBox=\"0.00 0.00 "  + w + " " + h + "\" ");


            string saveFile = fileLocation + "_save" ;
            FileStream f1 = new FileStream(saveFile +".svg", FileMode.Create);
            StreamWriter sw = new StreamWriter(f1);
            sw.Write(xmlBody);
            sw.Close();
            f1.Close();

            return (saveFile);
        }

        void convertFile(string saveFile, string type)
        {
          
            Process p = new Process();
            

            p.StartInfo.FileName = "inkscape.exe";
            string typeArgument = "-e";

            if (type.ToLower() == "pdf")
                typeArgument = "-A";
                    

            p.StartInfo.Arguments = " -f " + saveFile + ".svg" + " -D "+ typeArgument + " " + saveFile +"."+ type;
            p.StartInfo.UseShellExecute = false;

            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.Start();
            p.WaitForExit(10000);
            if (p.ExitCode != 0)
            {
                Debug.Write("Error");
            }
            p.Close();
        }

       
            

        public ActionResult GetNetworkJSON()
        {
            string globalFile = State.globalPath +"\\"+ State.userFileName +".json";
            string userFile = State.userFile + ".json";
            string path = "";
            if (State.networkFreshQuery)
                path = userFile;
            else
                path = globalFile;
                    

            
            FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            StreamReader sr = new StreamReader(fs);
            string obj = sr.ReadToEnd().ToString();
            JavaScriptSerializer deserialize = new JavaScriptSerializer();
            var sendJson = deserialize.Deserialize<object>(obj);

            
            return Json(sendJson, JsonRequestBehavior.AllowGet);
        }

        [ValidateInput(false)]
        public ActionResult ExportToExcel()
        {

            string csvBuffer = null;
            csvBuffer = Request["gridData"].ToString();
            /*
            try
            {
                csvBuffer = Request["gridData"].ToString();
            }
            catch (HttpRequestValidationException error)
            {
            }
             */
            /*
            int iAmpersand = csvBuffer.IndexOf('&');
            while (iAmpersand >= 0)
            {
                if(iAmpersand >= csvBuffer.Length - 3)
                    break;

                if (csvBuffer[iAmpersand + 1] == '%')
                {

                    switch (csvBuffer.Substring(iAmpersand + 1, 3))
                    {
                        case "%3C":
                            csvBuffer.Remove(iAmpersand, 4);
                            csvBuffer.Insert(iAmpersand, "<");
                            break;
                        case "%5B":
                            csvBuffer.Remove(iAmpersand, 4);
                            csvBuffer.Insert(iAmpersand, "[");
                            break;
                        case "%5C":
                            csvBuffer.Remove(iAmpersand, 4);
                            csvBuffer.Insert(iAmpersand, "]");
                            break;
                    }
                }

                int oldAmpersand = iAmpersand;
                iAmpersand = (csvBuffer.Substring(iAmpersand + 1)).IndexOf('&');

                if (iAmpersand >= 0)
                    iAmpersand += oldAmpersand; 
            }
            */

            csvBuffer = csvBuffer.Replace("&%3C", "<");
            csvBuffer = csvBuffer.Replace("&%5B", "[");
            csvBuffer = csvBuffer.Replace("&%5C", "]");

            HttpContextBase httpContext = HttpContext;

            int scale = 1;

            if (!httpContext.IsPostNotification)
            {
                throw new InvalidOperationException("Only POST messages allowed on this resource");
            }
            Stream httpBodyStream = httpContext.Request.InputStream;

            if (httpBodyStream.Length > int.MaxValue)
            {
                throw new ArgumentException("HTTP InputStream too large.");
            }

            int streamLength = Convert.ToInt32(httpBodyStream.Length);
            byte[] byteArray = new byte[streamLength];
            const int startAt = 0;

            /*
             * Copies the stream into a byte array
             */
            httpBodyStream.Read(byteArray, startAt, streamLength);

            /*
             * Convert the byte array into a string
             */
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < streamLength; i++)
            {
                sb.Append(Convert.ToChar(byteArray[i]));
            }

            string xmlBody = sb.ToString();

            //Sends XML Data To Model so it could be available on the ActionResult

            //Map working directory


            

            //Sends XML Data To Model so it could be available on the ActionResult

            //var products = _productRepository.GetAll();

            //var grid = new GridView();
            //grid.DataSource = from p in products
            //                  select new
            //                             {
            //                                 ProductName = p.ProductName,
            //                                 SomeProductId = p.ProductID
            //                             };
            //grid.DataBind();

            Response.ClearContent();
            Response.AddHeader("content-disposition", "attachment; filename=GridData.csv");

            Response.ContentType = "application/excel";

            //StringWriter sw = new StringWriter();

            //HtmlTextWriter htw = new HtmlTextWriter(sw);

            //grid.RenderControl(htw);

            if (String.IsNullOrEmpty(csvBuffer))
                csvBuffer = xmlBody;

            Response.Write(csvBuffer);           

            Response.End();

            return View("Index");
        }


        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult SendMessage(string name, string email, string subject, string message)
        {
            MembershipUser user = Membership.GetUser(name);

            string registered = "No";
            string activeUser = "Name";

            if (user != null)
            {
                registered = "Yes";
                activeUser = "UserName";
            }
            
            string messageAppend = "<b>Feedback Message from a user</b>" +
                                    "<br/><br/>" + activeUser + ": " + name +
                                    "<br/><br/>" + "Registered? " + registered +
                                    "<br/><br/>" + "Email: " + email +
                                    "<br/><br/>" + "Message: " + message +
                                    "<br/><br/>" + "--" + "<br/>" + "<a href=\"http://prometheus.med.utah.edu/~marclab/index.html\">Marc Lab, University of Utah</a>";



            SmtpClient ss2 = new SmtpClient("smtp.utah.edu", 25);

            ss2.DeliveryMethod = SmtpDeliveryMethod.Network;

            ss2.EnableSsl = true;

            MailMessage madmin = new MailMessage();

            madmin.From = new MailAddress("MarcLab@utah.edu", " Marc Lab, U of U");

            madmin.Subject = "Feedback: Message from " + name;

            madmin.Body = messageAppend;

            //madmin.To.Add(new MailAddress("james.r.anderson@utah.edu"));

            madmin.To.Add(new MailAddress("hishoeb@gmail.com"));            

            //madmin.CC.Add(new MailAddress("robert.marc@hsc.utah.edu"));


            madmin.IsBodyHtml = true;

            madmin.DeliveryNotificationOptions = DeliveryNotificationOptions.OnFailure;

            ss2.Send(madmin);

            madmin.Dispose();

            madmin = new MailMessage();

            madmin.From = new MailAddress("MarcLab@utah.edu", " Marc Lab, U of U");

            madmin.Subject =  name +" - Thank you for your Message";

            madmin.Body = "<br/>Thank you for your Message" +
                                      "<br/><br/>" + "(This is an automated message, please do not reply)<br/>" + "--" + "<br/>" + "<a href=\"http://prometheus.med.utah.edu/~marclab/index.html\">Marc Lab, University of Utah</a>";


            madmin.To.Add(new MailAddress(email));

            madmin.IsBodyHtml = true;

            madmin.DeliveryNotificationOptions = DeliveryNotificationOptions.OnFailure;

            ss2.Send(madmin);

            madmin.Dispose();


            return Content("success");
        }




     
        

    }

   

}
