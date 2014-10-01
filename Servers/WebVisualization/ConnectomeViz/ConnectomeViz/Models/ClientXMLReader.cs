using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.IO;
using System.Text;

namespace ConnectomeViz.Models
{
    public class ClientXMLReader : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            HttpContextBase httpContext = filterContext.HttpContext;
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

            base.OnActionExecuting(filterContext);
        }
    }
}
