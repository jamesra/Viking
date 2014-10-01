using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using TweetSharp;
using System.Diagnostics;
using System.Configuration;

namespace ConnectomeViz.Controllers
{
    public class TwitterController : Controller
    {
        //
        // GET: /Twitter/
       
        public ActionResult Authorize()
        {
          
        // OAuth Access Token Exchange
        TwitterService service = new TwitterService("consumerKey", "consumerSecret");
        OAuthAccessToken access = service.GetAccessTokenWithXAuth("username", "password"); // 

        return View();
        }

      
    }
}
