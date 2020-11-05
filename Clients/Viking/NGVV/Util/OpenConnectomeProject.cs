using System;
using System.Net;


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
            catch (WebException e)
            {
                return new string[] { e.Message };
            }
            catch (Newtonsoft.Json.JsonReaderException e)
            {
                return new string[] { e.Message };
            }
        }
    }
}
