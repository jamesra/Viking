using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using Newtonsoft.Json.Serialization;
using System.Linq;


namespace Viking.Common
{
    public static class OCPVolumes
    {
        public static string[] ReadServer(Uri OCPServerURL)
        {
            WebClient client = new WebClient();
            try
            {
                string responseString = client.DownloadString(OCPServerURL);
                string[] array = Newtonsoft.Json.JsonConvert.DeserializeObject<string[]>(responseString);
                return array;
            }
            catch(WebException e)
            {
                return new string[] { e.Message };
            } 
        }
    }
}
