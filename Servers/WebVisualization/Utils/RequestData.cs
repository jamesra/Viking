using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ConnectomeViz
{
    public class GraphRequestData
    {
        public int NumHops = 1;
        public bool RefreshGraph = false;
        public bool ReduceEdges = true;
        public bool ShowExtraHop = false;
        public bool PinNodePosition = false;
        public long[] CellIDs;

        public string Server;
        public string Volume;

        private static bool IsCheckboxSet(string val)
        {
            return !String.IsNullOrEmpty(val);
        }

        private static long[] GetRequestCellIDs(HttpRequestBase Request)
        {
            if (!String.IsNullOrEmpty(Request["ctl00$MainContent$NetworkInterface$structureID"]) && Int32.Parse(Request["ctl00$MainContent$NetworkInterface$NetworkInterface$structureID"]) != 0)
            {
                return new long[] { Convert.ToInt64(Request["ctl00$MainContent$structureID"]) };
            }
            else
            {
                return new long[] { Convert.ToInt64(Request["cellID"]) };
            }
        }

        public static GraphRequestData Create(HttpRequestBase Request)
        {
            GraphRequestData data = new GraphRequestData();
            data.NumHops = Convert.ToInt32(Request["hops"]);
            data.RefreshGraph = IsCheckboxSet(Request["freshQuery"]);
            data.ReduceEdges = IsCheckboxSet(Request["reduceEdges"]);
            data.ShowExtraHop = IsCheckboxSet(Request["showExtraHop"]);
            data.PinNodePosition = IsCheckboxSet(Request["pinNodes"]);
            data.CellIDs = GetRequestCellIDs(Request);

            data.Volume = Request["ctl00$MainContent$NetworkInterface$EndpointSelector$volumeList"];
            data.Server = Request["ctl00$MainContent$NetworkInterface$EndpointSelector$ServerList"];

            return data;
        }

        public string FileName
        {
            get
            {
                string FileName = Server + "_" + Volume + "_" + CellIDs[0] + "_Hops" + NumHops.ToString();

                if (ReduceEdges)
                {
                    FileName += "_EdgeMerge";
                }

                if (PinNodePosition)
                {
                    FileName += "_WithPos";
                }

                if (ShowExtraHop)
                {
                    FileName += "_ShowGhost";
                }

                return FileName;
            }
        }
    }

    public class GraphRequestPathData
    {
        string WorkingDirectory;
        string ApplicationPath;
        string VirtualRoot;

        /// <summary>
        /// Specific user's files
        /// </summary>
        string UserOutputPath;

        /// <summary>
        /// Path for files shared by all users
        /// </summary>
        string GlobalPath;


        public static GraphRequestPathData Create(HttpContextBase Context, HttpServerUtilityBase Server)
        {
            GraphRequestPathData data = new GraphRequestPathData();

            data.WorkingDirectory = Server.MapPath("~");
            data.ApplicationPath = Context.Request.ApplicationPath;
            if (data.ApplicationPath == "/")
                data.ApplicationPath = "";

            data.VirtualRoot = "http://" + Context.Request.Url.Authority + data.ApplicationPath;
            data.UserOutputPath = data.WorkingDirectory + "\\Files\\" + Context.User.Identity.Name + "\\";

            data.GlobalPath = data.ApplicationPath + "\\Files\\Global" + "\\";

            if (!System.IO.Directory.Exists(data.UserOutputPath))
            {
                System.IO.Directory.CreateDirectory(data.UserOutputPath);
            }

            if (!System.IO.Directory.Exists(data.GlobalPath))
            {
                System.IO.Directory.CreateDirectory(data.GlobalPath);
            }

            return data;
        }

        public string UserFileFullPath(string filename, string ext)
        {
            return UserOutputPath + "\\" + filename + "." + ext;
        }

        public string GlobalFileFullPath(string filename, string ext)
        {
            return GlobalPath + "\\" + filename + "." + ext;
        }
    }
}