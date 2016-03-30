using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ConnectomeODataV4
{
    public class OptionsModule : IHttpModule
    {
        public void Init(HttpApplication context)
        {
            context.BeginRequest += (sender, args) =>
            {
                var app = (HttpApplication)sender;

                if (app.Request.HttpMethod == "OPTIONS")
                {
                    app.Response.StatusCode = 200;
                    //app.Response.AddHeader("Access-Control-Allow-Headers", "content-type, OData-Version");
                    //app.Response.AddHeader("Access-Control-Allow-Origin", "*");
                    //app.Response.AddHeader("Access-Control-Allow-Credentials", "true");
                    //app.Response.AddHeader("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
                    //app.Response.AddHeader("Content-Type", "application/json");
                    try
                    {
                        app.Response.End();
                    }
                    catch (System.Threading.ThreadAbortException)
                    { }
                    
                }
                /*
                else
                {
                    app.Response.AddHeader("Access-Control-Allow-Headers", "content-type, OData-Version");
                    app.Response.AddHeader("Access-Control-Allow-Origin", "*");
                    app.Response.AddHeader("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
                }*/
            };
        }

        public void Dispose()
        {
        }
    }
}