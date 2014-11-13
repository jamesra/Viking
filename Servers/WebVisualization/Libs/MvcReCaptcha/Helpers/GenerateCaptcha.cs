using System.IO;
using Recaptcha;
using System.Web.UI;
using System.Web.Mvc;
using System.Configuration;

namespace MvcReCaptcha.Helpers
{
    /// <summary>
    /// 
    /// </summary>
    public static class ReCaptchaHelper
    {
        /// <summary>
        /// Html Helper to build and render the Captcha control
        /// </summary>
        /// <param name="helper">HtmlHelper class provides a set of helper methods whose purpose is to help you create HTML controls programmatically</param>
        /// <returns></returns>
        public static string GenerateCaptcha(this HtmlHelper helper)
        {
            var captchaControl = new RecaptchaControl
                                     {
                                         ID = "recaptcha",
                                         Theme = "clean", //http://wiki.recaptcha.net/index.php/Theme
                                         PublicKey = ConfigurationManager.AppSettings["ReCaptchaPublicKey"],
                                         PrivateKey = ConfigurationManager.AppSettings["ReCaptchaPrivateKey"]
                                     };
            var htmlWriter = new HtmlTextWriter(new StringWriter());
            captchaControl.RenderControl(htmlWriter);
            return htmlWriter.InnerWriter.ToString();
        }
    }
}    